using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>Beat 2, persistent shaft skeleton: shell, rings, columns and the marker lights above the walkway (14.5.2 a-b, 14.7.2).</summary>
    public sealed partial class Beat02_Bridge
    {
        const int C1_SEGMENTS = 128, COLUMN_SEGMENTS = 96;
        const float BAND_BEVEL = 0.05f;
        const float CORNER_AO = 0.62f, CORNER_AO_RANGE = 70f;   // shell: soft darkening into the 120 degree corners
        const float RING_BATCH_CHUNK = 480f;                    // 8 ring levels per instanced draw
        const float MARKER_MIN_Y = 20f;                         // marker lights only above the walkway (the abyss stays dark)
        const float RING_MARKER_D = 0.5f, BAND_MARKER_D = 0.3f;
        const float MARKER_STRENGTH = 0.7f, MARKER_ENERGY = 0.5f;          // look-dev r03: 0.5 -> 0.7, the rib rows read in KS2a
        const int BAND_MARKERS = 4;
        const int RIB_MARKERS_PER_FACE = 8;                     // 7 points between the corners of each ring face (~50 m apart)
        const float BAND_MARKER_MIN_Y = 100f;                   // C1 band markers from the 120 m band up (above the ledge lamps)
        const float BAND_MARKER_MIN_Y_FAR = -10f;               // C2-C4 from the y = 0 band up (KS2a: C3 shows its 120 m band rhythm)

        MeshRenderer _farFlatChunk;
        readonly List<MeshRenderer> _faceChunks = new List<MeshRenderer>();
        readonly MeshRenderer[][] _columnChunks = new MeshRenderer[4][];
        Beat02Rings.Set _rings;

        // ---------------------------------------------------------------- shaft shell

        void BuildShaftShell(BeatContext ctx, MeshBuilder mb)
        {
            Material wall = ctx.Mats[MatId.ConcreteBoard];
            Transform shell = BeatKit.Group(_geo, "ShaftShell");
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            float a = AbyssConst.SHAFT_APOTHEM;
            int chunks = Mathf.RoundToInt((AbyssConst.SHAFT_TOP_Y - AbyssConst.SHAFT_BOTTOM_Y) / CHUNK);
            bool shade = _tier != BeatTier.Graybox;

            // near-flat openings (world x, y on z = 60): gallery doorway, tunnel mouth, the square around the fan bore
            float zN = AbyssConst.SHAFT_NEAR_Z;
            Vector3 fan = AbyssConst.FAN_CENTRE;
            Vector3 mouth = AbyssConst.W[15];
            var holes = new List<BeatKit.Rect2>
            {
                new BeatKit.Rect2(-AbyssConst.EXIT_DOOR_W * 0.5f, AbyssConst.W[3].y, AbyssConst.EXIT_DOOR_W * 0.5f, AbyssConst.W[3].y + AbyssConst.EXIT_DOOR_H),
                new BeatKit.Rect2(mouth.x - AbyssConst.TUNNEL_W * 0.5f, mouth.y, mouth.x + AbyssConst.TUNNEL_W * 0.5f, mouth.y + AbyssConst.TUNNEL_H),
                new BeatKit.Rect2(fan.x - BeatKit.BORE_SQUARE_HALF, fan.y - BeatKit.BORE_SQUARE_HALF, fan.x + BeatKit.BORE_SQUARE_HALF, fan.y + BeatKit.BORE_SQUARE_HALF),
            };
            float xN0 = MeshKit.HexCorner(c, a, NEAR_FLAT).x, xN1 = MeshKit.HexCorner(c, a, NEAR_FLAT + 1).x;

            for (int k = 0; k < 6; k++)
            {
                Vector2 n = MeshKit.HexFaceNormal(k);
                for (int j = 0; j < chunks; j++)
                {
                    float y1 = AbyssConst.SHAFT_TOP_Y - CHUNK * j, y0 = y1 - CHUNK;
                    Vector3 origin;
                    if (k == NEAR_FLAT)
                    {
                        origin = new Vector3(0f, y0, zN);
                        mb.Push(Matrix4x4.Translate(-origin));
                        BeatKit.ZPlaneWithHoles(mb, zN, true, new BeatKit.Rect2(xN0, y0, xN1, y1), holes, WALL_CELL);
                        mb.Pop();
                    }
                    else
                    {
                        origin = new Vector3(c.x + n.x * a, y0, c.y + n.y * a);   // one xz per face (SHADERS.md 5)
                        mb.Push(Matrix4x4.Translate(-origin));
                        MeshKit.HexWallPanel(mb, c, a, k, y0, y1, 8, 4, true);
                        mb.Pop();
                    }
                    if (shade) ShadeCorners(mb, 0, origin, k);
                    MeshRenderer r = BeatKit.Emit(mb, shell, "Face" + k + "_" + j, wall, _meshes, ShadowCastingMode.Off, origin, true);
                    if (r == null) continue;
                    _faceChunks.Add(r);
                    if (k == FAR_FLAT && _farFlatChunk == null) _farFlatChunk = r;
                }
            }

            // the square piece around the bore hole (circle polygon shared with the bore lining and the bore card)
            var boreOrigin = new Vector3(fan.x, fan.y, zN);
            mb.Push(Matrix4x4.Translate(-boreOrigin));
            BeatKit.ZSquareMinusCircle(mb, zN, true, new Vector2(fan.x, fan.y), BeatKit.BORE_SQUARE_HALF, AbyssConst.FAN_R_OUT, BeatKit.BORE_SEGMENTS);
            mb.Pop();
            BeatKit.Emit(mb, shell, "NearFlatBoreRing", wall, _meshes, ShadowCastingMode.Off, boreOrigin, true);
        }

        /// <summary>Baked AO (vertex R) falling toward the two vertical corners of face k (chunk-local vertices + origin).</summary>
        static void ShadeCorners(MeshBuilder mb, int first, Vector3 origin, int face)
        {
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            Vector2 c0 = MeshKit.HexCorner(c, AbyssConst.SHAFT_APOTHEM, face), c1 = MeshKit.HexCorner(c, AbyssConst.SHAFT_APOTHEM, face + 1);
            List<Vector3> P = mb.Positions;
            List<Color32> C = mb.Colors;
            for (int i = first; i < P.Count; i++)
            {
                Vector2 p = new Vector2(P[i].x + origin.x, P[i].z + origin.z);
                float d = Mathf.Min((p - c0).magnitude, (p - c1).magnitude);
                float ao = Mathf.Lerp(CORNER_AO, 1f, BeatKit.Smoothstep(d / CORNER_AO_RANGE));
                Color32 col = C[i];
                col.r = (byte)Mathf.RoundToInt(col.r * ao);
                C[i] = col;
            }
        }

        // ---------------------------------------------------------------- ribs and belts

        void BuildRings(BeatContext ctx)
        {
            bool simplified = _tier == BeatTier.Simplified;
            _rings = Beat02Rings.Build(simplified, !simplified);
            _meshes.AddRange(_rings.Meshes);
            Material mat = ctx.Mats[MatId.ConcreteBoard];
            Transform rings = BeatKit.Group(_geo, "Rings");
            var batchOf = new Dictionary<Mesh, InstancedBatch>();
            for (int i = 0; i < _rings.Draws.Count; i++)
            {
                Beat02Rings.Draw d = _rings.Draws[i];
                if (d.Instanced)
                {
                    if (!batchOf.TryGetValue(d.Mesh, out InstancedBatch b))
                    {
                        b = new InstancedBatch(d.Mesh.name, d.Mesh, mat, ShadowCastingMode.Off, true, 0, RING_BATCH_CHUNK);
                        batchOf[d.Mesh] = b;
                        _batches.Add(b);
                    }
                    b.Add(d.Matrix);
                }
                else
                {
                    BeatKit.Renderer(rings, d.Mesh.name, d.Mesh, mat, d.Matrix.GetColumn(3), Quaternion.identity, ShadowCastingMode.Off, true);
                }
            }
        }

        // ---------------------------------------------------------------- columns

        static bool ColumnBuilt(BeatTier tier, int i) => tier != BeatTier.Simplified || i == 0 || i == 2;

        void BuildColumns(BeatContext ctx, MeshBuilder mb)
        {
            Transform cols = BeatKit.Group(_geo, "Columns");
            var bounds = new List<float>();
            var profile = new List<Vector2>(64);
            float top = AbyssConst.COLUMN_TOP_Y, bottom = AbyssConst.COLUMN_BOTTOM_Y;
            float hb = AbyssConst.COLUMN_BAND_H * 0.5f, sp = AbyssConst.COLUMN_BAND_SPACING;

            // chunk boundaries every 200 m from the top; a boundary that would cut a band moves just above it
            bounds.Add(top);
            for (float b = top - CHUNK; b > bottom + 1f; b -= CHUNK)
            {
                float k = Mathf.Round((top - b) / sp);
                float centre = top - sp * k;
                bounds.Add(Mathf.Abs(b - centre) < hb + 0.5f ? centre + hb + 0.5f : b);
            }
            bounds.Add(bottom);

            for (int i = 0; i < AbyssConst.COLUMNS.Length; i++)
            {
                if (!ColumnBuilt(_tier, i)) continue;
                Vector3 col = AbyssConst.COLUMNS[i];
                float R = col.z;
                int segs = i == 0 ? C1_SEGMENTS : COLUMN_SEGMENTS;
                Material mat = i == 0 ? ctx.Mats[MatId.ConcreteCyl] : ctx.Mats.Variant(MatId.ConcreteCyl, "C" + (i + 1), m =>
                {
                    m.SetFloat(CylRadiusId, R);
                    m.SetFloat(PanelCountId, Mathf.Round(2f * Mathf.PI * R / 1.2f));
                });
                Transform g = BeatKit.Group(cols, "C" + (i + 1));
                var chunks = new MeshRenderer[bounds.Count - 1];
                for (int j = 0; j + 1 < bounds.Count; j++)
                {
                    float hi = bounds[j], lo = bounds[j + 1];
                    MeshKit.BandedColumnProfile(profile, R, 0f, hi - lo, sp, AbyssConst.COLUMN_BAND_H, AbyssConst.COLUMN_BAND_PROTRUSION,
                        BAND_BEVEL, top - lo);
                    MeshKit.Lathe(mb, profile, segs);
                    // C1 near the walk casts (the key light grades the ledge); the far giants do not (14.2 shadows)
                    bool cast = i == 0 && hi > -200f && lo < 200f;
                    chunks[j] = BeatKit.Emit(mb, g, "C" + (i + 1) + "_" + j, mat, _meshes, cast ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        new Vector3(col.x, lo, col.y), true);
                }
                _columnChunks[i] = chunks;
            }
        }

        // ---------------------------------------------------------------- marker lights

        /// <summary>
        /// Dim cold marker points (Glow, at least 2.5 px, fogged additively): the six inner corners of every ring above the
        /// walkway and four points on every column band from y 120 up. They read the hexagon and the column rhythm far into the
        /// upper fog (depth layers far / extreme far); nothing is placed below the walkway, so the abyss stays dark (14.7.2,
        /// KS2b side strips).
        /// </summary>
        void BuildMarkers(BeatContext ctx)
        {
            if (_tier == BeatTier.Graybox) return;
            Material mat = GlowMaterial(ctx.Mats, "b2marker", AbyssPalette.COLD_LAMP, MARKER_STRENGTH, MARKER_ENERGY);
            var b = new InstancedBatch("B2_Markers", _glowQuad, mat, ShadowCastingMode.Off, false, 0, 1000f);
            for (int i = 0; i < _rings.Levels.Count; i++)
            {
                Beat02Rings.Level L = _rings.Levels[i];
                if (L.Y < MARKER_MIN_Y) continue;
                for (int c = 0; c < 6; c++)
                    b.Add(Matrix4x4.TRS(Beat02Rings.MarkerPosition(L, c), Quaternion.identity, Vector3.one * RING_MARKER_D));
                // look-dev r01: a row along every face as well (about every 50 m, the hoist lane left dark). The shaft sun lights
                // only the rings' top faces, which the eye sees edge-on, so without them the 60 m rhythm of the ribs above the
                // walkway did not read in KS2a against the fogged far wall and the right wall
                float ap = Beat02Rings.InnerApothem(L.Belt);
                Vector3 centre = Beat02Rings.Centre(L.Y);
                for (int c = 0; c < 6; c++)
                {
                    Vector3 c0 = Beat02Rings.Corner(c, ap, 0f), c1 = Beat02Rings.Corner(c + 1, ap, 0f);
                    Vector2 n = MeshKit.HexFaceNormal(c);
                    var inset = new Vector3(n.x, 0f, n.y) * Beat02Rings.MARKER_INSET;
                    for (int j = 1; j < RIB_MARKERS_PER_FACE; j++)
                    {
                        Vector3 p = centre + Vector3.Lerp(c0, c1, (float)j / RIB_MARKERS_PER_FACE) - inset;
                        if (L.Lane && c == Beat02Rings.FAR_SIDE && Mathf.Abs(p.x - HOIST_X) < Beat02Rings.LANE_HALF + 2f) continue;
                        b.Add(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * RING_MARKER_D));
                    }
                }
            }
            float sp = AbyssConst.COLUMN_BAND_SPACING, top = AbyssConst.COLUMN_TOP_Y;
            for (int i = 0; i < AbyssConst.COLUMNS.Length; i++)
            {
                if (!ColumnBuilt(_tier, i)) continue;
                Vector3 col = AbyssConst.COLUMNS[i];
                float r = col.z + AbyssConst.COLUMN_BAND_PROTRUSION + 0.15f;
                float minY = i == 0 ? BAND_MARKER_MIN_Y : BAND_MARKER_MIN_Y_FAR;
                for (int k = 1; top - sp * k >= minY; k++)
                {
                    float y = top - sp * k - 0.4f;
                    for (int m = 0; m < BAND_MARKERS; m++)
                    {
                        float ang = (45f + 90f * m + 22.5f * (i & 1)) * Mathf.Deg2Rad;
                        var p = new Vector3(col.x + r * Mathf.Cos(ang), y, col.y + r * Mathf.Sin(ang));
                        b.Add(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * BAND_MARKER_D));
                    }
                }
            }
            _batches.Add(b);
            _markerCount = b.Count;
        }

        int _markerCount;
    }
}
