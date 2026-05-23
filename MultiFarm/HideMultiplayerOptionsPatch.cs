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

            // Cabin count must equal player-limit-minus-1 to match the mod's expected
            // slot count. Game1.CurrentPlayerLimit is read-only (NetField-backed), so
            // the host has to pick the matching value in the wrench (Advanced Options)
            // menu themselves. Set startingCabins to the lesser of the two and warn if
            // the wrench cap is below the mod's configured target.
            int targetMax = ModEntry.Instance?.Config?.MaxPlayers ?? Game1.CurrentPlayerLimit;
            if (targetMax < 2) targetMax = 2;
            int effectiveMax = System.Math.Min(targetMax, Game1.CurrentPlayerLimit);
            Game1.startingCabins = effectiveMax - 1;

            if (targetMax > Game1.CurrentPlayerLimit)
            {
                _monitor?.Log(
                    $"MultiFarm config wants {targetMax} player slots but the host menu's " +
                    $"Max Players is set to {Game1.CurrentPlayerLimit}. Open the wrench " +
                    $"(Advanced Options) and raise Max Players to {targetMax} to use all " +
                    $"configured farm slots.",
                    LogLevel.Warn);
            }

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
