using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Utilities
{
    public static class MapGeometry
    {
        public static (float MinX, float MinY, float MaxX, float MaxY) CalculateBounds(List<MapNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return (0, 0, 0, 0);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in nodes)
            {
                if (node.Position.X < minX) minX = node.Position.X;
                if (node.Position.Y < minY) minY = node.Position.Y;
                if (node.Position.X > maxX) maxX = node.Position.X;
                if (node.Position.Y > maxY) maxY = node.Position.Y;
            }

            return (minX, minY, maxX, maxY);
        }

        public static bool TryGetLineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 result)
        {
            result = Vector2.Zero;
            float d = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
            if (d == 0) return false;

            float ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / d;
            float ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / d;

            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                result = new Vector2(p1.X + ua * (p2.X - p1.X), p1.Y + ua * (p2.Y - p1.Y));
                return true;
            }
            return false;
        }
    }
}