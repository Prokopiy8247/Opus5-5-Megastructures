using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// The player's shadow (14.2 Shadows, 14.5.7 scale cues): a humanoid proxy drawn ShadowsOnly with
    /// MaterialLibrary[MatId.ShadowCaster], a child of the Player so it follows the feet exactly (also on the lift).
    /// Enabled in beats 2, 3 and 7, where the key light can throw a body shadow onto the deck, ledge and balcony; in beat 7 it
    /// falls behind the player and is seen when looking down or back. The camera is inside the head, which is never drawn in
    /// colour, depth or depth-normals passes (ShadowOnly.shader has only a ShadowCaster pass).
    ///
    /// Arbitration of 14.2 (R1-02, logged by the lead in DEVELOPMENT.md): the spec's 0.5 x 1.8 m capsule throws, under the
    /// beat-2 Key (pitch -72), a 0.5 x 1.1 m pill at the centre of the KS2b nadir view that reads as a stain, not a person.
    /// The proxy keeps the capsule's 1.8 m height and about its 0.5 m overall width but is built from a head (0.18 x 0.24 x
    /// 0.21 on a neck), a tapered torso (shoulders 0.38, waist 0.30, hips 0.33, depth 0.19-0.22), two tapered legs 0.19 m
    /// apart with feet and two hanging arms, so the shadow shows two legs, the shoulders and the head protruding beyond them.
    /// Its shadow area stays under 75 % of the capsule's (PathTests). Limb radii are about 1 cm fuller than anatomical:
    /// the Key's normal bias and the soft-shadow filter erode a thin caster by about that much per side (KS2b r01).
    ///
    /// Motion (zero allocation per frame): the body yaw follows the walk direction with the head allowed
    /// <see cref="HEAD_TURN_MAX"/> degrees off it (critically damped, omega <see cref="BODY_YAW_OMEGA"/>), or the look yaw when
    /// standing; it snaps after a teleport and in capture mode. Legs and arms swing in phase with the footsteps
    /// (PlayerController.StepPhase01: a leg is at its extreme at each foot contact), amplitude proportional to the speed, zero
    /// when standing (the QA stills always see the neutral stance).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShadow : MonoBehaviour
    {
        // ---------------------------------------------------------------- proxy dimensions (body frame: +Z forward, +X right,
        // feet on y = 0; metres)
        public const float HEIGHT = 1.8f;                 // top of the head = CAPSULE_H
        public const float HEAD_Y = 1.685f, HEAD_Z = -0.06f, HEAD_RX = 0.09f, HEAD_RY = 0.12f, HEAD_RZ = 0.105f;
        public const float HIP_X = 0.095f, HIP_Y = 0.90f;
        public const float SHOULDER_X = 0.19f, SHOULDER_Y = 1.42f, SHOULDER_Z = -0.01f;
        public const float LEG_LENGTH = 0.80f;            // hip pivot to the ankle sphere centre
        /// <summary>Overall width (shoulders with the arms) of the neutral stance; the spec capsule was 0.5 m.</summary>
        public const float MAX_WIDTH = 0.55f;

        // ---------------------------------------------------------------- motion
        /// <summary>Leg swing (degrees from vertical) at 1.5 m/s; scales with speed up to x1.35 when brisk.</summary>
        public const float LEG_SWING = 20f;
        public const float ARM_SWING_RATIO = 0.6f;
        public const float BODY_YAW_OMEGA = 5f;
        public const float HEAD_TURN_MAX = 50f;
        const float MovingSpeed = 0.3f;

        public enum Part { Body = 0, LegL = 1, LegR = 2, ArmL = 3, ArmR = 4 }
        public const int PART_COUNT = 5;

        [Tooltip("Show the shadow in every beat (QA).")]
        public bool ForceVisible;

        PlayerController _pc;
        Transform _root;
        readonly Transform[] _parts = new Transform[PART_COUNT];
        readonly MeshRenderer[] _mr = new MeshRenderer[PART_COUNT];
        readonly Mesh[] _meshes = new Mesh[PART_COUNT];
        bool _hasMaterial, _on, _snap = true;
        float _bodyYaw, _bodyYawVel;
        float _lastStepPhase;
        float _parity = 1f;
        int _lastTeleports = -1;

        /// <summary>Current body yaw (degrees, QA / tests).</summary>
        public float BodyYaw => _bodyYaw;

        void Awake()
        {
            _pc = GetComponent<PlayerController>();
            float skin = _pc != null ? _pc.SkinWidth : 0f;

            var rootGo = new GameObject("PlayerShadowProxy");
            rootGo.layer = gameObject.layer;
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, -skin, 0f);   // proxy feet on the floor

            var mb = new MeshBuilder();
            for (int i = 0; i < PART_COUNT; i++)
            {
                var part = (Part)i;
                mb.Clear();
                BuildPart(mb, part);
                _meshes[i] = mb.ToMesh("PlayerShadow" + part);
                var go = new GameObject("PlayerShadow" + part);
                go.layer = gameObject.layer;
                var t = go.transform;
                t.SetParent(_root, false);
                t.localPosition = PivotOf(part);
                go.AddComponent<MeshFilter>().sharedMesh = _meshes[i];
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                mr.allowOcclusionWhenDynamic = false;
                mr.enabled = false;
                _parts[i] = t;
                _mr[i] = mr;
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < PART_COUNT; i++)
                if (_meshes[i] != null) Destroy(_meshes[i]);
        }

        void LateUpdate()
        {
            if (_mr[0] == null) return;
            if (!_hasMaterial)
            {
                var lib = MaterialLibrary.Instance;
                if (lib == null) return;
                var m = lib.materials != null && lib.materials.Length > (int)MatId.ShadowCaster ? lib.materials[(int)MatId.ShadowCaster] : null;
                if (m == null) return;
                for (int i = 0; i < PART_COUNT; i++) _mr[i].sharedMaterial = m;
                _hasMaterial = true;
            }
            int b = GameState.Beat;
            bool on = ForceVisible || b == 2 || b == 3 || b == 7;
            if (on != _on)
            {
                _on = on;
                for (int i = 0; i < PART_COUNT; i++) _mr[i].enabled = on;
                _snap = true;
            }
            if (!on || _pc == null) return;
            Animate(Time.deltaTime);
        }

        void Animate(float dt)
        {
            var pc = _pc;
            if (pc.TeleportCount != _lastTeleports) { _lastTeleports = pc.TeleportCount; _snap = true; }
            if (GameState.CaptureMode) _snap = true;   // stills: deterministic pose whatever the settle frames

            Vector3 v = pc.LocalVelocity;
            float speed = pc.HorizontalSpeed;
            float target = BodyYawTarget(pc.Yaw, v.x, v.z, speed);
            if (_snap)
            {
                _bodyYaw = target;
                _bodyYawVel = 0f;
                _snap = false;
            }
            else if (dt > 0f)
            {
                float goal = _bodyYaw + Mathf.DeltaAngle(_bodyYaw, target);
                float delta = _bodyYaw - goal;
                float j = (_bodyYawVel + BODY_YAW_OMEGA * delta) * dt;
                float e = Mathf.Exp(-BODY_YAW_OMEGA * dt);
                _bodyYaw = goal + (delta + j) * e;
                _bodyYawVel = (_bodyYawVel - BODY_YAW_OMEGA * j) * e;
                if (_bodyYaw >= 360f || _bodyYaw < 0f) _bodyYaw = Mathf.Repeat(_bodyYaw, 360f);
            }
            _root.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);   // the Player transform itself is never rotated

            // gait: one leg at its extreme at every foot contact (StepPhase01 wraps to 0), the other leg opposite
            float phase = pc.StepPhase01;
            if (phase < _lastStepPhase - 0.5f) _parity = -_parity;
            _lastStepPhase = phase;
            float swing = GaitSwing(speed, phase, _parity);
            _parts[(int)Part.LegL].localRotation = Quaternion.Euler(swing, 0f, 0f);
            _parts[(int)Part.LegR].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            _parts[(int)Part.ArmL].localRotation = Quaternion.Euler(-swing * ARM_SWING_RATIO, 0f, 0f);
            _parts[(int)Part.ArmR].localRotation = Quaternion.Euler(swing * ARM_SWING_RATIO, 0f, 0f);
        }

        // ---------------------------------------------------------------- pure helpers (EditMode tests)

        /// <summary>
        /// Body yaw the torso turns to: the look yaw when standing; when walking the walk direction (reversed when walking
        /// backwards), turned only by the part of the look that exceeds the <see cref="HEAD_TURN_MAX"/> head turn.
        /// </summary>
        public static float BodyYawTarget(float lookYaw, float vx, float vz, float speed)
        {
            if (speed < MovingSpeed) return Mathf.Repeat(lookYaw, 360f);
            float mv = Mathf.Atan2(vx, vz) * Mathf.Rad2Deg;
            float rel = Mathf.DeltaAngle(mv, lookYaw);
            if (Mathf.Abs(rel) > 120f) { mv += 180f; rel = Mathf.DeltaAngle(mv, lookYaw); }
            return Mathf.Repeat(mv + (rel - Mathf.Clamp(rel, -HEAD_TURN_MAX, HEAD_TURN_MAX)), 360f);
        }

        /// <summary>Leg swing in degrees (left leg; the right leg is its negative) at a speed and step phase.</summary>
        public static float GaitSwing(float speed, float stepPhase01, float parity)
        {
            float amp = LEG_SWING * Mathf.Clamp(speed / AbyssConst.V_WALK, 0f, 1.35f);
            return amp * parity * Mathf.Cos(Mathf.PI * Mathf.Clamp01(stepPhase01));
        }

        /// <summary>Pivot of a part in the body frame (hips and shoulders for the limbs).</summary>
        public static Vector3 PivotOf(Part part)
        {
            switch (part)
            {
                case Part.LegL: return new Vector3(-HIP_X, HIP_Y, 0f);
                case Part.LegR: return new Vector3(HIP_X, HIP_Y, 0f);
                case Part.ArmL: return new Vector3(-SHOULDER_X, SHOULDER_Y, SHOULDER_Z);
                case Part.ArmR: return new Vector3(SHOULDER_X, SHOULDER_Y, SHOULDER_Z);
                default: return Vector3.zero;
            }
        }

        /// <summary>Append the whole proxy in the body frame with the given leg swing (tests project it as a shadow).</summary>
        public static void BuildPose(MeshBuilder mb, float legSwingDeg)
        {
            for (int i = 0; i < PART_COUNT; i++)
            {
                var part = (Part)i;
                float a = 0f;
                if (part == Part.LegL) a = legSwingDeg;
                else if (part == Part.LegR) a = -legSwingDeg;
                else if (part == Part.ArmL) a = -legSwingDeg * ARM_SWING_RATIO;
                else if (part == Part.ArmR) a = legSwingDeg * ARM_SWING_RATIO;
                mb.Push(PivotOf(part), Quaternion.Euler(a, 0f, 0f));
                BuildPart(mb, part);
                mb.Pop();
            }
        }

        /// <summary>One part in its pivot frame: closed shells with outward winding, no degenerate triangles.</summary>
        public static void BuildPart(MeshBuilder mb, Part part)
        {
            switch (part)
            {
                case Part.Body: BuildBody(mb); break;
                case Part.LegL: BuildLeg(mb, -1f); break;
                case Part.LegR: BuildLeg(mb, 1f); break;
                case Part.ArmL: BuildArm(mb, -1f); break;
                default: BuildArm(mb, 1f); break;
            }
        }

        // torso rings (bottom to top): y, half-width, half-depth, centre z
        static readonly float[] TorsoY = { 0.86f, 0.94f, 1.04f, 1.12f, 1.24f, 1.34f, 1.42f, 1.47f, 1.505f };
        static readonly float[] TorsoHX = { 0.150f, 0.165f, 0.160f, 0.148f, 0.168f, 0.185f, 0.190f, 0.150f, 0.075f };
        static readonly float[] TorsoHZ = { 0.095f, 0.105f, 0.100f, 0.095f, 0.110f, 0.112f, 0.100f, 0.080f, 0.060f };
        static readonly float[] TorsoCZ = { 0f, 0f, 0f, 0f, 0.01f, 0.005f, -0.01f, -0.015f, -0.015f };

        static void BuildBody(MeshBuilder mb)
        {
            // torso (rounded-box cross-sections)
            Loft(mb, 20, 2.6f, new Vector3(0f, 0.835f, 0f), new Vector3(0f, 1.515f, -0.015f), TorsoY, TorsoHX, TorsoHZ, TorsoCZ);
            // neck
            Loft(mb, 12, 2f, new Vector3(0f, 1.455f, -0.02f), new Vector3(0f, 1.635f, -0.035f),
                 new[] { 1.47f, 1.55f, 1.62f }, new[] { 0.055f, 0.052f, 0.055f }, new[] { 0.055f, 0.052f, 0.055f },
                 new[] { -0.02f, -0.025f, -0.035f });
            // head (ellipsoid; the eye at (0, 1.7, 0) sits at its front)
            const int rings = 9;
            var ys = new float[rings - 1];
            var hx = new float[rings - 1];
            var hz = new float[rings - 1];
            var cz = new float[rings - 1];
            for (int i = 1; i < rings; i++)
            {
                float th = (-0.5f + (float)i / rings) * Mathf.PI;
                ys[i - 1] = HEAD_Y + HEAD_RY * Mathf.Sin(th);
                hx[i - 1] = HEAD_RX * Mathf.Cos(th);
                hz[i - 1] = HEAD_RZ * Mathf.Cos(th);
                cz[i - 1] = HEAD_Z;
            }
            Loft(mb, 16, 2f, new Vector3(0f, HEAD_Y - HEAD_RY, HEAD_Z), new Vector3(0f, HEAD_Y + HEAD_RY, HEAD_Z), ys, hx, hz, cz);
        }

        static void BuildLeg(MeshBuilder mb, float side)
        {
            // thigh to ankle, splayed 2 cm outward at the ankle (feet about 0.23 m apart)
            Vector3 ankle = new Vector3(side * 0.02f, -LEG_LENGTH, 0f);
            Limb(mb, Vector3.zero, ankle, new[] { 0f, 0.25f, 0.5f, 0.62f, 1f }, new[] { 0.095f, 0.085f, 0.068f, 0.070f, 0.050f });
            // foot: heel to toe, touching the floor (y = -HIP_Y)
            float fy = -HIP_Y + 0.05f;
            Limb(mb, new Vector3(side * 0.025f, fy, -0.05f), new Vector3(side * 0.035f, fy, 0.17f), new[] { 0f, 1f }, new[] { 0.05f, 0.045f });
        }

        static void BuildArm(MeshBuilder mb, float side)
        {
            // shoulder to fingertips, hanging with a slight abduction (about 3 degrees)
            Vector3 tip = new Vector3(side * 0.035f, -0.74f, 0.02f);
            Limb(mb, Vector3.zero, tip, new[] { 0f, 0.4f, 0.78f, 0.88f, 1f }, new[] { 0.060f, 0.050f, 0.042f, 0.046f, 0.036f });
        }

        /// <summary>
        /// A closed tapered capsule from a (top) to b (bottom): radii r[k] at fractions t[k] along a -> b, hemispherical ends.
        /// </summary>
        static void Limb(MeshBuilder mb, Vector3 a, Vector3 b, float[] t, float[] r)
        {
            const int seg = 12, hemi = 3;
            Vector3 axis = b - a;
            float len = axis.magnitude;
            Quaternion rot = Quaternion.FromToRotation(Vector3.down, axis / len);
            int n = t.Length;
            int count = (hemi - 1) * 2 + n;
            var ys = new float[count];
            var rs = new float[count];
            int k = 0;
            float rb = r[n - 1], rt = r[0];
            // bottom hemisphere (local y below -len), then the body from the bottom up, then the top hemisphere
            for (int i = 1; i < hemi; i++)
            {
                float th = (-0.5f + 0.5f * i / hemi) * Mathf.PI;
                ys[k] = -len + rb * Mathf.Sin(th); rs[k] = rb * Mathf.Cos(th); k++;
            }
            for (int i = n - 1; i >= 0; i--) { ys[k] = -len * t[i]; rs[k] = r[i]; k++; }
            for (int i = 1; i < hemi; i++)
            {
                float th = 0.5f * Mathf.PI * i / hemi;
                ys[k] = rt * Mathf.Sin(th); rs[k] = rt * Mathf.Cos(th); k++;
            }
            var cz = new float[count];
            mb.Push(a, rot);
            Loft(mb, seg, 2f, new Vector3(0f, -len - rb, 0f), new Vector3(0f, rt, 0f), ys, rs, rs, cz);
            mb.Pop();
        }

        /// <summary>
        /// One closed shell: rings bottom to top (strictly increasing y) with superellipse cross-sections (half-sizes hx, hz,
        /// centre (0, y, cz)), closed by fans to the two poles. Winding outward (MeshBuilder rule), normals approximate.
        /// </summary>
        static void Loft(MeshBuilder mb, int seg, float exponent, Vector3 bottomPole, Vector3 topPole,
                         float[] ys, float[] hx, float[] hz, float[] cz)
        {
            int n = ys.Length;
            float e = 2f / exponent;
            int bottom = mb.Vertex(bottomPole, Vector3.down);
            int first = mb.VertexCount;
            for (int k = 0; k < n; k++)
            {
                int k0 = k > 0 ? k - 1 : k, k1 = k < n - 1 ? k + 1 : k;
                float dy = ys[k1] - ys[k0];
                float slope = dy > 1e-6f ? -0.5f * ((hx[k1] - hx[k0]) + (hz[k1] - hz[k0])) / dy : 0f;
                for (int j = 0; j < seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    float c = Mathf.Cos(ph), s = Mathf.Sin(ph);
                    float ux = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                    float uz = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
                    var p = new Vector3(hx[k] * ux, ys[k], cz[k] + hz[k] * uz);
                    var radial = new Vector3(c / Mathf.Max(hx[k], 1e-3f), 0f, s / Mathf.Max(hz[k], 1e-3f)).normalized;
                    mb.Vertex(p, radial + Vector3.up * slope);
                }
            }
            int top = mb.Vertex(topPole, Vector3.up);

            for (int j = 0; j < seg; j++)
                mb.Tri(bottom, first + j, first + (j + 1) % seg);
            for (int k = 0; k < n - 1; k++)
            {
                int r0 = first + k * seg, r1 = r0 + seg;
                for (int j = 0; j < seg; j++)
                {
                    int jn = (j + 1) % seg;
                    int a = r0 + j, b = r0 + jn, c = r1 + j, d = r1 + jn;
                    mb.Tri(a, c, b);
                    mb.Tri(b, c, d);
                }
            }
            int last = first + (n - 1) * seg;
            for (int j = 0; j < seg; j++)
                mb.Tri(top, last + (j + 1) % seg, last + j);
        }
    }
}
