using UnityEngine;

namespace SolarTerminal.Data
{
    /// <summary>
    /// Authoritative data definition for one celestial body.
    /// ScriptableObject — one asset per body.
    ///
    /// Responsibilities:
    ///   - Identity and hierarchy membership
    ///   - Localization key (never a raw display string)
    ///   - Keplerian orbital elements
    ///   - Representation prefab references (near / medium / far)
    ///   - UI/tree metadata (visibility, sort order, selectability)
    ///
    /// Does NOT contain runtime state (see OrbitalBodyState).
    /// Does NOT contain simulation logic.
    ///
    /// Create: Assets > Create > SolarTerminal > CelestialBodyDefinition
    /// </summary>
    [CreateAssetMenu(
        menuName = "SolarTerminal/CelestialBodyDefinition",
        fileName = "Body_New")]
    public class CelestialBodyDefinition : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        // IDENTITY
        // ══════════════════════════════════════════════════════════════════

        [Header("Identity")]

        [Tooltip("Globally unique body id. Use snake_case, e.g. 'sol_earth'. " +
                 "Never change after assets reference this id.")]
        public string id;

        [Tooltip("ID of the parent body. Empty only for the root body (star/barycenter).")]
        public string parentId;

        [Tooltip("Localization key for the display name, e.g. 'body.sol.earth'. " +
                 "Never use a raw string as the authoritative name.")]
        public string displayNameKey;

        [Tooltip("Body category. Controls preset defaults, marker colors, and future gameplay rules.")]
        public BodyType bodyType = BodyType.Planet;

        // ══════════════════════════════════════════════════════════════════
        // UI / HIERARCHY METADATA
        // ══════════════════════════════════════════════════════════════════

        [Header("UI / Hierarchy")]

        [Tooltip("Show this body in the object tree panel. " +
                 "Set false for minor bodies, belt aggregates, background objects.")]
        public bool showInHierarchy = true;

        [Tooltip("Determines display order within the same parent level. Lower = higher in list.")]
        public int sortOrder = 0;

        [Tooltip("Can the player click/select this body in the orbital map?")]
        public bool isSelectable = true;

        // ══════════════════════════════════════════════════════════════════
        // VISUAL SCALE
        // ══════════════════════════════════════════════════════════════════

        [Header("Visual")]

        [Tooltip("Apparent radius used for camera focus distance and representation scaling. " +
                 "In visualization units — not simulation units.")]
        public float visualRadius = 1f;

        // ══════════════════════════════════════════════════════════════════
        // KEPLERIAN ORBITAL ELEMENTS
        // ══════════════════════════════════════════════════════════════════

        [Header("Keplerian Orbital Elements")]

        [Tooltip("Semi-major axis a (simulation units). 0 for root/fixed bodies.")]
        public float semiMajorAxis = 0f;

        [Tooltip("Eccentricity e. 0 = circle, (0,1) = ellipse.")]
        [Range(0f, 0.99f)]
        public float eccentricity = 0f;

        [Tooltip("Orbital inclination i relative to the reference plane (radians).")]
        public float inclination = 0f;

        [Tooltip("Longitude of Ascending Node Ω (radians).")]
        public float longitudeOfAscendingNode = 0f;

        [Tooltip("Argument of Periapsis ω (radians).")]
        public float argumentOfPeriapsis = 0f;

        [Tooltip("Mean anomaly at epoch t=0 (radians).")]
        public float meanAnomalyAtEpoch = 0f;

        [Tooltip("Sidereal orbital period (simulation seconds). 0 for fixed/root bodies.")]
        public float orbitalPeriod = 0f;

        // ══════════════════════════════════════════════════════════════════
        // REPRESENTATION PREFABS
        // ══════════════════════════════════════════════════════════════════

        [Header("Representations")]

        [Tooltip("Full-detail prefab — shown when camera is close. " +
                 "Falls back to legacy 'prefab' if null.")]
        public GameObject nearPrefab;

        [Tooltip("Simplified prefab for medium camera distances. " +
                 "Falls back to nearPrefab if null.")]
        public GameObject mediumPrefab;

        [Tooltip("Billboard / icon marker for system-scale view. " +
                 "A primitive fallback is generated at runtime if null.")]
        public GameObject farPrefab;

        // ══════════════════════════════════════════════════════════════════
        // PRESET LINK  (optional — for editor authoring workflow)
        // ══════════════════════════════════════════════════════════════════

        [Header("Preset (editor authoring only)")]

        [Tooltip("Optional: preset this body was derived from. " +
                 "Runtime systems do not read this field.")]
        public BodyTypePreset sourcePreset;

        // ══════════════════════════════════════════════════════════════════
        // LEGACY — preserved for asset compatibility, hidden from Inspector
        // ══════════════════════════════════════════════════════════════════

        [HideInInspector] public string     bodyName;     // replaced by displayNameKey
        [HideInInspector] public GameObject prefab;       // replaced by nearPrefab
        [HideInInspector] public float      orbitRadius;
        [HideInInspector] public float      orbitSpeed;
        [HideInInspector] public float      startAngle;

        // ══════════════════════════════════════════════════════════════════
        // RUNTIME HELPERS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Returns nearPrefab, falling back to legacy prefab field.</summary>
        public GameObject ResolvedNearPrefab => nearPrefab != null ? nearPrefab : prefab;

        /// <summary>True if this body has valid orbital data (not a root/fixed body).</summary>
        public bool HasOrbit => semiMajorAxis > 0f && orbitalPeriod > 0f;

        /// <summary>True if the body id is non-empty.</summary>
        public bool HasValidId => !string.IsNullOrWhiteSpace(id);
    }

    // ══════════════════════════════════════════════════════════════════════
    // BODY TYPE ENUM
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Canonical body categories.
    /// Extend here when adding stations, ships, belt aggregates, etc.
    /// </summary>
    public enum BodyType
    {
        Star          = 0,
        Planet        = 1,
        DwarfPlanet   = 2,
        Moon          = 3,
        AsteroidBelt  = 4,   // belt aggregate / visual ring
        Asteroid      = 5,   // individual small body
        Station       = 10,  // gap reserved for future non-natural bodies
        Ship          = 11,
    }
}
