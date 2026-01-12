using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]
    public class MapGeometryTests
    {
        [TestMethod]
        public void CalculateBounds_ReturnsCorrectMinMax()
        {
            // Removed null texture arg
            var nodes = new List<MapNode>
            {
                new MapNode(1, new Vector2(0, 0)),
                new MapNode(2, new Vector2(100, 50)),
                new MapNode(3, new Vector2(-50, 200))
            };

            // CalculateBounds now returns ints (scaled)
            var bounds = MapGeometry.CalculateBounds(nodes);

            Assert.AreEqual(-50 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, bounds.MinX);
            Assert.AreEqual(0, bounds.MinY);
            Assert.AreEqual(100 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, bounds.MaxX);
            Assert.AreEqual(200 * ChaosWarlords.Source.Core.Data.LogicVector2.ScaleFactor, bounds.MaxY);
        }

        [TestMethod]
        public void TryGetLineIntersection_DetectsCrossing()
        {
            var p1 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(0, 0));
            var p2 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(100, 100));
            var p3 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(0, 100));
            var p4 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(100, 0));

            bool intersects = MapGeometry.TryGetLineIntersection(p1, p2, p3, p4, out var result);

            Assert.IsTrue(intersects);
            var expected = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(50, 50));
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TryGetLineIntersection_ReturnsFalseForParallel()
        {
            var p1 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(0, 0));
            var p2 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(100, 0));
            var p3 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(0, 10));
            var p4 = ChaosWarlords.Source.Core.Data.LogicVector2.FromVector2(new Vector2(100, 10));

            bool intersects = MapGeometry.TryGetLineIntersection(p1, p2, p3, p4, out _);

            Assert.IsFalse(intersects);
        }
    }
}


