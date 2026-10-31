using UnityEngine;

namespace Megastructure
{
    /// <summary>Shared scalar helpers. Every function here has an HLSL twin in Shaders/Include/RingCommon.hlsl.</summary>
    public static class RingMath
    {
        public static float Saturate(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);

        /// <summary>HLSL smoothstep semantics (e0 may be greater than e1).</summary>
        public static float Smoothstep(float e0, float e1, float x)
        {
            float d = e1 - e0;
            float t = Saturate(Mathf.Abs(d) < 1e-12f ? (x >= e1 ? 1f : 0f) : (x - e0) / d);
            return t * t * (3f - 2f * t);
        }

        public static float Smootherstep01(float t)
        {
            t = Saturate(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static float Smootherstep(float e0, float e1, float x)
        {
            float d = e1 - e0;
            return Smootherstep01(Mathf.Abs(d) < 1e-12f ? (x >= e1 ? 1f : 0f) : (x - e0) / d);
        }

        /// <summary>Smooth maximum of Part 14.7 H8: (a + b + sqrt((a - b)^2 + k^2)) / 2, k = 0.03.</summary>
        public static float Smax(float a, float b, float k = 0.03f)
        {
            if (float.IsNegativeInfinity(a)) return b;
            if (float.IsNegativeInfinity(b)) return a;
            float d = a - b;
            return 0.5f * (a + b + Mathf.Sqrt(d * d + k * k));
        }

        /// <summary>Ramp between v0 and v1 with smootherstep over [x0, x1].</summary>
        public static float Ramp(float x, float x0, float x1, float v0, float v1) => v0 + (v1 - v0) * Smootherstep(x0, x1, x);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // ---------------- hashes (PCG, Jarzynski & Olano 2020) ----------------
        public static uint Pcg(uint v)
        {
            unchecked
            {
                uint state = v * 747796405u + 2891336453u;
                uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }
        }

        public static uint Pcg2(int x, int y) { unchecked { return Pcg((uint)x + Pcg((uint)y)); } }
        public static uint Pcg3(int x, int y, int z) { unchecked { return Pcg((uint)x + Pcg((uint)y + Pcg((uint)z))); } }

        /// <summary>Uniform [0, 1) from a hash (24 high-quality bits).</summary>
        public static float U01(uint h) => (h >> 8) * (1f / 16777216f);

        /// <summary>2D value noise in [0, 1] with quintic interpolation (twin of RingValueNoise in HLSL).</summary>
        public static float ValueNoise(float x, float y)
        {
            float fx = Mathf.Floor(x), fy = Mathf.Floor(y);
            int ix = (int)fx, iy = (int)fy;
            float tx = x - fx, ty = y - fy;
            float ux = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float uy = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);
            float a = U01(Pcg2(ix, iy)), b = U01(Pcg2(ix + 1, iy));
            float c = U01(Pcg2(ix, iy + 1)), d = U01(Pcg2(ix + 1, iy + 1));
            return Lerp(Lerp(a, b, ux), Lerp(c, d, ux), uy);
        }

        // ---------------- followers ----------------
        /// <summary>Critically damped spring step (exact for constant target). Returns new value, updates velocity.</summary>
        public static float CriticalDamp(float x, ref float v, float target, float omega, float dt)
        {
            float o = omega * dt;
            float e = Mathf.Exp(-o);
            float d = x - target;
            float temp = (v + omega * d) * dt;
            v = (v - omega * temp) * e;
            return target + (d + temp) * e;
        }

        /// <summary>Angle variant of <see cref="CriticalDamp"/> that takes the shortest way round (degrees).</summary>
        public static float CriticalDampAngle(float x, ref float v, float target, float omega, float dt)
        {
            float t = x + Mathf.DeltaAngle(x, target);
            return CriticalDamp(x, ref v, t, omega, dt);
        }

        public static float Slew(float current, float target, float maxRate, float dt)
        {
            float m = maxRate * dt;
            float d = target - current;
            if (d > m) d = m; else if (d < -m) d = -m;
            return current + d;
        }

        /// <summary>Visible fraction of a disc of radius r whose chord edge is at signed distance d from its centre.</summary>
        public static float DiscVisible(float d, float r)
        {
            if (d >= r) return 1f;
            if (d <= -r) return 0f;
            float a = r * r * Mathf.Acos(d / r) - d * Mathf.Sqrt(Mathf.Max(0f, r * r - d * d));
            return 1f - a / (Mathf.PI * r * r);
        }
    }
}
