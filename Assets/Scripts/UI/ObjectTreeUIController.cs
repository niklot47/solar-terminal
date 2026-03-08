using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SolarTerminal.Core;
using SolarTerminal.Data;
using SolarTerminal.View;

namespace SolarTerminal.UI
{
    public class ObjectTreeUIController
    {
        // Colours — match UITheme.uss tokens
        private static readonly Color COL_BG_HOVER    = new Color(0f,    0.82f, 0.55f, 0.08f);
        private static readonly Color COL_BG_SELECTED = new Color(0f,    0.82f, 0.55f, 0.20f);
        private static readonly Color COL_ACCENT      = new Color(0f,    0.82f, 0.55f, 1f);
        private static readonly Color COL_TEXT        = new Color(0.86f, 0.92f, 0.90f, 1f);
        private static readonly Color COL_STAR        = new Color(1f,    0.82f, 0.31f, 1f);
        private static readonly Color COL_PLANET      = new Color(0.39f, 0.78f, 0.47f, 1f);
        private static readonly Color COL_MOON        = new Color(0.51f, 0.63f, 0.61f, 1f);
        private static readonly Color COL_TRANSPARENT = new Color(0, 0, 0, 0);

        private readonly VisualElement         _container;
        private readonly SelectionManager      _selection;
        private readonly MapCameraController   _camera;

        private readonly List<TreeItemBinder> _binders = new List<TreeItemBinder>();
        private TreeItemBinder _selectedBinder;

        public ObjectTreeUIController(
            VisualElement         container,
            VisualTreeAsset       itemTemplate,   // kept for signature compat, not used
            SelectionManager      selection,
            MapCameraController   camera,
            ILocalizationProvider loc)
        {
            _container = container;
            _selection = selection;
            _camera    = camera;

            // Style the container itself
            _container.style.flexDirection = FlexDirection.Column;
            _container.style.flexGrow      = 1;

            if (_selection != null)
                _selection.OnSelectionChanged += OnExternalSelectionChanged;
        }

        public void Dispose()
        {
            if (_selection != null)
                _selection.OnSelectionChanged -= OnExternalSelectionChanged;
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        public void Build(IReadOnlyList<CelestialBodyView> views)
        {
            _container.Clear();
            _binders.Clear();
            _selectedBinder = null;

            var childMap = new Dictionary<string, List<CelestialBodyView>>();
            var roots    = new List<CelestialBodyView>();

            foreach (var view in views)
            {
                if (view.Definition == null) continue;
                if (string.IsNullOrEmpty(view.Definition.parentId))
                    roots.Add(view);
                else
                {
                    var pid = view.Definition.parentId;
                    if (!childMap.ContainsKey(pid)) childMap[pid] = new List<CelestialBodyView>();
                    childMap[pid].Add(view);
                }
            }

            foreach (var root in roots)
                InsertNode(root, childMap, 0);
        }

        // ------------------------------------------------------------------
        // Node — built entirely with inline styles, no USS dependency
        // ------------------------------------------------------------------

        private void InsertNode(
            CelestialBodyView view,
            Dictionary<string, List<CelestialBodyView>> childMap,
            int depth)
        {
            // Row container
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.alignItems     = Align.Center;
            row.style.height         = 34;
            row.style.paddingLeft    = 8 + depth * 18;
            row.style.paddingRight   = 8;
            row.style.backgroundColor = new StyleColor(COL_TRANSPARENT);

            // Colour dot
            var dot = new VisualElement();
            dot.style.width        = 7;
            dot.style.height       = 7;
            dot.style.borderTopLeftRadius     = 4;
            dot.style.borderTopRightRadius    = 4;
            dot.style.borderBottomLeftRadius  = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight  = 8;
            dot.style.flexShrink   = 0;
            dot.style.backgroundColor = new StyleColor(DotColor(view.Definition.bodyType));
            row.Add(dot);

            // Label
            var label = new Label(view.Definition.bodyName ?? view.BodyId ?? "?");
            label.style.color     = new StyleColor(COL_TEXT);
            label.style.fontSize  = 13;
            label.style.flexGrow  = 1;
            label.style.overflow  = Overflow.Hidden;
            row.Add(label);

            // Hover effect
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_selectedBinder?.Row != row)
                    row.style.backgroundColor = new StyleColor(COL_BG_HOVER);
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_selectedBinder?.Row != row)
                    row.style.backgroundColor = new StyleColor(COL_TRANSPARENT);
            });

            // Click
            var binder = new TreeItemBinder(view, row, label, this);
            row.RegisterCallback<ClickEvent>(_ => OnItemClicked(binder));
            _binders.Add(binder);

            _container.Add(row);

            // Recurse
            if (view.BodyId != null && childMap.TryGetValue(view.BodyId, out var children))
                foreach (var child in children)
                    InsertNode(child, childMap, depth + 1);
        }

        // ------------------------------------------------------------------
        // Interaction
        // ------------------------------------------------------------------

        private void OnItemClicked(TreeItemBinder binder)
        {
            _selection?.SelectBody(binder.View);
            _camera?.FocusOnTarget(binder.View);
        }

        internal void SetSelectedBinder(TreeItemBinder binder)
        {
            // Deselect previous
            if (_selectedBinder != null)
            {
                _selectedBinder.Row.style.backgroundColor = new StyleColor(COL_TRANSPARENT);
                _selectedBinder.Row.style.borderLeftWidth = 0;
                _selectedBinder.Label.style.color = new StyleColor(COL_TEXT);
            }

            _selectedBinder = binder;

            // Select new
            if (_selectedBinder != null)
            {
                _selectedBinder.Row.style.backgroundColor = new StyleColor(COL_BG_SELECTED);
                _selectedBinder.Row.style.borderLeftWidth = 2;
                _selectedBinder.Row.style.borderLeftColor = new StyleColor(COL_ACCENT);
                _selectedBinder.Label.style.color = new StyleColor(COL_ACCENT);
            }
        }

        private void OnExternalSelectionChanged(CelestialBodyView view)
        {
            if (view == null) { SetSelectedBinder(null); return; }
            foreach (var b in _binders)
                if (b.View == view) { SetSelectedBinder(b); return; }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Color DotColor(BodyType type) => type switch
        {
            BodyType.Star   => COL_STAR,
            BodyType.Planet => COL_PLANET,
            BodyType.Moon   => COL_MOON,
            _               => COL_PLANET,
        };
    }

    // ------------------------------------------------------------------
    // Binder
    // ------------------------------------------------------------------

    public class TreeItemBinder
    {
        public CelestialBodyView        View  { get; }
        public VisualElement            Row   { get; }
        public Label                    Label { get; }
        private readonly ObjectTreeUIController _owner;

        public TreeItemBinder(CelestialBodyView view, VisualElement row, Label label, ObjectTreeUIController owner)
        {
            View  = view;
            Row   = row;
            Label = label;
            _owner = owner;
        }
    }
}
