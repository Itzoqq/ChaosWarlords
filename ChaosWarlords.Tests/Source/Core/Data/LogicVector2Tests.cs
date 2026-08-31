using ChaosWarlords.Source.Core.Data;

namespace ChaosWarlords.Tests.Source.Core.Data
{
    [TestClass]
    [TestCategory("Unit")]
    public class LogicVector2Tests
    {
        // Vector2 <-> LogicVector2 conversion (ToVector2()/FromVector2()) moved to
        // LogicVectorExtensions in the client project - see LogicVectorExtensionsTests.cs -
        // so this Core-focused suite doesn't need a MonoGame reference.

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
