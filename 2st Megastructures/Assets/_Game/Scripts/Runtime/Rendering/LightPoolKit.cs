using System;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Which global level drives an additive light or a glow (decoded by RingAdditive.shader).</summary>
    public enum LampChannel
    {
        Constant = 0,   // level 1 (material / vertex intensity only)
        Lamp = 1,       // _Lamps.x / 3: bollard and lamp level (0..3 in RingSchedule, 3.0 = fully on)
        City = 2,       // _Lamps.y: city level N(u)
        Beacon = 3,     // _Lamps.z: beacon pulse 0..1
        Spill = 4,      // _Lamps.w: tunnel mouth spill 0..1
    }

    /// <summary>
    /// Mesh helpers for the additive light of The Ring (RingAdditive.shader): light pools on surfaces (RingMat.LightPool),
    /// glow billboards (RingMat.Glow) and dust motes (RingMat.Mote). Each helper appends to a MeshBuilder; the builder
    /// adds the mesh with <see cref="AddToWorld"/> (or ctx.AddMesh(name, mesh, RingMat.X, set, castShadows: false,
    /// bendMaxDistance) after growing billboard bounds with <see cref="ExpandForBillboards"/>). Keep one material per
    /// mesh: pools, glows and motes are separate meshes (the material selects the RingAdditive mode).
    /// The vertex layout is a contract with the shader (see the header of RingAdditive.shader). Billboards are stored
    /// as small object-space XY quads around their centre so meshes stay valid (no degenerate triangles, correct bounds);
    /// the vertex shader recovers the centre, applies RingBend and expands the quad toward the camera with a minimum
    /// on-screen size and energy-preserving intensity. Helpers bypass MeshBuilder.M for the quad offsets (the centre,
    /// foot point and directions are transformed by M; sizes stay in metres), so M must be rigid.
    /// Mesh bounds: the shader draws every billboard larger than its stored mesh quad (QuadPad core radii, at least the
    /// minimum on-screen size, angular glows at angle x distance) and moves motes by their drift. <see cref="AddToWorld"/>
    /// grows the bounds for all of this; a builder that adds a billboard mesh itself must call
    /// <see cref="ExpandForBillboards(Bounds, MeshBuilder, string, float)"/> on the mesh bounds before ctx.AddMesh.
    /// Light pools carry their lamp height per vertex (uv1.x = radius / lamp height), so pools under lamps of different
    /// heights share one material.
    /// Spec numbers below that are not yet in RingConstants (14.2.3, 14.2.6, 14.5.1) live here as the single C# source;
    /// beat builders and RenderingMaterialDefs reference these constants.
    /// </summary>
    public static class LightPoolKit
    {
        /// <summary>Pools float 0.02 m above their surface (14.5.4, 14.10 T4) plus polygon offset -1 -1 (material).</summary>
        public const float PoolLift = 0.02f;
        /// <summary>Light-pool radius (14.2.3: "additive light-pool quads (radius 3 m)").</summary>
        public const float PoolRadius = 3f;
        /// <summary>Lamp height above the pool surface used when a caller gives none (the former material _LampHeight).
        /// Callers should pass the real height of the lamp's emitting centre above the pool surface.</summary>
        public const float DefaultLampHeight = 1.1f;
        /// <summary>Smallest lamp height the pool profile accepts (m); keeps radius / height finite.</summary>
        public const float MinLampHeight = 0.05f;
        /// <summary>M13 motes: 4 mm quads (14.2.6).</summary>
        public const float MoteSize = 0.004f;
        /// <summary>Mesh half-extent of angular glows; twin of RING_ANGULAR_MESH_EXTENT in RingAdditive.shader.</summary>
        public const float AngularMeshExtent = 0.01f;
        /// <summary>Drawn quad half-extent in core radii; twin of RING_QUAD_PAD in RingAdditive.shader.</summary>
        public const float QuadPad = 1.5f;
        /// <summary>M13 "min 2 px" (14.2.6): default minimum core diameter of the Mote and Glow materials.</summary>
        public const float DefaultMinPx = 2f;
        /// <summary>Largest vertical pixel angle the game can render at: 720 px (Part 8.10) at the widest vFOV the general
        /// rules allow, 68 deg (Part 9; the 14.2.5 schedule itself stays at or below 66 deg). Bounds use it, so it is the
        /// conservative end.</summary>
        public const float MaxPixelAngle = 68f * Mathf.Deg2Rad / 720f;
        /// <summary>RingMat.Mote drift amplitude per axis (m), the material _Drift (14.5.1: drift about 0.02 m/s, at most
        /// 0.05 m/s; the shader's closed Lissajous runs at 0.113-0.152 rad/s, so 0.10 m gives at most 0.015 m/s).</summary>
        public const float MoteDriftX = 0.10f, MoteDriftY = 0.07f, MoteDriftZ = 0.10f;
        /// <summary>RingMat.Mote turbulence (14.5.1: "5 mm turbulence at 0.3 Hz"), the material _Turbulence (x m, y Hz).</summary>
        public const float MoteTurbulence = 0.005f, MoteTurbulenceHz = 0.3f;
        /// <summary>Largest per-axis displacement the mote shader applies (drift plus turbulence), the bounds margin.</summary>
        public static readonly float MoteDriftMargin = Mathf.Max(MoteDriftX, Mathf.Max(MoteDriftY, MoteDriftZ)) + MoteTurbulence;
        /// <summary>Suggested per-vertex pool intensity (added radiance gain at the pool centre before the lamp level).
        /// Art value, to be calibrated in captures: night path pixels under a pool should stay inside the 14.5.4 (3)
        /// lower-third range (luma 20-70 mean) at EV +1.6.</summary>
        public const float DefaultPoolIntensity = 0.1f;
        /// <summary>Suggested lamp-halo intensity for <see cref="AddGlow"/> (peak radiance gain before the lamp level).</summary>
        public const float DefaultGlowIntensity = 1.0f;

        /// <summary>WarmLamp (14.2.1), the default pool and lamp-glow tint.</summary>
        public static Color WarmLamp => RingPalette.Lin("WarmLamp");

        public static float ChannelCode(LampChannel channel) => Mathf.Clamp((int)channel, 0, 4) * 0.25f;

        // ---------------------------------------------------------------- world helper

        /// <summary>
        /// Builds <paramref name="mb"/> (no tangents) and adds it through ctx.AddMesh with castShadows false and
        /// receiveShadows false. For RingMat.Glow and RingMat.Mote the mesh bounds are first grown by
        /// <see cref="ExpandForBillboards(Bounds, MeshBuilder, string, float)"/> (drawn quad size, minimum on-screen size
        /// at <paramref name="bendMaxDistance"/>, mote drift), then AddMesh inflates them for the bend. Returns null for
        /// an empty builder. Load time only (inside IBeatBuilder.Build).
        /// </summary>
        public static GameObject AddToWorld(BuildContext ctx, MeshBuilder mb, string name, string matId, RingSet set,
            float bendMaxDistance, Transform parent = null)
        {
            if (ctx == null || mb == null || mb.VertexCount == 0) return null;
            Mesh mesh = mb.Build(name, false);
            mesh.bounds = ExpandForBillboards(mesh.bounds, mb, matId, bendMaxDistance);
            GameObject go = ctx.AddMesh(name, mesh, matId, set, false, bendMaxDistance, parent);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------- light pools

        /// <summary>WarmLamp pool (14.2.3: radius 3 m) under a lamp, driven by the lamp level _Lamps.x / 3 by default
        /// (the beat-4 bollards ramp on over u 0.30-0.60, 14.5.4). Pass LampChannel.Constant for a pool whose level is
        /// only the vertex intensity times the material _Intensity. <paramref name="lampHeight"/> = height of the lamp's
        /// emitting centre above the pool surface (m; e.g. RingConstants.LampH for the 3.2 m lamp posts).</summary>
        public static void AddLampPool(MeshBuilder mb, Vector3 foot, float intensity = DefaultPoolIntensity,
            LampChannel channel = LampChannel.Lamp, float radius = PoolRadius, int subdivisions = 1,
            float lampHeight = DefaultLampHeight)
        {
            AddLightPool(mb, foot, WarmLamp, intensity, channel, radius, subdivisions, lampHeight);
        }

        /// <summary>Square pool (side 2 x radius) on a horizontal surface whose top passes through <paramref name="foot"/>
        /// (the point under the lamp). The quad is lifted by <see cref="PoolLift"/>. <paramref name="lampHeight"/> =
        /// height of the lamp's emitting centre above the pool surface (m).</summary>
        public static void AddLightPool(MeshBuilder mb, Vector3 foot, Color tint, float intensity,
            LampChannel channel = LampChannel.Constant, float radius = PoolRadius, int subdivisions = 1,
            float lampHeight = DefaultLampHeight)
        {
            AddLightPoolRect(mb, foot, Vector3.up, Vector3.right, -radius, radius, -radius, radius, tint, intensity, channel,
                radius, subdivisions, null, lampHeight);
        }

        /// <summary>
        /// Pool clipped to a rectangle of its tangent frame (u along <paramref name="tangent"/>, v across), so a pool can
        /// cover just the path strip between the kerbs instead of cutting through them. The analytic falloff stays
        /// centred on <paramref name="foot"/> (the lamp may stand beside the covered area). The rectangle is intersected
        /// with [-radius, radius]^2. <paramref name="surfaceY"/> (optional, flat x, z -> surface top y in the builder's
        /// output space) makes a subdivided pool follow an uneven up-facing surface; it is evaluated at load only.
        /// <paramref name="lampHeight"/> = height of the lamp's emitting centre above this pool's surface (m, clamped to
        /// at least <see cref="MinLampHeight"/>): it sets the falloff shape (half maximum at r = 0.766 x height) and is
        /// stored per vertex as uv1.x = radius / lampHeight, so pools under lamps of different heights share one material.
        /// </summary>
        public static void AddLightPoolRect(MeshBuilder mb, Vector3 foot, Vector3 normal, Vector3 tangent,
            float uMin, float uMax, float vMin, float vMax, Color tint, float intensity,
            LampChannel channel = LampChannel.Constant, float radius = PoolRadius, int subdivisions = 1,
            Func<float, float, float> surfaceY = null, float lampHeight = DefaultLampHeight)
        {
            if (mb == null || !(radius > 0f)) return;
            uMin = Mathf.Max(uMin, -radius); uMax = Mathf.Min(uMax, radius);
            vMin = Mathf.Max(vMin, -radius); vMax = Mathf.Min(vMax, radius);
            if (uMax - uMin < 1e-3f || vMax - vMin < 1e-3f) return;
            int n = Mathf.Clamp(subdivisions, 1, 64);

            Matrix4x4 m = mb.M;
            Vector3 f = m.MultiplyPoint3x4(foot);
            Vector3 nn = m.MultiplyVector(normal);
            if (nn.sqrMagnitude < 1e-12f) nn = Vector3.up;
            nn.Normalize();
            Vector3 t = m.MultiplyVector(tangent);
            t -= Vector3.Dot(t, nn) * nn;
            if (t.sqrMagnitude < 1e-12f) t = Vector3.Cross(nn, Mathf.Abs(nn.x) < 0.9f ? Vector3.right : Vector3.forward);
            t.Normalize();
            Vector3 b = Vector3.Cross(nn, t).normalized;

            SplitTint(tint, intensity, out Color rgb, out float k);
            var col = new Color(rgb.r, rgb.g, rgb.b, ChannelCode(channel));
            // the profile depends only on r / h and r / R: the shader needs R / h (uv0 = offset / R gives r / R)
            float h = float.IsNaN(lampHeight) ? DefaultLampHeight : Mathf.Max(lampHeight, MinLampHeight);
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
                    if (surfaceY != null) p.y = surfaceY(p.x, p.z) + PoolLift;
                    int idx = mb.Vtx(p, nn, new Vector2(u / radius, v / radius), col);
                    mb.UV1[idx] = uv1;
                }
            }
            // MeshBuilder front face = Cross(b - a, c - a) (clockwise seen from the front): face the surface normal
            bool flip = Vector3.Dot(Vector3.Cross(t, b), nn) < 0f;
            int row = n + 1;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = baseIdx + j * row + i, a1 = a0 + 1, b1 = a0 + row + 1, b0 = a0 + row;
                    if (!flip) mb.Quad(a0, a1, b1, b0); else mb.Quad(a0, b0, b1, a1);
                }
            mb.M = m;
        }

        // ---------------------------------------------------------------- glows

        /// <summary>Camera-facing Gaussian glow with a world-space core radius (m): the 1/e^2 radius of the profile.
        /// tint is linear (HDR allowed: the excess over 1 is folded into the intensity).</summary>
        public static void AddGlow(MeshBuilder mb, Vector3 centre, float radius, Color tint, float intensity,
            LampChannel channel = LampChannel.Constant)
        {
            if (mb == null || !(radius > 0f)) return;
            SplitTint(tint, intensity, out Color rgb, out float k);
            AddBillboard(mb, centre, radius, new Vector2(radius, k), new Color(rgb.r, rgb.g, rgb.b, ChannelCode(channel)));
        }

        /// <summary>WarmLamp halo around a lamp lens (world core radius in m), on with the lamp level _Lamps.x / 3.</summary>
        public static void AddLampGlow(MeshBuilder mb, Vector3 centre, float radius, float intensity = DefaultGlowIntensity,
            LampChannel channel = LampChannel.Lamp)
        {
            AddGlow(mb, centre, radius, WarmLamp, intensity, channel);
        }

        /// <summary>Glow with a constant angular core radius (rad) at every distance, e.g. the beat-5 climber
        /// (14.5.5: constant footprint 1.4 mrad = 2.3 px at 4K, so angularRadius = 0.7e-3).</summary>
        public static void AddGlowAngular(MeshBuilder mb, Vector3 centre, float angularRadius, Color tint, float intensity,
            LampChannel channel = LampChannel.Constant)
        {
            if (mb == null || !(angularRadius > 0f)) return;
            SplitTint(tint, intensity, out Color rgb, out float k);
            AddBillboard(mb, centre, AngularMeshExtent, new Vector2(-angularRadius, k), new Color(rgb.r, rgb.g, rgb.b, ChannelCode(channel)));
        }

        // ---------------------------------------------------------------- motes

        /// <summary>One dust mote (M13). seed01 drives its private drift phase; beamWeight 1 = only bright inside the
        /// material's sunbeam (RingMat.Mote: the capsule porthole beam), 0 = ignore the beam. tint is clamped to 0..1.</summary>
        public static void AddMote(MeshBuilder mb, Vector3 centre, float seed01, Color tint, float beamWeight = 1f, float size = MoteSize)
        {
            if (mb == null) return;
            float half = Mathf.Max(size, 1e-4f) * 0.5f;
            var col = new Color(Mathf.Clamp01(tint.r), Mathf.Clamp01(tint.g), Mathf.Clamp01(tint.b), Mathf.Clamp01(beamWeight));
            AddBillboard(mb, centre, half, new Vector2(half, seed01 - Mathf.Floor(seed01)), col);
        }

        /// <summary>count motes uniformly in a box (deterministic from seed, RingMath PCG).</summary>
        public static void AddMoteCloud(MeshBuilder mb, Vector3 boxCentre, Vector3 boxSize, int count, uint seed, Color tint,
            float beamWeight = 1f, float brightnessJitter = 0.4f, float size = MoteSize)
        {
            if (mb == null) return;
            for (int i = 0; i < count; i++)
            {
                uint h = RingMath.Pcg(unchecked(seed * 2654435761u + (uint)i * 40503u + 17u));
                float rx = RingMath.U01(h); h = RingMath.Pcg(h);
                float ry = RingMath.U01(h); h = RingMath.Pcg(h);
                float rz = RingMath.U01(h); h = RingMath.Pcg(h);
                float rs = RingMath.U01(h); h = RingMath.Pcg(h);
                float rb = RingMath.U01(h);
                Vector3 p = boxCentre + Vector3.Scale(boxSize, new Vector3(rx - 0.5f, ry - 0.5f, rz - 0.5f));
                AddMote(mb, p, rs, Scale(tint, 1f - Mathf.Clamp01(brightnessJitter) * rb), beamWeight, size);
            }
        }

        /// <summary>count motes in a cylinder around a light beam (origin at the opening, dirIntoRoom along the light,
        /// i.e. minus the key vector), spread x beamRadius wide so the material's soft beam edge shows.</summary>
        public static void AddMotesInBeam(MeshBuilder mb, Vector3 origin, Vector3 dirIntoRoom, float beamRadius, float length,
            int count, uint seed, Color tint, float spread = 1.4f, float brightnessJitter = 0.4f, float size = MoteSize)
        {
            if (mb == null || dirIntoRoom.sqrMagnitude < 1e-12f) return;
            Vector3 w = dirIntoRoom.normalized;
            Vector3 a = Vector3.Cross(w, Mathf.Abs(w.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 c = Vector3.Cross(w, a);
            for (int i = 0; i < count; i++)
            {
                uint h = RingMath.Pcg(unchecked(seed * 2654435761u + (uint)i * 40503u + 91u));
                float r1 = RingMath.U01(h); h = RingMath.Pcg(h);
                float r2 = RingMath.U01(h); h = RingMath.Pcg(h);
                float r3 = RingMath.U01(h); h = RingMath.Pcg(h);
                float rs = RingMath.U01(h); h = RingMath.Pcg(h);
                float rb = RingMath.U01(h);
                float along = Mathf.Max(length, 0f) * r1;
                float rad = Mathf.Max(beamRadius, 0f) * spread * Mathf.Sqrt(r2);
                float ang = 2f * Mathf.PI * r3;
                Vector3 p = origin + w * along + (a * Mathf.Cos(ang) + c * Mathf.Sin(ang)) * rad;
                AddMote(mb, p, rs, Scale(tint, 1f - Mathf.Clamp01(brightnessJitter) * rb), 1f, size);
            }
        }

        // ---------------------------------------------------------------- bounds

        /// <summary>
        /// Expands billboard mesh bounds on every axis by the largest drawn quad half-extent around a billboard centre,
        /// plus <paramref name="extra"/> (the mote drift). The shader draws a camera-facing disc of radius
        /// QuadPad x rho with rho = max(true core radius, minimum on-screen radius); a disc of radius e in any plane stays
        /// within +-e of its centre on each axis, and the stored mesh quad lies in the object XY plane (zero extent in z),
        /// so the margin is the full drawn half-extent: QuadPad x max(0.5 minPx MaxPixelAngle d, maxCoreRadius,
        /// maxAngularRadius d) at d = <paramref name="maxDistance"/>. The glow pull toward the camera keeps the angular
        /// footprint, so it needs no margin.
        /// </summary>
        public static Bounds ExpandForBillboards(Bounds b, float maxDistance, float minPx = DefaultMinPx,
            float maxCoreRadius = 0f, float maxAngularRadius = 0f, float extra = 0f)
        {
            float d = Mathf.Max(maxDistance, 0f);
            float rhoMin = 0.5f * Mathf.Max(minPx, 0f) * MaxPixelAngle * d;
            float rhoTrue = Mathf.Max(Mathf.Max(maxCoreRadius, 0f), Mathf.Max(maxAngularRadius, 0f) * d);
            float e = QuadPad * Mathf.Max(rhoMin, rhoTrue) + Mathf.Max(extra, 0f);
            b.Expand(2f * e);
            return b;
        }

        /// <summary>
        /// Bounds of a billboard mesh built from <paramref name="mb"/> with material <paramref name="matId"/>: for
        /// RingMat.Glow and RingMat.Mote, <see cref="ExpandForBillboards(Bounds, float, float, float, float, float)"/>
        /// with the largest core and angular radii found in mb.UV1 and, for motes, <see cref="MoteDriftMargin"/>; any
        /// other material returns <paramref name="b"/> unchanged. Use it before ctx.AddMesh when not using
        /// <see cref="AddToWorld"/>. Load time only.
        /// </summary>
        public static Bounds ExpandForBillboards(Bounds b, MeshBuilder mb, string matId, float maxDistance)
        {
            bool mote = matId == RingMat.Mote;
            if (mb == null || !(mote || matId == RingMat.Glow)) return b;
            BillboardRadii(mb, out float maxCore, out float maxAngular);
            return ExpandForBillboards(b, maxDistance, DefaultMinPx, maxCore, maxAngular, mote ? MoteDriftMargin : 0f);
        }

        /// <summary>Largest world core radius (m, uv1.x > 0) and angular core radius (rad, uv1.x &lt; 0) of the billboards
        /// in <paramref name="mb"/> (layout of <see cref="AddGlow"/>, <see cref="AddGlowAngular"/>, <see cref="AddMote"/>).</summary>
        public static void BillboardRadii(MeshBuilder mb, out float maxCore, out float maxAngular)
        {
            maxCore = 0f; maxAngular = 0f;
            if (mb == null) return;
            var uv1 = mb.UV1;
            for (int i = 0; i < uv1.Count; i++)
            {
                float x = uv1[i].x;
                if (x > 0f) { if (x > maxCore) maxCore = x; }
                else if (-x > maxAngular) maxAngular = -x;
            }
        }

        // ---------------------------------------------------------------- internals

        static void AddBillboard(MeshBuilder mb, Vector3 centre, float meshHalf, Vector2 uv1, Color col)
        {
            Matrix4x4 m = mb.M;
            Vector3 c = m.MultiplyPoint3x4(centre);
            mb.M = Matrix4x4.identity;
            var n = new Vector3(0f, 0f, -1f);   // clockwise seen from -Z (MeshBuilder winding), matches the order below
            int i0 = mb.Vtx(c + new Vector3(-meshHalf, meshHalf, 0f), n, new Vector2(-1f, 1f), col);
            int i1 = mb.Vtx(c + new Vector3(meshHalf, meshHalf, 0f), n, new Vector2(1f, 1f), col);
            int i2 = mb.Vtx(c + new Vector3(meshHalf, -meshHalf, 0f), n, new Vector2(1f, -1f), col);
            int i3 = mb.Vtx(c + new Vector3(-meshHalf, -meshHalf, 0f), n, new Vector2(-1f, -1f), col);
            mb.UV1[i0] = uv1; mb.UV1[i1] = uv1; mb.UV1[i2] = uv1; mb.UV1[i3] = uv1;
            mb.Quad(i0, i1, i2, i3);
            mb.M = m;
        }

        // Vertex colours stay in 0..1 (safe for any colour format); HDR goes into the uv1 intensity.
        static void SplitTint(Color tint, float intensity, out Color rgb, out float k)
        {
            float r = Mathf.Max(tint.r, 0f), g = Mathf.Max(tint.g, 0f), bl = Mathf.Max(tint.b, 0f);
            float mx = Mathf.Max(r, Mathf.Max(g, bl));
            k = Mathf.Max(intensity, 0f);
            if (mx > 1f) { r /= mx; g /= mx; bl /= mx; k *= mx; }
            rgb = new Color(r, g, bl, 1f);
        }

        static Color Scale(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a);
    }
}
