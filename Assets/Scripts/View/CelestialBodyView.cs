using UnityEngine;
using SolarTerminal.Data;
using SolarTerminal.Simulation;

namespace SolarTerminal.View
{
    /// <summary>
    /// Syncs a celestial body's visual position to its OrbitalBodyState each LateUpdate.
    /// Owns the BodyRepresentationController — delegates all LOD/representation logic to it.
    ///
    /// Selection and camera systems bind to CelestialBodyView (the logical body),
    /// never to a specific representation instance. Switching representations is transparent.
    /// </summary>
    public class CelestialBodyView : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Runtime references — set by Initialize()
        // ------------------------------------------------------------------

        private OrbitalBodyState             _state;
        private BodyRepresentationController _representationCtrl;

        // ------------------------------------------------------------------
        // Public accessors — used by UI, camera, selection
        // ------------------------------------------------------------------

        /// <summary>ScriptableObject definition: name, type, orbital elements.</summary>
        public CelestialBodyDefinition Definition => _state?.Definition;

        /// <summary>Current world-space simulation position of this body.</summary>
        public Vector3 WorldPosition => _state != null ? _state.Position : Vector3.zero;

        /// <summary>Body id — convenience accessor.</summary>
        public string BodyId => _state?.Definition?.id;

        /// <summary>Current visual representation level (Near/Medium/Far). Informational.</summary>
        public BodyRepresentationController.RepresentationLevel RepresentationLevel
            => _representationCtrl != null
                ? _representationCtrl.CurrentLevel
                : BodyRepresentationController.RepresentationLevel.Near;

        // ------------------------------------------------------------------
        // Initialize — called by Bootstrap
        // ------------------------------------------------------------------

        /// <summary>
        /// Wire this view to its simulation state and set up representations.
        /// Bootstrap passes the already-instantiated near prefab so existing
        /// setup flow is preserved.
        /// </summary>
        public void Initialize(OrbitalBodyState state, GameObject nearPrefabInstance)
        {
            _state = state;

            // Move the near prefab instance under this view's transform
            nearPrefabInstance.transform.SetParent(transform);
            nearPrefabInstance.transform.localPosition = Vector3.zero;

            // Scale from definition
            float r = state.Definition.visualRadius > 0f ? state.Definition.visualRadius : 1f;
            nearPrefabInstance.transform.localScale = Vector3.one * r;

            // Build representation controller
            _representationCtrl = gameObject.AddComponent<BodyRepresentationController>();

            // Override the nearPrefab slot on the definition at runtime if it's empty,
            // so RepresentationController knows which instance to manage.
            // This avoids requiring a second prefab reference for the near slot.
            var def = state.Definition;
            if (def.nearPrefab == null)
            {
                // nearPrefabInstance already instantiated by Bootstrap — hand it to controller
                _representationCtrl.InitializeWithInstance(def, nearPrefabInstance, CameraDistanceEvaluator.MainCamera);
            }
            else
            {
                // Controller will instantiate its own copy from nearPrefab
                // (Bootstrap instance is kept as nearInstance via controller)
                _representationCtrl.InitializeWithInstance(def, nearPrefabInstance, CameraDistanceEvaluator.MainCamera);
            }
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_state == null) return;
            // Move this view's root transform to simulation position.
            // All child representations (near/medium/far) move with it for free.
            transform.position = _state.Position;
        }
    }
}
