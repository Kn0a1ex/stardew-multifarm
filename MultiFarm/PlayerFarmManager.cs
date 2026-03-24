using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MultiFarm
{
    /// <summary>
    /// Manages per-player farm assignments and the private GameLocation instances.
    ///
    /// Slot numbering: 1–8 (0 = unassigned).
    /// Each slot corresponds to:
    ///   - A unique location name: "MultiFarm_Player1" … "MultiFarm_Player8"
    ///   - A map file:            assets/maps/PlayerFarm.tmx (shared base map)
    ///   - A save data entry:     persisted in data/assignments.json in the mod folder
    /// </summary>
    public class PlayerFarmManager
    {
        private const string LocationPrefix    = "MultiFarm_Player";
        private const string AssignmentsFile   = "data/assignments.json";

        private readonly IModHelper _helper;
        private readonly IMonitor  _monitor;

        // slot → player display name
        private Dictionary<int, string> _assignments = new();

        // player display name → farm type ID chosen at setup
        private Dictionary<string, int> _farmTypes = new();

        public PlayerFarmManager(IModHelper helper, IMonitor monitor)
        {
            _helper  = helper;
            _monitor = monitor;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Assign a player to a farm slot. Overwrites any existing assignment for that slot.</summary>
        public void AssignFarm(string playerName, int slot)
        {
            if (slot < 1 || slot > ModEntry.Instance.Config.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(slot));

            // Remove any previous slot held by this player
            var existing = _assignments.FirstOrDefault(kv => kv.Value == playerName);
            if (existing.Value != null)
                _assignments.Remove(existing.Key);

            _assignments[slot] = playerName;
            EnsurePlayerFarmsExist();
        }

        /// <summary>Returns the slot number for a player, or 0 if unassigned.</summary>
        public int GetSlotForPlayer(string playerName)
        {
            foreach (var kv in _assignments)
                if (kv.Value == playerName) return kv.Key;
            return 0;
        }

        /// <summary>Returns all current (slot, playerName) assignments.</summary>
        public IEnumerable<(int slot, string name)> GetAssignments()
            => _assignments.Select(kv => (kv.Key, kv.Value));

        /// <summary>
        /// Called when a new peer connects. If they don't have a slot yet,
        /// trigger the farm selection UI.
        /// </summary>
        public void OnPeerConnected(IMultiplayerPeer peer)
        {
            // Resolve display name from connected peer
            var player = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == peer.PlayerID);
            if (player is null) return;

            if (GetSlotForPlayer(player.Name) == 0)
            {
                _monitor.Log($"{player.Name} has no farm assigned — prompting selection.", LogLevel.Info);
                PromptFarmSelection(player);
            }
        }

        /// <summary>Warp a farmer directly to a specific slot's farm location.</summary>
        public void WarpToFarm(Farmer farmer, int slot)
        {
            string locName = LocationName(slot);
            var loc = Game1.getLocationFromName(locName);
            if (loc is null)
            {
                _monitor.Log($"Farm location '{locName}' not found.", LogLevel.Warn);
                return;
            }
            Game1.warpFarmer(locName, 32, 10, facingDirectionAfterWarp: 2);
        }

        /// <summary>
        /// Ensure every assigned slot has a registered GameLocation in the world.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public void EnsurePlayerFarmsExist()
        {
            int maxSlots = ModEntry.Instance.Config.MaxPlayers;
            for (int slot = 1; slot <= maxSlots; slot++)
            {
                string locName = LocationName(slot);
                if (Game1.getLocationFromName(locName) is null)
                    RegisterFarmLocation(slot);
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public void LoadAssignments()
        {
            try
            {
                var data = _helper.Data.ReadJsonFile<AssignmentData>(AssignmentsFile);
                if (data is not null)
                {
                    _assignments = data.Assignments ?? new();
                    _farmTypes   = data.FarmTypes   ?? new();
                    _monitor.Log($"Loaded {_assignments.Count} farm assignment(s).", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not load assignments: {ex.Message}", LogLevel.Warn);
            }
        }

        public void SaveAssignments()
        {
            try
            {
                _helper.Data.WriteJsonFile(AssignmentsFile, new AssignmentData
                {
                    Assignments = _assignments,
                    FarmTypes   = _farmTypes,
                });
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not save assignments: {ex.Message}", LogLevel.Warn);
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static string LocationName(int slot) => $"{LocationPrefix}{slot}";

        private void RegisterFarmLocation(int slot)
        {
            try
            {
                string mapPath = _helper.ModContent
                    .GetInternalAssetName("assets/maps/PlayerFarm.tmx").Name;

                var farmLoc = new Farm(mapPath, LocationName(slot));
                Game1.locations.Add(farmLoc);

                // Add hub return warp
                var returnTile = FarmHubManager.GetFarmReturnTile();
                var hubEntry   = FarmHubManager.GetSlotWarpTile(slot);
                farmLoc.warps.Add(new Warp(
                    returnTile.X, returnTile.Y,
                    FarmHubManager.HubLocationName,
                    hubEntry.X, hubEntry.Y,
                    flipFarmer: false
                ));

                _monitor.Log($"Registered farm location for slot {slot}: '{LocationName(slot)}'.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to register farm slot {slot}: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Show the farm type selection menu to a player.
        /// Assigns them the first available slot.
        /// TODO: Replace with a proper in-game selection UI (NamingMenu or custom).
        /// </summary>
        private void PromptFarmSelection(Farmer player)
        {
            // Find first open slot
            int openSlot = 0;
            for (int i = 1; i <= ModEntry.Instance.Config.MaxPlayers; i++)
            {
                if (!_assignments.ContainsKey(i)) { openSlot = i; break; }
            }

            if (openSlot == 0)
            {
                _monitor.Log("All farm slots are full!", LogLevel.Warn);
                Game1.chatBox?.addMessage($"MultiFarm: All {ModEntry.Instance.Config.MaxPlayers} farm slots are taken.", Microsoft.Xna.Framework.Color.Red);
                return;
            }

            // Auto-assign for now; a proper UI picker is the next step
            AssignFarm(player.Name, openSlot);
            Game1.chatBox?.addMessage(
                $"MultiFarm: You've been assigned Farm {openSlot}. Head to the Farm Hub to find your path!",
                Microsoft.Xna.Framework.Color.Green
            );

            _monitor.Log($"Auto-assigned {player.Name} to slot {openSlot}.", LogLevel.Info);
        }

        // ── Serialisation data class ─────────────────────────────────────────

        private class AssignmentData
        {
            public Dictionary<int, string>? Assignments { get; set; }
            public Dictionary<string, int>? FarmTypes   { get; set; }
        }
    }
}
