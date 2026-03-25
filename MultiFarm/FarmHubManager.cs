using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the four independent Farm Hub locations that connect vanilla areas to
    /// player-private farms (each hub is 44×24 tiles).
    ///
    ///   Farm Hub      ←── vanilla Farm map  (west entrance)
    ///   BusStop Hub   ←── vanilla BusStop   (east entrance)
    ///   Backwoods Hub ←── vanilla Backwoods (north entrance)
    ///   Forest Hub    ←── vanilla Forest    (south entrance)
    ///
    ///   All hubs share the same 8 portal slot positions:
    ///     Upper row (y=5):  Slot1(6,5)  Slot2(15,5)  Slot3(24,5)  Slot4(33,5)
    ///     Lower row (y=16): Slot5(6,16) Slot6(15,16) Slot7(24,16) Slot8(33,16)
    ///
    ///   Player farm top edge → Farm Hub.
    /// </summary>
    public class FarmHubManager
    {
        // ── Location names ────────────────────────────────────────────────────
        public const string HubNameFarm      = "MultiFarm_Hub_Farm";
        public const string HubNameBusStop   = "MultiFarm_Hub_BusStop";
        public const string HubNameBackwoods = "MultiFarm_Hub_Backwoods";
        public const string HubNameForest    = "MultiFarm_Hub_Forest";

        /// <summary>Primary hub — player farm top-edge return warp lands here.</summary>
        public const string HubLocationName = HubNameFarm;

        // ── Portal positions (identical in all 4 hubs) ────────────────────────
        // Players arriving from a player farm land at (portal.X, portal.Y + 2).
        private static readonly Dictionary<int, Point> SlotWarpTiles = new()
        {
            { 1, new Point( 6,  5) },
            { 2, new Point(15,  5) },
            { 3, new Point(24,  5) },
            { 4, new Point(33,  5) },
            { 5, new Point( 6, 16) },
            { 6, new Point(15, 16) },
            { 7, new Point(24, 16) },
            { 8, new Point(33, 16) },
        };

        // ── Hub entry points (player arrives just inside the entrance) ─────────
        public static readonly Point HubFarmEntry      = new( 2, 10);  // west entrance
        public static readonly Point HubBusStopEntry   = new(41, 10);  // east entrance
        public static readonly Point HubBackwoodsEntry = new(21,  2);  // north entrance
        public static readonly Point HubForestEntry    = new(21, 21);  // south entrance

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
                Game1.getLocationFromName(HubNameBusStop)   is not null &&
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
                (HubNameBusStop,   $"Maps/{HubNameBusStop}"),
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
                Game1.getLocationFromName(HubNameBusStop)   is not null &&
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
                // Backwoods → Backwoods Hub (was Backwoods → Farm)
                PatchWarp(Game1.getLocationFromName("Backwoods"), "Farm",
                    HubNameBackwoods, HubBackwoodsEntry.X, HubBackwoodsEntry.Y);

                // Forest → Forest Hub (was Forest → Farm)
                PatchWarp(Game1.getLocationFromName("Forest"), "Farm",
                    HubNameForest, HubForestEntry.X, HubForestEntry.Y);

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

        /// <summary>Returns true if the given location name is one of the four hub locations.</summary>
        public static bool IsHubLocation(string? name) =>
            name == HubNameFarm      || name == HubNameBusStop ||
            name == HubNameBackwoods || name == HubNameForest;

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
