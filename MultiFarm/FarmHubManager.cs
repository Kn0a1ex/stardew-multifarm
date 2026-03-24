using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using xTile;
using xTile.Dimensions;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the Farm Hub location — a shared outdoor area between the main farm
    /// and the bus stop. Paths radiate out to each player's private farm (slots 1–8).
    ///
    /// Map layout (conceptual):
    ///
    ///   [BusStop warp]  ←──── path north
    ///                         │
    ///   [Farm7][Farm8]──west──HUB──east──[Farm1][Farm2]
    ///                         │
    ///                    south paths
    ///              [Farm3][Farm4][Farm5][Farm6]
    ///                         │
    ///                   [Farm warp]
    /// </summary>
    public class FarmHubManager
    {
        public const string HubLocationName = "MultiFarm_Hub";

        // Warp tile positions on the hub map (tile coords, direction OUT of hub).
        // These must match the TMX map you build in Tiled.
        private static readonly Dictionary<int, Point> SlotWarpTiles = new()
        {
            { 1, new Point(38, 10) },
            { 2, new Point(42, 10) },
            { 3, new Point(22, 26) },
            { 4, new Point(28, 26) },
            { 5, new Point(34, 26) },
            { 6, new Point(40, 26) },
            { 7, new Point(10, 10) },
            { 8, new Point(14, 10) },
        };

        // Where the hub connects back to vanilla locations
        private const string VanillaFarmWarpTile    = "Farm";        // source location
        private const string VanillaBusStopWarpTile = "BusStop";     // source location

        // Hub entry/exit tile coords (must match TMX)
        private static readonly Point HubEntryFromFarm    = new(25, 30);   // bottom of hub
        private static readonly Point HubEntryFromBusStop = new(25,  2);   // top of hub

        // Return warp positions on each player farm map (top-center of the map)
        private static readonly Point FarmReturnWarpTile = new(32, 1);

        public bool IsRegistered { get; private set; }

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        public FarmHubManager(IModHelper helper, IMonitor monitor)
        {
            _helper  = helper;
            _monitor = monitor;
        }

        /// <summary>
        /// Inject the hub GameLocation into the world on game launch.
        /// Called before any save is loaded so the location exists when saves reference it.
        /// </summary>
        public void RegisterLocations()
        {
            try
            {
                // Load the hub map from assets/maps/FarmHub.tmx
                string mapPath = _helper.ModContent.GetInternalAssetName("assets/maps/FarmHub.tmx").Name;
                var hubLocation = new GameLocation(mapPath, HubLocationName)
                {
                    IsOutdoors  = true,
                    IsFarm      = false,
                    IsGreenhouse = false,
                };
                Game1.locations.Add(hubLocation);

                IsRegistered = true;
                _monitor.Log($"Registered hub location '{HubLocationName}'.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to register hub location: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// After a save loads, redirect the vanilla Farm ↔ BusStop warps through the hub.
        /// </summary>
        public void PatchVanillaWarps()
        {
            if (!ModEntry.Instance.Config.ReplaceVanillaWarps) return;

            try
            {
                // Farm → hub (east exit of farm, was Farm → BusStop)
                var farm = Game1.getFarm();
                PatchWarp(farm, targetLocation: "BusStop",
                    newTarget: HubLocationName, newTargetX: HubEntryFromFarm.X, newTargetY: HubEntryFromFarm.Y);

                // BusStop → hub (west exit of bus stop, was BusStop → Farm)
                var busStop = Game1.getLocationFromName("BusStop");
                PatchWarp(busStop, targetLocation: "Farm",
                    newTarget: HubLocationName, newTargetX: HubEntryFromBusStop.X, newTargetY: HubEntryFromBusStop.Y);

                _monitor.Log("Patched vanilla Farm ↔ BusStop warps to route through hub.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to patch vanilla warps: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Called when a player steps into the hub. Could show path labels, etc.</summary>
        public void OnPlayerEnterHub(Farmer player)
        {
            int slot = ModEntry.Instance.FarmManager.GetSlotForPlayer(player.Name);
            if (slot > 0)
            {
                // Future: highlight the tile path to this player's farm
                _monitor.Log($"{player.Name} entered the hub (assigned slot {slot}).", LogLevel.Trace);
            }
        }

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
        /// Get the warp-back tile that a player farm should use to return to the hub.
        /// </summary>
        public static Point GetFarmReturnTile() => FarmReturnWarpTile;
    }
}
