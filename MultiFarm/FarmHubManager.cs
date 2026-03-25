using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// Manages the three shared Farm Hub locations that connect vanilla areas to
    /// player-private farms (each hub is 60×40 tiles).
    ///
    ///   East Hub  ←── Farm (west)    East Hub ──→ BusStop (east)
    ///   East Hub  ↕  North Hub  ←── Backwoods (north)
    ///   East Hub  ↕  South Hub  ──→ Forest (south)
    ///
    ///   All hubs share the same 8 portal slot positions:
    ///     Upper row (y=8):  Slot1(8,8)  Slot2(20,8)  Slot3(32,8)  Slot4(44,8)
    ///     Lower row (y=28): Slot5(8,28) Slot6(20,28) Slot7(32,28) Slot8(44,28)
    ///
    ///   Farm exits: top edge → East Hub; south edge → South Hub.
    /// </summary>
    public class FarmHubManager
    {
        // ── Location names ────────────────────────────────────────────────────
        public const string HubNameEast  = "MultiFarm_Hub_East";
        public const string HubNameNorth = "MultiFarm_Hub_North";
        public const string HubNameSouth = "MultiFarm_Hub_South";

        /// <summary>Alias for East Hub — used in code that needs "the main hub".</summary>
        public const string HubLocationName = HubNameEast;

        // ── Portal positions (identical in all 3 hubs) ────────────────────────
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

        // ── East Hub entry points ─────────────────────────────────────────────
        public static readonly Point HubEastEntryFromFarm      = new( 2, 20);  // from Farm (west)
        public static readonly Point HubEastEntryFromBusStop   = new(57, 20);  // from BusStop (east)
        public static readonly Point HubEastEntryFromNorth     = new(30,  2);  // from North Hub
        public static readonly Point HubEastEntryFromSouth     = new(30, 37);  // from South Hub

        // ── North Hub entry points ────────────────────────────────────────────
        public static readonly Point HubNorthEntryFromBackwoods = new(30,  2);  // from Backwoods
        public static readonly Point HubNorthEntryFromEast      = new(30, 37);  // from East Hub

        // ── South Hub entry points ────────────────────────────────────────────
        public static readonly Point HubSouthEntryFromEast      = new(30,  2);  // from East Hub
        public static readonly Point HubSouthEntryFromForest    = new(30, 37);  // from Forest

        // ── Backward-compat aliases (used elsewhere in the mod) ───────────────
        public static readonly Point HubEntryFromFarm      = HubEastEntryFromFarm;
        public static readonly Point HubEntryFromBusStop   = HubEastEntryFromBusStop;
        public static readonly Point HubEntryFromBackwoods = HubNorthEntryFromBackwoods;
        public static readonly Point HubEntryFromForest    = HubSouthEntryFromForest;

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
                Game1.getLocationFromName(HubNameEast)  is not null &&
                Game1.getLocationFromName(HubNameNorth) is not null &&
                Game1.getLocationFromName(HubNameSouth) is not null;

            if (allPresent)
            {
                IsRegistered = true;
                return;
            }

            foreach (var (name, mapFile) in new[]
            {
                (HubNameEast,  $"Maps/{HubNameEast}"),
                (HubNameNorth, $"Maps/{HubNameNorth}"),
                (HubNameSouth, $"Maps/{HubNameSouth}"),
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
                Game1.getLocationFromName(HubNameEast)  is not null &&
                Game1.getLocationFromName(HubNameNorth) is not null &&
                Game1.getLocationFromName(HubNameSouth) is not null;
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
                // Backwoods → North Hub (was Backwoods → Farm)
                PatchWarp(Game1.getLocationFromName("Backwoods"), "Farm",
                    HubNameNorth, HubNorthEntryFromBackwoods.X, HubNorthEntryFromBackwoods.Y);

                // Forest → South Hub (was Forest → Farm)
                PatchWarp(Game1.getLocationFromName("Forest"), "Farm",
                    HubNameSouth, HubSouthEntryFromForest.X, HubSouthEntryFromForest.Y);

                _monitor.Log("Patched vanilla warps: Backwoods → NorthHub, Forest → SouthHub.", LogLevel.Debug);
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
            name == HubNameEast || name == HubNameNorth || name == HubNameSouth;

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
