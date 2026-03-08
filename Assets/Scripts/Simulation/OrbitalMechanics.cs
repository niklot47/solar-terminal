using UnityEngine;

namespace SolarTerminal.Simulation
{
    /// <summary>
    /// Pure-static Keplerian orbital mechanics solver.
    /// No allocations, no MonoBehaviour, no Unity lifecycle dependencies.
    ///
    /// Coordinate convention:
    ///   Orbit plane is XZ (Y = up). Inclination tilts the orbit out of XZ.
    ///   All angles in radians unless noted otherwise.
    /// </summary>
    public static class OrbitalMechanics
    {
        // Maximum iterations for Newton-Raphson Kepler solver
        private const int   KEPLER_MAX_ITER  = 50;
        private const float KEPLER_TOLERANCE = 1e-6f;

        // ------------------------------------------------------------------
        // Kepler equation: M = E - e * sin(E)
        // Solve for E (eccentric anomaly) given M (mean anomaly) and e (eccentricity)
        // ------------------------------------------------------------------

        /// <summary>
        /// Solve Kepler's equation M = E - e·sin(E) for eccentric anomaly E.
        /// Uses Newton-Raphson iteration. Converges in < 10 steps for e < 0.9.
        /// </summary>
        public static float SolveKeplerEquation(float meanAnomaly, float eccentricity)
        {
            // Normalize M to [0, 2π]
            float M = meanAnomaly % (2f * Mathf.PI);
            if (M < 0f) M += 2f * Mathf.PI;

            // Initial guess — Danby (1988) starter
            float E = M + eccentricity * Mathf.Sin(M) * (1f + eccentricity * Mathf.Cos(M));

            for (int i = 0; i < KEPLER_MAX_ITER; i++)
            {
                float dE = (M - E + eccentricity * Mathf.Sin(E))
                         / (1f - eccentricity * Mathf.Cos(E));
                E += dE;
                if (Mathf.Abs(dE) < KEPLER_TOLERANCE) break;
            }

            return E;
        }

        // ------------------------------------------------------------------
        // True anomaly from eccentric anomaly
        // ------------------------------------------------------------------

        /// <summary>
        /// Compute true anomaly ν from eccentric anomaly E and eccentricity e.
        /// Returns value in [-π, π].
        /// </summary>
        public static float ComputeTrueAnomaly(float eccentricAnomaly, float eccentricity)
        {
            float E = eccentricAnomaly;
            float e = eccentricity;

            // Standard formula: tan(ν/2) = sqrt((1+e)/(1-e)) · tan(E/2)
            float halfE     = E * 0.5f;
            float tanHalfNu = Mathf.Sqrt((1f + e) / Mathf.Max(1f - e, 1e-6f)) * Mathf.Tan(halfE);
            return 2f * Mathf.Atan(tanHalfNu);
        }

        // ------------------------------------------------------------------
        // 3D world position from orbital elements
        // ------------------------------------------------------------------

        /// <summary>
        /// Compute world-space position of an orbiting body relative to its parent (at origin).
        ///
        /// Steps:
        ///   1. Compute mean anomaly from time and period
        ///   2. Solve Kepler → eccentric anomaly
        ///   3. Compute true anomaly
        ///   4. Compute position in orbital plane (perifocal frame)
        ///   5. Rotate into world space via Ω (LAN), i (inclination), ω (ArgPeri)
        /// </summary>
        /// <param name="semiMajorAxis">a — semi-major axis (sim units)</param>
        /// <param name="eccentricity">e — 0 = circle, 0..1 = ellipse</param>
        /// <param name="inclination">i — tilt of orbital plane (radians)</param>
        /// <param name="longitudeOfAscendingNode">Ω — rotation of ascending node (radians)</param>
        /// <param name="argumentOfPeriapsis">ω — rotation of periapsis within orbital plane (radians)</param>
        /// <param name="meanAnomalyAtEpoch">M₀ — mean anomaly at t=0 (radians)</param>
        /// <param name="orbitalPeriod">T — time for one full orbit (sim seconds)</param>
        /// <param name="simulationTime">Current simulation time (sim seconds)</param>
        /// <returns>Position relative to parent body in world space (XZ plane is ecliptic).</returns>
        public static Vector3 ComputeOrbitalPosition(
            float semiMajorAxis,
            float eccentricity,
            float inclination,
            float longitudeOfAscendingNode,
            float argumentOfPeriapsis,
            float meanAnomalyAtEpoch,
            float orbitalPeriod,
            float simulationTime)
        {
            if (orbitalPeriod <= 0f || semiMajorAxis <= 0f)
                return Vector3.zero;

            // 1. Mean anomaly at current time
            float n = 2f * Mathf.PI / orbitalPeriod;          // mean motion (rad/s)
            float M = meanAnomalyAtEpoch + n * simulationTime;

            // 2. Eccentric anomaly
            float E = SolveKeplerEquation(M, eccentricity);

            // 3. True anomaly
            float nu = ComputeTrueAnomaly(E, eccentricity);

            // 4. Distance from focus (parent body)
            float r = semiMajorAxis * (1f - eccentricity * Mathf.Cos(E));

            // 5. Position in perifocal (orbital) frame: x̂ points toward periapsis
            float xOrbit = r * Mathf.Cos(nu);
            float zOrbit = r * Mathf.Sin(nu);

            // 6. Rotate to world frame
            //    Uses 3-1-3 Euler rotation: Ω (around Y), i (around X), ω (around Y)
            //    We map the orbital XZ plane so inclination tilts around the X axis.
            return RotateToWorldFrame(xOrbit, zOrbit, inclination,
                                      longitudeOfAscendingNode, argumentOfPeriapsis);
        }

