// CelestialBodyView.cs
// Syncs a celestial body's visual transform hierarchy to its OrbitalBodyState each LateUpdate.
//
// Transform hierarchy inside each view:
//
//   CelestialBodyView (BodyRoot)   — follows orbital position
//     AxialTiltRoot                — constant axial tilt rotation (set once in Initialize)
//       VisualSpinRoot             — spins around local Y each frame
//         [nearPrefab instance]    — actual visual content
//         [ringPrefab instance]    — rings if present

using UnityEngine;
using SolarTerminal.Data;
using SolarTerminal.Simulation;

namespace SolarTerminal.View
{
    public class CelestialBodyView : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Runtime references
        // ------------------------------------------------------------------

        private OrbitalBodyState             _state;
        private BodyRepresentationController _representationCtrl;

        // Sub-roots of the transform hierarchy
        private Transform _axialTiltRoot;
        private Transform _visualSpinRoot;

        // ------------------------------------------------------------------
        // Public accessors
        // ------------------------------------------------------------------

        public CelestialBodyDefinition Definition  => _state?.Definition;
        public Vector3                 WorldPosition => _state != null ? _state.Position : Vector3.zero;
        public string                  BodyId      => _state?.Definition?.id;

        public BodyRepresentationController.RepresentationLevel RepresentationLevel
            => _representationCtrl != null
                ? _representationCtrl.CurrentLevel
                : BodyRepresentationController.RepresentationLevel.Near;

        /// <summary>
        /// The VisualSpinRoot transform — use this to attach surface ports or
        /// planet-surface objects that must rotate with the planet.
        /// </summary>
        public Transform VisualSpinRoot => _visualSpinRoot;

        // ------------------------------------------------------------------
        // Initialize — called by Bootstrap
        // ------------------------------------------------------------------

        public void Initialize(OrbitalBodyState state, GameObject nearPrefabInstance)
        {
            _state = state;
            var def = state.Definition;
            float r = def.visualRadius > 0f ? def.visualRadius : 1f;

            // ── Build transform hierarchy ─────────────────────────────────

            // AxialTiltRoot: constant tilt, set once
            var tiltGO = new GameObject("AxialTiltRoot");
            tiltGO.transform.SetParent(transform, worldPositionStays: false);
            tiltGO.transform.localPosition = Vector3.zero;
            tiltGO.transform.localRotation = Quaternion.Euler(def.axialTiltDegrees, 0f, 0f);
            _axialTiltRoot = tiltGO.transform;

            // VisualSpinRoot: rotates each LateUpdate
            var spinGO = new GameObject("VisualSpinRoot");
            spinGO.transform.SetParent(_axialTiltRoot, worldPositionStays: false);
            spinGO.transform.localPosition = Vector3.zero;
            spinGO.transform.localRotation = Quaternion.identity;
            _visualSpinRoot = spinGO.transform;

            // ── Place visual content under VisualSpinRoot ─────────────────
            nearPrefabInstance.transform.SetParent(_visualSpinRoot);
            nearPrefabInstance.transform.localPosition = Vector3.zero;
            nearPrefabInstance.transform.localScale    = Vector3.one * r;

            // ── Rings ─────────────────────────────────────────────────────
            // Rings are parented to AxialTiltRoot (not VisualSpinRoot)
            // so they stay in the equatorial plane without spinning with the surface.
            if (def.ringPrefab != null)
            {
                var ringInstance = Instantiate(def.ringPrefab);
                ringInstance.name = $"Rings_{def.id}";
                ringInstance.transform.SetParent(_axialTiltRoot, worldPositionStays: false);
                ringInstance.transform.localPosition = Vector3.zero;
                ringInstance.transform.localRotation = Quaternion.identity;
                ringInstance.transform.localScale    = Vector3.one * r * def.ringScale;
            }

            // ── Representation controller ─────────────────────────────────
            // Pass _visualSpinRoot so all representations live inside the spin hierarchy.
            _representationCtrl = gameObject.AddComponent<BodyRepresentationController>();
            _representationCtrl.InitializeWithInstance(
                def, nearPrefabInstance, _visualSpinRoot, CameraDistanceEvaluator.MainCamera);

            // ── Initial spin angle at epoch ───────────────────────────────
            ApplySpin(def.rotationPhaseAtEpochDegrees, def.isTidallyLocked, Vector3.forward);
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private int _debugFrame;

        private void LateUpdate()
        {
            if (_state == null) return;

            // 1. Move body root to orbital position
            transform.position = _state.Position;

            // 2. Apply spin or tidal lock
            if (_state.Definition.isTidallyLocked)
                ApplyTidalLock(_state.TidalLockForward);
            else
                ApplySpin(_state.SpinAngleDegrees,
                          tidally: false,
                          tidalForward: Vector3.forward);

            // Debug: log every 60 frames per body
            if (++_debugFrame % 60 == 0)
            {
                var def = _state.Definition;
                if (def.isTidallyLocked)
                {
                    Debug.Log($"[BodyView:{def.id}] TIDAL  tidalFwd={_state.TidalLockForward:F2}" +
                              $"  spinRootRot={_visualSpinRoot?.localEulerAngles:F1}" +
                              $"  tiltRootRot={_axialTiltRoot?.localEulerAngles:F1}");
                }
                else
                {
                    Debug.Log($"[BodyView:{def.id}] SPIN   spinAngle={_state.SpinAngleDegrees % 360f:F1}°" +
                              $"  period={def.rotationPeriodHours}h" +
                              $"  spinRootRot={_visualSpinRoot?.localEulerAngles:F1}" +
                              $"  tiltRootRot={_axialTiltRoot?.localEulerAngles:F1}" +
                              $"  worldPos={transform.position:F1}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Spin helpers
        // ------------------------------------------------------------------

        private void ApplySpin(float angleDegrees, bool tidally, Vector3 tidalForward)
        {
            if (_visualSpinRoot == null) return;
            _visualSpinRoot.localRotation = Quaternion.Euler(0f, angleDegrees, 0f);
        }

        private void ApplyTidalLock(Vector3 worldForward)
        {
            if (_visualSpinRoot == null || worldForward.sqrMagnitude < 0.0001f) return;

            // Convert world-space toward-parent direction into AxialTiltRoot's local space,
            // then align VisualSpinRoot's local Z toward that direction.
            Vector3 localForward = _axialTiltRoot.InverseTransformDirection(worldForward);
            if (localForward.sqrMagnitude > 0.0001f)
                _visualSpinRoot.localRotation = Quaternion.LookRotation(localForward, Vector3.up);
        }
    }
}
