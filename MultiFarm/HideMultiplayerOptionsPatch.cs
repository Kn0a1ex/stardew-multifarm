using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Linq;
using System.Reflection;

namespace MultiFarm
{
    /// <summary>
    /// Strips cabin-related controls from the character-creation screen.
    /// MultiFarm manages all player slots itself; the vanilla Starting Cabins /
    /// Cabin Layout options are irrelevant and would confuse players.
    /// </summary>
    [HarmonyPatch(typeof(CharacterCustomization))]
    internal static class HideMultiplayerOptionsPatch
    {
        private static IMonitor? _monitor;

        public static void Initialize(IMonitor monitor) => _monitor = monitor;

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, new[] { typeof(CharacterCustomization.Source), typeof(bool) })]
        private static void Constructor_Postfix(CharacterCustomization __instance)
        {
            // Only apply at new-game / host-new-farm creation.
            if (__instance.source != CharacterCustomization.Source.NewGame &&
                __instance.source != CharacterCustomization.Source.HostNewFarm)
                return;

            // Force cabins to zero regardless of source default.
            Game1.startingCabins = 0;

            // Remove "Cabins" arrow buttons from the selection lists (public fields).
            int removedLeft  = __instance.leftSelectionButtons .RemoveAll(b => b.name == "Cabins");
            int removedRight = __instance.rightSelectionButtons.RemoveAll(b => b.name == "Cabins");

            // Clear cabin-layout close/separate buttons (public field).
            int layoutCount = __instance.cabinLayoutButtons.Count;
            __instance.cabinLayoutButtons.Clear();

            // Move the two private cabin labels off-screen via reflection.
            // (Removing all left-sidebar labels would also take out Difficulty/Wallets.)
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

            // Move the wrench / advanced-options button off-screen.
            if (__instance.advancedOptionsButton is not null)
                __instance.advancedOptionsButton.bounds = new Rectangle(-9999, -9999, 0, 0);

            _monitor?.Log(
                $"HideMultiplayerOptionsPatch: removed {removedLeft} left, {removedRight} right Cabins buttons; " +
                $"{layoutCount} layout buttons; {removedLabels} sidebar labels; wrench hidden.",
                LogLevel.Debug);
        }
    }
}
