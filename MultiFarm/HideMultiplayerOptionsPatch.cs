using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MultiFarm
{
    /// <summary>
    /// Strips Starting Cabins and Cabin Layout controls from the character-creation screen.
    /// Patches ResetComponents (private) so the removal survives every rebuild of the UI,
    /// including the rebuild that happens when returning from AdvancedGameOptions.
    /// Difficulty and Wallets (also in ResetComponents for HostNewFarm) are left intact.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCustomization), "ResetComponents")]
    internal static class HideMultiplayerOptionsPatch
    {
        private static IMonitor? _monitor;

        public static void Initialize(IMonitor monitor) => _monitor = monitor;

        [HarmonyPostfix]
        private static void ResetComponents_Postfix(CharacterCustomization __instance)
        {
            if (__instance.source != CharacterCustomization.Source.HostNewFarm)
                return;

            // Force cabins to zero — prevents the cabin-count label from showing a number.
            Game1.startingCabins = 0;

            // Remove Starting Cabins arrow buttons.
            int removedLeft  = __instance.leftSelectionButtons .RemoveAll(b => b.name == "Cabins");
            int removedRight = __instance.rightSelectionButtons.RemoveAll(b => b.name == "Cabins");

            // Remove Cabin Layout (Close/Separate) buttons.
            int layoutCount = __instance.cabinLayoutButtons.Count;
            __instance.cabinLayoutButtons.Clear();

            // Move the two cabin labels off-screen via reflection.
            // (Bulk-removing labels by X position also removes Difficulty/Wallets labels.)
            int removedLabels = 0;
            foreach (string fieldName in new[] { "startingCabinsLabel", "cabinLayoutLabel" })
            {
                var field = typeof(CharacterCustomization)
                    .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(__instance) is ClickableComponent label)
                {
                    label.bounds = new Rectangle(-9999, -9999, 0, 0);
                    removedLabels++;
                }
            }

            // Leave advancedOptionsButton (wrench) visible — it opens AdvancedGameOptions,
            // which contains Difficulty and Wallets. Those settings should remain accessible.

            _monitor?.Log(
                $"HideMultiplayerOptionsPatch: removed {removedLeft}L/{removedRight}R Cabins buttons, " +
                $"{layoutCount} layout buttons, {removedLabels} cabin labels.",
                LogLevel.Debug);
        }
    }
}
