using System;
using UnityEngine;
using SolarTerminal.View;

namespace SolarTerminal.Core
{
    /// <summary>
    /// Maintains the currently selected celestial body.
    /// Acts as the single source of truth for selection state.
    /// Wire UI and camera to OnSelectionChanged — they must NOT poll this each frame.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Public state
        // ------------------------------------------------------------------

        /// <summary>Currently selected body. Null if nothing selected.</summary>
        public CelestialBodyView CurrentSelectedBody { get; private set; }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when selection changes. Argument may be null (cleared).</summary>
        public event Action<CelestialBodyView> OnSelectionChanged;

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Select a body. Fires OnSelectionChanged even if same body re-selected,
        /// so listeners can react to double-clicks / refocus requests.
        /// </summary>
        public void SelectBody(CelestialBodyView body)
        {
            CurrentSelectedBody = body;
            OnSelectionChanged?.Invoke(body);

            if (body != null)
                Debug.Log($"[SelectionManager] Selected: {body.Definition?.bodyName ?? body.BodyId}");
        }

        /// <summary>Deselect everything.</summary>
        public void ClearSelection()
        {
            CurrentSelectedBody = null;
            OnSelectionChanged?.Invoke(null);
        }
    }
}
