// TODO: Married NPC Pathing Redirect
//
// Problem: When a player marries an NPC (bachelor/bachelorette), the NPC's
// daily schedule points them to "FarmHouse" — the vanilla single-player
// farmhouse location. In MultiFarm, each player has their own
// "MultiFarm_FarmHouse_N" location. An NPC married to a MultiFarm player
// needs to target THAT player's farmhouse, not the vanilla one.
//
// Approach: Harmony prefix patch on NPC.parseMasterSchedule (or the method
// that resolves "FarmHouse" in schedule entries) to redirect the location
// name to the owning player's MultiFarm_FarmHouse_N.
//
// Requires adding Harmony to the project:
//   1. Add <PackageReference Include="Harmony" Version="2.*" /> to MultiFarm.csproj
//   2. In ModEntry.Entry(), call:
//        var harmony = new HarmonyLib.Harmony(ModManifest.UniqueID);
//        harmony.PatchAll();
//
// Skeleton:
//
//   [HarmonyLib.HarmonyPatch(typeof(NPC), "parseMasterSchedule")]
//   internal class NpcSchedulePatch
//   {
//       static void Prefix(NPC __instance, ref string rawData)
//       {
//           // Find which player (if any) this NPC is married to
//           Farmer? spouse = Game1.getAllFarmers()
//               .FirstOrDefault(f => f.spouse == __instance.Name);
//           if (spouse is null) return;
//
//           // Resolve their farmhouse name
//           int slot = ModEntry.Instance.FarmManager.GetSlotForPlayer(spouse.Name);
//           if (slot == 0) return;
//           string hubHouse = PlayerFarmManager.FarmHouseName(slot);
//
//           // Replace "FarmHouse" tokens in the raw schedule string
//           rawData = rawData.Replace(" FarmHouse ", $" {hubHouse} ")
//                            .Replace(" FarmHouse/", $" {hubHouse}/")
//                            .Replace("/FarmHouse ", $"/{hubHouse} ");
//       }
//   }
//
// Note: NPC path-finding inside the farmhouse also uses "FarmHouse" as the
// target location when the NPC is *already* inside and needs to navigate.
// A second patch on PathFindController or NPC.warpToPathControllerDestination
// may be needed to handle that case.
