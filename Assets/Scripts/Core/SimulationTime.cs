using UnityEngine;

namespace SolarTerminal.Core
{
    /// <summary>
    /// Centralized simulation time controller.
    /// All simulation systems must read simulationDeltaTime instead of Time.deltaTime.
    /// </summary>
    public class SimulationTime : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField] private float _timeScale = 1f;
        [SerializeField] private bool  _isPaused  = false;

        /// <summary>Current accumulated simulation time in seconds.</summary>
        public float SimTime       { get; private set; }

        /// <summary>Scaled delta time for this frame. Zero when paused.</summary>
        public float SimDeltaTime  { get; private set; }

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
            SimDeltaTime = _isPaused ? 0f : Time.deltaTime * _timeScale;
            SimTime     += SimDeltaTime;
        }

        public void Pause()   => _isPaused = true;
        public void Resume()  => _isPaused = false;
        public void TogglePause() => _isPaused = !_isPaused;

        /// <summary>Set time scale by common presets.</summary>
        public void SetPreset(TimePreset preset)
        {
            switch (preset)
            {
                case TimePreset.Realtime: TimeScale = 1f;    break;
                case TimePreset.Fast:     TimeScale = 10f;   break;
                case TimePreset.VeryFast: TimeScale = 100f;  break;
                case TimePreset.Warp:     TimeScale = 1000f; break;
            }
        }
    }

    public enum TimePreset { Realtime, Fast, VeryFast, Warp }
}
