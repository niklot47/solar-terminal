namespace SolarTerminal.Core
{
    /// <summary>
    /// Bridges the static LocalizationManager into the ILocalizationProvider interface.
    /// Used by all UI controllers so they stay decoupled from the concrete manager.
    ///
    /// To plug in Unity Localization Package or any other system later:
    /// create a new class that implements ILocalizationProvider and pass it
    /// to OrbitalMapUIDocumentController instead.
    /// </summary>
    public class DefaultLocalizationProvider : ILocalizationProvider
    {
        public string Get(string key) => LocalizationManager.Get(key);
    }
}
