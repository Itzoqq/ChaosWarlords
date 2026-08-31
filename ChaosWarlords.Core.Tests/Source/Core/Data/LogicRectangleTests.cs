using ChaosWarlords.Source.Core.Data;

namespace ChaosWarlords.Core.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class LogicRectangleTests
    {
        [TestMethod]
        public void Center_ReturnsMidpoint()
        {
            var rect = new LogicRectangle(0, 0, 100, 200);

            var center = rect.Center;

            Assert.AreEqual(50, center.X);
            Assert.AreEqual(100, center.Y);
        }

        [TestMethod]
        public void Contains_PointInside_ReturnsTrue()
        {
            var rect = new LogicRectangle(0, 0, 100, 100);

            Assert.IsTrue(rect.Contains(new LogicVector2(50, 50)));
        }

        [TestMethod]
        public void Contains_PointOutside_ReturnsFalse()
        {
            var rect = new LogicRectangle(0, 0, 100, 100);

            Assert.IsFalse(rect.Contains(new LogicVector2(150, 50)));
        }

        [TestMethod]
        public void Contains_PointOnRightOrBottomEdge_ReturnsFalse()
        {
            // Matches Rectangle.Contains semantics: [Left, Right) x [Top, Bottom)
            var rect = new LogicRectangle(0, 0, 100, 100);

            Assert.IsFalse(rect.Contains(new LogicVector2(100, 50)));
            Assert.IsFalse(rect.Contains(new LogicVector2(50, 100)));
        }

        [TestMethod]
        public void Equality_SameValues_AreEqual()
        {
            var a = new LogicRectangle(1, 2, 3, 4);
            var b = new LogicRectangle(1, 2, 3, 4);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
        }
    }
}
