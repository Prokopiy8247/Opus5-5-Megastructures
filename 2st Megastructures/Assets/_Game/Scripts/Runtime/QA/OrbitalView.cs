using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Capture-only model of the WHOLE ring seen from space (orbital stills, -shots orbit; not part of the walk).
    /// The walk renders the ring locally (bent terrain, the rim wall plane, the hull cap under the pier); seen from tens of
    /// kilometres away it needs the true shape: a cylinder of radius 16 km around the X axis (axis at y 16 000, z 660 in the
    /// flat frame, so the walk's floor at y 0 under z 660 is its bottom), 200 m thick (hull radius 16 200, ADR-011), with
    /// rim walls 12 km tall (inner radius 4 km) at both ends: the right wall x 114..234 (the one the walk exits through),
    /// the left wall 20 km away (x -20 006..-19 886). The profile below is revolved in 720 steps; uv.x carries the part id
    /// for Megastructure/RingOrbital (0 hull, 1 right outer face, 2 wall tops, 3 inner wall faces, 4 floor, 5 left outer face).
    /// Built on first use, inactive outside orbital renders.
    /// </summary>
    public static class OrbitalView
    {
        public const float AxisY = RingConstants.RingR, AxisZ = 660f;
        public const float FloorR = RingConstants.RingR, HullR = RingConstants.RingR + RingConstants.HullDepth, TopR = RingConstants.RingR - RingConstants.WallTop;
        public const float RightIn = RingConstants.WallX, RightOut = RingConstants.WallOuterX, Width = 20000f;
        public static float LeftIn => RightIn - Width;
        public static float LeftOut => LeftIn - RingConstants.WallThickness;
        public static Vector3 RightCapCentre => new Vector3(RightOut, AxisY, AxisZ);
        public static Vector3 Centre => new Vector3(0.5f * (RightOut + LeftOut), AxisY, AxisZ);

        static GameObject _go;

        public static GameObject Ensure()
        {
            if (_go != null) return _go;
            var sh = Shader.Find("Megastructure/RingOrbital");
            if (sh == null) { Debug.LogWarning("[Ring] OrbitalView: shader Megastructure/RingOrbital not found"); return null; }
            _go = new GameObject("QA.OrbitalRing");
            _go.SetActive(false);
            var mf = _go.AddComponent<MeshFilter>();
            var mr = _go.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildMesh();
            mr.sharedMaterial = new Material(sh);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return _go;
        }

        public static bool IsOrbital(Renderer r) => _go != null && r != null && r.gameObject == _go;

        // profile edge: (x0, r0) -> (x1, r1), outward normal in (x, r), part id
        struct Edge { public float X0, R0, X1, R1, Nx, Nr, Part; }

        static Mesh BuildMesh()
        {
            float lo = LeftOut, li = LeftIn;
            var edges = new[]
            {
                new Edge { X0 = lo, R0 = HullR, X1 = RightOut, R1 = HullR, Nx = 0, Nr = 1, Part = 0 },        // hull
                new Edge { X0 = RightOut, R0 = HullR, X1 = RightOut, R1 = TopR, Nx = 1, Nr = 0, Part = 1 },    // right outer face
                new Edge { X0 = RightOut, R0 = TopR, X1 = RightIn, R1 = TopR, Nx = 0, Nr = -1, Part = 2 },     // right wall top
                new Edge { X0 = RightIn, R0 = TopR, X1 = RightIn, R1 = FloorR, Nx = -1, Nr = 0, Part = 3 },    // right inner face
                new Edge { X0 = RightIn, R0 = FloorR, X1 = li, R1 = FloorR, Nx = 0, Nr = -1, Part = 4 },        // floor
                new Edge { X0 = li, R0 = FloorR, X1 = li, R1 = TopR, Nx = 1, Nr = 0, Part = 3 },               // left inner face
                new Edge { X0 = li, R0 = TopR, X1 = lo, R1 = TopR, Nx = 0, Nr = -1, Part = 2 },                // left wall top
                new Edge { X0 = lo, R0 = TopR, X1 = lo, R1 = HullR, Nx = -1, Nr = 0, Part = 5 },               // left outer face
            };
            const int N = 720;
            int vPer = (N + 1) * 2;
            var pos = new Vector3[edges.Length * vPer];
            var nrm = new Vector3[pos.Length];
            var uv = new Vector2[pos.Length];
            var tris = new int[edges.Length * N * 6];
            int vi = 0, ti = 0;
            foreach (var e in edges)
            {
                int b = vi;
                for (int i = 0; i <= N; i++)
                {
                    float phi = i * (2f * Mathf.PI / N);
                    var u = new Vector3(0f, -Mathf.Cos(phi), Mathf.Sin(phi));     // radial unit, phi 0 = the bottom (the walk)
                    var n = new Vector3(e.Nx, 0f, 0f) + u * e.Nr;
                    pos[vi] = new Vector3(e.X0, AxisY, AxisZ) + u * e.R0; nrm[vi] = n; uv[vi] = new Vector2(e.Part, 0f); vi++;
                    pos[vi] = new Vector3(e.X1, AxisY, AxisZ) + u * e.R1; nrm[vi] = n; uv[vi] = new Vector2(e.Part, 1f); vi++;
                }
                for (int i = 0; i < N; i++)
                {
                    int a0 = b + 2 * i, a1 = a0 + 1, c0 = a0 + 2, c1 = a0 + 3;
                    Tri(tris, ref ti, pos, nrm, a0, a1, c1);
                    Tri(tris, ref ti, pos, nrm, a0, c1, c0);
                }
            }
            var m = new Mesh { name = "QA.OrbitalRing", indexFormat = IndexFormat.UInt32 };
            m.vertices = pos; m.normals = nrm; m.uv = uv; m.triangles = tris;
            m.bounds = new Bounds(Centre, new Vector3(Width + 400f, 2f * HullR + 400f, 2f * HullR + 400f));
            return m;
        }

        // Unity front faces: cross(b - a, c - a) points along the outward normal
        static void Tri(int[] t, ref int i, Vector3[] p, Vector3[] n, int a, int b, int c)
        {
            if (Vector3.Dot(Vector3.Cross(p[b] - p[a], p[c] - p[a]), n[a]) < 0f) { int s = b; b = c; c = s; }
            t[i++] = a; t[i++] = b; t[i++] = c;
        }
    }
}
