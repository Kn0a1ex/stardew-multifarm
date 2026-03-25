using HarmonyLib;
using StardewValley;
using System.Linq;

namespace MultiFarm
{
    /// <summary>
    /// Harmony patches that redirect married NPCs to the correct per-player farmhouse.
    ///
    /// Problem: SDV hard-codes "FarmHouse" throughout NPC schedule strings and the
    /// NPC home-location properties.  In MultiFarm each player has their own
    /// "MultiFarm_FarmHouse_N" — married NPCs need to target that location.
    ///
    /// This class handles the schedule string side.  The NPC's DefaultMap (where
    /// they warp at the end of the night) is updated at day-start in ModEntry.
    /// </summary>
    [HarmonyPatch(typeof(NPC), "parseMasterSchedule")]
    internal class NpcSchedulePatch
    {
        /// <summary>
        /// Before the game parses an NPC's raw schedule string, replace every
        /// "FarmHouse" location token with the spouse's MultiFarm farmhouse name.
        /// </summary>
        static void Prefix(NPC __instance, ref string rawData)
        {
            // Only act when this NPC is married to a MultiFarm player
            Farmer? spouse = Game1.getAllFarmers()
                .FirstOrDefault(f => f.spouse == __instance.Name);
            if (spouse is null) return;

            int slot = ModEntry.Instance.FarmManager.GetSlotForPlayer(spouse.Name);
            if (slot == 0) return;

            string hubHouse = PlayerFarmManager.FarmHouseName(slot);

            // Pad the string so word-boundary replacements work uniformly at
            // the very start and end of rawData.
            string padded = " " + rawData + " ";
            padded = padded
                .Replace($" FarmHouse ",  $" {hubHouse} ")
                .Replace($"/FarmHouse ",  $"/{hubHouse} ")
                .Replace($" FarmHouse/",  $" {hubHouse}/")
                .Replace($"/FarmHouse/",  $"/{hubHouse}/");
            rawData = padded[1..^1];
        }
    }
}
