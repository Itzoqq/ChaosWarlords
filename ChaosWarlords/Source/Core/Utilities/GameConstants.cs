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

        /// <summary>
        /// Card rendering dimensions and layout constants.
        /// Centralizes magic numbers from Card.cs, CardRenderer.cs, and GameplayView.cs.
        /// </summary>
        public static class CardRendering
        {
            // Card Dimensions
            public const int CardWidth = 150;
            public const int CardHeight = 200;

            // Text Layout (CardRenderer)
            public const int TextPadding = 5;
            public const int EffectTextStartY = 40;
            public const int EffectTextSpacing = 20;
            public const int VictoryPointsOffsetY = 20;
            public const int BorderThickness = 2;

            // Hand/Played Area Layout (GameplayView)
            public const int HandBottomMargin = 20;
            public const int PlayedAreaGap = 10;
            public const int HandCardGap = 10;

            // Market Layout
            public const int MarketStartX = 100;
            public const int MarketStartY = 100;
            public const int MarketCardGap = 10;

            // Hover Effects
            public const float HoverBrighten = 0.3f;
        }

        /// <summary>
        /// General UI layout constants for buttons, spacing, and positioning.
        /// </summary>
        public static class UILayout
        {
            // Top Bar
            public const int TopBarHeight = 40;
            public const int TopBarPadding = 10;
            public const int TopBarSpacing = 30;

            // General Spacing
            public const int SmallPadding = 5;
            public const int MediumPadding = 10;
            public const int LargePadding = 20;

            // Button Sizes
            public const int DefaultButtonWidth = 200;
            public const int DefaultButtonHeight = 30;
            public const int LargeButtonHeight = 50;

            // Text Offsets
            public const int DefaultYOffset = 40;
            public const int HeaderTopMargin = 20;
            public const int SetupOverlayY = 60;

            // Tooltip
            public const int TooltipOffsetX = 20;
            public const int TooltipOffsetY = 20;
        }
    }
}


