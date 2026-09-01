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
