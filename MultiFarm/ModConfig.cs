namespace MultiFarm
{
    /// <summary>
    /// User-editable config values (config.json in the mod folder).
    /// </summary>
    public class ModConfig
    {
        /// <summary>Maximum number of private farms (1–8).</summary>
        public int MaxPlayers { get; set; } = 8;

        /// <summary>
        /// Farm types players can choose from.
        /// Maps to Stardew's internal farm type IDs:
        ///   0 = Standard, 1 = Riverland, 2 = Forest, 3 = Hill-top,
        ///   4 = Wilderness, 5 = Four Corners, 6 = Meadowlands
        /// </summary>
        public int[] AllowedFarmTypes { get; set; } = { 0, 1, 2, 3, 4, 5, 6 };

        /// <summary>
        /// Whether the hub warps replace the default Farm ↔ BusStop transition.
        /// Set false if you want to keep the original warp and add the hub separately.
        /// </summary>
        public bool ReplaceVanillaWarps { get; set; } = true;
    }
}
