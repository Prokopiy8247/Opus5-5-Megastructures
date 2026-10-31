using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// C# twin of Shaders/Include/RingBend.hlsl (Part 14.7 H1). Geometry, colliders and the path are flat; every
    /// shader rolls space onto the cylinder (axis parallel to X, radius R, centre line at y = R) relative to the
    /// camera z. Numerically stable form: B.y = h cos t + 2 R sin^2(t / 2).
    /// </summary>
    public static class RingBend
    {
        public const float R = RingConstants.RingR;

        /// <summary>float32 replica of the HLSL vertex bend (same operation order).</summary>
        public static Vector3 Bend(Vector3 p, Vector3 cam)
        {
            float d = p.z - cam.z;
            float t = d / R;
            float h = Mathf.Clamp(p.y, -50f, 0.9f * R);
            float st = Mathf.Sin(t), ct = Mathf.Cos(t);
            float sh = Mathf.Sin(0.5f * t);
            return new Vector3(p.x, h * ct + 2f * R * sh * sh, cam.z + (R - h) * st);
        }

        /// <summary>Double-precision reference of the bend.</summary>
        public static void BendD(double px, double py, double pz, double camZ, out double bx, out double by, out double bz)
        {
            double d = pz - camZ;
            double t = d / RingConstants.RingRD;
            double h = System.Math.Min(System.Math.Max(py, -50.0), 0.9 * RingConstants.RingRD);
            bx = px;
            by = RingConstants.RingRD - (RingConstants.RingRD - h) * System.Math.Cos(t);
            bz = camZ + (RingConstants.RingRD - h) * System.Math.Sin(t);
        }

        /// <summary>Rotation of normals, tangents and the key vector about +X by the bend angle t.</summary>
        public static Vector3 Rotate(Vector3 n, float t)
        {
            float s = Mathf.Sin(t), c = Mathf.Cos(t);
            return new Vector3(n.x, n.y * c + n.z * s, -n.y * s + n.z * c);
        }

        /// <summary>Angle of the bend for a point at axial distance d (= p.z - cam.z).</summary>
        public static float Theta(float d) => d / R;

        /// <summary>Vertical rise R (1 - cos(maxD / R)) used to inflate pre-bend bounds; stable form.</summary>
        public static float Rise(float maxD)
        {
            float s = Mathf.Sin(0.5f * maxD / R);
            return 2f * R * s * s;
        }

        /// <summary>Inflates pre-bend bounds so the bent geometry is never culled (y gets the rise, z a small margin).</summary>
        public static Bounds InflateBounds(Bounds b, float maxDistance)
        {
            float rise = Rise(maxDistance);
            // The bent z contracts by at most (R - h)(t - sin t) + h sin t; a margin of rise covers it generously.
            b.Expand(new Vector3(0f, 2f * rise + 1f, 2f * rise + 1f));
            return b;
        }

        public static void InflateBounds(Renderer renderer, float maxDistance)
        {
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var m = mf.sharedMesh;
                m.bounds = InflateBounds(m.bounds, maxDistance);
            }
        }

        /// <summary>World bounds for an instanced draw that covers everything within maxDistance of the path area.</summary>
        public static Bounds WorldBounds(Vector3 centre, Vector3 size, float maxDistance) => InflateBounds(new Bounds(centre, size), maxDistance);
    }
}
