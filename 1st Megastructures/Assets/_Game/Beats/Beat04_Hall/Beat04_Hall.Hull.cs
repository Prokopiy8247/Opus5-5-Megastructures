using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Hull of beat 4 (14.5.4, 14.6 persistent skeleton). Exterior: the box x -142..-32, z 199..339, y -45..+39 with the
    /// 12 x 12 m chamfer on the lower -X edge (from (-142, -33) to (-130, -45)), 1 m walls, the 20 x 24 portal and exit
    /// openings (y -19..+5) with their reveals, M_ConcreteBoard at 0.92 (linear) on lattice-aligned grids (one 200 m chunk,
    /// chunk-local vertices around the hull centre) with the macro term raised to +/-18 % and a baked ambient falloff from
    /// the roof (1) to the bottom (0.6), a 6 m panel grid of dark joint
    /// strips (horizontal 0.04 proud, vertical 0.07, never co-planar) in a hierarchy (critique R2-08: the full-contrast 6 m
    /// grid read as an office facade): the 24 m joints wide and full contrast, the 12 m joints full near and 55 % beyond
    /// 200 m, the remaining 6 m joints full within 30 m and fading to 20 % by 110 m (far tint, per pixel); one cold lamp row
    /// every 20 m (y -24, -4, 16, 36): bracketed rectangular fixtures every 24 m, staggered by half a pitch on alternate rows,
    /// +/-25 % intensity, left dark by a static hash (45 % on the two bridge-facing faces, -Z and +X, so no lamp grid reads
    /// as storeys; 15 % elsewhere), each lit fixture with a small Glow core and a large dim Glow pool under it (the short
    /// downward gradient on the wall); the dark interior proxy cards in both openings.
    /// Load paths and scale (critiques R1-19, R2-08): two box girders (10.6 m deep) from the hull's -Z face to the near-flat
    /// belt at y -30 (its inner face z 80), with corbels under their hull ends; a service walkway with 1.1 m rails on the top
    /// of the nearer girder leading to a lit service door in the hull face (a human-scale element against the 84 m wall); two
    /// concrete hangers, one over each girder, rising from plinths on the roof to the next belt up the near flat (y 210, its
    /// inner face z 80), so the hull hangs off the wall (tension above, struts below) and its roofline is crossed by members
    /// that leave every bridge frame at the top instead of being topped by free-standing stacks. All of them dark, fogged,
    /// without lights of their own. The service door is lit (a gradient card under its lamp). An invisible ShadowsOnly
    /// continuation of C1 above y 200 completes the column's shadow on the hull's lit +X face (C1_SHADOW_Y0).
    /// Interior: inner wall faces, floor (-21), ceiling (+37.5, the 1.5 m roof slab
    /// under the +39 hull top), pilasters every 10 m, corbels under the truss ends, 12 Warren roof trusses (1.2 m I-section
    /// members, 8 m deep, spanning the 110 m length at v = +/-5 ... +/-55, i.e. every 10 m across the width) with bottom
    /// ties and top purlins, and the exit proxy: a cold gradient card behind the parked platform that closes the shaft view
    /// while the hall far plane is 600 m (until d 420, 14.6). Nothing protrudes more than 0.45 m on the -X face, the chamfer
    /// or the bottom, so the lift keeps its 3.3 m clearance (14.5.5); lamps on the -X face skip z 270-301 (the lift's guide
    /// head frame ties into that wall).
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        static readonly Vector3 HULL_ORIGIN = new Vector3(-87f, -3f, 269f);
        /// <summary>Underside of the roof slab (hull top +39, slab 1.5 m).</summary>
        const float CEILING_Y = 37.5f;
        /// <summary>
        /// Hull exterior albedo factor (linear) against CONC_MID (critique R1-19: darken 10-20 %; R2-08: 0.92 with the height
        /// falloff below, so the top of the hull is about as light as at 0.82 x 1.1 and the face as a whole stays as dark).
        /// </summary>
        const float HULL_TONE = 0.92f;
        /// <summary>
        /// Height falloff of the hull exterior's ambient (vertex R = AO, ambient and environment only; critique R2-08, lead:
        /// "a colossal object, not a dark panel"): 1 at the roof, HULL_AO_BOTTOM at the hull bottom (linear in y), on the
        /// shell's outer faces and on the joint strips. The skylit face fades into the dark lower shaft like a mass hanging
        /// in it, instead of one flat tone; the Key (the lit +X face) is not affected.
        /// </summary>
        const float HULL_AO_BOTTOM = 0.6f;
        const float PANEL_GRID = 6f, PANEL_W = 0.3f, PANEL_H_PROUD = 0.04f, PANEL_V_PROUD = 0.07f, PANEL_EMBED = 0.02f, PANEL_END = 0.6f;
        /// <summary>
        /// Joint hierarchy of the 6 m panel grid (critique R2-08): level 0 = every 24 m (the opening jambs and every fourth line),
        /// drawn 0.45 m wide at full contrast; level 1 = the other 12 m lines; level 2 = the remaining 6 m lines. Far tint of
        /// levels 1 and 2: the joint albedo blends toward the hull albedo plus this share of the joint contrast between the
        /// two camera distances (per pixel, Surface _FarRange), so at 120-250 m from the bridge the 6 m joints are at most
        /// about 1 level and the panels read as 12 / 24 m bays. The 6 m fade runs from 30 m to 110 m (critique: full within
        /// about 80 m, 30 % by 200 m): with (80, 200) (hall_r21) and (50, 150) (hall_r22) every 6 m line on the KS2a face, at
        /// 85-160 m, still measured -1.6 to -2.2 levels against -2.2 to -2.6 for the 24 m lines (the same graph paper); the
        /// grid stays complete where it is seen up close (apron, portal, bridge end within 30 m).
        /// </summary>
        const float PANEL_W_24 = 0.45f;
        const float JOINT12_KEEP = 0.55f, JOINT12_FAR0 = 60f, JOINT12_FAR1 = 200f;
        const float JOINT6_KEEP = 0.20f, JOINT6_FAR0 = 30f, JOINT6_FAR1 = 110f;
        /// <summary>Macro value noise amplitude of the hull exterior (1 = +/-12 %; R2-08: the hull read as a flat tone at 120-250 m).</summary>
        const float HULL_MACRO = 1.5f;
        /// <summary>Cold lamp rows on the hull exterior, 20 m apart (14.5.4).</summary>
        static readonly float[] HULL_LAMP_ROWS = { -24f, -4f, 16f, 36f };
        /// <summary>
        /// Lamp pitch along a row (staggered by half a pitch on alternate rows, at panel centres), the share left dark by the
        /// static hash on the two bridge-facing faces (-Z, +X; critique R2-08: 40-50 %, so no lamp grid reads as storeys) and on
        /// the others, and the +/-25 % intensity spread (three Glow batches per kind: 0.75, 1.0, 1.25).
        /// </summary>
        const float HULL_LAMP_SPACING = 24f, HULL_LAMP_DEAD_BRIDGE = 0.45f, HULL_LAMP_DEAD = 0.15f, HULL_LAMP_SPREAD = 0.25f;
        /// <summary>Glow core of a lit fixture (diameter m, strength x COLD_LAMP) just in front of its lens.</summary>
        const float HULL_GLOW_D = 0.45f, HULL_GLOW_I = 2.4f;
        /// <summary>
        /// Dim Glow pool under every lit fixture (the short downward gradient on the wall, no real light: the hull lamps are
        /// skeleton geometry visible from beats 2-5): diameter, peak strength, drop below the lamp, distance off the wall.
        /// </summary>
        const float HULL_POOL_D = 4.4f, HULL_POOL_I = 0.018f, HULL_POOL_DROP = 1.6f, HULL_POOL_OUT = 0.9f;
        /// <summary>Lamp pairs on the -X face skip this z range (the lift's guide head frame and its girders tie into the wall).</summary>
        const float HULL_EXIT_LAMP_SKIP_Z0 = 270f, HULL_EXIT_LAMP_SKIP_Z1 = 301f;
        // ---------------------------------------------------------------- load-path girders, walkway, door, stacks (R1-19)
        /// <summary>
        /// Girder centre lines (x) at panel centres; they run from the near-flat belt (inner face z 80) to the hull's -Z face.
        /// The nearer one stays at |x| >= 67.5 (flange edge) so the bridge's nadir view KS2b never sees it: at its lowest depth
        /// (y -35.6, 34.3 m below the eye) the 21:9 frame reaches |x| = 1.539 x 34.3 = 52.8 m (hall_r12 test: at x -53 the
        /// flange edge entered the 21:9 frame).
        /// </summary>
        static readonly float[] GIRDER_X = { -71f, -119f };
        /// <summary>
        /// Girder section: bottom and top of the flanges (inside the belt band y -36..-24), web width, flange width and
        /// thickness. Critique R2-08: the r1 box girders (6 m web, 7 m flanges, a 6.6 m railed walkway) read as a road
        /// overpass; the deep narrow section (3 m web, 4 m flanges, 10.6 m deep) reads as a beam, its lit top a thin line.
        /// </summary>
        const float GIRDER_Y0 = -35.6f, GIRDER_Y1 = -25f, GIRDER_WEB = 3f, GIRDER_FLANGE_W = 4f, GIRDER_FLANGE_T = 0.6f, GIRDER_RIB_PITCH = 12f;
        /// <summary>
        /// Baked ambient occlusion (vertex R) of the girder spans at the wall end, rising linearly to 1 at the hull end (critique
        /// R2-08 "let the girders fade toward the wall"): the ambient and haze-skylit sides recede into the dark lower shaft
        /// toward the belt, while the hull end, the walkway and the door stay readable.
        /// </summary>
        const float GIRDER_WALL_AO = 0.35f;
        /// <summary>Walkway rails on the nearer girder (x GIRDER_X[0] +/- RAIL_X: a 3.2 m service walkway), posts every 2 m; the service door (w x h) in the hull face.</summary>
        const float WALK_RAIL_X = 1.6f, DOOR_W = 1.3f, DOOR_H = 2.42f;
        /// <summary>
        /// The service door lit by its lamp (critique R2-08: "give the door one small lamp so it reads at 120 m"; the dark
        /// DOOR_LEAF leaf under the lamp was invisible at KS2a, 82 m): a fogged GradientCard on the leaf front, light under the
        /// lamp and darker at the sill (sRGB top / bottom), so a 1.3 x 2.42 m lit door (13 x 25 px at KS2a) measures the
        /// 84 m wall. Well below the lamp lenses and C1's lit pool.
        /// </summary>
        const int DOOR_LIT_TOP = 0x3A444D, DOOR_LIT_BOTTOM = 0x222930;
        /// <summary>
        /// C1 shadow continuation (lead_b01 KS2a, critique R2-08 "thin diagonal line across the hull's lit face"): the beat-2
        /// sun (yaw 205, pitch -72) reaches the hull's +X face past C1, whose shadow-casting chunks end at y 200 (Beat02 Shaft:
        /// only the chunks between -200 and 200 cast). For the face's corner strip (z 199-227) the shadow ray leaves C1 between
        /// y 100 and 250, so the face showed a truncated column shadow: a narrow wedge, the y 200 rim as one thin line and the
        /// y 120 band ring (0.4 m) as a second, crossing it (a ray-traced model of that shadow matches the capture line for
        /// line). This invisible ShadowsOnly cylinder inside C1 (radius R - inset, y 195-420) completes the column's shadow,
        /// so the strip lies in one whole shadow with a vertical edge. Its other receivers: only the hull roof (rays through
        /// C1 above y 200 from the bridge, ledge and apron miss C1 by more than 18 m).
        /// </summary>
        public const float C1_SHADOW_Y0 = 195f, C1_SHADOW_Y1 = 420f, C1_SHADOW_INSET = 0.1f;
        /// <summary>
        /// Hangers (critique R2-08, replacing the r1 roof stacks that gave the hull a factory-with-chimneys silhouette): one
        /// over each girder (same x), a 6 x 4 m concrete member from a plinth on the roof up to the belt of ring level
        /// <see cref="HANGER_BELT_K"/> (y 210, band 204..216) on the near flat. The axis runs from the foot (y, z) through the
        /// belt's inner face (z 80) at y 208, where the member's vertical extent (+/-3.3 m) lies inside the belt face (204..212,
        /// under the 4 m top chamfer), and ends 3 m inside the belt so its end cap stays under the chamfer. From every bridge
        /// pose the member leaves the frame through its top edge (the bridge sees at most 33 degrees up), so the hull reads as
        /// hung in the shaft, with no readable top.
        /// </summary>
        const int HANGER_BELT_K = 16;
        const float HANGER_W = 6f, HANGER_D = 4f, HANGER_FOOT_Y = 41f, HANGER_FOOT_Z = 207f, HANGER_ENTRY_Y = 208f, HANGER_END_Z = 77f;
        /// <summary>Roof plinth of a hanger (x / y / z size; it stands 0.4 m into the 1.5 m roof slab, above the ceiling at 37.5).</summary>
        static readonly Vector3 HANGER_PLINTH = new Vector3(8f, 5.4f, 8f);
        const float PILASTER_W = 1.6f, PILASTER_D = 1.0f;
        const float TRUSS_DEPTH = 8f, TRUSS_MEMBER = 1.2f;
        const int TRUSS_BAYS = 14;
        /// <summary>Exit proxy plane (beyond the parked platform, x -172, and its guide rails, x >= -173) and extent.</summary>
        const float EXIT_PROXY_X = -176f, EXIT_PROXY_Y0 = -50f, EXIT_PROXY_Y1 = 30f, EXIT_PROXY_HALF_W = 33f;
        /// <summary>
        /// Emission gain of the exit proxy (look-dev): the shaft outside the exit is sunlit at hall level (beat-5 sun, full
        /// above y -30), so seen from the dark hall it reads as a cold bright rectangle (KS4 extreme far layer), not as the
        /// unlit fog colour.
        /// </summary>
        const float EXIT_PROXY_GAIN = 5.5f;

        static readonly int KeyMaskId = Shader.PropertyToID("_KeyLightMask");
        static readonly int CardFogId = Shader.PropertyToID("_CardFog");
        static readonly int MinPixelsId = Shader.PropertyToID("_MinPixels");
        static readonly int EnergyConserveId = Shader.PropertyToID("_EnergyConserve");
        static readonly int RustId = Shader.PropertyToID("_RustAmount");
        static readonly int WearId = Shader.PropertyToID("_WearAmount");
        static readonly int MacroAmpId = Shader.PropertyToID("_MacroAmp");
        static readonly int FarTintId = Shader.PropertyToID("_FarTint");
        static readonly int FarRangeId = Shader.PropertyToID("_FarRange");

        readonly List<Bounds> _occ = new List<Bounds>(256);

        // ================================================================================================ materials

        /// <summary>Interior materials never receive the Key (the hull blocks it; beyond the shadow distance it would not).</summary>
        Material Nokey(MatId id) => _mats.Variant(id, "nokey", m => m.SetFloat(KeyMaskId, 0f));

        Material ThinMat() => _mats.Variant(MatId.SteelPainted, "b4thin", m =>
        {
            m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            m.SetFloat(KeyMaskId, 0f);
        });

        /// <summary>
        /// Roof trusses: the machine grey (DOOR_LEAF) instead of STEEL_DARK, so the members catch the can uplights and read as
        /// converging lines against the dark roof in KS4 (STEEL_DARK stayed under the contrast toe); ThinLine like the rails.
        /// </summary>
        Material TrussMat() => _mats.Variant(MatId.SteelPainted, "b4truss", m =>
        {
            m.SetColor(ShaderIds.BaseColor, AbyssPalette.DOOR_LEAF);
            m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            m.SetFloat(KeyMaskId, 0f);
        });

        /// <summary>Machine paint: the DOOR_LEAF grey (palette) on the SteelPainted recipe, little rust (<= 12 %, desaturated).</summary>
        Material MachineMat() => _mats.Variant(MatId.SteelPainted, "b4machine", m =>
        {
            m.SetColor(ShaderIds.BaseColor, AbyssPalette.DOOR_LEAF);
            m.SetFloat(ShaderIds.Roughness, 0.45f);
            m.SetFloat(RustId, 0.04f);
            m.SetFloat(WearId, 1.2f);
            m.SetFloat(KeyMaskId, 0f);
        });

        /// <summary>
        /// Guide frames (critique R1-20): a darker matte paint than the machines (40 % of the way from STEEL_DARK to DOOR_LEAF,
        /// roughness 0.7, little edge wear), so the frames read as dark silhouettes and the lit cylinders, wheels and moving
        /// crossheads (DOOR_LEAF) stand out against them.
        /// </summary>
        Material FrameMat() => _mats.Variant(MatId.SteelPainted, "b4frame", m =>
        {
            m.SetColor(ShaderIds.BaseColor, Color.Lerp(AbyssPalette.STEEL_DARK, AbyssPalette.DOOR_LEAF, 0.4f));
            m.SetFloat(ShaderIds.Roughness, 0.7f);
            m.SetFloat(RustId, 0.03f);
            m.SetFloat(WearId, 0.4f);
            m.SetFloat(KeyMaskId, 0f);
        });

        /// <summary>
        /// Hull exterior (critiques R1-19, R2-08): the ConcreteBoard recipe at HULL_TONE (linear albedo) with the baked height
        /// falloff (vertex R), so the hull reads as a mass behind C1 instead of competing with it in the first shaft views.
        /// </summary>
        Material HullMat() => _mats.Variant(MatId.ConcreteBoard, "b4hull", m =>
        {
            m.SetColor(ShaderIds.BaseColor, ScaleLinear(AbyssPalette.CONC_MID, HULL_TONE));
            m.SetFloat(MacroAmpId, HULL_MACRO);
        });

        /// <summary>
        /// Panel joints of the hull grid: CONC_DARK (about 40 % of the wall albedo), rough, so the grid reads as recessed dark
        /// grooves. level 0 (24 m) keeps that contrast at every distance; levels 1 (12 m) and 2 (6 m) blend per pixel toward the
        /// hull albedo plus 'keep' of the joint contrast between the far-tint distances (critique R2-08 joint hierarchy).
        /// </summary>
        Material HullJointMat(int level)
        {
            if (level <= 0)
                return _mats.Variant(MatId.ConcreteBoard, "b4joint", m =>
                {
                    m.SetColor(ShaderIds.BaseColor, AbyssPalette.CONC_DARK);
                    m.SetFloat(ShaderIds.Roughness, 0.95f);
                });
            float keep = level == 1 ? JOINT12_KEEP : JOINT6_KEEP;
            float f0 = level == 1 ? JOINT12_FAR0 : JOINT6_FAR0, f1 = level == 1 ? JOINT12_FAR1 : JOINT6_FAR1;
            return _mats.Variant(MatId.ConcreteBoard, level == 1 ? "b4joint12" : "b4joint6", m =>
            {
                m.SetColor(ShaderIds.BaseColor, AbyssPalette.CONC_DARK);
                m.SetFloat(ShaderIds.Roughness, 0.95f);
                m.SetColor(FarTintId, JointFarTint(keep));
                m.SetVector(FarRangeId, new Vector4(f0, f1, 0f, 0f));
            });
        }

        /// <summary>
        /// Far albedo of a faded joint level (sRGB colour for SetColor; the shader receives it linear): the hull albedo
        /// (CONC_MID x HULL_TONE, linear) plus 'keep' of the linear step down to CONC_DARK.
        /// </summary>
        public static Color JointFarTint(float keep)
        {
            Color hull = AbyssPalette.CONC_MID.linear, dark = AbyssPalette.CONC_DARK.linear;
            hull = new Color(hull.r * HULL_TONE, hull.g * HULL_TONE, hull.b * HULL_TONE, 1f);
            Color l = Color.Lerp(hull, dark, keep);
            l.a = 1f;
            return l.gamma;
        }

        /// <summary>The load-path girders and roof stacks: dark concrete (CONC_DARK), fogged silhouettes, no lights.</summary>
        Material GirderMat() => _mats.Variant(MatId.ConcreteBoard, "b4girder", m => m.SetColor(ShaderIds.BaseColor, AbyssPalette.CONC_DARK));

        /// <summary>Exterior thin steel (gantry rails): ThinLine like the catwalk rails, but lit by the Key (outside the hull).</summary>
        Material ThinExtMat() => _mats.Variant(MatId.SteelPainted, "b4thinext", m => m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX));

        /// <summary>Lens of a hull lamp left dark by the static hash: dark glass.</summary>
        Material DeadLensMat() => _mats.Variant(MatId.SteelPainted, "b4deadlens", m =>
        {
            m.SetColor(ShaderIds.BaseColor, AbyssPalette.GLASS);
            m.SetFloat(ShaderIds.Roughness, 0.3f);
            m.SetFloat(WearId, 0f);
        });

        /// <summary>An sRGB palette colour with its linear value scaled by k (alpha 1).</summary>
        static Color ScaleLinear(Color srgb, float k)
        {
            Color l = srgb.linear;
            return new Color(l.r * k, l.g * k, l.b * k, 1f).gamma;
        }

        Material FloorMat() => _mats.Variant(MatId.ConcreteFloor, "b4floor", m =>
        {
            m.SetFloat(BeatKit.WearWidthId, 0f);
            m.SetFloat(KeyMaskId, 0f);
        });

        Material WeatheredMat() => _mats.Variant(MatId.SteelWeathered, "b4pit", m =>
        {
            m.SetFloat(RustId, 0.10f);
            m.SetFloat(KeyMaskId, 0f);
        });

        // ================================================================================================ persistent skeleton

        void BuildSkeleton(BeatContext ctx)
        {
            Transform sk = BeatKit.Group(_geo, "Skeleton");
            Material conc = HullMat();
            var mb = new MeshBuilder();
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, z0 = AbyssConst.HALL_Z0, z1 = AbyssConst.HALL_Z1;
            float yb = AbyssConst.HALL_HULL_BOTTOM_Y, yt = AbyssConst.HALL_ROOF_Y, ch = AbyssConst.HALL_CHAMFER;
            float zc = AbyssConst.W[8].z, ow = AbyssConst.HALL_OPENING_W * 0.5f;
            float hz0 = zc - ow, hz1 = zc + ow, hy0 = OPENING_Y0, hy1 = OPENING_Y0 + AbyssConst.HALL_OPENING_H;

            // ---- shell: six faces, the chamfer, the reveals (chunk-local around the hull centre)
            mb.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            Face(mb, new Vector3(x0, 0f, 0f), Vector3.forward, Vector3.up, Vector3.right, z0, z1, yb, yt, LAT_Z0, LAT_Z, LAT_Y0, LAT_XY,
                true, hz0, hz1, hy0, hy1);
            Face(mb, new Vector3(x1, 0f, 0f), Vector3.forward, Vector3.up, Vector3.left, z0, z1, yb + ch, yt, LAT_Z0, LAT_Z, LAT_Y0, LAT_XY,
                true, hz0, hz1, hy0, hy1);
            Face(mb, new Vector3(0f, yt, 0f), Vector3.right, Vector3.forward, Vector3.up, x1, x0, z0, z1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);
            Face(mb, new Vector3(0f, yb, 0f), Vector3.right, Vector3.forward, Vector3.down, x1 + ch, x0, z0, z1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);
            float sq2 = Mathf.Sqrt(2f);
            Vector3 slant = new Vector3(-1f, 1f, 0f) / sq2;
            Face(mb, new Vector3(x1 + ch, yb, 0f), Vector3.forward, slant, new Vector3(-1f, -1f, 0f) / sq2, z0, z1, 0f, ch * sq2,
                LAT_Z0, LAT_Z, 0f, LAT_XY * sq2);
            SideFace(mb, z0, Vector3.back);
            SideFace(mb, z1, Vector3.forward);
            HeightAO(mb, 0);   // outer faces only (the reveals keep their light)

            int first = mb.VertexCount;
            Reveals(mb, x0 - HULL_WALL_T, x0, hz0, hz1, hy0, hy1);
            _portalReveal = BeatKit.VertexBounds(mb, first);
            first = mb.VertexCount;
            Reveals(mb, x1, x1 + HULL_WALL_T, hz0, hz1, hy0, hy1);
            _exitReveal = BeatKit.VertexBounds(mb, first);
            Mesh shell = BeatKit.Bake(mb, "HullShell", _meshes);
            // the hull casts: it shades the platform riding under it (beat-5 sun) and the apron side of the column
            _hullShell = BeatKit.Renderer(sk, "HullShell", shell, conc, HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.On, true);

            // ---- 6 m panel grid: dark joint strips (read as recessed grooves, not as lighter bevel lines), one mesh per joint
            //      level of the hierarchy (24 m / 12 m / 6 m)
            for (int level = 0; level < 3; level++)
            {
                mb.Push(Matrix4x4.Translate(-HULL_ORIGIN));
                PanelBands(mb, level);
                HeightAO(mb, 0);
                string pname = level == 0 ? "HullPanelGrid" : (level == 1 ? "HullPanelGrid12" : "HullPanelGrid6");
                Mesh panels = BeatKit.Bake(mb, pname, _meshes);
                BeatKit.Renderer(sk, pname, panels, HullJointMat(level), HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off, true);
            }

            // ---- cold lamp rows (bracketed fixtures, Glow cores and wall pools), the service door's lamp
            BuildHullLamps(sk);

            // ---- load paths, walkway and door, hangers
            BuildHullStructure(sk);

            // ---- dark interior proxy cards in both openings (mid-depth of the reveal, 5 cm into sill, lintel and jambs)
            Material dark = _mats.Variant(MatId.GradientCard, "b4dark", m =>
            {
                m.SetColor(ShaderIds.CardTop, AbyssPalette.Hex(0x171C21));
                m.SetColor(ShaderIds.CardBottom, AbyssPalette.Hex(0x0F1317));
                m.SetFloat(CardFogId, 1f);
                m.SetFloat(ShaderIds.EmissionStrength, 1f);
            });
            const float e = 0.05f;
            float xp = x0 - HULL_WALL_T * 0.5f, xe = x1 + HULL_WALL_T * 0.5f;
            CardQuad(mb, new Vector3(xp, hy0 - e, hz0 - e), new Vector3(xp, hy1 + e, hz0 - e), new Vector3(xp, hy1 + e, hz1 + e),
                new Vector3(xp, hy0 - e, hz1 + e), Vector3.right);
            _portalCard = BeatKit.Emit(mb, sk, "PortalDarkCard", dark, _meshes, ShadowCastingMode.Off);
            CardQuad(mb, new Vector3(xe, hy0 - e, hz1 + e), new Vector3(xe, hy1 + e, hz1 + e), new Vector3(xe, hy1 + e, hz0 - e),
                new Vector3(xe, hy0 - e, hz0 - e), Vector3.left);
            _exitCard = BeatKit.Emit(mb, sk, "ExitDarkCard", dark, _meshes, ShadowCastingMode.Off);
        }

        /// <summary>Side face (z = const) of the chamfered hull: the rectangle above the chamfer, the strip right of it and six 2 m bands along the 45 degree edge.</summary>
        static void SideFace(MeshBuilder mb, float z, Vector3 n)
        {
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, yb = AbyssConst.HALL_HULL_BOTTOM_Y, yt = AbyssConst.HALL_ROOF_Y;
            float ch = AbyssConst.HALL_CHAMFER;
            Vector3 o = new Vector3(0f, 0f, z);
            Face(mb, o, Vector3.right, Vector3.up, n, x1, x0, yb + ch, yt, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
            Face(mb, o, Vector3.right, Vector3.up, n, x1 + ch, x0, yb, yb + ch, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
            int bands = Mathf.RoundToInt(ch / LAT_XY);
            for (int i = 0; i < bands; i++)
            {
                float ya = yb + ch - LAT_XY * i, yc = ya - LAT_XY;
                float xa = x1 + LAT_XY * i, xb = xa + LAT_XY;
                // triangle on the chamfer edge, then quads to x1 + ch (2 m lattice: matches the rectangles and the chamfer face)
                int i0 = mb.Vertex(new Vector3(xa, ya, z), n, new Vector2(xa, ya));
                int i1 = mb.Vertex(new Vector3(xb, ya, z), n, new Vector2(xb, ya));
                int i2 = mb.Vertex(new Vector3(xb, yc, z), n, new Vector2(xb, yc));
                BeatKit.Tri(mb, i0, i1, i2);
                for (float x = xb; x < x1 + ch - 1e-3f; x += LAT_XY)
                    BeatKit.Quad(mb, new Vector3(x, ya, z), new Vector3(x + LAT_XY, ya, z), new Vector3(x + LAT_XY, yc, z), new Vector3(x, yc, z), n);
            }
        }

        /// <summary>Opening reveal through a wall from xa to xb: sill, lintel and both jambs (lattice-aligned with the faces).</summary>
        static void Reveals(MeshBuilder mb, float xa, float xb, float z0, float z1, float y0, float y1)
        {
            Face(mb, new Vector3(0f, y0, 0f), Vector3.right, Vector3.forward, Vector3.up, xa, xb, z0, z1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);
            Face(mb, new Vector3(0f, y1, 0f), Vector3.right, Vector3.forward, Vector3.down, xa, xb, z0, z1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);
            Face(mb, new Vector3(0f, 0f, z0), Vector3.right, Vector3.up, Vector3.forward, xa, xb, y0, y1, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
            Face(mb, new Vector3(0f, 0f, z1), Vector3.right, Vector3.up, Vector3.back, xa, xb, y0, y1, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
        }

        /// <summary>Ambient factor of the hull exterior at world height y: HULL_AO_BOTTOM at the hull bottom, 1 at the roof, linear between.</summary>
        public static float HullHeightAO(float y)
        {
            float t = Mathf.Clamp01((y - AbyssConst.HALL_HULL_BOTTOM_Y) / (AbyssConst.HALL_ROOF_Y - AbyssConst.HALL_HULL_BOTTOM_Y));
            return Mathf.Lerp(HULL_AO_BOTTOM, 1f, t);
        }

        /// <summary>Multiply vertex R (AO) of the vertices first..count-1 (chunk-local around HULL_ORIGIN) by <see cref="HullHeightAO"/>.</summary>
        static void HeightAO(MeshBuilder mb, int first)
        {
            for (int i = first; i < mb.Colors.Count; i++)
            {
                Color32 c = mb.Colors[i];
                c.r = (byte)Mathf.RoundToInt(c.r * HullHeightAO(mb.Positions[i].y + HULL_ORIGIN.y));
                mb.Colors[i] = c;
            }
        }

        /// <summary>Joint level of the k-th grid line counted from a 24 m datum: 0 every 4th line (24 m), 1 every 2nd (12 m), 2 otherwise (6 m).</summary>
        public static int JointLevel(int k)
        {
            k = k < 0 ? -k : k;
            return (k & 3) == 0 ? 0 : ((k & 1) == 0 ? 1 : 2);
        }

        /// <summary>
        /// Joint level of a vertical band on an end face (index j of <see cref="PanelBands"/>): the opening jambs (|j| = 1) and
        /// every 24 m outward from them are level 0, the 12 m lines between them level 1, the remaining 6 m lines level 2; the
        /// band on the walk axis above and below the opening is level 1.
        /// </summary>
        static int EndFaceJointLevel(int j) => j == 0 ? 1 : JointLevel((j < 0 ? -j : j) - 1);

        /// <summary>
        /// Raised panel grid every 6 m on the four walls, only the joints of one hierarchy level (<see cref="JointLevel"/>):
        /// horizontal bands (0.04 proud) and vertical bands (0.07 proud, so the crossings never share a plane), 0.3 m wide
        /// (0.45 m for the 24 m joints), ending 0.6 m short of the box edges and split around the openings. The -X face only
        /// carries bands above the chamfer (the lift passes under it).
        /// </summary>
        void PanelBands(MeshBuilder mb, int level)
        {
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, z0 = AbyssConst.HALL_Z0, z1 = AbyssConst.HALL_Z1;
            float yb = AbyssConst.HALL_HULL_BOTTOM_Y, yt = AbyssConst.HALL_ROOF_Y, ch = AbyssConst.HALL_CHAMFER;
            float zc = AbyssConst.W[8].z, ow = AbyssConst.HALL_OPENING_W * 0.5f;
            float hz0 = zc - ow, hz1 = zc + ow, hy0 = OPENING_Y0, hy1 = OPENING_Y0 + AbyssConst.HALL_OPENING_H;
            float w = level == 0 ? PANEL_W_24 : PANEL_W;
            float hw = w * 0.5f, hwMax = PANEL_W_24 * 0.5f;
            int rows = Mathf.RoundToInt((yt - yb) / PANEL_GRID);

            // end faces (portal x0 facing +X, exit x1 facing -X)
            for (int f = 0; f < 2; f++)
            {
                float xf = f == 0 ? x0 : x1, sx = f == 0 ? 1f : -1f;
                float yLow = f == 0 ? yb : yb + ch;
                // horizontal bands
                for (int k = 1; k < rows; k++)
                {
                    if (JointLevel(k) != level) continue;
                    float y = yb + PANEL_GRID * k;
                    if (y < yLow + 1f) continue;
                    float za = z0 + PANEL_END, zb = z1 - PANEL_END;
                    if (y > hy0 - hw && y < hy1 + hw)
                    {
                        BandZ(mb, xf, sx, y, za, hz0, PANEL_H_PROUD, w);
                        BandZ(mb, xf, sx, y, hz1, zb, PANEL_H_PROUD, w);
                    }
                    else BandZ(mb, xf, sx, y, za, zb, PANEL_H_PROUD, w);
                }
                // vertical bands: on the opening jambs, every 6 m outward from them, and one on the walk axis
                for (int j = -10; j <= 10; j++)
                {
                    if (EndFaceJointLevel(j) != level) continue;
                    float z = j == 0 ? zc : (j < 0 ? hz0 + PANEL_GRID * (j + 1) : hz1 + PANEL_GRID * (j - 1));
                    if (z < z0 + PANEL_END + hwMax || z > z1 - PANEL_END - hwMax) continue;
                    float ya = yLow + PANEL_END, yz = yt - PANEL_END;
                    if (z > hz0 - hwMax - 1e-3f && z < hz1 + hwMax + 1e-3f)
                    {
                        if (hy0 - ya > 0.5f) BandY(mb, xf, sx, z, ya, hy0, PANEL_V_PROUD, w);
                        BandY(mb, xf, sx, z, hy1, yz, PANEL_V_PROUD, w);
                    }
                    else BandY(mb, xf, sx, z, ya, yz, PANEL_V_PROUD, w);
                }
            }
            // side faces (z0 facing -Z, z1 facing +Z)
            for (int f = 0; f < 2; f++)
            {
                float zf = f == 0 ? z0 : z1, sz = f == 0 ? -1f : 1f;
                for (int k = 1; k < rows; k++)
                {
                    if (JointLevel(k) != level) continue;
                    float y = yb + PANEL_GRID * k;
                    float xa = y < yb + ch ? x1 + (yb + ch - y) : x1;   // chamfer edge x at height y
                    BandX(mb, zf, sz, y, xa + PANEL_END + hwMax, x0 - PANEL_END, PANEL_H_PROUD, w);
                }
                for (int j = 1; ; j++)
                {
                    float x = x0 - PANEL_GRID * j;
                    if (x < x1 + PANEL_END + hwMax) break;
                    if (JointLevel(j) != level) continue;
                    float yLow = x < x1 + ch ? yb + ch - (x - x1) : yb;   // chamfer edge height at x
                    BandXv(mb, zf, sz, x, yLow + PANEL_END + hwMax, yt - PANEL_END, PANEL_V_PROUD, w);
                }
            }
        }

        /// <summary>Horizontal band (width w) on an x-plane face, along z from za to zb at height y.</summary>
        static void BandZ(MeshBuilder mb, float xf, float sx, float y, float za, float zb, float proud, float w)
        {
            if (zb - za < 0.5f) return;
            float d = proud + PANEL_EMBED;
            MeshKit.ChamferBox(mb, new Vector3(xf + sx * (proud - d * 0.5f), y, (za + zb) * 0.5f), new Vector3(d, w, zb - za), 0.01f);
        }

        /// <summary>Vertical band (width w) on an x-plane face at z, from ya to yb.</summary>
        static void BandY(MeshBuilder mb, float xf, float sx, float z, float ya, float yb, float proud, float w)
        {
            if (yb - ya < 0.5f) return;
            float d = proud + PANEL_EMBED;
            MeshKit.ChamferBox(mb, new Vector3(xf + sx * (proud - d * 0.5f), (ya + yb) * 0.5f, z), new Vector3(d, yb - ya, w), 0.01f);
        }

        /// <summary>Horizontal band (width w) on a z-plane face, along x from xa to xb at height y.</summary>
        static void BandX(MeshBuilder mb, float zf, float sz, float y, float xa, float xb, float proud, float w)
        {
            if (xb - xa < 0.5f) return;
            float d = proud + PANEL_EMBED;
            MeshKit.ChamferBox(mb, new Vector3((xa + xb) * 0.5f, y, zf + sz * (proud - d * 0.5f)), new Vector3(xb - xa, w, d), 0.01f);
        }

        /// <summary>Vertical band (width w) on a z-plane face at x, from ya to yb.</summary>
        static void BandXv(MeshBuilder mb, float zf, float sz, float x, float ya, float yb, float proud, float w)
        {
            if (yb - ya < 0.5f) return;
            float d = proud + PANEL_EMBED;
            MeshKit.ChamferBox(mb, new Vector3(x, (ya + yb) * 0.5f, zf + sz * (proud - d * 0.5f)), new Vector3(w, yb - ya, d), 0.01f);
        }

        // ================================================================================================ hull lamps (R1-19)

        /// <summary>Glow positions of the lit fixtures per intensity level (0.85 / 1.0 / 1.15): cores and wall pools (build only).</summary>
        readonly List<Vector3>[] _lampCores = { new List<Vector3>(48), new List<Vector3>(48), new List<Vector3>(48) };
        readonly List<Vector3>[] _lampPools = { new List<Vector3>(48), new List<Vector3>(48), new List<Vector3>(48) };
        int _hullLampsLit, _hullLampsDark, _hullLampsLitBridge, _hullLampsDarkBridge, _bridgeLampSeq;

        /// <summary>
        /// The four cold lamp rows (14.5.4, y -24, -4, 16, 36): fixtures at panel centres every 24 m along each face, alternate
        /// rows shifted by 12 m (staggered), a static hash leaves 45 % dark on the bridge-facing faces (-Z, +X) and 15 % on the
        /// others and picks one of three intensities (+/-25 %); the service door gets its own always-lit fixture. Housings
        /// receive the Key (exterior); lenses LampCold, dead lenses dark glass; Glow cores and pools in six instanced batches.
        /// </summary>
        void BuildHullLamps(Transform sk)
        {
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, z0 = AbyssConst.HALL_Z0, z1 = AbyssConst.HALL_Z1;
            float yb = AbyssConst.HALL_HULL_BOTTOM_Y, ch = AbyssConst.HALL_CHAMFER, zc = AbyssConst.W[8].z;
            for (int i = 0; i < 3; i++) { _lampCores[i].Clear(); _lampPools[i].Clear(); }
            _hullLampsLit = _hullLampsDark = _hullLampsLitBridge = _hullLampsDarkBridge = _bridgeLampSeq = 0;
            var housing = new MeshBuilder();
            var lens = new MeshBuilder();
            var dead = new MeshBuilder();
            housing.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            lens.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            dead.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            float xm = x0 - 57f;                                   // -89: a panel centre (x0 - 3 - 6 k)
            float halfW = 0.5f * (z1 - z0), ow = AbyssConst.HALL_OPENING_W * 0.5f;
            int index = 0;
            for (int r = 0; r < HULL_LAMP_ROWS.Length; r++)
            {
                float y = HULL_LAMP_ROWS[r];
                float shift = (r & 1) == 1 ? 0.5f * HULL_LAMP_SPACING : 0f;
                // -Z and +Z faces (110 m along x)
                for (int j = -2; j <= 2; j++)
                {
                    float x = xm + HULL_LAMP_SPACING * j + shift;
                    if (x < x1 + 4f || x > x0 - 4f) continue;
                    HullLamp(housing, lens, dead, new Vector3(x, y, z0), Vector3.back, index++, -1, true);
                    HullLamp(housing, lens, dead, new Vector3(x, y, z1), Vector3.forward, index++, -1, false);
                }
                // +X (portal) and -X (exit) faces: panel centres 9 m (+ 24 k, + 12 on odd rows) outside the opening jambs
                for (int j = 0; j < 3; j++)
                {
                    float off = ow + 9f + HULL_LAMP_SPACING * j + shift;
                    if (off > halfW - 2.5f) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float z = zc + s * off;
                        HullLamp(housing, lens, dead, new Vector3(x0, y, z), Vector3.right, index++, -1, true);
                        if (y > yb + ch + 1f && (z < HULL_EXIT_LAMP_SKIP_Z0 || z > HULL_EXIT_LAMP_SKIP_Z1))
                            HullLamp(housing, lens, dead, new Vector3(x1, y, z), Vector3.left, index++, -1, false);
                    }
                }
            }
            // the service door lamp (always lit, brightest level) above the door on the -Z face
            HullLamp(housing, lens, dead, new Vector3(GIRDER_X[0], GIRDER_Y1 + DOOR_H + 0.95f, z0), Vector3.back, index++, 2, false);

            Mesh housings = BeatKit.Bake(housing, "HullLampHousings", _meshes);
            BeatKit.Renderer(sk, "HullLampHousings", housings, _mats[MatId.SteelPainted], HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off);
            Mesh lenses = BeatKit.Bake(lens, "HullLampLenses", _meshes);
            BeatKit.Renderer(sk, "HullLampLenses", lenses, _mats[MatId.LampCold], HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off);
            if (dead.VertexCount > 0)
            {
                Mesh deadLenses = BeatKit.Bake(dead, "HullLampLensesDark", _meshes);
                BeatKit.Renderer(sk, "HullLampLensesDark", deadLenses, DeadLensMat(), HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off);
            }
            for (int i = 0; i < 3; i++)
            {
                float k = 1f + HULL_LAMP_SPREAD * (i - 1);
                _hullGlows[i] = GlowBatch("Beat04HullLampCore" + i, "b4core" + i, HULL_GLOW_I * k, HULL_GLOW_D, _lampCores[i]);
                _hullGlows[3 + i] = GlowBatch("Beat04HullLampPool" + i, "b4pool" + i, HULL_POOL_I * k, HULL_POOL_D, _lampPools[i]);
            }
        }

        /// <summary>One cold Glow batch (COLD_LAMP x strength, true diameter diam) at the given points; registered in play mode.</summary>
        InstancedBatch GlowBatch(string name, string key, float strength, float diam, List<Vector3> points)
        {
            if (points.Count == 0) return null;
            Material glow = _mats.Variant(MatId.Glow, key, m =>
            {
                m.SetColor(ShaderIds.EmissionColor, AbyssPalette.COLD_LAMP.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, strength);
                m.SetFloat(MinPixelsId, 2.5f);
                m.SetFloat(EnergyConserveId, 1f);
            });
            var b = new InstancedBatch(name, ShaderMeshes.GlowQuad(), glow, ShadowCastingMode.Off, false);
            for (int i = 0; i < points.Count; i++) b.Add(Matrix4x4.TRS(points[i], Quaternion.identity, Vector3.one * diam));
            b.Seal();
            if (!_editMode) InstancedRegistry.Register(b);
            return b;
        }

        /// <summary>
        /// One bracketed rectangular fixture at p on a face with outward normal n (at most 0.435 m proud): wall plate, head
        /// 1.3 x 0.26 x 0.40, a lens strip under the head and one on its lower front edge. level -1: the static hash decides
        /// dark (45 % on a bridge-facing face, 15 % elsewhere) or one of the three intensities; 0..2 forces that intensity.
        /// </summary>
        void HullLamp(MeshBuilder housing, MeshBuilder lens, MeshBuilder dead, Vector3 p, Vector3 n, int index, int level, bool bridgeFace)
        {
            Quaternion q = Quaternion.LookRotation(n, Vector3.up);
            Vector3 up = Vector3.up;
            MeshKit.ChamferBox(housing, p + n * 0.025f, new Vector3(0.5f, 0.6f, 0.07f), 0.01f, q);
            MeshKit.ChamferBox(housing, p + n * 0.22f, new Vector3(1.3f, 0.26f, 0.40f), 0.02f, q);
            bool lit = true;
            if (level < 0)
            {
                uint h = VertexBake.Pcg((uint)index * 0x9E3779B9u ^ (_seed * 0x85EBCA6Bu) ^ 0x1A3Bu);
                if (bridgeFace)
                {
                    // golden-ratio sequence from a seeded start: quasi-random along the rows, dark share within about 1/N of 45 %
                    double u = (_bridgeLampSeq++ * 0.6180339887498949 + (VertexBake.Pcg(_seed ^ 0x5EEDu) & 0xFFFFu) / 65536.0) % 1.0;
                    lit = u >= HULL_LAMP_DEAD_BRIDGE;
                }
                else lit = (h & 0xFFFFu) >= (uint)(HULL_LAMP_DEAD * 65536f);
                level = (int)((h >> 16) % 3u);
            }
            MeshBuilder lb = lit ? lens : dead;
            MeshKit.ChamferBox(lb, p + n * 0.24f - up * 0.143f, new Vector3(1.1f, 0.03f, 0.30f), 0.005f, q);
            MeshKit.ChamferBox(lb, p + n * 0.42f - up * 0.07f, new Vector3(1.1f, 0.07f, 0.03f), 0.005f, q);
            if (bridgeFace) { if (lit) _hullLampsLitBridge++; else _hullLampsDarkBridge++; }
            if (!lit) { _hullLampsDark++; return; }
            _hullLampsLit++;
            _lampCores[level].Add(p + n * 0.5f - up * 0.12f);
            _lampPools[level].Add(p + n * HULL_POOL_OUT - up * HULL_POOL_DROP);
        }

        // ================================================================================================ load paths, walkway, door, stacks (R1-19)

        /// <summary>
        /// The structure that carries and measures the hull (critiques R1-19, R2-08), all persistent skeleton and dark: two deep
        /// girders (4 m flanges, 3 m web, stiffeners every 12 m, ambient fading toward the wall) from the near-flat belt at y -30
        /// (k 20, band -36..-24, inner face z 80) to the hull's -Z face, both ends 0.5 m into the solids, a 45 degree corbel under
        /// each hull end; on the nearer girder a 3.2 m walkway (1.1 m rails, posts every 2 m, ThinLine) to a service door
        /// (1.3 x 2.42) in the hull face; two
        /// hangers (see <see cref="HANGER_BELT_K"/>) from roof plinths to the y 210 belt. Nothing touches the lift corridor
        /// (z 259-289 under the -X chamfer).
        /// </summary>
        void BuildHullStructure(Transform sk)
        {
            var mb = new MeshBuilder();
            float zA = AbyssConst.SHAFT_NEAR_Z + (AbyssConst.SHAFT_APOTHEM - AbyssConst.BELT_INNER_APOTHEM) - 0.5f;   // 79.5
            float zB = AbyssConst.HALL_Z0 + 0.5f;                                                                       // 199.5
            float T = GIRDER_FLANGE_T, zm = 0.5f * (zA + zB), len = zB - zA;
            float webY0 = GIRDER_Y0 + T - 0.05f, webY1 = GIRDER_Y1 - T + 0.05f, webYm = 0.5f * (webY0 + webY1);
            float yHull = AbyssConst.HALL_HULL_BOTTOM_Y;
            for (int g = 0; g < GIRDER_X.Length; g++)
            {
                float gx = GIRDER_X[g];
                MeshKit.ChamferBox(mb, new Vector3(gx, webYm, zm), new Vector3(GIRDER_WEB, webY1 - webY0, len), 0.05f);
                MeshKit.ChamferBox(mb, new Vector3(gx, GIRDER_Y1 - 0.5f * T, zm), new Vector3(GIRDER_FLANGE_W, T, len), 0.04f);
                MeshKit.ChamferBox(mb, new Vector3(gx, GIRDER_Y0 + 0.5f * T, zm), new Vector3(GIRDER_FLANGE_W, T, len), 0.04f);
                for (float z = zA + 0.5f + GIRDER_RIB_PITCH; z < zB - 3f; z += GIRDER_RIB_PITCH)
                    for (int s = -1; s <= 1; s += 2)
                        MeshKit.ChamferBox(mb, new Vector3(gx + s * (0.5f * GIRDER_WEB + 0.145f), webYm, z), new Vector3(0.35f, webY1 - webY0 - 0.04f, 0.5f), 0.02f);
                // corbel: from inside the hull (1.6 m behind the face, 2 m above the bottom) up at 45 degrees into the web
                float za = AbyssConst.HALL_Z0 + 1.6f, ya = yHull + 2f, yc = webY0 + 2f;
                MeshKit.ChamferMember(mb, new Vector3(gx, ya, za), new Vector3(gx, yc, za - (yc - ya)), GIRDER_WEB, 2.2f, 0.04f, Vector3.up);
            }
            // ambient occlusion fading the spans toward the wall (vertex R; G edge wear untouched): 1 at the hull face, GIRDER_WALL_AO at the belt
            for (int i = 0; i < mb.Colors.Count; i++)
            {
                float zv = mb.Positions[i].z;
                if (zv >= AbyssConst.HALL_Z0) continue;
                float t = Mathf.Clamp01((zv - zA) / (AbyssConst.HALL_Z0 - zA));
                Color32 c = mb.Colors[i];
                c.r = (byte)Mathf.RoundToInt(c.r * Mathf.Lerp(GIRDER_WALL_AO, 1f, t));
                mb.Colors[i] = c;
            }
            BeatKit.Emit(mb, sk, "HullGirders", GirderMat(), _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- walkway rails on the nearer girder (full length, ThinLine; 2 m pieces so the widening is per bay)
            float wx = GIRDER_X[0], top = GIRDER_Y1, rr = RAIL_TUBE * 0.5f, rh = AbyssConst.RAIL_H;
            float zr0 = zA + 1.0f, zr1 = AbyssConst.HALL_Z0 + 0.02f;
            int pieces = Mathf.Max(1, Mathf.RoundToInt((zr1 - zr0) / POST_SPACING));
            for (int s = -1; s <= 1; s += 2)
            {
                float x = wx + s * WALK_RAIL_X;
                RailTubeZ(mb, zr0, zr1, top + rh - rr, x, rr, pieces);
                RailTubeZ(mb, zr0, zr1, top + rh * 0.5f, x, rr, pieces);
                for (float z = zr0; z < AbyssConst.HALL_Z0 - 0.3f; z += POST_SPACING)
                    MeshKit.Cylinder(mb, new Vector3(x, top - 0.02f, z), new Vector3(x, top + rh - rr, z), rr, 8, true, true);
            }
            BeatKit.Emit(mb, sk, "HullWalkwayRails", ThinExtMat(), _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- service door in the -Z face at the walkway end: jambs and lintel 0.2 deep, the leaf recessed 8 cm behind them
            float fz = AbyssConst.HALL_Z0, jh = DOOR_H + 0.3f, jx = 0.5f * DOOR_W + 0.105f;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(wx + s * jx, top - 0.05f + 0.5f * jh, fz - 0.08f), new Vector3(0.25f, jh, 0.2f), 0.02f);
            MeshKit.ChamferBox(mb, new Vector3(wx, top + DOOR_H - 0.12f + 0.15f, fz - 0.09f), new Vector3(2f * jx + 0.29f, 0.3f, 0.22f), 0.02f);
            BeatKit.Emit(mb, sk, "HullServiceDoorFrame", _mats[MatId.SteelPainted], _meshes, ShadowCastingMode.Off);
            // leaf z 198.93..199.02 (2 cm into the face), its front 5 cm behind the jamb fronts (198.88)
            float leafZ0 = fz - 0.07f, leafZ1 = fz + 0.02f, ly0 = top - 0.03f, ly1 = ly0 + DOOR_H, lx = 0.5f * DOOR_W + 0.02f;
            MeshKit.ChamferBox(mb, new Vector3(wx, 0.5f * (ly0 + ly1), 0.5f * (leafZ0 + leafZ1)), new Vector3(2f * lx, DOOR_H, leafZ1 - leafZ0), 0.01f);
            BeatKit.Emit(mb, sk, "HullServiceDoorLeaf", _mats[MatId.DoorLeaf], _meshes, ShadowCastingMode.Off);
            // the lamp's light on the leaf (R2-08): a fogged gradient card 1.5 cm in front of the leaf, inside the jambs
            float cz = leafZ0 - 0.015f, cx = 0.5f * DOOR_W - 0.01f;
            CardQuad(mb, new Vector3(wx + cx, ly0 + 0.01f, cz), new Vector3(wx + cx, ly1 - 0.01f, cz), new Vector3(wx - cx, ly1 - 0.01f, cz),
                new Vector3(wx - cx, ly0 + 0.01f, cz), Vector3.back);
            Material doorLit = _mats.Variant(MatId.GradientCard, "b4doorlit", m =>
            {
                m.SetColor(ShaderIds.CardTop, AbyssPalette.Hex(DOOR_LIT_TOP));
                m.SetColor(ShaderIds.CardBottom, AbyssPalette.Hex(DOOR_LIT_BOTTOM));
                m.SetFloat(CardFogId, 1f);
                m.SetFloat(ShaderIds.EmissionStrength, 1f);
            });
            BeatKit.Emit(mb, sk, "HullServiceDoorLight", doorLit, _meshes, ShadowCastingMode.Off);

            // ---- hangers: roof plinth, then one straight 6 x 4 m member up to the belt of level HANGER_BELT_K on the near flat
            for (int g = 0; g < GIRDER_X.Length; g++)
            {
                HangerAxis(GIRDER_X[g], out Vector3 foot, out Vector3 end);
                MeshKit.ChamferBox(mb, new Vector3(foot.x, PlinthBottomY + 0.5f * HANGER_PLINTH.y, HANGER_FOOT_Z), HANGER_PLINTH, 0.05f);
                MeshKit.ChamferMember(mb, foot, end, HANGER_W, HANGER_D, 0.05f, Vector3.up);
            }
            BeatKit.Emit(mb, sk, "HullHangers", GirderMat(), _meshes, ShadowCastingMode.Off, Vector3.zero, true);

            // ---- C1's shadow on the hull (lead_b01 KS2a, R2-08): see C1_SHADOW_Y0
            Vector3 c1 = AbyssConst.COLUMNS[0];
            MeshKit.Cylinder(mb, new Vector3(c1.x, C1_SHADOW_Y0, c1.y), new Vector3(c1.x, C1_SHADOW_Y1, c1.y), c1.z - C1_SHADOW_INSET, 96, true);
            BeatKit.Emit(mb, sk, "HullC1ShadowContinuation", _mats[MatId.ShadowCaster], _meshes, ShadowCastingMode.ShadowsOnly, Vector3.zero, true);
        }

        /// <summary>Bottom of a hanger plinth: 0.4 m into the roof slab (hull top +39), above the hall ceiling (+37.5).</summary>
        static float PlinthBottomY => AbyssConst.HALL_ROOF_Y - 0.4f;

        /// <summary>World y of the hanger's belt (ring level <see cref="HANGER_BELT_K"/>) and z of that belt's inner face on the near flat.</summary>
        public static float HangerBeltY => AbyssConst.RingY(HANGER_BELT_K);
        public static float HangerBeltInnerZ => AbyssConst.SHAFT_NEAR_Z + (AbyssConst.SHAFT_APOTHEM - AbyssConst.BELT_INNER_APOTHEM);
        public const int HangerBeltLevel = HANGER_BELT_K;

        /// <summary>
        /// Axis of the hanger at x: from the foot inside its plinth (y <see cref="HANGER_FOOT_Y"/>, z <see cref="HANGER_FOOT_Z"/>)
        /// along the line through the belt's inner face at y <see cref="HANGER_ENTRY_Y"/>, ending at z <see cref="HANGER_END_Z"/>
        /// inside the belt.
        /// </summary>
        public static void HangerAxis(float x, out Vector3 foot, out Vector3 end)
        {
            foot = new Vector3(x, HANGER_FOOT_Y, HANGER_FOOT_Z);
            float zIn = HangerBeltInnerZ;
            float slope = (HANGER_ENTRY_Y - HANGER_FOOT_Y) / (HANGER_FOOT_Z - zIn);   // rise per metre toward -Z
            end = new Vector3(x, HANGER_ENTRY_Y + slope * (zIn - HANGER_END_Z), HANGER_END_Z);
        }

        /// <summary>One rail tube along z from za to zb as 'pieces' collinear ThinLine cylinders (see <see cref="RailTube"/>).</summary>
        static void RailTubeZ(MeshBuilder mb, float za, float zb, float y, float x, float r, int pieces)
        {
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.Lerp(za, zb, (float)i / pieces), b = Mathf.Lerp(za, zb, (float)(i + 1) / pieces);
                bool caps = i == 0 || i == pieces - 1;
                MeshKit.Cylinder(mb, new Vector3(x, y, a), new Vector3(x, y, b), r, 10, caps, true);
            }
        }

        // ================================================================================================ interior shell

        void BuildInteriorShell(BeatContext ctx, Transform interior)
        {
            Transform shell = BeatKit.Group(interior, "Shell");
            Material conc = Nokey(MatId.ConcreteBoard);
            var mb = new MeshBuilder();
            float t = HULL_WALL_T;
            float xi0 = AbyssConst.HALL_X0 - t, xi1 = AbyssConst.HALL_X1 + t, zi0 = AbyssConst.HALL_Z0 + t, zi1 = AbyssConst.HALL_Z1 - t;
            float yf = AbyssConst.HALL_FLOOR_Y, yc = CEILING_Y;
            float zc = AbyssConst.W[8].z, ow = AbyssConst.HALL_OPENING_W * 0.5f;
            float hz0 = zc - ow, hz1 = zc + ow, hy0 = OPENING_Y0, hy1 = OPENING_Y0 + AbyssConst.HALL_OPENING_H;

            // walls and ceiling
            mb.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            Face(mb, new Vector3(xi0, 0f, 0f), Vector3.forward, Vector3.up, Vector3.left, zi0, zi1, yf, yc, LAT_Z0, LAT_Z, LAT_Y0, LAT_XY,
                true, hz0, hz1, hy0, hy1);
            Face(mb, new Vector3(xi1, 0f, 0f), Vector3.forward, Vector3.up, Vector3.right, zi0, zi1, yf, yc, LAT_Z0, LAT_Z, LAT_Y0, LAT_XY,
                true, hz0, hz1, hy0, hy1);
            Face(mb, new Vector3(0f, 0f, zi0), Vector3.right, Vector3.up, Vector3.forward, xi1, xi0, yf, yc, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
            Face(mb, new Vector3(0f, 0f, zi1), Vector3.right, Vector3.up, Vector3.back, xi1, xi0, yf, yc, LAT_X0, LAT_XY, LAT_Y0, LAT_XY);
            Face(mb, new Vector3(0f, yc, 0f), Vector3.right, Vector3.forward, Vector3.down, xi1, xi0, zi0, zi1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);

            // pilasters on the side walls every 10 m (u = 5 + 10 k) and on the end walls under the truss ends; corbels at v = +/-5
            _occ.Clear();
            float h = yc - yf + 0.1f, ym = (yf + yc) * 0.5f;
            for (int k = 0; k <= 10; k++)
            {
                float x = X(5f + 10f * k);
                for (int s = 0; s < 2; s++)
                {
                    float zf = s == 0 ? zi0 : zi1, sz = s == 0 ? 1f : -1f;
                    Vector3 c = new Vector3(x, ym, zf + sz * (PILASTER_D * 0.5f - 0.01f));
                    MeshKit.ChamferBox(mb, c, new Vector3(PILASTER_W, h, PILASTER_D + 0.02f), 0.03f);
                    _occ.Add(new Bounds(c - HULL_ORIGIN, new Vector3(PILASTER_W, h, PILASTER_D)));
                }
            }
            for (int e = 0; e < 2; e++)
            {
                float xf = e == 0 ? xi0 : xi1, sx = e == 0 ? -1f : 1f;
                for (int j = 1; j <= 5; j++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 c = new Vector3(xf + sx * (PILASTER_D * 0.5f - 0.01f), ym, zc + s * (5f + 10f * j));
                        MeshKit.ChamferBox(mb, c, new Vector3(PILASTER_D + 0.02f, h, PILASTER_W), 0.03f);
                        _occ.Add(new Bounds(c - HULL_ORIGIN, new Vector3(PILASTER_D, h, PILASTER_W)));
                    }
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferBox(mb, new Vector3(xf + sx * 0.74f, 27.75f, zc + s * 5f), new Vector3(1.5f, 3.5f, PILASTER_W), 0.03f);
            }
            // AO on the walls: floor and ceiling slabs, the walls themselves, the pilasters
            _occ.Add(Shift(Bx(-87f, -33f, 269f, 112f, 24f, 142f), -HULL_ORIGIN));
            _occ.Add(Shift(Bx(-87f, 38.25f, 269f, 112f, 1.5f, 142f), -HULL_ORIGIN));
            _occ.Add(Shift(Bx(-87f, 8f, AbyssConst.HALL_Z0 + 0.5f, 112f, 60f, 1f), -HULL_ORIGIN));
            _occ.Add(Shift(Bx(-87f, 8f, AbyssConst.HALL_Z1 - 0.5f, 112f, 60f, 1f), -HULL_ORIGIN));
            _occ.Add(Shift(Bx(AbyssConst.HALL_X0 - 0.5f, 8f, 269f, 1f, 60f, 142f), -HULL_ORIGIN));
            _occ.Add(Shift(Bx(AbyssConst.HALL_X1 + 0.5f, 8f, 269f, 1f, 60f, 142f), -HULL_ORIGIN));
            VertexBake.BakeAO(mb, _occ, 5f, 12, _seed ^ 0x4A11u, 0.8f, 0);
            Mesh walls = BeatKit.Bake(mb, "HallWalls", _meshes);
            BeatKit.Renderer(shell, "HallWalls", walls, conc, HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off, true);

            // floor with baked contact darkening around everything standing on it
            mb.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            Face(mb, new Vector3(0f, yf, 0f), Vector3.right, Vector3.forward, Vector3.up, xi1, xi0, zi0, zi1, LAT_X0, LAT_XY, LAT_Z0, LAT_Z);
            _occ.Clear();
            CollectFloorOccluders(_occ);
            for (int i = 0; i < _occ.Count; i++) _occ[i] = Shift(_occ[i], -HULL_ORIGIN);
            VertexBake.BakeAO(mb, _occ, 9f, 20, _seed ^ 0xF100u, 0.9f, 0);
            Mesh floor = BeatKit.Bake(mb, "HallFloor", _meshes);
            _hallFloor = BeatKit.Renderer(shell, "HallFloor", floor, FloorMat(), HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off, true);
        }

        /// <summary>World boxes of everything that stands on the hall floor (AO occluders for the floor bake).</summary>
        void CollectFloorOccluders(List<Bounds> dst)
        {
            float t = HULL_WALL_T;
            dst.Add(Bx(AbyssConst.HALL_X0 - 0.5f * t, 8f, 269f, t, 60f, 142f));
            dst.Add(Bx(AbyssConst.HALL_X1 + 0.5f * t, 8f, 269f, t, 60f, 142f));
            dst.Add(Bx(-87f, 8f, AbyssConst.HALL_Z0 + 0.5f * t, 112f, 60f, t));
            dst.Add(Bx(-87f, 8f, AbyssConst.HALL_Z1 - 0.5f * t, 112f, 60f, t));
            for (int k = 0; k <= 10; k++)
            {
                float x = X(5f + 10f * k);
                dst.Add(Bx(x, 8f, AbyssConst.HALL_Z0 + t + 0.5f * PILASTER_D, PILASTER_W, 60f, PILASTER_D));
                dst.Add(Bx(x, 8f, AbyssConst.HALL_Z1 - t - 0.5f * PILASTER_D, PILASTER_W, 60f, PILASTER_D));
            }
            for (int k = 0; k < 4; k++)
            {
                if (!_bankBuilt[k]) continue;
                Matrix4x4 m = BankMatrix(k);
                CollectBankFloorBoxes(m, dst);
            }
            CollectCatwalkFloorBoxes(dst);
        }

        // ================================================================================================ trusses

        void BuildTrusses(BeatContext ctx, Transform interior, bool full)
        {
            var mb = new MeshBuilder();
            mb.Push(Matrix4x4.Translate(-HULL_ORIGIN));
            float zc = AbyssConst.W[8].z;
            float xa = AbyssConst.HALL_X0 - 0.3f, xb = AbyssConst.HALL_X1 + 0.3f;        // 0.7 m into both end walls
            float yTop = CEILING_Y - 0.3f;                                                // top flange 0.3 m inside the roof slab
            float yBot = yTop - TRUSS_DEPTH;
            int count = full ? 12 : 6;
            float spacing = full ? 10f : 20f;
            for (int i = 0; i < count; i++)
            {
                float v = -0.5f * spacing * (count - 1) + spacing * i;
                MeshKit.WarrenTruss(mb, new Vector3(xa, yTop, zc + v), new Vector3(xb, yTop, zc + v), TRUSS_DEPTH, TRUSS_BAYS, TRUSS_MEMBER,
                    Vector3.up, TrussMemberShape.IBeam, true, true);
            }
            _trussCount = count;
            // bottom ties at the bottom nodes and purlins at the inner top nodes, wall to wall across the width
            float zw0 = AbyssConst.HALL_Z0 + HULL_WALL_T - 0.3f, zw1 = AbyssConst.HALL_Z1 - HULL_WALL_T + 0.3f;
            for (int k = 1; k < TRUSS_BAYS; k++)
            {
                float x = xa + (xb - xa) * k / TRUSS_BAYS;
                if ((k & 1) == 1)
                    MeshKit.IBeam(mb, new Vector3(x, yBot, zw0), new Vector3(x, yBot, zw1), 0.5f, 0.4f, 0.03f, 0.05f, Vector3.up, true);
                else
                    MeshKit.IBeam(mb, new Vector3(x, CEILING_Y - 0.35f, zw0), new Vector3(x, CEILING_Y - 0.35f, zw1), 0.8f, 0.5f, 0.04f, 0.06f, Vector3.up, true);
            }
            Mesh m = BeatKit.Bake(mb, "RoofTrusses", _meshes);
            _trusses = BeatKit.Renderer(interior, "RoofTrusses", m, TrussMat(), HULL_ORIGIN, Quaternion.identity, ShadowCastingMode.Off, true);
        }

        // ================================================================================================ exit proxy (14.6)

        /// <summary>
        /// Cold gradient card beyond the parked platform (x = -176, facing the hall): approximates the shaft fog seen through the
        /// exit (FogSky of the hall rows: #10141A at the bottom of the view cone to #1D262E at its top) and closes the shaft view
        /// while the hall far plane is 600 m. It covers the view cone through the 20 x 24 opening from every catwalk point up to
        /// u = 90.5 (d 420); it dissolves over d 405-420.
        /// </summary>
        void BuildExitProxy(BeatContext ctx, Transform interior)
        {
            Material m = _mats.Variant(MatId.GradientCard, "b4exitproxy", mat =>
            {
                mat.SetColor(ShaderIds.CardTop, AbyssPalette.Hex(0x1D262E));
                mat.SetColor(ShaderIds.CardBottom, AbyssPalette.Hex(0x10141A));
                mat.SetFloat(CardFogId, 1f);
                mat.SetFloat(ShaderIds.EmissionStrength, EXIT_PROXY_GAIN);
            });
            var mb = new MeshBuilder();
            float x = EXIT_PROXY_X, zc = AbyssConst.W[8].z;
            CardQuad(mb, new Vector3(x, EXIT_PROXY_Y0, zc + EXIT_PROXY_HALF_W), new Vector3(x, EXIT_PROXY_Y1, zc + EXIT_PROXY_HALF_W),
                new Vector3(x, EXIT_PROXY_Y1, zc - EXIT_PROXY_HALF_W), new Vector3(x, EXIT_PROXY_Y0, zc - EXIT_PROXY_HALF_W), Vector3.right);
            _exitProxy = BeatKit.Emit(mb, interior, "ExitProxyCard", m, _meshes, ShadowCastingMode.Off);
        }
    }

}
