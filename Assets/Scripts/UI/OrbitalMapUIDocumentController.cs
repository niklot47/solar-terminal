using UnityEngine;
using UnityEngine.UIElements;
using SolarTerminal.Core;
using SolarTerminal.Bootstrap;

namespace SolarTerminal.UI
{
    /// <summary>
    /// Root MonoBehaviour for the orbital map UI Toolkit layer.
    /// Attach to the UIDocument GameObject in the scene.
    ///
    /// All scene references are found automatically by type in Start().
    /// Assign Color Scheme in Inspector if you want a non-default theme.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class OrbitalMapUIDocumentController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector — only assets, no scene object references
        // (scene objects are found automatically — avoids cross-type drag issues)
        // ------------------------------------------------------------------

        [Header("UI Assets")]
        [Tooltip("Assign TreeItem.uxml here")]
        [SerializeField] private VisualTreeAsset _treeItemTemplate;

        [Header("Colour Theme (optional)")]
        [Tooltip("Leave empty to use default teal theme from UITheme.uss")]
        [SerializeField] private UIColorScheme _colorScheme;

        // ------------------------------------------------------------------
        // Runtime — resolved in Start()
        // ------------------------------------------------------------------

        private UIDocument                   _document;
        private SidePanelUIController        _sidePanelCtrl;
        private ObjectTreeUIController       _treeCtrl;
        private ILocalizationProvider        _loc;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            _document = GetComponent<UIDocument>();

            // Auto-find all required scene components
            var selection = FindFirstObjectByType<SelectionManager>();
            var camera    = FindFirstObjectByType<MapCameraController>();
            var bootstrap = FindFirstObjectByType<OrbitalMapBootstrap>();

            if (selection == null)
                Debug.LogWarning("[UIDocumentController] SelectionManager not found in scene.");
            if (camera == null)
                Debug.LogWarning("[UIDocumentController] MapCameraController not found in scene.");
            if (bootstrap == null)
            {
                Debug.LogError("[UIDocumentController] OrbitalMapBootstrap not found — tree will be empty.");
                return;
            }

            _loc = new DefaultLocalizationProvider();

            var root = _document.rootVisualElement;

            // Apply colour theme
            if (_colorScheme != null)
                _colorScheme.ApplyTo(root);
            else
                root.AddToClassList("theme-default");

            // Create sub-controllers
            _sidePanelCtrl = new SidePanelUIController(root, _loc, camera, selection);

            if (_treeItemTemplate != null)
            {
                var container = root.Q<VisualElement>("tree-container");
                if (container == null)
                {
                    Debug.LogError("[UIDocumentController] 'tree-container' not found in UXML.");
                    return;
                }

                _treeCtrl = new ObjectTreeUIController(
                    container, _treeItemTemplate, selection, camera, _loc);

                // Bootstrap.Views is populated in Bootstrap.Start() —
                // if both Start() run in the same frame order may vary.
                // Use a one-frame delay to be safe.
                StartCoroutine(BuildTreeNextFrame(bootstrap));
            }
            else
            {
                Debug.LogWarning("[UIDocumentController] TreeItem template not assigned in Inspector.");
            }
        }

        private System.Collections.IEnumerator BuildTreeNextFrame(OrbitalMapBootstrap bootstrap)
        {
            // Wait one frame so Bootstrap.Start() has finished creating views
            yield return null;
            _treeCtrl.Build(bootstrap.Views);
            Debug.Log($"[UIDocumentController] Tree built with {bootstrap.Views.Count} views.");
        }

        private void OnDestroy()
        {
            _sidePanelCtrl?.Dispose();
            _treeCtrl?.Dispose();
        }

        // ------------------------------------------------------------------
        // Public API — theme switching at runtime
        // ------------------------------------------------------------------

        /// <summary>
        /// Switch colour scheme at runtime, e.g. call when entering combat.
        /// combatScheme.themeClassName should be "theme-combat".
        /// </summary>
        public void ApplyTheme(UIColorScheme scheme)
        {
            _colorScheme = scheme;
            if (scheme != null && _document != null)
                scheme.ApplyTo(_document.rootVisualElement);
        }
    }
}
