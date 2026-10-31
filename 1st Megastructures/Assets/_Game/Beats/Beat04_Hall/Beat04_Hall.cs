using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 4 - machine hall (14.5.4, d 297.5-439.5). Hall coordinates: u = -32 - x (0..110 along the walk), v = z - 269
    /// (positive to the walker's right, +Z). Partial class, one file per part:
    /// <list type="bullet">
    /// <item>Beat04_Hall.cs: constants, build orchestration, crank-slider kinematics and posing, activation (14.6), Tick, QA.</item>
    /// <item>Beat04_Hall.Hull.cs: persistent skeleton (hull exterior with the 6 m panel grid in a 24 / 12 / 6 m joint
    /// hierarchy, lamp rows, load-path girders and hangers, dark proxy cards),
    /// interior shell (walls, floor, ceiling, pilasters), 12 roof trusses, the exit proxy card.</item>
    /// <item>Beat04_Hall.Machines.cs: the four crank-slider assemblies (wheel, pin, crankshaft, rod, crosshead, piston rod,
    /// flanged cylinder, guide frames, piers, crank pits, pipes, bolts merged / instanced, detail tiers).</item>
    /// <item>Beat04_Hall.Catwalk.cs: catwalk (grating, 0.6 m bearer bars, rails, kick plates, 14 m posts), valve stations,
    /// floor pipes.</item>
    /// <item>Beat04_Hall.Lights.cs: the 14.2 rig (8 spot cans, 6 amber hazards) plus the art-direction pools (can uplights,
    /// catwalk lamps, wall washers, the lintel washer over the exit), the daylight spills through both openings and the exit
    /// haze shaft (BeamWedge mode 2); intensities calibrated against the KS4 luma targets (see the file header).</item>
    /// <item>Beat04_Hall.Graybox.cs: the Phase 3 graybox (Graybox tier; hull top at +39 like the full tier).</item>
    /// <item>Beat04_Hall.Geo.cs: lattice-aligned grid faces and small geometry helpers.</item>
    /// </list>
    /// Tiers (14.5.4 Fallbacks): Full = everything; Simplified = 2 assemblies (inner-left, inner-right) and trusses every 20 m;
    /// Graybox = boxes on the same crank equation.
    ///
    /// Activation (14.6, full and simplified tiers): the hull exterior, its lamp rows and the dark proxy cards in the openings
    /// are the persistent skeleton. The interior (shell, machines, catwalk, lights) is active for E in [250, 499.54): on at
    /// d 250 behind the closed openings; the portal card dissolves (dithered _AbFade) over d 275-285 and is gone at 285; the
    /// exit proxy (a cold gradient card behind the parked platform that closes the shaft view while the far plane is 600 m)
    /// dissolves over d 405-420; the interior is swapped back to the equal-luma dark cards at ride second 40, when the
    /// platform is under the hull and the exit face points away from the camera. Arbitration: the spec's "hall interior at
    /// d 380" lies 50 m inside the hall in full view, so it is read as "swap to the proxy once it can no longer be seen".
    ///
    /// Colliders are final (unchanged from the graybox): catwalk floor, rails as walls (z = 269 +/- 0.75), the hull faces
    /// beside the portal (x = -32) and exit (x = -142) openings, safety volume. The rod plane (y +4.5) stays 11 m above the
    /// catwalk and the inner banks keep |v| >= 3, clear of posts and rails.
    /// </summary>
    public sealed partial class Beat04_Hall : BeatBase
    {
        public override int Index => 4;

        // ================================================================================================ constants
        // ---------------------------------------------------------------- hull (graybox values kept)
        const float HULL_WALL_T = 1f, ROOF_T = 2f;
        /// <summary>Openings 20 x 24 centred on the walk line z = 269; their sills sit 12 m below the catwalk (y -19..+5).</summary>
        const float OPENING_Y0 = -19f;
        // ---------------------------------------------------------------- machines (graybox values kept, shared by every tier)
        const float WHEEL_PLANE_Y = 1f, WHEEL_T = 4f, PIN_D = 2.5f;
        /// <summary>Connecting rod, crosshead, piston rod and cylinder axis height: on top of the wheel (y -1..+3) with clearance.</summary>
        const float ROD_Y = 4.5f, ROD_SECTION = 1.5f;
        const float CROSSHEAD_L = 4f, CROSSHEAD_H = 3f, CROSSHEAD_W = 3f;
        const float PISTON_ROD_L = 27f;
        const float CYL_LEN = 28f;
        const float CYL_U0_A = 68f, CYL_U0_B = 14f;
        /// <summary>Cylinder rear head (crank side) distance from the crank centre along the slider axis (A: 68 - 20, B: 90 - 42).</summary>
        const float CYL_S0 = 48f;
        const float CRANKSHAFT_D = 3f, PIT_SIZE = 8f, PIT_H = 2f;
        const float CATWALK_GRATING_T = 0.05f, RAIL_TUBE = 0.05f, POST_SPACING = 2f;
        const float SUPPORT_SPACING = 5f, SUPPORT_V = 1.0f, SUPPORT_SECTION = 0.3f;
        const int ROOF_BEAMS = 10;
        const float BEAM_W = 1.2f, BEAM_H = 3f;
        // ---------------------------------------------------------------- 14.2 lighting rig (graybox and full)
        const float SPOT_Y = 22f, SPOT_I = 900f, SPOT_RANGE = 60f, SPOT_ANGLE = 70f, SPOT_OFFSET_U = 7f;
        const float HAZARD_I = 3.5f, HAZARD_RANGE = 8f;
        // ---------------------------------------------------------------- activation (14.6)
        const float INTERIOR_ON_E = 250f;
        /// <summary>Ride second 40: the platform is under the hull (x > -132.7 even at its -X edge), the exit face points away.</summary>
        const float INTERIOR_OFF_E = AbyssConst.D_LIFT + 40f;
        const float ACTIVATION_HYSTERESIS = 5f;
        const float PORTAL_CARD_FADE0 = 275f, PORTAL_CARD_FADE1 = 285f;
        const float EXIT_PROXY_FADE0 = 405f, EXIT_PROXY_FADE1 = 420f;
        /// <summary>E used by EditMode builds (KS4 pose, u = 28): interior on, portal open, exit proxy shown.</summary>
        const float EDIT_MODE_E = 357.54f;
        // ---------------------------------------------------------------- near-detail tier (bolts, fine dressing)
        /// <summary>
        /// Bolts and pit guards of a bank by camera distance to the bank: fully shown within 70 m when approaching (every bank is
        /// within 60 m of the catwalk, so nothing is ever held mid-dissolve inside the hall), cross-faded over 70-90 m (approach)
        /// / 90-110 m (recede): seen through the portal from the ledge and looking back from the dock.
        /// </summary>
        const float DETAIL_NEAR = 110f, DETAIL_HYST = 20f, DETAIL_FADE = 20f;
        // ---------------------------------------------------------------- KS4 composition-lint subjects (14.10)
        /// <summary>
        /// Near u limits of the far subjects at the arbitrated KS4 pose (u -2): the outer-right cylinder's far 16 m (u 80-96),
        /// the outer-left crank wheel (its whole O30 disc, u 75-105: the far half alone was 21 % unoccluded in hall_r13) and the
        /// far truss field (u 60-110, wider than the frame: judged by the in-frame rule; the last 15 m alone were 29 %
        /// unoccluded behind the nearer trusses).
        /// </summary>
        const float KS4_OUTER_RIGHT_U = 80f, KS4_OUTER_LEFT_U = 75f, KS4_TRUSS_U = 60f;
        /// <summary>
        /// Name suffix of a KS4 subject the KS4 pose cannot show (known spec conflict: the outer-right bank, hidden behind the
        /// inner-right cylinder; see <see cref="CollectSubjects"/>). QA tools match it to report a waiver instead of a regression.
        /// </summary>
        public const string SpecConflictTag = " [spec-conflict]";

        // ================================================================================================ QA types

        /// <summary>One moving or listed part for the 14.5.4 bounds test (world AABB of its mesh vertices at the current pose).</summary>
        public struct HallPart
        {
            public string Kind;   // "wheel", "rod", "pistonrod", "shaft", "crosshead", "cylinder", "post", "rail"
            public int Bank;      // 0..3, -1 for catwalk parts
            public Bounds World;
            public HallPart(string kind, int bank, Bounds world) { Kind = kind; Bank = bank; World = world; }
        }

        // ================================================================================================ state

        readonly List<Mesh> _meshes = new List<Mesh>();
        Transform _geo;
        bool _graybox, _editMode;
        bool[] _bankBuilt = new bool[4];
        MaterialLibrary _mats;
        uint _seed;

        // graybox references (also used by the full tier where noted)
        MeshRenderer _roof, _catwalk, _openBelow, _openAbove, _openLeft, _openRight;
        readonly MeshRenderer[] _cylinders = new MeshRenderer[4];
        readonly Transform[] _wheel = new Transform[4], _rod = new Transform[4], _crosshead = new Transform[4];
        readonly MeshRenderer[] _wheelR = new MeshRenderer[4], _rodR = new MeshRenderer[4];

        // full-tier moving parts
        readonly MeshRenderer[] _crossheadR = new MeshRenderer[4], _pistonRodR = new MeshRenderer[4], _shaftR = new MeshRenderer[4];
        /// <summary>Full tier: the rod mesh origin is the crank pin (graybox: rod midpoint).</summary>
        bool _rodPivotAtPin;
        /// <summary>Full tier: one wheel mesh with its pin on local +X (graybox: one mesh per type with the pin toward the slider).</summary>
        bool _wheelPinAtPlusX;
        readonly Vector3[] _pinPos = new Vector3[4], _chPos = new Vector3[4];

        // measurement sources (full tier: shell, floor, rails, posts, trusses; graybox: hull base, catwalk steel, roof beams in _trusses)
        MeshRenderer _hullShell, _hallFloor, _catwalkRails, _catwalkPosts, _trusses;
        MeshRenderer _hullBase, _grayCatwalkSteel;
        Bounds _portalReveal, _exitReveal;
        float _measuredBore, _measuredCylLength;
        int _trussCount, _spotCount, _hazardCount, _flangeBolts;

        // activation
        GameObject _interiorGo;
        MeshRenderer _portalCard, _exitCard, _exitProxy;
        MaterialPropertyBlock _cardMpb;
        bool _interiorOn = true, _activationApplied;
        float _portalFadeApplied = -1f, _exitCardApplied = -1f, _exitProxyApplied = -1f;

        // near-detail tiers per bank (merged bolts for the inner banks, instanced for the outer ones)
        readonly DetailTier[] _bankTier = new DetailTier[4];
        readonly InstancedBatch[] _bankBoltBatch = new InstancedBatch[4];
        readonly InstancedBatch[] _hullGlows = new InstancedBatch[6];

        // QA cache (built on first use, never per frame)
        Dictionary<Mesh, Vector3[]> _qaVerts;

        // ================================================================================================ coordinates

        static float X(float u) => AbyssConst.HALL_X0 - u;
        static float Z(float v) => AbyssConst.W[8].z + v;
        static float CrankU(int k) => AbyssConst.BANK_TYPE_A[k] ? AbyssConst.CRANK_U_A : AbyssConst.CRANK_U_B;
        /// <summary>World x direction of the slider axis: type A strokes toward +u (-X), type B toward -u (+X).</summary>
        static float AxisX(int k) => AbyssConst.BANK_TYPE_A[k] ? -1f : 1f;
        /// <summary>Crank centre of bank k at y = 0 (the canonical bank frame origin).</summary>
        static Vector3 BankOrigin(int k) => new Vector3(X(CrankU(k)), 0f, Z(AbyssConst.BANK_V[k]));
        /// <summary>Bank frame rotation: canonical +X (slider axis, toward the cylinder) to the world axis direction.</summary>
        static Quaternion BankRotation(int k) => AbyssConst.BANK_TYPE_A[k] ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        static Matrix4x4 BankMatrix(int k) => Matrix4x4.TRS(BankOrigin(k), BankRotation(k), Vector3.one);

        // ================================================================================================ kinematics (14.5.4)

        /// <summary>Crosshead distance from the crank centre (14.5.4), range 20..44 m.</summary>
        public static double SliderX(double thetaRad)
        {
            double r = AbyssConst.CRANK_R, l = AbyssConst.CRANK_L, s = System.Math.Sin(thetaRad);
            return r * System.Math.Cos(thetaRad) + System.Math.Sqrt(l * l - r * r * s * s);
        }

        /// <summary>Crank angle of bank k in degrees, wrapped to [0, 360), from the global clock (IL at 60 degrees at t = 219.69).</summary>
        public static double BankThetaDeg(int k, double t)
        {
            double tw = t - AbyssConst.CRANK_PERIOD * System.Math.Floor(t / AbyssConst.CRANK_PERIOD);   // exact: 3 deg/s x 120 s = 360
            double th = AbyssConst.CrankThetaIL(tw) + AbyssConst.BANK_PHASE_DEG[k];
            return th - 360.0 * System.Math.Floor(th / 360.0);
        }

        /// <summary>Crosshead distance from the crank centre of bank k at scene time t (m).</summary>
        public static double SlideDistance(int k, double t) => SliderX(BankThetaDeg(k, t) * System.Math.PI / 180.0);

        /// <summary>True when bank k was generated in this tier (Simplified builds only banks 0 and 1).</summary>
        public bool IsBankBuilt(int k) => k >= 0 && k < 4 && _bankBuilt[k];

        /// <summary>World position of the crosshead centre (pin) of bank k at the current pose.</summary>
        public Vector3 CrossheadPosition(int k) => _chPos[k];

        /// <summary>World position of the crank pin of bank k at the current pose (rod big end).</summary>
        public Vector3 PinPosition(int k) => _pinPos[k];

        /// <summary>Pose every moving part for scene time t (QA and tests; same as PrepareCapture without activation).</summary>
        public void PoseAt(double t) => ApplyPose(t);

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat04");
            _mats = ctx.Mats;
            _seed = ctx.Seed;
            _editMode = ctx.EditMode;
            _graybox = ctx.Tier == BeatTier.Graybox;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            BuildColliders(ctx);
            for (int k = 0; k < 4; k++) _bankBuilt[k] = _graybox || ctx.Tier == BeatTier.Full || k < 2;

            if (_graybox)
            {
                BuildGraybox(ctx);
                BuildGrayboxLights(ctx);
                ApplyPose(SceneClock.Time);
                return;
            }

            _rodPivotAtPin = true;
            _wheelPinAtPlusX = true;
            _cardMpb = new MaterialPropertyBlock();
            bool full = ctx.Tier == BeatTier.Full;

            // persistent skeleton: exterior hull, panel grid, lamp rows, dark cards in both openings
            BuildSkeleton(ctx);

            // interior detail (14.6: active from d 250 to ride second 40)
            Transform interior = BeatKit.Group(_geo, "Interior");
            _interiorGo = interior.gameObject;
            BuildInteriorShell(ctx, interior);
            BuildTrusses(ctx, interior, full);
            BuildExitProxy(ctx, interior);
            BuildCatwalkFull(ctx, interior);
            BuildMachines(ctx, interior);
            BuildFullLights(ctx, interior);

            ApplyPose(SceneClock.Time);
            ApplyActivation(_editMode ? EDIT_MODE_E : GameState.E, true);
            Debug.Log("[ABYSS] Beat04 built (" + ctx.Tier + "): " + _meshes.Count + " meshes, " + TriangleCount() + " triangles, "
                      + _trussCount + " trusses, " + _spotCount + " spot cans, " + _hazardCount + " hazard points");
        }

        int TriangleCount()
        {
            long tris = 0;
            for (int i = 0; i < _meshes.Count; i++)
            {
                Mesh m = _meshes[i];
                if (m == null) continue;
                for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
            }
            return (int)tris;
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            float y = AbyssConst.CATWALK_Y, hw = AbyssConst.CATWALK_W * 0.5f, zc = AbyssConst.W[8].z;
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1;
            float ow = AbyssConst.HALL_OPENING_W * 0.5f;

            CorridorKit.Floor(c, "CatwalkFloor", new Vector3((x0 + x1) * 0.5f, y, zc), new Vector2(x0 - x1, AbyssConst.CATWALK_W + 1f));
            BeatKit.Wall(c, "CatwalkRailS", new Vector2(x0, zc - hw), new Vector2(x1, zc - hw), y - 1f, 4f, new Vector2(0f, -1f));
            BeatKit.Wall(c, "CatwalkRailN", new Vector2(x0, zc + hw), new Vector2(x1, zc + hw), y - 1f, 4f, new Vector2(0f, 1f));
            // hull faces beside the catwalk at the portal (walkable side x > -32) and at the exit (walkable side x < -142)
            BeatKit.Wall(c, "PortalFaceS", new Vector2(x0, zc - ow), new Vector2(x0, zc - hw), y - 1f, 4f, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "PortalFaceN", new Vector2(x0, zc + hw), new Vector2(x0, zc + ow), y - 1f, 4f, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "ExitFaceS", new Vector2(x1, zc - ow), new Vector2(x1, zc - hw), y - 1f, 4f, new Vector2(1f, 0f));
            BeatKit.Wall(c, "ExitFaceN", new Vector2(x1, zc + hw), new Vector2(x1, zc + ow), y - 1f, 4f, new Vector2(1f, 0f));
            CorridorKit.SafetyBelow(c, "SafetyCatwalk", new Vector3((x0 + x1) * 0.5f, y, zc), new Vector2(x0 - x1, AbyssConst.CATWALK_W));
        }

        static void Box(MeshBuilder mb, float cx, float cy, float cz, float sx, float sy, float sz, float bevel = 0.03f)
            => MeshKit.ChamferBox(mb, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), bevel);

        // ================================================================================================ posing

        /// <summary>Place every moving part for scene time t (exact crank-slider kinematics, allocation-free).</summary>
        void ApplyPose(double t)
        {
            for (int k = 0; k < 4; k++)
            {
                if (!_bankBuilt[k] || _wheel[k] == null) continue;
                double thDeg = BankThetaDeg(k, t);
                double th = thDeg * System.Math.PI / 180.0;
                PoseBank(k, th, thDeg);
            }
        }

        void PoseBank(int k, double th, double thDeg)
        {
            float ax = AxisX(k);
            float xc = X(CrankU(k)), zc = Z(AbyssConst.BANK_V[k]);
            float r = AbyssConst.CRANK_R;
            float xs = (float)SliderX(th);
            float cth = (float)System.Math.Cos(th), sth = (float)System.Math.Sin(th);
            var pin = new Vector3(xc + ax * r * cth, ROD_Y, zc + r * sth);
            var ch = new Vector3(xc + ax * xs, ROD_Y, zc);
            _pinPos[k] = pin;
            _chPos[k] = ch;
            // type A turns clockwise seen from above (Unity +yaw), type B counter-clockwise; the pin lies on +axis at theta = 0.
            // Graybox meshes carry the pin toward the slider (yaw -ax theta); the full-tier mesh carries it on local +X, which
            // the extra 180 degrees turns toward -X for type A.
            float yaw = (float)(-ax * thDeg);
            if (_wheelPinAtPlusX && AbyssConst.BANK_TYPE_A[k]) yaw += 180f;
            _wheel[k].localRotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion look = Quaternion.LookRotation(ch - pin, Vector3.up);
            _rod[k].localPosition = _rodPivotAtPin ? pin : (pin + ch) * 0.5f;
            _rod[k].localRotation = look;
            _crosshead[k].localPosition = ch;
        }

        // ================================================================================================ activation (14.6)

        static float Smooth01(float x)
        {
            x = x < 0f ? 0f : (x > 1f ? 1f : x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Interior on/off and the three proxy-card fades for extended progress e. Allocation-free; applies changes only.</summary>
        void ApplyActivation(float e, bool force)
        {
            if (_graybox || _interiorGo == null) return;
            if (_editMode) e = EDIT_MODE_E;
            float lower = _interiorOn && _activationApplied ? INTERIOR_ON_E - ACTIVATION_HYSTERESIS : INTERIOR_ON_E;
            bool on = e >= lower && e < INTERIOR_OFF_E;
            if (force || !_activationApplied || on != _interiorOn)
            {
                _interiorOn = on;
                _activationApplied = true;
                if (_interiorGo.activeSelf != on) _interiorGo.SetActive(on);
                if (!on)
                    for (int k = 0; k < 4; k++) if (_bankBoltBatch[k] != null) _bankBoltBatch[k].Visible = false;
            }
            float portal = on ? 1f - Smooth01((e - PORTAL_CARD_FADE0) / (PORTAL_CARD_FADE1 - PORTAL_CARD_FADE0)) : 1f;
            float exitProxy = on ? 1f - Smooth01((e - EXIT_PROXY_FADE0) / (EXIT_PROXY_FADE1 - EXIT_PROXY_FADE0)) : 0f;
            float exitCard = on ? 0f : 1f;
            ApplyCardFade(_portalCard, portal, ref _portalFadeApplied, force);
            ApplyCardFade(_exitProxy, exitProxy, ref _exitProxyApplied, force);
            ApplyCardFade(_exitCard, exitCard, ref _exitCardApplied, force);
        }

        /// <summary>Dithered card dissolve (_AbFade near convention: 1 opaque, 0 gone); the renderer is disabled at 0.</summary>
        void ApplyCardFade(MeshRenderer r, float f, ref float applied, bool force)
        {
            if (r == null) return;
            if (!force && f == applied) return;
            applied = f;
            bool visible = f > 0f;
            if (r.enabled != visible) r.enabled = visible;
            if (visible && f < 1f)
            {
                _cardMpb.SetFloat(ShaderIds.AbFade, f);
                r.SetPropertyBlock(_cardMpb);
            }
            else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
        }

        void UpdateTiers(Vector3 cameraPos)
        {
            for (int k = 0; k < 4; k++)
            {
                DetailTier tier = _bankTier[k];
                if (tier == null) continue;
                tier.Update(cameraPos);
                InstancedBatch b = _bankBoltBatch[k];
                if (b == null) continue;
                float c = tier.Coverage;
                b.Visible = c > 0f;
                b.Fade = c < 1f ? c : 1f;
            }
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            if (_graybox)
            {
                ApplyPose(f.Time);
                return;
            }
            ApplyActivation(f.E, false);
            if (!_interiorOn) return;
            ApplyPose(f.Time);
            UpdateTiers(f.CameraPos);
        }

        public override void PrepareCapture(double sceneTime)
        {
            if (!_graybox) ApplyActivation(GameState.E, true);
            ApplyPose(sceneTime);
            if (!_graybox && _interiorOn) UpdateTiers(GameState.CameraPos);
        }

        // ================================================================================================ QA hooks

        /// <summary>
        /// KS4 subjects (14.10: every object the description names, one box each). Boxes are the parts the frame is meant to
        /// show: the catwalk segment 6-24 m ahead of the 14.10 eye, the two inner cylinders, the inner piston rods ("crosshead
        /// rods") between u 46 and 70 where they run between the inner cylinders, the left crank wheel (a turn-independent box:
        /// the O30 disc around its crank centre), the far outer banks as two subjects (the outer-right cylinder's far 16 m, the
        /// outer-left crank wheel), the far truss field and the exit. Wheels use the disc box, not the AABB of the rotated mesh.
        /// <para>
        /// KS4 pose (critique R1-20): the qa owner arbitrated the capture pose to u -2 (eye x -30, 2 m outside the portal),
        /// yaw 274, pitch +8 (KeyShots.KS4_X / KS4_YAW / KS4_PITCH), because at the 14.10 pose (u 28, yaw 270, pitch +4) the
        /// inner-right B cylinder "alongside" had its axis at least 51 degrees off the view axis against a half width of 46.9
        /// degrees at 16:9 and the left crank wheel was centred 8 m behind the eye. At the arbitrated pose the cylinder box
        /// centres at about (0.85, 0.22) at 16:9 and the wheel, larger than the frame, passes the lint's in-frame rule (in-frame
        /// centre about (0.22, 0.27), 97 % unoccluded in hall_r13): neither carries a tag any more.
        /// KNOWN SPEC CONFLICT (of the arbitrated pose): the outer-right bank (v 41-55) lies behind the inner-right cylinder
        /// (v 11-25, y -2.5..11.5, u 14-42) for every ray from an eye near the portal: B's top silhouette stands 26-27 degrees
        /// above the eye in that direction, higher than any part of bank 2 including its pipes up to the ceiling; hall_r13 lint:
        /// 0 % unoccluded. A pose that frames B "alongside" cannot also show the far outer-right bank, so it keeps its box and
        /// carries <see cref="SpecConflictTag"/>.
        /// </para>
        /// </summary>
        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            if (keyShotTag != "KS4_hall_pistons") return;
            float y = AbyssConst.CATWALK_Y, zc = AbyssConst.W[8].z;
            subjects.Add(new KeyShotSubject("catwalk rails and grating",
                new Bounds(new Vector3(X(43f), y + AbyssConst.RAIL_H * 0.5f, zc), new Vector3(18f, AbyssConst.RAIL_H + 0.05f, AbyssConst.CATWALK_W))));
            subjects.Add(new KeyShotSubject("inner-right cylinder (B)", MeshBounds(_cylinders[1]), 0.1f));
            subjects.Add(new KeyShotSubject("inner-left cylinder (A)", MeshBounds(_cylinders[0])));
            MeshRenderer r0 = _pistonRodR[0] != null ? _pistonRodR[0] : (_crosshead[0] != null ? _crosshead[0].GetComponent<MeshRenderer>() : null);
            MeshRenderer r1 = _pistonRodR[1] != null ? _pistonRodR[1] : (_crosshead[1] != null ? _crosshead[1].GetComponent<MeshRenderer>() : null);
            Bounds rods = ClipX(MeshBounds(r0), X(70f), X(46f));
            rods.Encapsulate(ClipX(MeshBounds(r1), X(70f), X(46f)));
            subjects.Add(new KeyShotSubject("crosshead rods", rods));
            subjects.Add(new KeyShotSubject("left crank wheel", WheelDisc(0)));
            if (_bankBuilt[2] && _cylinders[2] != null)
                subjects.Add(new KeyShotSubject("outer-right bank" + SpecConflictTag, ClipX(MeshBounds(_cylinders[2]), X(110f), X(KS4_OUTER_RIGHT_U))));
            if (_bankBuilt[3] && _wheelR[3] != null)
                subjects.Add(new KeyShotSubject("outer-left bank", ClipX(WheelDisc(3), X(110f), X(KS4_OUTER_LEFT_U))));
            if (_trusses != null)
                subjects.Add(new KeyShotSubject("trusses", ClipX(MeshBounds(_trusses), X(110f), X(KS4_TRUSS_U))));
            subjects.Add(new KeyShotSubject("exit opening", new Bounds(new Vector3(AbyssConst.HALL_X1, OPENING_Y0 + AbyssConst.HALL_OPENING_H * 0.5f, zc),
                new Vector3(HULL_WALL_T, AbyssConst.HALL_OPENING_H, AbyssConst.HALL_OPENING_W))));
        }

        /// <summary>World box of crank wheel k that does not depend on its turn: the O30 disc around the crank centre, mesh height.</summary>
        Bounds WheelDisc(int k)
        {
            MeshRenderer r = _wheelR[k];
            if (r == null) return new Bounds(Vector3.zero, Vector3.zero);
            Bounds b = MeshBounds(r);
            Vector3 c = r.transform.position;
            return new Bounds(new Vector3(c.x, b.center.y, c.z), new Vector3(AbyssConst.WHEEL_D, b.size.y, AbyssConst.WHEEL_D));
        }

        /// <summary>The part of a box between two x planes (empty box at the nearer plane when they do not overlap).</summary>
        static Bounds ClipX(Bounds b, float xMin, float xMax)
        {
            Vector3 mn = b.min, mx = b.max;
            mn.x = Mathf.Clamp(mn.x, xMin, xMax);
            mx.x = Mathf.Clamp(mx.x, xMin, xMax);
            var r = new Bounds();
            r.SetMinMax(mn, mx);
            return r;
        }

        /// <summary>World bounds of a renderer's mesh under its transform (valid for inactive objects too).</summary>
        static Bounds MeshBounds(MeshRenderer r)
        {
            if (r == null) return new Bounds(Vector3.zero, Vector3.zero);
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(r.transform.position, Vector3.zero);
            return InstancedBatch.TransformBounds(r.transform.localToWorldMatrix, mf.sharedMesh.bounds);
        }

        public override void ReportScale(ScaleReport report)
        {
            if (_graybox) ReportGrayboxScale(report);
            else ReportFullScale(report);

            // stroke and rod length measured on the moving parts over one revolution (1 degree steps), then restored
            if (_crosshead[0] != null)
            {
                float xc = X(CrankU(0));
                float dMin = float.MaxValue, dMax = float.MinValue, rodErr = 0f;
                for (int i = 0; i < 360; i++)
                {
                    PoseBank(0, i * System.Math.PI / 180.0, i);
                    float d = Mathf.Abs(_crosshead[0].localPosition.x - xc);
                    dMin = Mathf.Min(dMin, d); dMax = Mathf.Max(dMax, d);
                    float xs = (float)SliderX(i * System.Math.PI / 180.0);
                    Vector3 start = _rodPivotAtPin ? _rod[0].localPosition
                        : _rod[0].localPosition - _rod[0].localRotation * new Vector3(0f, 0f, AbyssConst.CRANK_L * 0.5f);
                    rodErr = Mathf.Max(rodErr, Mathf.Abs((start - _crosshead[0].localPosition).magnitude - AbyssConst.CRANK_L) + Mathf.Abs(d - xs)
                                               + (start - _pinPos[0]).magnitude);
                }
                ApplyPose(SceneClock.Time);
                report.Add("piston.stroke", dMax - dMin);
                report.Add("crank.slider.min", dMin);
                report.Add("crank.slider.max", dMax);
                report.Add("crank.rod.error", rodErr);
                // crank r and rod l from the measured dead centres (x = l + r at theta 0, l - r at 180 degrees)
                report.Add("crank.radius", (dMax - dMin) * 0.5f);
                report.Add("crank.rod.length", (dMax + dMin) * 0.5f);
            }

            // piston rod O1.6, 27 m from the crosshead front to the piston (full tier: its own mesh; graybox: crosshead mesh)
            MeshRenderer pr = _pistonRodR[0] != null ? _pistonRodR[0] : (_crosshead[0] != null ? _crosshead[0].GetComponent<MeshRenderer>() : null);
            if (MeasurePistonRod(pr, out float rodD, out float rodL))
            {
                report.Add("piston.rod.diameter", rodD);
                report.Add("piston.rod.length", rodL);
            }

            // 14.12 crank u (A 20 / B 90), bank v (-18, 18, 48, -48) and phases (0, 45, 90, 135) on the posed wheels
            ApplyPose(AbyssConst.PORTAL_T + 7.3);
            if (_wheel[0] != null)
            {
                float th0 = MeasuredThetaDeg(0);
                for (int k = 0; k < 4; k++)
                {
                    if (!_bankBuilt[k] || _wheel[k] == null) continue;
                    Vector3 c = _wheel[k].position;
                    report.Add("crank.u." + k, AbyssConst.HALL_X0 - c.x);
                    report.Add("bank.v." + k, c.z - AbyssConst.W[8].z);
                    float ph = MeasuredThetaDeg(k) - th0;
                    if (ph < 0f) ph += 360f;
                    report.Add("crank.phase." + k, ph);
                }
            }
            ApplyPose(SceneClock.Time);

            // clock: cycle and dead-centre spacing measured on the kinematics (0.01 s steps over 240 s)
            MeasureClock(out double cycle, out double dcMin, out double dcMax, out int dcCount);
            report.Add("crank.cycle", cycle);
            report.Add("crank.omega", 2.0 * System.Math.PI / cycle);
            report.Add("crank.deadcentre.interval.min", dcMin);
            report.Add("crank.deadcentre.interval.max", dcMax);
            report.Add("crank.deadcentre.per.cycle", dcCount);
            MeasureStrokeTime(out double stMin, out double stMax);
            report.Add("crank.stroke.time.min", stMin);
            report.Add("crank.stroke.time.max", stMax);

            if (!_graybox)
            {
                var parts = new List<HallPart>(32);
                report.Add("hall.parts.overlaps", CountOverlapsOverRevolution(parts, 72));
                ApplyPose(SceneClock.Time);
            }
        }

        void ReportGrayboxScale(ScaleReport report)
        {
            if (_roof != null)
            {
                Bounds b = MeshBounds(_roof);
                report.Add("hall.length", b.size.x);
                report.Add("hall.width", b.size.z);
                report.Add("hall.x0", b.max.x);
                report.Add("hall.x1", b.min.x);
                report.Add("hall.z0", b.min.z);
                report.Add("hall.z1", b.max.z);
                report.Add("hall.roof.y", b.max.y);
                if (_hullBase != null) report.Add("hall.hull.height", b.max.y - MeshBounds(_hullBase).min.y);
            }
            if (_hullBase != null)
            {
                Bounds b = MeshBounds(_hullBase);
                report.Add("hall.floor.y", b.max.y);
                report.Add("hall.hull.bottom", b.min.y);
                if (MeasureChamfer(_hullBase, b, out float run, out float rise))
                {
                    report.Add("hall.chamfer.x", run);
                    report.Add("hall.chamfer.y", rise);
                }
            }
            if (_catwalk != null)
            {
                Bounds b = MeshBounds(_catwalk);
                report.Add("hall.catwalk.width", b.size.z);
                report.Add("hall.catwalk.y", b.max.y);
            }
            // rails, posts, kick plates and supports are one mesh; its top is the top rail
            if (_grayCatwalkSteel != null) report.Add("hall.rail.height", MeshBounds(_grayCatwalkSteel).max.y - AbyssConst.CATWALK_Y);
            if (_openLeft != null && _openRight != null && _openBelow != null && _openAbove != null)
            {
                report.Add("hall.opening.width", _openRight.bounds.min.z - _openLeft.bounds.max.z);
                report.Add("hall.opening.height", _openAbove.bounds.min.y - _openBelow.bounds.max.y);
            }
            if (_cylinders[0] != null)
            {
                Bounds b = _cylinders[0].GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("piston.bore", b.size.y);
                report.Add("piston.cylinder.length", b.size.x);
            }
            if (_wheelR[0] != null) report.Add("wheel.diameter", _wheelR[0].GetComponent<MeshFilter>().sharedMesh.bounds.size.z);
        }

        void ReportFullScale(ScaleReport report)
        {
            if (_hullShell != null)
            {
                Bounds b = MeshBounds(_hullShell);   // world: chunk-local vertices + HULL_ORIGIN
                report.Add("hall.length", b.size.x);
                report.Add("hall.width", b.size.z);
                report.Add("hall.hull.height", b.size.y);
                report.Add("hall.x0", b.max.x);
                report.Add("hall.x1", b.min.x);
                report.Add("hall.z0", b.min.z);
                report.Add("hall.z1", b.max.z);
                report.Add("hall.roof.y", b.max.y);
                report.Add("hall.hull.bottom", b.min.y);
                if (MeasureChamfer(_hullShell, b, out float run, out float rise))
                {
                    report.Add("hall.chamfer.x", run);
                    report.Add("hall.chamfer.y", rise);
                }
            }
            if (_hallFloor != null) report.Add("hall.floor.y", MeshBounds(_hallFloor).max.y);
            if (_catwalk != null)
            {
                Bounds b = MeshBounds(_catwalk);
                report.Add("hall.catwalk.width", b.size.z);
                report.Add("hall.catwalk.length", b.size.x);
                report.Add("hall.catwalk.y", b.max.y);
            }
            report.Add("hall.opening.width", _portalReveal.size.z);
            report.Add("hall.opening.height", _portalReveal.size.y);
            report.Add("hall.exit.width", _exitReveal.size.z);
            report.Add("hall.exit.height", _exitReveal.size.y);
            report.Add("piston.bore", _measuredBore);
            report.Add("piston.cylinder.length", _measuredCylLength);
            if (_wheelR[0] != null)
            {
                Bounds w = _wheelR[0].GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("wheel.diameter", w.size.z);
            }
            if (_catwalkRails != null)
            {
                Bounds b = _catwalkRails.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("hall.rail.height", b.max.y - AbyssConst.CATWALK_Y);
            }
            if (_catwalkPosts != null)
            {
                Bounds b = _catwalkPosts.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("hall.support.top", b.max.y);
                report.Add("hall.support.bottom", b.min.y);
            }
            report.Add("hall.trusses", _trussCount);
            report.Add("hall.hull.lamps.lit", _hullLampsLit);
            report.Add("hall.hull.lamps.dark", _hullLampsDark);
            report.Add("hall.hull.lamps.lit.bridgefaces", _hullLampsLitBridge);
            report.Add("hall.hull.lamps.dark.bridgefaces", _hullLampsDarkBridge);
            report.Add("hall.hull.girders", GIRDER_X.Length);
            report.Add("hall.hull.hangers", GIRDER_X.Length);
            report.Add("hall.spot.count", _spotCount);
            report.Add("hall.hazard.count", _hazardCount);
            report.Add("cylinder.flange.bolts", _flangeBolts);
        }

        /// <summary>
        /// Crank cycle and dead-centre spacing measured on the kinematics (QA, allocates a little). Cycle: the period T in
        /// 60..180 s (0.01 s steps, then refined) that best returns all four sliders to their start positions. Dead centres:
        /// extrema of any slider (sign changes of its speed, refined by a parabola through three samples) over 240 s.
        /// </summary>
        public static void MeasureClock(out double cycle, out double dcMin, out double dcMax, out int dcPerCycle)
        {
            const double dt = 0.01, t0 = 1000.0;
            // cycle
            double best = double.MaxValue;
            cycle = 0.0;
            for (int i = 6000; i <= 18000; i++)
            {
                double T = i * dt, err = 0.0;
                for (int k = 0; k < 4; k++)
                    for (int j = 0; j < 4; j++)
                    {
                        double ts = t0 + j * 7.3;
                        err += System.Math.Abs(SlideDistance(k, ts + T) - SlideDistance(k, ts));
                    }
                if (err < best) { best = err; cycle = T; }
            }
            // dead centres
            var events = new List<double>(64);
            for (int k = 0; k < 4; k++) DeadCentres(k, t0, dt, 24000, events);
            events.Sort();
            dcMin = double.MaxValue; dcMax = 0.0; dcPerCycle = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] - t0 < AbyssConst.CRANK_PERIOD) dcPerCycle++;
                if (i == 0) continue;
                double gap = events[i] - events[i - 1];
                if (gap < dcMin) dcMin = gap;
                if (gap > dcMax) dcMax = gap;
            }
            if (dcMin == double.MaxValue) dcMin = 0.0;
        }

        /// <summary>
        /// Appends the dead-centre times of bank k over [t0, t0 + steps dt]: sign changes of the slider speed, refined by a
        /// parabola through three samples.
        /// </summary>
        static void DeadCentres(int k, double t0, double dt, int steps, List<double> events)
        {
            double xa = SlideDistance(k, t0), xb = SlideDistance(k, t0 + dt);
            for (int i = 2; i <= steps; i++)
            {
                double t = t0 + i * dt, xc = SlideDistance(k, t);
                if ((xb - xa) * (xc - xb) < 0.0)
                {
                    double den = xa - 2.0 * xb + xc;
                    double off = System.Math.Abs(den) > 1e-15 ? 0.5 * (xa - xc) / den : 0.0;
                    events.Add(t - dt + off * dt);
                }
                xa = xb; xb = xc;
            }
        }

        /// <summary>
        /// STROKE_TIME (14.12, 60 s) measured on the kinematics: shortest and longest time between consecutive dead centres of
        /// the same piston (inner to outer and back), every bank, 0.01 s steps over 240 s (QA, allocates a little).
        /// </summary>
        public static void MeasureStrokeTime(out double min, out double max)
        {
            const double dt = 0.01, t0 = 1000.0;
            var events = new List<double>(16);
            min = double.MaxValue; max = 0.0;
            for (int k = 0; k < 4; k++)
            {
                events.Clear();
                DeadCentres(k, t0, dt, 24000, events);
                for (int i = 1; i < events.Count; i++)
                {
                    double gap = events[i] - events[i - 1];
                    if (gap < min) min = gap;
                    if (gap > max) max = gap;
                }
            }
            if (min == double.MaxValue) min = 0.0;
        }

        /// <summary>
        /// Crank angle of bank k read back from its posed wheel (degrees, [0, 360)): the direction of the crank pin (graybox
        /// meshes carry it on the slider side of local X, the full-tier mesh on local +X) in the bank frame, 0 on the slider
        /// axis toward the cylinder, increasing with the scene clock for both turning senses.
        /// </summary>
        float MeasuredThetaDeg(int k)
        {
            Transform w = _wheel[k];
            float ax = AxisX(k), side = _wheelPinAtPlusX ? 1f : ax;
            Vector3 c = w.position, pin = w.TransformPoint(new Vector3(side * AbyssConst.CRANK_R, 0f, 0f));
            float th = Mathf.Atan2(pin.z - c.z, ax * (pin.x - c.x)) * Mathf.Rad2Deg;
            return th < 0f ? th + 360f : th;
        }

        /// <summary>
        /// The 12 x 12 chamfer of the lower -X hull edge read from the hull vertices (world): its run along x at the hull bottom
        /// (smallest x of the vertices at y = bottom, minus the -X face) and its rise up the -X face (lowest y of the vertices
        /// at x = -X face, minus the bottom). QA only (copies the vertex array).
        /// </summary>
        static bool MeasureChamfer(MeshRenderer r, Bounds world, out float run, out float rise)
        {
            run = rise = -1f;
            MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return false;
            Vector3[] v = mf.sharedMesh.vertices;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            float xFace = world.min.x, yBottom = world.min.y;
            float xMin = float.MaxValue, yMin = float.MaxValue;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(v[i]);
                if (Mathf.Abs(p.y - yBottom) < 1e-3f && p.x < xMin) xMin = p.x;
                if (Mathf.Abs(p.x - xFace) < 1e-3f && p.y < yMin) yMin = p.y;
            }
            if (xMin == float.MaxValue || yMin == float.MaxValue) return false;
            run = xMin - xFace;
            rise = yMin - yBottom;
            return true;
        }

        /// <summary>
        /// Piston rod diameter and length read from the mesh that carries it (local X = rod axis through the crosshead pin):
        /// vertices beyond the crosshead and its collar (|x| > 2.5) belong to the rod; length = rod end minus the crosshead
        /// front (half the crosshead length). QA only (copies the vertex array).
        /// </summary>
        static bool MeasurePistonRod(MeshRenderer r, out float diameter, out float length)
        {
            diameter = length = -1f;
            MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return false;
            Vector3[] v = mf.sharedMesh.vertices;
            float hl = CROSSHEAD_L * 0.5f, rMax = 0f, xMax = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                float ax = Mathf.Abs(v[i].x);
                if (ax < hl + 0.5f) continue;
                rMax = Mathf.Max(rMax, Mathf.Sqrt(v[i].y * v[i].y + v[i].z * v[i].z));
                xMax = Mathf.Max(xMax, ax);
            }
            if (xMax <= 0f) return false;
            diameter = 2f * rMax;
            length = xMax - hl;
            return true;
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);

        // ------------------------------------------------------------------------------------------ bounds test support

        /// <summary>
        /// World AABBs (exact, from mesh vertices) of every listed part at the current pose: per built bank the wheel disc,
        /// crankshaft with pin ("shaft"), connecting rod, crosshead, piston rod and cylinder; plus the catwalk posts and rails.
        /// QA / test use only (allocates a vertex cache on the first call).
        /// </summary>
        public void CollectParts(List<HallPart> parts)
        {
            parts.Clear();
            if (_qaVerts == null) _qaVerts = new Dictionary<Mesh, Vector3[]>();
            for (int k = 0; k < 4; k++)
            {
                if (!_bankBuilt[k]) continue;
                AddPart(parts, "wheel", k, _wheelR[k]);
                AddPart(parts, "shaft", k, _shaftR[k]);
                AddPart(parts, "rod", k, _rodR[k]);
                MeshRenderer ch = _crossheadR[k];
                if (ch == null && _crosshead[k] != null) ch = _crosshead[k].GetComponent<MeshRenderer>();
                AddPart(parts, "crosshead", k, ch);
                AddPart(parts, "pistonrod", k, _pistonRodR[k]);
                AddPart(parts, "cylinder", k, _cylinders[k]);
            }
            AddPart(parts, "post", -1, _catwalkPosts);
            AddPart(parts, "rail", -1, _catwalkRails);
        }

        void AddPart(List<HallPart> parts, string kind, int bank, MeshRenderer r)
        {
            if (r == null) return;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Mesh m = mf.sharedMesh;
            if (!_qaVerts.TryGetValue(m, out Vector3[] v))
            {
                v = m.vertices;
                _qaVerts[m] = v;
            }
            if (v.Length == 0) return;
            Matrix4x4 l2w = r.transform.localToWorldMatrix;
            var b = new Bounds(l2w.MultiplyPoint3x4(v[0]), Vector3.zero);
            for (int i = 1; i < v.Length; i++) b.Encapsulate(l2w.MultiplyPoint3x4(v[i]));
            parts.Add(new HallPart(kind, bank, b));
        }

        /// <summary>
        /// Mechanically joined pairs of one bank that touch by design and are excluded from the bounds test: the rod's big end
        /// on the crank pin ("rod"/"shaft"), its small end in the crosshead fork around the crosshead pin ("rod"/"crosshead",
        /// "rod"/"pistonrod": the pin belongs to the piston-rod mesh), the piston rod in the crosshead ("pistonrod"/"crosshead")
        /// and through the rear head into the cylinder ("pistonrod"/"cylinder"), and the crankshaft through the wheel hub
        /// ("shaft"/"wheel"). Everything else, across all banks and against the catwalk posts and rails, must stay apart.
        /// </summary>
        public static bool IsJoinedPair(HallPart a, HallPart b)
        {
            if (a.Bank != b.Bank || a.Bank < 0) return false;
            return Pair(a, b, "rod", "shaft") || Pair(a, b, "rod", "crosshead") || Pair(a, b, "rod", "pistonrod")
                || Pair(a, b, "pistonrod", "crosshead") || Pair(a, b, "pistonrod", "cylinder") || Pair(a, b, "shaft", "wheel");
        }

        /// <summary>Guide-frame stations along the slider axis (m from the crank centre) and the lateral leg offset (QA / tests).</summary>
        public static int GuideFrameCount => FRAME_S.Length;
        public static float GuideFrameStation(int i) => FRAME_S[i];
        public const float GuideFrameLegOffset = FRAME_LEG_L;
        /// <summary>Bank-frame lateral side (+1 / -1) of the braced leg plane of bank k: the one facing away from the catwalk.</summary>
        public static int GuideFrameOutboardSide(int k) => OutboardSide(k);
        /// <summary>Hull lamp rows (y) and the girder centre lines (x) of the exterior (QA / tests).</summary>
        public static float HullLampRowY(int i) => HULL_LAMP_ROWS[i];
        public static int HullLampRowCount => HULL_LAMP_ROWS.Length;
        public static float HullGirderX(int i) => GIRDER_X[i];
        public static int HullGirderCount => GIRDER_X.Length;
        /// <summary>Half the leg section (0.8 m square) and the largest half width of the connecting rod (shank 1.1 m at the big end).</summary>
        public const float GuideFrameLegHalf = 0.4f, RodHalfWidthMax = 0.55f;
        /// <summary>Crank centre of bank k (y = 0) and the world x direction of its slider axis.</summary>
        public static Vector3 CrankCentre(int k) => BankOrigin(k);
        public static float SliderAxisX(int k) => AxisX(k);

        static bool Pair(HallPart a, HallPart b, string x, string y) => (a.Kind == x && b.Kind == y) || (a.Kind == y && b.Kind == x);

        /// <summary>Number of intersecting (non-joined) part AABB pairs summed over 'steps' poses of one full revolution (120 s).</summary>
        int CountOverlapsOverRevolution(List<HallPart> parts, int steps)
        {
            int overlaps = 0;
            for (int s = 0; s < steps; s++)
            {
                ApplyPose(AbyssConst.PORTAL_T + AbyssConst.CRANK_PERIOD * s / steps);
                CollectParts(parts);
                for (int i = 0; i < parts.Count; i++)
                    for (int j = i + 1; j < parts.Count; j++)
                    {
                        if (IsJoinedPair(parts[i], parts[j])) continue;
                        if (parts[i].World.Intersects(parts[j].World)) overlaps++;
                    }
            }
            return overlaps;
        }
    }
}
