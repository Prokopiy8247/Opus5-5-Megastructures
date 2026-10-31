using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Additive meshes for RingAdditive.shader (motes, glows, light pools) for beats 1-2. This is a local twin of the
    /// vertex layout documented in the header of Shaders/RingAdditive.shader (and written by LightPoolKit), kept here
    /// so the beat 1-2 module depends only on the Core contracts:
    ///   billboards: POSITION = centre + (uv0.x, uv0.y, 0) * e, uv0 = corner (-1..1), COLOR.rgb = tint (0..1);
    ///     mote: uv1 = (core radius m, seed 0..1), COLOR.a = sunbeam weight; glow: uv1 = (radius m, HDR intensity),
    ///     COLOR.a = lamp channel / 4;
    ///   light pool: POSITION = surface + 0.02 m, uv0 = offset from the lamp foot / radius R, uv1 = (R / lamp height h,
    ///     HDR intensity), COLOR.rgb = tint, COLOR.a = lamp channel / 4. The per-vertex R / h carries each lamp's own
    ///     height, so every pool uses the shared RingMat.LightPool material (RingAdditive has no per-material height).
    /// Lamp channels: 0 constant, 1 _Lamps.x / 3 (lamps), 2 _Lamps.y, 3 _Lamps.z, 4 _Lamps.w.
    /// </summary>
    internal static class B12Additive
    {
        public const float PoolLift = 0.02f;     // 14.5.4: pools 0.02 m above their surface
        public const float PoolRadius = 3f;      // 14.2.3: light pools radius 3 m
        public const float MoteSize = 0.004f;    // 14.2.6 M13: 4 mm quads
        public const float ChannelConstant = 0f, ChannelLamp = 0.25f;
        /// <summary>Smallest lamp height the pool profile accepts (m), keeps R / h finite (twin of LightPoolKit.MinLampHeight).</summary>
        public const float MinLampHeight = 0.05f;

        /// <summary>Pool clipped to a rectangle (u along tangent, v across) around the lamp foot; surfaceY (x, z) -> y
        /// makes a subdivided pool follow a curved deck. lampHeight = height of the lamp's emitting centre above the
        /// pool's surface (m): the shader's falloff shape (half maximum at r = 0.766 h) comes from uv1.x = R / h.</summary>
        public static void Pool(MeshBuilder mb, Vector3 foot, Vector3 tangent, float uMin, float uMax, float vMin, float vMax,
            Color tint, float intensity, float channel, float radius, int subdivisions, System.Func<float, float, float> surfaceY,
            float lampHeight)
        {
            if (!(radius > 0f)) return;
            uMin = Mathf.Max(uMin, -radius); uMax = Mathf.Min(uMax, radius);
            vMin = Mathf.Max(vMin, -radius); vMax = Mathf.Min(vMax, radius);
            if (uMax - uMin < 1e-3f || vMax - vMin < 1e-3f) return;
            float h = lampHeight > MinLampHeight ? lampHeight : MinLampHeight;   // also catches NaN
            int n = Mathf.Clamp(subdivisions, 1, 64);
            Vector3 nn = Vector3.up;
            Vector3 t = tangent; t.y = 0f;
            if (t.sqrMagnitude < 1e-8f) t = Vector3.forward;
            t.Normalize();
            Vector3 b = Vector3.Cross(nn, t).normalized;
            Split(tint, intensity, out Color rgb, out float k);
            var col = new Color(rgb.r, rgb.g, rgb.b, channel);
            var uv1 = new Vector2(radius / h, k);   // RingAdditive pool contract: uv1.x = R / lamp height
            var old = mb.M;
            mb.M = Matrix4x4.identity;
            int baseIdx = mb.VertexCount;
            for (int j = 0; j <= n; j++)
            {
                float v = Mathf.Lerp(vMin, vMax, (float)j / n);
                for (int i = 0; i <= n; i++)
                {
                    float u = Mathf.Lerp(uMin, uMax, (float)i / n);
                    Vector3 p = foot + t * u + b * v + nn * PoolLift;
                    if (surfaceY != null) p.y = surfaceY(p.x, p.z) + PoolLift;
                    int idx = mb.Vtx(p, nn, new Vector2(u / radius, v / radius), col);
                    mb.UV1[idx] = uv1;
                }
            }
            bool flip = Vector3.Dot(Vector3.Cross(t, b), nn) < 0f;
            int row = n + 1;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = baseIdx + j * row + i, a1 = a0 + 1, b1 = a0 + row + 1, b0 = a0 + row;
                    if (!flip) mb.Quad(a0, a1, b1, b0); else mb.Quad(a0, b0, b1, a1);
                }
            mb.M = old;
        }

        /// <summary>Camera-facing Gaussian glow (world core radius).</summary>
        public static void Glow(MeshBuilder mb, Vector3 centre, float radius, Color tint, float intensity, float channel)
        {
            if (radius <= 0f) return;
            Split(tint, intensity, out Color rgb, out float k);
            Billboard(mb, centre, radius, new Vector2(radius, k), new Color(rgb.r, rgb.g, rgb.b, channel));
        }

        /// <summary>One dust mote (M13).</summary>
        public static void Mote(MeshBuilder mb, Vector3 centre, float seed01, Color tint, float beamWeight)
        {
            float half = MoteSize * 0.5f;
            var col = new Color(Mathf.Clamp01(tint.r), Mathf.Clamp01(tint.g), Mathf.Clamp01(tint.b), Mathf.Clamp01(beamWeight));
            Billboard(mb, centre, half, new Vector2(half, seed01 - Mathf.Floor(seed01)), col);
        }

        static void Billboard(MeshBuilder mb, Vector3 centre, float meshHalf, Vector2 uv1, Color col)
        {
            var old = mb.M;
            Vector3 c = old.MultiplyPoint3x4(centre);
            mb.M = Matrix4x4.identity;
            var n = new Vector3(0f, 0f, -1f);
            int i0 = mb.Vtx(c + new Vector3(-meshHalf, meshHalf, 0f), n, new Vector2(-1f, 1f), col);
            int i1 = mb.Vtx(c + new Vector3(meshHalf, meshHalf, 0f), n, new Vector2(1f, 1f), col);
            int i2 = mb.Vtx(c + new Vector3(meshHalf, -meshHalf, 0f), n, new Vector2(1f, -1f), col);
            int i3 = mb.Vtx(c + new Vector3(-meshHalf, -meshHalf, 0f), n, new Vector2(-1f, -1f), col);
            mb.UV1[i0] = uv1; mb.UV1[i1] = uv1; mb.UV1[i2] = uv1; mb.UV1[i3] = uv1;
            mb.Quad(i0, i1, i2, i3);
            mb.M = old;
        }

        static void Split(Color tint, float intensity, out Color rgb, out float k)
        {
            float r = Mathf.Max(tint.r, 0f), g = Mathf.Max(tint.g, 0f), bl = Mathf.Max(tint.b, 0f);
            float mx = Mathf.Max(r, Mathf.Max(g, bl));
            k = Mathf.Max(intensity, 0f);
            if (mx > 1f) { r /= mx; g /= mx; bl /= mx; k *= mx; }
            rgb = new Color(r, g, bl, 1f);
        }

        /// <summary>Bounds growth for billboards drawn at their minimum on-screen size (2 px at 720 p, vFOV 66).</summary>
        public static Bounds GrowForBillboards(Bounds b, float maxDistance)
        {
            const float pixel = 66f * Mathf.Deg2Rad / 720f;
            b.Expand(2f * 1.5f * 0.5f * 2f * pixel * Mathf.Max(maxDistance, 0f));
            return b;
        }
    }
}
