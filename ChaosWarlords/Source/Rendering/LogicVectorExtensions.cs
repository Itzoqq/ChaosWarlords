using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Data;

namespace ChaosWarlords.Source.Rendering
{
    /// <summary>
    /// Conversion between the logic layer's deterministic, fixed-point
    /// <see cref="LogicVector2"/>/<see cref="LogicRectangle"/> and MonoGame's float-based
    /// <see cref="Vector2"/>/<see cref="Rectangle"/>. Lives here, in the client project,
    /// rather than on the logic types themselves, so ChaosWarlords.Core stays free of any
    /// MonoGame package reference. See LogicVector2.ScaleFactor for the fixed-point scale.
    /// </summary>
    public static class LogicVectorExtensions
    {
        public static Vector2 ToVector2(this LogicVector2 v) =>
            new((float)v.X / LogicVector2.ScaleFactor, (float)v.Y / LogicVector2.ScaleFactor);

        public static LogicVector2 ToLogicVector2(this Vector2 v) =>
            new((int)Math.Round(v.X * LogicVector2.ScaleFactor), (int)Math.Round(v.Y * LogicVector2.ScaleFactor));

        public static Rectangle ToRectangle(this LogicRectangle r) =>
            new(r.X / LogicVector2.ScaleFactor, r.Y / LogicVector2.ScaleFactor,
                r.Width / LogicVector2.ScaleFactor, r.Height / LogicVector2.ScaleFactor);
    }
}
