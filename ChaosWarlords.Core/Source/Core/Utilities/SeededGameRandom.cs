using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Deterministic random number generator using a seed.
    /// This ensures that the same seed always produces the same sequence of random numbers,
    /// which is critical for multiplayer synchronization and replay functionality.
    ///
    /// Built on Pcg32 (see its own doc comment) rather than System.Random - .NET's Random
    /// doesn't guarantee its algorithm/output stays identical across .NET versions, only that
    /// it stays deterministic within a fixed one. Pcg32 is a from-scratch, fully-specified
    /// algorithm with no runtime dependency, so a seed's sequence is stable forever, on any
    /// platform, independent of which .NET version produced it. See planning.txt.
    ///
    /// For multiplayer games: The server generates a seed at match start and sends it to all clients.
    /// All clients initialize their SeededGameRandom with this same seed, ensuring identical RNG sequences.
    /// </summary>
    public class SeededGameRandom : IGameRandom
    {
        private readonly Pcg32 _rng;

        public int Seed { get; }

        /// <summary>
        /// Initializes a new instance of the SeededGameRandom class with the specified seed.
        /// </summary>
        /// <param name="seed">The seed value for the random number generator.</param>
        public SeededGameRandom(int seed, IGameLogger logger)
        {
            Seed = seed;
            // Widen to Pcg32's 64-bit state space. unchecked + explicit cast, not implicit
            // conversion, so a negative seed (a valid int, and this project's seeds are
            // ordinary ints - see MatchFactory/WorldData.Seed) maps deterministically instead
            // of throwing OverflowException in a checked build configuration.
            _rng = new Pcg32(unchecked((ulong)(long)seed), 0);
            logger?.Log($"Game RNG initialized with seed: {seed}", LogChannel.Info);
        }

        public int CallCount { get; private set; }

        /// <inheritdoc/>
        public int NextInt(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be non-negative.");
            CallCount++;
            return (int)_rng.NextBoundedUInt32((uint)maxValue);
        }

        /// <inheritdoc/>
        public int NextInt(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must not exceed maxValue.");
            CallCount++;
            uint range = unchecked((uint)((long)maxValue - minValue));
            return unchecked(minValue + (int)_rng.NextBoundedUInt32(range));
        }

        /// <inheritdoc/>
        public void Shuffle<T>(IList<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            // Fisher-Yates shuffle algorithm
            int n = list.Count;
            while (n > 1)
            {
                n--;
                CallCount++;
                int k = (int)_rng.NextBoundedUInt32((uint)(n + 1));
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
