// PlanetShadowApplicator.cs
// Spawns shadow overlay per planet. Every frame writes BOTH _StarWorldPos and
// _PlanetWorldPos into a per-renderer MaterialPropertyBlock so URP picks them up correctly.

using System.Collections.Generic;
using UnityEngine;
using SolarTerminal.Data;
using SolarTerminal.View;
using SolarTerminal.Bootstrap;

namespace SolarTerminal.View
{
    public class PlanetShadowApplicator : MonoBehaviour
    {
        [Header("Overlay material")]
        [SerializeField] private Material _overlayMaterial;
        [SerializeField] private float    _overlayScale = 1.02f;
        [SerializeField] private bool     _applyToStars = false;

        private struct OverlayEntry
        {
            public MeshRenderer renderer;
            public Transform    planetView;
        }

        private static readonly int StarWorldPosID   = Shader.PropertyToID("_StarWorldPos");
        private static readonly int PlanetWorldPosID = Shader.PropertyToID("_PlanetWorldPos");

        private OverlayEntry[]        _entries;
        private MaterialPropertyBlock _mpb;
        private bool _applied;

        // Star position filled by StarLightDirector each LateUpdate
        [HideInInspector] public Vector3 starWorldPos;

        private void Update()
        {
            if (!_applied)
            {
                var bootstrap = FindFirstObjectByType<OrbitalMapBootstrap>();
                if (bootstrap == null || bootstrap.Views.Count == 0) return;
                Apply(bootstrap);
                _applied = true;
            }

            if (_entries == null) return;

            // Write both positions into each renderer's property block every frame
            foreach (var e in _entries)
            {
                if (e.renderer == null || e.planetView == null) continue;
                _mpb.SetVector(StarWorldPosID,   (Vector4)starWorldPos);
                _mpb.SetVector(PlanetWorldPosID, (Vector4)e.planetView.position);
                e.renderer.SetPropertyBlock(_mpb);
            }
        }

        private void Apply(OrbitalMapBootstrap bootstrap)
        {
            if (_overlayMaterial == null)
            {
                Debug.LogWarning("[PlanetShadowApplicator] No overlay material assigned.");
                return;
            }

            _mpb = new MaterialPropertyBlock();
            var list = new List<OverlayEntry>();

            foreach (var view in bootstrap.Views)
            {
                if (view.Definition == null) continue;
                if (!_applyToStars && view.Definition.bodyType == BodyType.Star) continue;

                float radius = view.Definition.visualRadius > 0 ? view.Definition.visualRadius : 1f;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"ShadowOverlay_{view.BodyId}";
                go.transform.SetParent(view.transform, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale    = Vector3.one * radius * _overlayScale;
                go.transform.rotation      = Quaternion.identity;

                Destroy(go.GetComponent<Collider>());

                var rend = go.GetComponent<MeshRenderer>();
                rend.sharedMaterial    = _overlayMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows    = false;

                list.Add(new OverlayEntry { renderer = rend, planetView = view.transform });
            }

            _entries = list.ToArray();
            Debug.Log($"[PlanetShadowApplicator] Done — {_entries.Length} overlays created.");
        }

        [ContextMenu("Force Apply Now")]
        public void ForceApply()
        {
            var bootstrap = FindFirstObjectByType<OrbitalMapBootstrap>();
            if (bootstrap == null) return;
            Apply(bootstrap);
            _applied = true;
        }
    }
}
