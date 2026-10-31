using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2, the falling bolt (14.5.2 Motion, lead decision D3 of fix round 2) and the row of cold service lamps on the -30
    /// belt of the right-hand far wall (face 0, normal 30 degrees) that it falls from.
    ///
    /// D3 (critique R2-09): the spec drops the bolt from beside the second outrigger lamp, 0.9 m outside the deck edge, where
    /// the 1.2 m deck occludes it from every walkable eye within about a second (a point at lateral offset X is hidden once it
    /// is more than about 1.83 X below the deck), so the "free-fall depth cue that dissolves into the fog between fall seconds
    /// 8 and 12" could never be seen. The bolt now comes loose from a cold lamp bracketed to the inner face of the -30 belt on
    /// face 0, <see cref="BOLT_LAMP_X"/> = 300 m to the right of the bridge axis (z about 550): it stays in open view down to
    /// about 550 m below the deck (fall second 10.6), well past the dissolve start. It is released once per run, when d first
    /// reaches <see cref="BoltReleaseD"/> by walking (beat-2 time <see cref="BOLT_BEAT_TIME"/> = 15 s, d 82.5), in the middle of
    /// the gaze "14-22 pan right to the wall" (GazeCues.Beat2 13-18 s: relative yaw 12 to 65); from the walker it sits at
    /// bearing 33 degrees, 3 degrees below the horizon, 555 m away. Positioned from the global clock (free fall, g = 9.81), it
    /// passes the rib and belt levels 1.5 m clear of the belts' inner faces and is dissolved over fall seconds 8-12 (314-706 m
    /// down, where the downward fog has already taken most of it). Emissive COLD_LAMP x 3.0 (spec), drawn as a Glow sprite of at
    /// least <see cref="BOLT_MIN_PX"/> px (spec at least 2.5). No sound, no light.
    ///
    /// The lamp row: three small cold flood lamps on 1.5 m brackets in front of the belt's inner face at y -31 (the bolt lamp near
    /// the face centre, two more 123 and 210 m along toward the far corner, which stand behind C3 seen from the bridge and are
    /// twin floods whose up and down washes stage C3, see Beat02_Bridge.FarLayer). Each head is a steel can with a cold lens and
    /// a Glow point of at least 2.5 px; they are always on (far fixtures).
    /// </summary>
    public sealed partial class Beat02_Bridge
    {
        /// <summary>Face index of the right-hand far wall (normal 30 degrees, MeshKit.HexFaceNormal(0)).</summary>
        public const int BELT_LAMP_FACE = 0;
        /// <summary>The belt the lamps hang on: k 20, y -30 (band -36..-24, inner face at apothem 330).</summary>
        public const int BELT_LAMP_K = 20;
        /// <summary>Lateral offset of the bolt lamp from the bridge axis x = 0 (lead decision D3: about 300 m).</summary>
        public const float BOLT_LAMP_X = 300f;
        /// <summary>Beat-2 time of the release (GazeCues beat time = (d - 60) / 1.5): 15 s, inside the 14-22 s pan right.</summary>
        public const float BOLT_BEAT_TIME = 15f;
        /// <summary>
        /// Minimum drawn diameter of the bolt's Glow sprite in pixels (D3: at least 2.5). 4: the Gaussian sprite's half-maximum
        /// width is about 0.74 of the drawn diameter, so 4 px gives a 3 px spark that survives the YouTube encode at 555 m.
        /// </summary>
        public const float BOLT_MIN_PX = 4f;
        /// <summary>The lamp heads stand this far in front of the belt's inner face (on a bracket); the bolt falls from there.</summary>
        public const float BELT_LAMP_STANDOFF = 1.5f;
        /// <summary>Lamp head height: 1 m below the belt centre, on the vertical part of the inner face (below its top chamfer).</summary>
        public const float BELT_LAMP_DROP = 1f;
        /// <summary>
        /// Positions of the row along face 0, metres from the bolt lamp toward the far corner; [0] is the bolt lamp (x 300, z 547).
        /// Lamp 1 (x 238, z 654) and lamp 2 (x 195, z 729; the lamps' line meets the inner corner 221 m from the bolt lamp)
        /// stand behind C3 seen from the bridge (C3 covers bearings 16-25 degrees from KS2a; the lamps 24 and 18).
        /// </summary>
        static readonly float[] s_beltLampU = { 0f, 123f, 210f };
        /// <summary>The up head of a twin flood sits this far above the down head.</summary>
        const float BELT_LAMP_TWIN_UP = 0.5f;
        const float BELT_LAMP_GLOW_D = 0.45f, BELT_LAMP_GLOW_STRENGTH = 2f, BELT_LAMP_GLOW_ENERGY = 0.5f;

        /// <summary>d at which the bolt is released (82.5).</summary>
        public static float BoltReleaseD => AbyssConst.BEAT_START_D[1] + BOLT_BEAT_TIME * AbyssConst.V_WALK;

        /// <summary>y of the belt the lamps hang on.</summary>
        public static float BeltLampBeltY => AbyssConst.RingY(BELT_LAMP_K);

        /// <summary>Point on face 0 at apothem a (from the shaft axis), u metres along the face from its centre, height y.</summary>
        public static Vector3 OnFace(int face, float apothem, float u, float y)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            var t = new Vector2(-n.y, n.x);
            return new Vector3(c.x + n.x * apothem + t.x * u, y, c.y + n.y * apothem + t.y * u);
        }

        /// <summary>u (along face 0 from its centre) of the point of face 0's plane with world x = <paramref name="x"/>.</summary>
        static float FaceUAtX(int face, float apothem, float x)
        {
            Vector2 n = MeshKit.HexFaceNormal(face);
            float tx = -n.y;
            return (x - (AbyssConst.SHAFT_CENTRE.x + n.x * apothem)) / tx;
        }

        /// <summary>Head (lens centre) of belt lamp i; i = 0 is the bolt lamp at x = BOLT_LAMP_X.</summary>
        public static Vector3 BeltLampHead(int i)
        {
            float a = AbyssConst.BELT_INNER_APOTHEM - BELT_LAMP_STANDOFF;
            float u0 = FaceUAtX(BELT_LAMP_FACE, a, BOLT_LAMP_X);
            return OnFace(BELT_LAMP_FACE, a, u0 + s_beltLampU[i], BeltLampBeltY - BELT_LAMP_DROP);
        }

        public static int BeltLampCount => s_beltLampU.Length;

        /// <summary>Release point of the bolt: just under the bolt lamp's can.</summary>
        public static Vector3 BoltStart => BeltLampHead(0) + new Vector3(0f, -0.3f, 0f);

        // ---------------------------------------------------------------- state
        MeshRenderer _bolt;
        MaterialPropertyBlock _boltMpb;
        Vector3 _boltStart;
        double _boltT0 = -1.0;
        bool _boltDone;
        float _prevD = float.NaN;
        float _boltFade = -1f;

        void BuildBolt(BeatContext ctx, MeshBuilder mb)
        {
            if (_tier == BeatTier.Graybox) return;
            BuildBeltLamps(ctx, mb);
            Material mat = GlowMaterial(ctx.Mats, "b2bolt", AbyssPalette.COLD_LAMP, BOLT_STRENGTH, 0f, BOLT_MIN_PX);
            _boltStart = BoltStart;
            _bolt = BeatKit.Renderer(_geo, "FallingBolt", _glowQuad, mat, _boltStart, Quaternion.identity, ShadowCastingMode.Off);
            _bolt.transform.localScale = Vector3.one * BOLT_D;
            _bolt.receiveShadows = false;
            _bolt.enabled = false;
            _boltMpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// The three belt lamps: a steel bracket from the belt's inner face, a small can with a cold lens facing down and out, and a
        /// Glow point (one instanced batch). Vertices are lamp-local (the renderers carry the lamp positions).
        /// </summary>
        void BuildBeltLamps(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform g = BeatKit.Group(_geo, "BeltLamps");
            Material steel = mats.Variant(MatId.SteelPainted, "b2beltlamp", m =>
            {
                m.SetFloat(RustId, 0.04f);
                m.SetFloat(WearId, 1f);
            });
            Vector2 n2 = MeshKit.HexFaceNormal(BELT_LAMP_FACE);
            var outward = new Vector3(n2.x, 0f, n2.y);            // toward the wall
            var inward = -outward;                                 // toward the shaft axis
            var glow = new InstancedBatch("B2_BeltLampGlows", _glowQuad,
                GlowMaterial(mats, "b2beltlampglow", AbyssPalette.COLD_LAMP, BELT_LAMP_GLOW_STRENGTH, BELT_LAMP_GLOW_ENERGY),
                ShadowCastingMode.Off, false, 0, 1000f);
            for (int i = 0; i < BeltLampCount; i++)
            {
                Vector3 head = BeltLampHead(i);
                // can aimed 30 degrees off straight down toward the wall below (its wash), lens at the head
                Vector3 dir = (Vector3.down * 0.87f + outward * 0.5f).normalized;
                MeshKit.TaperedCylinder(mb, -dir * 0.3f, dir * 0.12f, 0.14f, 0.18f, 16, true);
                // bracket: a horizontal arm from 0.2 m inside the belt's inner face to the back of the can
                Vector3 back = -dir * 0.3f;
                MeshKit.ChamferMember(mb, outward * (BELT_LAMP_STANDOFF + 0.2f) + Vector3.up * (back.y + 0.08f),
                    back + inward * 0.05f + Vector3.up * 0.08f, 0.1f, 0.1f, 0.01f, Vector3.up);
                BeatKit.Emit(mb, g, "BeltLamp" + i, steel, _meshes, ShadowCastingMode.Off, head);
                MeshKit.Disc(mb, dir * 0.125f, dir, 0.17f, 16);
                BeatKit.Emit(mb, g, "BeltLampLens" + i, mats[MatId.LampCold], _meshes, ShadowCastingMode.Off, head);
                glow.Add(Matrix4x4.TRS(head + dir * 0.2f, Quaternion.identity, Vector3.one * BELT_LAMP_GLOW_D));
                if (i > 0)
                {
                    // twin flood (FarLayer): a second can above the first, aimed 30 degrees off straight up toward the wall above
                    Vector3 o = Vector3.up * BELT_LAMP_TWIN_UP;
                    Vector3 dirUp = (Vector3.up * 0.87f + outward * 0.5f).normalized;
                    MeshKit.TaperedCylinder(mb, o - dirUp * 0.3f, o + dirUp * 0.12f, 0.14f, 0.18f, 16, true);
                    Vector3 backUp = o - dirUp * 0.3f;
                    MeshKit.ChamferMember(mb, outward * (BELT_LAMP_STANDOFF + 0.2f) + Vector3.up * (backUp.y - 0.08f),
                        backUp + inward * 0.05f + Vector3.up * -0.08f, 0.1f, 0.1f, 0.01f, Vector3.up);
                    BeatKit.Emit(mb, g, "BeltLampUp" + i, steel, _meshes, ShadowCastingMode.Off, head);
                    MeshKit.Disc(mb, o + dirUp * 0.125f, dirUp, 0.17f, 16);
                    BeatKit.Emit(mb, g, "BeltLampUpLens" + i, mats[MatId.LampCold], _meshes, ShadowCastingMode.Off, head);
                    glow.Add(Matrix4x4.TRS(head + o + dirUp * 0.2f, Quaternion.identity, Vector3.one * BELT_LAMP_GLOW_D));
                }
            }
            _batches.Add(glow);
        }

        void TickBolt(in BeatFrame f)
        {
            if (_bolt == null) return;
            float dRel = BoltReleaseD;
            if (_boltT0 < 0.0 && !_boltDone && !float.IsNaN(_prevD) && _prevD < dRel && f.D >= dRel && f.D - _prevD < 2f && !QaHooks.Capturing)
                _boltT0 = f.Time;
            _prevD = f.D;
            ApplyBolt(f.Time);
        }

        /// <summary>Height change of the falling bolt after the given fall seconds (free fall, g = 9.81).</summary>
        public static double BoltFallY(double fallSeconds) => -0.5 * BOLT_G * fallSeconds * fallSeconds;

        /// <summary>Visibility of the falling bolt at a fall second (1 until 8 s, smooth to 0 at 12 s).</summary>
        public static float BoltVisibility(double fallSeconds)
        {
            if (fallSeconds < 0.0 || fallSeconds >= BOLT_FADE1) return 0f;
            return 1f - BeatKit.Smoothstep((float)((fallSeconds - BOLT_FADE0) / (BOLT_FADE1 - BOLT_FADE0)));
        }

        /// <summary>World position of the bolt after the given fall seconds (straight down from <see cref="BoltStart"/>).</summary>
        public static Vector3 BoltPosition(double fallSeconds)
        {
            Vector3 p = BoltStart;
            p.y = (float)(p.y + BoltFallY(fallSeconds));
            return p;
        }

        void ApplyBolt(double t)
        {
            if (_bolt == null) return;
            double tau = _boltT0 >= 0.0 ? t - _boltT0 : -1.0;
            float vis = BoltVisibility(tau);
            if (_boltT0 >= 0.0 && tau >= BOLT_FADE1) _boltDone = true;
            if (vis <= 1e-3f)
            {
                if (_bolt.enabled) _bolt.enabled = false;
                return;
            }
            Vector3 p = _boltStart;
            p.y = (float)(_boltStart.y + BoltFallY(tau));
            _bolt.transform.localPosition = p;
            if (vis != _boltFade)
            {
                _boltFade = vis;
                _boltMpb.SetFloat(ShaderIds.AbFade, vis);
                _bolt.SetPropertyBlock(_boltMpb);
            }
            if (!_bolt.enabled) _bolt.enabled = true;
        }
    }
}
