using UnityEngine;

namespace SolarTerminal.Data
{
    /// <summary>
    /// Reusable template that defines default field values for a category of body.
    /// Content authors apply a preset when creating a new body definition to avoid
    /// filling every field from scratch.
    ///
    /// Presets do NOT override manually authored values after application.
    /// They are a starting point, not a binding constraint.
    ///
    /// Create: Assets > Create > SolarTerminal > BodyTypePreset
    /// Recommended names: Preset_Star, Preset_RockyPlanet, Preset_GasGiant, etc.
    /// </summary>
    [CreateAssetMenu(
        menuName = "SolarTerminal/BodyTypePreset",
        fileName = "Preset_New")]
    public class BodyTypePreset : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        // PRESET IDENTITY
        // ══════════════════════════════════════════════════════════════════

        [Header("Preset Identity")]

        [Tooltip("Human-readable preset name for editor menus. Not used at runtime.")]
        public string presetLabel = "New Preset";

        [Tooltip("Body type this preset is intended for.")]
        public BodyType bodyType = BodyType.Planet;

        // ══════════════════════════════════════════════════════════════════
        // UI / HIERARCHY DEFAULTS
        // ══════════════════════════════════════════════════════════════════

        [Header("UI Defaults")]
        public bool showInHierarchy = true;
        public bool isSelectable    = true;
        public int  sortOrder       = 0;

        // ══════════════════════════════════════════════════════════════════
        // VISUAL DEFAULTS
        // ══════════════════════════════════════════════════════════════════

        [Header("Visual Defaults")]
        public float visualRadius = 1f;

        [Tooltip("Default near prefab for this category. Override per-body as needed.")]
        public GameObject defaultNearPrefab;

        [Tooltip("Default far marker prefab for this category.")]
        public GameObject defaultFarPrefab;

        // ══════════════════════════════════════════════════════════════════
        // ORBITAL DEFAULTS  (useful for belts and fixed categories)
        // ══════════════════════════════════════════════════════════════════

        [Header("Orbital Defaults (optional)")]

        [Tooltip("Default eccentricity if none supplied in import data.")]
        [Range(0f, 0.99f)]
        public float defaultEccentricity = 0f;

        [Tooltip("Default inclination (radians).")]
        public float defaultInclination = 0f;

        // ══════════════════════════════════════════════════════════════════
        // APPLICATION HELPER  (used by importer and editor tools)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply this preset's defaults to a definition.
        /// Only writes fields that are unset / at their zero-value,
        /// so existing manual data is preserved.
        /// </summary>
        public void ApplyDefaults(CelestialBodyDefinition def)
        {
            def.bodyType = bodyType;

            // UI defaults are always applied (low-risk overwrite)
            def.showInHierarchy = showInHierarchy;
            def.isSelectable    = isSelectable;
            if (def.sortOrder == 0) def.sortOrder = sortOrder;

            // Visual radius only if not yet set
            if (def.visualRadius <= 0f) def.visualRadius = visualRadius;

            // Prefab references only if empty
            if (def.nearPrefab == null && defaultNearPrefab != null)
                def.nearPrefab = defaultNearPrefab;
            if (def.farPrefab == null && defaultFarPrefab != null)
                def.farPrefab = defaultFarPrefab;

            // Orbital defaults — only if body has no orbit set yet
            if (!def.HasOrbit)
            {
                if (def.eccentricity == 0f) def.eccentricity = defaultEccentricity;
                if (def.inclination  == 0f) def.inclination  = defaultInclination;
            }

            def.sourcePreset = this;
        }
    }
}
