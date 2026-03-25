using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the Farm Hub location — a shared outdoor area inserted between
    /// the vanilla Farm and BusStop (FarmHub.tmx, 60×40).
    ///
    /// Map layout:
    ///   [Farm] ←─── E-W path (y=17-22) ───→ [BusStop]
    ///
    ///   Upper portals (y=8):  Slot1(8,8)  Slot2(20,8)  Slot3(32,8)  Slot4(44,8)
    ///   Lower portals (y=28): Slot5(8,28) Slot6(20,28) Slot7(32,28) Slot8(44,28)
    ///
    /// Vanilla warp patches (applied after each save load):
    ///   Farm  → BusStop  redirected to  Hub at (2, 20)
    ///   BusStop → Farm   redirected to  Hub at (57, 20)
    /// </summary>
    public class FarmHubManager
    {
        public const string HubLocationName = "MultiFarm_Hub";

        // Portal tile positions on the hub map — must match FarmHub.tmx Warp entries.
        // Players arriving from a player farm land at (portal.X, portal.Y + 2).
        private static readonly Dictionary<int, Point> SlotWarpTiles = new()
        {
            { 1, new Point( 8,  8) },
            { 2, new Point(20,  8) },
            { 3, new Point(32,  8) },
            { 4, new Point(44,  8) },
            { 5, new Point( 8, 28) },
            { 6, new Point(20, 28) },
            { 7, new Point(32, 28) },
            { 8, new Point(44, 28) },
        };

        // Where players arrive in the hub when coming from vanilla locations
        public static readonly Point HubEntryFromFarm      = new( 2, 20);  // from vanilla Farm (west)
        public static readonly Point HubEntryFromBusStop   = new(57, 20);  // from BusStop (east)
        public static readonly Point HubEntryFromBackwoods = new(30,  1);  // from Backwoods (north)
        public static readonly Point HubEntryFromForest    = new(30, 38);  // from Forest (south)

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
            // SDV wipes Game1.locations on every save load, so we must re-register each time.
            // Check the live list rather than IsRegistered to avoid stale state.
            if (Game1.getLocationFromName(HubLocationName) is not null)
            {
                IsRegistered = true;
                return;
            }

            try
            {
                // Use the Maps/ prefix so SDV loads the map through SMAPI's content
                // pipeline (served by OnAssetRequested). This ensures the location is
                // fully initialised and warps to it don't Ghost Warp.
                string mapPath = $"Maps/{HubLocationName}";
                var hubLocation = new GameLocation(mapPath, HubLocationName)
                {
                    IsOutdoors   = true,
                    IsFarm       = false,
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
                PatchWarp(Game1.getFarm(), "BusStop",
                    HubLocationName, HubEntryFromFarm.X, HubEntryFromFarm.Y);

                // BusStop → hub (west exit of bus stop, was BusStop → Farm)
                PatchWarp(Game1.getLocationFromName("BusStop"), "Farm",
                    HubLocationName, HubEntryFromBusStop.X, HubEntryFromBusStop.Y);

                // Backwoods → hub north entry (was Backwoods → Farm)
                PatchWarp(Game1.getLocationFromName("Backwoods"), "Farm",
                    HubLocationName, HubEntryFromBackwoods.X, HubEntryFromBackwoods.Y);

                // Forest → hub south entry (was Forest → Farm)
                PatchWarp(Game1.getLocationFromName("Forest"), "Farm",
                    HubLocationName, HubEntryFromForest.X, HubEntryFromForest.Y);

                _monitor.Log("Patched vanilla warps: Farm/BusStop/Backwoods/Forest → hub.", LogLevel.Debug);
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