        // ------------------------------------------------------------------
        // Frame rotation — perifocal → world
        // ------------------------------------------------------------------

        /// <summary>
        /// Rotate a point from the perifocal (orbital) frame to world space.
        /// World convention: ecliptic plane is XZ, Y is up.
        ///
        /// Rotation order (standard):
        ///   1. ω  — argument of periapsis  (rotate in orbital plane)
        ///   2. i  — inclination            (tilt plane out of ecliptic)
        ///   3. Ω  — longitude of ascending node (rotate around Y)
        /// </summary>
        public static Vector3 RotateToWorldFrame(
            float xOrbit, float zOrbit,
            float inclination,
            float longitudeOfAscendingNode,
            float argumentOfPeriapsis)
        {
            float cosO = Mathf.Cos(longitudeOfAscendingNode);
            float sinO = Mathf.Sin(longitudeOfAscendingNode);
            float cosI = Mathf.Cos(inclination);
            float sinI = Mathf.Sin(inclination);
            float cosW = Mathf.Cos(argumentOfPeriapsis);
            float sinW = Mathf.Sin(argumentOfPeriapsis);

            // Combine into rotation matrix rows (world X, Y, Z):
            // P vector (toward periapsis in world space)
            float Px = cosO * cosW - sinO * sinW * cosI;
            float Py = sinO * sinW * sinI;       // Y component (out of ecliptic)
            float Pz = sinO * cosW + cosO * sinW * cosI;

            // Q vector (90° from P in orbital plane, toward +ν direction)
            float Qx = -cosO * sinW - sinO * cosW * cosI;
            float Qy =  sinO * cosW * sinI;
            float Qz = -sinO * sinW + cosO * cosW * cosI;

            // World position = xOrbit * P + zOrbit * Q
            // (xOrbit is along periapsis axis, zOrbit is along semi-latus rectum axis)
            float wx = xOrbit * Px + zOrbit * Qx;
            float wy = xOrbit * Py + zOrbit * Qy;
            float wz = xOrbit * Pz + zOrbit * Qz;

            return new Vector3(wx, wy, wz);
        }

        // ------------------------------------------------------------------
        // Orbit ellipse sampling — used by OrbitLineView
        // ------------------------------------------------------------------

        /// <summary>
        /// Fill a pre-allocated array with world-space points tracing the full orbit ellipse.
        /// No allocation — caller provides the array.
        /// </summary>
        public static void SampleOrbitPoints(
            Vector3[] outPoints,
            float semiMajorAxis,
            float eccentricity,
            float inclination,
            float longitudeOfAscendingNode,
            float argumentOfPeriapsis)
        {
            int count = outPoints.Length;
            float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(Mathf.Max(0f, 1f - eccentricity * eccentricity));

            for (int i = 0; i < count; i++)
            {
                // Parametric angle (eccentric anomaly surrogate for uniform point distribution)
                float E   = (i / (float)count) * 2f * Mathf.PI;
                float nu  = ComputeTrueAnomaly(E, eccentricity);
                float r   = semiMajorAxis * (1f - eccentricity * Mathf.Cos(E));

                float xOrbit = r * Mathf.Cos(nu);
                float zOrbit = r * Mathf.Sin(nu);

                outPoints[i] = RotateToWorldFrame(
                    xOrbit, zOrbit, inclination, longitudeOfAscendingNode, argumentOfPeriapsis);
            }
        }
    }
}
