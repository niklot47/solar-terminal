// PlanetLightingApplicator.cs
// Optional helper — applies PlanetLit material to all CelestialBodyView objects at startup.
// Attach to the OrbitalSystem GameObject alongside OrbitalMapBootstrap.
//
// If your procedural planet asset already assigns a material via its own workflow,
// use ApplyMode.Override only for bodies that need the custom shader.

using System.Collections.Generic;
using UnityEngine;
using SolarTerminal.View;

namespace SolarTerminal.View
{
    /// <summary>
    /// Scans all CelestialBodyView instances and applies the PlanetLit material
    /// to their renderers. Runs once in Start(), after Bootstrap has created views.
    ///
    /// Two modes:
    ///   ApplyToAll    — replaces every renderer's material (simplest setup)
    ///   ApplyByType   — applies different materials per BodyType (Star/Planet/Moon)
    /// </summary>
    public class PlanetLightingApplicator : MonoBehaviour
    {
        public enum ApplyMode { ApplyToAll, ApplyByType }

        [Header("Mode")]
        [SerializeField] private ApplyMode _mode = ApplyMode.ApplyToAll;

        [Header("Materials")]
        [Tooltip("Applied to all bodies when mode = ApplyToAll, " +
                 "or to Planet/Moon when mode = ApplyByType.")]
        [SerializeField] private Material _planetMaterial;

        [Tooltip("Applied to Star bodies when mode = ApplyByType. " +
                 "Leave null to skip stars (recommended — stars should self-illuminate).")]
        [SerializeField] private Material _starMaterial;

        [Tooltip("Applied to Moon bodies when mode = ApplyByType. " +
                 "Leave null to use _planetMaterial for moons too.")]
        [SerializeField] private Material _moonMaterial;

        [Header("Options")]
        [Tooltip("Delay in frames before applying — gives Bootstrap time to create views.")]
        [SerializeField] private int _applyAfterFrames = 2;

        private int _frameCounter;
        private bool _applied;

        private void Update()
        {
            if (_applied) return;
            if (++_frameCounter < _applyAfterFrames) return;

            var bootstrap = FindFirstObjectByType<SolarTerminal.Bootstrap.OrbitalMapBootstrap>();
            if (bootstrap == null || bootstrap.Views.Count == 0) return; // retry next frame

            Apply(bootstrap);
            _applied = true;
        }

        private void Apply(SolarTerminal.Bootstrap.OrbitalMapBootstrap bootstrap)
        {
            int count = 0;
            foreach (var view in bootstrap.Views)
            {
                var mat = ResolveMaterial(view);
                if (mat == null) continue;

                var renderers = view.GetComponentsInChildren<Renderer>(includeInactive: true);
                foreach (var rend in renderers)
                {
                    rend.sharedMaterial = mat;
                    count++;
                }
            }

            Debug.Log($"[PlanetLightingApplicator] Applied materials to {count} renderer(s).");
        }

        private Material ResolveMaterial(CelestialBodyView view)
        {
            if (_mode == ApplyMode.ApplyToAll)
                return _planetMaterial;

            var def = view.Definition;
            if (def == null) return _planetMaterial;

            return def.bodyType switch
            {
                SolarTerminal.Data.BodyType.Star   => _starMaterial,
                SolarTerminal.Data.BodyType.Moon   => _moonMaterial ?? _planetMaterial,
                _                                  => _planetMaterial,
            };
        }

        [ContextMenu("Force Apply Materials Now")]
        public void ForceApply()
        {
            var bootstrap = FindFirstObjectByType<SolarTerminal.Bootstrap.OrbitalMapBootstrap>();
            if (bootstrap == null) { Debug.LogWarning("[PlanetLightingApplicator] Bootstrap not found."); return; }
            Apply(bootstrap);
            _applied = true;
        }
    }
}
