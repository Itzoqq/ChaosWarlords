namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Centralized game balance and configuration constants.
    /// Modify these values to tune gameplay without searching through code.
    /// </summary>
    public static class GameConstants
    {
        // Combat Costs
        public const int DEPLOY_POWER_COST = 1;
        public const int ASSASSINATE_POWER_COST = 3;
        public const int RETURN_SPY_POWER_COST = 3;

        // Card Management
        public const int HAND_SIZE = 5;
        public const int MARKET_ROW_SIZE = 6;

        // Starting Resources
        public const int STARTING_TROOPS = 40;
        public const int STARTING_SPIES = 5;
        public const int TARGET_VICTORY_POINTS = 40;

        // UI Layout (Site Bounds)
        public static class SiteVisuals
        {
            public const int SIDE_PADDING = 35;
            public const int TOP_PADDING = 70;
            public const int BOTTOM_PADDING = 35;
        }
    }
}


