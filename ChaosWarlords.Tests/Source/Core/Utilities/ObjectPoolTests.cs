using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Utilities;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]
    [TestCategory("Unit")]
    public class ObjectPoolTests
    {
        private class TestObject
        {
            public int Id { get; set; }
        }

        [TestMethod]
        public void Rent_ReturnsNonNullInstance()
        {
            var pool = new ObjectPool<TestObject>();
            var obj = pool.Rent();
            
            Assert.IsNotNull(obj);
        }

        [TestMethod]
        public void Rent_ReturnsNewInstances_WhenPoolEmpty()
        {
            // Capacity 0, so pool starts empty
            var pool = new ObjectPool<TestObject>(0, 10);
            
            var obj1 = pool.Rent();
            var obj2 = pool.Rent();

            Assert.IsNotNull(obj1);
            Assert.IsNotNull(obj2);
            Assert.AreNotSame(obj1, obj2);
        }

        [TestMethod]
        public void Return_AddsObjectBackToPool()
        {
            var pool = new ObjectPool<TestObject>(0, 10);
            var obj = new TestObject();

            pool.Return(obj);
            
            Assert.AreEqual(1, pool.AvailableCount);
        }

        [TestMethod]
        public void Rent_ReusesReturnedObject()
        {
            var pool = new ObjectPool<TestObject>(0, 10);
            var original = new TestObject();
            
            pool.Return(original);
            var rented = pool.Rent();

            Assert.AreSame(original, rented);
            Assert.AreEqual(0, pool.AvailableCount);
        }

        [TestMethod]
        public void Return_DoesNotExceedMaxSize()
        {
            // Max size 1
            var pool = new ObjectPool<TestObject>(0, 1);
            
            var obj1 = new TestObject();
            var obj2 = new TestObject();

            pool.Return(obj1);
            pool.Return(obj2); // Should be ignored/dropped

            Assert.AreEqual(1, pool.AvailableCount);
        }

        [TestMethod]
        public void Clear_EmptiesPool()
        {
            var pool = new ObjectPool<TestObject>(5, 10);
            Assert.AreEqual(5, pool.AvailableCount);

            pool.Clear();
            
            Assert.AreEqual(0, pool.AvailableCount);
        }

        [TestMethod]
        public void Constructor_PreAllocatesObjects()
        {
            var pool = new ObjectPool<TestObject>(10, 20);
            
            Assert.AreEqual(10, pool.AvailableCount);
        }
    }
}
