using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Rendering.UI
{
    /// <summary>
    /// Defines the distinct locations of the shuffled market row and fixed recruit piles.
    /// </summary>
    public static class MarketLayout
    {
        public const int FixedPileTopMargin = 60;
        public const int FixedPileGap = 20;

        public static Vector2 GetMarketRowPosition(int index)
        {
            return new Vector2(
                GameConstants.CardRendering.MarketStartX + (index * (Card.Width + GameConstants.CardRendering.MarketCardGap)),
                GameConstants.CardRendering.MarketStartY);
        }

        public static Vector2 GetFixedRecruitPilePosition(int index, int pileCount, int viewportWidth)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pileCount);
            ArgumentOutOfRangeException.ThrowIfLessThan(viewportWidth, Card.Width);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, pileCount);

            int width = (pileCount * Card.Width) + ((pileCount - 1) * FixedPileGap);
            int startX = (viewportWidth - width) / 2;
            return new Vector2(
                startX + (index * (Card.Width + FixedPileGap)),
                GameConstants.CardRendering.MarketStartY + Card.Height + FixedPileTopMargin);
        }
    }
}
