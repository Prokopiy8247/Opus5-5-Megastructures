using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Full and Simplified tiers of the gallery (14.5.1 Geometry; Simplified: no pilasters, bolts or wall fan, 5 fixtures).
    ///
    /// Layout (world metres, floor y 0 / -3, ceiling = floor + 2.5): portal frames every 6 m (pilaster pairs 0.40 x 0.10 at
    /// z = 1.5 + 6 k with 0.15 haunches) under ceiling beams every 3 m (2.4 x 0.25 x 0.30 at z = 1.5 + 3 k, sheared to follow the
    /// sloped ceiling over the stairs); the lighting trunk along the ceiling centre carries the 10 fixtures between the beams
    /// (z = 3 + 6 i); the pipe O 0.18 runs on the left wall at floor + 0.6 (24-sided tube with 0.35 m elbows at the stairs) with a
    /// weld-neck flange pair every 3 m (z = 3 k) carrying 8 instanced hex bolts each and a clamp support between flanges; the
    /// cable tray (0.30 x 0.06, floor + 2.1) runs on 39 brackets on the right wall and stops either side of the bulkhead, where
    /// its 3 cables O 0.04 cross through a sealed transit frame; the steel bulkhead at z = 22 has a 0.07 coaming around the
    /// 1.1 x 2.0 opening, stiffeners, dogs and hinges, and its leaf stands open at 92 degrees on the left with the torus
    /// handwheel O 0.4 toward the corridor; the angled wall circulator O 0.5 (6 twisted blades, 0.5 rev/s) sits on a wall arm at
    /// z = 30, centre y 1.7; the 0.3 x 0.3 drain grating at (0, 0, 12) lies 12 mm below its frame over a dark sump; a worn
    /// 0.10 m paint line 5 mm proud runs on the left (x -0.78) with painted nosings on all 16 stair edges; a polished
    /// handrail runs on the right over the stairs; a closed steel door sits in the start wall; the amber well-glass lamp hangs
    /// on the end wall over the exit inside a steel architrave.
    ///
    /// Everything inside the end wall is one interior detail tier (DetailTier by camera distance, 20 m hysteresis, dithered):
    /// near = the interior, far = the equal-luma proxy card in the doorway, gone by 30 m (d 90 on the bridge, 14.6). The jambs,
    /// lintel, threshold, Face boss and the shadow-only roof stay for the whole walk.
    /// </summary>
    public sealed partial class Beat01_Gallery
    {
        // ---------------------------------------------------------------- structure
        const int BEAM_COUNT = 20;
        const float BEAM_Z0 = 1.5f, BEAM_SPACING = 3f, BEAM_DEPTH = 0.25f, BEAM_THICK = 0.30f, BEAM_EMBED = 0.02f, BEAM_BEVEL = 0.02f;
        const int PILASTER_PAIRS = 10;
        const float PILASTER_Z0 = 1.5f, PILASTER_SPACING = 6f, PILASTER_W = 0.40f, PILASTER_D = 0.10f, PILASTER_BEVEL = 0.02f;
        const float HAUNCH = 0.15f;
        /// <summary>Elements embed this far into walls, floor and ceiling (light leaks at joints, 14.5.1 pitfalls).</summary>
        const float EMBED = 0.02f;

        // ---------------------------------------------------------------- lighting trunk and fixtures
        const float TRUNK_HALF_W = 0.05f, TRUNK_HALF_H = 0.03f, TRUNK_TOP_EMBED = 0.015f;
        const float FIX_HOUSING_W = 0.30f, FIX_HOUSING_H = 0.09f, FIX_HOUSING_L = 1.10f, FIX_HOUSING_TOP = 0.035f;
        const float FIX_LENS_HALF_W = 0.11f, FIX_LENS_L = 1.0f, FIX_LENS_DROP = 0.015f, FIX_LENS_EDGE_GAIN = 0.15f;
        /// <summary>Fixture point light this far below the ceiling (7-8 cm under the lens): the ceiling gets grazing light only.</summary>
        const float FIX_LIGHT_BELOW = 0.22f;

        // ---------------------------------------------------------------- pipe (left wall)
        const float PIPE_X = -0.99f, PIPE_Y = 0.6f, PIPE_R = 0.09f, PIPE_ELBOW_R = 0.35f;
        const int PIPE_SEGMENTS = 24;
        const int FLANGE_COUNT = 20, BOLTS_PER_FLANGE = 8;
        const float FLANGE_Z0 = 0f, FLANGE_SPACING = 3f, FLANGE_FACE = 0.028f;
        const float BOLT_AF = 0.024f, BOLT_CIRCLE_R = 0.128f;
        const float SUPPORT_Z0 = 1.5f, SUPPORT_SPACING = 3f;

        // ---------------------------------------------------------------- cable tray (right wall)
        const float TRAY_Y = 2.1f, TRAY_X0 = 0.78f, TRAY_X1 = 1.08f, TRAY_HALF_H = 0.03f, TRAY_T = 0.008f, TRAY_BEND_R = 0.3f;
        const float TRAY_GAP0 = 21.62f, TRAY_GAP1 = 22.38f;
        const float TRAY_BRACKET_Z0 = 0.75f, TRAY_BRACKET_SPACING = 1.5f;
        const float CABLE_R = 0.02f;
        static readonly float[] CABLE_X = { 0.84f, 0.93f, 1.02f };

        // ---------------------------------------------------------------- pressure door (z = 22)
        const float COAMING_W = 0.07f, PLATE_T = 0.03f, STIFF_T = 0.012f, STIFF_D = 0.07f, STIFF_X = 0.76f;
        static readonly Vector3 LEAF_HINGE = new Vector3(-0.68f, 0f, 21.84f);
        const float LEAF_OPEN_DEG = 92f;
        /// <summary>Handwheel torus: centre radius and tube radius, outer diameter 2 (R + r) = 0.40 (14.5.1).</summary>
        const float WHEEL_R = 0.183f, WHEEL_TUBE = 0.017f;

        // ---------------------------------------------------------------- wall fan (left wall, z = 30, centre y 1.7)
        const float FAN_Z = 30f, FAN_Y = 1.7f, FAN_TIP_R = 0.25f, FAN_REV_PER_S = 0.5f;
        const int FAN_BLADES = 6;
        static readonly Vector3 FAN_MOTOR = new Vector3(-0.86f, FAN_Y, FAN_Z);
        /// <summary>Fan axis: into the corridor and toward the approaching player, tipped 6 degrees down.</summary>
        static readonly Vector3 FAN_AXIS = new Vector3(0.60f, -0.10f, -0.79f).normalized;

        // ---------------------------------------------------------------- floor details
        const float DRAIN_HALF = 0.15f, DRAIN_HOLE_HALF = 0.175f, DRAIN_FRAME_OUT = 0.18f, DRAIN_DEPTH = 0.3f, GRATING_DROP = 0.012f;
        const float LINE_X = -0.78f, LINE_W = 0.10f, LINE_OFFSET = 0.005f, PAINT_T = 0.012f, NOSING_W = 0.05f;
        const float RAIL_X = 1.04f, RAIL_R = 0.0225f, RAIL_H = 0.9f;
        /// <summary>Stair egress markers: set back from the stair ends, height above the floor, lens emission, glow strength.</summary>
        const float MARKER_SETBACK = 0.25f, MARKER_Y = 0.30f, MARKER_EMISSION = 1.2f, MARKER_GLOW = 0.6f;

        // ---------------------------------------------------------------- doorway shaft and proxy card
        /// <summary>Doorway spill prism (BeamWedge mode 2): 12 m into the gallery ("the last 12 m brighten"), density per metre.</summary>
        const float SHAFT_LENGTH = 12f, SHAFT_DENSITY = 0.02f;
        /// <summary>
        /// Equal-luma proxy for the interior seen from the bridge (linear, before EV 0): through the 1.2 x 2.0 opening the bridge
        /// sees the lower floor, the lit risers and the amber-warmed first metres; tune against the d 90 look-back capture.
        /// </summary>
        static readonly Color PROXY_TOP = new Color(0.034f, 0.037f, 0.042f, 1f);
        static readonly Color PROXY_BOTTOM = new Color(0.046f, 0.040f, 0.036f, 1f);

        readonly List<Renderer> _near = new List<Renderer>(48);
        InstancedBatch _bolts;
        MeshRenderer _shaft;
        MaterialPropertyBlock _shaftMpb; // created in Build (Unity forbids engine objects in field initializers)
        Bounds _trayNearBounds, _fanBounds;
        int _beamCount, _pilasterCount, _flangeCount, _cableCount, _nosingCount;
        float _beamDepth, _pipeDiameter, _cableDiameter, _trayY, _lineWidth, _lineOffset, _wheelDiameter, _fanDiameter;

        // scratch (build time)
        readonly List<Vector3> _run = new List<Vector3>(8);
        readonly List<Vector3> _path = new List<Vector3>(64);
        readonly List<LoftFrame> _frames = new List<LoftFrame>(64);
        readonly CrossSection _sec = new CrossSection();

        MeshRenderer AddNear(MeshRenderer r)
        {
            if (r != null) _near.Add(r);
            return r;
        }

        static Color Srgb(Color linear) { Color c = linear.gamma; c.a = 1f; return c; }

        // ================================================================================================ build

        void BuildFull(BeatContext ctx, MeshBuilder mb, bool simplified)
        {
            MaterialLibrary mats = ctx.Mats;
            Material conc = NoKey(mats, MatId.ConcreteBoard);
            Material steel = NoKey(mats, MatId.SteelPainted);
            Material leaf = NoKey(mats, MatId.DoorLeaf);
            Material rail = NoKey(mats, MatId.RodPolished);
            Material cable = NoKey(mats, MatId.Cable);
            Material grating = NoKey(mats, MatId.Grating);
            Material pipe = mats.Variant(MatId.SteelPainted, "nokey/pipe", m =>
            {
                m.SetFloat(KeyLightMaskId, 0f);
                m.SetColor(ShaderIds.BaseColor, AbyssPalette.DOOR_LEAF);   // a shade lighter than the frames, so the run reads
                m.SetFloat("_RustAmount", 0.04f);
            });
            Material paint = mats.Variant(MatId.ConcreteFloor, "nokey/paint", m =>
            {
                m.SetFloat(KeyLightMaskId, 0f);
                m.SetColor(ShaderIds.BaseColor, AbyssPalette.CONC_LIGHT);   // faded pale paint on concrete
                m.SetFloat(ShaderIds.Roughness, 0.55f);
                m.SetFloat(BeatKit.WearWidthId, 0f);
            });
            Material sump = mats.Variant(MatId.ConcreteBoard, "nokey/sump", m =>
            {
                m.SetFloat(KeyLightMaskId, 0f);
                m.SetColor(ShaderIds.BaseColor, AbyssPalette.CONC_DARK);
            });

            BuildStructure(mb, conc, simplified);
            BuildPaint(mb, paint, ctx.Seed);
            BuildDrain(mb, sump, grating, steel);
            BuildTrunkAndFixtures(ctx, mb, steel);
            BuildPipe(mb, pipe, steel, simplified, ctx.Seed);
            BuildTray(mb, steel, cable);
            BuildBulkhead(mb, steel, leaf);
            if (!simplified) BuildWallFan(mb, steel, leaf);
            BuildHandrail(mb, rail, steel);
            BuildStartDoor(mb, steel, leaf);
            BuildStairMarkers(ctx, mb, steel);
            BuildExitLamp(ctx, mb, steel);
            BuildDoorwayShaft(ctx, mb);
            BuildProxyAndTier(ctx, mb);
        }

        // ------------------------------------------------------------------------------------------ helpers

        /// <summary>Floor-following polyline at lateral x and height floor + yOff from zStart to zEnd, with the stair corners.</summary>
        void FloorRun(float x, float yOff, float zStart, float zEnd)
        {
            _run.Clear();
            _run.Add(new Vector3(x, FloorY(zStart) + yOff, zStart));
            if (zStart < AbyssConst.STAIR_Z0 && zEnd > AbyssConst.STAIR_Z0) _run.Add(new Vector3(x, AbyssConst.W[1].y + yOff, AbyssConst.STAIR_Z0));
            if (zStart < AbyssConst.STAIR_Z1 && zEnd > AbyssConst.STAIR_Z1) _run.Add(new Vector3(x, AbyssConst.W[2].y + yOff, AbyssConst.STAIR_Z1));
            _run.Add(new Vector3(x, FloorY(zEnd) + yOff, zEnd));
        }

        static bool OnStairSlope(float z) => z > AbyssConst.STAIR_Z0 && z < AbyssConst.STAIR_Z1;

        static readonly Vector3 SLOPE_DIR = new Vector3(0f, -AbyssConst.STAIR_DROP, AbyssConst.STAIR_Z1 - AbyssConst.STAIR_Z0).normalized;

        /// <summary>Unit direction of a floor-following run at z (away from the fillets).</summary>
        static Vector3 RunTangent(float z) => OnStairSlope(z) ? SLOPE_DIR : Vector3.forward;

        /// <summary>Vertical half height of a perpendicular section of half height hh at z (sheared by the slope).</summary>
        static float VerticalHalf(float z, float hh) => OnStairSlope(z) ? hh / SLOPE_DIR.z : hh;

        static void Box(MeshBuilder mb, float x0, float x1, float y0, float y1, float z0, float z1, float bevel)
        {
            MeshKit.ChamferBox(mb, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), bevel);
        }

        // ------------------------------------------------------------------------------------------ floor (called by BuildShared)

        /// <summary>
        /// Upper floor top (y 0, z -0.65..40) with the drain hole, its 2 cm front chamfer and the face of the first riser. The
        /// slab body is never visible (walls and the start wall stand on it), so only the top and the step edge are built.
        /// </summary>
        void BuildUpperFloorWithDrain(MeshBuilder mb)
        {
            float x0 = -1.25f, x1 = 1.25f, z0 = SHELL_START_Z - 0.05f, z1 = AbyssConst.STAIR_Z0, y = AbyssConst.W[1].y;
            float hh = DRAIN_HOLE_HALF, zc = AbyssConst.DRAIN_Z;
            Beat01Geo.FloorTopWithHole(mb, x0, x1, z0, z1 - 0.02f, y, -hh, hh, zc - hh, zc + hh);
            Color32 saved = mb.Color;
            mb.Color = new Color32(saved.r, 255, saved.b, saved.a);
            Vector3 n = new Vector3(0f, 0.70710678f, 0.70710678f);
            Beat01Geo.QuadUV(mb, new Vector3(x0, y, z1 - 0.02f), new Vector3(x1, y, z1 - 0.02f), new Vector3(x1, y - 0.02f, z1), new Vector3(x0, y - 0.02f, z1), n,
                new Vector2(x0, z1 - 0.02f), new Vector2(x1, z1 - 0.02f), new Vector2(x1, z1), new Vector2(x0, z1));
            mb.Color = saved;
            Beat01Geo.ZQuad(mb, z1, true, x0, x1, y - 0.2f, y - 0.02f);
        }

        // ------------------------------------------------------------------------------------------ beams, pilasters, haunches

        void BuildStructure(MeshBuilder mb, Material conc, bool simplified)
        {
            for (int k = 0; k < BEAM_COUNT; k++)
            {
                int first = mb.VertexCount;
                AddBeam(mb, BEAM_Z0 + BEAM_SPACING * k);
                if (k == 0) _beamDepth = BeatKit.VertexBounds(mb, first).size.y - BEAM_EMBED;
                _beamCount++;
            }
            VertexBake.EdgeWearFromNormals(mb);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryBeams", conc, _meshes, ShadowCastingMode.On));
            if (simplified) return;

            float hw = AbyssConst.GALLERY_W * 0.5f;
            for (int k = 0; k < PILASTER_PAIRS; k++)
            {
                float zc = PILASTER_Z0 + PILASTER_SPACING * k;
                float za = zc - PILASTER_W * 0.5f, zb = zc + PILASTER_W * 0.5f;
                // below any tread top in the footprint (tread tops sit up to one rise under the slope line) and above the ceiling
                float yBot = Mathf.Min(FloorY(za), FloorY(zb)) - AbyssConst.STAIR_RISE - EMBED;
                float yTop = Mathf.Max(CeilY(za), CeilY(zb)) + EMBED;
                for (int s = -1; s <= 1; s += 2)
                {
                    float xa = s * (hw - PILASTER_D), xb = s * (hw + EMBED);
                    Box(mb, Mathf.Min(xa, xb), Mathf.Max(xa, xb), yBot, yTop, za, zb, PILASTER_BEVEL);
                    _pilasterCount++;
                    if (!OnStairSlope(za) && !OnStairSlope(zb)) AddHaunch(mb, s, zc);
                }
            }
            VertexBake.EdgeWearFromNormals(mb);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryPilasters", conc, _meshes, ShadowCastingMode.On));
        }

        /// <summary>
        /// One ceiling beam across the gallery at zc: a 2.44 m extrusion (2 cm into each wall) of a section that hangs 0.25 under
        /// the local ceiling and reaches 2 cm into it, so over the stairs it shears with the sloped ceiling; 2 cm chamfers on the
        /// two bottom edges. Extrude along +X maps section x to world -z.
        /// </summary>
        void AddBeam(MeshBuilder mb, float zc)
        {
            float z0 = zc - BEAM_THICK * 0.5f, z1 = zc + BEAM_THICK * 0.5f;
            var a = new Vector2(z0, CeilY(z0) + BEAM_EMBED);
            var b = new Vector2(z1, CeilY(z1) + BEAM_EMBED);
            var c = new Vector2(z1, CeilY(z1) - BEAM_DEPTH);
            var d = new Vector2(z0, CeilY(z0) - BEAM_DEPTH);
            _sec.Clear(true);
            AddZY(zc, a);
            AddZY(zc, b);
            AddZY(zc, c + (b - c).normalized * BEAM_BEVEL);
            AddZY(zc, c + (d - c).normalized * BEAM_BEVEL);
            AddZY(zc, d + (c - d).normalized * BEAM_BEVEL);
            AddZY(zc, d + (a - d).normalized * BEAM_BEVEL);
            float hx = AbyssConst.GALLERY_W * 0.5f + EMBED;
            MeshKit.Extrude(mb, _sec, new Vector3(-hx, 0f, zc), new Vector3(hx, 0f, zc), Vector3.up, true);
        }

        void AddZY(float zc, Vector2 zy) => _sec.Add(new Vector2(zc - zy.x, zy.y), true);

        /// <summary>45-degree haunch (0.15) between a pilaster face and the frame beam above it, 1 cm into both, 5 mm inside the beam faces.</summary>
        void AddHaunch(MeshBuilder mb, float side, float zc)
        {
            float face = AbyssConst.GALLERY_W * 0.5f - PILASTER_D;
            float yb = CeilY(zc) - BEAM_DEPTH;
            _sec.Clear(true);
            _sec.Add(new Vector2(side * (face + 0.01f), yb + 0.01f), true);
            _sec.Add(new Vector2(side * (face + 0.01f), yb - HAUNCH), true);
            _sec.Add(new Vector2(side * (face - HAUNCH), yb + 0.01f), true);
            float h = BEAM_THICK * 0.5f - 0.005f;
            MeshKit.Extrude(mb, _sec, new Vector3(0f, 0f, zc - h), new Vector3(0f, 0f, zc + h), Vector3.up, true);
        }

        // ------------------------------------------------------------------------------------------ paint: floor line and nosings

        void BuildPaint(MeshBuilder mb, Material paint, uint seed)
        {
            float yTop = AbyssConst.W[1].y, yLow = AbyssConst.W[2].y;
            // the 0.10 m line on the left, clear of the pipe flanges, interrupted at the door leaf and bulkhead; worn gaps by hash
            int first = mb.VertexCount;
            AddLine(mb, 0.4f, 20.4f, yTop, seed * 7919u + 11u);
            Bounds lb = BeatKit.VertexBounds(mb, first);
            _lineWidth = lb.size.x;
            _lineOffset = lb.max.y - yTop;
            AddLine(mb, 22.3f, AbyssConst.STAIR_Z0 - 0.15f, yTop, seed * 7919u + 23u);
            AddLine(mb, AbyssConst.STAIR_Z1 + 0.17f, JAMB_Z0 - 0.15f, yLow, seed * 7919u + 37u);

            // painted nosings on the 16 stair edges (the upper floor edge and treads 0..14), worn through where feet land
            for (int k = -1; k < AbyssConst.STAIR_COUNT - 1; k++)
            {
                float zEdge = AbyssConst.STAIR_Z0 + AbyssConst.STAIR_RUN * (k + 1);
                float top = k < 0 ? yTop : _treadTop[k];
                float za = zEdge - 0.015f - NOSING_W, zb = zEdge - 0.015f;
                float h = Beat01Geo.Hash01(seed * 104729u + (uint)(k + 7) * 2654435761u);
                if (h < 0.55f)
                {
                    float g0 = -0.28f + 0.2f * h, g1 = 0.12f + 0.3f * Beat01Geo.Hash01((uint)(k + 101) * 40503u + seed);
                    PaintBox(mb, -1.18f, g0, za, zb, top);
                    PaintBox(mb, g1, 1.18f, za, zb, top);
                }
                else PaintBox(mb, -1.18f, 1.18f, za, zb, top);
                _nosingCount++;
            }
            AddNear(BeatKit.Emit(mb, _geo, "GalleryPaint", paint, _meshes, ShadowCastingMode.Off));
        }

        /// <summary>The floor line from z0 to z1 in 1 m cells; about one cell in six loses a 0.15-0.5 m piece of paint.</summary>
        void AddLine(MeshBuilder mb, float z0, float z1, float floorY, uint seed)
        {
            float x0 = LINE_X - LINE_W * 0.5f, x1 = LINE_X + LINE_W * 0.5f;
            float start = z0;
            int cells = Mathf.CeilToInt(z1 - z0);
            for (int i = 0; i < cells; i++)
            {
                float h = Beat01Geo.Hash01(seed + (uint)i * 2246822519u);
                if (h >= 0.17f) continue;
                float len = 0.15f + 0.35f * Beat01Geo.Hash01(seed ^ ((uint)i * 3266489917u + 1u));
                float g0 = z0 + i + 0.2f + 0.4f * h / 0.17f;
                float g1 = Mathf.Min(g0 + len, z1);
                if (g0 >= z1) break;
                if (g0 - start > 0.05f) PaintBox(mb, x0, x1, start, g0, floorY);
                start = Mathf.Max(start, g1);
            }
            if (z1 - start > 0.05f) PaintBox(mb, x0, x1, start, z1, floorY);
        }

        /// <summary>Paint strip: 12 mm thick, top 5 mm above the surface (no co-planar faces), 3 mm chamfer.</summary>
        static void PaintBox(MeshBuilder mb, float x0, float x1, float z0, float z1, float surfaceY)
        {
            if (x1 - x0 < 0.02f || z1 - z0 < 0.02f) return;
            Box(mb, x0, x1, surfaceY + LINE_OFFSET - PAINT_T, surfaceY + LINE_OFFSET, z0, z1, 0.003f);
        }

        // ------------------------------------------------------------------------------------------ drain (0, 0, 12)

        void BuildDrain(MeshBuilder mb, Material sump, Material grating, Material steel)
        {
            float y = AbyssConst.W[1].y, zc = AbyssConst.DRAIN_Z, hh = DRAIN_HOLE_HALF;
            // sump: four inward walls and a floor 0.3 m down (dark concrete), edges shared with the floor hole
            Beat01Geo.XQuad(mb, -hh, true, zc - hh, zc + hh, y - DRAIN_DEPTH, y);
            Beat01Geo.XQuad(mb, hh, false, zc - hh, zc + hh, y - DRAIN_DEPTH, y);
            Beat01Geo.ZQuad(mb, zc - hh, true, -hh, hh, y - DRAIN_DEPTH, y);
            Beat01Geo.ZQuad(mb, zc + hh, false, -hh, hh, y - DRAIN_DEPTH, y);
            Beat01Geo.FloorQuad(mb, -hh, hh, zc - hh, zc + hh, y - DRAIN_DEPTH);
            AddNear(BeatKit.Emit(mb, _geo, "DrainSump", sump, _meshes, ShadowCastingMode.Off));

            // steel frame: 30 mm angle, 3 mm proud, lapping 5 mm onto the floor around the hole
            float fi = DRAIN_HALF, fo = DRAIN_FRAME_OUT, ft = y + 0.003f, fb = y - 0.04f;
            Box(mb, -fo, fo, fb, ft, zc - fo, zc - fi, 0.004f);
            Box(mb, -fo, fo, fb, ft, zc + fi, zc + fo, 0.004f);
            Box(mb, -fo, -fi, fb, ft, zc - fi, zc + fi, 0.004f);
            Box(mb, fi, fo, fb, ft, zc - fi, zc + fi, 0.004f);
            AddNear(BeatKit.Emit(mb, _geo, "DrainFrame", steel, _meshes, ShadowCastingMode.Off));

            // grating 0.3 x 0.3, 12 mm under the frame top (analytic bars, alpha to coverage, Cull Off)
            Beat01Geo.FloorQuad(mb, -fi - 0.001f, fi + 0.001f, zc - fi - 0.001f, zc + fi + 0.001f, y - GRATING_DROP);
            AddNear(BeatKit.Emit(mb, _geo, "DrainGrating", grating, _meshes, ShadowCastingMode.Off));
        }

        // ------------------------------------------------------------------------------------------ trunk and fixtures

        static float HousingZ(int i)
        {
            float z = FixtureZ(i);
            // keep every housing on a flat ceiling (fixture 7 at z 45 would start 3 cm onto the stair slope)
            float lo = z - FIX_HOUSING_L * 0.5f;
            if (lo < AbyssConst.STAIR_Z1 + 0.05f && lo > AbyssConst.STAIR_Z0) z += AbyssConst.STAIR_Z1 + 0.05f - lo;
            return z;
        }

        void BuildTrunkAndFixtures(BeatContext ctx, MeshBuilder mb, Material steel)
        {
            // trunk along the ceiling centre: top 1.5 cm into the ceiling, perpendicular sections over the stairs (hides the
            // soffit's centre expansion joint and leads the eye to the doorway)
            FloorRun(0f, AbyssConst.GALLERY_H + TRUNK_TOP_EMBED - TRUNK_HALF_H, SHELL_START_Z - EMBED, JAMB_Z0 + EMBED);
            Beat01Geo.Fillet(_run, 0.25f, 6, _path);
            Beat01Geo.PerpFrames(_path, Vector3.up, _frames);
            float w = TRUNK_HALF_W, hh = TRUNK_HALF_H;
            _sec.Clear(true);
            _sec.Add(new Vector2(-w, -hh + 0.01f), true);
            _sec.Add(new Vector2(-w + 0.01f, -hh), true);
            _sec.Add(new Vector2(w - 0.01f, -hh), true);
            _sec.Add(new Vector2(w, -hh + 0.01f), true);
            _sec.Add(new Vector2(w, hh), true);
            _sec.Add(new Vector2(-w, hh), true);
            MeshKit.Loft(mb, _sec, _frames, true);

            // housings hang 1 cm into the trunk; the emissive lens is a separate renderer per fixture (wake ramp)
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                if (!_fixtureOn[i]) continue;
                float zc = HousingZ(i);
                float top = CeilY(zc) - FIX_HOUSING_TOP;
                MeshKit.ChamferBox(mb, new Vector3(0f, top - FIX_HOUSING_H * 0.5f, zc), new Vector3(FIX_HOUSING_W, FIX_HOUSING_H, FIX_HOUSING_L), 0.012f);
            }
            AddNear(BeatKit.Emit(mb, _geo, "GalleryTrunk", steel, _meshes, ShadowCastingMode.On));

            Color top0 = AbyssPalette.COLD_LAMP;
            Color edge = Srgb(AbyssPalette.COLD_LAMP.linear * FIX_LENS_EDGE_GAIN);
            Material lens = ctx.Mats.Variant(MatId.GradientCard, "galleryFixture", m =>
            {
                m.SetColor(ShaderIds.CardTop, top0);        // lens centre (UV0.y = 1)
                m.SetColor(ShaderIds.CardBottom, edge);     // lens edges (UV0.y = 0)
                m.SetFloat(BeatKit.CardModeId, 0f);
                m.SetFloat("_CardFog", 1f);
                m.SetFloat(ShaderIds.EmissionStrength, FIXTURE_EMISSION);
                m.SetFloat("_Cull", (float)CullMode.Back);
            });
            for (int i = 0; i < FIXTURE_COUNT; i++)
            {
                if (!_fixtureOn[i]) continue;
                float zc = HousingZ(i);
                float yb = CeilY(zc) - FIX_HOUSING_TOP - FIX_HOUSING_H;
                AddLens(mb, zc, yb);
                _fixtureR[i] = BeatKit.Emit(mb, _geo, "FixtureLens" + i, lens, _meshes, ShadowCastingMode.Off);
                _fixtureMpb[i] = new MaterialPropertyBlock();
            }
        }

        /// <summary>Lens segments along its length, and the ridge's gradient value at the two ends (R2-22: a diffuser, not a card).</summary>
        const int FIX_LENS_SEGMENTS = 8;
        const float FIX_LENS_END_UV = 0.18f;

        /// <summary>
        /// Gradient value along the lens ridge at t in [-1, 1] (0 = the lens centre): 1 at the centre falling smoothly
        /// (1 - t^2) to FIX_LENS_END_UV at the ends. With the edges at 0 (FIX_LENS_EDGE_GAIN x the centre colour) the lens is a
        /// pillow-shaped diffuser: brightest in its middle, about 0.3 of it (linear) at the ends of the ridge, 0.15 at the edges.
        /// </summary>
        static float LensRidgeUV(float t) => Mathf.Lerp(FIX_LENS_END_UV, 1f, 1f - t * t);

        /// <summary>
        /// Shallow prismatic lens under a housing whose bottom is at yb: two facets meeting 15 mm below the housing along the
        /// centre line, edges 3 mm inside the housing, in FIX_LENS_SEGMENTS strips along its length. UV0.y = LensRidgeUV on the
        /// ridge and 0 on the edges (GradientCard: brighter centre, darker edges and ends). The gradient is smooth (shared
        /// vertices per facet, no step), so it cannot alias at distance.
        /// </summary>
        static void AddLens(MeshBuilder mb, float zc, float yb)
        {
            float w = FIX_LENS_HALF_W, za = zc - FIX_LENS_L * 0.5f, zb = zc + FIX_LENS_L * 0.5f;
            float ye = yb + 0.003f, yr = yb - FIX_LENS_DROP;
            var nl = new Vector3(-(ye - yr), -w, 0f).normalized;
            var nr = new Vector3(ye - yr, -w, 0f).normalized;
            for (int side = 0; side < 2; side++)
            {
                float xe = side == 0 ? -w : w;
                Vector3 n = side == 0 ? nl : nr;
                int first = mb.VertexCount;
                for (int k = 0; k <= FIX_LENS_SEGMENTS; k++)
                {
                    float t = -1f + 2f * k / FIX_LENS_SEGMENTS;
                    float z = zc + 0.5f * FIX_LENS_L * t;
                    float u = (float)k / FIX_LENS_SEGMENTS;
                    mb.Vertex(new Vector3(xe, ye, z), n, new Vector2(u, 0f));
                    mb.Vertex(new Vector3(0f, yr, z), n, new Vector2(u, LensRidgeUV(t)));
                }
                for (int k = 0; k < FIX_LENS_SEGMENTS; k++)
                {
                    int e0 = first + 2 * k, r0 = e0 + 1, e1 = e0 + 2, r1 = e0 + 3;
                    // clockwise seen from below (normal = cross(b - a, c - a) points down and outward)
                    if (side == 0) { BeatKit.Tri(mb, e0, e1, r1); BeatKit.Tri(mb, e0, r1, r0); }
                    else { BeatKit.Tri(mb, e0, r0, r1); BeatKit.Tri(mb, e0, r1, e1); }
                }
            }
            float endUV = LensRidgeUV(1f);
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? za : zb;
                Vector3 n = e == 0 ? Vector3.back : Vector3.forward;
                int i0 = mb.Vertex(new Vector3(-w, ye, z), n, new Vector2(0f, 0f));
                int i1 = mb.Vertex(new Vector3(0f, yr, z), n, new Vector2(0.5f, endUV));
                int i2 = mb.Vertex(new Vector3(w, ye, z), n, new Vector2(1f, 0f));
                BeatKit.Tri(mb, i0, i1, i2);
            }
        }

        // ------------------------------------------------------------------------------------------ pipe, flanges, supports, bolts

        static Vector3 PipeCentre(float z) => new Vector3(PIPE_X, FloorY(z) + PIPE_Y, z);

        void BuildPipe(MeshBuilder mb, Material pipeMat, Material steel, bool simplified, uint seed)
        {
            FloorRun(PIPE_X, PIPE_Y, SHELL_START_Z - EMBED, JAMB_Z0 + EMBED);
            Beat01Geo.Fillet(_run, PIPE_ELBOW_R, 6, _path);
            Beat01Geo.Tube(mb, _path, PIPE_R, PIPE_SEGMENTS, false, Vector3.up);
            _pipeDiameter = BeatKit.VertexBounds(mb, 0).size.x;

            // weld-neck flange pairs: neck cone out of the pipe, two 25 mm flanges with a gasket groove, 4 mm chamfers
            int flangeFirst = mb.VertexCount;
            var prof = new List<Vector2>
            {
                new Vector2(0.088f, -0.065f), new Vector2(0.110f, -FLANGE_FACE), new Vector2(0.146f, -FLANGE_FACE), new Vector2(0.150f, -0.024f),
                new Vector2(0.150f, -0.003f), new Vector2(0.145f, 0f), new Vector2(0.150f, 0.003f), new Vector2(0.150f, 0.024f),
                new Vector2(0.146f, FLANGE_FACE), new Vector2(0.110f, FLANGE_FACE), new Vector2(0.088f, 0.065f), new Vector2(0.088f, -0.065f),
            };
            for (int k = 0; k < FLANGE_COUNT; k++)
            {
                float z = FLANGE_Z0 + FLANGE_SPACING * k;
                mb.Push(PipeCentre(z), Quaternion.FromToRotation(Vector3.up, RunTangent(z)));
                MeshKit.Lathe(mb, prof, PIPE_SEGMENTS, 40f);
                mb.Pop();
                _flangeCount++;
            }
            // sleeve where the pipe passes the bulkhead plate
            MeshKit.Cylinder(mb, new Vector3(PIPE_X, PIPE_Y, 21.88f), new Vector3(PIPE_X, PIPE_Y, 22.12f), 0.125f, PIPE_SEGMENTS, true);
            VertexBake.EdgeWearFromNormals(mb, 20f, 50f, 1e-4f, flangeFirst);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryPipe", pipeMat, _meshes, ShadowCastingMode.On));

            // supports between the flanges: wall arm under the pipe and a clamp band around it
            var band = new List<Vector2>
            {
                new Vector2(0.086f, -0.015f), new Vector2(0.097f, -0.015f), new Vector2(0.097f, 0.015f), new Vector2(0.086f, 0.015f), new Vector2(0.086f, -0.015f),
            };
            for (int k = 0; k < FLANGE_COUNT; k++)
            {
                float z = SUPPORT_Z0 + SUPPORT_SPACING * k;
                Vector3 c = PipeCentre(z);
                float ya = c.y - PIPE_R - 0.019f;
                MeshKit.ChamferMember(mb, new Vector3(-AbyssConst.GALLERY_W * 0.5f - EMBED, ya, z), new Vector3(PIPE_X + 0.05f, ya, z), 0.06f, 0.044f, 0.006f, Vector3.up);
                mb.Push(c, Quaternion.FromToRotation(Vector3.up, RunTangent(z)));
                MeshKit.Lathe(mb, band, 16, 40f);
                mb.Pop();
            }
            AddNear(BeatKit.Emit(mb, _geo, "GalleryPipeSupports", steel, _meshes, ShadowCastingMode.On));

            if (simplified) return;
            // 160 instanced hex bolts (8 per flange pair) on the upstream flange face, each turned by a hash
            MeshKit.HexBolt(mb, Vector3.zero, Vector3.forward, BOLT_AF);
            Mesh bolt = BeatKit.Bake(mb, "GalleryHexBolt", _meshes);
            _bolts = new InstancedBatch("GalleryFlangeBolts", bolt, steel, ShadowCastingMode.Off, true, 0, 200f);
            for (int k = 0; k < FLANGE_COUNT; k++)
            {
                float z = FLANGE_Z0 + FLANGE_SPACING * k;
                Vector3 t = RunTangent(z), c = PipeCentre(z);
                Vector3 u = Beat01Geo.Perp(t, Vector3.up), v = Vector3.Cross(t, u), n = -t;
                Quaternion face = Quaternion.FromToRotation(Vector3.forward, n);
                for (int j = 0; j < BOLTS_PER_FLANGE; j++)
                {
                    float a = (22.5f + 45f * j) * Mathf.Deg2Rad;
                    Vector3 p = c - t * FLANGE_FACE + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * BOLT_CIRCLE_R;
                    float spin = 60f * Beat01Geo.Hash01(seed * 31u + (uint)(k * BOLTS_PER_FLANGE + j) * 747796405u + 17u);
                    _bolts.Add(Matrix4x4.TRS(p, Quaternion.AngleAxis(spin, n) * face, Vector3.one));
                }
            }
            _bolts.Seal();
            InstancedRegistry.Register(_bolts);
        }

        // ------------------------------------------------------------------------------------------ cable tray and cables

        static float TrayBottomY(float z) => FloorY(z) + TRAY_Y - VerticalHalf(z, TRAY_HALF_H);

        void BuildTray(MeshBuilder mb, Material steel, Material cableMat)
        {
            // U tray (8 mm sheet), perpendicular sections: it tilts with the stair slope like a real tray
            float y0 = -TRAY_HALF_H, y1 = TRAY_HALF_H, t = TRAY_T;
            _sec.Clear(true);
            _sec.Add(new Vector2(TRAY_X0, y0), true);
            _sec.Add(new Vector2(TRAY_X1, y0), true);
            _sec.Add(new Vector2(TRAY_X1, y1), true);
            _sec.Add(new Vector2(TRAY_X1 - t, y1), true);
            _sec.Add(new Vector2(TRAY_X1 - t, y0 + t), true);
            _sec.Add(new Vector2(TRAY_X0 + t, y0 + t), true);
            _sec.Add(new Vector2(TRAY_X0 + t, y1), true);
            _sec.Add(new Vector2(TRAY_X0, y1), true);
            int first = mb.VertexCount;
            FloorRun(0f, TRAY_Y, SHELL_START_Z - EMBED, TRAY_GAP0);
            Beat01Geo.Fillet(_run, TRAY_BEND_R, 6, _path);
            Beat01Geo.PerpFrames(_path, Vector3.up, _frames);
            MeshKit.Loft(mb, _sec, _frames, true);   // capped: the ends at the bulkhead gap are seen
            _trayY = BeatKit.VertexBounds(mb, first).center.y;
            FloorRun(0f, TRAY_Y, TRAY_GAP1, JAMB_Z0 + EMBED);
            Beat01Geo.Fillet(_run, TRAY_BEND_R, 6, _path);
            Beat01Geo.PerpFrames(_path, Vector3.up, _frames);
            MeshKit.Loft(mb, _sec, _frames, true);
            // KS1 subject: the foreground run the frame shows whole (from 1.5 m ahead of the KS1 eye to z 16; nearer, the
            // tray passes over the right and top frame edges and a box reaching the near plane projects off screen)
            _trayNearBounds = new Bounds();
            _trayNearBounds.SetMinMax(new Vector3(TRAY_X0, TRAY_Y - TRAY_HALF_H, KS1_EYE_Z + KS1_FRAME_DZ),
                new Vector3(TRAY_X1, TRAY_Y + TRAY_HALF_H, KS1_FOREGROUND_Z1));

            // cantilever brackets every 1.5 m (between the pilasters): arm under the tray and a diagonal strut to the wall
            float wall = AbyssConst.GALLERY_W * 0.5f;
            for (int k = 0; ; k++)
            {
                float z = TRAY_BRACKET_Z0 + TRAY_BRACKET_SPACING * k;
                if (z > JAMB_Z0 - 0.3f) break;
                if (z > TRAY_GAP0 - 0.1f && z < TRAY_GAP1 + 0.1f) continue;
                float ya = TrayBottomY(z) - 0.018f;
                MeshKit.ChamferMember(mb, new Vector3(wall + EMBED, ya, z), new Vector3(TRAY_X0 - 0.02f, ya, z), 0.04f, 0.04f, 0.006f, Vector3.up);
                MeshKit.ChamferMember(mb, new Vector3(wall + 0.01f, ya - 0.24f, z), new Vector3(TRAY_X0 + 0.12f, ya - 0.012f, z), 0.03f, 0.03f, 0.005f, Vector3.forward);
            }

            // sealed transit frames where the cables cross the bulkhead plate
            float fx0 = TRAY_X0 + 0.015f, fx1 = TRAY_X1 - 0.015f, fy0 = TRAY_Y - 0.035f, fy1 = TRAY_Y + 0.035f, fz0 = 21.93f, fz1 = 22.07f;
            Box(mb, fx0, fx1, fy0, fy0 + 0.012f, fz0, fz1, 0.003f);
            Box(mb, fx0, fx1, fy1 - 0.012f, fy1, fz0, fz1, 0.003f);
            Box(mb, fx0, fx0 + 0.012f, fy0, fy1, fz0, fz1, 0.003f);
            Box(mb, fx1 - 0.012f, fx1, fy0, fy1, fz0, fz1, 0.003f);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryTray", steel, _meshes, ShadowCastingMode.On));

            // three cables O 0.04 resting on the tray bottom from wall to wall (ThinLine), through the rubber transit block
            for (int c = 0; c < CABLE_X.Length; c++)
            {
                int cf = mb.VertexCount;
                FloorRun(CABLE_X[c], TRAY_Y - TRAY_HALF_H + TRAY_T + CABLE_R + 0.0005f, SHELL_START_Z - EMBED, JAMB_Z0 + EMBED);
                Beat01Geo.Fillet(_run, TRAY_BEND_R, 6, _path);
                Beat01Geo.Tube(mb, _path, CABLE_R, 8, true, Vector3.up);
                if (c == 0) _cableDiameter = BeatKit.VertexBounds(mb, cf).size.x;
                _cableCount++;
            }
            Box(mb, fx0 + 0.009f, fx1 - 0.009f, fy0 + 0.009f, fy1 - 0.009f, fz0 + 0.01f, fz1 - 0.01f, 0.004f);
            AddNear(BeatKit.Emit(mb, _geo, "GalleryCables", cableMat, _meshes, ShadowCastingMode.Off));
        }

        // ------------------------------------------------------------------------------------------ pressure door (z = 22)

        void BuildBulkhead(MeshBuilder mb, Material steel, Material leafMat)
        {
            float pz = AbyssConst.PRESSURE_DOOR_Z, pd = AbyssConst.PRESSURE_DOOR_W * 0.5f, ph = AbyssConst.PRESSURE_DOOR_H;
            float wall = AbyssConst.GALLERY_W * 0.5f + EMBED, ceil = AbyssConst.GALLERY_H + EMBED, cw = COAMING_W;
            float hz = BULKHEAD_T * 0.5f;
            int first = mb.VertexCount;

            // coaming: the 0.20 deep frame whose inner faces are the 1.1 x 2.0 opening (the colliders' edges)
            int c0 = mb.VertexCount;
            Box(mb, -pd - cw, -pd, -0.01f, ph + cw, pz - hz, pz + hz, 0.012f);
            Bounds cl = BeatKit.VertexBounds(mb, c0);
            c0 = mb.VertexCount;
            Box(mb, pd, pd + cw, -0.01f, ph + cw, pz - hz, pz + hz, 0.012f);
            Bounds cr = BeatKit.VertexBounds(mb, c0);
            c0 = mb.VertexCount;
            Box(mb, -pd - cw, pd + cw, ph, ph + cw, pz - hz, pz + hz, 0.012f);
            Bounds ch = BeatKit.VertexBounds(mb, c0);
            _pdWidth = cr.min.x - cl.max.x;
            _pdHeight = ch.min.y - AbyssConst.W[0].y;

            // plate (30 mm) into walls, floor and ceiling, 1 cm into the coaming
            float pt = PLATE_T * 0.5f, po = pd + cw - 0.01f;
            Box(mb, -wall, -po, -0.02f, ceil, pz - pt, pz + pt, 0.005f);
            Box(mb, po, wall, -0.02f, ceil, pz - pt, pz + pt, 0.005f);
            Box(mb, -po, po, ph + cw - 0.01f, ceil, pz - pt, pz + pt, 0.005f);

            // stiffeners on both faces: verticals at +/-0.76, rails at y 1.0 on the side panels, a header rail at y 2.29
            for (int s = -1; s <= 1; s += 2)
            {
                float za = s < 0 ? pz - pt - STIFF_D : pz + pt, zb = za + STIFF_D;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * STIFF_X;
                    Box(mb, x - STIFF_T * 0.5f, x + STIFF_T * 0.5f, -0.02f, ceil, za, zb, 0.003f);
                    float xi = side * (pd + cw - 0.005f), xo = side * wall;
                    Box(mb, Mathf.Min(xi, xo), Mathf.Max(xi, xo), 1.0f - STIFF_T * 0.5f, 1.0f + STIFF_T * 0.5f, za, zb, 0.003f);
                }
                Box(mb, -wall, wall, 2.29f - STIFF_T * 0.5f, 2.29f + STIFF_T * 0.5f, za, zb, 0.003f);
            }

            // dogs on the approach side: bosses on the coaming with levers swung open (down on the jamb, up on the header)
            float zf = pz - hz;
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = i < 3 ? new Vector3(pd + cw * 0.5f, 0.4f + 0.6f * i, zf) : new Vector3(i == 3 ? -0.25f : 0.25f, ph + cw * 0.5f, zf);
                Vector3 lever = i < 3 ? new Vector3(0f, -0.085f, 0f) : new Vector3(0f, 0.085f, 0f);
                MeshKit.Cylinder(mb, p + new Vector3(0f, 0f, 0.004f), p + new Vector3(0f, 0f, -0.035f), 0.02f, 12, true);
                MeshKit.ChamferMember(mb, p + new Vector3(0f, 0f, -0.04f), p + lever + new Vector3(0f, 0f, -0.04f), 0.022f, 0.016f, 0.004f, Vector3.forward);
            }

            // hinge brackets (welded to the plate) and knuckles on the hinge axis
            for (int i = 0; i < 2; i++)
            {
                float y0 = i == 0 ? 0.30f : 1.62f;
                Box(mb, LEAF_HINGE.x - 0.05f, -pd - cw + 0.005f, y0 + 0.02f, y0 + 0.14f, LEAF_HINGE.z + 0.022f, pz - pt + 0.005f, 0.006f);
                MeshKit.Cylinder(mb, new Vector3(LEAF_HINGE.x, y0, LEAF_HINGE.z), new Vector3(LEAF_HINGE.x, y0 + 0.16f, LEAF_HINGE.z), 0.028f, 12, true);
            }

            // steel saddle across the opening, 14 mm proud of the floor
            Box(mb, -pd - 0.002f, pd + 0.002f, -0.01f, 0.014f, pz - 0.13f, pz + 0.13f, 0.006f);

            // handwheel on the corridor face of the open leaf (spindle, hub, 4 spokes, torus rim with worn bare-steel grip)
            Quaternion open = Quaternion.Euler(0f, LEAF_OPEN_DEG, 0f);
            mb.Push(LEAF_HINGE, open);
            var w = new Vector3(0.68f, 1.05f, 0.165f);
            MeshKit.Cylinder(mb, new Vector3(w.x, w.y, 0.04f), new Vector3(w.x, w.y, 0.15f), 0.016f, 12, true);
            MeshKit.Cylinder(mb, new Vector3(w.x, w.y, 0.14f), new Vector3(w.x, w.y, 0.19f), 0.035f, 16, true);
            for (int s = 0; s < 4; s++)
            {
                float a = (45f + 90f * s) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                MeshKit.ChamferMember(mb, w + dir * 0.03f, w + dir * (WHEEL_R - 0.004f), 0.022f, 0.014f, 0.004f, Vector3.forward);
            }
            Color32 saved = mb.Color;
            mb.Color = new Color32(saved.r, 210, saved.b, saved.a);
            int wheelFirst = mb.VertexCount;
            MeshKit.Torus(mb, w, Vector3.forward, WHEEL_R, WHEEL_TUBE, 32, 10);
            mb.Color = saved;
            mb.Pop();
            Bounds wb = BeatKit.VertexBounds(mb, wheelFirst);
            _wheelDiameter = Mathf.Max(wb.size.y, Mathf.Max(wb.size.x, wb.size.z));
            _pressureDoorBounds = BeatKit.VertexBounds(mb, first);
            AddNear(BeatKit.Emit(mb, _geo, "PressureBulkhead", steel, _meshes, ShadowCastingMode.On));

            // the leaf: 90 mm plate with raised panels on both faces and two hinge straps, open 92 degrees toward the approach
            mb.Push(LEAF_HINGE, open);
            MeshKit.ChamferBox(mb, new Vector3(0.68f, 1.041f, 0f), new Vector3(1.24f, 2.058f, 0.09f), 0.02f);
            MeshKit.ChamferBox(mb, new Vector3(0.68f, 1.041f, 0.05f), new Vector3(1.0f, 1.82f, 0.02f), 0.006f);
            MeshKit.ChamferBox(mb, new Vector3(0.68f, 1.041f, -0.05f), new Vector3(1.0f, 1.82f, 0.02f), 0.006f);
            for (int i = 0; i < 2; i++)
            {
                float y0 = i == 0 ? 0.30f : 1.62f;
                MeshKit.ChamferBox(mb, new Vector3(0.12f, y0 + 0.08f, 0f), new Vector3(0.24f, 0.12f, 0.11f), 0.008f);
            }
            mb.Pop();
            AddNear(BeatKit.Emit(mb, _geo, "PressureDoorLeaf", leafMat, _meshes, ShadowCastingMode.On));
        }

        // ------------------------------------------------------------------------------------------ wall fan (z = 30, left)

        void BuildWallFan(MeshBuilder mb, Material steel, Material bladeMat)
        {
            Vector3 o = FAN_MOTOR, a = FAN_AXIS;
            Vector3 u = Beat01Geo.Perp(a, Vector3.up), v = Vector3.Cross(a, u);
            Vector3 disc = o + a * 0.11f;
            float wall = AbyssConst.GALLERY_W * 0.5f;
            int first = mb.VertexCount;

            // wall plate, arm, motor
            Box(mb, -wall - 0.005f, -wall + 0.025f, FAN_Y - 0.12f, FAN_Y + 0.12f, FAN_Z - 0.09f, FAN_Z + 0.09f, 0.006f);
            MeshKit.ChamferMember(mb, new Vector3(-wall + 0.02f, FAN_Y, FAN_Z), o - a * 0.05f, 0.05f, 0.05f, 0.008f, Vector3.up);
            MeshKit.Cylinder(mb, o - a * 0.09f, o + a * 0.07f, 0.08f, 20, true);

            // shroud ring (1.2 cm clear of the blade tips) and three struts behind the blades
            var ring = new List<Vector2>
            {
                new Vector2(0.262f, -0.045f), new Vector2(0.278f, -0.045f), new Vector2(0.282f, -0.041f), new Vector2(0.282f, 0.041f),
                new Vector2(0.278f, 0.045f), new Vector2(0.262f, 0.045f), new Vector2(0.262f, -0.045f),
            };
            mb.Push(disc, Quaternion.FromToRotation(Vector3.up, a));
            MeshKit.Lathe(mb, ring, 48, 40f);
            mb.Pop();
            for (int s = 0; s < 3; s++)
            {
                float ang = (90f + 120f * s) * Mathf.Deg2Rad;
                Vector3 e = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                Vector3 back = disc - a * 0.05f;
                MeshKit.ChamferMember(mb, back + e * 0.07f, back + e * 0.272f, 0.016f, 0.03f, 0.004f, a);
            }
            VertexBake.EdgeWearFromNormals(mb, 20f, 50f, 1e-4f, first);
            _fanBounds = BeatKit.VertexBounds(mb, first);
            AddNear(BeatKit.Emit(mb, _geo, "WallFan", steel, _meshes, ShadowCastingMode.On));

            // rotor (local +Z = fan axis): hub spinner and 6 twisted blades (chord 85 -> 120 mm, pitch 34 -> 17 degrees)
            _fanRotor = BeatKit.Group(_geo, "WallFanRotor", disc, Quaternion.LookRotation(a, u));
            _fanBase = _fanRotor.localRotation;
            var hub = new List<Vector2>
            {
                new Vector2(0f, -0.045f), new Vector2(0.05f, -0.045f), new Vector2(0.05f, 0.015f), new Vector2(0.042f, 0.04f),
                new Vector2(0.025f, 0.058f), new Vector2(0f, 0.065f),
            };
            mb.Push(Vector3.zero, Quaternion.FromToRotation(Vector3.up, Vector3.forward));
            MeshKit.Lathe(mb, hub, 20, 40f);
            mb.Pop();
            const int stations = 5;
            CrossSection blade = _sec;
            for (int b = 0; b < FAN_BLADES; b++)
            {
                float psi = 2f * Mathf.PI * b / FAN_BLADES;
                var radial = new Vector3(Mathf.Cos(psi), Mathf.Sin(psi), 0f);
                var tang = new Vector3(-Mathf.Sin(psi), Mathf.Cos(psi), 0f);
                _frames.Clear();
                for (int i = 0; i < stations; i++)
                {
                    float s = i / (stations - 1f);
                    float beta = Mathf.Lerp(34f, 17f, s) * Mathf.Deg2Rad;
                    Vector3 n = -Mathf.Sin(beta) * tang + Mathf.Cos(beta) * Vector3.forward;
                    _frames.Add(new LoftFrame(radial * Mathf.Lerp(0.045f, FAN_TIP_R, s), Quaternion.LookRotation(radial, n)));
                }
                MeshKit.LoftVarying(mb, i => MeshKit.Airfoil(Mathf.Lerp(0.085f, 0.12f, i / (stations - 1f)), Mathf.Lerp(0.010f, 0.005f, i / (stations - 1f)), 4, blade),
                    _frames, true);
            }
            _fanDiameter = 2f * FAN_TIP_R;
            MeshRenderer rotor = BeatKit.Emit(mb, _fanRotor, "WallFanBlades", bladeMat, _meshes, ShadowCastingMode.On);
            AddNear(rotor);
            if (rotor != null) _fanBounds.Encapsulate(new Bounds(disc, new Vector3(0.6f, 0.6f, 0.6f)));
            SetFanAngle(0.0);
        }

        // ------------------------------------------------------------------------------------------ stair handrail (right wall)

        void BuildHandrail(MeshBuilder mb, Material rail, Material steel)
        {
            float wall = AbyssConst.GALLERY_W * 0.5f, y0 = AbyssConst.W[1].y + RAIL_H, y1 = AbyssConst.W[2].y + RAIL_H;
            float z0 = AbyssConst.STAIR_Z0, z1 = AbyssConst.STAIR_Z1;
            _run.Clear();
            _run.Add(new Vector3(wall + 0.01f, y0, z0 - 0.62f));
            _run.Add(new Vector3(RAIL_X, y0, z0 - 0.45f));
            _run.Add(new Vector3(RAIL_X, y0, z0));
            _run.Add(new Vector3(RAIL_X, y1, z1));
            _run.Add(new Vector3(RAIL_X, y1, z1 + 0.45f));
            _run.Add(new Vector3(wall + 0.01f, y1, z1 + 0.62f));
            Beat01Geo.Fillet(_run, 0.12f, 6, _path);
            Beat01Geo.Tube(mb, _path, RAIL_R, 16, false, Vector3.up);
            AddNear(BeatKit.Emit(mb, _geo, "StairHandrail", rail, _meshes, ShadowCastingMode.On));

            // brackets: wall rosette, arm, saddle post (clear of the stair pilaster at z 43.3-43.7)
            float[] zs = { z0 - 0.2f, 41.2f, 42.6f, 44.1f, z1 + 0.3f };
            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];
                float yr = FloorY(z) + RAIL_H, ya = yr - 0.09f;
                MeshKit.Cylinder(mb, new Vector3(wall + 0.005f, ya, z), new Vector3(wall - 0.012f, ya, z), 0.03f, 16, true);
                MeshKit.ChamferMember(mb, new Vector3(wall - 0.005f, ya, z), new Vector3(RAIL_X - 0.008f, ya, z), 0.016f, 0.016f, 0.003f, Vector3.up);
                MeshKit.ChamferMember(mb, new Vector3(RAIL_X, ya - 0.008f, z), new Vector3(RAIL_X, yr - RAIL_R + 0.004f, z), 0.016f, 0.016f, 0.003f, Vector3.forward);
            }
            AddNear(BeatKit.Emit(mb, _geo, "StairHandrailBrackets", steel, _meshes, ShadowCastingMode.Off));
        }

        // ------------------------------------------------------------------------------------------ closed door in the start wall

        void BuildStartDoor(MeshBuilder mb, Material steel, Material leafMat)
        {
            float z = SHELL_START_Z;
            // frame: 70 mm steel jambs and head, 45 mm proud (still 5 cm behind the back collider face)
            Box(mb, -0.57f, -0.50f, -0.01f, 2.12f, z - 0.02f, z + 0.045f, 0.008f);
            Box(mb, 0.50f, 0.57f, -0.01f, 2.12f, z - 0.02f, z + 0.045f, 0.008f);
            Box(mb, -0.57f, 0.57f, 2.05f, 2.12f, z - 0.02f, z + 0.045f, 0.008f);
            // lever handle
            MeshKit.Cylinder(mb, new Vector3(0.38f, 1.0f, z + 0.03f), new Vector3(0.38f, 1.0f, z + 0.058f), 0.022f, 12, true);
            MeshKit.ChamferMember(mb, new Vector3(0.38f, 1.0f, z + 0.062f), new Vector3(0.25f, 1.0f, z + 0.062f), 0.02f, 0.02f, 0.004f, Vector3.up);
            AddNear(BeatKit.Emit(mb, _geo, "StartDoorFrame", steel, _meshes, ShadowCastingMode.On));
            // leaf with a raised panel
            Box(mb, -0.50f, 0.50f, 0.008f, 2.05f, z - 0.02f, z + 0.022f, 0.01f);
            Box(mb, -0.40f, 0.40f, 0.12f, 1.93f, z + 0.015f, z + 0.032f, 0.005f);
            AddNear(BeatKit.Emit(mb, _geo, "StartDoorLeaf", leafMat, _meshes, ShadowCastingMode.On));
        }

        // ------------------------------------------------------------------------------------------ stair egress markers

        /// <summary>
        /// Always-on egress markers (small cold lenses in steel housings, 0.3 m above the floor on both walls) at the top and the
        /// bottom of the stairs. They are emissive accents only (no Light): the fixtures past 20 m are still asleep at KS1, and
        /// from the start the stair's sloped ceiling hides the whole lower section, so this pair is what marks the drop in the
        /// far layer. A min-pixel Glow keeps each one a stable >= 2.5 px point at 32 m.
        /// </summary>
        void BuildStairMarkers(BeatContext ctx, MeshBuilder mb, Material steel)
        {
            float wall = AbyssConst.GALLERY_W * 0.5f;
            float[] zs = { AbyssConst.STAIR_Z0 - MARKER_SETBACK, AbyssConst.STAIR_Z1 + MARKER_SETBACK };
            for (int i = 0; i < zs.Length; i++)
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zs[i], y = FloorY(z) + MARKER_Y;
                float xa = s * (wall + 0.015f), xb = s * (wall - 0.03f);
                Box(mb, Mathf.Min(xa, xb), Mathf.Max(xa, xb), y - 0.045f, y + 0.045f, z - 0.08f, z + 0.08f, 0.006f);
                if (i == 0)   // the top pair belongs to the stair head KS1 sees
                {
                    _stairHeadBounds.Encapsulate(new Vector3(Mathf.Min(xa, xb), y - 0.045f, z - 0.08f));
                    _stairHeadBounds.Encapsulate(new Vector3(Mathf.Max(xa, xb), y + 0.045f, z + 0.08f));
                }
            }
            AddNear(BeatKit.Emit(mb, _geo, "StairMarkerHousings", steel, _meshes, ShadowCastingMode.Off));

            Material lens = ctx.Mats.Variant(MatId.LampCold, "galleryMarker", m => m.SetFloat(ShaderIds.EmissionStrength, MARKER_EMISSION));
            for (int i = 0; i < zs.Length; i++)
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zs[i], y = FloorY(z) + MARKER_Y, x = s * (wall - 0.032f);
                Vector3 n = s > 0 ? Vector3.left : Vector3.right;
                Beat01Geo.QuadUV(mb, new Vector3(x, y - 0.02f, z - 0.05f), new Vector3(x, y + 0.02f, z - 0.05f), new Vector3(x, y + 0.02f, z + 0.05f),
                    new Vector3(x, y - 0.02f, z + 0.05f), n, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            }
            AddNear(BeatKit.Emit(mb, _geo, "StairMarkerLenses", lens, _meshes, ShadowCastingMode.Off));

            Mesh quad = Beat01Geo.GlowQuad(mb, "GalleryMarkerGlowQuad", _meshes);
            Material glow = ctx.Mats.Variant(MatId.Glow, "galleryMarker", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, MARKER_GLOW);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.SetFloat("_EnergyConserve", 0f);
                m.SetFloat("_Ignite", 0f);
            });
            for (int i = 0; i < zs.Length; i++)
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zs[i], y = FloorY(z) + MARKER_Y;
                MeshRenderer g = BeatKit.Renderer(_geo, "StairMarkerGlow" + i + (s < 0 ? "L" : "R"), quad, glow, new Vector3(s * (wall - 0.05f), y, z),
                    Quaternion.identity, ShadowCastingMode.Off);
                g.transform.localScale = new Vector3(0.10f, 0.10f, 0.10f);
                g.receiveShadows = false;
                AddNear(g);
            }
        }

        // ------------------------------------------------------------------------------------------ exit: architrave, amber lamp

        void BuildExitLamp(BeatContext ctx, MeshBuilder mb, Material steel)
        {
            float yLow = AbyssConst.W[2].y, zw = JAMB_Z0;
            float dw = AbyssConst.EXIT_DOOR_W * 0.5f, dh = AbyssConst.EXIT_DOOR_H;
            // steel architrave on the end wall around the opening (5 mm back from the reveal edges, 12 mm proud)
            Box(mb, -dw - 0.065f, -dw - 0.005f, yLow - 0.01f, yLow + dh + 0.065f, zw - 0.012f, zw + 0.01f, 0.004f);
            Box(mb, dw + 0.005f, dw + 0.065f, yLow - 0.01f, yLow + dh + 0.065f, zw - 0.012f, zw + 0.01f, 0.004f);
            Box(mb, -dw - 0.065f, dw + 0.065f, yLow + dh + 0.005f, yLow + dh + 0.065f, zw - 0.012f, zw + 0.01f, 0.004f);
            // well-glass lamp over the door: base plate and visor
            float yc = yLow + AbyssConst.GALLERY_H - 0.275f;
            Box(mb, -0.11f, 0.11f, yc - 0.065f, yc + 0.085f, zw - 0.0425f, zw + 0.0025f, 0.008f);
            Box(mb, -0.10f, 0.10f, yc + 0.063f, yc + 0.077f, zw - 0.14f, zw - 0.04f, 0.004f);
            AddNear(BeatKit.Emit(mb, _geo, "ExitLampHousing", steel, _meshes, ShadowCastingMode.On));

            // amber dome bulging EXIT_DOME_R toward the gallery. Lead decision D4 (R2-21): the near lamp head keeps M_Lamp's
            // AMBER x 8.0 only in a small white-hot core, with a saturated amber falloff to the rim (f03 B1_end: a flat
            // cream-white disc, the R2-21 defect). GradientCard emitter, UV0.y = DomeCoreWeight of the projected radius from
            // the dome axis (the gallery looks at it along the axis); the profile is dense near the apex so the core is smooth.
            var dome = new List<Vector2>(EXIT_DOME_RINGS + 1);
            for (int i = 0; i <= EXIT_DOME_RINGS; i++)
            {
                float t = 0.5f * Mathf.PI * Mathf.Sqrt((float)i / EXIT_DOME_RINGS);   // rings crowd toward the apex (t = 90)
                dome.Add(new Vector2(EXIT_DOME_R * Mathf.Cos(t), EXIT_DOME_R * Mathf.Sin(t)));
            }
            Vector3 domeCentre = new Vector3(0f, yc, zw - 0.04f);
            int domeFirst = mb.VertexCount;
            mb.Push(domeCentre, Quaternion.FromToRotation(Vector3.up, Vector3.back));
            MeshKit.Lathe(mb, dome, 24, 40f);
            mb.Pop();
            for (int i = domeFirst; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.Positions[i] - domeCentre;
                float rho = Mathf.Sqrt(p.x * p.x + p.y * p.y) / EXIT_DOME_R;
                mb.UV0[i] = new Vector2(mb.UV0[i].x, DomeCoreWeight(rho));
            }
            Material domeMat = ctx.Mats.Variant(MatId.GradientCard, "galleryExitDome", m =>
            {
                m.SetColor(ShaderIds.CardTop, AbyssPalette.AMBER);
                Color limb = EXIT_DOME_LIMB_SRGB.linear * (EXIT_DOME_LIMB_GAIN / AbyssPalette.AMBER_EMISSIVE);
                limb = limb.gamma;
                limb.a = 1f;
                m.SetColor(ShaderIds.CardBottom, limb);
                m.SetFloat(BeatKit.CardModeId, 0f);
                m.SetFloat("_CardFog", 1f);
                m.SetFloat(ShaderIds.EmissionStrength, AbyssPalette.AMBER_EMISSIVE);
                m.SetFloat("_Cull", (float)CullMode.Back);
            });
            _exitLamp = AddNear(BeatKit.Emit(mb, _geo, "ExitLamp", domeMat, _meshes, ShadowCastingMode.Off));
            // its amber point stands EXIT_LAMP_LIGHT_OFFSET in front of the dome's apex, never against the mount
            _exitLightPos = new Vector3(0f, yc, zw - 0.04f - EXIT_DOME_R - EXIT_LAMP_LIGHT_OFFSET);

            // a small amber glow so the lamp keeps a >= 2.5 px point from the stairs (_MinPixels). Its true diameter is the
            // dome's (R1-18: the f02 0.22 m halo spread warm glare over about 1 % of B1_end at 1.2 m); close up the dome and
            // its bloom are the lamp.
            Mesh quad = Beat01Geo.GlowQuad(mb, "GalleryAmberGlowQuad", _meshes);
            Material glow = ctx.Mats.Variant(MatId.Glow, "galleryAmber", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.AMBER.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, 1.2f);
                m.SetFloat(ShaderIds.MinPixels, 2.5f);
                m.SetFloat("_EnergyConserve", 0f);
                m.SetFloat("_Ignite", 0f);
            });
            MeshRenderer g = BeatKit.Renderer(_geo, "ExitLampGlow", quad, glow, new Vector3(0f, yc, zw - 0.07f), Quaternion.identity, ShadowCastingMode.Off);
            g.transform.localScale = new Vector3(EXIT_GLOW_DIAMETER, EXIT_GLOW_DIAMETER, EXIT_GLOW_DIAMETER);
            g.receiveShadows = false;
            AddNear(g);
        }

        /// <summary>
        /// Exit dome radius and the glow sprite's true diameter. R1-18: amber at most 0.3 % of a frame; at B1_end (1.2 m from
        /// the lamp) the 0.10 m dome with a 0.08 m glow measured 0.375 % warm-and-bright pixels in gallery r11 (dome bloom
        /// ring), so the dome is 0.084 m across (area x0.71) and the glow matches it.
        /// </summary>
        const float EXIT_DOME_R = 0.042f, EXIT_GLOW_DIAMETER = 0.07f;

        /// <summary>
        /// Dome emitter (D4, R2-21): profile rings, the white-hot core radius and the end of its falloff (fractions of the
        /// projected radius), the core weight there, and the limb colour (deep amber, hue 33, S 0.72) and its strength (r22 at
        /// 0.14 / 1.3: a pale peach disc, hue 27, S 0.41). gallery
        /// r21 with the ledge globe's limb (1, 0.55, 0.2): the beat-1 grade (temperature -6, saturation -22) turned the dome
        /// salmon (hue 21-24, S 0.41-0.50); the yellower limb lands at the AMBER hue band after the grade.
        /// </summary>
        const int EXIT_DOME_RINGS = 12;
        const float EXIT_DOME_CORE_RHO = 0.12f, EXIT_DOME_MID_RHO = 0.3f, EXIT_DOME_MID_WEIGHT = 0.07f, EXIT_DOME_LIMB_GAIN = 0.8f;
        static readonly Color EXIT_DOME_LIMB_SRGB = new Color(1f, 0.68f, 0.28f, 1f);

        /// <summary>
        /// Dome GradientCard weight at projected radius fraction rho: 1 in the core (rho at most EXIT_DOME_CORE_RHO), smoothstep
        /// down to EXIT_DOME_MID_WEIGHT at EXIT_DOME_MID_RHO, then smoothstep to 0 (the deep-amber limb) at the rim. Monotonic.
        /// </summary>
        public static float DomeCoreWeight(float rho)
        {
            float core = 1f - BeatKit.Smoothstep((rho - EXIT_DOME_CORE_RHO) / (EXIT_DOME_MID_RHO - EXIT_DOME_CORE_RHO));
            float band = 1f - BeatKit.Smoothstep((rho - EXIT_DOME_MID_RHO) / (1f - EXIT_DOME_MID_RHO));
            return EXIT_DOME_MID_WEIGHT * band + (1f - EXIT_DOME_MID_WEIGHT) * core;
        }

        // ------------------------------------------------------------------------------------------ doorway shaft (BeamWedge mode 2)

        void BuildDoorwayShaft(BeatContext ctx, MeshBuilder mb)
        {
            // the cold spill enters through the door (object +Z = into the gallery = world -z) and falls 0.25 per metre
            Vector3 dirWorld = new Vector3(0f, -0.25f, -1f).normalized;
            Quaternion rot = Quaternion.Euler(0f, 180f, 0f);
            Vector3 dirObject = Quaternion.Inverse(rot) * dirWorld;
            Vector2 half = new Vector2(AbyssConst.EXIT_DOOR_W * 0.5f, AbyssConst.EXIT_DOOR_H * 0.5f);
            Mesh prism = Beat01Geo.BeamPrism(mb, "GalleryDoorwayShaftPrism", _meshes, half, dirObject, SHAFT_LENGTH);
            Material beam = ctx.Mats.Variant(MatId.BeamWedge, "galleryDoor", m =>
            {
                m.SetFloat(ShaderIds.Recipe, 2f);
                m.SetVector("_BeamParams", new Vector4(0f, 0f, 0f, SHAFT_LENGTH));
                m.SetVector("_BeamDir", new Vector4(dirWorld.x, dirWorld.y, dirWorld.z, 0f));
                m.SetVector("_BeamRect", new Vector4(half.x, half.y, 0f, 0f));
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, SHAFT_DENSITY);
            });
            Vector3 origin = new Vector3(0f, AbyssConst.W[2].y + half.y, AbyssConst.EXIT_DOOR_Z - JAMB_RECESS);
            _shaft = BeatKit.Renderer(_geo, "DoorwaySpillShaft", prism, beam, origin, rot, ShadowCastingMode.Off);
            _shaft.receiveShadows = false;
            ApplyShaft();
        }

        /// <summary>
        /// The doorway spill's per-renderer block. BeamWedge multiplies every mode by the global _AbBeamFade, which
        /// ProgressDirector holds at 0 until the ride starts (the fan beams fade in over ride seconds 0-10), so with the shared
        /// global the spill would be dark for the whole of beat 1 (14.5.1 "the last 12 m brighten from the doorway spill").
        /// The block pins _AbBeamFade to 1 for this renderer only (a block value overrides the global; the renderer leaves the
        /// SRP Batcher, one draw) and carries the interior tier's coverage as _AbFade (additive: brightness |_AbFade|), so the
        /// shaft hands over with the rest of the interior to the proxy card by d 90 (14.6). Called on coverage changes only.
        /// </summary>
        void ApplyShaft()
        {
            if (_shaft == null) return;
            float c = _coverage;
            bool visible = c > 0f;
            if (_shaft.enabled != visible) _shaft.enabled = visible;
            if (!visible) return;
            _shaftMpb.SetFloat(ShaderIds.AbBeamFade, 1f);
            _shaftMpb.SetFloat(ShaderIds.AbFade, c >= 1f ? 1f : c);
            _shaft.SetPropertyBlock(_shaftMpb);
        }

        // ------------------------------------------------------------------------------------------ proxy card and the interior tier

        void BuildProxyAndTier(BeatContext ctx, MeshBuilder mb)
        {
            Material card = ctx.Mats.Variant(MatId.GradientCard, "galleryProxy", m =>
            {
                m.SetColor(ShaderIds.CardTop, Srgb(PROXY_TOP));
                m.SetColor(ShaderIds.CardBottom, Srgb(PROXY_BOTTOM));
                m.SetFloat(BeatKit.CardModeId, 0f);
                m.SetFloat("_CardFog", 1f);
                m.SetFloat(ShaderIds.EmissionStrength, 1f);
                m.SetFloat("_Cull", (float)CullMode.Back);
            });
            float yLow = AbyssConst.W[2].y, dw = AbyssConst.EXIT_DOOR_W * 0.5f;
            // facing the shaft, 2 cm inside the end-wall plane; its 6 cm margins hide behind the jambs and lintel
            BeatKit.ZCard(mb, -dw - 0.06f, dw + 0.06f, yLow - 0.04f, yLow + AbyssConst.EXIT_DOOR_H + 0.04f, JAMB_Z0 - 0.02f, true);
            MeshRenderer proxy = BeatKit.Emit(mb, _geo, "GalleryProxyCard", card, _meshes, ShadowCastingMode.Off);
            if (proxy != null) proxy.receiveShadows = false;

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-1.3f, yLow - 0.4f, SHELL_START_Z - 0.1f), new Vector3(1.3f, AbyssConst.W[1].y + AbyssConst.GALLERY_H + 0.1f, AbyssConst.EXIT_DOOR_Z));
            _interior = new DetailTier("Beat01Interior", bounds, INTERIOR_NEAR, INTERIOR_HYSTERESIS, INTERIOR_FADE);
            for (int i = 0; i < _near.Count; i++) _interior.AddNear(_near[i]);
            if (_bolts != null) _interior.AddNear(_bolts);
            _interior.AddFar(proxy);
            if (proxy != null) proxy.enabled = false;   // interior shown until the first Tick says otherwise
            _coverage = 1f;
        }

        // ------------------------------------------------------------------------------------------ scale report (Full / Simplified)

        void ReportFullScale(ScaleReport report)
        {
            if (_tier == BeatTier.Graybox) return;
            report.Add("gallery.beams.count", _beamCount);
            report.Add("gallery.beam.depth", _beamDepth);
            report.Add("gallery.pilasters.count", _pilasterCount);
            report.Add("gallery.flanges.count", _flangeCount);
            report.Add("gallery.bolts.count", _bolts != null ? _bolts.Count : 0);
            report.Add("gallery.pipe.diameter", _pipeDiameter);
            report.Add("gallery.cables.count", _cableCount);
            report.Add("gallery.cable.diameter", _cableDiameter);
            report.Add("gallery.tray.y", _trayY);
            report.Add("gallery.handwheel.diameter", _wheelDiameter);
            report.Add("gallery.drain.size", 2f * DRAIN_HALF);
            report.Add("gallery.drain.z", AbyssConst.DRAIN_Z);
            report.Add("gallery.floorline.width", _lineWidth);
            report.Add("gallery.floorline.offset", _lineOffset);
            report.Add("gallery.nosings.count", _nosingCount);
            if (_fanRotor != null)
            {
                report.Add("gallery.fan.diameter", _fanDiameter);
                report.Add("gallery.fan.blades", FAN_BLADES);
                report.Add("gallery.fan.y", FAN_Y);
                report.Add("gallery.fan.z", FAN_Z);
            }
        }
    }
}
