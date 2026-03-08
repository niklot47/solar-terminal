using UnityEngine;
using SolarTerminal.Data;

namespace SolarTerminal.Simulation
{
    /// <summary>
    /// Runtime state for a single celestial body.
    /// Owned and updated exclusively by OrbitalSimulation — no external writes.
    /// </summary>
    public class OrbitalBodyState
    {
        /// <summary>Immutable data definition for this body.</summary>
        public readonly CelestialBodyDefinition Definition;

        /// <summary>World-space position, updated each Tick().</summary>
        public Vector3 Position { get; set; }

        /// <summary>Current true anomaly (radians) — informational, set by simulation.</summary>
        public float TrueAnomaly { get; set; }

        // Legacy field kept so Bootstrap.CreateViews() compiles unchanged
        /// <summary>Not used in Keplerian simulation. Preserved for API compatibility.</summary>
        public float CurrentAngle { get; set; }

        public OrbitalBodyState(CelestialBodyDefinition definition)
        {
            Definition   = definition;
            CurrentAngle = definition.startAngle;
            Position     = Vector3.zero;
        }
    }
}
