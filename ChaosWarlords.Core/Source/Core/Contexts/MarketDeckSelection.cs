using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Contexts
{
    /// <summary>
    /// Immutable setup choice for the two market aspects used by a match.
    /// </summary>
    public sealed class MarketDeckSelection
    {
        public static MarketDeckSelection Default { get; } = new(CardAspect.Warlord, CardAspect.Sorcery);

        public CardAspect First { get; }
        public CardAspect Second { get; }

        public MarketDeckSelection(CardAspect first, CardAspect second)
        {
            if (!IsSelectable(first) || !IsSelectable(second) || first == second)
            {
                throw new ArgumentException("A market deck must use two distinct selectable aspects.", nameof(second));
            }

            First = first;
            Second = second;
        }

        public bool Includes(CardAspect aspect) => First == aspect || Second == aspect;

        public MarketDeckSelection WithFirst(CardAspect aspect) => new(aspect, Second);

        public MarketDeckSelection WithSecond(CardAspect aspect) => new(First, aspect);

        public static CardAspect NextDistinct(CardAspect current, CardAspect excluded)
        {
            var values = new[] { CardAspect.Warlord, CardAspect.Sorcery, CardAspect.Shadow, CardAspect.Order, CardAspect.Blasphemy };
            int nextIndex = (Array.IndexOf(values, current) + 1) % values.Length;
            while (values[nextIndex] == excluded)
            {
                nextIndex = (nextIndex + 1) % values.Length;
            }

            return values[nextIndex];
        }

        private static bool IsSelectable(CardAspect aspect) => aspect is CardAspect.Warlord or CardAspect.Sorcery or CardAspect.Shadow or CardAspect.Order or CardAspect.Blasphemy;
    }
}
