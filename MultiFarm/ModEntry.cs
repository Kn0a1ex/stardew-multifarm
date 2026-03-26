using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using System;

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

        /// <summary>Host → client: all farm slots are taken.</summary>
        public const string MsgSlotFull = "MultiFarm.SlotFull";

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
            HideMultiplayerOptionsPatch.Initialize(Monitor);

            helper.Events.Content.AssetRequested        += OnAssetRequested;
            helper.Events.GameLoop.GameLaunched         += OnGameLaunched;
            helper.Events.GameLoop.SaveCreated          += OnSaveCreated;
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

        // ── Asset pipeline ────────────────────────────────────────────────────

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!Config.ReplaceVanillaWarps) return;

            // Redirect vanilla map Warp properties so players route through the hubs.
            // CP TextOperations can't replace multi-token warp groups, so this stays in C#.
            // Hub location registration is handled by CP's CustomLocations block.
            // Farm↔BusStop is intercepted by WarpInterceptPatch (Harmony prefix).

            if (e.NameWithoutLocale.IsEquivalentTo("Maps/Backwoods"))
                e.Edit(asset => RedirectMapWarps(asset, "Farm",
                    FarmHubManager.HubNameBackwoods,
                    FarmHubManager.HubBackwoodsEntryFromBackwoods.X,
                    FarmHubManager.HubBackwoodsEntryFromBackwoods.Y), AssetEditPriority.Default);

            if (e.NameWithoutLocale.IsEquivalentTo("Maps/Forest"))
                e.Edit(asset => RedirectMapWarps(asset, "Farm",
                    FarmHubManager.HubNameForest,
                    FarmHubManager.HubForestEntryFromForest.X,
                    FarmHubManager.HubForestEntryFromForest.Y), AssetEditPriority.Default);

            if (e.NameWithoutLocale.IsEquivalentTo("Maps/Farm"))
                e.Edit(asset =>
                {
                    RedirectMapWarps(asset, "Backwoods",
                        FarmHubManager.HubNameBackwoods,
                        FarmHubManager.HubBackwoodsEntryFromFarm.X,
                        FarmHubManager.HubBackwoodsEntryFromFarm.Y);
                    RedirectMapWarps(asset, "Forest",
                        FarmHubManager.HubNameForest,
                        FarmHubManager.HubForestEntryFromFarm.X,
                        FarmHubManager.HubForestEntryFromFarm.Y);
                }, AssetEditPriority.Default);
        }

        private static void RedirectMapWarps(IAssetData asset,
            string fromTarget, string toTarget, int toX, int toY)
        {
            var map = asset.AsMap().Data;
            if (!map.Properties.TryGetValue("Warp", out var warpProp)) return;

            string[] tokens = warpProp.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var result = new System.Text.StringBuilder();
            for (int i = 0; i + 5 <= tokens.Length; i += 5)
            {
                if (tokens[i + 2].Equals(fromTarget, StringComparison.OrdinalIgnoreCase))
                {
                    tokens[i + 2] = toTarget;
                    tokens[i + 3] = toX.ToString();
                    tokens[i + 4] = toY.ToString();
                }
                if (result.Length > 0) result.Append(' ');
                result.Append(
                    $"{tokens[i]} {tokens[i+1]} {tokens[i+2]} {tokens[i+3]} {tokens[i+4]}");
            }

            map.Properties["Warp"] = result.ToString();
        }

        // ── Game loop ─────────────────────────────────────────────────────────

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            HubManager.RegisterLocations();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            HubManager.RegisterLocations();
            FarmManager.LoadAssignments();

            // Always re-assign host to slot 1 on load so the correct farm type is stored.
            // Game1.whichFarm is only available after save load, not at mod entry.
            if (Context.IsMainPlayer)
            {
                Monitor.Log($"Host farm type: Game1.whichFarm = {(int)Game1.whichFarm}", LogLevel.Info);
                FarmManager.AssignFarm(Game1.player.Name, 1, (int)Game1.whichFarm,
                    Game1.player.farmName?.Value ?? "");
                Game1.player.team.useSeparateWallets.Value = true;
                BuryCabins();
            }

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

        private void OnSaveCreated(object? sender, SaveCreatedEventArgs e)
        {
            Game1.player.team.useSeparateWallets.Value = true;
            BuryCabins();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            FarmManager.SaveAssignments();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!Config.ReplaceVanillaWarps) return;

            if (FarmHubManager.IsHubLocation(e.NewLocation?.Name))
                HubManager.OnPlayerEnterHub(e.Player, e.NewLocation!.Name);

            // Hub→farm arrival position is now corrected in WarpInterceptPatch before
            // the warp fires, so no re-warp is needed here.
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            // Draw player names above their portal tiles when inside any hub.
            if (!Context.IsWorldReady) return;
            if (!FarmHubManager.IsHubLocation(Game1.currentLocation?.Name)) return;

            SpriteBatch b = e.SpriteBatch;
            string currentHub = Game1.currentLocation?.Name ?? "";
            foreach (var (slot, _) in FarmManager.GetAssignments())
            {
                var portal = FarmHubManager.GetSlotWarpTile(slot, currentHub);
                if (portal == Point.Zero) continue;

                string label = FarmManager.GetFarmDisplayLabel(slot);
                if (string.IsNullOrEmpty(label)) continue;

                // Center the label over the portal tile (one tile above it)
                Vector2 measure  = Game1.smallFont.MeasureString(label);
                float   screenX  = portal.X * Game1.tileSize - Game1.viewport.X
                                   + (Game1.tileSize - measure.X) / 2f;
                float   screenY  = (portal.Y - 1) * Game1.tileSize - Game1.viewport.Y;

                Utility.drawTextWithShadow(b, label, Game1.smallFont,
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
                    // We are the host and a client sent their farm-type and name choice
                    if (Game1.IsMasterGame)
                    {
                        var payload = e.ReadAs<PlayerFarmManager.FarmChosenPayload>();
                        FarmManager.OnRemoteFarmTypeChosen(e.FromPlayerID, payload.FarmType, payload.FarmName);
                    }
                    break;

                case MsgSyncAssignments:
                    // Host sent the current assignment table
                    var sync = e.ReadAs<PlayerFarmManager.SyncPayload>();
                    FarmManager.OnSyncAssignments(sync);
                    break;

                case MsgSlotFull:
                    var msg = e.ReadAs<string>();
                    Game1.chatBox?.addMessage(msg, Color.Red);
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

        private void BuryCabins()
        {
            if (!Context.IsMainPlayer) return;
            var farm = Game1.getFarm();
            if (farm is null) return;
            int count = 0;
            foreach (var building in farm.buildings)
            {
                if (building.buildingType.Value == "Cabin")
                {
                    building.tileX.Value = -1000;
                    building.tileY.Value = -1000;
                    count++;
                }
            }
            if (count > 0)
                Monitor.Log($"BuryCabins: moved {count} cabin(s) off-map.", LogLevel.Debug);
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is null) return;
            if (!Context.IsWorldReady) return;

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
