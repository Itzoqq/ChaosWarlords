using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Contexts
{
    /// <summary>
    /// Immutable setup choice for the two market half-decks used by a match.
    /// </summary>
    public sealed class MarketDeckSelection
    {
        public static MarketDeckSelection Default { get; } = new(MarketHalfDeck.Drow, MarketHalfDeck.Dragons);

        public MarketHalfDeck First { get; }
        public MarketHalfDeck Second { get; }

        public MarketDeckSelection(MarketHalfDeck first, MarketHalfDeck second)
        {
            if (first == second)
            {
                throw new ArgumentException("A market deck must use two distinct half-decks.", nameof(second));
            }

            First = first;
            Second = second;
        }

        public bool Includes(MarketHalfDeck halfDeck) => First == halfDeck || Second == halfDeck;

        public MarketDeckSelection WithFirst(MarketHalfDeck halfDeck) => new(halfDeck, Second);

        public MarketDeckSelection WithSecond(MarketHalfDeck halfDeck) => new(First, halfDeck);

        public static MarketHalfDeck NextDistinct(MarketHalfDeck current, MarketHalfDeck excluded)
        {
            var values = Enum.GetValues<MarketHalfDeck>();
            int nextIndex = (Array.IndexOf(values, current) + 1) % values.Length;
            while (values[nextIndex] == excluded)
            {
                nextIndex = (nextIndex + 1) % values.Length;
            }

            return values[nextIndex];
        }
    }
}
