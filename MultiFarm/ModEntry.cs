using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MultiFarm
{
    /// <summary>
    /// Mod entry point. Wires up SMAPI events and coordinates all subsystems.
    /// </summary>
    public class ModEntry : Mod
    {
        // ── Multiplayer message type IDs ──────────────────────────────────────
        /// <summary>Host → client: "you need to pick a farm type".</summary>
        public const string MsgNeedsFarmSelection = "MultiFarm.NeedsFarmSelection";

        /// <summary>Client → host: "I chose this farm type".</summary>
        public const string MsgFarmChosen = "MultiFarm.FarmChosen";

        /// <summary>Host → all clients: current assignment table.</summary>
        public const string MsgSyncAssignments = "MultiFarm.SyncAssignments";

        // ── Properties ───────────────────────────────────────────────────────
        internal static ModEntry Instance { get; private set; } = null!;
        internal ModConfig Config { get; private set; } = null!;
        internal FarmHubManager HubManager { get; private set; } = null!;
        internal PlayerFarmManager FarmManager { get; private set; } = null!;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();

            HubManager  = new FarmHubManager(helper, Monitor);
            FarmManager = new PlayerFarmManager(helper, Monitor);

            new Harmony(ModManifest.UniqueID).PatchAll();

            helper.Events.GameLoop.GameLaunched         += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded           += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted           += OnDayStarted;
            helper.Events.GameLoop.Saving               += OnSaving;
            helper.Events.Player.Warped                 += OnWarped;
            helper.Events.Multiplayer.PeerConnected     += OnPeerConnected;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            helper.Events.Display.MenuChanged           += OnMenuChanged;
            helper.Events.Display.RenderedWorld         += OnRenderedWorld;

            // Console commands for debugging
            helper.ConsoleCommands.Add("mf_status",  "Show MultiFarm status.",                          CmdStatus);
            helper.ConsoleCommands.Add("mf_assign",  "Assign farm: mf_assign <player> <slot 1-8>.",     CmdAssign);
            helper.ConsoleCommands.Add("mf_goto",    "Warp to a farm slot: mf_goto <slot 1-8>.",        CmdGoto);
            helper.ConsoleCommands.Add("mf_selectfarm", "Open farm-selection menu for local player.",   CmdSelectFarm);
        }

        // ── Event handlers ───────────────────────────────────────────────────
        // Hub map loading and vanilla warp redirects are handled by the
        // [MultiFarm] Content CP pack (content.json). Only the Farm↔BusStop
        // redirect lives here, via WarpInterceptPatch (Harmony prefix).

        // ── Game loop ─────────────────────────────────────────────────────────

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            HubManager.RegisterLocations();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            HubManager.RegisterLocations();
            FarmManager.LoadAssignments();

            // Auto-assign host to slot 1 (vanilla Farm) if not yet assigned.
            if (Context.IsMainPlayer && FarmManager.GetSlotForPlayer(Game1.player.Name) == 0)
                FarmManager.AssignFarm(Game1.player.Name, 1, 0);

            FarmManager.EnsurePlayerFarmsExist();

            // Prompt any unassigned player to choose their farm type.
            if (FarmManager.GetSlotForPlayer(Game1.player.Name) == 0)
                FarmManager.ShowFarmSelectionMenu(Game1.player);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // Re-register defensively — map reloads can reset locations.
            HubManager.RegisterLocations();
            FarmManager.EnsurePlayerFarmsExist();

            // Redirect married NPC home locations to the spouse's private farmhouse.
            // The schedule-string side is handled by NpcSchedulePatch (Harmony prefix).
            // Here we fix DefaultMap/DefaultPosition so the NPC warps to the right
            // location at the end of the night.  Only the host has authority to do this.
            if (!Context.IsMainPlayer) return;
            foreach (var farmer in Game1.getAllFarmers())
            {
                if (string.IsNullOrEmpty(farmer.spouse)) continue;
                int slot = FarmManager.GetSlotForPlayer(farmer.Name);
                if (slot == 0) continue;

                var npc = Game1.getCharacterFromName(farmer.spouse);
                if (npc is null) continue;

                string hubHouse = PlayerFarmManager.FarmHouseName(slot);
                if (npc.DefaultMap != hubHouse)
                {
                    npc.DefaultMap      = hubHouse;
                    npc.DefaultPosition = new Vector2(3, 11) * Game1.tileSize;
                    Monitor.Log(
                        $"Redirected {npc.Name}'s home → {hubHouse}.",
                        LogLevel.Debug);
                }
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            FarmManager.SaveAssignments();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!Config.ReplaceVanillaWarps) return;

            // Note: vanilla Farm↔BusStop/Backwoods/Forest warp interception is now
            // handled upstream without double-warp:
            //   Farm↔BusStop     → WarpInterceptPatch (Harmony prefix on Game1.warpFarmer)
            //   Farm↔Backwoods   → OnAssetRequested edit on Maps/Farm + Maps/Backwoods
            //   Farm↔Forest      → OnAssetRequested edit on Maps/Farm + Maps/Forest

            if (FarmHubManager.IsHubLocation(e.NewLocation?.Name))
                HubManager.OnPlayerEnterHub(e.Player, e.NewLocation!.Name);

            // Hub portal → player farm: re-warp to correct spawn position based on which hub
            // the player came from. The TMX warp uses a placeholder destination; OnWarped
            // immediately corrects it so the player lands at the right edge of their farm.
            if (e.Player.IsLocalPlayer && FarmHubManager.IsHubLocation(e.OldLocation?.Name))
            {
                string farmName = e.NewLocation?.Name ?? "";
                bool isPlayerFarm = farmName == "Farm" ||
                                    farmName.StartsWith(PlayerFarmManager.FarmPrefix);
                if (isPlayerFarm)
                {
                    int slot = FarmManager.GetSlotForPlayer(e.Player.UniqueMultiplayerID);
                    if (slot > 0)
                    {
                        var (rx, ry, rfacing) =
                            FarmManager.GetHubArrivalOnFarm(slot, e.OldLocation!.Name);
                        Game1.warpFarmer(farmName, rx, ry, rfacing);
                        return;
                    }
                }
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            // Draw player names above their portal tiles when inside any hub.
            if (!Context.IsWorldReady) return;
            if (!FarmHubManager.IsHubLocation(Game1.currentLocation?.Name)) return;

            SpriteBatch b = e.SpriteBatch;
            string currentHub = Game1.currentLocation?.Name ?? "";
            foreach (var (slot, name) in FarmManager.GetAssignments())
            {
                var portal = FarmHubManager.GetSlotWarpTile(slot, currentHub);
                if (portal == Point.Zero) continue;

                // Center the label over the portal tile (one tile above it)
                Vector2 measure  = Game1.smallFont.MeasureString(name);
                float   screenX  = portal.X * Game1.tileSize - Game1.viewport.X
                                   + (Game1.tileSize - measure.X) / 2f;
                float   screenY  = (portal.Y - 1) * Game1.tileSize - Game1.viewport.Y;

                Utility.drawTextWithShadow(b, name, Game1.smallFont,
                    new Vector2(screenX, screenY), Color.White);
            }
        }

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            FarmManager.OnPeerConnected(e.Peer);
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID) return;

            switch (e.Type)
            {
                case MsgNeedsFarmSelection:
                    // We are a client and the host wants us to pick a farm type
                    FarmManager.ShowFarmSelectionMenu(Game1.player);
                    break;

                case MsgFarmChosen:
                    // We are the host and a client sent their farm-type choice
                    if (Game1.IsMasterGame)
                    {
                        var payload = e.ReadAs<PlayerFarmManager.FarmChosenPayload>();
                        FarmManager.OnRemoteFarmTypeChosen(e.FromPlayerID, payload.FarmType);
                    }
                    break;

                case MsgSyncAssignments:
                    // Host sent the current assignment table
                    var sync = e.ReadAs<PlayerFarmManager.SyncPayload>();
                    FarmManager.OnSyncAssignments(sync);
                    break;
            }
        }

        // ── Console commands ─────────────────────────────────────────────────

        private void CmdStatus(string cmd, string[] args)
        {
            Monitor.Log("=== MultiFarm Status ===", LogLevel.Info);
            Monitor.Log($"Hub registered: {HubManager.IsRegistered}", LogLevel.Info);
            foreach (var (slot, name) in FarmManager.GetAssignments())
            {
                int type = FarmManager.GetFarmTypeForPlayer(name);
                Monitor.Log($"  Slot {slot}: {name} (farm type {type})", LogLevel.Info);
            }
        }

        private void CmdAssign(string cmd, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int slot)
                || slot < 1 || slot > Config.MaxPlayers)
            {
                Monitor.Log($"Usage: mf_assign <playerName> <slot 1-{Config.MaxPlayers}>", LogLevel.Warn);
                return;
            }
            int farmType = args.Length >= 3 && int.TryParse(args[2], out int t) ? t : 0;
            FarmManager.AssignFarm(args[0], slot, farmType);
            Monitor.Log($"Assigned {args[0]} → Slot {slot} (type {farmType})", LogLevel.Info);
        }

        private void CmdGoto(string cmd, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int slot)
                || slot < 1 || slot > Config.MaxPlayers)
            {
                Monitor.Log($"Usage: mf_goto <slot 1-{Config.MaxPlayers}>", LogLevel.Warn);
                return;
            }
            FarmManager.WarpToFarm(Game1.player, slot);
        }

        private void CmdSelectFarm(string cmd, string[] args)
        {
            FarmManager.ShowFarmSelectionMenu(Game1.player);
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (e.NewMenu is null) return;

            // Redirect the carpenter / building-placement menu to use the player's own farm.
            // This prevents players from accidentally (or intentionally) placing buildings
            // on another player's farm by ensuring the menu targets their MultiFarm_Farm_N.
            string menuTypeName = e.NewMenu.GetType().Name;
            if (!menuTypeName.Contains("Carpenter") && !menuTypeName.Contains("BuildingPlacement"))
                return;

            int slot = FarmManager.GetSlotForPlayer(Game1.player.Name);
            if (slot == 0) return;

            var playerFarm = Game1.getLocationFromName(PlayerFarmManager.FarmName(slot)) as Farm;
            if (playerFarm is null) return;

            // The field name holding the target Farm varies by SDV version; try known names.
            string[] fieldNames = { "buildingLocation", "TargetFarm", "farm", "_farm" };
            foreach (var fieldName in fieldNames)
            {
                try
                {
                    var field = Helper.Reflection.GetField<Farm>(e.NewMenu, fieldName, required: false);
                    if (field is not null)
                    {
                        field.SetValue(playerFarm);
                        Monitor.Log(
                            $"Redirected carpenter menu → {playerFarm.Name} (field '{fieldName}').",
                            LogLevel.Debug);
                        return;
                    }
                }
                catch { /* try next name */ }
            }

            Monitor.Log(
                "Could not find carpenter menu farm field — building restriction not applied. " +
                "Check the field name for your SDV version.",
                LogLevel.Warn);
        }
    }
}
