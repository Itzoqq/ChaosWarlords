using System.Text.Json;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;

namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Loads a flat key/value JSON bundle (e.g. Content/data/localization/en_US.json) and
    /// resolves keys against it. Headless - zero MonoGame references, same as CardDatabase.
    /// </summary>
    public class LocalizationManager : ILocalizationService
    {
        private readonly IGameLogger? _logger;
        private Dictionary<string, string> _strings = new();

        public LocalizationManager(IGameLogger? logger = null)
        {
            _logger = logger;
        }

        public void Load(Stream stream)
        {
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            LoadFromJson(json);
        }

        internal void LoadFromJson(string json)
        {
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Merges additional localization strings into an already-loaded bundle - the string-
        /// data counterpart of CardDatabase.LoadAdditionalFromJson, for test-only fixture cards
        /// whose name/description strings must never ship in production en_US.json. Throws if
        /// any key in <paramref name="json"/> collides with a key already loaded.
        /// </summary>
        internal void LoadAdditionalFromJson(string json)
        {
            var additional = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

            // Validate every key BEFORE merging any of them - a mid-batch throw must never
            // leave a partial merge behind (same reasoning as CardDatabase.LoadAdditionalFromJson).
            foreach (var key in additional.Keys)
            {
                if (_strings.ContainsKey(key))
                {
                    throw new InvalidDataException($"Cannot merge fixture localization key '{key}' - it already exists in the bundle.");
                }
            }

            foreach (var (key, value) in additional)
            {
                _strings.Add(key, value);
            }
        }

        public string GetString(string key)
        {
            if (_strings.TryGetValue(key, out var value))
            {
                return value;
            }

            // Visible, non-crashing fallback (see ILocalizationService) - also logged so a
            // missing key surfaces during content authoring rather than only in a screenshot.
            _logger?.Log($"[Localization] Missing key: {key}", LogChannel.Warning);
            return $"[MISSING:{key}]";
        }
    }
}
