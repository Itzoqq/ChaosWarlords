using System.Collections.Generic;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for deterministic random number generation.
    /// All random events in the game must use this interface to ensure
    /// reproducible gameplay for multiplayer and replay functionality.
    /// </summary>
    public interface IGameRandom
    {
        /// <summary>
        /// Gets the seed used to initialize this random number generator.
        /// </summary>
        int Seed { get; }

        /// <summary>
        /// Returns a non-negative random integer that is less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound of the random number to be generated.</param>
        /// <returns>A 32-bit signed integer that is greater than or equal to 0, and less than maxValue.</returns>
        int Next(int maxValue);

        /// <summary>
        /// Returns a random integer that is within a specified range.
        /// </summary>
        /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
        /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
        /// <returns>A 32-bit signed integer greater than or equal to minValue and less than maxValue.</returns>
        int Next(int minValue, int maxValue);

        /// <summary>
        /// Shuffles the elements of a list using the Fisher-Yates algorithm.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        void Shuffle<T>(IList<T> list);
    }
}
