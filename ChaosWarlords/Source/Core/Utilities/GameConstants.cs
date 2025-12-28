namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Centralized game balance and configuration constants.
    /// Modify these values to tune gameplay without searching through code.
    /// </summary>
    public static class GameConstants
    {
        // Combat Costs
        public const int DeployPowerCost = 1;
        public const int AssassinatePowerCost = 3;
        public const int ReturnSpyPowerCost = 3;

        // Card Management
        public const int HandSize = 5;
        public const int MarketRowSize = 6;

        // Starting Resources
        public const int StartingTroops = 40;
        public const int StartingSpies = 5;
        public const int TargetVictoryPoints = 40;

        // UI Layout (Site Bounds)
        public static class SiteVisuals
        {
            public const int SidePadding = 35;
            public const int TopPadding = 70;
            public const int BottomPadding = 35;
        }
    }
}


