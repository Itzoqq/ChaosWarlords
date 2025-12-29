using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Core.Utilities
{
    [TestClass]

    [TestCategory("Unit")]
    public class SeededGameRandomTests
    {
        [TestMethod]
        public void Next_WithSameSeed_ProducesSameSequence()
        {
            int seed = 12345;
            var rng1 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var rng2 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(rng1.NextInt(100), rng2.NextInt(100));
            }
        }

        [TestMethod]
        public void Next_Range_ProducesSameSequence()
        {
            int seed = 98765;
            var rng1 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var rng2 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(rng1.NextInt(50, 150), rng2.NextInt(50, 150));
            }
        }

        [TestMethod]
        public void Shuffle_WithSameSeed_ProducesSameOrder()
        {
            int seed = 42;

            var list1 = Enumerable.Range(0, 50).ToList();
            var list2 = Enumerable.Range(0, 50).ToList();

            var rng1 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var rng2 = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            rng1.Shuffle(list1);
            rng2.Shuffle(list2);

            CollectionAssert.AreEqual(list1, list2);
        }

        [TestMethod]
        public void Seed_Property_ReturnsInitializedValue()
        {
            int seed = 1337;
            var rng = new SeededGameRandom(seed, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            Assert.AreEqual(seed, rng.Seed);
        }

        [TestMethod]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            // Note: Theoretically they COULD produce the same sequence, 
            // but for a large enough range and sequence length, it's virtually impossible.
            var rng1 = new SeededGameRandom(1, ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var rng2 = new SeededGameRandom(2, ChaosWarlords.Tests.Utilities.TestLogger.Instance);

            bool matches = true;
            for (int i = 0; i < 20; i++)
            {
                if (rng1.NextInt(1000000) != rng2.NextInt(1000000))
                {
                    matches = false;
                    break;
                }
            }

            Assert.IsFalse(matches, "Sequences from different seeds should not match.");
        }
    }
}
