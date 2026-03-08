using UnityEngine;
using SolarTerminal.Data;

namespace SolarTerminal.View
{
    /// <summary>
    /// Manages Near / Medium / Far visual representations of one celestial body.
    /// Switches between them based on camera distance — no simulation data touched.
    ///
    /// All representations are children of the same transform.
    /// CelestialBodyView moves that transform to simulation position every frame.
    /// Switching is done via SetActive() — no per-frame instantiation.
    ///
    /// Label system can be attached later — see extension point at the bottom.
    /// </summary>
    public class BodyRepresentationController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Representation level
        // ------------------------------------------------------------------

        public enum RepresentationLevel { Near, Medium, Far }

        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Distance Thresholds (world units)")]
        [Tooltip("Camera closer than this → Near representation")]
        [SerializeField] private float _nearThreshold   = 40f;

        [Tooltip("Camera closer than this, farther than Near → Medium representation")]
        [SerializeField] private float _mediumThreshold = 150f;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private Camera                  _camera;
        private CelestialBodyDefinition _definition;

        private GameObject _nearInstance;
        private GameObject _mediumInstance;
        private GameObject _farInstance;
        private bool       _nearIsMedium; // true when medium reuses near instance

        private RepresentationLevel _currentLevel = (RepresentationLevel)(-1);

        public RepresentationLevel CurrentLevel => _currentLevel;

        // ------------------------------------------------------------------
        // Initialization paths
        // ------------------------------------------------------------------

        /// <summary>
        /// Primary init path: Bootstrap has already instantiated the near prefab.
        /// Controller receives that instance directly and builds medium/far on top.
        /// </summary>
        public void InitializeWithInstance(
            CelestialBodyDefinition def,
            GameObject              nearInstance,
            Camera                  camera)
        {
            _definition  = def;
            _camera      = camera != null ? camera : Camera.main;
            _nearInstance = nearInstance;

            BuildMediumAndFar();
            EvaluateAndApply();
        }

        /// <summary>
        /// Alternative init path: controller instantiates everything itself from def.nearPrefab.
        /// Use this when Bootstrap is not involved.
        /// </summary>
        public void Initialize(CelestialBodyDefinition def, Camera camera)
        {
            _definition = def;
            _camera     = camera != null ? camera : Camera.main;

            // Instantiate near from definition
            if (def.nearPrefab != null)
            {
                _nearInstance = Instantiate(def.nearPrefab, transform);
                _nearInstance.name = $"Near_{def.id}";
                _nearInstance.transform.localPosition = Vector3.zero;
                _nearInstance.transform.localScale    = Vector3.one * Mathf.Max(def.visualRadius, 1f);
            }

            BuildMediumAndFar();
            EvaluateAndApply();
        }

        // ------------------------------------------------------------------
        // Build medium and far representations
        // ------------------------------------------------------------------

        private void BuildMediumAndFar()
        {
            var def = _definition;

            // Medium — optional separate prefab, otherwise share near instance
            if (def.mediumPrefab != null)
            {
                _mediumInstance = Instantiate(def.mediumPrefab, transform);
                _mediumInstance.name = $"Medium_{def.id}";
                _mediumInstance.transform.localPosition = Vector3.zero;
                _mediumInstance.transform.localScale    = Vector3.one * Mathf.Max(def.visualRadius, 1f);
                _nearIsMedium = false;
            }
            else
            {
                _mediumInstance = _nearInstance; // reuse same instance
                _nearIsMedium   = true;
            }

            // Far — optional prefab, otherwise build fallback marker
            _farInstance = def.farPrefab != null
                ? InstantiateFarPrefab(def)
                : BuildFallbackMarker(def);
        }

