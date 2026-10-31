using System.Collections.Generic;
using UnityEngine;


namespace Megastructure.Beats
{
    /// <summary>
    /// Geometry helpers shared by the beat-3 and beat-4 builders. All helpers that orient faces compare the winding
    /// normal with a desired outward normal and flip the index order when needed, so every face is front-facing from
    /// the side its normal points to (MeshBuilder convention: clockwise seen from the front). They assume
    /// <see cref="MeshBuilder.M"/> is the identity.
    /// </summary>
    public static class Beat34Geo
    {
        /// <summary>Vertex colour convention: R = AO, G = edge wear, B = baked strip light, A = exterior exposure.</summary>
        public static Color Col(float ao, float wear = 0f, float strip = 0f, float expo = 1f) => new Color(ao, wear, strip, expo);

        public static void QuadO(MeshBuilder b, int i0, int i1, int i2, int i3, Vector3 desired)
        {
            Vector3 p0 = b.V[i0], p1 = b.V[i1], p2 = b.V[i2], p3 = b.V[i3];
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0) + Vector3.Cross(p2 - p0, p3 - p0);
            if (Vector3.Dot(n, desired) >= 0f) b.Quad(i0, i1, i2, i3);
            else b.Quad(i0, i3, i2, i1);
        }

        public static void TriO(MeshBuilder b, int i0, int i1, int i2, Vector3 desired)
        {
            Vector3 n = Vector3.Cross(b.V[i1] - b.V[i0], b.V[i2] - b.V[i0]);
            if (Vector3.Dot(n, desired) >= 0f) b.Tri(i0, i1, i2);
            else b.Tri(i0, i2, i1);
        }

        /// <summary>Flat quad with its own four vertices; the normal comes from the geometry, oriented toward side.</summary>
        public static void FlatQuad(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Vector3 side, Color col,
                                    Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            Vector3 n = Vector3.Cross(c1 - a, c2 - a) + Vector3.Cross(c2 - a, d - a);
            if (Vector3.Dot(n, side) < 0f) n = -n;
            n = n.sqrMagnitude > 1e-20f ? n.normalized : side.normalized;
            int i0 = b.Vtx(a, n, uvA, col), i1 = b.Vtx(c1, n, uvB, col), i2 = b.Vtx(c2, n, uvC, col), i3 = b.Vtx(d, n, uvD, col);
            QuadO(b, i0, i1, i2, i3, n);
        }

        /// <summary>Flat quad with planar uvs taken from two world axes (metres).</summary>
        public static void FlatQuadP(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Vector3 side, Color col, int uAxis, int vAxis)
        {
            FlatQuad(b, a, c1, c2, d, side, col, new Vector2(a[uAxis], a[vAxis]), new Vector2(c1[uAxis], c1[vAxis]),
                     new Vector2(c2[uAxis], c2[vAxis]), new Vector2(d[uAxis], d[vAxis]));
        }

        /// <summary>
        /// Regular grid on a plane: origin o, unit axes u and v, extents along u (us) and v (vs) given as sorted
        /// coordinate lists, outward normal n. Colour per vertex from colFn(u, v) (may be null).
        /// </summary>
        public static void Grid(MeshBuilder b, Vector3 o, Vector3 u, Vector3 v, IList<float> us, IList<float> vs, Vector3 n,
                                System.Func<float, float, Color> colFn, int uvU = 0, int uvV = 1)
        {
            int nu = us.Count, nv = vs.Count;
            int baseIdx = b.V.Count;
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    Vector3 p = o + u * us[i] + v * vs[j];
                    Color c = colFn != null ? colFn(us[i], vs[j]) : Col(1f);
                    b.Vtx(p, n, new Vector2(p[uvU], p[uvV]), c);
                }
            for (int j = 0; j < nv - 1; j++)
                for (int i = 0; i < nu - 1; i++)
                {
                    int a = baseIdx + j * nu + i;
                    QuadO(b, a, a + 1, a + nu + 1, a + nu, n);
                }
        }

        /// <summary>Sorted list from a..b with steps no longer than maxStep, plus extra breakpoints inside (a, b).</summary>
        public static List<float> Steps(float a, float b, float maxStep, IList<float> extra = null)
        {
            var l = new List<float>();
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a) / Mathf.Max(maxStep, 1e-3f) - 1e-4f));
            for (int i = 0; i <= n; i++) l.Add(a + (b - a) * i / n);
            if (extra != null)
                for (int i = 0; i < extra.Count; i++)
                {
                    float e = extra[i];
                    if (e <= a + 1e-3f || e >= b - 1e-3f) continue;
                    // an exact breakpoint replaces a grid value closer than 1 mm (shared edges must match exactly)
                    int near = -1;
                    for (int k = 1; k < l.Count - 1; k++) if (Mathf.Abs(l[k] - e) < 1e-3f) { near = k; break; }
                    if (near >= 0) l[near] = e; else l.Add(e);
                }
            l.Sort();
            return l;
        }

        /// <summary>Chamfered box with a rotation (MeshBuilder.M is restored afterwards).</summary>
        public static void Box(MeshBuilder b, Vector3 centre, Vector3 size, float chamfer, Quaternion rot, Color col)
        {
            var oldM = b.M; var oldC = b.Col;
            b.M = Matrix4x4.TRS(centre, rot, Vector3.one);
            b.Col = col;
            b.ChamferedBox(Vector3.zero, size, chamfer);
            b.M = oldM; b.Col = oldC;
        }

        public static void Box(MeshBuilder b, Vector3 centre, Vector3 size, float chamfer, Color col) => Box(b, centre, size, chamfer, Quaternion.identity, col);

        /// <summary>
        /// Registry marker (RegistryMeasure rule 3, the convention for clear openings and interiors): an empty
        /// GameObject (no renderer, no collider) whose position is the centre and whose lossyScale is the size of a
        /// named box, so its dimensions are exact and never bend-inflated. The parent must be unscaled and unrotated
        /// for an axis-aligned box. Registered under key.
        /// </summary>
        public static GameObject Marker(BuildContext ctx, string key, Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            ctx.Register(key, go);
            return go;
        }

        /// <summary>Load-time self check (Part 8.7): logs a warning for invalid meshes; nothing per frame.</summary>
        public static void Check(Mesh m, string who)
        {
            var r = MeshValidation.Validate(m);
            float w = MeshValidation.WindingAgreement(m);
            if (!r.ok || w < 0.97f)
                Debug.LogWarning("[Ring] " + who + ": mesh '" + m.name + "' degenerate " + r.degenerate + ", badIndex " + r.badIndex +
                                 ", nan " + r.nanVerts + ", zeroNormals " + r.zeroNormals + ", winding agreement " + w.ToString("0.000"));
        }

        /// <summary>Adds a renderer through BuildContext.AddMesh (bounds inflated for the bend) after validating it.
        /// Additive pools and glow billboards go through Beat34Additive.AddToWorld instead.</summary>
        public static GameObject Add(BuildContext ctx, string name, MeshBuilder b, string matId, RingSet set, bool shadows,
                                     float bendMaxDistance, Transform parent = null)
        {
            if (b.TriangleCount == 0) return null;
            var mesh = b.Build(name);
            Check(mesh, ctx.CurrentBuilder);
            return ctx.AddMesh(name, mesh, matId, set, shadows, bendMaxDistance, parent);
        }
    }
}
