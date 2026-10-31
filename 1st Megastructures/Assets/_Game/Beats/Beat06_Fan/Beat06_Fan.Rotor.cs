using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 6 full tier, part 1: the fan (14.5.6, 14.7.4). Frames: the static parts are authored around the fan centre
    /// (-45, -317, 50) with world axes ("fan frame"); the rotor parts in the rotor frame (same origin, rotated about +Z by
    /// theta). Lathes revolve about their local +Y, pushed onto +Z with Euler(90, 0, 0) (local x -> x, y -> z, z -> -y).
    /// </summary>
    public sealed partial class Beat06_Fan
    {
        // ---------------------------------------------------------------- housing, collar, bore (world z)
        const float HOUSING_Z_BACK = 24f, HOUSING_Z_FRONT = 59f;       // ring body: front face 1 m behind the near flat
        const float HOUSING_R_OUTER = 149.95f;                           // 5 cm inside the R 150 lining (back-to-back, hidden)
        const float HOUSING_CHAMFER = 0.6f;
        const int HOUSING_SEGMENTS = 256;                                // facet sagitta 2 cm at R 138 (tip clearance stays >= 4 cm)
        // Trim collar on the near flat: covers the raw wall / lining corner with a bevelled edge, 1.5 m outside the bore only.
        // Beat02Rings gaps the near-flat ribs and belts (k 23..27) to the bore chord + 4 m at the band edge nearest the fan
        // centre; the binding level is k 27 (band top 130 m below the centre: gap half 78.83, collar chord 77.80, 1.04 clear),
        // and the visible rim stays 303 m across (housing 300) with its lower edge 18.5 m above the walkway (Beat06Tests).
        const float COLLAR_R0 = 146f, COLLAR_R1 = AbyssConst.FAN_R_OUT + 1.5f;
        const float COLLAR_Z_BACK = 58.8f, COLLAR_Z_FRONT = 62f, COLLAR_CHAMFER = 0.4f;

        // ---------------------------------------------------------------- rotor (z relative to the fan plane z = 50)
        const float HUB_ZL_BACK = -9f, HUB_ZL_FRONT = 9f, HUB_CHAMFER = 1.4f;
        const float HUB_FRONT_STEP_R = 34.6f, HUB_NOSE_R = 34f, HUB_NOSE_ZL0 = 9.6f, HUB_NOSE_ZL1 = 17.5f;
        const int HUB_SEGMENTS = 96, HUB_NOSE_STEPS = 10;
        const float ROOT_COLLAR_R = 7.2f, ROOT_COLLAR_BASE = 39f, ROOT_COLLAR_TOP = 42.4f, ROOT_COLLAR_CHAMFER = 0.5f;
        const int ROOT_COLLAR_SEGMENTS = 32;
        const float HUB_BOLT_R = 36.6f, HUB_BOLT_AF = 0.55f, ROOT_BOLT_RHO = 6.4f, ROOT_BOLT_AF = 0.4f;
        const int HUB_BOLTS = 48, ROOT_BOLTS = 16;
        const float ROOT_BOLT_CLEAR_DEG = 28f;                           // no bolt within 28 degrees of the root chord line

        // ---------------------------------------------------------------- stator (downstream of the rotor, Face side)
        const float NACELLE_R = 36f, NACELLE_ZL_TIP = -40f, NACELLE_ZL_DRUM = -25f, NACELLE_ZL_FRONT = -9.8f, NACELLE_CHAMFER = 0.6f;
        const int NACELLE_SEGMENTS = 64, NACELLE_TAIL_STEPS = 8;
        const float STRUT_CHORD = 7f, STRUT_THICK = 1.6f, STRUT_ZL = -18f, STRUT_R0 = 34f, STRUT_R1 = 140.5f;
        const int STRUT_SIDE_POINTS = 8;
        const float ANCHOR_DX = 6f, ANCHOR_Y = -455f, ANCHOR_Z = 59.9f;   // 14.5.6: anchors at (x = -45 +/- 6, y = -455) on the housing lip
        const float ANCHOR_W = 2.0f, ANCHOR_H = 2.8f, ANCHOR_D = 2.2f, ANCHOR_PIN_R = 0.15f, ANCHOR_ATTACH_PITCH = 0.28f;

        // ---------------------------------------------------------------- far-wall lighthouse patches (14.5.6)
        // The patch quad stands in front of the far flat's ribs and belts (they protrude 6 / 20 m): at 0.75 m the -510 rib
        // hid a horizontal band of every lower patch, which ended in straight hard edges (R2-15, cue_b6_lookback y 479). The
        // shader projects each fragment back along the light, so 21 m forward shifts the patches by only 6.8 m on the wall;
        // the light now also lies on the rib and belt fronts, as projected light would.
        const float PATCH_WALL_GAP = 21f, PATCH_FEATHER = 6f, PATCH_STRETCH = 1.05f, PATCH_MARGIN = 20f;   // margin: 12 px feather + 6.8 m shift
        // Linear radiance before fog. Seen from the gantry the far wall is 500-700 m away in the beat-6 haze (D50 300:
        // transmittance about 0.04 at 600 m), so f03's 0.3 lifted the wall by only about 6 levels (0.024) and the patches read
        // as faint smudges (R2-28); 14.5.6 asks for +0.08 luma. In the clearer lift shaft (D50 380: transmittance about 0.3 at
        // 490 m) the same radiance would be 8x brighter, so the patches carry PATCH_RIDE_SCALE of it until the 5-to-6 haze
        // window (ride seconds 110-120, the beams' chamber blend): the ride view of the far wall keeps the f03 value (0.3).
        // fan_r13 at 1.2: B6_freelook_back wall p50 0.224, patches 0.30-0.335 (+0.08-0.11, read as bright dashes); 0.9 aims
        // at the spec +0.08 (linear lift x 0.75).
        const float PATCH_STRENGTH = 0.9f;
        const float PATCH_RIDE_SCALE = 0.3333f;   // the ride keeps the f03 radiance 0.3
        // Minimum screen feather of the patch edges (px of the wall footprint, _BeamFeather.y): BEAM_MIN_PX is 12; the 6 m
        // feather is 8 px at 600 m, so the patches use 16 px for a soft lighthouse smudge with margin (R2-15).
        const float PATCH_FEATHER_PX = 16f;

        /// <summary>Wall-patch radiance for the ride second (PATCH_RIDE_SCALE in the lift shaft, full in the fan chamber).</summary>
        public static float PatchStrength(float rideSeconds) => PATCH_STRENGTH * Mathf.Lerp(PATCH_RIDE_SCALE, 1f, BeamChamber(rideSeconds));

        // ---------------------------------------------------------------- rotor surface (R2-10, R2-13, R2-17)
        // Contre-jour from the lift (R2-13): the beat-5 sun (yaw 205, pitch -72) front-lit the blades to a mid grey (KS5b 0.18),
        // so the 300 m fan read as a dark desk fan in front of a paper card. The Key is dimmed on the rotor (hub, collars,
        // blades, bolts) while the shaft suns are the key: the blades keep the ambient, the FogSky environment and the fog in
        // front of them and read as dark shapes against the lit bore. At a full mask (0, lead_b01) they were flat cut-outs
        // (blade 0.106, std 0.004: only the fog veil of 220 m of haze) and the hub a flat black disc; ROTOR_KEY_SHAFT keeps
        // part of the sun so the hub drum, root collars and the blade twist keep their modelling (R2-13 lead note: the blades
        // must still read as huge 3-D objects). The mask returns to 1 inside
        // the tunnel (E of d 674.5-684.5, the fan out of sight), so in beat 7 the exterior sun lights the blade backs seen
        // through the bore (KS7d). In beat 6 the fan key travels +Z through the bore and never reaches the downstream faces
        // the gantry sees, so the mask changes nothing there.
        const float ROTOR_KEY_SHAFT = 0.5f;
        static float RotorKeyE0 => ProgressTables.EPost(AbyssConst.BEAT_START_D[6] + 6f);
        static float RotorKeyE1 => ProgressTables.EPost(AbyssConst.BEAT_START_D[6] + 16f);
        // Painted blades read as speckled with the weathered-steel rust blobs and paw-print stains (R2-10), and their rounded
        // leading edge (0.3-0.7 m nose radius, sub-pixel beyond about 250 m) caught the exterior sun and the grazing FogSky
        // reflection as a hot 1-px line that crawls as the fan turns (KS7d, R2-17). Rotor paint: no rust or stain blobs (the
        // recipe's grime runs and patches stay), a rough dielectric finish (roughness 0.8, metallic 0.05: the GGX lobe and the
        // grazing environment term of a sub-pixel nose drop about 4x); the blade wear stays below the chamfer mark
        // (FanBlade.MAX_WEAR), so the sub-pixel bevel resolve never bends the nose normal onto a sun-facing axis face.
        // lead_b01 KS7d still showed the hot 1-px line (px 495-625, y 405-520, about 190 on 55): the erosion strip and the
        // trailing edge (vertex G 0.9 / 0.55) turn into bare steel (metallic 0.85, roughness 0.38 in AbRecipeSteel) and mirror
        // the bright beat-7 sky on a strip under 1.5 px. ROTOR_WEAR scales that wear (_WearAmount) so no edge strip is more
        // than slightly bare: the erosion shield stays as a faintly lighter, rough band. fan_r31 at 0.3: the KS7d line fell
        // from 167-174 to 83-88 levels on a 40-45 blade; 0.1 takes the bare-steel term out of it.
        const float ROTOR_ROUGHNESS = 0.8f, ROTOR_METALLIC = 0.05f, ROTOR_RUST = 0f, ROTOR_WEAR = 0.1f;
        // The stator (nacelle, struts, stay anchors) gets the same rough dielectric paint, and the nacelle and struts inside the
        // bore an AO of STATOR_AO / 255 (R2-17, KS7d).
        const float STATOR_ROUGHNESS = 0.8f, STATOR_METALLIC = 0.05f;
        const byte STATOR_AO = 200;

        /// <summary>Stator paint: roughness, metallic (the base SteelPainted has 0.55 / 0.2).</summary>
        public static Vector2 StatorPaint => new Vector2(STATOR_ROUGHNESS, STATOR_METALLIC);

        /// <summary>Key-light mask of the rotor at extended progress E (ROTOR_KEY_SHAFT up to the tunnel, 1 in beat 7).</summary>
        public static float RotorKeyMask(float e)
        {
            float e0 = RotorKeyE0, e1 = RotorKeyE1;
            if (e <= e0) return ROTOR_KEY_SHAFT;
            if (e >= e1) return 1f;
            return Mathf.Lerp(ROTOR_KEY_SHAFT, 1f, BeatKit.Smootherstep((e - e0) / (e1 - e0)));
        }

        /// <summary>Rotor paint (shared by hub, root collars, blades and bolts): rust, roughness, metallic.</summary>
        public static Vector3 RotorPaint => new Vector3(ROTOR_RUST, ROTOR_ROUGHNESS, ROTOR_METALLIC);

        /// <summary>Rotor edge-wear scale (_WearAmount; the base weathered steel has 1).</summary>
        public static float RotorWear => ROTOR_WEAR;

        /// <summary>Rotor key mask in the shaft (bridge, ledge, hall, lift ride and gantry).</summary>
        public static float RotorKeyShaft => ROTOR_KEY_SHAFT;

        /// <summary>Minimum screen feather of the far-wall patches (px, _BeamFeather.y of the patch material).</summary>
        public static float PatchFeatherPx => PATCH_FEATHER_PX;

        // ---------------------------------------------------------------- beam look (14.7.4; round 1 R1-10, look-dev slot "fan")
        // KS6 and KS5b both stand INSIDE the R 70-130 annulus about 215-240 m down the light, looking back up it, so every
        // wedge that sweeps past the eye is seen end-on (f01 with no shaping: KS6 mean 0.51). f02 cut the energy instead
        // (exp(-s / 60) along the light): the spokes died inside the fan disc and the gaps read as a flat card (R1-10).
        // Round 1 moves the light to where it reads as spokes. Fan chamber (beat 6, KS6): a 40 m fade-in from the fan plane
        // (the fan's own blade / gap silhouette stays crisp), full density from about 40 to 60 m (where the spokes cross the
        // housing ring), a fade-out over 60-130 m as they run out over the darker walls, ending well before the gantry eye
        // heights in the annulus (the wedge around the viewer is dark, the end-on chord is short), an 18 m radial feather (a soft
        // peak at mid-span) and a 30 m saturation length (the end-on glow under the vanishing point stays near one spoke's
        // level). The dark gaps between the spokes come from the key shadow (mode 3, below).
        // Look-dev fan_r01 (0.0018, fade-out 120-210): KS6 0.233 / 16:10 0.241 with the frame at about 0.194 without beams
        // (inverted with a CPU mirror of the shader); the spokes read but the side wedges near the eye washed the walls.
        // fan_r02 (0.0017, 90-170): KS6 0.219 / 16:10 0.227, QA spoke contrast 0.033 (advisory target 0.04). The light is
        // now packed into the first 130 m and the key shadow runs out past the tips (below): mirror prediction KS6 about
        // 0.209 / 16:10 0.216 with a spoke contrast of about 0.045.
        const float BEAM_DENSITY = 0.0026f;     // _EmissionStrength: radiance per metre of full density (x key colour)
        const float BEAM_PHASE_G = 0.1f;        // HG lobe: mildly forward
        const float BEAM_EXTINCTION = 0f;       // m: exp(-s / L) along the light, off: the axial window shapes the spokes
        const float BEAM_NEAR0 = 12f, BEAM_NEAR1 = 60f;   // m: near-camera density fade (camera distance)
        const float BEAM_SATURATION = 30f;      // m: chord saturation length (end-on chords roll off)
        const float BEAM_FADE_IN = 40f;         // m: axial fade-in from the fan plane (_BeamFeather.w)
        const float BEAM_CHORD_FEATHER = 18f;   // m: radial feather at R 70 / 130 (_BeamFeather.z; the base material has 8)
        const float BEAM_OUT0 = 60f, BEAM_OUT1 = 130f;    // m along the light: axial fade-out window (_BeamAxial)

        // Lift ride (ride seconds before the 110-120 haze window): the deck descends INSIDE the annulus 220-240 m down the
        // light, so every wedge is seen from within its own ring and the chamber look laid a veil over the whole fan (fan_r01
        // KS5b at the R1-04 pose: 0.193 against the beat-5 band 0.06-0.16, beam-free about 0.149). In the clearer shaft air
        // only the first tens of metres beyond the gaps are drawn (near fade 170-225 m from the eye, a 4 m fade-in), so the
        // light spills out of each gap as a short soft spoke and the fan keeps its contrast. The look blends to the chamber
        // values with the density over ride seconds 110-120.
        // R2-13: at 170-225 m only the first ~50 m beyond the gaps were drawn, so KS5b showed no rays and the gaps read as a
        // flat card (lead_b01). 125-200 m draws the first ~95 m (the axial window ends at 60-130 m anyway): seen from the
        // lift below the axis, the rays fan out of the gaps away from the light's vanishing point (about 12 degrees below
        // the hub), the lower ones toward the deck, while the stretch within 125 m of the eye (the end-on wash) stays dark.
        const float BEAM_RIDE_NEAR0 = 125f, BEAM_RIDE_NEAR1 = 200f, BEAM_RIDE_FADE_IN = 4f;

        // ---------------------------------------------------------------- key shadow (BeamWedge mode 3, R1-10 / R1-11)
        // The blades shadow the key: the 14.5.6 "scatter 0.35 toward -Z" lobe of the fog is lit haze everywhere, so without
        // a shadow the haze between the light wedges is as bright as the haze around them and the spokes have no dark gaps.
        // Mode 3 removes that lobe's in-scatter from the stretch of each view ray inside a blade shadow: 12 prisms on the
        // blade centres (30 k degrees in the rotor frame), 18 degrees wide (the 30 degree pitch minus the 12 degree light
        // wedge), 300 m along the light, from the axis (the hub's own shadow) out to R 250: past the tips the housing and
        // the near flat shadow the key everywhere, but carving only the blade sectors there keeps the lit sectors running
        // on as spokes beyond the ring instead of ending in a uniformly lit halo (a 30 m radial feather, so neither end of
        // the shadow has an edge; a 4 m minimum side feather softens the sweep of a sector edge across the eye). Only while
        // the fan key is the active key (the shader gates by the light direction), so the beat-5 sun lobe is never touched.
        const float SHADOW_R0 = 4f, SHADOW_R1 = 250f;
        const float SHADOW_SIDE_FEATHER = 4f, SHADOW_CHORD_FEATHER = 30f;
        const float SHADOW_HALF_DEG = 0.5f * (360f / AbyssConst.FAN_BLADES - AbyssConst.BEAM_WIDTH_DEG);
        const float SHADOW_STRENGTH = 1f;        // fraction of the lobe in-scatter removed inside a shadow (<= 1)
        const float SHADOW_NEAR1 = 20f;          // m: near-camera fade of the shadow (its weight dF/dt is ~0 there anyway)

        /// <summary>
        /// Beam density during the lift ride relative to the fan chamber. The beams are in-scatter of the shaft haze: the lift
        /// shaft (14.2 beat-5 row: D50 380 m, key scatter 0.10) is clearer than the fan chamber (beat 6: D50 300 m, scatter
        /// 0.35 toward -Z), so the beams carry 0.25 of their beat-6 density (about the 0.10 / 0.35 scatter ratio) in the ride
        /// look until the 5-to-6 haze window of 14.6 (ride seconds 110-120, the fog and exposure window) and reach full
        /// density and the chamber look with it.
        /// </summary>
        const float BEAM_RIDE_HAZE = 0.25f;
        const float BEAM_HAZE_RIDE0 = 110f, BEAM_HAZE_RIDE1 = 120f;
        // R2-13 (R1-10): while the fan is the subject of the ride (gaze 65-110 s, KS5b at 75 s) the beams carry at least half
        // of the chamber density, so soft rays spill out of the 12 gaps toward the deck (contre-jour). The step from 0.25 runs
        // over ride seconds 50-60, while the gaze passes C1 (45-65 s) and the fan is out of frame.
        // fan_r31 at 0.5 (near fade 125-200): the rays fan out of every gap but add only +0.02-0.06 over the outer blades and
        // the housing (KS5b mean 0.204); 0.8 makes the sunburst read while the frame stays inside the D8 band (0.09-0.22).
        const float BEAM_RIDE_HAZE_FAN = 0.8f;
        const float BEAM_FAN_RIDE0 = 50f, BEAM_FAN_RIDE1 = 60f;

        /// <summary>Beam density scale in the lift shaft before the fan is the subject (up to ride second 50).</summary>
        public static float BeamRideHaze => BEAM_RIDE_HAZE;

        /// <summary>Beam density scale of the ride while the fan is the subject (ride seconds 60-110, KS5b).</summary>
        public static float BeamRideHazeFan => BEAM_RIDE_HAZE_FAN;

        /// <summary>Ride beam scale before the chamber blend: 0.25, smootherstep to 0.5 over ride seconds 50-60.</summary>
        static float RideHaze(float rideSeconds)
        {
            if (rideSeconds <= BEAM_FAN_RIDE0) return BEAM_RIDE_HAZE;
            if (rideSeconds >= BEAM_FAN_RIDE1) return BEAM_RIDE_HAZE_FAN;
            return Mathf.Lerp(BEAM_RIDE_HAZE, BEAM_RIDE_HAZE_FAN, BeatKit.Smootherstep((rideSeconds - BEAM_FAN_RIDE0) / (BEAM_FAN_RIDE1 - BEAM_FAN_RIDE0)));
        }

        /// <summary>Beam density per metre in the fan chamber (full haze); the material carries this x <see cref="BeamHaze"/>.</summary>
        public static float BeamDensityFull => BEAM_DENSITY;

        /// <summary>Ride-to-chamber blend of the beam look: 0 up to ride second 110, smootherstep to 1 by 120, 1 after the ride.</summary>
        public static float BeamChamber(float rideSeconds)
        {
            if (rideSeconds >= BEAM_HAZE_RIDE1) return 1f;
            if (rideSeconds <= BEAM_HAZE_RIDE0) return 0f;
            return BeatKit.Smootherstep((rideSeconds - BEAM_HAZE_RIDE0) / (BEAM_HAZE_RIDE1 - BEAM_HAZE_RIDE0));
        }

        /// <summary>
        /// Beam density scale for the ride seconds: 0.25 up to ride second 50, 0.5 from 60 (the fan in view), smootherstep to 1
        /// over 110-120 (the 5-to-6 haze window), 1 after the ride.
        /// </summary>
        public static float BeamHaze(float rideSeconds) => Mathf.Lerp(RideHaze(rideSeconds), 1f, BeamChamber(rideSeconds));

        /// <summary>_BeamShape at a ride second: axial extinction, near-camera fade start / end, saturation length (m).</summary>
        public static Vector4 BeamShapeAt(float rideSeconds)
        {
            float w = BeamChamber(rideSeconds);
            return new Vector4(BEAM_EXTINCTION, Mathf.Lerp(BEAM_RIDE_NEAR0, BEAM_NEAR0, w), Mathf.Lerp(BEAM_RIDE_NEAR1, BEAM_NEAR1, w), BEAM_SATURATION);
        }

        /// <summary>_BeamFeather at a ride second: min side feather (m), side feather (px), chord feather (m), axial fade-in (m).</summary>
        public static Vector4 BeamFeatherAt(float rideSeconds) =>
            new Vector4(2f, 12f, BEAM_CHORD_FEATHER, Mathf.Lerp(BEAM_RIDE_FADE_IN, BEAM_FADE_IN, BeamChamber(rideSeconds)));

        /// <summary>_BeamAxial of the fan beams: axial fade-out window start / end (m along the light).</summary>
        public static Vector4 BeamAxial => new Vector4(BEAM_OUT0, BEAM_OUT1, 0f, 0f);

        static readonly Quaternion YToZ = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>World position of the cable attachment for portal i on side s (-1 = west, +1 = east): one row on the anchor face.</summary>
        static Vector3 AnchorAttach(int i, int s)
        {
            float x = AbyssConst.W[14].x + s * ANCHOR_DX;
            float y = ANCHOR_Y + (3.5f - i) * ANCHOR_ATTACH_PITCH;   // the nearest (steepest) cable lowest, so no two cables cross
            return new Vector3(x, y, ANCHOR_Z + ANCHOR_D * 0.5f - 0.05f);
        }

        static Vector4 BeamDirV() => (Vector4)AbyssConst.Dir(0f, -AbyssConst.BEAM_PITCH_DEG);

        static Vector4 BeamParamsV() => new Vector4(AbyssConst.BEAM_R0, AbyssConst.BEAM_R1,
            0.5f * AbyssConst.BEAM_WIDTH_DEG * Mathf.Deg2Rad, AbyssConst.BEAM_LENGTH);

        /// <summary>_BeamParams of the key shadow: R range hub to tip, half width (rad), length (as the light wedges).</summary>
        internal static Vector4 ShadowParamsV() => new Vector4(SHADOW_R0, SHADOW_R1, SHADOW_HALF_DEG * Mathf.Deg2Rad, AbyssConst.BEAM_LENGTH);

        /// <summary>Outer radius of the key-shadow prisms (the mesh bounds follow it).</summary>
        public static float ShadowOuterRadius => SHADOW_R1;

        /// <summary>Fraction of the fog's key lobe removed inside a blade shadow (mode-3 _EmissionStrength).</summary>
        public static float ShadowStrength => SHADOW_STRENGTH;

        void BuildFanFull(BeatContext ctx, MeshBuilder mb, bool full, List<DetailTier> tiers)
        {
            MaterialLibrary mats = ctx.Mats;
            Vector3 fc = AbyssConst.FAN_CENTRE;
            Transform fan = BeatKit.Group(_geo, "Fan");
            var profile = new List<Vector2>(32);

            // M_ConcreteBoard everywhere (14.5.6): its world projection suits these horizontal-axis (Z) cylinders (boards run
            // along z on the side walls, planar on the crown and invert, blended over |N.y| 0.6-0.8). The ConcreteCyl recipe
            // assumes a vertical column (v = world y, horizontal Tu) and would collapse into stripes here.
            Material concrete = mats[MatId.ConcreteBoard];
            Material rotorMat = mats.Variant(MatId.SteelWeathered, "b6rotor", m =>
            {
                m.SetFloat(FanIds.Rust, ROTOR_RUST);
                m.SetFloat(ShaderIds.Roughness, ROTOR_ROUGHNESS);
                m.SetFloat(ShaderIds.Metallic, ROTOR_METALLIC);
                m.SetFloat(FanIds.WearAmount, ROTOR_WEAR);
                m.SetFloat(FanIds.KeyLightMask, RotorKeyMask(GameState.E));
            });
            _rotorMat = rotorMat;
            _rotorKey = -1f;
            Material statorMat = mats.Variant(MatId.SteelPainted, "b6stator", m =>
            {
                m.SetFloat(ShaderIds.Roughness, STATOR_ROUGHNESS);
                m.SetFloat(ShaderIds.Metallic, STATOR_METALLIC);
            });
            float rOut = AbyssConst.FAN_R_OUT, rIn = AbyssConst.FAN_R_INNER;

            // ---- 1 bore lining through the web (128-gon shared with the wall holes and the bore card), seen from inside
            profile.Add(new Vector2(rOut, -BORE_OVERLAP));
            profile.Add(new Vector2(rOut, AbyssConst.WEB + BORE_OVERLAP));
            MeshKit.Lathe(mb, profile, BeatKit.BORE_SEGMENTS, MeshKit.HardEdgeAngleDeg, 0f, 360f, false, true);
            Mesh bore = BeatKit.Bake(mb, "BoreLining", _meshes);
            BeatKit.Renderer(fan, "BoreLining", bore, concrete, new Vector3(fc.x, fc.y, AbyssConst.W[16].z), YToZ, ShadowCastingMode.On, true);

            // ---- 2 housing ring R 150 / 138: the inner liner face and the body (both board-formed)
            float zb = HOUSING_Z_BACK - fc.z, zf = HOUSING_Z_FRONT - fc.z, ch = HOUSING_CHAMFER;
            profile.Clear();
            profile.Add(new Vector2(rIn, zb + ch));
            profile.Add(new Vector2(rIn, zf - ch));
            MeshKit.Lathe(mb, profile, HOUSING_SEGMENTS, MeshKit.HardEdgeAngleDeg, 0f, 360f, false, true);
            Mesh liner = BeatKit.Bake(mb, "FanHousingLiner", _meshes);
            BeatKit.Renderer(fan, "FanHousingLiner", liner, concrete, fc, YToZ, ShadowCastingMode.On, true);

            profile.Clear();   // counter-clockwise in (r, z): back face, outer face, front face (outward normals)
            profile.Add(new Vector2(rIn, zb + ch));
            profile.Add(new Vector2(rIn + ch, zb));
            profile.Add(new Vector2(HOUSING_R_OUTER, zb));
            profile.Add(new Vector2(HOUSING_R_OUTER, zf));
            profile.Add(new Vector2(rIn + ch, zf));
            profile.Add(new Vector2(rIn, zf - ch));
            MeshKit.Lathe(mb, profile, HOUSING_SEGMENTS);
            Mesh housing = BeatKit.Bake(mb, "FanHousing", _meshes);
            _housing = BeatKit.Renderer(fan, "FanHousing", housing, concrete, fc, YToZ, ShadowCastingMode.On, true);

            // ---- 3 collar: a trim rim around the bore, 2 m proud of the near flat and 1.5 m outside the bore (its back is buried
            //      in the housing and the web), inside every ring gap of Beat02Rings
            float cb = COLLAR_Z_BACK - fc.z, cf = COLLAR_Z_FRONT - fc.z, cc = COLLAR_CHAMFER;
            profile.Clear();
            profile.Add(new Vector2(COLLAR_R0, cb));
            profile.Add(new Vector2(COLLAR_R1, cb));
            profile.Add(new Vector2(COLLAR_R1, cf - cc));
            profile.Add(new Vector2(COLLAR_R1 - cc, cf));
            profile.Add(new Vector2(COLLAR_R0 + cc, cf));
            profile.Add(new Vector2(COLLAR_R0, cf - cc));
            profile.Add(new Vector2(COLLAR_R0, cb));
            MeshKit.Lathe(mb, profile, HOUSING_SEGMENTS);
            Mesh collar = BeatKit.Bake(mb, "FanCollar", _meshes);
            _collar = BeatKit.Renderer(fan, "FanCollar", collar, concrete, fc, YToZ, ShadowCastingMode.On, true);

            // ---- 4 stator (static, fan frame): tail nacelle, 12 streamlined struts, the two stay anchors
            BuildStator(mb, profile);
            BeatKit.Emit(mb, fan, "FanStator", statorMat, _meshes, ShadowCastingMode.Off, fc, true);

            // ---- 5 rotor (rotor frame): hub drum with spinner nose, root collars, 12 lofted blades
            _rotor = BeatKit.Group(fan, "Rotor", fc, Quaternion.identity);
            BuildHub(mb, profile);
            Mesh hub = BeatKit.Bake(mb, "FanHub", _meshes);
            _hub = BeatKit.Renderer(_rotor, "Hub", hub, rotorMat, Vector3.zero, Quaternion.identity, ShadowCastingMode.On, true);

            BuildRootCollars(mb, profile);
            BeatKit.Emit(mb, _rotor, "FanRootCollars", rotorMat, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            BuildBlades(mb);
            Mesh blades = BeatKit.Bake(mb, "FanBlades", _meshes);
            // Simplified: static-lit blades (no moving blade shadows)
            _blade = BeatKit.Renderer(_rotor, "Blades", blades, rotorMat, Vector3.zero, Quaternion.identity,
                full ? ShadowCastingMode.On : ShadowCastingMode.Off, true);

            if (full)
            {
                BuildRotorBolts(mb);
                MeshRenderer bolts = BeatKit.Emit(mb, _rotor, "FanRotorBolts", rotorMat, _meshes, ShadowCastingMode.Off, Vector3.zero, true);
                var tier = new DetailTier("Beat06Rotor", new Bounds(fc, new Vector3(2f * rOut, 2f * rOut, 50f)));
                tier.AddNear(bolts);
                tiers.Add(tier);
            }

            // ---- 6 beams: 12 (Simplified 6) analytic wedge prisms riding on the rotor, centred in the blade gaps (14.7.4)
            _beamCount = full ? AbyssConst.BEAM_COUNT : AbyssConst.BEAM_COUNT / 2;
            Material beamMat = mats.Variant(MatId.BeamWedge, "b6fan", m =>
            {
                m.SetFloat(ShaderIds.Recipe, 0f);
                m.SetVector(FanIds.BeamDir, BeamDirV());
                m.SetVector(FanIds.BeamParams, BeamParamsV());
                m.SetFloat(ShaderIds.EmissionStrength, BEAM_DENSITY);
                m.SetFloat(FanIds.PhaseG, BEAM_PHASE_G);
                m.SetVector(FanIds.BeamShape, BeamShapeAt(GameState.RideSeconds));
                m.SetVector(FanIds.BeamFeather, BeamFeatherAt(GameState.RideSeconds));
                m.SetVector(FanIds.BeamAxial, BeamAxial);
            });
            _beamR = BeatKit.Renderer(_rotor, "FanBeams", FanMeshes.Beams(_beamCount), beamMat, Vector3.zero, Quaternion.identity,
                ShadowCastingMode.Off, true);
            _beamR.receiveShadows = false;
            _beamMat = beamMat;
            _beamHaze = -1f;

            // ---- 6b key shadow (Full only): the blade shadows carved out of the fog's key lobe, riding on the rotor
            if (full)
            {
                Material shadowMat = mats.Variant(MatId.BeamWedge, "b6keyshadow", m =>
                {
                    m.SetFloat(ShaderIds.Recipe, 3f);
                    m.SetFloat(FanIds.BeamBlendOp, (float)BlendOp.ReverseSubtract);
                    m.SetVector(FanIds.BeamDir, BeamDirV());
                    m.SetVector(FanIds.BeamParams, ShadowParamsV());
                    m.SetFloat(ShaderIds.EmissionStrength, SHADOW_STRENGTH);
                    m.SetVector(FanIds.BeamShape, new Vector4(0f, 0f, SHADOW_NEAR1, 0f));
                    m.SetVector(FanIds.BeamFeather, new Vector4(SHADOW_SIDE_FEATHER, 12f, SHADOW_CHORD_FEATHER, 4f));
                });
                MeshRenderer sr = BeatKit.Renderer(_rotor, "FanKeyShadow", FanMeshes.Shadows(), shadowMat, Vector3.zero, Quaternion.identity,
                    ShadowCastingMode.Off, true);
                sr.receiveShadows = false;
            }

            // ---- 7 bore card (window onto the Exterior sky, culled from outside)
            BuildBoreCard(ctx, mb, fan);

            // ---- 8 far-wall lighthouse patches (Full only: the patch shader assumes 12 wedges)
            if (full)
            {
                Material patch = mats.Variant(MatId.BeamWedge, "b6patch", m =>
                {
                    m.SetFloat(ShaderIds.Recipe, 1f);
                    m.SetFloat(FanIds.Cull, (float)CullMode.Back);
                    m.SetFloat(FanIds.ZTest, (float)CompareFunction.LessEqual);
                    m.SetVector(FanIds.BeamDir, BeamDirV());
                    m.SetVector(FanIds.BeamParams, BeamParamsV());
                    m.SetVector(FanIds.BeamPatch, new Vector4(PATCH_FEATHER, PATCH_STRETCH, GAP_PHASE_DEG * Mathf.Deg2Rad, 1f));
                    m.SetVector(FanIds.BeamCentre, (Vector4)AbyssConst.FAN_CENTRE);
                    Vector4 pf = m.GetVector(FanIds.BeamFeather);
                    pf.y = PATCH_FEATHER_PX;
                    m.SetVector(FanIds.BeamFeather, pf);
                    m.SetFloat(ShaderIds.EmissionStrength, PatchStrength(GameState.RideSeconds));
                });
                _patchMat = patch;
                _patchStrength = -1f;
                // the wedge ring R 70-130 projected along the light from the fan plane onto the far wall z = 760
                float zWall = AbyssConst.SHAFT_FAR_Z - PATCH_WALL_GAP;
                Vector3 l = AbyssConst.Dir(0f, -AbyssConst.BEAM_PITCH_DEG);
                float travel = (AbyssConst.SHAFT_FAR_Z - fc.z) / l.z;
                var pc = new Vector3(fc.x + l.x * travel, fc.y + l.y * travel, zWall);
                float hx = AbyssConst.BEAM_R1 + PATCH_FEATHER + PATCH_MARGIN, hy = hx * PATCH_STRETCH + PATCH_MARGIN;
                BeatKit.ZCard(mb, -hx, hx, -hy, hy, 0f, false);
                BeatKit.Emit(mb, fan, "FarWallPatches", patch, _meshes, ShadowCastingMode.Off, pc, true);
            }
        }

        // ---------------------------------------------------------------- stator

        void BuildStator(MeshBuilder mb, List<Vector2> profile)
        {
            // The struts and the nacelle sit inside the 60 m bore (occluded sky: AO STATOR_AO on the ambient and FogSky terms).
            // R2-17: a strut's round edge seen at grazing from the balcony (KS7d) reflected the bright beat-7 sky as a 1-px
            // line (0.35 linear on a 0.03 background); with the rough stator paint and this AO it falls about 4x.
            Color32 savedColor = mb.Color;
            mb.Color = new Color32(STATOR_AO, savedColor.g, savedColor.b, savedColor.a);
            // tail nacelle: blunt elliptic tail cone (tip at z = 10, 9 m clear of the bore card) into a drum R 36
            profile.Clear();
            for (int i = 0; i <= NACELLE_TAIL_STEPS; i++)
            {
                float u = 0.5f * Mathf.PI * i / NACELLE_TAIL_STEPS;
                profile.Add(new Vector2(NACELLE_R * Mathf.Sin(u), NACELLE_ZL_DRUM - (NACELLE_ZL_DRUM - NACELLE_ZL_TIP) * Mathf.Cos(u)));
            }
            profile.Add(new Vector2(NACELLE_R, NACELLE_ZL_FRONT - NACELLE_CHAMFER));
            profile.Add(new Vector2(NACELLE_R - NACELLE_CHAMFER, NACELLE_ZL_FRONT));
            profile.Add(new Vector2(0f, NACELLE_ZL_FRONT));
            mb.Push(Matrix4x4.Rotate(YToZ));
            MeshKit.Lathe(mb, profile, NACELLE_SEGMENTS);
            mb.Pop();

            // 12 struts at 15 + 30 k degrees (static): the gantry centreline maps to 270 degrees in the fan plane, midway
            // between the struts at 255 and 285, so no strut ever shadows the walkway. Streamlined section, leading edge
            // upstream (+Z), embedded in the nacelle drum and in the housing body.
            CrossSection sec = MeshKit.Airfoil(STRUT_CHORD, STRUT_THICK, STRUT_SIDE_POINTS);
            var frames = new List<LoftFrame>(2);
            for (int k = 0; k < AbyssConst.FAN_BLADES; k++)
            {
                float a = (GAP_PHASE_DEG + 360f / AbyssConst.FAN_BLADES * k) * Mathf.Deg2Rad;
                var er = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var et = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                Quaternion rot = Quaternion.LookRotation(er, et);   // section x -> -Z (leading edge at -c/2 -> +Z), y -> tangential
                frames.Clear();
                frames.Add(new LoftFrame(er * STRUT_R0 + new Vector3(0f, 0f, STRUT_ZL), rot));
                frames.Add(new LoftFrame(er * STRUT_R1 + new Vector3(0f, 0f, STRUT_ZL), rot));
                MeshKit.Loft(mb, sec, frames);
            }
            mb.Color = savedColor;

            // stay anchors on the housing lip (14.5.6): a block buried 0.2 m in the housing front face and a clevis pin
            Vector3 fc = AbyssConst.FAN_CENTRE;
            for (int s = -1; s <= 1; s += 2)
            {
                var c = new Vector3(s * ANCHOR_DX, ANCHOR_Y - fc.y, ANCHOR_Z - fc.z);
                MeshKit.ChamferBox(mb, c, new Vector3(ANCHOR_W, ANCHOR_H, ANCHOR_D), 0.03f);
                float zp = c.z + ANCHOR_D * 0.5f - 0.45f;
                MeshKit.Cylinder(mb, new Vector3(c.x - ANCHOR_W * 0.5f - 0.12f, c.y + ANCHOR_H * 0.5f - 0.4f, zp),
                    new Vector3(c.x + ANCHOR_W * 0.5f + 0.12f, c.y + ANCHOR_H * 0.5f - 0.4f, zp), ANCHOR_PIN_R, 12);
                // side cheek plates (proud of the block by 4 cm)
                for (int side = -1; side <= 1; side += 2)
                    MeshKit.ChamferBox(mb, new Vector3(c.x + side * (ANCHOR_W * 0.5f + 0.02f), c.y, c.z + 0.2f), new Vector3(0.08f, ANCHOR_H - 0.4f, ANCHOR_D - 0.2f), 0.01f);
            }
        }

        // ---------------------------------------------------------------- rotor

        void BuildHub(MeshBuilder mb, List<Vector2> profile)
        {
            profile.Clear();
            float r = AbyssConst.FAN_R_HUB, c = HUB_CHAMFER;
            profile.Add(new Vector2(0f, HUB_ZL_BACK));
            profile.Add(new Vector2(r - c, HUB_ZL_BACK));
            profile.Add(new Vector2(r, HUB_ZL_BACK + c));
            profile.Add(new Vector2(r, HUB_ZL_FRONT - c));
            profile.Add(new Vector2(r - c, HUB_ZL_FRONT));
            profile.Add(new Vector2(HUB_FRONT_STEP_R, HUB_ZL_FRONT));
            // spinner nose: a hard 45 degree step, then an elliptic dome to the apex
            for (int i = 0; i <= HUB_NOSE_STEPS; i++)
            {
                float u = 0.5f * Mathf.PI * i / HUB_NOSE_STEPS;
                profile.Add(new Vector2(HUB_NOSE_R * Mathf.Cos(u), HUB_NOSE_ZL0 + (HUB_NOSE_ZL1 - HUB_NOSE_ZL0) * Mathf.Sin(u)));
            }
            mb.Push(Matrix4x4.Rotate(YToZ));
            MeshKit.Lathe(mb, profile, HUB_SEGMENTS);
            mb.Pop();
        }

        void BuildRootCollars(MeshBuilder mb, List<Vector2> profile)
        {
            // one collar about the blade's radial axis, from inside the drum to 2.4 m proud of it; the blade pierces its top
            profile.Clear();
            profile.Add(new Vector2(ROOT_COLLAR_R, ROOT_COLLAR_BASE));
            profile.Add(new Vector2(ROOT_COLLAR_R, ROOT_COLLAR_TOP - ROOT_COLLAR_CHAMFER));
            profile.Add(new Vector2(ROOT_COLLAR_R - ROOT_COLLAR_CHAMFER, ROOT_COLLAR_TOP));
            profile.Add(new Vector2(0f, ROOT_COLLAR_TOP));
            var one = new MeshBuilder();
            one.Push(Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.right)));
            MeshKit.Lathe(one, profile, ROOT_COLLAR_SEGMENTS);
            one.Pop();
            for (int k = 0; k < AbyssConst.FAN_BLADES; k++)
                mb.Append(one, Matrix4x4.Rotate(Quaternion.AngleAxis(360f / AbyssConst.FAN_BLADES * k, Vector3.forward)));
        }

        void BuildBlades(MeshBuilder mb)
        {
            var one = new MeshBuilder();
            _tipR = FanBlade.Build(one);

            // measured on the generated blade (projected on the rotation plane): arc at R 90, gaps at R 48 / 90 / 130
            float step = 360f / AbyssConst.FAN_BLADES;
            _gapMinDeg = float.MaxValue; _gapMaxDeg = float.MinValue;
            float r48 = 48f, r90 = 90f, r130 = 130f;
            for (int q = 0; q < 3; q++)
            {
                float r = q == 0 ? r48 : q == 1 ? r90 : r130;
                if (!FanBlade.Coverage(one.Positions, one.Indices, 0, one.Indices.Count, r, out float a0, out float a1)) continue;
                float gap = step - (a1 - a0);
                _gapAtDeg[q] = gap;
                _gapMinDeg = Mathf.Min(_gapMinDeg, gap);
                _gapMaxDeg = Mathf.Max(_gapMaxDeg, gap);
                if (q == 1) _bladeArcDeg = a1 - a0;
            }

            _blades = 0;
            for (int k = 0; k < AbyssConst.FAN_BLADES; k++)
            {
                mb.Append(one, Matrix4x4.Rotate(Quaternion.AngleAxis(step * k, Vector3.forward)));
                _blades++;
            }
        }

        void BuildRotorBolts(MeshBuilder mb)
        {
            // hub front ring
            for (int i = 0; i < HUB_BOLTS; i++)
            {
                float a = (i + 0.5f) * 2f * Mathf.PI / HUB_BOLTS;
                MeshKit.HexBolt(mb, new Vector3(HUB_BOLT_R * Mathf.Cos(a), HUB_BOLT_R * Mathf.Sin(a), HUB_ZL_FRONT), Vector3.forward, HUB_BOLT_AF,
                    a * Mathf.Rad2Deg);
            }
            // root collar bolt circles, clear of the root chord line (pitch p(40) from the rotation plane)
            float chordDeg = (float)(FanBlade.Pitch(FanBlade.DESIGN_ROOT_R) * Mathf.Rad2Deg);
            for (int k = 0; k < AbyssConst.FAN_BLADES; k++)
            {
                float phi = 360f / AbyssConst.FAN_BLADES * k * Mathf.Deg2Rad;
                var er = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0f);
                var et = new Vector3(-Mathf.Sin(phi), Mathf.Cos(phi), 0f);
                for (int b = 0; b < ROOT_BOLTS; b++)
                {
                    float beta = (b + 0.5f) * 360f / ROOT_BOLTS;
                    float d0 = Mathf.Abs(Mathf.DeltaAngle(beta, chordDeg)), d1 = Mathf.Abs(Mathf.DeltaAngle(beta, chordDeg + 180f));
                    if (d0 < ROOT_BOLT_CLEAR_DEG || d1 < ROOT_BOLT_CLEAR_DEG) continue;
                    float br = beta * Mathf.Deg2Rad;
                    Vector3 p = er * ROOT_COLLAR_TOP + (et * Mathf.Cos(br) + Vector3.forward * Mathf.Sin(br)) * ROOT_BOLT_RHO;
                    MeshKit.HexBolt(mb, p, er, ROOT_BOLT_AF, beta);
                }
            }
        }
    }

    /// <summary>Shader property ids of beat 6 (kept out of the MonoBehaviour so they never initialise during deserialisation).</summary>
    internal static class FanIds
    {
        public static readonly int BeamDir = Shader.PropertyToID("_BeamDir");
        public static readonly int BeamParams = Shader.PropertyToID("_BeamParams");
        public static readonly int BeamPatch = Shader.PropertyToID("_BeamPatch");
        public static readonly int BeamCentre = Shader.PropertyToID("_BeamCentre");
        public static readonly int BeamShape = Shader.PropertyToID("_BeamShape");
        public static readonly int BeamFeather = Shader.PropertyToID("_BeamFeather");
        public static readonly int BeamAxial = Shader.PropertyToID("_BeamAxial");
        public static readonly int BeamBlendOp = Shader.PropertyToID("_BeamBlendOp");
        public static readonly int PhaseG = Shader.PropertyToID("_PhaseG");
        public static readonly int Cull = Shader.PropertyToID("_Cull");
        public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        public static readonly int FarTint = Shader.PropertyToID("_FarTint");
        public static readonly int FarRange = Shader.PropertyToID("_FarRange");
        public static readonly int Rust = Shader.PropertyToID("_RustAmount");
        public static readonly int KeyLightMask = Shader.PropertyToID("_KeyLightMask");
        public static readonly int WearAmount = Shader.PropertyToID("_WearAmount");
        public static readonly int CardMode = Shader.PropertyToID("_CardMode");
        public static readonly int CardFog = Shader.PropertyToID("_CardFog");
        public static readonly int EnergyConserve = Shader.PropertyToID("_EnergyConserve");
    }

    /// <summary>
    /// Shader-built meshes shared by every build (HideFlags.DontSave, never validated: BeamWedge positions are built in the
    /// vertex shader). Cached so EditMode test builds and scene restarts do not leak a copy each time.
    /// </summary>
    internal static class FanMeshes
    {
        static Mesh s_beams12, s_beams6, s_shadows12, s_glow;

        /// <summary>The 12 key-shadow prisms on the blade centres (phase 0: blade k at 30 k degrees in the rotor frame).</summary>
        public static Mesh Shadows()
        {
            if (s_shadows12 == null)
                s_shadows12 = ShaderMeshes.BeamWedgeMesh(AbyssConst.FAN_BLADES, 0f, Beat06_Fan.ShadowOuterRadius, AbyssConst.BEAM_LENGTH);
            return s_shadows12;
        }

        public static Mesh Beams(int count)
        {
            float phase = Beat06_Fan.GAP_PHASE_DEG * Mathf.Deg2Rad;
            if (count == AbyssConst.BEAM_COUNT)
            {
                if (s_beams12 == null) s_beams12 = ShaderMeshes.BeamWedgeMesh(count, phase, AbyssConst.BEAM_R1, AbyssConst.BEAM_LENGTH);
                return s_beams12;
            }
            if (s_beams6 == null) s_beams6 = ShaderMeshes.BeamWedgeMesh(count, phase, AbyssConst.BEAM_R1, AbyssConst.BEAM_LENGTH);
            return s_beams6;
        }

        public static Mesh Glow()
        {
            if (s_glow == null) s_glow = ShaderMeshes.GlowQuad();
            return s_glow;
        }
    }
}
