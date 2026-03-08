using UnityEngine;
using UnityEngine.UIElements;
using SolarTerminal.Core;
using SolarTerminal.View;

namespace SolarTerminal.UI
{
    public class SidePanelUIController
    {
        private readonly VisualElement         _panel;
        private readonly Button                _toggleBtn;
        private readonly VisualElement         _hudRoot;
        private readonly Label                 _footerName;
        private readonly Button                _btnFocus;
        private readonly Button                _btnFollow;
        private readonly ILocalizationProvider _loc;
        private readonly MapCameraController   _camera;
        private readonly SelectionManager      _selection;

        private bool _isOpen = true;

        private const string KEY_TITLE  = "ui.panel.objects";
        private const string KEY_FOCUS  = "ui.button.focus";
        private const string KEY_FOLLOW = "ui.button.follow";
        private const string KEY_NOSEL  = "ui.panel.no_selection";

        // Panel width in px — must match inline style set below
        private const float PANEL_WIDTH = 260f;

        public SidePanelUIController(
            VisualElement         root,
            ILocalizationProvider loc,
            MapCameraController   camera,
            SelectionManager      selection)
        {
            _loc       = loc;
            _camera    = camera;
            _selection = selection;

            _hudRoot    = root.Q<VisualElement>("hud-root");
            _panel      = root.Q<VisualElement>("side-panel");
            _toggleBtn  = root.Q<Button>("panel-toggle");
            _footerName = root.Q<Label>("footer-name");
            _btnFocus   = root.Q<Button>("btn-focus");
            _btnFollow  = root.Q<Button>("btn-follow");

            // Panel inline styles
            if (_panel != null)
            {
                _panel.style.width           = PANEL_WIDTH;
                _panel.style.height          = new StyleLength(new Length(100, LengthUnit.Percent));
                _panel.style.backgroundColor = new StyleColor(new Color(0.03f, 0.07f, 0.06f, 0.92f));
                _panel.style.flexDirection   = FlexDirection.Column;
                _panel.style.borderRightWidth  = 1;
                _panel.style.borderRightColor  = new StyleColor(new Color(0f, 0.82f, 0.55f, 0.22f));
            }

            // Static labels
            var title = root.Q<Label>("panel-title");
            if (title != null)
            {
                title.text = _loc.Get(KEY_TITLE).ToUpper();
                title.style.color    = new StyleColor(new Color(0.55f, 0.67f, 0.63f, 1f));
                title.style.fontSize = 11;
                title.style.marginLeft = 4;
            }

            if (_btnFocus  != null) _btnFocus.text  = _loc.Get(KEY_FOCUS);
            if (_btnFollow != null) _btnFollow.text = _loc.Get(KEY_FOLLOW);
            if (_footerName != null) _footerName.text = _loc.Get(KEY_NOSEL);

            // Toggle button — positioned via inline styles
            if (_toggleBtn != null)
            {
                ApplyToggleBtnStyle();
                _toggleBtn.clicked += Toggle;
            }

            if (_btnFocus  != null) _btnFocus.clicked  += OnFocusClicked;
            if (_btnFollow != null) _btnFollow.clicked += OnFollowClicked;

            if (_selection != null)
                _selection.OnSelectionChanged += OnSelectionChanged;

            SetFooterVisible(false);
        }

        public void Dispose()
        {
            if (_selection != null)
                _selection.OnSelectionChanged -= OnSelectionChanged;
        }

        // ------------------------------------------------------------------
        // Toggle
        // ------------------------------------------------------------------

        public void Toggle()
        {
            _isOpen = !_isOpen;

            if (_panel != null)
                _panel.style.display = _isOpen ? DisplayStyle.Flex : DisplayStyle.None;

            // Move toggle button: flush to left edge when closed, next to panel when open
            if (_toggleBtn != null)
            {
                _toggleBtn.text = _isOpen ? "\u25C4" : "\u25BA";  // ◄ ►
                _toggleBtn.style.left = _isOpen ? PANEL_WIDTH : 0;
            }
        }

        // ------------------------------------------------------------------
        // Toggle button styling
        // ------------------------------------------------------------------

        private void ApplyToggleBtnStyle()
        {
            if (_toggleBtn == null) return;
            _toggleBtn.text = "\u25C4";
            _toggleBtn.style.position        = Position.Absolute;
            _toggleBtn.style.left            = PANEL_WIDTH;  // next to panel edge
            _toggleBtn.style.top             = new StyleLength(new Length(50, LengthUnit.Percent));
            _toggleBtn.style.marginTop       = -26;           // center vertically
            _toggleBtn.style.width           = 20;
            _toggleBtn.style.height          = 52;
            _toggleBtn.style.backgroundColor = new StyleColor(new Color(0.03f, 0.07f, 0.06f, 0.95f));
            _toggleBtn.style.color           = new StyleColor(new Color(0f, 0.82f, 0.55f, 1f));
            _toggleBtn.style.borderLeftWidth   = 0;
            _toggleBtn.style.borderTopWidth    = 1;
            _toggleBtn.style.borderBottomWidth = 1;
            _toggleBtn.style.borderRightWidth  = 1;
            _toggleBtn.style.borderTopColor    = new StyleColor(new Color(0f, 0.82f, 0.55f, 0.4f));
            _toggleBtn.style.borderBottomColor = new StyleColor(new Color(0f, 0.82f, 0.55f, 0.4f));
            _toggleBtn.style.borderRightColor  = new StyleColor(new Color(0f, 0.82f, 0.55f, 0.4f));
            _toggleBtn.style.unityTextAlign    = TextAnchor.MiddleCenter;
            _toggleBtn.style.paddingLeft  = 0;
            _toggleBtn.style.paddingRight = 0;
        }

        // ------------------------------------------------------------------
        // Footer
        // ------------------------------------------------------------------

        private void OnSelectionChanged(CelestialBodyView view)
        {
            if (view == null)
            {
                if (_footerName != null) _footerName.text = _loc.Get(KEY_NOSEL);
                SetFooterVisible(false);
                return;
            }
            if (_footerName != null)
                _footerName.text = view.Definition?.bodyName ?? view.BodyId ?? "?";
            SetFooterVisible(true);
            UpdateFollowButtonState();
        }

        private void SetFooterVisible(bool visible)
        {
            var footer = _panel?.Q<VisualElement>("panel-footer");
            if (footer != null)
                footer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateFollowButtonState()
        {
            if (_btnFollow == null || _camera == null) return;
            bool following = _camera.Mode == CameraMode.Follow;
            if (following) _btnFollow.AddToClassList("action-btn--active");
            else           _btnFollow.RemoveFromClassList("action-btn--active");
        }

        private void OnFocusClicked()  => _camera?.FocusOnTarget(_selection?.CurrentSelectedBody);

        private void OnFollowClicked()
        {
            var body = _selection?.CurrentSelectedBody;
            if (body == null) return;
            if (_camera?.Mode == CameraMode.Follow) _camera.StopFollowing();
            else _camera?.FollowTarget(body);
            UpdateFollowButtonState();
        }
    }
}
