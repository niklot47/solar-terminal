using UnityEngine;

namespace SolarTerminal.View
{
    /// <summary>
    /// Utility: returns the main camera and its current distance to a world point.
    /// Centralizes camera access so representation controllers don't each call Camera.main.
    ///
    /// Used by BodyRepresentationController.
    /// Could also be used by label system, LOD system, etc. in the future.
    /// </summary>
    public static class CameraDistanceEvaluator
    {
        private static Camera _cachedCamera;

        /// <summary>
        /// Returns the scene's main camera. Caches after first call.
        /// Call InvalidateCache() if the camera is replaced at runtime.
        /// </summary>
        public static Camera MainCamera
        {
            get
            {
                if (_cachedCamera == null)
                    _cachedCamera = Camera.main;
                return _cachedCamera;
            }
        }

        /// <summary>World-space distance from main camera to a point.</summary>
        public static float DistanceTo(Vector3 worldPoint)
        {
            var cam = MainCamera;
            if (cam == null) return float.MaxValue;
            return Vector3.Distance(cam.transform.position, worldPoint);
        }

        /// <summary>Call if the camera GameObject is replaced at runtime.</summary>
        public static void InvalidateCache() => _cachedCamera = null;
    }
}
