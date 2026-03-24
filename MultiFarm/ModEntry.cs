using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;

namespace MultiFarm
{
    /// <summary>
    /// Mod entry point. Wires up SMAPI events and coordinates all subsystems.
    /// </summary>
    public class ModEntry : Mod
    {
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

            // Location injection — register the hub and all player farms
            helper.Events.GameLoop.GameLaunched    += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded      += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted      += OnDayStarted;
            helper.Events.GameLoop.Saving          += OnSaving;
            helper.Events.Player.Warped            += OnWarped;
            helper.Events.Multiplayer.PeerConnected += OnPeerConnected;

            // Console commands for debugging
            helper.ConsoleCommands.Add("mf_status",  "Show MultiFarm status.",        CmdStatus);
            helper.ConsoleCommands.Add("mf_assign",  "Assign farm: mf_assign <player> <slot 1-8>.", CmdAssign);
            helper.ConsoleCommands.Add("mf_goto",    "Warp to a farm slot: mf_goto <slot 1-8>.",   CmdGoto);
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            HubManager.RegisterLocations();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            FarmManager.LoadAssignments();
            HubManager.PatchVanillaWarps();
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
            // When a player enters the hub, show their farm path highlight
            if (e.NewLocation?.Name == FarmHubManager.HubLocationName)
                HubManager.OnPlayerEnterHub(e.Player);
        }

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            // When a new player joins, ensure they have a farm and offer selection
            FarmManager.OnPeerConnected(e.Peer);
        }

        // ── Console commands ─────────────────────────────────────────────────

        private void CmdStatus(string cmd, string[] args)
        {
            Monitor.Log("=== MultiFarm Status ===", LogLevel.Info);
            Monitor.Log($"Hub registered: {HubManager.IsRegistered}", LogLevel.Info);
            foreach (var (slot, name) in FarmManager.GetAssignments())
                Monitor.Log($"  Slot {slot}: {name}", LogLevel.Info);
        }

        private void CmdAssign(string cmd, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int slot) || slot < 1 || slot > Config.MaxPlayers)
            {
                Monitor.Log($"Usage: mf_assign <playerName> <slot 1-{Config.MaxPlayers}>", LogLevel.Warn);
                return;
            }
            FarmManager.AssignFarm(args[0], slot);
            Monitor.Log($"Assigned {args[0]} → Slot {slot}", LogLevel.Info);
        }

        private void CmdGoto(string cmd, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int slot) || slot < 1 || slot > Config.MaxPlayers)
            {
                Monitor.Log($"Usage: mf_goto <slot 1-{Config.MaxPlayers}>", LogLevel.Warn);
                return;
            }
            FarmManager.WarpToFarm(Game1.player, slot);
        }
    }
}
