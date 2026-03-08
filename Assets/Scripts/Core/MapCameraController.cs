using UnityEngine;
using UnityEngine.InputSystem;
using SolarTerminal.View;

namespace SolarTerminal.Core
{
    public enum CameraMode { Free, Focus, Follow }

    /// <summary>
    /// Orbital map camera — isometric-style 40° top-down perspective.
    /// Pan moves in world XZ plane. Zoom changes distance along look direction.
    /// Uses new Unity Input System.
    /// </summary>
    public class MapCameraController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Zoom")]
        [SerializeField] private float _zoomMin       = 30f;
        [SerializeField] private float _zoomMax       = 900f;
        [SerializeField] private float _zoomSpeed     = 35f;
        [SerializeField] private float _zoomSmoothing = 8f;

        [Header("Tilt")]
        [Tooltip("Vertical angle in degrees (0 = horizontal, 90 = straight down)")]
        [SerializeField] private float _tiltAngle = 40f;

        [Header("Focus / Follow")]
        [SerializeField] private float _focusSmoothing   = 5f;
        [SerializeField] private float _focusArrivalDist = 1f;
        [SerializeField] private float _focusRadiusMult  = 7f;

        [Header("Free Pan")]
        [SerializeField] private float _panSpeed = 0.45f;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        public CameraMode Mode { get; private set; } = CameraMode.Free;

        private CelestialBodyView _target;

        // Desired look-at point on the XZ ground plane
        private Vector3 _desiredLookAt;

        // Desired distance from look-at point
        private float _desiredDist;
        private float _currentDist;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Start looking at origin from above-behind
            _desiredLookAt = Vector3.zero;
            _desiredDist   = 300f;
            _currentDist   = _desiredDist;
            ApplyTransform(Vector3.zero, _currentDist);
        }

        private void Update()
        {
            HandleZoomInput();

            switch (Mode)
            {
                case CameraMode.Free:   UpdateFree();   break;
                case CameraMode.Focus:  UpdateFocus();  break;
                case CameraMode.Follow: UpdateFollow(); break;
            }

            ApplyTransformSmooth();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        public void FocusOnTarget(CelestialBodyView target)
        {
            if (target == null) return;
            _target = target;
            Mode    = CameraMode.Focus;

            // Set initial zoom to fit the body — player can then scroll freely
            if (target.Definition != null)
            {
                float ideal = Mathf.Clamp(
                    target.Definition.visualRadius * _focusRadiusMult, _zoomMin, _zoomMax);
                _desiredDist = ideal;
            }
        }

        public void FollowTarget(CelestialBodyView target)
        {
            if (target == null) return;
            _target = target;
            Mode    = CameraMode.Follow;
        }

        public void StopFollowing()
        {
            _target = null;
            Mode    = CameraMode.Free;
        }

        // ------------------------------------------------------------------
        // Update modes
        // ------------------------------------------------------------------

        private void UpdateFree()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.isPressed || mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();

                // Pan in camera's XZ-projected right and forward axes
                Vector3 right   = transform.right;
                right.y = 0f; right.Normalize();

                // Forward projected onto XZ plane
                Vector3 forward = transform.forward;
                forward.y = 0f; forward.Normalize();

                float speed = _panSpeed * _currentDist * 0.012f;
                _desiredLookAt -= right   * delta.x * speed;
                _desiredLookAt -= forward * delta.y * speed;
            }
        }

        private void UpdateFocus()
        {
            if (_target == null) { Mode = CameraMode.Free; return; }

            var tp = TargetXZPoint();
            _desiredLookAt = Vector3.Lerp(_desiredLookAt, tp, Time.deltaTime * _focusSmoothing * 2f);

            // _desiredDist is NOT overwritten here — player can scroll to zoom freely
            // Transition to Free once camera has arrived at the target
            if (Vector3.Distance(_desiredLookAt, tp) < _focusArrivalDist)
                Mode = CameraMode.Free;
        }

        private void UpdateFollow()
        {
            if (_target == null) { Mode = CameraMode.Free; return; }
            _desiredLookAt = TargetXZPoint();
        }

        // ------------------------------------------------------------------
        // Zoom
        // ------------------------------------------------------------------

        private void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _desiredDist -= (scroll / 120f) * _zoomSpeed * (_currentDist * 0.1f);
                _desiredDist  = Mathf.Clamp(_desiredDist, _zoomMin, _zoomMax);

                // Scroll in Follow → break to Free
                // Scroll in Focus → keep focus target, just adjust distance
                if (Mode == CameraMode.Follow) StopFollowing();
            }
        }

        // ------------------------------------------------------------------
        // Transform application
        // ------------------------------------------------------------------

        private void ApplyTransformSmooth()
        {
            _currentDist = Mathf.Lerp(_currentDist, _desiredDist, Time.deltaTime * _zoomSmoothing);

            var smoothLook = Vector3.Lerp(
                GetCurrentLookAt(), _desiredLookAt, Time.deltaTime * _focusSmoothing);

            ApplyTransform(smoothLook, _currentDist);
        }

        /// <summary>
        /// Position camera at <dist> units away from <lookAt>,
        /// along a direction that is <_tiltAngle> degrees below horizontal.
        /// Camera always looks toward +X axis horizontally (can change if needed).
        /// </summary>
        private void ApplyTransform(Vector3 lookAt, float dist)
        {
            float tiltRad = _tiltAngle * Mathf.Deg2Rad;

            // Offset from lookAt: pull back along -Z and up by tilt
            Vector3 offset = new Vector3(
                0f,
                Mathf.Sin(tiltRad) * dist,
                -Mathf.Cos(tiltRad) * dist);

            transform.position = lookAt + offset;
            transform.LookAt(lookAt);
        }

        private Vector3 GetCurrentLookAt()
        {
            // Reverse-project: where is the camera looking at on XZ plane?
            float tiltRad = _tiltAngle * Mathf.Deg2Rad;
            float dist    = _currentDist;
            Vector3 offset = new Vector3(
                0f,
                Mathf.Sin(tiltRad) * dist,
                -Mathf.Cos(tiltRad) * dist);
            return transform.position - offset;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private Vector3 TargetXZPoint()
        {
            if (_target == null) return _desiredLookAt;
            var p = _target.WorldPosition;
            return new Vector3(p.x, 0f, p.z);
        }
    }
}
