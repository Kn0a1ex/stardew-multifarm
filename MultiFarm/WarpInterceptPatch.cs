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
                tileX                   = FarmHubManager.HubFarmEntryFromFarm.X;
                tileY                   = FarmHubManager.HubFarmEntryFromFarm.Y;
                facingDirectionAfterWarp = 1; // face east into hub
            }
            // BusStop west edge → Farm Hub (east wall, spine position)
            else if (dest == "Farm" && from == "BusStop")
            {
                locationRequest = new LocationRequest(
                    FarmHubManager.HubNameFarm, false,
                    Game1.getLocationFromName(FarmHubManager.HubNameFarm));
                tileX                   = FarmHubManager.HubFarmEntryFromBusStop.X;
                tileY                   = FarmHubManager.HubFarmEntryFromBusStop.Y;
                facingDirectionAfterWarp = 3; // face west into hub
            }
        }
    }
}
