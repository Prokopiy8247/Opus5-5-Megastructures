using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2, the bridge (14.5.2 c-d): landing and console, deck and edge lines, Warren truss with gussets, floor beams and
    /// bearings, outrigger lamps, fascia bolts, far proxies, detail tiers and the falling bolt event.
    /// </summary>
    public sealed partial class Beat02_Bridge
    {
        const float DECK_T = 0.3f, DECK_BEVEL = 0.02f, LANDING_T = 0.5f;
        const float EDGE_LINE_W = 0.12f, EDGE_LINE_T = 0.008f, EDGE_LINE_X = 0.52f;     // pale lines 0.46-0.58 from the centre
        const float EDGE_LINE_FADE0 = 40f, EDGE_LINE_FADE1 = 60f;                       // fade into the deck tone beyond 60 m
        const float TRUSS_MEMBER = 0.4f;
        const int TRUSS_BAYS = 25;
        const float LAMP_POST_R = 0.03f, LAMP_GLOBE_R = 0.055f, LAMP_END_INSET = 0.1f;
        const float FASCIA_BOLT_PITCH = 0.75f, FASCIA_BOLT_AF = 0.024f, BOLT_CHUNK = 25f;
        const float BOLT_TIER_NEAR = 50f;   // bolts fully in within 10 m when approaching, gone beyond 50 m (sub-pixel there)
        const float CONSOLE_HALF_W = 1.5f, CONSOLE_FRONT_Z = 65.9f, CONSOLE_BOTTOM_Y = -10f, CONSOLE_EMBED = 0.3f;
        const float CONSOLE2_HALF_W = 1.2f, CONSOLE2_FRONT_Z = 63f, CONSOLE2_BOTTOM_Y = -14f;
        const float BOLT_D = 0.12f, BOLT_STRENGTH = 3f, BOLT_FADE0 = 8f, BOLT_FADE1 = 12f;
        const double BOLT_G = 9.81;
        // pale paint, desaturated, never white: look-dev r02 lowered from 0.58-0.62, because under the amber lamps the line read
        // 0.76 in KS2b and set its p99 at 0.69 (limit 0.70). Fix round 2 (R2-23: the right line beside the KS2b lamp read 0.67,
        // limit 0.5): linear albedo x 0.7 and matte (roughness 0.65), with the lamps back at the spec I 5 (lead_b01 at x 0.75:
        // right line p95 0.502, max 0.535)
        static readonly Color EDGE_LINE_SRGB = new Color(0.41f, 0.427f, 0.445f, 1f);
        const float EDGE_LINE_ROUGHNESS = 0.65f;
        // black bracket paint (#0A0B0D, linear 0.003, the BLUE_BLACK floor): the arm lies 1.3 m under its lamp
        static readonly Color ARM_SRGB = new Color(0.038f, 0.043f, 0.05f, 1f);
        const float DECK_ROUGHNESS = 0.7f;
        const float LAMP_HALO_D = 0.5f, LAMP_HALO_STRENGTH = 0.3f;
        /// <summary>
        /// Outrigger globe and its dot sprite (critique R2-21, lead decision D4: small and distant amber emitters use an emissive
        /// of at most 2.5, because the Neutral tonemapper clips M_Lamp's AMBER x 8.0 to cream-white, HSV S 0.22-0.26). The 0.11 m
        /// globes are small emitters at every distance the walk sees them from (1.5 m beside the deck and beyond), so they emit a
        /// slightly deeper amber (#FF9A3C) x 2.0, which the grade displays at about (230, 177, 80): hue 39, S 0.65, the AMBER
        /// identity (#FFB45A is hue 33, S 0.65) after the tonemapper's shift toward yellow. The dot sprite (at least 2.5 px) uses
        /// the same colour x 1.6, the haze halo x 0.3.
        /// </summary>
        public static readonly Color GLOBE_SRGB = new Color(1f, 0.604f, 0.235f, 1f);
        public const float GLOBE_STRENGTH = 2f, GLOBE_DOT_STRENGTH = 1.6f;

        MeshRenderer _deck, _landing, _truss, _edgeLines;
        readonly Vector3[] _lampHeads = new Vector3[5];
        int _lampCount;
        float _postTopY;
        int _trussMembers;
        DetailTier _bridgeTier;
        readonly List<DetailTier> _boltTiers = new List<DetailTier>();

        static float LampZ(int i)
        {
            float z = AbyssConst.BRIDGE_LAMP_Z[i];
            // the end lamps stand on the deck just inside the landing edge and the ledge's outer face (r 28 around C1)
            if (i == 0) return z + LAMP_END_INSET;
            if (i == AbyssConst.BRIDGE_LAMP_Z.Length - 1) return z - LAMP_END_INSET;
            return z;
        }

        void BuildBridge(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform br = BeatKit.Group(_geo, "Bridge");
            bool gray = _tier == BeatTier.Graybox, full = _tier == BeatTier.Full;
            float y = AbyssConst.BRIDGE_Y;
            float z0 = AbyssConst.SHAFT_NEAR_Z, z1 = AbyssConst.BRIDGE_Z0, z2 = AbyssConst.BRIDGE_Z1;

            Material steel = gray ? mats[MatId.SteelPainted] : mats.Variant(MatId.SteelPainted, "b2steel", m =>
            {
                m.SetFloat(RustId, 0.03f);
                m.SetFloat(WearId, 1.1f);
            });
            Material thin = gray ? mats[MatId.SteelPainted] : mats.Variant(MatId.SteelPainted, "b2thin", m =>
            {
                m.SetFloat(RustId, 0.04f);
                m.SetFloat(WearId, 1.1f);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            });
            // look-dev r01: rougher paint, so the shaft sun's broad sheen (KS2b looks straight down the reflection) no longer
            // washes the deck to a pale grey and the amber pools read on it. No rust on the walked deck (R1-33): the rust mask
            // samples the 3 m NoiseTex tile, so on the 1.2 m strip one patch beside the left edge line repeated every 3 m as a
            // stamped half-disc decal (KS2a, KS2b); a walked painted deck shows wear, not rust patches
            Material deckMat = gray ? mats[MatId.SteelPainted] : mats.Variant(MatId.SteelPainted, "b2deck", m =>
            {
                m.SetFloat(RustId, 0f);
                m.SetFloat(WearId, 1.3f);
                m.SetFloat(ShaderIds.Roughness, DECK_ROUGHNESS);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            });
            // lamp arms: matte near-black paint, no bare-steel wear. Each arm lies 1.3 m under its lamp, which lights its top
            // at E ~ I / 1.7; in KS2b (straight down over lamp z 141) the arm crosses the right void strip, which must stay
            // below luma 0.12 (14.5.2 acceptance)
            Material armMat = gray ? mats[MatId.SteelPainted] : mats.Variant(MatId.SteelPainted, "b2arm", m =>
            {
                m.SetColor(ShaderIds.BaseColor, ARM_SRGB);
                m.SetFloat(ShaderIds.Roughness, 0.92f);
                // F0 = lerp(0.04, albedo, metallic): with the lamp straight above and the KS2b eye straight above, the dielectric
                // 0.04 sheen alone lit the arm to luma 0.16; at metallic 0.85 (F0 0.009) the amber spot's specular still drew it
                // as a 20 px brown bar (0.07 luma, R > B) across the right void strip of KS2b (R2-23). Fully metallic black
                // paint (F0 = albedo 0.003, no diffuse) takes about 0.003 x E there, under the radiance floor, so the arm settles
                // on the abyss colour (FOG_DOWN) like the void around it.
                m.SetFloat(ShaderIds.Metallic, 1f);
                m.SetFloat(RustId, 0f);
                m.SetFloat(WearId, 0f);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            });

            if (!gray)
                _bridgeTier = new DetailTier("B2_BridgeNear", MakeBounds(new Vector3(-4.2f, -16.5f, z0 - 0.5f), new Vector3(4.2f, -1.5f, z2 + 7f)),
                    NEAR_TIER, TIER_HYST, TIER_FADE);

            // ---- landing 8 x 6 (z 60-66), walk surface y -3
            MeshKit.ChamferBox(mb, new Vector3(0f, y - LANDING_T * 0.5f, (z0 + z1) * 0.5f), new Vector3(AbyssConst.LANDING_W, LANDING_T, z1 - z0), 0.02f);
            BeatKit.FloorUVAlongZ(mb, 0, 0f);
            _landing = BeatKit.Emit(mb, br, "Landing", LandingMaterial(mats), _meshes, ShadowCastingMode.On);
            if (!gray)
            {
                _bridgeTier.AddNear(_landing);
                BuildConsole(ctx, mb, br, full);
                // far proxies: slab and console as plain boxes in the same materials (persistent skeleton after d 380)
                mb.Box(new Vector3(0f, y - LANDING_T * 0.5f, (z0 + z1) * 0.5f), new Vector3(AbyssConst.LANDING_W, LANDING_T, z1 - z0));
                BeatKit.FloorUVAlongZ(mb, 0, 0f);
                _bridgeTier.AddFar(BeatKit.Emit(mb, br, "LandingProxy", LandingMaterial(mats), _meshes, ShadowCastingMode.Off));
                mb.Box(new Vector3(0f, 0.5f * (y - LANDING_T + 0.05f + CONSOLE_BOTTOM_Y), 0.5f * (z0 - CONSOLE_EMBED + CONSOLE_FRONT_Z)),
                    new Vector3(2f * CONSOLE_HALF_W, y - LANDING_T + 0.05f - CONSOLE_BOTTOM_Y, CONSOLE_FRONT_Z - z0 + CONSOLE_EMBED));
                _bridgeTier.AddFar(BeatKit.Emit(mb, br, "ConsoleProxy", mats[MatId.ConcreteBoard], _meshes, ShadowCastingMode.Off));
            }

            // ---- deck 1.2 x 150 x 0.3 (persistent), ThinLine so it never drops under 1.5 px from the lift
            MeshKit.ChamferMember(mb, new Vector3(0f, y - DECK_T * 0.5f, z1), new Vector3(0f, y - DECK_T * 0.5f, z2), AbyssConst.BRIDGE_W, DECK_T,
                DECK_BEVEL, Vector3.up, !gray);
            if (!gray) WornPathTop(mb, y, z1, z2);
            _deck = BeatKit.Emit(mb, br, "Deck", deckMat, _meshes, ShadowCastingMode.On);

            // ---- pale edge lines 0.12 m, 5 mm proud of the deck (bottom 3 mm inside it), fading to the deck tone beyond 60 m
            if (!gray)
            {
                Material lineMat = mats.Variant(MatId.SteelPainted, "b2edgeline", m =>
                {
                    m.SetColor(ShaderIds.BaseColor, EDGE_LINE_SRGB);
                    m.SetFloat(ShaderIds.Roughness, EDGE_LINE_ROUGHNESS);
                    m.SetFloat(RustId, 0f);
                    m.SetFloat(WearId, 0.25f);
                    m.SetColor(FarTintId, AbyssPalette.STEEL_DARK);
                    m.SetVector(FarRangeId, new Vector4(EDGE_LINE_FADE0, EDGE_LINE_FADE1, 0f, 0f));
                });
                float ly = y + 0.005f - EDGE_LINE_T * 0.5f;
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferBox(mb, new Vector3(s * EDGE_LINE_X, ly, (z1 + z2) * 0.5f), new Vector3(EDGE_LINE_W, EDGE_LINE_T, z2 - z1 - 0.1f), 0.003f);
                _edgeLines = BeatKit.Emit(mb, br, "EdgeLines", lineMat, _meshes, ShadowCastingMode.Off);
            }

            // ---- Warren truss 6 m deep, 25 bays, 75 I-section members (0.4 m) with gusset plates; top chord axis at the deck underside
            var ta = new Vector3(0f, y - DECK_T, z1);
            var tb = new Vector3(0f, y - DECK_T, z2);
            _trussMembers = MeshKit.WarrenTruss(mb, ta, tb, AbyssConst.BRIDGE_TRUSS_DEPTH, TRUSS_BAYS, TRUSS_MEMBER, Vector3.up,
                TrussMemberShape.IBeam, !gray, true);
            _truss = BeatKit.Emit(mb, br, "Truss", thin, _meshes, ShadowCastingMode.On);
            if (!gray)
            {
                _bridgeTier.AddNear(_truss);
                BuildTrussExtras(mb, br, thin, full);
                BuildTrussProxy(mb, br, thin, ta, tb);
            }

            // ---- outrigger lamps: arm from inside the deck, post O 0.06 rising 1.1 m above the deck 0.9 m outside the right edge,
            // collar, amber globe and a small hood
            float px = AbyssConst.BRIDGE_W * 0.5f + AbyssConst.BRIDGE_LAMP_OUTSIDE;
            _postTopY = y + AbyssConst.BRIDGE_LAMP_POST_H;
            float globeY = _postTopY + 0.065f;
            _lampCount = AbyssConst.BRIDGE_LAMP_Z.Length;
            for (int i = 0; i < _lampCount; i++)
            {
                float z = LampZ(i);
                MeshKit.ChamferMember(mb, new Vector3(AbyssConst.BRIDGE_W * 0.5f - 0.15f, y - 0.18f, z), new Vector3(px + 0.05f, y - 0.18f, z),
                    0.06f, 0.08f, 0.01f, Vector3.up, !gray);
            }
            BeatKit.Emit(mb, br, "LampArms", armMat, _meshes, ShadowCastingMode.On);
            for (int i = 0; i < _lampCount; i++)
            {
                float z = LampZ(i);
                MeshKit.Cylinder(mb, new Vector3(px, y - 0.26f, z), new Vector3(px, _postTopY, z), LAMP_POST_R, 12, true, !gray);
                MeshKit.Cylinder(mb, new Vector3(px, _postTopY - 0.03f, z), new Vector3(px, _postTopY + 0.015f, z), 0.04f, 16, true);
                MeshKit.Cylinder(mb, new Vector3(px, globeY + LAMP_GLOBE_R - 0.005f, z), new Vector3(px, globeY + LAMP_GLOBE_R + 0.015f, z), 0.07f, 20, true);
            }
            BeatKit.Emit(mb, br, "LampPosts", thin, _meshes, ShadowCastingMode.On);
            for (int i = 0; i < _lampCount; i++)
            {
                _lampHeads[i] = new Vector3(px, globeY, LampZ(i));
                MeshKit.Sphere(mb, _lampHeads[i], LAMP_GLOBE_R, 16, 10);
            }
            Material globeMat = mats.Variant(MatId.LampAmber, "b2globe", m =>
            {
                m.SetColor(ShaderIds.EmissionColor, GLOBE_SRGB.linear);   // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, GLOBE_STRENGTH);
            });
            BeatKit.Emit(mb, br, "LampGlobes", globeMat, _meshes, ShadowCastingMode.Off);
            if (!gray)
            {
                var glow = new InstancedBatch("B2_LampGlows", _glowQuad, GlowMaterial(mats, "b2lamp", GLOBE_SRGB, GLOBE_DOT_STRENGTH, 0.6f),
                    ShadowCastingMode.Off, false, 0, 1000f);
                for (int i = 0; i < _lampCount; i++) glow.Add(Matrix4x4.TRS(_lampHeads[i], Quaternion.identity, Vector3.one * (2f * LAMP_GLOBE_R)));
                _batches.Add(glow);
                // soft haze halo around each globe (true size, never enlarged), so the 0.11 m lamps read as lamps from 20-60 m
                var halo = new InstancedBatch("B2_LampHalos", _glowQuad, GlowMaterial(mats, "b2lamphalo", GLOBE_SRGB, LAMP_HALO_STRENGTH, 1f),
                    ShadowCastingMode.Off, false, 0, 1000f);
                for (int i = 0; i < _lampCount; i++) halo.Add(Matrix4x4.TRS(_lampHeads[i], Quaternion.identity, Vector3.one * LAMP_HALO_D));
                _batches.Add(halo);
            }

            if (full) BuildFasciaBolts(mb, br, steel, px);
            if (!gray) BuildFloodlight(ctx, mb, br, steel);
        }

        // ================================================================================================ column floodlight

        /// <summary>
        /// Cold floodlight that grades C1's face seen from the bridge (14.2 beat-3 rig "cold spots on the column face"; 14.5.3
        /// "grade the face from CONC_DARK at the foot to CONC_LIGHT highlights 60 m up"). Critic round 1 (R1-30): from the ledge
        /// kerb (6 m from the face, aimed 64 m up, 6 / 38 degree cone) the pool was a narrow hard teardrop about 10 m wide on
        /// the 44 m column, a flashlight on a smooth pillar. A light 6 m from the face cannot grade it broadly (E ~ cos / d^2
        /// falls 130x from 10 m to 60 m up), so the can now hangs off the bridge's left fascia at z 176, 46 m in front of the
        /// face and 1.2 m outboard on a bracket (the walker sees it only when looking down over the edge), aimed 40 m up the
        /// face with a wide soft cone (70 degrees, 8 degree core): the collar, the y = 0 band, the ledge lamps and the bridge
        /// end stay outside the cone, the face brightens continuously from about y 4 over the whole lit width of the column to
        /// a broad highlight about 25 m up and fades above 50 m, and the cylinder shades off toward both silhouettes. The cone
        /// clears the deck edge (cos margin 0.04), the outrigger lamps and the ledge (Beat02Tests). Always on (like the beat-3
        /// spot it replaces): C1 keeps its grade when seen again from the hall exit and the lift.
        /// </summary>
        public static readonly Vector3 FLOOD_PIVOT = new Vector3(-1.8f, -3.6f, 176f);
        public static readonly Vector3 FLOOD_AIM = new Vector3(0f, 40f, 222f);
        public const float FLOOD_I = 7500f, FLOOD_RANGE = 90f, FLOOD_ANGLE = 70f, FLOOD_INNER = 8f, FLOOD_LENS = 0.21f;

        /// <summary>Unit beam axis of the floodlight.</summary>
        public static Vector3 FloodDirection => (FLOOD_AIM - FLOOD_PIVOT).normalized;

        void BuildFloodlight(BeatContext ctx, MeshBuilder mb, Transform br, Material steel)
        {
            Vector3 pos = FLOOD_PIVOT, dir = FloodDirection;
            var dirH = new Vector3(dir.x, 0f, dir.z);
            Vector3 pin = Vector3.Cross(Vector3.up, dirH).normalized;   // horizontal, square to the beam's heading
            float yoke = pos.y + 0.31f, armY = AbyssConst.BRIDGE_Y - DECK_T * 0.5f - 0.05f;
            float hx = -AbyssConst.BRIDGE_W * 0.5f;

            // can, bezel, back cap and the cold lens
            MeshKit.TaperedCylinder(mb, pos - dir * 0.2f, pos + dir * 0.165f, 0.105f, 0.12f, 20, true);
            MeshKit.Cylinder(mb, pos + dir * 0.16f, pos + dir * 0.205f, 0.14f, 20, true);
            MeshKit.Cylinder(mb, pos - dir * 0.235f, pos - dir * 0.195f, 0.06f, 12, true);
            // pivot pin, hanging yoke (two straps from the yoke bar down to the pin ends), the bar
            MeshKit.Cylinder(mb, pos - pin * 0.19f, pos + pin * 0.19f, 0.018f, 10);
            Vector3 bar = new Vector3(pos.x, yoke, pos.z);
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferMember(mb, bar + pin * (s * 0.175f), pos + pin * (s * 0.175f), 0.03f, 0.012f, 0.003f, dirH.normalized);
            MeshKit.ChamferMember(mb, bar - pin * 0.19f, bar + pin * 0.19f, 0.04f, 0.04f, 0.006f, Vector3.up);
            // hanger from the bar up to the bracket arm, the arm back to a bolted plate on the left fascia
            MeshKit.ChamferMember(mb, new Vector3(pos.x, yoke - 0.02f, pos.z), new Vector3(pos.x, armY + 0.03f, pos.z), 0.05f, 0.05f, 0.006f, Vector3.forward);
            MeshKit.ChamferMember(mb, new Vector3(hx - 0.005f, armY, pos.z), new Vector3(pos.x - 0.03f, armY, pos.z), 0.06f, 0.06f, 0.006f, Vector3.up);
            MeshKit.ChamferBox(mb, new Vector3(hx - 0.0085f, armY, pos.z), new Vector3(0.017f, 0.16f, 0.2f), 0.003f);
            for (int q = 0; q < 4; q++)
                MeshKit.HexBolt(mb, new Vector3(hx - 0.017f, armY + ((q & 1) == 0 ? -0.055f : 0.055f), pos.z + ((q & 2) == 0 ? -0.07f : 0.07f)),
                    Vector3.left, 0.022f, 10f * q, 0.008f, 0.01f);
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "Floodlight", steel, _meshes, ShadowCastingMode.On));
            MeshKit.Disc(mb, pos + dir * 0.207f, dir, 0.118f, 20);
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "FloodlightLens", ctx.Mats[MatId.LampCold], _meshes, ShadowCastingMode.Off));
        }

        /// <summary>
        /// Landing slab: the worn-floor recipe at CONC_MID x 0.85, like the ledge walking surface (Beat03 "b3floor"). R1-37: the
        /// landing under the gallery's cold spill and the first lamp's pool was the brightest plane of B2_start (luma 0.30).
        /// </summary>
        static Material LandingMaterial(MaterialLibrary mats) =>
            mats.Variant(MatId.ConcreteFloor, "b2landing", m =>
            {
                Color c = AbyssPalette.CONC_MID.linear * 0.85f;
                c = c.gamma;
                c.a = 1f;
                m.SetColor(ShaderIds.BaseColor, c);
            });

        static Bounds MakeBounds(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        /// <summary>
        /// Stepped concrete console under the landing (the bridge springs from it): a 3 m wide block down to y -10 whose front
        /// face (z 65.9, 10 cm behind the landing edge) carries the bottom-chord bearing, and a narrower block below it. Both run
        /// 0.3 m into the near flat. Full tier adds the steel architrave around the doorway on the shaft side (2 cm reveal) and
        /// the bearing plates (landing end: bottom chord plate with four bolts; ledge end: a strut from the last bottom node into C1).
        /// </summary>
        void BuildConsole(BeatContext ctx, MeshBuilder mb, Transform br, bool full)
        {
            MaterialLibrary mats = ctx.Mats;
            float y = AbyssConst.BRIDGE_Y, zW = AbyssConst.SHAFT_NEAR_Z - CONSOLE_EMBED;
            float top = y - LANDING_T + 0.05f;    // 5 cm into the landing slab
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (top + CONSOLE_BOTTOM_Y), 0.5f * (zW + CONSOLE_FRONT_Z)),
                new Vector3(2f * CONSOLE_HALF_W, top - CONSOLE_BOTTOM_Y, CONSOLE_FRONT_Z - zW), 0.03f);
            float top2 = CONSOLE_BOTTOM_Y + 0.05f;
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (top2 + CONSOLE2_BOTTOM_Y), 0.5f * (zW + CONSOLE2_FRONT_Z)),
                new Vector3(2f * CONSOLE2_HALF_W, top2 - CONSOLE2_BOTTOM_Y, CONSOLE2_FRONT_Z - zW), 0.03f);
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "Console", mats[MatId.ConcreteBoard], _meshes, ShadowCastingMode.On));

            if (!full) return;
            Material steel = mats.Variant(MatId.SteelPainted, "b2steel", m => { m.SetFloat(RustId, 0.03f); m.SetFloat(WearId, 1.1f); });

            // architrave on the near flat around the 1.2 x 2.0 doorway: jambs 0.12 wide, head 0.12 high, 7-8 cm proud
            float zA = AbyssConst.SHAFT_NEAR_Z, hw = AbyssConst.EXIT_DOOR_W * 0.5f + 0.02f, top3 = y + AbyssConst.EXIT_DOOR_H + 0.02f;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(s * (hw + 0.06f), 0.5f * (y - 0.02f + top3 + 0.1f), zA + 0.025f),
                    new Vector3(0.12f, top3 + 0.1f - (y - 0.02f), 0.07f), 0.01f);
            MeshKit.ChamferBox(mb, new Vector3(0f, top3 + 0.06f, zA + 0.03f), new Vector3(2f * (hw + 0.13f), 0.12f, 0.08f), 0.01f);
            // bulkhead lamp housing over the doorway (the visible source of the gallery's cold spill onto the landing)
            float lampY = top3 + 0.27f;
            MeshKit.ChamferBox(mb, new Vector3(0f, lampY, zA + 0.10f), new Vector3(0.6f, 0.16f, 0.24f), 0.01f);

            // landing-end bearing plate for the bottom chord (embedded 2 cm in the console, the chord end 2 cm in the plate)
            float by = y - DECK_T - AbyssConst.BRIDGE_TRUSS_DEPTH;
            MeshKit.ChamferBox(mb, new Vector3(0f, by, CONSOLE_FRONT_Z + 0.05f), new Vector3(0.6f, 0.6f, 0.14f), 0.01f);
            for (int i = 0; i < 4; i++)
            {
                float bx = (i & 1) == 0 ? -0.24f : 0.24f, byy = by + ((i & 2) == 0 ? -0.22f : 0.22f);
                MeshKit.HexBolt(mb, new Vector3(bx, byy, CONSOLE_FRONT_Z + 0.12f), Vector3.forward, 0.04f, 15f * i, 0.016f, 0.03f);
            }
            // ledge-end strut: the last bottom node (z 216) into the column C1 (surface z 222 on the centreline)
            Vector3 c1 = AbyssConst.COLUMNS[0];
            float zEnd = c1.y - c1.z + 0.4f;
            MeshKit.ChamferMember(mb, new Vector3(0f, by, AbyssConst.BRIDGE_Z1 - 0.02f), new Vector3(0f, by, zEnd), 0.44f, 0.44f, 0.015f, Vector3.up);
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "Bearings", steel, _meshes, ShadowCastingMode.On));
            // its cold lens, 1.5 cm proud of the housing underside
            MeshKit.ChamferBox(mb, new Vector3(0f, lampY - 0.08f - 0.005f, zA + 0.115f), new Vector3(0.5f, 0.03f, 0.16f), 0.005f);
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "DoorLamp", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off));
        }

        /// <summary>Full / Simplified: transverse floor beams under the deck at the inner top nodes (I 0.2, 5 mm into the deck).</summary>
        void BuildTrussExtras(MeshBuilder mb, Transform br, Material thin, bool full)
        {
            if (!full) return;
            float y = AbyssConst.BRIDGE_Y - DECK_T - 0.1f + 0.005f;
            float half = AbyssConst.BRIDGE_W * 0.5f - 0.04f;
            float bay = (AbyssConst.BRIDGE_Z1 - AbyssConst.BRIDGE_Z0) / TRUSS_BAYS;
            for (int i = 1; i < TRUSS_BAYS; i++)
            {
                float z = AbyssConst.BRIDGE_Z0 + bay * i;
                MeshKit.IBeam(mb, new Vector3(-half, y, z), new Vector3(half, y, z), 0.2f, 0.12f, 0.01f, 0.014f, Vector3.up, true);
            }
            _bridgeTier.AddNear(BeatKit.Emit(mb, br, "FloorBeams", thin, _meshes, ShadowCastingMode.On));
        }

        /// <summary>
        /// Far proxy of the truss (14.6 skeleton after d 380, and the far tier beyond 150 m): the same 75 members as plain
        /// 12-triangle boxes with ThinLine data (diagonals 4 cm narrower so no face is coplanar with a chord).
        /// </summary>
        void BuildTrussProxy(MeshBuilder mb, Transform br, Material thin, Vector3 ta, Vector3 tb)
        {
            Vector3 axis = tb - ta;
            Vector3 drop = Vector3.down * AbyssConst.BRIDGE_TRUSS_DEPTH;
            for (int i = 0; i < TRUSS_BAYS; i++)
            {
                Vector3 t0 = ta + axis * ((float)i / TRUSS_BAYS), t1 = ta + axis * ((float)(i + 1) / TRUSS_BAYS);
                Vector3 b0 = t0 + drop, b1 = t1 + drop;
                ProxyMember(mb, t0, t1, TRUSS_MEMBER);
                ProxyMember(mb, b0, b1, TRUSS_MEMBER);
                if ((i & 1) == 0) ProxyMember(mb, t0, b1, TRUSS_MEMBER - 0.04f);
                else ProxyMember(mb, b0, t1, TRUSS_MEMBER - 0.04f);
            }
            _bridgeTier.AddFar(BeatKit.Emit(mb, br, "TrussProxy", thin, _meshes, ShadowCastingMode.Off));
        }

        static void ProxyMember(MeshBuilder mb, Vector3 a, Vector3 b, float size)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Vector3 dir = d / len;
            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.99f ? Vector3.forward : Vector3.up);
            bool w1 = mb.WriteUV1, w2 = mb.WriteUV2;
            while (mb.UV1.Count < mb.VertexCount) mb.UV1.Add(Vector4.zero);
            while (mb.UV2.Count < mb.VertexCount) mb.UV2.Add(Vector4.zero);
            mb.WriteUV1 = true; mb.CurrentUV1 = new Vector4(dir.x, dir.y, dir.z, 0.5f * size);
            mb.WriteUV2 = true; mb.CurrentUV2 = new Vector4(mid.x, mid.y, mid.z, 1f);
            mb.Push(mid, rot);
            mb.Box(Vector3.zero, new Vector3(size, size, len));
            mb.Pop();
            mb.WriteUV1 = w1; mb.WriteUV2 = w2;
        }

        /// <summary>
        /// Full tier: a row of hex bolts (24 mm across flats) every 0.75 m along both deck fascias and a bolted base plate at each
        /// lamp arm, merged per 25 m chunk; each chunk fades in within 40 m of the camera (DetailTier, 20 m hysteresis), so the
        /// sub-pixel heads never sparkle far down the deck.
        /// </summary>
        void BuildFasciaBolts(MeshBuilder mb, Transform br, Material steel, float px)
        {
            float y = AbyssConst.BRIDGE_Y - DECK_T * 0.5f, hx = AbyssConst.BRIDGE_W * 0.5f;
            float z1 = AbyssConst.BRIDGE_Z0, z2 = AbyssConst.BRIDGE_Z1;
            int chunks = Mathf.CeilToInt((z2 - z1) / BOLT_CHUNK - 1e-3f);
            uint h = 0x2B02u;
            for (int c = 0; c < chunks; c++)
            {
                float ca = z1 + BOLT_CHUNK * c, cb = Mathf.Min(z2, ca + BOLT_CHUNK);
                for (float z = z1 + 0.375f; z < z2 - 0.2f; z += FASCIA_BOLT_PITCH)
                {
                    if (z < ca || z >= cb) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (s > 0 && NearLamp(z, 0.16f)) continue;
                        h = h * 747796405u + 2891336453u;
                        MeshKit.HexBolt(mb, new Vector3(s * hx, y, z), new Vector3(s, 0f, 0f), FASCIA_BOLT_AF, (h >> 24) * (60f / 256f), 0.009f, 0.02f);
                    }
                }
                for (int i = 0; i < _lampCount; i++)
                {
                    float z = LampZ(i);
                    if (z < ca || z >= cb) continue;
                    float ay = AbyssConst.BRIDGE_Y - 0.18f;
                    MeshKit.ChamferBox(mb, new Vector3(hx + 0.0035f, ay, z), new Vector3(0.017f, 0.14f, 0.14f), 0.003f);
                    for (int q = 0; q < 4; q++)
                        MeshKit.HexBolt(mb, new Vector3(hx + 0.012f, ay + ((q & 1) == 0 ? -0.04f : 0.04f), z + ((q & 2) == 0 ? -0.04f : 0.04f)),
                            Vector3.right, 0.018f, 10f * q, 0.007f, 0.01f);
                }
                MeshRenderer r = BeatKit.Emit(mb, br, "FasciaBolts_" + c, steel, _meshes, ShadowCastingMode.Off);
                if (r == null) continue;
                var tier = new DetailTier("B2_Bolts_" + c, MakeBounds(new Vector3(-hx - 0.1f, y - 0.2f, ca), new Vector3(hx + 0.1f, y + 0.2f, cb)),
                    BOLT_TIER_NEAR, TIER_HYST, TIER_FADE);
                tier.AddNear(r);
                _boltTiers.Add(tier);
            }
        }

        /// <summary>
        /// Worn walking line on the deck (critique R2-14: the deck read as a flat graybox plane, high-pass 0.97 levels in KS2a).
        /// The painted deck's top face is rebuilt as <see cref="DECK_PATH_COLUMNS"/> lateral strips whose vertex G (the steel
        /// recipe's paint-wear weight, below the 0.85 bevel threshold, so no normal is bent) follows a Gaussian of half-width
        /// <see cref="DECK_PATH_HALF"/> (about 40 % of the 1.2 m width): the paint along the centre line is worn toward bare
        /// steel by about +8-15 % albedo, broken into patches by the recipe's 3 m noise, with the walking-deck scuffs and grit
        /// on top. The strips run the full length (no vertex along the long edges, so the bevels still meet the face at its two
        /// corners); ThinLine data and the face outline are those of the member's own top face, which is removed.
        /// </summary>
        public const int DECK_PATH_COLUMNS = 12;
        public const float DECK_PATH_HALF = 0.26f;
        public const byte DECK_PATH_G = 5;

        static void WornPathTop(MeshBuilder mb, float top, float z1, float z2)
        {
            List<int> I = mb.Indices;
            List<Vector3> P = mb.Positions, N = mb.Normals;
            int src = -1;
            for (int t = I.Count - 3; t >= 0; t -= 3)
            {
                int a = I[t], b = I[t + 1], c = I[t + 2];
                if (N[a].y < 0.9999f || N[b].y < 0.9999f || N[c].y < 0.9999f) continue;
                if (P[a].y < top - 1e-4f || P[b].y < top - 1e-4f || P[c].y < top - 1e-4f) continue;
                src = a;
                I.RemoveRange(t, 3);
            }
            if (src < 0) return;
            float xe = AbyssConst.BRIDGE_W * 0.5f - DECK_BEVEL, ze0 = z1 + DECK_BEVEL, ze1 = z2 - DECK_BEVEL;
            bool w1 = mb.WriteUV1, w2 = mb.WriteUV2;
            Vector4 c1 = mb.CurrentUV1, c2 = mb.CurrentUV2;
            if (mb.UV1.Count > src) { mb.WriteUV1 = true; mb.CurrentUV1 = mb.UV1[src]; }
            if (mb.UV2.Count > src) { mb.WriteUV2 = true; mb.CurrentUV2 = mb.UV2[src]; }
            Color32 saved = mb.Color;
            mb.Color = mb.Colors[src];
            int first = mb.VertexCount;
            MeshKit.GridQuad(mb, new Vector3(-xe, top, ze0), Vector3.right, Vector3.forward, 2f * xe, ze1 - ze0, DECK_PATH_COLUMNS, 1);
            for (int i = first; i < mb.VertexCount; i++)
            {
                float u = P[i].x / DECK_PATH_HALF;
                Color32 col = mb.Colors[i];
                col.g = (byte)Mathf.RoundToInt(DECK_PATH_G * Mathf.Exp(-u * u));
                mb.Colors[i] = col;
            }
            mb.Color = saved;
            mb.WriteUV1 = w1; mb.CurrentUV1 = c1;
            mb.WriteUV2 = w2; mb.CurrentUV2 = c2;
        }

        bool NearLamp(float z, float d)
        {
            for (int i = 0; i < AbyssConst.BRIDGE_LAMP_Z.Length; i++) if (Mathf.Abs(z - LampZ(i)) < d) return true;
            return false;
        }
    }
}
