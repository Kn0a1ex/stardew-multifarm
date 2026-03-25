using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the three Farm Hub locations that connect vanilla areas to
    /// player-private farms (each hub is 44×24 tiles).
    ///
    ///   Farm Hub      ←──── vanilla Farm (west)   ────→ vanilla BusStop (east)
    ///   Backwoods Hub ←──── vanilla Backwoods (north) / vanilla Farm north trail (south)
    ///   Forest Hub    ←──── vanilla Forest (south) / vanilla Farm south edge (north)
    ///
    ///   All hubs share the same 8 portal slot positions:
    ///     Upper row (y=5):  Slot1(6,5)  Slot2(15,5)  Slot3(24,5)  Slot4(33,5)
    ///     Lower row (y=16): Slot5(6,16) Slot6(15,16) Slot7(24,16) Slot8(33,16)
    ///
    ///   Player farm top edge → Backwoods Hub (south entrance).
    ///   Player farm south edge → Forest Hub (north entrance).
    /// </summary>
    public class FarmHubManager
    {
        // ── Location names ────────────────────────────────────────────────────
        public const string HubNameFarm      = "MultiFarm_Hub_Farm";
        public const string HubNameBackwoods = "MultiFarm_Hub_Backwoods";
        public const string HubNameForest    = "MultiFarm_Hub_Forest";

        // ── Portal positions (identical in all 3 hubs) ────────────────────────
        // Single row of 8 at y=5, spacing 5 tiles center-to-center.
        // Players arriving from a player farm land at (portal.X, portal.Y + 2).
        private static readonly Dictionary<int, Point> SlotWarpTiles = new()
        {
            { 1, new Point( 4,  5) },
            { 2, new Point( 9,  5) },
            { 3, new Point(14,  5) },
            { 4, new Point(19,  5) },
            { 5, new Point(24,  5) },
            { 6, new Point(29,  5) },
            { 7, new Point(34,  5) },
            { 8, new Point(39,  5) },
        };

        // ── Farm Hub entry points (horizontal hub, west+east exits) ───────────
        public static readonly Point HubFarmEntryFromFarm    = new( 2, 10);  // from Farm west
        public static readonly Point HubFarmEntryFromBusStop = new(41, 10);  // from BusStop east

        // ── Backwoods Hub entry points (vertical hub, north+south exits) ──────
        public static readonly Point HubBackwoodsEntryFromBackwoods = new(21,  2);  // from Backwoods north
        public static readonly Point HubBackwoodsEntryFromFarm      = new(21, 21);  // from Farm south (mountain trail)

        // ── Forest Hub entry points (vertical hub, north+south exits) ─────────
        public static readonly Point HubForestEntryFromForest = new(21, 21);  // from Forest south
        public static readonly Point HubForestEntryFromFarm   = new(21,  2);  // from Farm north (south edge)

        public bool IsRegistered { get; private set; }

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        public FarmHubManager(IModHelper helper, IMonitor monitor)
        {
            _helper  = helper;
            _monitor = monitor;
        }

        /// <summary>
        /// Inject all three hub GameLocations into the world.
        /// SDV wipes Game1.locations on every save load so this must be called after each load.
        /// </summary>
        public void RegisterLocations()
        {
            bool allPresent =
                Game1.getLocationFromName(HubNameFarm)      is not null &&
                Game1.getLocationFromName(HubNameBackwoods) is not null &&
                Game1.getLocationFromName(HubNameForest)    is not null;

            if (allPresent)
            {
                IsRegistered = true;
                return;
            }

            foreach (var (name, mapFile) in new[]
            {
                (HubNameFarm,      $"Maps/{HubNameFarm}"),
                (HubNameBackwoods, $"Maps/{HubNameBackwoods}"),
                (HubNameForest,    $"Maps/{HubNameForest}"),
            })
            {
                if (Game1.getLocationFromName(name) is not null) continue;
                try
                {
                    var loc = new GameLocation(mapFile, name)
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
                    _monitor.Log($"Failed to register hub location '{name}': {ex.Message}", LogLevel.Error);
                }
            }

            IsRegistered =
                Game1.getLocationFromName(HubNameFarm)      is not null &&
                Game1.getLocationFromName(HubNameBackwoods) is not null &&
                Game1.getLocationFromName(HubNameForest)    is not null;
        }

        /// <summary>
        /// After a save loads, redirect vanilla warps through the appropriate hub.
        /// Farm/BusStop are handled via OnWarped (SDV 1.6 no longer stores them in location.warps).
        /// Backwoods/Forest are patched here as a runtime fallback; AssetRequested handles reloads.
        /// </summary>
        public void PatchVanillaWarps()
        {
            if (!ModEntry.Instance.Config.ReplaceVanillaWarps) return;

            try
            {
                // Backwoods south edge → Backwoods Hub north entrance
                PatchWarp(Game1.getLocationFromName("Backwoods"), "Farm",
                    HubNameBackwoods,
                    HubBackwoodsEntryFromBackwoods.X, HubBackwoodsEntryFromBackwoods.Y);

                // Forest north edge → Forest Hub south entrance
                PatchWarp(Game1.getLocationFromName("Forest"), "Farm",
                    HubNameForest,
                    HubForestEntryFromForest.X, HubForestEntryFromForest.Y);

                _monitor.Log("Patched vanilla warps: Backwoods → BackwoodsHub, Forest → ForestHub.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to patch vanilla warps: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Called when a player steps into any hub location.</summary>
        public void OnPlayerEnterHub(Farmer player, string hubName)
        {
            int slot = ModEntry.Instance.FarmManager.GetSlotForPlayer(player.Name);
            if (slot > 0)
            {
                _monitor.Log($"{player.Name} entered {hubName} (assigned slot {slot}).", LogLevel.Trace);
            }
        }

        /// <summary>Returns true if the given location name is one of the three hub locations.</summary>
        public static bool IsHubLocation(string? name) =>
            name == HubNameFarm || name == HubNameBackwoods || name == HubNameForest;

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Replace an existing warp on a location with a new destination.
        /// </summary>
        private static void PatchWarp(GameLocation location, string targetLocation,
            string newTarget, int newTargetX, int newTargetY)
        {
            if (location is null) return;
            var warps = location.warps;
            for (int i = 0; i < warps.Count; i++)
            {
                if (warps[i].TargetName.Equals(targetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    int srcX = warps[i].X;
                    int srcY = warps[i].Y;
                    warps[i] = new Warp(srcX, srcY, newTarget, newTargetX, newTargetY, flipFarmer: false);
                }
            }
        }

        /// <summary>
        /// Get the hub tile coordinate for the warp exit leading to a specific farm slot.
        /// </summary>
        public static Point GetSlotWarpTile(int slot)
        {
            return SlotWarpTiles.TryGetValue(slot, out var pt) ? pt : Point.Zero;
        }

        /// <summary>
        /// Get the hub arrival position for a player returning from a specific farm slot.
        /// Lands 2 tiles below the portal so players don't immediately re-enter.
        /// </summary>
        public static Point GetHubArrivalForSlot(int slot)
        {
            var portal = GetSlotWarpTile(slot);
            return new Point(portal.X, portal.Y + 2);
        }
    }
}
