using UnityEngine;
using UnityEngine.UIElements;

namespace SolarTerminal.UI
{
    /// <summary>
    /// Colour scheme asset. Create one per theme (e.g. NavyTheme, CombatTheme).
    ///
    /// Each scheme corresponds to a USS class applied to the root element.
    /// All colour overrides live in USS — C# only switches the class name.
    ///
    /// Create via: Assets > Create > SolarTerminal > UIColorScheme
    /// </summary>
    [CreateAssetMenu(menuName = "SolarTerminal/UIColorScheme", fileName = "NewColorScheme")]
    public class UIColorScheme : ScriptableObject
    {
        [Tooltip("USS class name to apply to the root element, e.g. 'theme-combat'. " +
                 "Define matching overrides in a USS file.")]
        public string themeClassName = "theme-default";

        // ------------------------------------------------------------------
        // Runtime application
        // ------------------------------------------------------------------

        private string _lastApplied;

        /// <summary>
        /// Swap theme by replacing the active theme USS class on the root element.
        /// Define each theme's colour overrides in USS using the class selector,
        /// e.g.:  .theme-combat { --color-accent: rgba(220,40,40,1); }
        /// </summary>
        public void ApplyTo(VisualElement root)
        {
            // Remove previous theme class
            if (!string.IsNullOrEmpty(_lastApplied))
                root.RemoveFromClassList(_lastApplied);

            // Apply new theme class
            if (!string.IsNullOrEmpty(themeClassName))
            {
                root.AddToClassList(themeClassName);
                _lastApplied = themeClassName;
            }
        }
    }
}
