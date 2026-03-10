using UnityEngine;

namespace SolarTerminal.Core
{
    /// <summary>
    /// Centralized simulation time controller.
    ///
    /// Canonical time unit: HOURS.
    /// All simulation systems (orbital periods, rotation periods) use hours.
    ///
    /// TimeScale = 1  →  1 real second = 1 simulation second = 1/3600 simulation hour
    ///                    Earth rotates once in ~23.9h real time (correct)
    /// TimeScale = 3600 → 1 real second = 1 simulation hour
    ///                    Earth rotates once in ~24 real seconds
    /// </summary>
    public class SimulationTime : MonoBehaviour
    {
        private const float SECONDS_PER_HOUR = 3600f;

        [Header("Time Settings")]
        [SerializeField] private float _timeScale = 1f;
        [SerializeField] private bool  _isPaused  = false;

        /// <summary>
        /// Current accumulated simulation time in HOURS.
        /// </summary>
        public float SimTime { get; private set; }

        /// <summary>
        /// Simulation delta time in HOURS for this frame. Zero when paused.
        /// Formula: (Time.deltaTime / 3600) * TimeScale
        /// </summary>
        public float SimDeltaTime { get; private set; }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        private void Update()
        {
            // Convert real seconds → simulation hours, then apply time scale
            SimDeltaTime = _isPaused ? 0f : (Time.deltaTime / SECONDS_PER_HOUR) * _timeScale;
            SimTime     += SimDeltaTime;
        }

        public void Pause()       => _isPaused = true;
        public void Resume()      => _isPaused = false;
        public void TogglePause() => _isPaused = !_isPaused;

        /// <summary>Set time scale by common presets.</summary>
        public void SetPreset(TimePreset preset)
        {
            switch (preset)
            {
                case TimePreset.Realtime: TimeScale = 1f;       break; // 1 real sec = 1 sim sec
                case TimePreset.Fast:     TimeScale = 3600f;    break; // 1 real sec = 1 sim hour
                case TimePreset.VeryFast: TimeScale = 86400f;   break; // 1 real sec = 1 sim day
                case TimePreset.Warp:     TimeScale = 2628000f; break; // 1 real sec = 1 sim month
            }
        }
    }

    public enum TimePreset { Realtime, Fast, VeryFast, Warp }
}
