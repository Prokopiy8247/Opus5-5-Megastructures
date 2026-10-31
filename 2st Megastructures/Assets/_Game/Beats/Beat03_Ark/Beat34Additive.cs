using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Additive meshes for RingAdditive.shader (RingMat.LightPool, RingMat.Glow) of the beat-3/4 builders. This is a
    /// local twin of the Rendering module's LightPoolKit (Scripts/Runtime/Rendering/LightPoolKit.cs), kept here so the
    /// beat-3/4 module compiles against the Core contracts alone (fastcheck --only), as B12Additive does for beats 1-2.
    /// It writes exactly the vertex layout of the RingAdditive.shader header and of LightPoolKit:
    ///   light pool: POSITION = surface + 0.02 m along the up normal, uv0 = offset from the lamp foot / R,
    ///     uv1 = (R / lamp height h, HDR intensity), COLOR.rgb = tint (0..1), COLOR.a = lamp channel / 4;
    ///   glow: POSITION = centre + (uv0.x, uv0.y, 0) x r (object-space XY quad, normal -Z), uv0 = corner (-1..1),
    ///     uv1 = (core radius m, HDR intensity), COLOR.a = lamp channel / 4.
    /// HDR tints are split as in LightPoolKit (vertex colour 0..1, the excess folded into the uv1 intensity).
    /// Keep in sync with LightPoolKit (PoolLift, MinLampHeight, QuadPad, MaxPixelAngle, DefaultMinPx, the layout);
    /// delete this twin once LightPoolKit is part of the always-compiled contract set.
    /// </summary>
    internal static class Beat34Additive
    {
        /// <summary>LightPoolKit.PoolLift: pools float 0.02 m above their surface (14.5.4) plus the material's polygon offset.</summary>
        public const float PoolLift = 0.02f;
        /// <summary>LightPoolKit.PoolRadius: light pools of radius 3 m (14.2.3).</summary>
        public const float PoolRadius = 3f;
        /// <summary>LightPoolKit.MinLampHeight: smallest lamp height the pool profile accepts (m).</summary>
        public const float MinLampHeight = 0.05f;
        /// <summary>LightPoolKit.QuadPad = RING_QUAD_PAD of RingAdditive.shader: drawn quad half-extent in core radii.</summary>
        public const float QuadPad = 1.5f;
        /// <summary>LightPoolKit.DefaultMinPx: minimum core diameter of the Glow material ("min 2 px", 14.2.6).</summary>
        public const float DefaultMinPx = 2f;
        /// <summary>LightPoolKit.MaxPixelAngle: 720 px over the widest vFOV the rules allow (68 deg): the bounds use it.</summary>
        public const float MaxPixelAngle = 68f * Mathf.Deg2Rad / 720f;
        /// <summary>Lamp channel codes (COLOR.a = channel / 4): constant level, and _Lamps.x / 3 (lamps and bollards).</summary>
        public const float ChannelConstant = 0f, ChannelLamp = 0.25f;

        /// <summary>WarmLamp (14.2.1), the pool tint of lamps and bollards.</summary>
        public static Color WarmLamp => RingPalette.Lin("WarmLamp");

        /// <summary>
        /// Pool on a horizontal surface whose top passes through <paramref name="foot"/> (the point under the lamp),
        /// clipped to the rectangle [uMin, uMax] x [vMin, vMax] of the frame u = <paramref name="tangent"/> (made
        /// horizontal), v = Cross(up, u), intersected with [-radius, radius]^2. The analytic falloff stays centred on the
        /// foot. <paramref name="lampHeight"/> = height of the lamp's emitting centre above this surface (m). Returns the
        /// index of the first vertex written ((subdivisions + 1)^2 vertices, row-major in v then u), or -1 when the
        /// clipped rectangle is empty. MeshBuilder.M must be the identity or rigid.
        /// </summary>
        public static int PoolRect(MeshBuilder mb, Vector3 foot, Vector3 tangent, float uMin, float uMax, float vMin, float vMax,
            Color tint, float intensity, float channel, float lampHeight, int subdivisions, float radius = PoolRadius)
        {
            if (mb == null || !(radius > 0f)) return -1;
            uMin = Mathf.Max(uMin, -radius); uMax = Mathf.Min(uMax, radius);
            vMin = Mathf.Max(vMin, -radius); vMax = Mathf.Min(vMax, radius);
            if (uMax - uMin < 1e-3f || vMax - vMin < 1e-3f) return -1;
            int n = Mathf.Clamp(subdivisions, 1, 64);

            Matrix4x4 m = mb.M;
            Vector3 f = m.MultiplyPoint3x4(foot);
            Vector3 nn = Vector3.up;
            Vector3 t = m.MultiplyVector(tangent);
            t.y = 0f;
            if (t.sqrMagnitude < 1e-12f) t = Vector3.forward;
            t.Normalize();
            Vector3 b = Vector3.Cross(nn, t).normalized;

            Split(tint, intensity, out Color rgb, out float k);
            var col = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(channel));
            float h = float.IsNaN(lampHeight) ? 1.1f : Mathf.Max(lampHeight, MinLampHeight);
            var uv1 = new Vector2(radius / h, k);

            mb.M = Matrix4x4.identity;
            int baseIdx = mb.VertexCount;
            for (int j = 0; j <= n; j++)
            {
                float v = Mathf.Lerp(vMin, vMax, (float)j / n);
                for (int i = 0; i <= n; i++)
                {
                    float u = Mathf.Lerp(uMin, uMax, (float)i / n);
                    Vector3 p = f + t * u + b * v + nn * PoolLift;
                    int idx = mb.Vtx(p, nn, new Vector2(u / radius, v / radius), col);
                    mb.UV1[idx] = uv1;
                }
            }
            // MeshBuilder front face = Cross(b - a, c - a) (clockwise seen from the front): face up
            bool flip = Vector3.Dot(Vector3.Cross(t, b), nn) < 0f;
            int row = n + 1;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = baseIdx + j * row + i, a1 = a0 + 1, b1 = a0 + row + 1, b0 = a0 + row;
                    if (!flip) mb.Quad(a0, a1, b1, b0); else mb.Quad(a0, b0, b1, a1);
                }
            mb.M = m;
            return baseIdx;
        }

        /// <summary>
        /// Wall wash: the same pool (same vertex layout, same windowed point-lamp profile) on a VERTICAL face, for a lamp
        /// held <paramref name="lampDistance"/> m in front of the face at <paramref name="foot"/> (the point of the face
        /// nearest the lamp). Frame u = <paramref name="along"/> (horizontal, in the face), v = up; the quad covers
        /// [uMin, uMax] x [vMin, vMax] intersected with [-radius, radius]^2 and floats PoolLift along the outward
        /// <paramref name="normal"/> (polygon offset in the material as for floor pools). Returns the first vertex index
        /// or -1. MeshBuilder.M must be the identity.
        /// </summary>
        public static int WallPool(MeshBuilder mb, Vector3 foot, Vector3 normal, Vector3 along, float uMin, float uMax, float vMin, float vMax,
            Color tint, float intensity, float channel, float lampDistance, int subdivisions, float radius = PoolRadius)
        {
            if (mb == null || !(radius > 0f)) return -1;
            uMin = Mathf.Max(uMin, -radius); uMax = Mathf.Min(uMax, radius);
            vMin = Mathf.Max(vMin, -radius); vMax = Mathf.Min(vMax, radius);
            if (uMax - uMin < 1e-3f || vMax - vMin < 1e-3f) return -1;
            int n = Mathf.Clamp(subdivisions, 1, 64);
            Vector3 nn = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.back;
            Vector3 t = along - Vector3.Dot(along, nn) * nn;
            t.y = 0f;
            if (t.sqrMagnitude < 1e-12f) t = Vector3.Cross(Vector3.up, nn);
            t.Normalize();
            Vector3 up = Vector3.Cross(nn, t).normalized;
            if (up.y < 0f) { up = -up; }
            Split(tint, intensity, out Color rgb, out float k);
            var col = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(channel));
            float h = Mathf.Max(lampDistance, MinLampHeight);
            var uv1 = new Vector2(radius / h, k);
            int baseIdx = mb.VertexCount;
            for (int j = 0; j <= n; j++)
            {
                float v = Mathf.Lerp(vMin, vMax, (float)j / n);
                for (int i = 0; i <= n; i++)
                {
                    float u = Mathf.Lerp(uMin, uMax, (float)i / n);
                    int idx = mb.Vtx(foot + t * u + up * v + nn * PoolLift, nn, new Vector2(u / radius, v / radius), col);
                    mb.UV1[idx] = uv1;
                }
            }
            // MeshBuilder front face = Cross(b - a, c - a) (clockwise seen from the front): face along the normal
            bool flip = Vector3.Dot(Vector3.Cross(t, up), nn) < 0f;
            int row = n + 1;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = baseIdx + j * row + i, a1 = a0 + 1, b1 = a0 + row + 1, b0 = a0 + row;
                    if (!flip) mb.Quad(a0, a1, b1, b0); else mb.Quad(a0, b0, b1, a1);
                }
            return baseIdx;
        }

        /// <summary>Scales the HDR intensity (uv1.y) of vertices [first, end) by visibility(world position) in 0..1 (a
        /// baked occluder shadow; the radiance is interpolated linearly between vertices).</summary>
        public static void ScaleIntensity(MeshBuilder mb, int first, int end, System.Func<Vector3, float> visibility)
        {
            if (mb == null || first < 0 || visibility == null) return;
            end = Mathf.Min(end, mb.VertexCount);
            for (int i = first; i < end; i++)
            {
                Vector2 uv1 = mb.UV1[i];
                uv1.y *= Mathf.Clamp01(visibility(mb.V[i]));
                mb.UV1[i] = uv1;
            }
        }

        /// <summary>Camera-facing Gaussian glow with a world-space core radius (m, the 1/e^2 radius of the profile).</summary>
        public static void Glow(MeshBuilder mb, Vector3 centre, float radius, Color tint, float intensity, float channel)
        {
            if (mb == null || !(radius > 0f)) return;
            Split(tint, intensity, out Color rgb, out float k);
            var col = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(channel));
            var uv1 = new Vector2(radius, k);
            Matrix4x4 m = mb.M;
            Vector3 c = m.MultiplyPoint3x4(centre);
            mb.M = Matrix4x4.identity;
            var n = new Vector3(0f, 0f, -1f);   // clockwise seen from -Z (MeshBuilder winding), matches the order below
            int i0 = mb.Vtx(c + new Vector3(-radius, radius, 0f), n, new Vector2(-1f, 1f), col);
            int i1 = mb.Vtx(c + new Vector3(radius, radius, 0f), n, new Vector2(1f, 1f), col);
            int i2 = mb.Vtx(c + new Vector3(radius, -radius, 0f), n, new Vector2(1f, -1f), col);
            int i3 = mb.Vtx(c + new Vector3(-radius, -radius, 0f), n, new Vector2(-1f, -1f), col);
            mb.UV1[i0] = uv1; mb.UV1[i1] = uv1; mb.UV1[i2] = uv1; mb.UV1[i3] = uv1;
            mb.Quad(i0, i1, i2, i3);
            mb.M = m;
        }

        /// <summary>
        /// Grows glow-mesh bounds on every axis by the largest drawn quad half-extent (LightPoolKit.ExpandForBillboards):
        /// the shader draws a camera-facing disc of radius QuadPad x max(core radius, minimum on-screen radius at
        /// <paramref name="maxDistance"/>), and the stored quad lies in the object XY plane.
        /// </summary>
        public static Bounds GrowForGlows(Bounds bounds, MeshBuilder mb, float maxDistance)
        {
            float maxCore = 0f;
            if (mb != null)
                for (int i = 0; i < mb.UV1.Count; i++) maxCore = Mathf.Max(maxCore, mb.UV1[i].x);
            float d = Mathf.Max(maxDistance, 0f);
            float rhoMin = 0.5f * DefaultMinPx * MaxPixelAngle * d;
            float e = QuadPad * Mathf.Max(rhoMin, maxCore);
            bounds.Expand(2f * e);
            return bounds;
        }

        /// <summary>
        /// Builds <paramref name="mb"/> (no tangents) and adds it through ctx.AddMesh with castShadows false and
        /// receiveShadows false (LightPoolKit.AddToWorld). RingMat.Glow meshes first get <see cref="GrowForGlows"/>, then
        /// AddMesh inflates the bounds for the bend. Returns null for an empty builder. Load time only.
        /// </summary>
        public static GameObject AddToWorld(BuildContext ctx, MeshBuilder mb, string name, string matId, RingSet set,
            float bendMaxDistance, Transform parent = null)
        {
            if (ctx == null || mb == null || mb.VertexCount == 0) return null;
            Mesh mesh = mb.Build(name, false);
            if (matId == RingMat.Glow) mesh.bounds = GrowForGlows(mesh.bounds, mb, bendMaxDistance);
            GameObject go = ctx.AddMesh(name, mesh, matId, set, false, bendMaxDistance, parent);
            var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
            if (mr != null) mr.receiveShadows = false;
            return go;
        }

        // Vertex colours stay in 0..1 (safe for any colour format); HDR goes into the uv1 intensity (as LightPoolKit).
        static void Split(Color tint, float intensity, out Color rgb, out float k)
        {
            float r = Mathf.Max(tint.r, 0f), g = Mathf.Max(tint.g, 0f), bl = Mathf.Max(tint.b, 0f);
            float mx = Mathf.Max(r, Mathf.Max(g, bl));
            k = Mathf.Max(intensity, 0f);
            if (mx > 1f) { r /= mx; g /= mx; bl /= mx; k *= mx; }
            rgb = new Color(r, g, bl, 1f);
        }
    }
}
