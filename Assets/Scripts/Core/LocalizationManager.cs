using System.Collections.Generic;
using UnityEngine;

namespace SolarTerminal.Core
{
    /// <summary>
    /// Minimal localization system.
    /// Loads a JSON file from Resources/Localization/{locale}.json on first use.
    ///
    /// Usage:
    ///   LocalizationManager.Get("ui.panel.objects")  →  "Объекты"
    ///
    /// To add a new language: create Resources/Localization/en.json with the same keys.
    /// To switch language at runtime: call LocalizationManager.SetLocale("en").
    /// </summary>
    public static class LocalizationManager
    {
        // Default locale — change here or call SetLocale() before any Get()
        private static string _locale = "ru";

        private static Dictionary<string, string> _strings;
        private static bool _loaded = false;

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Return localized string for key.
        /// Falls back to the key itself if not found (visible in UI — easy to spot missing keys).
        /// </summary>
        public static string Get(string key)
        {
            EnsureLoaded();

            if (_strings.TryGetValue(key, out var value))
                return value;

            Debug.LogWarning($"[Localization] Missing key: '{key}' in locale '{_locale}'");
            return key;
        }

        /// <summary>Switch locale and reload strings.</summary>
        public static void SetLocale(string locale)
        {
            _locale = locale;
            _loaded = false;
            _strings = null;
        }

        // ------------------------------------------------------------------
        // Internal
        // ------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            _strings = new Dictionary<string, string>();
            _loaded  = true;

            // Load from Resources/Localization/{locale}.json
            var path   = $"Localization/{_locale}";
            var asset  = Resources.Load<TextAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"[Localization] File not found: Resources/{path}.json");
                return;
            }

            ParseJson(asset.text);
            Debug.Log($"[Localization] Loaded '{_locale}' — {_strings.Count} strings.");
        }

        /// <summary>
        /// Minimal JSON parser for flat key-value string objects.
        /// Does not require any external JSON library.
        /// Format: { "key": "value", ... }
        /// </summary>
        private static void ParseJson(string json)
        {
            // Strip outer braces
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}"))   json = json.Substring(0, json.Length - 1);

            // Split by lines, parse each "key": "value" pair
            var lines = json.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Find the colon separating key from value
                int colon = line.IndexOf(':');
                if (colon < 0) continue;

                var key   = ExtractString(line.Substring(0, colon).Trim());
                var value = ExtractString(line.Substring(colon + 1).Trim());

                if (key != null && value != null)
                    _strings[key] = value;
            }
        }

        /// <summary>Extract content between first and last double-quote.</summary>
        private static string ExtractString(string token)
        {
            int first = token.IndexOf('"');
            int last  = token.LastIndexOf('"');
            if (first < 0 || last <= first) return null;
            return token.Substring(first + 1, last - first - 1);
        }
    }
}
