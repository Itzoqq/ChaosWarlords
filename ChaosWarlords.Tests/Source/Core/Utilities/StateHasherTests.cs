using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    /// <summary>
    /// StateHasher had zero test coverage before this file, despite now being the sole
    /// implementation MatchContext.GetStateHash() builds on (see its doc comment) - the
    /// mechanism multiplayer/replay desync detection depends on. A silent regression here
    /// (e.g. a string field starting to use string.GetHashCode() again, which is randomized
    /// per-process by default in modern .NET) would defeat that detection without any test
    /// noticing, since the symptom only shows up as two DIFFERENT processes disagreeing -
    /// invisible to a single-process test suite unless it's checked explicitly.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class StateHasherTests
    {
        [TestMethod]
        public void ComputeHash_IsDeterministic_ForIdenticalInputs()
        {
            int hash1 = StateHasher.ComputeHash(1, "card_id", true, LogChannel.Info);
            int hash2 = StateHasher.ComputeHash(1, "card_id", true, LogChannel.Info);

            Assert.AreEqual(hash1, hash2);
        }

        [TestMethod]
        public void ComputeHash_DiffersForDifferentInt()
        {
            Assert.AreNotEqual(StateHasher.ComputeHash(1), StateHasher.ComputeHash(2));
        }

        [TestMethod]
        public void ComputeHash_DiffersForDifferentLong()
        {
            Assert.AreNotEqual(StateHasher.ComputeHash(1L), StateHasher.ComputeHash(2L));
        }

        [TestMethod]
        public void ComputeHash_DiffersForDifferentBool()
        {
            Assert.AreNotEqual(StateHasher.ComputeHash(true), StateHasher.ComputeHash(false));
        }

        [TestMethod]
        public void ComputeHash_DiffersForDifferentEnum()
        {
            Assert.AreNotEqual(StateHasher.ComputeHash(LogChannel.Info), StateHasher.ComputeHash(LogChannel.Warning));
        }

        [TestMethod]
        public void ComputeHash_DiffersForDifferentString()
        {
            Assert.AreNotEqual(StateHasher.ComputeHash("card_a"), StateHasher.ComputeHash("card_b"));
        }

        [TestMethod]
        public void ComputeHash_TreatsNullComponentAsZero()
        {
            Assert.AreEqual(StateHasher.ComputeHash((object)null!), StateHasher.ComputeHash(0));
        }

        [TestMethod]
        public void ComputeHash_OrderMatters()
        {
            // FNV-1a-style mixing must be order-sensitive - two states with the same fields
            // in a different order (e.g. map nodes iterated in a different sequence) must
            // not accidentally collide to the same hash.
            Assert.AreNotEqual(StateHasher.ComputeHash(1, 2), StateHasher.ComputeHash(2, 1));
        }

        [TestMethod]
        public void ComputeHash_StringHashing_DoesNotUseNetStringGetHashCode()
        {
            // Regression test for the actual bug this class exists to avoid: string.GetHashCode()
            // is randomized per-process by default in modern .NET (a DoS mitigation), so if
            // Mix ever fell back to it for strings, the SAME logical state would hash
            // differently across two processes (e.g. a server and a client) - exactly the
            // scenario StateHasher is meant to make safe. HashString is a manual per-char
            // FNV-1a mix, so it must match .NET's own (in-process) computation for
            // "card_id".GetHashCode() only by coincidence, not by construction - assert they
            // actually differ so nobody "simplifies" HashString back into a
            // string.GetHashCode() call believing it's equivalent.
            const string sample = "cultist_of_myrkul";
            int viaStateHasher = StateHasher.ComputeHash(sample);
            int viaNetGetHashCode = StateHasher.Init();
            unchecked
            {
                viaNetGetHashCode ^= sample.GetHashCode();
                viaNetGetHashCode *= 16777619;
            }

            Assert.AreNotEqual(viaNetGetHashCode, viaStateHasher,
                "This would only fail if HashString happened to produce the exact same " +
                "int as string.GetHashCode() for this input - vanishingly unlikely unless " +
                "HashString was rewritten to just call string.GetHashCode() directly, which " +
                "is the actual regression this test exists to catch.");
        }

        [TestMethod]
        public void Mix_IsUsedConsistently_WithComputeHash()
        {
            // ComputeHash(params object[]) is sugar over repeated Mix calls starting from
            // Init() - verify that equivalence explicitly, since MatchContext.GetStateHash()
            // calls Mix directly (it can't use ComputeHash's params array without allocating
            // one every call) and both must agree.
            int viaComputeHash = StateHasher.ComputeHash(42, "x", false);

            int viaMix = StateHasher.Init();
            viaMix = StateHasher.Mix(viaMix, 42);
            viaMix = StateHasher.Mix(viaMix, "x");
            viaMix = StateHasher.Mix(viaMix, false);

            Assert.AreEqual(viaComputeHash, viaMix);
        }

        [TestMethod]
        public void Mix_ComplexObjectFallback_IsDocumentedAsZero_NotAsError()
        {
            // KNOWN LIMITATION (see StateHasher.Mix's own comment): a component that isn't
            // int/long/bool/enum/string/null mixes in as 0 rather than throwing or being
            // rejected - meaning two states differing ONLY in such a field would collide.
            // Nothing in MatchContext.GetStateHash() currently passes a complex object (it
            // always passes primitives/IDs/counts), so this isn't reachable today, but it's
            // a real footgun for whoever adds a new hash contribution later without reading
            // this comment. Documenting the current behavior explicitly here rather than
            // leaving it to be rediscovered by a silent desync-detection blind spot.
            var complexObject = new List<int> { 1, 2, 3 };

            Assert.AreEqual(StateHasher.ComputeHash(complexObject), StateHasher.ComputeHash((object)null!));
        }
    }
}
