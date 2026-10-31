using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Shared build-time helpers of the seven beat generators (same assembly, so every beat folder uses this file; it lives in
    /// Beat01_Gallery only because a file needs a folder). Nothing here runs per frame.
    ///
    /// Conventions used by every beat:
    /// - Beat roots sit at the world origin with identity rotation and unit scale (SceneBuilder), so root-local coordinates
    ///   are world coordinates. CorridorKit places colliders in world space, which only matches the visible geometry under
    ///   that assumption; <see cref="CheckRoot"/> warns otherwise.
    /// - Every mesh a beat creates goes through <see cref="Bake"/> into the beat's mesh list (CollectMeshes, cleanup).
    /// - Renderers never use light or reflection probes (CONTRACTS 5); huge meshes disable dynamic occlusion.
    /// - Walls with holes are decomposed into GridQuads (axis-aligned rectangles) on z-planes; a circular hole is a separate
    ///   square-minus-circle piece whose circle vertices are those of a 128-gon from +X toward +Y (the bore lining, the bore
    ///   card and every bore hole use the same polygon, so their edges meet without cracks).
    /// </summary>
    internal static class BeatKit
    {
        public static readonly int DistFadeId = Shader.PropertyToID("_DistFade");
        public static readonly int CardModeId = Shader.PropertyToID("_CardMode");
        public static readonly int WearWidthId = Shader.PropertyToID("_WearWidth");

        /// <summary>Polygon resolution of every circular bore (fan bore lining, holes, bore card).</summary>
        public const int BORE_SEGMENTS = 128;

        /// <summary>Half size of the square piece around a bore hole (bore radius 150 + 5 m margin, inside the wall faces).</summary>
        public const float BORE_SQUARE_HALF = 155f;

        /// <summary>
        /// The gallery starts at W0 on the Face plane z = 0, so its back end (the start wall stands at z = -0.6 to fit the
        /// capsule) lies just outside the Face. The Face (Beat07) leaves this rectangle open and the gallery (Beat01) closes it
        /// with an outward-facing boss (invisible from inside the gallery, a 0.65 m concrete block on the Face from outside).
        /// x and y are world coordinates on z = 0.
        /// </summary>
        public static readonly Rect2 GALLERY_FACE_HOLE = new Rect2(-1.3f, -0.35f, 1.3f, 2.6f);
        public const float GALLERY_BOSS_Z = -0.65f;

        // ------------------------------------------------------------------------------------------ hierarchy

        public static void CheckRoot(BeatContext ctx, string beat)
        {
            if (ctx.Root == null) return;
            Transform r = ctx.Root;
            if (r.position.sqrMagnitude > 1e-6f || Quaternion.Angle(r.rotation, Quaternion.identity) > 1e-3f ||
                (r.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
                Debug.LogWarning("[ABYSS] " + beat + ": beat root is not at the origin with identity transform; colliders are world-space");
        }

        public static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        public static Transform Group(Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go.transform;
        }

        // ------------------------------------------------------------------------------------------ meshes and renderers

        /// <summary>Turn the builder into a Mesh (registered in <paramref name="meshes"/>) and clear the builder.</summary>
        public static Mesh Bake(MeshBuilder mb, string name, List<Mesh> meshes)
        {
            Mesh m = mb.ToMesh(name);
            meshes.Add(m);
            mb.Clear();
            return m;
        }

        public static MeshRenderer Renderer(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos,
            Quaternion localRot, ShadowCastingMode shadows, bool huge = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = !huge;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return mr;
        }

        /// <summary>Bake the builder and add a renderer for it at localPos (returns null for an empty builder).</summary>
        public static MeshRenderer Emit(MeshBuilder mb, Transform parent, string name, Material mat, List<Mesh> meshes,
            ShadowCastingMode shadows, Vector3 localPos, bool huge = false)
        {
            if (mb.VertexCount == 0) { mb.Clear(); return null; }
            Mesh m = Bake(mb, name, meshes);
            return Renderer(parent, name, m, mat, localPos, Quaternion.identity, shadows, huge);
        }

        public static MeshRenderer Emit(MeshBuilder mb, Transform parent, string name, Material mat, List<Mesh> meshes,
            ShadowCastingMode shadows) => Emit(mb, parent, name, mat, meshes, shadows, Vector3.zero);

        /// <summary>World-space bounds of a renderer, or an empty bounds at the origin for null.</summary>
        public static Bounds BoundsOf(Renderer r) => r != null ? r.bounds : new Bounds(Vector3.zero, Vector3.zero);

        /// <summary>Union of renderer bounds (null entries skipped).</summary>
        public static Bounds Union(Renderer a, Renderer b)
        {
            if (a == null) return BoundsOf(b);
            Bounds u = a.bounds;
            if (b != null) u.Encapsulate(b.bounds);
            return u;
        }

        /// <summary>Bounds of the vertices first..count-1 currently in the builder (positions in the builder's space).</summary>
        public static Bounds VertexBounds(MeshBuilder mb, int first)
        {
            var p = mb.Positions;
            if (first >= p.Count) return new Bounds(Vector3.zero, Vector3.zero);
            var b = new Bounds(p[first], Vector3.zero);
            for (int i = first + 1; i < p.Count; i++) b.Encapsulate(p[i]);
            return b;
        }

        /// <summary>Min and max horizontal distance of mesh vertices from an axis through centreXZ (vertices closer than minR ignored).</summary>
        public static void RadialRange(Mesh mesh, Vector3 meshOriginWorld, Vector2 centreXZ, float minR, out float rMin, out float rMax)
        {
            rMin = float.MaxValue; rMax = 0f;
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                float dx = v[i].x + meshOriginWorld.x - centreXZ.x, dz = v[i].z + meshOriginWorld.z - centreXZ.y;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                if (r < minR) continue;
                if (r < rMin) rMin = r;
                if (r > rMax) rMax = r;
            }
            if (rMin == float.MaxValue) rMin = 0f;
        }

        // ------------------------------------------------------------------------------------------ oriented primitives

        /// <summary>Triangle wound to agree with its vertex normals; degenerate triangles are skipped.</summary>
        public static void Tri(MeshBuilder mb, int a, int b, int c)
        {
            var p = mb.Positions;
            Vector3 pa = p[a];
            Vector3 cr = Vector3.Cross(p[b] - pa, p[c] - pa);
            if (cr.sqrMagnitude < 1e-18f) return;
            var n = mb.Normals;
            Vector3 ns = n[a] + n[b] + n[c];
            if (Vector3.Dot(cr, ns) >= 0f) mb.Tri(a, b, c);
            else mb.Tri(a, c, b);
        }

        /// <summary>Flat quad with its own vertices and normal n (corners in cyclic order, either orientation).</summary>
        public static void Quad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i0 = mb.Vertex(a, n, new Vector2(a.x, a.y));
            int i1 = mb.Vertex(b, n, new Vector2(b.x, b.y));
            int i2 = mb.Vertex(c, n, new Vector2(c.x, c.y));
            int i3 = mb.Vertex(d, n, new Vector2(d.x, d.y));
            Tri(mb, i0, i1, i2);
            Tri(mb, i0, i2, i3);
        }

        /// <summary>Card quad on a z-plane with UV0 = (0..1, 0..1) (GradientCard: UV0.y 0 at the bottom, 1 at the top).</summary>
        public static void ZCard(MeshBuilder mb, float x0, float x1, float y0, float y1, float z, bool facePlusZ)
        {
            Vector3 n = facePlusZ ? Vector3.forward : Vector3.back;
            int i0 = mb.Vertex(new Vector3(x0, y0, z), n, new Vector2(0f, 0f));
            int i1 = mb.Vertex(new Vector3(x0, y1, z), n, new Vector2(0f, 1f));
            int i2 = mb.Vertex(new Vector3(x1, y1, z), n, new Vector2(1f, 1f));
            int i3 = mb.Vertex(new Vector3(x1, y0, z), n, new Vector2(1f, 0f));
            Tri(mb, i0, i1, i2);
            Tri(mb, i0, i2, i3);
        }

        /// <summary>Set UV0 of upward-facing vertices from first on to (lateral offset from the centreline x = centreX, z): ConcreteFloor worn path.</summary>
        public static void FloorUVAlongZ(MeshBuilder mb, int first, float centreX)
        {
            for (int i = first; i < mb.Positions.Count; i++)
                if (mb.Normals[i].y > 0.7f) mb.UV0[i] = new Vector2(mb.Positions[i].x - centreX, mb.Positions[i].z);
        }

        /// <summary>As <see cref="FloorUVAlongZ"/> for a path along X (lateral offset from z = centreZ).</summary>
        public static void FloorUVAlongX(MeshBuilder mb, int first, float centreZ)
        {
            for (int i = first; i < mb.Positions.Count; i++)
                if (mb.Normals[i].y > 0.7f) mb.UV0[i] = new Vector2(mb.Positions[i].z - centreZ, mb.Positions[i].x);
        }

        /// <summary>A straight hand rail between two floor points: top and mid tubes (square section) and posts.</summary>
        public static void Rail(MeshBuilder mb, Vector3 a, Vector3 b, float height, float tube, float postSpacing)
        {
            Vector3 up = Vector3.up;
            MeshKit.ChamferMember(mb, a + up * (height - tube * 0.5f), b + up * (height - tube * 0.5f), tube, tube, 0.008f, up);
            MeshKit.ChamferMember(mb, a + up * (height * 0.5f), b + up * (height * 0.5f), tube, tube, 0.008f, up);
            float len = (b - a).magnitude;
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / postSpacing));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / posts);
                MeshKit.ChamferBox(mb, p + up * (height * 0.5f), new Vector3(tube, height, tube), 0.008f);
            }
        }

        // ------------------------------------------------------------------------------------------ walls with holes (z-planes)

        public struct Rect2
        {
            public float X0, Y0, X1, Y1;
            public Rect2(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public float W => X1 - X0;
            public float H => Y1 - Y0;
        }

        static readonly List<float> s_breaks = new List<float>(16);
        static readonly List<Rect2> s_band = new List<Rect2>(8);

        /// <summary>
        /// Planar wall on z = planeZ facing +Z (facePlusZ) or -Z: the outer rectangle minus axis-aligned holes (world x, y;
        /// holes must not overlap each other). Decomposed into horizontal bands and emitted as GridQuads with cells of at most
        /// maxCell metres, in the builder's current frame (push a translation of -chunkOrigin for chunk-local vertices).
        /// </summary>
        public static void ZPlaneWithHoles(MeshBuilder mb, float planeZ, bool facePlusZ, Rect2 outer, IList<Rect2> holes, float maxCell)
        {
            const float eps = 1e-4f;
            s_breaks.Clear();
            s_breaks.Add(outer.Y0);
            s_breaks.Add(outer.Y1);
            int hc = holes != null ? holes.Count : 0;
            for (int i = 0; i < hc; i++)
            {
                Rect2 h = holes[i];
                if (h.X1 <= outer.X0 + eps || h.X0 >= outer.X1 - eps) continue;
                if (h.Y0 > outer.Y0 + eps && h.Y0 < outer.Y1 - eps) s_breaks.Add(h.Y0);
                if (h.Y1 > outer.Y0 + eps && h.Y1 < outer.Y1 - eps) s_breaks.Add(h.Y1);
            }
            SortFloats(s_breaks);

            for (int b = 0; b + 1 < s_breaks.Count; b++)
            {
                float ya = s_breaks[b], yb = s_breaks[b + 1];
                if (yb - ya < eps) continue;
                s_band.Clear();
                for (int i = 0; i < hc; i++)
                {
                    Rect2 h = holes[i];
                    if (h.Y0 >= yb - eps || h.Y1 <= ya + eps) continue;
                    float x0 = Mathf.Max(h.X0, outer.X0), x1 = Mathf.Min(h.X1, outer.X1);
                    if (x1 - x0 < eps) continue;
                    s_band.Add(new Rect2(x0, ya, x1, yb));
                }
                SortByX(s_band);
                float x = outer.X0;
                for (int i = 0; i < s_band.Count; i++)
                {
                    if (s_band[i].X0 > x + eps) ZRect(mb, planeZ, facePlusZ, x, s_band[i].X0, ya, yb, maxCell);
                    if (s_band[i].X1 > x) x = s_band[i].X1;
                }
                if (outer.X1 > x + eps) ZRect(mb, planeZ, facePlusZ, x, outer.X1, ya, yb, maxCell);
            }
        }

        /// <summary>One subdivided rectangle on a z-plane (GridQuad: front normal = cross(axisV, axisU)).</summary>
        public static void ZRect(MeshBuilder mb, float z, bool facePlusZ, float x0, float x1, float y0, float y1, float maxCell)
        {
            float w = x1 - x0, h = y1 - y0;
            if (w <= 1e-4f || h <= 1e-4f) return;
            int du = Mathf.Max(1, Mathf.CeilToInt(w / maxCell)), dv = Mathf.Max(1, Mathf.CeilToInt(h / maxCell));
            if (facePlusZ) MeshKit.GridQuad(mb, new Vector3(x1, y0, z), Vector3.left, Vector3.up, w, h, du, dv);
            else MeshKit.GridQuad(mb, new Vector3(x0, y0, z), Vector3.right, Vector3.up, w, h, du, dv);
        }

        /// <summary>
        /// The square [c - half, c + half]^2 on a z-plane minus a circle of the given radius (a bore hole): quads between the
        /// circle polygon (segments a multiple of 8, vertices from +X toward +Y) and its radial projection onto the square.
        /// </summary>
        public static void ZSquareMinusCircle(MeshBuilder mb, float z, bool facePlusZ, Vector2 c, float half, float radius, int segments)
        {
            int n = Mathf.Max(8, (segments / 8) * 8);
            Vector3 nrm = facePlusZ ? Vector3.forward : Vector3.back;
            for (int i = 0; i < n; i++)
            {
                Vector2 d0 = Dir(i, n), d1 = Dir(i + 1, n);
                Vector2 c0 = c + d0 * radius, c1 = c + d1 * radius;
                Vector2 s0 = c + d0 * (half / Mathf.Max(Mathf.Abs(d0.x), Mathf.Abs(d0.y)));
                Vector2 s1 = c + d1 * (half / Mathf.Max(Mathf.Abs(d1.x), Mathf.Abs(d1.y)));
                Quad(mb, new Vector3(c0.x, c0.y, z), new Vector3(c1.x, c1.y, z), new Vector3(s1.x, s1.y, z), new Vector3(s0.x, s0.y, z), nrm);
            }
        }

        /// <summary>Unit direction of polygon vertex i of n (from +X toward +Y), computed in double.</summary>
        public static Vector2 Dir(int i, int n)
        {
            double a = 2.0 * System.Math.PI * (i % n) / n;
            return new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a));
        }

        static void SortFloats(List<float> l)
        {
            for (int i = 1; i < l.Count; i++)
            {
                float v = l[i];
                int j = i - 1;
                while (j >= 0 && l[j] > v) { l[j + 1] = l[j]; j--; }
                l[j + 1] = v;
            }
        }

        static void SortByX(List<Rect2> l)
        {
            for (int i = 1; i < l.Count; i++)
            {
                Rect2 v = l[i];
                int j = i - 1;
                while (j >= 0 && l[j].X0 > v.X0) { l[j + 1] = l[j]; j--; }
                l[j + 1] = v;
            }
        }

        // ------------------------------------------------------------------------------------------ colliders

        /// <summary>
        /// Corridor wall whose inner face runs along a -> b (world x, z), from baseY up height, its 2 m body on the side of
        /// <paramref name="outward"/> (x, z), the direction away from the walkable area.
        /// </summary>
        public static BoxCollider Wall(Transform parent, string name, Vector2 a, Vector2 b, float baseY, float height, Vector2 outward,
            float extendEnds = 0f, float thickness = CorridorKit.WallThickness)
        {
            Vector2 d = b - a;
            Vector2 right = new Vector2(d.y, -d.x);   // cross(up, dir) in (x, z)
            bool outwardIsRight = Vector2.Dot(right, outward) > 0f;
            return CorridorKit.Wall(parent, name, new Vector3(a.x, 0f, a.y), new Vector3(b.x, 0f, b.y), baseY, outwardIsRight, height,
                thickness, extendEnds);
        }

        /// <summary>Ceiling clamp: a box whose BOTTOM face runs along the centreline a -> b (ceiling height), width across.</summary>
        public static BoxCollider CeilingClamp(Transform parent, string name, Vector3 a, Vector3 b, float width, float thickness = 1f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion r = Quaternion.LookRotation(d / Mathf.Max(len, 1e-6f), Vector3.up);
            Vector3 up = r * Vector3.up;
            Vector3 c = (a + b) * 0.5f + up * (thickness * 0.5f);
            return CorridorKit.Box(parent, name, c, new Vector3(width, thickness, len), r);
        }

        // ------------------------------------------------------------------------------------------ lights

        public static Light Point(Transform parent, string name, Vector3 pos, Color srgb, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = srgb;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        public static Light Spot(Transform parent, string name, Vector3 pos, Vector3 direction, Color srgb, float intensity, float range,
            float angleDeg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(direction.normalized, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = srgb;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angleDeg;
            l.innerSpotAngle = angleDeg * 0.7f;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------------------------------ materials

        /// <summary>ConcreteFloor without the worn centre path (slabs whose UV0.x is not a lateral offset), optionally in a fog domain.</summary>
        public static Material FloorNoWear(MaterialLibrary mats, FogDomain domain)
        {
            return mats.Variant(MatId.ConcreteFloor, domain, "nowear", m => m.SetFloat(WearWidthId, 0f));
        }

        // ------------------------------------------------------------------------------------------ maths

        public static float Smoothstep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float Smootherstep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        /// <summary>Point on the C1 ledge circle (14.4 alpha convention: alpha 0 at -Z from C1, pi/2 at +X) at radius r.</summary>
        public static Vector3 LedgePoint(float alpha, float r, float y)
        {
            Vector2 c = AbyssConst.ARC_CENTRE;
            return new Vector3(c.x + r * Mathf.Sin(alpha), y, c.y - r * Mathf.Cos(alpha));
        }

        /// <summary>Ledge top height at alpha (y falls 4 m linearly from W6 at alpha 0 to W8 at alpha pi; flat before alpha 0).</summary>
        public static float LedgeY(float alpha)
        {
            float y0 = AbyssConst.W[6].y, y1 = AbyssConst.W[8].y;
            if (alpha <= 0f) return y0;
            if (alpha >= Mathf.PI) return y1;
            return y0 + (y1 - y0) * alpha / Mathf.PI;
        }
    }
}
