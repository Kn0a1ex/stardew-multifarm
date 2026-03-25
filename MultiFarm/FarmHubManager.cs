using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the three Farm Hub locations.
    ///
    ///   Farm Hub      — vanilla Farm (west) ↔ BusStop (east); portals on west side
    ///   Backwoods Hub — Backwoods (north) / vanilla Farm mountain trail (south); portals south side
    ///   Forest Hub    — Forest (south) / vanilla Farm south edge (north); portals north side
    ///
    /// Portal positions per hub (all hubs 44×24):
    ///   Farm Hub      y=5,  x=3,6,9,12,15,18,21,24  hub-arrivals y=7
    ///   Backwoods Hub y=18, x=4,9,14,19,24,29,34,39  hub-arrivals y=20
    ///   Forest Hub    y=5,  x=4,9,14,19,24,29,34,39  hub-arrivals y=7
    ///
    /// Slot 1 = vanilla "Farm" (host farm). Slots 2-8 = MultiFarm_Farm_N.
    /// </summary>
    public class FarmHubManager
    {
        // ── Location names ────────────────────────────────────────────────────
        public const string HubNameFarm      = "MultiFarm_Hub_Farm";
        public const string HubNameBackwoods = "MultiFarm_Hub_Backwoods";
        public const string HubNameForest    = "MultiFarm_Hub_Forest";

        // ── Per-hub portal positions ──────────────────────────────────────────
        // Farm Hub: west side, y=5, spacing 3
        private static readonly Dictionary<int, Point> SlotWarpTilesFarm = new()
        {
            { 1, new Point( 3, 5) }, { 2, new Point( 6, 5) },
            { 3, new Point( 9, 5) }, { 4, new Point(12, 5) },
            { 5, new Point(15, 5) }, { 6, new Point(18, 5) },
            { 7, new Point(21, 5) }, { 8, new Point(24, 5) },
        };

        // Backwoods Hub: south side, y=18, spacing 5
        private static readonly Dictionary<int, Point> SlotWarpTilesBackwoods = new()
        {
            { 1, new Point( 4, 18) }, { 2, new Point( 9, 18) },
            { 3, new Point(14, 18) }, { 4, new Point(19, 18) },
            { 5, new Point(24, 18) }, { 6, new Point(29, 18) },
            { 7, new Point(34, 18) }, { 8, new Point(39, 18) },
        };

        // Forest Hub: north side, y=5, spacing 5
        private static readonly Dictionary<int, Point> SlotWarpTilesForest = new()
        {
            { 1, new Point( 4, 5) }, { 2, new Point( 9, 5) },
            { 3, new Point(14, 5) }, { 4, new Point(19, 5) },
            { 5, new Point(24, 5) }, { 6, new Point(29, 5) },
            { 7, new Point(34, 5) }, { 8, new Point(39, 5) },
        };

        // ── Hub entrance points ───────────────────────────────────────────────
        // Farm Hub (horizontal spine y=9-11)
        public static readonly Point HubFarmEntryFromFarm    = new( 2, 10);
        public static readonly Point HubFarmEntryFromBusStop = new(41, 10);

        // Backwoods Hub (vertical spine x=20-22)
        public static readonly Point HubBackwoodsEntryFromBackwoods = new(21,  2);
        public static readonly Point HubBackwoodsEntryFromFarm      = new(21, 21);

        // Forest Hub (vertical spine x=20-22)
        public static readonly Point HubForestEntryFromForest = new(21, 21);
        public static readonly Point HubForestEntryFromFarm   = new(21,  2);

        public bool IsRegistered { get; private set; }

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        public FarmHubManager(IModHelper helper, IMonitor monitor)
        {
            _helper  = helper;
            _monitor = monitor;
        }

        public void RegisterLocations()
        {
            bool allPresent =
                Game1.getLocationFromName(HubNameFarm)      is not null &&
                Game1.getLocationFromName(HubNameBackwoods) is not null &&
                Game1.getLocationFromName(HubNameForest)    is not null;

            if (allPresent) { IsRegistered = true; return; }

            foreach (var (name, _) in new[]
            {
                (HubNameFarm,      ""),
                (HubNameBackwoods, ""),
                (HubNameForest,    ""),
            })
            {
                if (Game1.getLocationFromName(name) is not null) continue;
                try
                {
                    var loc = new GameLocation($"Maps/{name}", name)
                    {
                        IsOutdoors   = true,
                        IsFarm       = false,
                        IsGreenhouse = false,
                    };
                    Game1.locations.Add(loc);
                    _monitor.Log($"Registered hub location '{name}'.", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Failed to register hub '{name}': {ex.Message}", LogLevel.Error);
                }
            }

            IsRegistered =
                Game1.getLocationFromName(HubNameFarm)      is not null &&
                Game1.getLocationFromName(HubNameBackwoods) is not null &&
                Game1.getLocationFromName(HubNameForest)    is not null;
        }

        public void PatchVanillaWarps()
        {
            if (!ModEntry.Instance.Config.ReplaceVanillaWarps) return;
            try
            {
                PatchWarp(Game1.getLocationFromName("Backwoods"), "Farm",
                    HubNameBackwoods,
                    HubBackwoodsEntryFromBackwoods.X, HubBackwoodsEntryFromBackwoods.Y);

                PatchWarp(Game1.getLocationFromName("Forest"), "Farm",
                    HubNameForest,
                    HubForestEntryFromForest.X, HubForestEntryFromForest.Y);

                _monitor.Log("Patched vanilla warps.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to patch vanilla warps: {ex.Message}", LogLevel.Warn);
            }
        }

        public void OnPlayerEnterHub(Farmer player, string hubName)
        {
            int slot = ModEntry.Instance.FarmManager.GetSlotForPlayer(player.Name);
            if (slot > 0)
                _monitor.Log($"{player.Name} entered {hubName} (slot {slot}).", LogLevel.Trace);
        }

        public static bool IsHubLocation(string? name) =>
            name == HubNameFarm || name == HubNameBackwoods || name == HubNameForest;

        // ── Slot warp tile accessors ──────────────────────────────────────────

        /// <summary>
        /// Returns the portal tile position for a slot in the given hub.
        /// </summary>
        public static Point GetSlotWarpTile(int slot, string hubName)
        {
            var dict = hubName == HubNameBackwoods ? SlotWarpTilesBackwoods
                     : hubName == HubNameForest    ? SlotWarpTilesForest
                     :                               SlotWarpTilesFarm;
            return dict.TryGetValue(slot, out var pt) ? pt : Point.Zero;
        }

        /// <summary>
        /// Returns the portal tile position for a slot in the Farm Hub (default).
        /// Used for rendering labels — caller should pass hub name when possible.
        /// </summary>
        public static Point GetSlotWarpTile(int slot) =>
            GetSlotWarpTile(slot, HubNameFarm);

        /// <summary>
        /// Returns the hub tile where a player arriving from a player farm should land.
        /// Lands 2 tiles past the portal so they don't immediately re-enter.
        /// </summary>
        public static Point GetHubArrivalForSlot(int slot, string hubName)
        {
            var portal = GetSlotWarpTile(slot, hubName);
            return new Point(portal.X, portal.Y + 2);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void PatchWarp(GameLocation location, string targetLocation,
            string newTarget, int newTargetX, int newTargetY)
        {
            if (location is null) return;
            var warps = location.warps;
            for (int i = 0; i < warps.Count; i++)
            {
                if (warps[i].TargetName.Equals(targetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    int srcX = warps[i].X, srcY = warps[i].Y;
                    warps[i] = new Warp(srcX, srcY, newTarget, newTargetX, newTargetY, false);
                }
            }
        }
    }
}
