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

        /// <summary>
        /// Current spin angle around the body's own axis (degrees).
        /// Accounts for rotationPhaseAtEpoch and elapsed simulation time.
        /// Updated each Tick() by OrbitalSimulation.
        /// </summary>
        public float SpinAngleDegrees { get; set; }

        /// <summary>
        /// For tidally locked bodies: world-space direction from this body toward its parent.
        /// CelestialBodyView uses this to orient the visual spin root each frame.
        /// Meaningless for non-locked bodies.
        /// </summary>
        public Vector3 TidalLockForward { get; set; }

        // Legacy field kept so Bootstrap.CreateViews() compiles unchanged
        /// <summary>Not used in Keplerian simulation. Preserved for API compatibility.</summary>
        public float CurrentAngle { get; set; }

        public OrbitalBodyState(CelestialBodyDefinition definition)
        {
            Definition        = definition;
            CurrentAngle      = definition.startAngle;
            SpinAngleDegrees  = definition.rotationPhaseAtEpochDegrees;
            TidalLockForward  = Vector3.forward;
            Position          = Vector3.zero;
        }
    }
}