        private GameObject InstantiateFarPrefab(CelestialBodyDefinition def)
        {
            var go = Instantiate(def.farPrefab, transform);
            go.name = $"Far_{def.id}";
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        // ------------------------------------------------------------------
        // Fallback marker
        // ------------------------------------------------------------------

        private GameObject BuildFallbackMarker(CelestialBodyDefinition def)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"FarMarker_{def.id}";
            marker.transform.SetParent(transform);
            marker.transform.localPosition = Vector3.zero;

            // Marker visual size is intentionally small and fixed —
            // it is a readable dot in system view, NOT a scaled simulation object.
            float markerSize = Mathf.Clamp(def.visualRadius * 0.4f, 0.5f, 3f);
            marker.transform.localScale = Vector3.one * markerSize;

            // Remove collider — markers are purely visual
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Unlit bright material — placeholder, easy to replace with a real marker prefab
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                if (mat.shader.name == "Hidden/InternalErrorShader")
                    mat = new Material(Shader.Find("Sprites/Default"));
                mat.color      = MarkerColor(def.bodyType);
                rend.material  = mat;
                rend.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows       = false;
            }

            return marker;
        }

        private static Color MarkerColor(BodyType type) => type switch
        {
            BodyType.Star   => new Color(1.0f, 0.90f, 0.35f, 1f),
            BodyType.Planet => new Color(0.40f, 0.82f, 0.52f, 1f),
            BodyType.Moon   => new Color(0.60f, 0.65f, 0.72f, 1f),
            _               => Color.white,
        };

        // ------------------------------------------------------------------
        // Per-frame update
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_camera == null) return;
            EvaluateAndApply();

            // Billboard: far marker always faces camera
            if (_currentLevel == RepresentationLevel.Far && _farInstance != null)
            {
                Vector3 dir = _camera.transform.position - _farInstance.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    _farInstance.transform.rotation = Quaternion.LookRotation(-dir);
            }
        }

        // ------------------------------------------------------------------
        // Evaluation and switching
        // ------------------------------------------------------------------

        private void EvaluateAndApply()
        {
            float dist  = Vector3.Distance(_camera.transform.position, transform.position);
            var   level = Evaluate(dist);
            if (level != _currentLevel)
                ApplyLevel(level);
        }

        private RepresentationLevel Evaluate(float dist)
        {
            if (dist < _nearThreshold)   return RepresentationLevel.Near;
            if (dist < _mediumThreshold) return RepresentationLevel.Medium;
            return RepresentationLevel.Far;
        }

        private void ApplyLevel(RepresentationLevel level)
        {
            _currentLevel = level;

            switch (level)
            {
                case RepresentationLevel.Near:
                    SetActive(_nearInstance,   true);
                    if (!_nearIsMedium) SetActive(_mediumInstance, false);
                    SetActive(_farInstance,    false);
                    break;

                case RepresentationLevel.Medium:
                    // If near == medium, the shared instance stays on
                    SetActive(_nearInstance,   _nearIsMedium);
                    if (!_nearIsMedium) SetActive(_mediumInstance, true);
                    SetActive(_farInstance,    false);
                    break;

                case RepresentationLevel.Far:
                    SetActive(_nearInstance,   false);
                    if (!_nearIsMedium) SetActive(_mediumInstance, false);
                    SetActive(_farInstance,    true);
                    break;
            }
        }

        private static void SetActive(GameObject obj, bool active)
        {
            if (obj != null && obj.activeSelf != active)
                obj.SetActive(active);
        }

        // ------------------------------------------------------------------
        // Runtime configuration
        // ------------------------------------------------------------------

        public void SetThresholds(float near, float medium)
        {
            _nearThreshold   = near;
            _mediumThreshold = medium;
            _currentLevel    = (RepresentationLevel)(-1); // force re-eval
        }

        // ------------------------------------------------------------------
        // Future: label system extension point
        // ------------------------------------------------------------------
        //
        // To attach labels later:
        //
        //   public void SetLabelHandler(IBodyLabelHandler handler)
        //   {
        //       _labelHandler = handler;
        //       // Enable label in Far mode, disable in Near/Medium
        //   }
        //
        // IBodyLabelHandler.SetVisible(bool) called from ApplyLevel().
        // No changes to this class structure required.
    }
}
