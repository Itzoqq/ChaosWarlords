using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using System;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Deterministic random number generator using a seed.
    /// This ensures that the same seed always produces the same sequence of random numbers,
    /// which is critical for multiplayer synchronization and replay functionality.
    /// </summary>
    public class SeededGameRandom : IGameRandom
    {
        private readonly Random _rng;

        public int Seed { get; }

        /// <summary>
        /// Initializes a new instance of the SeededGameRandom class with the specified seed.
        /// </summary>
        /// <param name="seed">The seed value for the random number generator.</param>
        public SeededGameRandom(int seed)
        {
            Seed = seed;
            _rng = new Random(seed);
            GameLogger.Log($"Game RNG initialized with seed: {seed}", LogChannel.Info);
        }

        /// <inheritdoc/>
        public int Next(int maxValue)
        {
            return _rng.Next(maxValue);
        }

        /// <inheritdoc/>
        public int Next(int minValue, int maxValue)
        {
            return _rng.Next(minValue, maxValue);
        }

        /// <inheritdoc/>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            // Fisher-Yates shuffle algorithm
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
