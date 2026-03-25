using HarmonyLib;
using StardewValley;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MultiFarm
{
    /// <summary>
    /// Intercepts Farm↔BusStop warps at the source (before any transition animation)
    /// and redirects them through the Farm Hub.
    ///
    /// In SDV 1.6 the Farm↔BusStop connection is no longer stored in location.warps,
    /// so it can't be redirected via map asset edits.  Patching Game1.warpFarmer lets
    /// us change the destination before the game starts the fade-to-black, eliminating
    /// the brief flash of the wrong location that the OnWarped approach causes.
    /// </summary>
    [HarmonyPatch]
    internal class BusStopWarpPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            // Only patch the overload that has both "locationName" (string) and
            // "facingDirectionAfterWarp" (int).  Other overloads use LocationRequest
            // instead of a string name, or bool flip instead of facing — injecting
            // our prefix into those causes a Harmony parameter-not-found crash.
            return typeof(Game1)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m =>
                    m.Name == "warpFarmer" &&
                    m.GetParameters().Length >= 1 &&
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters().Any(p => p.Name == "facingDirectionAfterWarp"));
        }

        [HarmonyPrefix]
        static void Prefix(
            ref string locationName,
            ref int    tileX,
            ref int    tileY,
            ref int    facingDirectionAfterWarp)
        {
            if (!ModEntry.Instance.Config.ReplaceVanillaWarps) return;

            string from = Game1.player?.currentLocation?.Name ?? "";

            // Farm east edge → Farm Hub (west wall, slot 1 arrival position)
            if (locationName == "BusStop" && from == "Farm")
            {
                locationName            = FarmHubManager.HubNameFarm;
                tileX                   = FarmHubManager.HubFarmEntryFromFarm.X;
                tileY                   = FarmHubManager.HubFarmEntryFromFarm.Y;
                facingDirectionAfterWarp = 1; // face east into hub
            }
            // BusStop west edge → Farm Hub (east wall, spine position)
            else if (locationName == "Farm" && from == "BusStop")
            {
                locationName            = FarmHubManager.HubNameFarm;
                tileX                   = FarmHubManager.HubFarmEntryFromBusStop.X;
                tileY                   = FarmHubManager.HubFarmEntryFromBusStop.Y;
                facingDirectionAfterWarp = 3; // face west into hub
            }
        }
    }
}
