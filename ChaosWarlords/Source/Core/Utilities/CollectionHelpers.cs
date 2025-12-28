using System;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Utility methods for collection operations.
    /// </summary>
    public static class CollectionHelpers
    {
        private static readonly Random _rng = new Random();

        /// <summary>
        /// Shuffles a list in-place using the Fisher-Yates algorithm.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        /// <exception cref="ArgumentNullException">Thrown when list is null.</exception>
        public static void Shuffle<T>(this List<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]); // Modern C# tuple swap
            }
        }
    }
}


