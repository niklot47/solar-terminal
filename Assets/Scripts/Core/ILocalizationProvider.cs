namespace SolarTerminal.Core
{
    /// <summary>
    /// Minimal contract for any localization backend.
    /// The UI layer depends only on this interface, never on a concrete class.
    /// Swap implementations without touching UI code.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>Return localized string for key. Falls back to key itself if missing.</summary>
        string Get(string key);
    }
}
