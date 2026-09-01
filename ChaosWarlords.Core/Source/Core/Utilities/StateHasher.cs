using System.Text;

namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Provides deterministic hashing algorithms for game state verification.
    /// Replaces standard .NET GetHashCode which is not stable across platforms/versions.
    /// Uses FNV-1a algorithm for mixing.
    /// </summary>
    public static class StateHasher
    {
        private const int FnvOffsetBasis = -2128831035; // 0x811C9DC5 as signed int
        private const int FnvPrime = 16777619; // 0x01000193

        public static int ComputeHash(params object[] components)
        {
            int hash = FnvOffsetBasis;

            foreach (var component in components)
            {
                hash = Mix(hash, component);
            }

            return hash;
        }

        public static int Init() => FnvOffsetBasis;

        public static int Mix(int hash, object? component)
        {
            unchecked
            {
                hash ^= ToMixableInt(component);
                hash *= FnvPrime;
                return hash;
            }
        }

        /// <summary>
        /// Reduces a component to the single int Mix actually folds into the running hash -
        /// split out of Mix itself so the FNV-1a mixing arithmetic (the part that must never
        /// change without breaking every existing replay recording) sits apart from the type
        /// dispatch (which can safely gain a new case, e.g. a future numeric type, without
        /// touching the arithmetic at all).
        /// </summary>
        private static int ToMixableInt(object? component) => component switch
        {
            null => 0,
            int i => i,
            long l => (int)(l ^ (l >> 32)),
            bool b => b ? 1 : 0,
            Enum e => Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture),
            string s => HashString(s),
            // Fallback for complex objects - ideally shouldn't happen in strict mode. But we
            // can recurse or use a specific interface if we had one, i.e. IDeterministicHashable.
            _ => 0,
        };

        private static int HashString(string s)
        {
            unchecked
            {
                int hash = FnvOffsetBasis;
                // Treat string as sequence of bytes (UTF8) or chars for consistency
                foreach (char c in s)
                {
                    hash ^= c;
                    hash *= FnvPrime;
                }
                return hash;
            }
        }
    }
}
