namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// A minimal, from-scratch implementation of PCG32 (the "XSH-RR" variant), the reference
    /// algorithm from O'Neill, M.E., "PCG: A Family of Simple Fast Space-Efficient Statistically
    /// Good Algorithms for Random Number Generation" (pcg-random.org). Public domain, fully
    /// specified by the ~15 lines of arithmetic below - nothing here depends on any runtime or
    /// platform library, so a given seed produces the exact same output sequence forever, on
    /// any OS, regardless of .NET version.
    ///
    /// This exists because SeededGameRandom used to wrap System.Random directly.
    /// System.Random IS deterministic for a fixed seed within one .NET version/runtime, but
    /// .NET explicitly does NOT guarantee its algorithm stays the same across .NET versions -
    /// only that it stays deterministic within whichever version produced a given sequence.
    /// That's a real risk for this project specifically: once a server and a client (or two
    /// server instances) could ever run on different .NET versions, a routine upgrade on just
    /// one side would silently desync every RNG-derived piece of state (deck shuffles, market
    /// generation) - no exception, no error, just quietly wrong game state. See planning.txt.
    /// </summary>
    internal sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <param name="initState">Arbitrary seed value (any 64-bit value is valid).</param>
        /// <param name="initSeq">Selects which of PCG32's independent output streams this
        /// generator walks - two instances with the same initState but different initSeq
        /// produce different sequences. A fixed constant is fine here (SeededGameRandom only
        /// ever needs one stream per seed), so callers just pass initSeq: 0.</param>
        public Pcg32(ulong initState, ulong initSeq)
        {
            _increment = (initSeq << 1) | 1UL;
            _state = 0UL;
            NextUInt32(); // Per the reference algorithm: advance once before folding in initState...
            _state = unchecked(_state + initState);
            NextUInt32(); // ...then advance again so initState fully mixes into the stream.
        }

        /// <summary>Returns the next raw 32-bit output. Full period 2^64, uniformly distributed
        /// over [0, 2^32).</summary>
        public uint NextUInt32()
        {
            ulong oldState = _state;
            _state = unchecked(oldState * Multiplier + _increment);

            // XSH-RR: xorshift the high bits down, then rotate by the top 5 bits of the
            // PRE-advance state - this rotation is what gives PCG its statistical strength
            // despite the trivially-invertible linear congruential step above.
            uint xorShifted = unchecked((uint)(((oldState >> 18) ^ oldState) >> 27));
            int rot = (int)(oldState >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>
        /// Returns a uniformly-distributed value in [0, exclusiveBound), with no modulo bias -
        /// the "Debiased Modulo (Once) - the OpenBSD Way" rejection scheme from pcg-random.org
        /// (the same one OpenBSD's arc4random_uniform uses): reject draws that fall below
        /// (2^32 mod bound) so every valid output class has an EQUAL number of raw draws
        /// mapping to it, not just an approximately-equal one like plain "raw % bound" gives.
        /// </summary>
        public uint NextBoundedUInt32(uint exclusiveBound)
        {
            if (exclusiveBound == 0) return 0;

            uint threshold = unchecked((uint)-(int)exclusiveBound) % exclusiveBound;
            while (true)
            {
                uint candidate = NextUInt32();
                if (candidate >= threshold) return candidate % exclusiveBound;
            }
        }
    }
}
