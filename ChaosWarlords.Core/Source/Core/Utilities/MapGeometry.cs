using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Utilities
{
    public static class MapGeometry
    {
        public static (int MinX, int MinY, int MaxX, int MaxY) CalculateBounds(List<MapNode> nodes)
        {
            if (nodes is null || nodes.Count == 0) return (0, 0, 0, 0);

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var node in nodes)
            {
                if (node.LogicPosition.X < minX) minX = node.LogicPosition.X;
                if (node.LogicPosition.Y < minY) minY = node.LogicPosition.Y;
                if (node.LogicPosition.X > maxX) maxX = node.LogicPosition.X;
                if (node.LogicPosition.Y > maxY) maxY = node.LogicPosition.Y;
            }

            return (minX, minY, maxX, maxY);
        }

        public static bool TryGetLineIntersection(Core.Data.LogicVector2 p1, Core.Data.LogicVector2 p2, Core.Data.LogicVector2 p3, Core.Data.LogicVector2 p4, out Core.Data.LogicVector2 result)
        {
            result = Core.Data.LogicVector2.Zero;

            long d = (long)(p4.Y - p3.Y) * (p2.X - p1.X) - (long)(p4.X - p3.X) * (p2.Y - p1.Y);
            if (d == 0) return false;

            // Numerators for ua and ub
            long num_ua = (long)(p4.X - p3.X) * (p1.Y - p3.Y) - (long)(p4.Y - p3.Y) * (p1.X - p3.X);
            long num_ub = (long)(p2.X - p1.X) * (p1.Y - p3.Y) - (long)(p2.Y - p1.Y) * (p1.X - p3.X);

            // Check if intersection occurs within segments [0, 1]
            // We need 0 <= ua/d <= 1  AND  0 <= ub/d <= 1

            // To check 0 <= num/d <= 1 without division:
            // If d > 0: 0 <= num <= d
            // If d < 0: 0 >= num >= d  (or d <= num <= 0)

            bool uaValid, ubValid;

            if (d > 0)
            {
                uaValid = num_ua >= 0 && num_ua <= d;
                ubValid = num_ub >= 0 && num_ub <= d;
            }
            else
            {
                uaValid = num_ua <= 0 && num_ua >= d;
                ubValid = num_ub <= 0 && num_ub >= d;
            }

            if (uaValid && ubValid)
            {
                // Calculate intersection point
                // x = p1.x + ua * (p2.x - p1.x)
                // x = p1.x + (num_ua / d) * (p2.x - p1.x)
                // Integer division maps to grid

                long offsetX = (num_ua * (p2.X - p1.X)) / d;
                long offsetY = (num_ua * (p2.Y - p1.Y)) / d;

                result = new Core.Data.LogicVector2(
                    p1.X + (int)offsetX,
                    p1.Y + (int)offsetY
                );
                return true;
            }

            return false;
        }
    }
}


