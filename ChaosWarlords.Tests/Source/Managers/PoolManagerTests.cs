using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Tests.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class PoolManagerTests
    {
        private class TestItem { }

        [TestMethod]
        public void GetOrCreatePool_CreatesNewPool_IfNoneExists()
        {
            using var manager = new PoolManager();
            
            var pool = manager.GetOrCreatePool<TestItem>("client");
            
            Assert.IsNotNull(pool);
            Assert.AreEqual(1, manager.ActiveContextCount);
        }

        [TestMethod]
        public void GetOrCreatePool_ReturnsExistingPool_ForSameKeyAndType()
        {
            using var manager = new PoolManager();
            
            var pool1 = manager.GetOrCreatePool<TestItem>("client");
            var pool2 = manager.GetOrCreatePool<TestItem>("client");
            
            Assert.AreSame(pool1, pool2);
            Assert.AreEqual(1, manager.ActiveContextCount);
        }

        [TestMethod]
        public void GetOrCreatePool_ReturnsDifferentPools_ForDifferentKeys()
        {
            using var manager = new PoolManager();
            
            var clientPool = manager.GetOrCreatePool<TestItem>("client");
            var serverPool = manager.GetOrCreatePool<TestItem>("server");
            
            Assert.AreNotSame(clientPool, serverPool);
            Assert.AreEqual(2, manager.ActiveContextCount);
        }

        [TestMethod]
        public void ClearContext_RemovesOnlySpecificContextPools()
        {
            using var manager = new PoolManager();
            
            manager.GetOrCreatePool<TestItem>("client");
            manager.GetOrCreatePool<TestItem>("server");
            manager.GetOrCreatePool<object>("client"); // Different type, same context

            Assert.AreEqual(3, manager.ActiveContextCount);

            manager.ClearContext("client");

            Assert.AreEqual(1, manager.ActiveContextCount); // Only server pool remains
        }

        [TestMethod]
        public void ClearAll_RemovesAllPools()
        {
            using var manager = new PoolManager();
            
            manager.GetOrCreatePool<TestItem>("client");
            manager.GetOrCreatePool<TestItem>("server");

            manager.ClearAll();

            Assert.AreEqual(0, manager.ActiveContextCount);
        }
    }
}
