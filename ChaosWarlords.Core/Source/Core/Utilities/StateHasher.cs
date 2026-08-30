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
                int valueToMix = 0;

                if (component == null)
                {
                    valueToMix = 0;
                }
                else if (component is int i)
                {
                    valueToMix = i;
                }
                else if (component is long l)
                {
                    valueToMix = (int)(l ^ (l >> 32));
                }
                else if (component is bool b)
                {
                    valueToMix = b ? 1 : 0;
                }
                else if (component is Enum e)
                {
                    valueToMix = Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (component is string s)
                {
                    valueToMix = HashString(s);
                }
                else
                {
                    // Fallback for complex objects - ideally shouldn't happen in strict mode
                    // But we can recurse or use a specific interface if we had one i.e IDeterministicHashable
                    valueToMix = 0; 
                }

                hash ^= valueToMix;
                hash *= FnvPrime;
                return hash;
            }
        }

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
