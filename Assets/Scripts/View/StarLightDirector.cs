// StarLightDirector.cs
// Finds the star each LateUpdate and writes its position to PlanetShadowApplicator.
// PlanetShadowApplicator then pushes it into each renderer's PropertyBlock together
// with that planet's own position — so URP sees both values correctly.

using UnityEngine;
using SolarTerminal.Data;
using SolarTerminal.Bootstrap;

namespace SolarTerminal.View
{
    public class StarLightDirector : MonoBehaviour
    {
        private CelestialBodyView      _starView;
        private PlanetShadowApplicator _applicator;
        private bool _initialized;

        private void LateUpdate()
        {
            if (!_initialized) TryInit();
            if (_starView == null || _applicator == null) return;

            _applicator.starWorldPos = _starView.WorldPosition;
        }

        private void TryInit()
        {
            var bootstrap = FindFirstObjectByType<OrbitalMapBootstrap>();
            if (bootstrap == null || bootstrap.Views.Count == 0) return;

            _applicator = FindFirstObjectByType<PlanetShadowApplicator>();

            foreach (var view in bootstrap.Views)
            {
                if (view.Definition != null && view.Definition.bodyType == BodyType.Star)
                {
                    _starView    = view;
                    _initialized = true;
                    Debug.Log($"[StarLightDirector] Star: '{view.BodyId}'  applicator found: {_applicator != null}");
                    return;
                }
            }

            _initialized = true;
            Debug.LogWarning("[StarLightDirector] No BodyType.Star found.");
        }
    }
}
