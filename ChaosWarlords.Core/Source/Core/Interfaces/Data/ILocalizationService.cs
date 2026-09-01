namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    /// <summary>
    /// Resolves localization keys (e.g. "wight_name", "wight_description") to display text.
    /// Card.Id-derived keys are the only convention used today - see CardFactory - but any
    /// other flat key can be resolved through the same GetString(key) call.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Loads a flat key/value JSON bundle (e.g. Content/data/localization/en_US.json).
        /// Replaces any previously loaded bundle.
        /// </summary>
        void Load(Stream stream);

        /// <summary>
        /// Resolves a key to its display text. A missing key is never a crash or a silent
        /// blank string - it returns a visible "[MISSING:key]" placeholder so a missing
        /// localization entry fails loudly during content authoring instead of shipping a
        /// blank card name.
        /// </summary>
        string GetString(string key);
    }
}
