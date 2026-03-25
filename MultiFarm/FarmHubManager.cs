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
    /// Slot arrival positions per hub (all hubs 24×20, wall-edge style):
    ///   Farm Hub      x=2,  y=3,5,7,9,11,13,15,17   warp triggers at x=-1
    ///   Backwoods Hub y=17, x=2,4,6,8,10,12,14,16   warp triggers at y=20
    ///   Forest Hub    y=2,  x=2,4,6,8,10,12,14,16   warp triggers at y=-1
    ///
    /// Slot 1 = vanilla "Farm" (host farm). Slots 2-8 = MultiFarm_Farm_N.
    /// </summary>
    public class FarmHubManager
    {
        // ── Location names ────────────────────────────────────────────────────
        public const string HubNameFarm      = "MultiFarm_Hub_Farm";
        public const string HubNameBackwoods = "MultiFarm_Hub_Backwoods";
        public const string HubNameForest    = "MultiFarm_Hub_Forest";

        // ── Per-hub slot arrival positions (wall-edge style) ──────────────────
        // Farm Hub: west wall, x=2, y=3..17 spacing 2
        private static readonly Dictionary<int, Point> SlotWarpTilesFarm = new()
        {
            { 1, new Point(2,  3) }, { 2, new Point(2,  5) },
            { 3, new Point(2,  7) }, { 4, new Point(2,  9) },
            { 5, new Point(2, 11) }, { 6, new Point(2, 13) },
            { 7, new Point(2, 15) }, { 8, new Point(2, 17) },
        };

        // Backwoods Hub: south wall, y=17, x=2..16 spacing 2
        private static readonly Dictionary<int, Point> SlotWarpTilesBackwoods = new()
        {
            { 1, new Point( 2, 17) }, { 2, new Point( 4, 17) },
            { 3, new Point( 6, 17) }, { 4, new Point( 8, 17) },
            { 5, new Point(10, 17) }, { 6, new Point(12, 17) },
            { 7, new Point(14, 17) }, { 8, new Point(16, 17) },
        };

        // Forest Hub: north wall, y=2, x=2..16 spacing 2
        private static readonly Dictionary<int, Point> SlotWarpTilesForest = new()
        {
            { 1, new Point( 2, 2) }, { 2, new Point( 4, 2) },
            { 3, new Point( 6, 2) }, { 4, new Point( 8, 2) },
            { 5, new Point(10, 2) }, { 6, new Point(12, 2) },
            { 7, new Point(14, 2) }, { 8, new Point(16, 2) },
        };

        // ── Hub entrance points ───────────────────────────────────────────────
        // All hubs 24×20. Spine center x=11 (range 10-12), spine center y=10 (range 9-11).
        // Farm Hub — from BusStop: east spine; from Farm (slot 1): west wall slot 1 pos
        public static readonly Point HubFarmEntryFromFarm    = new( 2,  3);  // slot 1 west-wall pos
        public static readonly Point HubFarmEntryFromBusStop = new(21, 10);  // east spine (24-wide hub)

        // Backwoods Hub — from Backwoods: north spine y=2; from Farm (slot 1): south wall slot 1 pos
        public static readonly Point HubBackwoodsEntryFromBackwoods = new(11,  2);
        public static readonly Point HubBackwoodsEntryFromFarm      = new( 2, 17);  // slot 1 south-wall pos

        // Forest Hub — from Forest: south spine y=17; from Farm (slot 1): north wall slot 1 pos
        public static readonly Point HubForestEntryFromForest = new(11, 17);
        public static readonly Point HubForestEntryFromFarm   = new( 2,  2);  // slot 1 north-wall pos

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
        /// With wall-edge connections the slot position IS the arrival position.
        /// </summary>
        public static Point GetHubArrivalForSlot(int slot, string hubName)
            => GetSlotWarpTile(slot, hubName);

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
