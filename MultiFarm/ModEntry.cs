using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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

            helper.Events.GameLoop.GameLaunched      += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded        += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted        += OnDayStarted;
            helper.Events.GameLoop.Saving            += OnSaving;
            helper.Events.Player.Warped              += OnWarped;
            helper.Events.Multiplayer.PeerConnected  += OnPeerConnected;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            helper.Events.Display.MenuChanged        += OnMenuChanged;

            // Console commands for debugging
            helper.ConsoleCommands.Add("mf_status",  "Show MultiFarm status.",                          CmdStatus);
            helper.ConsoleCommands.Add("mf_assign",  "Assign farm: mf_assign <player> <slot 1-8>.",     CmdAssign);
            helper.ConsoleCommands.Add("mf_goto",    "Warp to a farm slot: mf_goto <slot 1-8>.",        CmdGoto);
            helper.ConsoleCommands.Add("mf_selectfarm", "Open farm-selection menu for local player.",   CmdSelectFarm);
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            HubManager.RegisterLocations();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            FarmManager.LoadAssignments();
            FarmManager.EnsurePlayerFarmsExist();
            HubManager.PatchVanillaWarps();

            // If the local player has no slot yet, show the farm-selection menu
            if (FarmManager.GetSlotForPlayer(Game1.player.Name) == 0)
                FarmManager.ShowFarmSelectionMenu(Game1.player);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            FarmManager.EnsurePlayerFarmsExist();
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            FarmManager.SaveAssignments();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (e.NewLocation?.Name == FarmHubManager.HubLocationName)
                HubManager.OnPlayerEnterHub(e.Player);
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
