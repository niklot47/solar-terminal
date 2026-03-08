using System.Collections.Generic;
using UnityEngine;
using SolarTerminal.Core;
using SolarTerminal.Data;
using SolarTerminal.Simulation;
using SolarTerminal.View;

namespace SolarTerminal.Bootstrap
{
    /// <summary>
    /// Entry point for the OrbitalMap scene.
    ///
    /// Accepts either:
    ///   A) A single OrbitalSystemDefinition (preferred — new workflow)
    ///   B) A direct list of CelestialBodyDefinitions (legacy / prototyping)
    ///
    /// If both are provided, OrbitalSystemDefinition takes priority.
    /// </summary>
    public class OrbitalMapBootstrap : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("System Definition (preferred)")]
        [Tooltip("Assign an OrbitalSystemDefinition asset. " +
                 "Takes priority over the legacy body list below.")]
        [SerializeField] private OrbitalSystemDefinition _systemDefinition;

        [Header("Legacy — direct body list (fallback)")]
        [SerializeField] private List<CelestialBodyDefinition> _bodyDefinitions
            = new List<CelestialBodyDefinition>();

        [Header("Core References")]
        [SerializeField] private SimulationTime      _simulationTime;
        [SerializeField] private SelectionManager    _selectionManager;
        [SerializeField] private MapCameraController _cameraController;

        [Header("Orbit Lines")]
        [SerializeField] private bool _drawOrbitLines        = true;
        [SerializeField] private bool _moonLinesFollowParent = true;

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        private OrbitalSimulation               _simulation;
        private readonly List<CelestialBodyView> _views = new List<CelestialBodyView>();

        // ------------------------------------------------------------------
        // Public API (used by validator, UI, camera systems)
        // ------------------------------------------------------------------

        public OrbitalSimulation               Simulation       => _simulation;
        public IReadOnlyList<CelestialBodyView> Views           => _views;
        public OrbitalSystemDefinition         SystemDefinition => _systemDefinition;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            if (_simulationTime == null)
            {
                Debug.LogError("[Bootstrap] SimulationTime is not assigned.");
                return;
            }

            var bodies = ResolveBodies();
            if (bodies.Count == 0)
            {
                Debug.LogError("[Bootstrap] No body definitions found. " +
                               "Assign an OrbitalSystemDefinition or add entries to Body Definitions.");
                return;
            }

            InitializeSimulation(bodies);
            CreateViews(bodies);
        }

        private void Update()
        {
            if (_simulation == null) return;
            _simulation.Tick(_simulationTime);
        }

        // ------------------------------------------------------------------
        // Body list resolution
        // ------------------------------------------------------------------

        private IReadOnlyList<CelestialBodyDefinition> ResolveBodies()
        {
            if (_systemDefinition != null && _systemDefinition.bodies.Count > 0)
            {
                Debug.Log($"[Bootstrap] Using OrbitalSystemDefinition '{_systemDefinition.systemId}' " +
                          $"({_systemDefinition.bodies.Count} bodies).");
                return _systemDefinition.bodies;
            }

            Debug.Log($"[Bootstrap] Using legacy body list ({_bodyDefinitions.Count} bodies).");
            return _bodyDefinitions;
        }

        // ------------------------------------------------------------------
        // Simulation init
        // ------------------------------------------------------------------

        private void InitializeSimulation(IReadOnlyList<CelestialBodyDefinition> bodies)
        {
            _simulation = new OrbitalSimulation();
            _simulation.Initialize(bodies);
        }

        // ------------------------------------------------------------------
        // View creation
        // ------------------------------------------------------------------

        private void CreateViews(IReadOnlyList<CelestialBodyDefinition> bodies)
        {
            Debug.Log($"[Bootstrap] CreateViews — {bodies.Count} definitions.");

            foreach (var def in bodies)
            {
                if (def == null)
                {
                    Debug.LogWarning("[Bootstrap] Null definition entry — skipped.");
                    continue;
                }

                if (!_simulation.TryGetState(def.id, out var state))
                {
                    Debug.LogWarning($"[Bootstrap] No simulation state for '{def.id}' — skipped.");
                    continue;
                }

                // Resolve near prefab (new field preferred, legacy prefab as fallback)
                var nearPrefab = def.ResolvedNearPrefab;
                if (nearPrefab == null)
                {
                    Debug.LogWarning($"[Bootstrap] No prefab for '{def.id}' — body not rendered.");
                    continue;
                }

                // Instantiate near prefab
                var nearInstance  = Instantiate(nearPrefab, state.Position, Quaternion.identity);
                nearInstance.name = $"Near_{def.id}";

                // Create view host
                var viewHost = new GameObject($"View_{def.id}");
                viewHost.transform.SetParent(transform);

                var bodyView = viewHost.AddComponent<CelestialBodyView>();
                bodyView.Initialize(state, nearInstance);
                _views.Add(bodyView);

                // Orbit line — requires valid Keplerian orbit
                if (_drawOrbitLines && def.HasOrbit)
                    CreateOrbitLine(def, state, viewHost);
            }

            Debug.Log($"[Bootstrap] CreateViews done — {_views.Count} views.");
        }

        private void CreateOrbitLine(
            CelestialBodyDefinition def,
            OrbitalBodyState        state,
            GameObject              parent)
        {
            var lineHost = new GameObject($"OrbitLine_{def.id}");
            lineHost.transform.SetParent(parent.transform);

            var lineView = lineHost.AddComponent<OrbitLineView>();

            OrbitalBodyState parentState = null;
            if (!string.IsNullOrEmpty(def.parentId))
                _simulation.TryGetState(def.parentId, out parentState);

            lineView.Initialize(state, parentState);

            if (_moonLinesFollowParent && def.bodyType == BodyType.Moon)
                lineView.SetFollowParent(true);
        }
    }
}
