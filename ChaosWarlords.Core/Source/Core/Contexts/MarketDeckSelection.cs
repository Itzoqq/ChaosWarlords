using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Contexts
{
    /// <summary>
    /// Immutable setup choice for the two market half-decks used by a match (rulebook p.4:
    /// "Choose 2 of the market half-decks... First Game: use the Drow and Dragon half-decks").
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

        /// <summary>
        /// Walks forward from <paramref name="current"/> through <paramref name="candidates"/>
        /// (defaulting to every declared <see cref="MarketHalfDeck"/> value), skipping
        /// <paramref name="excluded"/>, and returns the first match. Bounded by
        /// <paramref name="candidates"/>'s own length rather than looping unconditionally, so a
        /// caller passing a restricted candidate list (e.g. MatchSetupState only offering
        /// half-decks ICardDatabase.GetCompleteHalfDecks reports as real/complete) can't hang if
        /// every candidate happens to equal <paramref name="excluded"/> - returns
        /// <paramref name="current"/> unchanged in that case, since there's genuinely nothing else
        /// to cycle to.
        /// </summary>
        public static MarketHalfDeck NextDistinct(MarketHalfDeck current, MarketHalfDeck excluded, IReadOnlyList<MarketHalfDeck>? candidates = null)
        {
            var values = candidates ?? Enum.GetValues<MarketHalfDeck>();
            if (values.Count == 0)
            {
                throw new ArgumentException("candidates must contain at least one half-deck to cycle through.", nameof(candidates));
            }

            int startIndex = Math.Max(IndexOf(values, current), 0);
            for (int step = 1; step <= values.Count; step++)
            {
                var candidate = values[(startIndex + step) % values.Count];
                if (candidate != excluded) return candidate;
            }

            return current;
        }

        private static int IndexOf(IReadOnlyList<MarketHalfDeck> values, MarketHalfDeck value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value) return i;
            }

            return -1;
        }
    }
}
