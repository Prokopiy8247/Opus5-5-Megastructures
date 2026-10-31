using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 7 - tunnel, balcony, honeycomb and goal door (14.5.7, d 668.5-802.5), the final money shot.
    ///
    /// Full tier (this file + .Structures / .Honeycomb):
    /// - Tunnel (fog domain Tunnel, never lit by the Key: "nokey" variants): 60 m board-marked lining (3.6 x 3.4, chamfered
    ///   inner corners) in five 12 m segments with recessed joints between them, floor slab with the worn path, six ceiling
    ///   fixtures fading toward the mouth, cable tray with cables on the left wall, drain cover plates on the right (those
    ///   props swap by camera distance through a DetailTier), steel threshold at the mouth.
    /// - The Face (Exterior): 473.4 m wide (x -236.7..+236.7), y -1087..+1200, 200 m chunks with chunk-local vertices, holes
    ///   for the fan bore, the tunnel mouth and the gallery; horizontal bands at the S0 ring levels (ribs every 60 m, belts
    ///   every 240 m, gapped around the bore); a bore collar (R 150 -> 153, 1 m proud, edges bevelled 0.05); the mouth portal
    ///   (jambs and lintel, edges bevelled 0.05, overlapping the lining by 5 cm against light leaks); rod anchor plates.
    /// - Balcony 20 x 14 x 2.5 (chamfered slab + 5 cm topping with the worn path), corbel underside, kerbs 0.15 with the pier
    ///   opening; pier 3 x 60 tapered corbel slab (4 m deep at the root, 0.6 m at the tip), kerbs 0.10, topping.
    /// - Door assembly at z = -74: frame 3.0 x 2.6 x 0.6 (leaf set 0.4 m back in the reveal), back panel, leaf 1.0 x 2.0
    ///   with handle, 3 hinges and kick plate, amber backing quad (AMBER x 3.0) 0.15 m behind the leaf, amber lamp above
    ///   (I 10, a downward spot), door spot (I 14, follows the opening), two 0.15 m tension rods with clevis ends and a
    ///   turnbuckle to anchors on the Face 14 m above the deck (x -45 +/- 1.9). Lamp, lens and backing stay dark until the
    ///   honeycomb ignition sweep has run (the door is the last lamp to light).
    /// - Honeycomb (Exterior): tier 0 real hex tubes (600 m deep, 10 rings at the S0 ring levels, landing, bridge deck at
    ///   y -1089) up to 1.6 km, tier 1 simplified instanced tubes to 6.6 km, tier 2 the Lattice shader on a camera-following
    ///   annulus (BackdropRig, radius 28 km, y -1087.6, 100 m grid, rotation locked); per-pixel dithered cross-fades by camera
    ///   distance over 1.44-1.76 km and 5.4-6.6 km (_DistFade); amber lamps as Glow sprites (>= 2.5 px) igniting from the
    ///   balcony front (z -11.5) nearest to farthest over 12 s.
    /// - The dark card (320 x 360 at (-45, -335, 120), facing -Z) while d &gt; 735.
    /// Simplified: honeycomb tiers 0 and 2 only (lattice from 1.6 km). Graybox: the Phase-3 geometry (hex-hole plane).
    ///
    /// Goal: within 1.2 m of the door (feet z &lt;= -72.8 on the lower level) sets GameState.GoalReached and GoalRealTime; the
    /// leaf then opens 12 degrees over 3 s and the door spot follows it. Colliders are final (unchanged from Phase 3).
    /// </summary>
    public sealed partial class Beat07_Reveal : BeatBase
    {
        public override int Index => 7;

        // ---------------------------------------------------------------- beat-specific constants
        const float CHUNK = 200f, WALL_CELL = 50f;
        const float LINING_SKIRT = 0.25f, LINING_OVERLAP = 0.05f, FLOOR_T = 0.3f;
        // the floor slab stops 1 cm short of Beat02's near flat (z = 60, facing +Z, solid under the mouth): its end face sits
        // behind the wall instead of on it (no co-planar faces across beats; tunnel.length 59.99 stays inside +/-0.1)
        const float SHAFT_RECESS = 0.01f;
        // KS7b foreground subject: the visible strip of the front kerb across the pier opening (14.5.7: "at 90 % of the frame
        // height", bottom 10 %); the 20 m kerb is far wider than the frame 2 m away and its near half lies below the frame
        const float KS7B_KERB_SPAN = 4f;
        const int STRIPS = 6;
        const float STRIP_Z0 = 55f, STRIP_SPACING = 10f, STRIP_EMISSION = 4f, STRIP_I = 7f, STRIP_RANGE = 10f;
        /// <summary>
        /// Level of strip i (z 55 at the gantry end to z 5 at the mouth), still fading toward the mouth (14.5.7). R1-23: the
        /// old 1.0 - 0.12 i put the brightest strip 3 m inside the entrance, where the tunnel already runs at EV +0.9 (14.6):
        /// B7_start (z 58) measured mean 0.331 with the lens clipped, twice B6_end 4 m earlier and three times KS7a, a
        /// brightness pulse inside the last compression. The first strip is now 1.7 EV dimmer and the fade is gentler, so the
        /// frame mean falls from the gantry (about 0.16) through the tunnel to about 0.1 at KS7a and the mouth stays brightest.
        /// </summary>
        static readonly float[] STRIP_LEVELS = { 0.30f, 0.29f, 0.28f, 0.27f, 0.26f, 0.25f };
        const float KERB_H = 0.15f, KERB_W = 0.2f;
        const float PIER_ROOT_DEPTH = 4f, PIER_TIP_DEPTH = 0.6f, PIER_OPENING = 2f;
        const float FRAME_W = 3f, FRAME_H = 2.6f, FRAME_DEPTH = 0.6f;
        const float LEAF_T = 0.05f, BACKING_GAP = 0.15f, BACK_PANEL_T = 0.3f;
        public const float LEAF_OPEN_DEG = 12f, LEAF_OPEN_TIME = 3f;
        const float BACKING_EMISSION = 3f;
        const float DOOR_LAMP_I = 10f, DOOR_LAMP_RANGE = 12f;
        /// <summary>
        /// R1-21 / R1-32: the door lamp is a spot just under the lens of the housing (a point light clipped the lintel and
        /// washed the frame and rods amber). R2-05: it no longer pools on the pier (the f03 pitch -60 / 100 degree cone lit the
        /// pier amber before the door opened, so the sliver had no cold pier to land on). It now looks down into the recess:
        /// axis 8 degrees back from the vertical (yaw 180), 24 degree cone, so it lights the sill and the lower leaf face and
        /// reaches at most 0.25 m out over the pier; the lintel face behind the lamp stays 21 degrees off the axis (5 degrees
        /// outside the cone). Same I 10, range 12 (14.5.7).
        /// </summary>
        public const float DOOR_LAMP_PITCH = -82f, DOOR_LAMP_ANGLE = 24f;
        const float DOOR_LAMP_DROP = 0.11f;
        const float DOOR_SPOT_I = 14f, DOOR_SPOT_RANGE = 14f, DOOR_SPOT_ANGLE = 60f, DOOR_SPOT_INNER = 12f;
        /// <summary>
        /// R2-05: where the door spot aims on the floor (offset from the frame front face, + toward the pier) when the leaf is
        /// shut and when it is fully open: the light slides out of the recess onto the pier as the leaf opens (14.11 "a sliver
        /// of amber light that slides onto the pier").
        /// </summary>
        public const float DOOR_SPOT_AIM_SHUT = -0.2f, DOOR_SPOT_AIM_OPEN = 3.2f;
        /// <summary>
        /// R2-05: the door spot sits low in the latch-side gap (DOOR_SPOT_HEIGHT above the deck, DOOR_SPOT_X from the door axis
        /// toward the latch) and grazes the floor (pitch about -16 shut to -6 open) with a narrow 12 degree core in the 60
        /// degree cone of the spec: its floor footprint is a streak running out of the gap toward the camera, not a round
        /// pool (player_r31s cue_ending: the 1.0 m high spot drew a second round pool on the pier, a lamp switching on rather
        /// than light through a crack). Additional lights cast no shadows (RenderSetup), so the low graze is what shapes it.
        /// </summary>
        public const float DOOR_SPOT_HEIGHT = 0.32f, DOOR_SPOT_X = 0.42f;
        /// <summary>
        /// R2-05: light from the amber backing spilling through the 12 degree latch-side gap: a small point light between the
        /// leaf and the backing at the latch side, following the opening (0 when shut). It lights the latch-side jamb reveal,
        /// which the camera on the walk axis sees as a 20 px strip (the backing itself hides behind that jamb from the axis).
        /// </summary>
        const float DOOR_SPILL_I = 0.6f, DOOR_SPILL_RANGE = 1.4f;
        /// <summary>
        /// Lead decision D2 (R2-05, 14.11 arbitration): the door trigger is 3.0 m from the door plane (z -74): feet z &lt;= -71.0
        /// (spec 1.2 m, z -72.8). The cinematic walker stops at z -70.8 facing the door so the opening, the backing, the door
        /// spot and the sliver on the pier are in frame; a walker that has finished its walk within WALKER_STOP_SLACK of the
        /// trigger also reaches the goal.
        /// </summary>
        public const float GOAL_TRIGGER_Z = -71.0f, WALKER_STOP_SLACK = 0.6f;
        const float ROD_R = 0.075f, ROD_ANCHOR_UP = 14f, ROD_ANCHOR_DX = 1.9f;
        const float ACTIVATION_HYSTERESIS = 20f, DARK_CARD_HYSTERESIS = 5f;
        const float DARK_CARD_W = 320f, DARK_CARD_H = 360f;
        static readonly Vector3 DARK_CARD_CENTRE = new Vector3(-45f, -335f, 120f);
        /// <summary>
        /// R1-24 (arbitration of 14.5.7 Motion, logged in DEVELOPMENT.md): the honeycomb lamps ignite when the walker reaches
        /// the balcony front (z -11.5, the start of hold H7, where the gaze tilts to -12 and the near shafts enter the frame),
        /// not at z = -2 (AbyssConst.LAMP_IGNITE_Z), where the gaze is level, the balcony floor hides every cell within about
        /// 4 km and the near lamps lit off-screen. R2-20: the sweep follows the distance-warped curve IgniteDelay: the lamps
        /// out to 12 km (everything readable) light over IGNITE_SECONDS, the visible 0.7-10 km band over 10.6 s of the 14 s hold.
        /// </summary>
        public const float HONEY_IGNITE_Z = -11.5f;
        public const float IGNITE_SECONDS = 12f;
        /// <summary>
        /// R1-22: the door lamp, the lamp lens and the amber backing stay dark until the ignition sweep has run (the door is the
        /// last lamp to light: the 12 km lamps finish their 1 s ramp at 13 s), then ease on over DOOR_LIGHT_RAMP seconds: about
        /// clock 61-63.5 of the cinematic, the end of H7 and the 62-68 "lock on the door" cue.
        /// </summary>
        public const float DOOR_LIGHT_DELAY = IGNITE_SECONDS + 1f, DOOR_LIGHT_RAMP = 2.5f;
        const float LOWER_LEVEL_Y = -400f;

        static readonly int KeyLightMaskId = Shader.PropertyToID("_KeyLightMask");
        static readonly int IgniteId = Shader.PropertyToID("_Ignite");
        static readonly int MacroAmpId = Shader.PropertyToID("_MacroAmp");

        readonly List<Mesh> _meshes = new List<Mesh>();
        Transform _geo, _leafPivot;
        GameObject _latticeGo, _darkCard;
        Material _latticeMat, _lensMat, _backingMat;
        Light _doorSpot, _doorLamp, _doorSpill;
        DetailTier _tunnelProps;
        bool _editMode, _latticeOn, _darkOn, _ignited;
        float _lastOpen = -1f, _doorLight = -1f, _lensBase;
        double _igniteStart = -1.0;
        BeatTier _tier;

        // measured from the generated vertices (ReportScale, CollectSubjects)
        Bounds _tunnelFloorB, _liningB, _balconyB, _pierB, _leafB, _frameB, _faceB;
        float _walkTopY, _latticeRadius, _frameBaseY;
        bool _faceBSet;

        static float X => AbyssConst.W[15].x;
        static float Yf => AbyssConst.W[15].y;

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat07");
            _editMode = ctx.EditMode;
            _tier = ctx.Tier;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            BuildColliders(ctx);
            var mb = new MeshBuilder();
            if (ctx.Tier == BeatTier.Graybox)
            {
                BuildGraybox(ctx, mb);
            }
            else
            {
                BuildTunnelFull(ctx, mb);
                BuildFaceFull(ctx, mb);
                BuildBalconyAndPierFull(ctx, mb);
                BuildDoorFull(ctx, mb);
                BuildHoneycomb(ctx, mb, ctx.Tier == BeatTier.Simplified);
                BuildDarkCard(ctx, mb);
            }
            BuildLights(ctx);
            if (!ctx.EditMode) Shader.SetGlobalVector(ShaderIds.AbLampIgnite, new Vector4(-1f, 0f, 0f, 0f));
            _ignited = false;
            _igniteStart = -1.0;
            ApplyActivation(GameState.D, true);
            ApplyLeaf(0f);
            _doorLight = -1f;
            ApplyDoorLight(0f);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            float x = X, y = Yf, tw = AbyssConst.TUNNEL_W * 0.5f;
            float zM = AbyssConst.W[15].z, zF = AbyssConst.W[16].z, zB = AbyssConst.W[17].z, zD = AbyssConst.W[18].z;
            float wb = y - 1f, wh = 4f;

            // tunnel
            CorridorKit.Floor(c, "TunnelFloor", new Vector3(x, y, (zM + zF) * 0.5f), new Vector2(AbyssConst.TUNNEL_W + 1f, zM - zF));
            BeatKit.Wall(c, "TunnelWallW", new Vector2(x - tw, zM), new Vector2(x - tw, zF), wb, wh, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "TunnelWallE", new Vector2(x + tw, zM), new Vector2(x + tw, zF), wb, wh, new Vector2(1f, 0f));
            BeatKit.CeilingClamp(c, "TunnelCeiling", new Vector3(x, y + AbyssConst.TUNNEL_H, zM), new Vector3(x, y + AbyssConst.TUNNEL_H, zF), AbyssConst.TUNNEL_W + 1f);

            // balcony 20 x 14: edge walls 0.6 m inside the visible edges, the Face behind, the 2.0 m pier opening in front
            float bh = AbyssConst.BALCONY_W * 0.5f - PathGuide.BALCONY_EDGE_INSET;   // 9.4
            float bf = zB + PathGuide.BALCONY_EDGE_INSET;                              // -13.4
            float po = PIER_OPENING * 0.5f;
            CorridorKit.Floor(c, "BalconyFloor", new Vector3(x, y, (zF + zB) * 0.5f), new Vector2(AbyssConst.BALCONY_W, zF - zB));
            BeatKit.Wall(c, "BalconyEdgeW", new Vector2(x - bh, zF), new Vector2(x - bh, bf), wb, wh, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "BalconyEdgeE", new Vector2(x + bh, zF), new Vector2(x + bh, bf), wb, wh, new Vector2(1f, 0f));
            BeatKit.Wall(c, "BalconyFrontW", new Vector2(x - bh, bf), new Vector2(x - po, bf), wb, wh, new Vector2(0f, -1f));
            BeatKit.Wall(c, "BalconyFrontE", new Vector2(x + po, bf), new Vector2(x + bh, bf), wb, wh, new Vector2(0f, -1f));
            BeatKit.Wall(c, "FaceW", new Vector2(x - bh, zF), new Vector2(x - tw, zF), wb, wh, new Vector2(0f, 1f));
            BeatKit.Wall(c, "FaceE", new Vector2(x + tw, zF), new Vector2(x + bh, zF), wb, wh, new Vector2(0f, 1f));

            // pier 3 x 60: edge walls 0.5 m inside (corridor 2.0) from the balcony front wall to the door
            float ph = AbyssConst.PIER_W * 0.5f - PathGuide.PIER_EDGE_INSET;   // 1.0
            float zFrame = zD + FRAME_DEPTH;                                    // the frame's front face (-73.4)
            CorridorKit.Floor(c, "PierFloor", new Vector3(x, y, (zB + zD) * 0.5f), new Vector2(AbyssConst.PIER_W, zB - zD));
            BeatKit.Wall(c, "PierEdgeW", new Vector2(x - ph, bf), new Vector2(x - ph, zD), wb, wh, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "PierEdgeE", new Vector2(x + ph, bf), new Vector2(x + ph, zD), wb, wh, new Vector2(1f, 0f));
            BeatKit.Wall(c, "DoorEndWall", new Vector2(x - ph - 0.5f, zFrame), new Vector2(x + ph + 0.5f, zFrame), wb, wh, new Vector2(0f, -1f));

            CorridorKit.SafetyBelow(c, "SafetyTunnel", new Vector3(x, y, (zM + zF) * 0.5f), new Vector2(AbyssConst.TUNNEL_W, zM - zF));
            CorridorKit.SafetyBelow(c, "SafetyBalcony", new Vector3(x, y, (zF + zB) * 0.5f), new Vector2(AbyssConst.BALCONY_W, zF - zB));
            CorridorKit.SafetyBelow(c, "SafetyPier", new Vector3(x, y, (zB + zD) * 0.5f), new Vector2(AbyssConst.PIER_W, zB - zD));
        }

        static float StripLevel(int i) => STRIP_LEVELS[Mathf.Clamp(i, 0, STRIP_LEVELS.Length - 1)];

        void BuildDarkCard(BeatContext ctx, MeshBuilder mb)
        {
            Vector3 dc = DARK_CARD_CENTRE;
            BeatKit.ZCard(mb, dc.x - DARK_CARD_W * 0.5f, dc.x + DARK_CARD_W * 0.5f, dc.y - DARK_CARD_H * 0.5f, dc.y + DARK_CARD_H * 0.5f, dc.z, false);
            MeshRenderer r = BeatKit.Emit(mb, _geo, "DarkCard", ctx.Mats[MatId.GradientCard], _meshes, UnityEngine.Rendering.ShadowCastingMode.Off, Vector3.zero, true);
            if (r != null) _darkCard = r.gameObject;
        }

        // ---------------------------------------------------------------- lights

        void BuildLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            float x = X, y = Yf, h = AbyssConst.TUNNEL_H, zD = AbyssConst.W[18].z;
            for (int i = 0; i < STRIPS; i++)
                BeatKit.Point(lights, "TunnelStrip" + i, new Vector3(x, y + h - 0.35f, STRIP_Z0 - STRIP_SPACING * i), AbyssPalette.COLD_LAMP,
                    STRIP_I * StripLevel(i), STRIP_RANGE);
            // under the lens of the lamp housing on the lintel face (DOOR_LAMP_Y, 0.1 m in front of the frame), aimed down into
            // the recess (R2-05: the pier stays cold until the leaf opens)
            _doorLamp = BeatKit.Spot(lights, "DoorLamp", new Vector3(x, y + DOOR_LAMP_Y - DOOR_LAMP_DROP, zD + FRAME_DEPTH + 0.1f),
                AbyssConst.Dir(180f, DOOR_LAMP_PITCH), AbyssPalette.AMBER_LIGHT, DOOR_LAMP_I, DOOR_LAMP_RANGE, DOOR_LAMP_ANGLE);
            // behind the leaf at the latch side, aimed through the opening down onto the floor: as the leaf opens the aim slides
            // from the sill out onto the pier (ApplyLeaf), the sliver of amber light of 14.11; a narrow bright core in the 60
            // degree cone of the spec
            float zLeaf = LeafZ(_tier);
            _doorSpot = BeatKit.Spot(lights, "DoorSpot", DoorSpotPos(_tier), DoorSpotAim(_tier, 0f) - DoorSpotPos(_tier),
                AbyssPalette.AMBER_LIGHT, 0f, DOOR_SPOT_RANGE, DOOR_SPOT_ANGLE);
            _doorSpot.innerSpotAngle = DOOR_SPOT_INNER;
            // light from the backing spilling through the latch-side gap onto the jamb reveal (R2-05)
            _doorSpill = BeatKit.Point(lights, "DoorSpill", new Vector3(x + 0.38f, y + 1.0f, zLeaf - LEAF_T * 0.5f - 0.065f),
                AbyssPalette.AMBER_LIGHT, 0f, DOOR_SPILL_RANGE);
        }

        /// <summary>World position of the door spot: between the leaf and the amber backing, at the latch side.</summary>
        static Vector3 DoorSpotPos(BeatTier tier) => new Vector3(X + DOOR_SPOT_X, Yf + DOOR_SPOT_HEIGHT, LeafZ(tier) - LEAF_T * 0.5f - 0.065f);

        /// <summary>
        /// Floor point the door spot aims at for the leaf opening open (0..1): from the sill out onto the pier, turning a little
        /// toward the latch side as the gap widens (the leaf swings on its -X hinge).
        /// </summary>
        static Vector3 DoorSpotAim(BeatTier tier, float open)
        {
            float zFront = AbyssConst.W[18].z + FRAME_DEPTH;
            return new Vector3(X + DOOR_SPOT_X + Mathf.Lerp(0f, 0.25f, open), Yf, zFront + Mathf.Lerp(DOOR_SPOT_AIM_SHUT, DOOR_SPOT_AIM_OPEN, open));
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            ApplyActivation(f.D, false);
            ApplyHoneycomb(false);
            if (_tunnelProps != null) _tunnelProps.Update(f.CameraPos);

            Vector3 p = GameState.PlayerPos;
            bool lower = p.y < LOWER_LEVEL_Y;
            if (!GameState.GoalReached && lower && AtDoor(p))
            {
                GameState.GoalReached = true;
                GameState.GoalRealTime = SceneClock.RealTime;
                Debug.Log("[ABYSS] goal reached at t = " + SceneClock.Time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s");
            }
            float open = GameState.GoalReached && GameState.GoalRealTime >= 0.0
                ? BeatKit.Smootherstep((float)((SceneClock.RealTime - GameState.GoalRealTime) / LEAF_OPEN_TIME)) : 0f;
            ApplyLeaf(open);

            if (!_ignited && lower && p.z <= HONEY_IGNITE_Z) Ignite(f.Time, p);
            UpdateGlowFades(f.CameraPos, f.Time);
            UpdateDoorLight(f.Time);
        }

        /// <summary>
        /// D2: on the pier within 3.0 m of the door plane (feet z &lt;= GOAL_TRIGGER_Z), or the AutoWalker has finished its walk
        /// (it stops at about z -70.8 facing the door) within WALKER_STOP_SLACK of that line.
        /// </summary>
        static bool AtDoor(Vector3 feet)
        {
            if (Mathf.Abs(feet.x - X) >= AbyssConst.PIER_W) return false;
            AutoWalker w = AutoWalker.Instance;
            // QA captures teleport the player to still poses (B7_end stands inside the 3 m trigger): in capture mode only a
            // walked approach (the cue_ending sequence) reaches the goal, so no still starts the ending fade for the shots after it
            if (GameState.CaptureMode && (w == null || !w.IsRunning)) return false;
            if (feet.z <= GOAL_TRIGGER_Z) return true;
            return w != null && w.Finished && feet.z <= GOAL_TRIGGER_Z + WALKER_STOP_SLACK;
        }

        /// <summary>Door lamp, lens and backing: off until the ignition sweep has run, then eased on (R1-22); on at the goal.</summary>
        void UpdateDoorLight(double sceneTime)
        {
            float k = 0f;
            if (GameState.GoalReached) k = 1f;
            else if (_igniteStart >= 0.0) k = DoorLightAt(sceneTime - _igniteStart);
            ApplyDoorLight(k);
        }

        /// <summary>Door light level (0..1) at a time after the ignition start.</summary>
        public static float DoorLightAt(double secondsAfterIgnition)
            => BeatKit.Smootherstep((float)((secondsAfterIgnition - DOOR_LIGHT_DELAY) / DOOR_LIGHT_RAMP));

        void ApplyDoorLight(float k)
        {
            if (k == _doorLight) return;
            _doorLight = k;
            if (_doorLamp != null) _doorLamp.intensity = DOOR_LAMP_I * k;
            if (_lensMat != null) _lensMat.SetFloat(ShaderIds.EmissionStrength, _lensBase * k);
            if (_backingMat != null) _backingMat.SetFloat(ShaderIds.EmissionStrength, BACKING_EMISSION * k);
        }

        /// <summary>QA / tests: the current door light level (0 dark .. 1 lit).</summary>
        public float DoorLightLevel => _doorLight;

        void ApplyActivation(float d, bool force)
        {
            bool lattice = _latticeOn ? d >= AbyssConst.D_EXTERIOR_ON - ACTIVATION_HYSTERESIS : d >= AbyssConst.D_EXTERIOR_ON;
            bool dark = _darkOn ? d > AbyssConst.D_DARK_CARD - DARK_CARD_HYSTERESIS : d > AbyssConst.D_DARK_CARD;
            if (_editMode) { lattice = true; dark = true; }
            if (force || lattice != _latticeOn)
            {
                _latticeOn = lattice;
                ApplyHoneycomb(true);
            }
            if (force || dark != _darkOn)
            {
                _darkOn = dark;
                if (_darkCard != null) _darkCard.SetActive(dark);
            }
        }

        void ApplyLeaf(float open)
        {
            if (open == _lastOpen) return;
            _lastOpen = open;
            if (_leafPivot != null) _leafPivot.localRotation = Quaternion.Euler(0f, -LEAF_OPEN_DEG * open, 0f);
            if (_doorSpot != null)
            {
                _doorSpot.intensity = DOOR_SPOT_I * open;
                Vector3 d = DoorSpotAim(_tier, open) - DoorSpotPos(_tier);
                _doorSpot.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            if (_doorSpill != null) _doorSpill.intensity = DOOR_SPILL_I * open;
        }

        void Ignite(double sceneTime, Vector3 origin)
        {
            _ignited = true;
            _igniteStart = sceneTime;
            if (_editMode) return;
            // the Lattice's dots ignite linearly in the shader: start shifted and speed fitted to the R2-20 curve (5.4-12 km)
            double t = sceneTime + LatticeIgniteOffset;
            double start = t - 3600.0 * System.Math.Floor(t / 3600.0);
            Shader.SetGlobalVector(ShaderIds.AbLampIgnite, new Vector4((float)start, origin.x, origin.z, LatticeIgniteSpeed));
        }

        public override void PrepareCapture(double sceneTime)
        {
            ApplyActivation(GameState.D, true);
            ApplyHoneycomb(true);
            Vector3 p = GameState.PlayerPos, cam = GameState.CameraPos;
            bool past = (p.y < LOWER_LEVEL_Y && p.z <= HONEY_IGNITE_Z) || (cam.y < LOWER_LEVEL_Y && cam.z <= HONEY_IGNITE_Z);
            if (past)
            {
                // fully lit for the capture (lamps and door): ignition a minute before the capture time
                Ignite(sceneTime - 60.0, cam.y < LOWER_LEVEL_Y ? cam : p);
            }
            else
            {
                _ignited = false;
                _igniteStart = -1.0;
                if (!_editMode) Shader.SetGlobalVector(ShaderIds.AbLampIgnite, new Vector4(-1f, 0f, 0f, 0f));
            }
            UpdateGlowFades(cam, sceneTime);
            UpdateDoorLight(sceneTime);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _allBatches.Count; i++) InstancedRegistry.Unregister(_allBatches[i]);
        }

        // ================================================================================================ QA hooks

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            float x = X, y = Yf, zF = AbyssConst.W[16].z;
            var mouth = new Bounds(new Vector3(x, y + AbyssConst.TUNNEL_H * 0.5f, zF), new Vector3(AbyssConst.TUNNEL_W, AbyssConst.TUNNEL_H, 0.1f));
            if (keyShotTag == "KS7a_tunnel_mouth")
            {
                subjects.Add(new KeyShotSubject("bright mouth rectangle", mouth, 0.15f));
            }
            else if (keyShotTag == "KS7b_honeycomb")
            {
                float zB = AbyssConst.W[17].z;
                var kerb = new Bounds(new Vector3(x, y + KERB_H * 0.5f, zB + EDGE_INSET + KERB_W * 0.5f), new Vector3(KS7B_KERB_SPAN, KERB_H, KERB_W));
                subjects.Add(new KeyShotSubject("balcony front edge and kerb", kerb));
                subjects.Add(new KeyShotSubject("holes 0.7-2.8 km", new Bounds(new Vector3(x, AbyssConst.TERRACE_Y - LATTICE_DROP, -1750f), new Vector3(2000f, 1f, 2100f)), 0.1f));
            }
            else if (keyShotTag == "KS7c_goal_door")
            {
                Bounds door = _frameB.size.sqrMagnitude > 0f ? _frameB
                    : new Bounds(new Vector3(x, y + FRAME_H * 0.5f, AbyssConst.W[18].z + FRAME_DEPTH * 0.5f), new Vector3(FRAME_W, FRAME_H, FRAME_DEPTH));
                subjects.Add(new KeyShotSubject("door", door, 0.04f));
            }
            else if (keyShotTag == "KS7d_look_back")
            {
                subjects.Add(new KeyShotSubject("tunnel mouth", mouth));
                Vector3 fan = AbyssConst.FAN_CENTRE;
                subjects.Add(new KeyShotSubject("fan bore lower rim", new Bounds(new Vector3(fan.x, fan.y - AbyssConst.FAN_R_OUT + 15f, zF), new Vector3(200f, 30f, 1f))));
            }
        }

        public override void ReportScale(ScaleReport report)
        {
            report.Add("tunnel.length", _tunnelFloorB.size.z);
            report.Add("tunnel.width", _liningB.size.x);
            report.Add("tunnel.height", _liningB.max.y - Yf);
            report.Add("balcony.width", _balconyB.size.x);
            report.Add("balcony.depth", _balconyB.size.z);
            report.Add("balcony.altitude", _walkTopY - AbyssConst.TERRACE_Y);
            report.Add("balcony.thickness", _walkTopY - _balconyB.min.y);
            report.Add("pier.length", _balconyB.min.z - _pierB.min.z);
            report.Add("pier.width", _pierB.size.x);
            report.Add("door.height", _leafB.size.y);
            report.Add("door.width", _leafB.size.x);
            report.Add("door.frame.width", _frameB.size.x);
            report.Add("door.frame.height", _frameB.max.y - _frameBaseY);
            report.Add("door.z", _frameB.min.z);
            if (_faceBSet)
            {
                report.Add("face.width", _faceB.size.x);
                report.Add("face.height", _faceB.size.y);
            }
            report.Add("honeycomb.radius", _latticeRadius);
            report.Add("honeycomb.cells.28km", CountCellsWithin(AbyssConst.SHAFT_CENTRE, AbyssConst.HONEY_TIER2));
            if (_tier != BeatTier.Graybox)
            {
                report.Add("honeycomb.tier0.cells", _tier0Cells.Count);
                report.Add("honeycomb.tier1.cells", _tier1Count);
                report.Add("honeycomb.lamps", _lampCount);
                report.Add("honeycomb.tube.depth", _tubeDepth);
                report.Add("honeycomb.rings", _ringCount);
                report.Add("honeycomb.bridge.length", _bridgeLength);
                report.Add("honeycomb.deck.y", _deckTopY);
            }
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);

        // ================================================================================================ helpers

        static Material TunnelVariant(MaterialLibrary mats, MatId id)
        {
            // tunnel interiors never receive the Key (lead decision): the Key Light Mask scales the main light to 0
            return mats.Variant(id, FogDomain.Tunnel, "nokey", m => m.SetFloat(KeyLightMaskId, 0f));
        }

        void AddFaceBounds(Bounds b)
        {
            if (!_faceBSet) { _faceB = b; _faceBSet = true; }
            else _faceB.Encapsulate(b);
        }

        static Bounds Offset(Bounds b, Vector3 o) => new Bounds(b.center + o, b.size);

        static float Smooth01(float a, float b, float x)
        {
            float t = b > a ? (x - a) / (b - a) : (x >= b ? 1f : 0f);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
