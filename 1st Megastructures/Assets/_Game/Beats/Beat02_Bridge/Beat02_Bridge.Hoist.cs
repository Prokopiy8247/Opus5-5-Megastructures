using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2, the far hoist block (14.5.2 f): a 10 m steel cage block on the far flat (z 754.5, lead arbitration) at
    /// x = <see cref="HOIST_X"/>, moving y(t) = 200 - 700 S(PingPong(u0 + t / 583.3)) from the global clock. It runs in the
    /// hoist lane (rings gapped by <see cref="Beat02Rings"/>) on two guide rails from a buffer plinth at y -530 to the shaft top,
    /// hangs on two cables, and carries two cold lamps (emissive quads 2.5 x 1.0 m plus Glow points of at least 2.5 px) and a
    /// cold wash light that throws a slow-moving pool on the wall below it. Graybox: the block and its two lamp boxes only.
    /// </summary>
    public sealed partial class Beat02_Bridge
    {
        /// <summary>
        /// Look-dev (bridge r01): the lane moved from the spec's x = 60 (AbyssConst.HOIST_POS.x) to x = 140. From every point of
        /// the bridge (x 0, z 66-216) the far flat at x = 60 lies behind C1 (bearing 4.9-5.4 degrees, C1 subtends 7.1-12 degrees
        /// half-width), so 14.5.2 f "seen from the bridge" and KS2a "the hoist block as a 1 degree light" were unreachable. At
        /// x = 140 it stands between C1 and C3: 12.4 degrees right of KS2a's axis (C1 ends at 10.2, C3 starts at 16.1), 11.4 at
        /// the bridge start. Reported to the lead to update AbyssConst.HOIST_POS.
        /// </summary>
        public const float HOIST_X = 140f;
        public static Vector3 HoistPos => new Vector3(HOIST_X, AbyssConst.HOIST_POS.y, AbyssConst.HOIST_POS.z);

        /// <summary>
        /// Phase of the ping-pong (arbitration of 14.5.2 f "u0 = 0.2166 so that y(0) = 150", critique R2-07 (3)). With the
        /// spec's u0 the block sinks from y 150 at clock 0 and passes the level of the ledge lamps and the ledge cold spot around
        /// clock 80-100, so in KS2a and on most of the bridge its two lamps sat 15 px above the ledge floodlight at C1's right
        /// edge and read as part of the ledge rig. u0 = 1.7146 runs the same curve on its rising half: y(40) = 150 when the
        /// walker steps onto the bridge (the spec's "+12 to +16 degrees at the start": 12.4 degrees from z 66), rising slowly
        /// (about 0.1 degree per second at first) to the top turn at clock 166 (y 182 at clock 80, 199.6 at 144), so over the
        /// whole crossing (clock 40-144) it stays at y 150 or higher, at least 11 degrees above the ledge lights. The y range,
        /// period, speed profile and lane are unchanged. y(0) = 99 (the walker is in the gallery then; the hoist is unseen).
        /// </summary>
        public const double HOIST_U0 = 1.7146;

        const float HOIST_LAMP_W = 2.5f, HOIST_LAMP_H = 1.0f, HOIST_LAMP_X = 2.2f, HOIST_LAMP_Y = 1.6f;
        const float HOIST_POST = 0.5f, HOIST_RAIL = 0.46f, HOIST_BODY = 9.2f;
        const float GUIDE_X = 3.5f, GUIDE_W = 0.4f, GUIDE_D = 0.5f, GUIDE_EMBED = 0.05f, GUIDE_BOTTOM_Y = -530f;
        const float CABLE_R = 0.1f, CABLE_X = 1.2f, CABLE_LENGTH = 1700f;
        const float HOIST_GLOW_D = 1.2f, HOIST_GLOW_STRENGTH = 4f;
        /// <summary>R2-07 (3): the two hoist lamps read as 3-4 px points at 640 m (1.2 m is 1.6 px there), not 2.5 px specks.</summary>
        public const float HOIST_GLOW_MIN_PX = 3.5f;
        const float WASH_I = 2500f, WASH_RANGE = 90f, WASH_ANGLE = 80f;

        Transform _hoist;
        MeshRenderer _hoistBody;
        GameObject _hoistDetail, _hoistProxy;
        Light _hoistWash;

        void BuildHoist(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            bool gray = _tier == BeatTier.Graybox;
            float s = AbyssConst.HOIST_SIZE, h = 0.5f * s;
            _hoist = BeatKit.Group(_geo, "Hoist", HoistPos, Quaternion.identity);

            if (gray)
            {
                MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(s, s, s), 0.05f);
                _hoistBody = BeatKit.Emit(mb, _hoist, "HoistBlock", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.Off);
                for (int i = 0; i < 2; i++)
                    MeshKit.ChamferBox(mb, new Vector3((i == 0 ? -1f : 1f) * HOIST_LAMP_X, HOIST_LAMP_Y, -h - 0.05f), new Vector3(HOIST_LAMP_W, HOIST_LAMP_H, 0.12f), 0.02f);
                BeatKit.Emit(mb, _hoist, "HoistLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);
                ApplyHoist(SceneClock.Time);
                return;
            }

            Material steel = mats.Variant(MatId.SteelWeathered, "b2hoist", m => m.SetFloat(RustId, 0.07f));
            Material rail = mats.Variant(MatId.SteelPainted, "b2thin", m =>
            {
                m.SetFloat(RustId, 0.04f);
                m.SetFloat(WearId, 1.1f);
                m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX);
            });

            // ---- detail: cage frame (4 posts full height, 8 edge rails between them, slightly thinner so no face is shared),
            // recessed body, crosshead on top, lamp visors
            _hoistDetail = BeatKit.Group(_hoist, "Detail").gameObject;
            Transform det = _hoistDetail.transform;
            float pc = h - 0.5f * HOIST_POST;                   // 4.75
            float re = pc - 0.5f * HOIST_POST + 0.02f;          // rails end 2 cm inside the posts (4.52)
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -pc : pc, z = (i & 2) == 0 ? -pc : pc;
                MeshKit.ChamferMember(mb, new Vector3(x, -h, z), new Vector3(x, h, z), HOIST_POST, HOIST_POST, 0.03f, Vector3.forward);
            }
            for (int i = 0; i < 4; i++)
            {
                float yy = (i & 1) == 0 ? -pc : pc, side = (i & 2) == 0 ? -pc : pc;
                MeshKit.ChamferMember(mb, new Vector3(-re, yy, side), new Vector3(re, yy, side), HOIST_RAIL, HOIST_RAIL, 0.03f, Vector3.up);
                MeshKit.ChamferMember(mb, new Vector3(side, yy, -re), new Vector3(side, yy, re), HOIST_RAIL, HOIST_RAIL, 0.03f, Vector3.up);
            }
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(HOIST_BODY, HOIST_BODY, HOIST_BODY), 0.08f);
            MeshKit.ChamferBox(mb, new Vector3(0f, h + 0.35f, 0f), new Vector3(s, 0.8f, 2f), 0.03f);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * HOIST_LAMP_X;
                MeshKit.ChamferBox(mb, new Vector3(x, HOIST_LAMP_Y + 0.5f * HOIST_LAMP_H + 0.08f, -0.5f * HOIST_BODY - 0.23f), new Vector3(HOIST_LAMP_W + 0.3f, 0.12f, 0.5f), 0.02f);
            }
            _hoistBody = BeatKit.Emit(mb, det, "HoistBlock", steel, _meshes, ShadowCastingMode.Off);

            // ---- proxy (skeleton after d 380): one chamfered block of the same silhouette whose front face runs through the
            // lamp panels (z -4.62), so the lamps stay visible
            _hoistProxy = BeatKit.Group(_hoist, "Proxy").gameObject;
            float pf = -0.5f * HOIST_BODY - 0.02f;
            MeshKit.ChamferBox(mb, new Vector3(0f, 0f, 0.5f * (pf + h)), new Vector3(s, s, h - pf), 0.05f);
            BeatKit.Emit(mb, _hoistProxy.transform, "HoistProxy", steel, _meshes, ShadowCastingMode.Off);
            _hoistProxy.SetActive(false);

            // ---- two cold lamps on the front (-Z) face: emissive panels 2 cm into the body, plus min-pixel glow points
            for (int i = 0; i < 2; i++)
                MeshKit.ChamferBox(mb, new Vector3((i == 0 ? -1f : 1f) * HOIST_LAMP_X, HOIST_LAMP_Y, -0.5f * HOIST_BODY - 0.03f),
                    new Vector3(HOIST_LAMP_W, HOIST_LAMP_H, 0.1f), 0.02f);
            BeatKit.Emit(mb, _hoist, "HoistLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);
            Material glow = GlowMaterial(mats, "b2hoistlamp", AbyssPalette.COLD_LAMP, HOIST_GLOW_STRENGTH, 0.3f, HOIST_GLOW_MIN_PX);
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3((i == 0 ? -1f : 1f) * HOIST_LAMP_X, HOIST_LAMP_Y, -0.5f * HOIST_BODY - 0.1f);
                MeshRenderer g = BeatKit.Renderer(_hoist, "HoistGlow" + i, _glowQuad, glow, p, Quaternion.identity, ShadowCastingMode.Off);
                g.transform.localScale = Vector3.one * HOIST_GLOW_D;
                g.receiveShadows = false;
            }

            // ---- cables from the crosshead up past the shaft top (chunk-local, 200 m pieces; they never leave the lane)
            Material cable = mats[MatId.Cable];
            float cy0 = h + 0.7f;
            for (float a = 0f; a < CABLE_LENGTH - 1e-3f; a += CHUNK)
            {
                float len = Mathf.Min(CHUNK, CABLE_LENGTH - a);
                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0 ? -1f : 1f) * CABLE_X;
                    MeshKit.Cylinder(mb, new Vector3(x, 0f, 0f), new Vector3(x, len, 0f), CABLE_R, 6, false, true);
                }
                BeatKit.Emit(mb, _hoist, "Cables_" + Mathf.RoundToInt(a), cable, _meshes, ShadowCastingMode.Off, new Vector3(0f, cy0 + a, 0f), true);
            }

            // ---- guide rails on the far flat (world-fixed, 200 m chunks) and the buffer plinth at the lane bottom
            Transform lane = BeatKit.Group(_geo, "HoistLane");
            float zr = AbyssConst.SHAFT_FAR_Z + GUIDE_EMBED - 0.5f * GUIDE_D;
            float hx = HOIST_X;
            for (float y1 = AbyssConst.SHAFT_TOP_Y; y1 > GUIDE_BOTTOM_Y + 1e-3f; y1 -= CHUNK)
            {
                float y0 = Mathf.Max(GUIDE_BOTTOM_Y - 0.2f, y1 - CHUNK);
                var origin = new Vector3(hx, y0, zr);
                for (int i = 0; i < 2; i++)
                {
                    float x = (i == 0 ? -1f : 1f) * GUIDE_X;
                    MeshKit.ChamferMember(mb, new Vector3(x, 0f, 0f), new Vector3(x, y1 - y0, 0f), GUIDE_W, GUIDE_D, 0.02f, Vector3.forward, true);
                }
                BeatKit.Emit(mb, lane, "GuideRails_" + Mathf.RoundToInt(y0), rail, _meshes, ShadowCastingMode.Off, origin, true);
            }
            MeshKit.ChamferBox(mb, new Vector3(hx, GUIDE_BOTTOM_Y - 2f, AbyssConst.SHAFT_FAR_Z - 1.85f), new Vector3(16f, 4f, 4.3f), 0.05f);
            BeatKit.Emit(mb, lane, "LaneBuffer", mats[MatId.ConcreteBoard], _meshes, ShadowCastingMode.Off);

            // ---- cold wash light under the block, aimed down and onto the wall below it
            _hoistWash = BeatKit.Spot(_hoist, "HoistWash", new Vector3(0f, -h - 0.4f, -2f), new Vector3(0f, -1f, 0.3f), AbyssPalette.COLD_LAMP,
                WASH_I, WASH_RANGE, WASH_ANGLE);
            ApplyHoist(SceneClock.Time);
        }

        /// <summary>Hoist height y(t) = 200 - 700 S(PingPong(u0 + t / 583.3)), S = smootherstep (14.5.2 f; u0 = <see cref="HOIST_U0"/>), in double.</summary>
        public static double HoistY(double t)
        {
            double u = HOIST_U0 + t / AbyssConst.HOIST_PERIOD;
            double w = u - 2.0 * System.Math.Floor(u * 0.5);
            double p = w < 1.0 ? w : 2.0 - w;
            double sm = p * p * p * (p * (p * 6.0 - 15.0) + 10.0);
            return HoistYTop - HoistTravel * sm;
        }

        void ApplyHoist(double t)
        {
            if (_hoist == null) return;
            Vector3 p = HoistPos;
            p.y = (float)HoistY(t);
            _hoist.localPosition = p;
        }
    }
}
