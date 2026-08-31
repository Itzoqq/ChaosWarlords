using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Core.Tests.Source.Core.Utilities
{
    /// <summary>
    /// Pcg32 replaced System.Random as SeededGameRandom's underlying engine (see planning.txt
    /// - .NET doesn't guarantee Random's algorithm stays identical across .NET versions).
    /// These tests exercise the raw generator directly, independent of SeededGameRandom's own
    /// tests (which only ever compare two instances against each other, not against any
    /// property of the algorithm itself) - catching a broken/degenerate implementation
    /// (constant output, badly skewed distribution, out-of-range bounded values) that a purely
    /// relative "same seed -> same sequence" test would pass even if the underlying algorithm
    /// were garbage.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class Pcg32Tests
    {
        [TestMethod]
        public void NextUInt32_SameSeed_ProducesIdenticalSequence()
        {
            var rng1 = new Pcg32(12345, 0);
            var rng2 = new Pcg32(12345, 0);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(rng1.NextUInt32(), rng2.NextUInt32());
            }
        }

        [TestMethod]
        public void NextUInt32_DifferentSeeds_ProduceDifferentSequences()
        {
            var rng1 = new Pcg32(1, 0);
            var rng2 = new Pcg32(2, 0);

            bool anyDiffered = false;
            for (int i = 0; i < 20; i++)
            {
                if (rng1.NextUInt32() != rng2.NextUInt32())
                {
                    anyDiffered = true;
                    break;
                }
            }

            Assert.IsTrue(anyDiffered, "Different seeds should not produce an identical sequence.");
        }

        [TestMethod]
        public void NextUInt32_IsNotConstant()
        {
            // Guards against a degenerate implementation (e.g. a state-update bug that leaves
            // _state unchanged) slipping past the "same seed -> same sequence" tests, which
            // would trivially still pass if every draw were the same fixed value.
            var rng = new Pcg32(999, 0);

            var seen = new HashSet<uint>();
            for (int i = 0; i < 100; i++)
            {
                seen.Add(rng.NextUInt32());
            }

            Assert.IsGreaterThan(90, seen.Count, "100 draws from a working 32-bit PRNG should be overwhelmingly distinct.");
        }

        [TestMethod]
        public void NextBoundedUInt32_AlwaysWithinExclusiveBound()
        {
            var rng = new Pcg32(42, 0);
            const uint bound = 37; // deliberately not a power of 2, to exercise the rejection path

            for (int i = 0; i < 5000; i++)
            {
                uint value = rng.NextBoundedUInt32(bound);
                Assert.IsLessThan(bound, value);
            }
        }

        [TestMethod]
        public void NextBoundedUInt32_ZeroBound_ReturnsZero()
        {
            var rng = new Pcg32(1, 0);
            Assert.AreEqual(0u, rng.NextBoundedUInt32(0));
        }

        [TestMethod]
        public void NextBoundedUInt32_RoughlyUniform_NoBucketWildlyOverOrUnderRepresented()
        {
            // Coarse distribution sanity check, not a rigorous statistical test: with 20000
            // draws over 10 buckets, each bucket's expected count is 2000 - a working
            // generator should land every bucket within +/-25% of that. This is loose enough
            // to never flake on a correct implementation, but would reliably catch a gross
            // bias bug (e.g. forgetting the XSH-RR step and using the raw LCG state, which
            // is heavily biased in its low bits).
            var rng = new Pcg32(2026, 0);
            const uint bucketCount = 10;
            const int draws = 20000;
            var buckets = new int[bucketCount];

            for (int i = 0; i < draws; i++)
            {
                buckets[rng.NextBoundedUInt32(bucketCount)]++;
            }

            int expected = draws / (int)bucketCount;
            int tolerance = expected / 4;
            for (int b = 0; b < bucketCount; b++)
            {
                Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(buckets[b] - expected),
                    $"Bucket {b} had {buckets[b]} draws, expected ~{expected} (+/-{tolerance}) - distribution looks biased.");
            }
        }
    }
}
