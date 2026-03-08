using UnityEngine;
using SolarTerminal.Simulation;

namespace SolarTerminal.View
{
    /// <summary>
    /// Draws the correct Keplerian orbit ellipse using a LineRenderer.
    /// Geometry is computed from orbital elements via OrbitalMechanics.SampleOrbitPoints().
    /// The line is static by default; set followParent=true for moon-around-planet orbits.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitLineView : MonoBehaviour
    {
        [Header("Line Quality")]
        [SerializeField] private int   _segments  = 128;
        [SerializeField] private float _lineWidth  = 0.15f;
        [SerializeField] private Color _lineColor  = new Color(1f, 1f, 1f, 0.15f);

        [SerializeField] private bool _followParent = false;

        private LineRenderer     _lineRenderer;
        private OrbitalBodyState _bodyState;
        private OrbitalBodyState _parentState;

        // Pre-allocated point buffer — no allocations in BuildRing()
        private Vector3[] _points;

        // ------------------------------------------------------------------
        // Init
        // ------------------------------------------------------------------

        public void Initialize(OrbitalBodyState bodyState, OrbitalBodyState parentState)
        {
            _bodyState   = bodyState;
            _parentState = parentState;

            _lineRenderer               = GetComponent<LineRenderer>();
            _lineRenderer.loop          = true;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth    = _lineWidth;
            _lineRenderer.endWidth      = _lineWidth;
            _lineRenderer.startColor    = _lineColor;
            _lineRenderer.endColor      = _lineColor;

            if (_lineRenderer.material == null ||
                _lineRenderer.material.shader.name == "Standard")
            {
                _lineRenderer.material       = new Material(Shader.Find("Sprites/Default"));
                _lineRenderer.material.color = _lineColor;
            }

            _points = new Vector3[_segments];
            BuildRing();
        }

        public void SetFollowParent(bool follow) => _followParent = follow;

        // ------------------------------------------------------------------
        // Ring construction
        // ------------------------------------------------------------------

        /// <summary>
        /// Rebuild the orbit ellipse from the body's current orbital elements.
        /// The parent body's world position is used as the focus of the ellipse.
        /// </summary>
        public void BuildRing()
        {
            if (_bodyState == null) return;

            var def = _bodyState.Definition;

            if (def.semiMajorAxis <= 0f || def.orbitalPeriod <= 0f)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            // Sample orbit ellipse in local frame (relative to parent)
            OrbitalMechanics.SampleOrbitPoints(
                _points,
                def.semiMajorAxis,
                def.eccentricity,
                def.inclination,
                def.longitudeOfAscendingNode,
                def.argumentOfPeriapsis);

            // Offset all points by parent world position (focus of the ellipse)
            Vector3 focus = _parentState?.Position ?? Vector3.zero;

            _lineRenderer.positionCount = _segments;
            for (int i = 0; i < _segments; i++)
                _lineRenderer.SetPosition(i, focus + _points[i]);
        }

        // ------------------------------------------------------------------
        // Per-frame update (only when following a moving parent)
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_followParent) BuildRing();
        }
    }
}
