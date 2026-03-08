using System.Collections.Generic;
using UnityEngine;

namespace SolarTerminal.Data
{
    /// <summary>
    /// Top-level container for one star system.
    /// Owns the canonical ordered list of all body definitions in this system.
    ///
    /// A scene's OrbitalMapBootstrap references one OrbitalSystemDefinition
    /// instead of a raw List of CelestialBodyDefinitions.
    ///
    /// Create: Assets > Create > SolarTerminal > OrbitalSystemDefinition
    /// </summary>
    [CreateAssetMenu(
        menuName = "SolarTerminal/OrbitalSystemDefinition",
        fileName = "System_New")]
    public class OrbitalSystemDefinition : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        // IDENTITY
        // ══════════════════════════════════════════════════════════════════

        [Header("Identity")]

        [Tooltip("Unique system id, e.g. 'sol'.")]
        public string systemId;

        [Tooltip("Localization key for the system name, e.g. 'system.sol'.")]
        public string displayNameKey;

        [Tooltip("The id of the central/root body (typically the star).")]
        public string centralBodyId;

        // ══════════════════════════════════════════════════════════════════
        // BODY LIST
        // ══════════════════════════════════════════════════════════════════

        [Header("Bodies")]

        [Tooltip("All body definitions that belong to this system. " +
                 "Order does not need to be parent-before-child here; " +
                 "OrbitalSimulation resolves that at runtime.")]
        public List<CelestialBodyDefinition> bodies = new List<CelestialBodyDefinition>();

        // ══════════════════════════════════════════════════════════════════
        // VISUALIZATION METADATA  (extensible, no runtime logic here)
        // ══════════════════════════════════════════════════════════════════

        [Header("Visualization (future use)")]

        [Tooltip("Simulation-unit radius that defines the 'system boundary' for camera defaults.")]
        public float systemRadius = 500f;

        // ══════════════════════════════════════════════════════════════════
        // EDITOR HELPERS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Returns body by id, or null. O(n) — editor/init use only.</summary>
        public CelestialBodyDefinition FindById(string bodyId)
        {
            foreach (var b in bodies)
                if (b != null && b.id == bodyId) return b;
            return null;
        }
    }
}
