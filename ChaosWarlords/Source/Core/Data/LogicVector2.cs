using System;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Core.Data
{
    /// <summary>
    /// Represents a 2D vector using deterministic integer coordinates.
    /// Used for logic-critical pathing and positioning to prevent floating-point desyncs in multiplayer.
    /// </summary>
    public struct LogicVector2 : IEquatable<LogicVector2>
    {
        // 1.0f in World Space = 1000 units in Logic Space
        // This allows 3 decimal places of precision while keeping math integer-based.
        public const int ScaleFactor = 1000;

        public int X { get; set; }
        public int Y { get; set; }

        public LogicVector2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static LogicVector2 Zero => new LogicVector2(0, 0);

        /// <summary>
        /// Converts a floating-point world position to logic position.
        /// </summary>
        public static LogicVector2 FromVector2(Vector2 vector)
        {
            return new LogicVector2(
                (int)Math.Round(vector.X * ScaleFactor),
                (int)Math.Round(vector.Y * ScaleFactor)
            );
        }

        /// <summary>
        /// Converts logic position back to floating-point world position for rendering.
        /// </summary>
        public Vector2 ToVector2()
        {
            return new Vector2(
                (float)X / ScaleFactor,
                (float)Y / ScaleFactor
            );
        }

        /// <summary>
        /// Calculates the determininstic squared distance between two points.
        /// Returns long to prevent overflow.
        /// </summary>
        public static long DistanceSquared(LogicVector2 a, LogicVector2 b)
        {
            long dx = (long)b.X - a.X;
            long dy = (long)b.Y - a.Y;
            return (dx * dx) + (dy * dy);
        }

        /// <summary>
        /// Deterministic linear interpolation.
        /// </summary>
        /// <param name="start">Start point</param>
        /// <param name="end">End point</param>
        /// <param name="numerator">Numerator of the interpolation factor (e.g. current step)</param>
        /// <param name="denominator">Denominator of the interpolation factor (e.g. total steps)</param>
        public static LogicVector2 Lerp(LogicVector2 start, LogicVector2 end, int numerator, int denominator)
        {
            if (denominator == 0) return end;

            /*
             * Formula: start + (end - start) * (num / den)
             * Integer math: start + ((end - start) * num) / den
             */

            long dx = (long)end.X - start.X;
            long dy = (long)end.Y - start.Y;

            int x = start.X + (int)((dx * numerator) / denominator);
            int y = start.Y + (int)((dy * numerator) / denominator);

            return new LogicVector2(x, y);
        }

        public static LogicVector2 operator +(LogicVector2 a, LogicVector2 b)
        {
            return new LogicVector2(a.X + b.X, a.Y + b.Y);
        }

        public static LogicVector2 operator -(LogicVector2 a, LogicVector2 b)
        {
            return new LogicVector2(a.X - b.X, a.Y - b.Y);
        }
        
        public static bool operator ==(LogicVector2 a, LogicVector2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(LogicVector2 a, LogicVector2 b) => !(a == b);

        public bool Equals(LogicVector2 other) => this == other;

        public override bool Equals(object? obj) => obj is LogicVector2 other && this == other;

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"{{X:{X} Y:{Y}}}";
    }
}
