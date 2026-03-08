using System.Collections.Generic;
using UnityEngine;
using SolarTerminal.Data;
using SolarTerminal.Core;

namespace SolarTerminal.Simulation
{
    /// <summary>
    /// Maintains runtime orbital states and advances the simulation each tick
    /// using Keplerian orbital mechanics.
    ///
    /// No rendering happens here — View layer reads Position from states after Tick().
    /// Simulation time accumulates independently; positions are fully deterministic
    /// given the same simulation time value.
    /// </summary>
    public class OrbitalSimulation
    {
        private readonly Dictionary<string, OrbitalBodyState> _states
            = new Dictionary<string, OrbitalBodyState>();

        private readonly List<OrbitalBodyState> _updateOrder
            = new List<OrbitalBodyState>();

        // Accumulated simulation time in simulation-seconds
        private float _simulationTime;

        public IReadOnlyDictionary<string, OrbitalBodyState> States => _states;

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        public void Initialize(IEnumerable<CelestialBodyDefinition> definitions)
        {
            _states.Clear();
            _updateOrder.Clear();
            _simulationTime = 0f;

            foreach (var def in definitions)
            {
                if (string.IsNullOrEmpty(def.id))
                {
                    Debug.LogWarning($"[OrbitalSimulation] Definition '{def.name}' has no id — skipped.");
                    continue;
                }
                _states[def.id] = new OrbitalBodyState(def);
            }

            // Resolve parent-before-child update order
            var visited = new HashSet<string>();
            foreach (var id in _states.Keys)
                ResolveOrder(id, visited);

            // Compute initial positions at t=0
            ComputeAllPositions(0f);
        }

        private void ResolveOrder(string id, HashSet<string> visited)
        {
            if (visited.Contains(id)) return;
            visited.Add(id);

            var state    = _states[id];
            var parentId = state.Definition.parentId;

            if (!string.IsNullOrEmpty(parentId) && _states.ContainsKey(parentId))
                ResolveOrder(parentId, visited);

            _updateOrder.Add(state);
        }

        // ------------------------------------------------------------------
        // Simulation step
        // ------------------------------------------------------------------

        /// <summary>
        /// Advance simulation by SimDeltaTime and recompute all body positions.
        /// </summary>
        public void Tick(SimulationTime simTime)
        {
            _simulationTime += simTime.SimDeltaTime;
            ComputeAllPositions(_simulationTime);
        }

        // ------------------------------------------------------------------
        // Position computation
        // ------------------------------------------------------------------

        private void ComputeAllPositions(float time)
        {
            foreach (var state in _updateOrder)
            {
                var def = state.Definition;

                // Root body — fixed at world origin
                if (string.IsNullOrEmpty(def.parentId) || def.orbitalPeriod <= 0f)
                {
                    state.Position = Vector3.zero;
                    continue;
                }

                // Compute position relative to parent using Keplerian mechanics
                Vector3 localPos = OrbitalMechanics.ComputeOrbitalPosition(
                    def.semiMajorAxis,
                    def.eccentricity,
                    def.inclination,
                    def.longitudeOfAscendingNode,
                    def.argumentOfPeriapsis,
                    def.meanAnomalyAtEpoch,
                    def.orbitalPeriod,
                    time);

                // Add parent world position
                Vector3 parentPos = Vector3.zero;
                if (_states.TryGetValue(def.parentId, out var parentState))
                    parentPos = parentState.Position;

                state.Position = parentPos + localPos;
            }
        }

        // ------------------------------------------------------------------
        // Query
        // ------------------------------------------------------------------

        public bool TryGetState(string id, out OrbitalBodyState state)
            => _states.TryGetValue(id, out state);

        /// <summary>Current accumulated simulation time (sim-seconds).</summary>
        public float SimulationTime => _simulationTime;
    }
}
