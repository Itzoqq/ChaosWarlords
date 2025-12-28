using System.Text;

namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// A lightweight wrapper around StringBuilder to prevent frame-by-frame string allocations.
    /// It only rebuilds the string if the tracked value changes.
    /// </summary>
    public class CachedIntText
    {
        public StringBuilder Output { get; private set; }

        private readonly string _prefix;
        private readonly string _suffix;
        private int _lastValue;
        private bool _isDirty;

        public CachedIntText(string prefix, int initialValue = -1, string suffix = "")
        {
            Output = new StringBuilder();
            _prefix = prefix;
            _suffix = suffix;
            _lastValue = initialValue;
            _isDirty = true;

            // Initial build
            Update(initialValue, force: true);
        }

        public void Update(int newValue, bool force = false)
        {
            if (_lastValue != newValue || force || _isDirty)
            {
                _lastValue = newValue;
                Output.Clear();
                Output.Append(_prefix);
                Output.Append(newValue);
                Output.Append(_suffix);
                _isDirty = false;
            }
        }

        // Implicit conversion to StringBuilder for easy use in DrawString
        public static implicit operator StringBuilder(CachedIntText cached) => cached.Output;
    }
}

