using HarmonyLib;
using StardewValley;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MultiFarm
{
    /// <summary>
    /// Intercepts warps before the fade-to-black transition so the player always
    /// arrives at the right tile on the first warp with no double-warp flash.
    ///
    /// Handles:
    ///   Farm↔BusStop   — hardcoded in SDV 1.6, not in location.warps
    ///   Hub→player farm — TMX warps use a placeholder tile; corrected here
    /// </summary>
    [HarmonyPatch]
    internal class BusStopWarpPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            // In SDV 1.6 the primary warpFarmer overload takes a LocationRequest object,
            // not a bare string.  The string overload either doesn't exist or isn't called
            // for edge-of-map transitions, so we must patch the LocationRequest variant.
            return typeof(Game1)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m =>
                    m.Name == "warpFarmer" &&
                    m.GetParameters().Length >= 1 &&
                    m.GetParameters()[0].ParameterType == typeof(LocationRequest) &&
                    m.GetParameters().Any(p => p.Name == "facingDirectionAfterWarp"));
        }

        [HarmonyPrefix]
        static void Prefix(
            ref LocationRequest locationRequest,
            ref int             tileX,
            ref int             tileY,
            ref int             facingDirectionAfterWarp)
        {
            if (!ModEntry.Instance.Config.ReplaceVanillaWarps) return;

            string from = Game1.player?.currentLocation?.Name ?? "";
            string dest = locationRequest?.Name ?? "";

            // Farm east edge → Farm Hub (west wall, slot 1 arrival position)
            if (dest == "BusStop" && from == "Farm")
            {
                locationRequest = new LocationRequest(
                    FarmHubManager.HubNameFarm, false,
                    Game1.getLocationFromName(FarmHubManager.HubNameFarm));
                tileX                    = FarmHubManager.HubFarmEntryFromFarm.X;
                tileY                    = FarmHubManager.HubFarmEntryFromFarm.Y;
                facingDirectionAfterWarp = 1;
            }
            // BusStop west edge → Farm Hub (east wall, spine position)
            else if (dest == "Farm" && from == "BusStop")
            {
                locationRequest = new LocationRequest(
                    FarmHubManager.HubNameFarm, false,
                    Game1.getLocationFromName(FarmHubManager.HubNameFarm));
                tileX                    = FarmHubManager.HubFarmEntryFromBusStop.X;
                tileY                    = FarmHubManager.HubFarmEntryFromBusStop.Y;
                facingDirectionAfterWarp = 3;
            }
            // Hub portal → player farm: TMX warp uses a placeholder tile (75,15).
            // Correct destination tile here so the player arrives right on the first warp.
            else if (FarmHubManager.IsHubLocation(from) &&
                     (dest == "Farm" || dest.StartsWith(PlayerFarmManager.FarmPrefix)))
            {
                var player = Game1.player;
                if (player is not null)
                {
                    int slot = ModEntry.Instance.FarmManager
                                   .GetSlotForPlayer(player.UniqueMultiplayerID);
                    if (slot > 0)
                    {
                        var (rx, ry, rfacing) = ModEntry.Instance.FarmManager
                                                    .GetHubArrivalOnFarm(slot, from);
                        tileX                    = rx;
                        tileY                    = ry;
                        facingDirectionAfterWarp = rfacing;
                        // locationRequest stays the same — destination farm is correct
                    }
                }
            }
        }
    }
}
