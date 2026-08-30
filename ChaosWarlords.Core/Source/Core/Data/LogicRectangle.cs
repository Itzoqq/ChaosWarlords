namespace ChaosWarlords.Source.Core.Data
{
    /// <summary>
    /// Represents an axis-aligned bounding box using deterministic integer coordinates,
    /// in the same fixed-point logic space as <see cref="LogicVector2"/> (see its
    /// <c>ScaleFactor</c> doc comment). Used for site bounds so map/hit-testing logic
    /// doesn't need a MonoGame package reference.
    /// </summary>
    public struct LogicRectangle : IEquatable<LogicRectangle>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public LogicRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Right => X + Width;
        public int Top => Y;
        public int Bottom => Y + Height;

        public LogicVector2 Center => new LogicVector2(X + Width / 2, Y + Height / 2);

        public bool Contains(LogicVector2 point) =>
            point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

        public static bool operator ==(LogicRectangle a, LogicRectangle b) =>
            a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        public static bool operator !=(LogicRectangle a, LogicRectangle b) => !(a == b);

        public bool Equals(LogicRectangle other) => this == other;

        public override bool Equals(object? obj) => obj is LogicRectangle other && this == other;

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public override string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}
