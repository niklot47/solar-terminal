using System;

namespace SolarTerminal.Data
{
    /// <summary>
    /// Plain data record matching one row in the JSON / CSV import source.
    /// No Unity types here — this class must survive JSON deserialization
    /// without any editor/runtime dependency.
    ///
    /// All fields are strings or primitives to maximise import tool compatibility.
    /// </summary>
    [Serializable]
    public class BodyImportRecord
    {
        // Identity
        public string id            = "";
        public string parentId      = "";
        public string displayNameKey = "";
        public string bodyType      = "Planet";   // matches BodyType enum name

        // UI metadata
        public bool showInHierarchy = true;
        public int  sortOrder       = 0;
        public bool isSelectable    = true;

        // Visual
        public float visualRadius   = 1f;

        // Keplerian orbital elements
        public float semiMajorAxis           = 0f;
        public float eccentricity            = 0f;
        public float inclination             = 0f;
        public float longitudeOfAscendingNode = 0f;
        public float argumentOfPeriapsis     = 0f;
        public float meanAnomalyAtEpoch      = 0f;
        public float orbitalPeriod           = 0f;

        // Preset key — optional, resolved by importer against preset assets
        // e.g. "Preset_RockyPlanet"
        public string presetKey = "";

        // Prefab paths — optional, resolved via Resources.Load or AssetDatabase
        // Leave empty to assign prefabs manually after import
        public string nearPrefabPath   = "";
        public string mediumPrefabPath = "";
        public string farPrefabPath    = "";
    }

    /// <summary>
    /// Root wrapper for the JSON import file.
    /// Supports an optional system-level header plus the body array.
    /// </summary>
    [Serializable]
    public class OrbitalSystemImportData
    {
        public string systemId       = "";
        public string displayNameKey = "";
        public string centralBodyId  = "";
        public BodyImportRecord[] bodies = Array.Empty<BodyImportRecord>();
    }
}
