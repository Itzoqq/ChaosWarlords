using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Data;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    public class LogicVector2Tests
    {
        [TestMethod]
        public void Conversion_Vector2_To_Logic_IsAccurate()
        {
            var worldPos = new Vector2(123.456f, 789.012f);
            var logic = LogicVector2.FromVector2(worldPos);

            // 123.456 * 1000 = 123456
            Assert.AreEqual(123456, logic.X);
            Assert.AreEqual(789012, logic.Y);
        }

        [TestMethod]
        public void Conversion_Logic_To_Vector2_IsAccurate()
        {
            var logic = new LogicVector2(123456, 789012);
            var world = logic.ToVector2();

            Assert.AreEqual(123.456f, world.X, 0.0001f);
            Assert.AreEqual(789.012f, world.Y, 0.0001f);
        }

        [TestMethod]
        public void DistanceSquared_IsDeterministic()
        {
            var p1 = new LogicVector2(0, 0);
            var p2 = new LogicVector2(3000, 4000); // 3, 4 triangle scaled by 1000

            long distSq = LogicVector2.DistanceSquared(p1, p2);
            
            // 3000^2 + 4000^2 = 9M + 16M = 25M
            Assert.AreEqual(25_000_000L, distSq);
        }

        [TestMethod]
        public void Lerp_IsDeterministic()
        {
            var start = new LogicVector2(0, 0);
            var end = new LogicVector2(1000, 2000);

            // 50%
            var mid = LogicVector2.Lerp(start, end, 1, 2);
            Assert.AreEqual(500, mid.X);
            Assert.AreEqual(1000, mid.Y);

            // 33% (1/3)
            // 1000 / 3 = 333
            // 2000 / 3 = 666
            var third = LogicVector2.Lerp(start, end, 1, 3);
            Assert.AreEqual(333, third.X);
            Assert.AreEqual(666, third.Y);
        }
    }
}
