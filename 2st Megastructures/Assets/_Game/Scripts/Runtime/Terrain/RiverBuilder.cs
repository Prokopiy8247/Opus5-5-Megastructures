using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The river ribbon (MASTER_PROMPT 14.5.2, 14.2.6 M03, DEVELOPMENT.md ADR-006): a flat strip at the surface height
    /// y -2.2 along the extended centre line <see cref="RingWorld.River"/> (x -8 200 .. 8 200), 40 m wide so its edges
    /// always lie under the 1:3 bank (the visible river is the 36 m between the waterlines, where the analytic carve
    /// passes below the plane: no co-planar faces). Mitred joints keep every edge exactly 20 m from the centre line.
    /// 4 m steps at the walk, 10 m at 1.5 km, 32 m far away, 8 strips across (about 18 k triangles). FarValley set (it
    /// persists to s 810), no shadow casting. Vertex data for RingWater.shader: uv0 = (arc length u from the node
    /// (0, 150), across offset v), uv1 = flow direction (x, z), normal up.
    /// </summary>
    public sealed class RiverBuilder : IBeatBuilder
    {
        public int Order => 11;
        public string Name => "River";

        struct Row
        {
            public Vector2 p;       // centre (x, z)
            public Vector2 offset;  // mitred across vector: p + offset * v is v metres from the centre line
            public Vector2 flow;    // unit flow direction
            public float u;         // arc length from the origin node
        }

        public IEnumerator Build(BuildContext ctx)
        {
            List<Row> rows = BuildRows();
            Mesh mesh = BuildMesh(rows, out int tris);
            yield return null;

            GameObject go = ctx.AddMesh("River.Ribbon", mesh, RingMat.Water, RingSet.FarValley, false, TerrainConfig.RibbonBendDistance);
            var info = go.AddComponent<TerrainInfo>();
            info.Kind = "River";
            info.RibbonWidth = TerrainConfig.RibbonWidth;
            info.WaterlineWidth = RingConstants.RiverWidth;
            info.SurfaceY = RingConstants.RiverSurfaceY;
            info.MeshTriangles = tris;
            info.VisibleTriangles = tris;
            ctx.Register("River.Ribbon", go);
            Debug.Log("[Ring] River ribbon: " + rows.Count + " rows x " + (TerrainConfig.RibbonStrips + 1) + " columns, " + tris + " triangles");
        }

        static float Step(float x)
        {
            return Mathf.Clamp(TerrainConfig.RibbonStepMin + TerrainConfig.RibbonStepGrowth * Mathf.Abs(x),
                               TerrainConfig.RibbonStepMin, TerrainConfig.RibbonStepMax);
        }

        static Vector2 Left(Vector2 d) => new Vector2(-d.y, d.x);

        /// <summary>Samples of the centre line with mitred offsets.</summary>
        static List<Row> BuildRows()
        {
            Vector2[] r = RingWorld.River;
            int nSeg = r.Length - 1;
            var dirs = new Vector2[nSeg];
            var lens = new float[nSeg];
            var cum = new float[r.Length];
            for (int i = 0; i < nSeg; i++)
            {
                Vector2 d = r[i + 1] - r[i];
                lens[i] = d.magnitude;
                dirs[i] = d / Mathf.Max(lens[i], 1e-4f);
                cum[i + 1] = cum[i] + lens[i];
            }
            int o = Mathf.Clamp(TerrainConfig.RiverOriginNode, 0, r.Length - 1);
            float u0 = cum[o];

            var rows = new List<Row>(1100);
            for (int i = 0; i < nSeg; i++)
            {
                // the vertex row at the start of segment i (mitre between i - 1 and i)
                Vector2 nIn = Left(i > 0 ? dirs[i - 1] : dirs[i]);
                Vector2 nOut = Left(dirs[i]);
                Vector2 m = (nIn + nOut).normalized;
                float scale = 1f / Mathf.Max(Vector2.Dot(m, nOut), 0.2f);
                Vector2 f0 = (i > 0 ? dirs[i - 1] + dirs[i] : dirs[i]).normalized;
                rows.Add(new Row { p = r[i], offset = m * scale, flow = f0, u = cum[i] - u0 });

                // interior rows of segment i; the last step is merged when it would be shorter than 35 % of a step
                float t = Step(r[i].x);
                while (t < lens[i] - 0.35f * Step((r[i] + dirs[i] * t).x))
                {
                    Vector2 p = r[i] + dirs[i] * t;
                    rows.Add(new Row { p = p, offset = nOut, flow = dirs[i], u = cum[i] + t - u0 });
                    t += Step(p.x);
                }
            }
            Vector2 last = Left(dirs[nSeg - 1]);
            rows.Add(new Row { p = r[nSeg], offset = last, flow = dirs[nSeg - 1], u = cum[nSeg] - u0 });
            return rows;
        }

        static Mesh BuildMesh(List<Row> rows, out int tris)
        {
            var mb = new MeshBuilder();
            int cols = TerrainConfig.RibbonStrips + 1;
            float half = 0.5f * TerrainConfig.RibbonWidth;
            float y = RingConstants.RiverSurfaceY;
            for (int k = 0; k < rows.Count; k++)
            {
                Row row = rows[k];
                for (int j = 0; j < cols; j++)
                {
                    float v = -half + TerrainConfig.RibbonWidth * j / (cols - 1);
                    Vector2 p = row.p + row.offset * v;
                    int idx = mb.Vtx(new Vector3(p.x, y, p.y), Vector3.up, new Vector2(row.u, v));
                    mb.UV1[idx] = row.flow;
                }
            }
            for (int k = 0; k < rows.Count - 1; k++)
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = k * cols + j, b = (k + 1) * cols + j, c = (k + 1) * cols + j + 1, d = k * cols + j + 1;
                    // up-facing (clockwise seen from +Y): choose the order whose winding normal points up
                    Vector3 pa = mb.V[a], pb = mb.V[b], pc = mb.V[c];
                    if (Vector3.Cross(pb - pa, pc - pa).y > 0f) mb.Quad(a, b, c, d);
                    else mb.Quad(a, d, c, b);
                }
            tris = mb.TriangleCount;
            return mb.Build("RiverRibbon", false);
        }
    }
}
