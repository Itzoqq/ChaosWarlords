using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Rendering
{
    /// <summary>
    /// Maps the logic layer's <see cref="PlayerColor"/> to an actual MonoGame <see cref="Color"/>
    /// for rendering - the same "logic type -> MonoGame type, client-only" boundary conversion
    /// <see cref="LogicVectorExtensions"/> already establishes for <c>LogicVector2</c>/
    /// <c>LogicRectangle</c>, just for player identity color instead of position.
    /// </summary>
    public static class PlayerColorExtensions
    {
        public static Color ToColor(this PlayerColor color) => color switch
        {
            PlayerColor.Red => Color.Red,
            PlayerColor.Blue => Color.Blue,
            // A literal near-black would be unreadable against this UI's dark panels and
            // visually indistinguishable from a dimmed/ineligible row - Purple reads as a
            // distinct player identity instead.
            PlayerColor.Black => Color.Purple,
            PlayerColor.Orange => Color.Orange,
            _ => Color.White // Neutral/None - never a real seated player, defensive fallback only
        };
    }
}
