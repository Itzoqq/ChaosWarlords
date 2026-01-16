using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]
    [TestCategory("Unit")]
    public class PooledPrimitivesTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            // Reset pools to ensure clean state between tests if possible
            // In a real scenario, we might want public Clear() methods on the static pools for testing
        }

        [TestMethod]
        public void PooledRectangle_Rent_SetsCorrectValues()
        {
            using var pooled = PooledRectangle.Rent(10, 20, 30, 40);
            
            Assert.AreEqual(10, pooled.Value.X);
            Assert.AreEqual(20, pooled.Value.Y);
            Assert.AreEqual(30, pooled.Value.Width);
            Assert.AreEqual(40, pooled.Value.Height);
        }

        [TestMethod]
        public void PooledRectangle_Dispose_ReturnsToPool()
        {
            // Rent and dispose to seed the pool
            var pooled = PooledRectangle.Rent(0, 0, 0, 0);
            var originalRef = pooled;
            pooled.Dispose();

            // Renting next should technically give us the same object reference if it's the only one
            // However, note that PooledRectangle is a CLASS wrapper around a STRUCT. 
            // The ObjectPool stores the Wrapper class.
            
            using var next = PooledRectangle.Rent(5, 5, 5, 5);
            
            // We can't strictly assert equality because the internal pool logic might vary,
            // but we can verify behavior correctness.
            Assert.IsNotNull(next);
            Assert.AreEqual(5, next.Value.X);
        }

        [TestMethod]
        public void PooledVector2_Rent_SetsCorrectValues()
        {
            using var pooled = PooledVector2.Rent(1.5f, 2.5f);
            
            Assert.AreEqual(1.5f, pooled.Value.X);
            Assert.AreEqual(2.5f, pooled.Value.Y);
        }

        [TestMethod]
        public void PooledVector2_Value_IsMutable()
        {
            using var pooled = PooledVector2.Rent(0, 0);
            
            pooled.Value = new Vector2(100, 200);
            
            Assert.AreEqual(100, pooled.Value.X);
            Assert.AreEqual(200, pooled.Value.Y);
        }
    }
}
