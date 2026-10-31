using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Measures objects registered in <see cref="BuildContext.Registry"/> for the numeric scale tests (14.10) and the
    /// composition check (Part 10 #35). Renderer bounds cannot be used directly because AddMesh inflates mesh bounds
    /// for the bend (14.7 H1), so measurement uses the actual vertices. Precedence (first that applies):
    /// 1 a MeshFilter on the object itself: world AABB of its vertices;
    /// 2 a BoxCollider on the object itself: world AABB of the oriented box;
    /// 3 a marker transform (no renderer, collider or mesh on itself, lossyScale != 1): oriented box of size lossyScale
    ///   centred on the position (the convention for clear openings and interiors);
    /// 4 MeshFilters of the children: combined vertex AABB;
    /// 5 BoxColliders of the children; 6 Renderer bounds (inflated); 7 the position as a point.
    /// A Mesh object is measured by its vertices (local space).
    /// </summary>
    public static class RegistryMeasure
    {
        static readonly List<Vector3> _tmp = new List<Vector3>(1 << 16);

        public static GameObject AsGameObject(Object o)
        {
            if (o is GameObject g) return g;
            if (o is Component c) return c.gameObject;
            return null;
        }

        public static bool TryWorldBounds(Object o, out Bounds b, out string method)
        {
            b = default; method = "none";
            if (o == null) return false;
            if (o is Mesh mesh)
            {
                if (!mesh.isReadable) { b = mesh.bounds; method = "mesh.bounds (not readable)"; return true; }
                mesh.GetVertices(_tmp);
                if (_tmp.Count == 0) return false;
                b = Encapsulate(_tmp, Matrix4x4.identity);
                method = "mesh vertices";
                return true;
            }
            var go = AsGameObject(o);
            if (go == null) return false;
            var t = go.transform;

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && MeshAabb(mf, out b)) { method = "self mesh vertices"; return true; }

            var bc = go.GetComponent<BoxCollider>();
            if (bc != null) { b = OrientedBox(t.localToWorldMatrix, bc.center, bc.size); method = "self BoxCollider"; return true; }

            var ls = t.lossyScale;
            bool marker = go.GetComponent<Renderer>() == null && go.GetComponent<Collider>() == null &&
                          (Mathf.Abs(ls.x - 1f) > 1e-4f || Mathf.Abs(ls.y - 1f) > 1e-4f || Mathf.Abs(ls.z - 1f) > 1e-4f);
            if (marker) { b = OrientedBox(Matrix4x4.TRS(t.position, t.rotation, Vector3.one), Vector3.zero, ls); method = "marker transform"; return true; }

            bool any = false;
            foreach (var cmf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (cmf.sharedMesh == null || !MeshAabb(cmf, out var cb)) continue;
                if (!any) { b = cb; any = true; } else b.Encapsulate(cb);
            }
            if (any) { method = "children mesh vertices"; return true; }

            foreach (var cbc in go.GetComponentsInChildren<BoxCollider>(true))
            {
                var cb = OrientedBox(cbc.transform.localToWorldMatrix, cbc.center, cbc.size);
                if (!any) { b = cb; any = true; } else b.Encapsulate(cb);
            }
            if (any) { method = "children BoxColliders"; return true; }

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) { method = "renderer bounds (bend-inflated)"; return true; }

            b = new Bounds(t.position, Vector3.zero);
            method = "transform position";
            return true;
        }

        /// <summary>All mesh vertices of the object and its children in world space (empty if none).</summary>
        public static int WorldVertices(Object o, List<Vector3> into)
        {
            into.Clear();
            if (o is Mesh mesh) { if (mesh.isReadable) mesh.GetVertices(into); return into.Count; }
            var go = AsGameObject(o);
            if (go == null) return 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                m.GetVertices(_tmp);
                var l2w = mf.transform.localToWorldMatrix;
                for (int i = 0; i < _tmp.Count; i++) into.Add(l2w.MultiplyPoint3x4(_tmp[i]));
            }
            return into.Count;
        }

        static bool MeshAabb(MeshFilter mf, out Bounds b)
        {
            b = default;
            var m = mf.sharedMesh;
            if (!m.isReadable)
            {
                var r = mf.GetComponent<Renderer>();
                if (r == null) return false;
                b = r.bounds;
                return true;
            }
            m.GetVertices(_tmp);
            if (_tmp.Count == 0) return false;
            b = Encapsulate(_tmp, mf.transform.localToWorldMatrix);
            return true;
        }

        static Bounds Encapsulate(List<Vector3> v, Matrix4x4 m)
        {
            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
            for (int i = 0; i < v.Count; i++)
            {
                var p = m.MultiplyPoint3x4(v[i]);
                mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
            }
            var b = new Bounds(); b.SetMinMax(mn, mx);
            return b;
        }

        public static Bounds OrientedBox(Matrix4x4 m, Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
            for (int i = 0; i < 8; i++)
            {
                var c = centre + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z);
                var p = m.MultiplyPoint3x4(c);
                mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
            }
            var b = new Bounds(); b.SetMinMax(mn, mx);
            return b;
        }
    }
}
