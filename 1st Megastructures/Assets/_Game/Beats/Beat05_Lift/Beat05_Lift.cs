using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 5 - lift and the vertical city (14.5.5, 14.7.1, 14.7.3; d 439.5-469.5, ride seconds 0-120). The money shot of scale.
    ///
    /// Full tier (this file: flow, lights, per-frame, QA hooks; the parts live in the partial files of this folder):
    /// - Dock (Beat05_Lift.Platform.cs): the 10 m apron at the hall exit, concrete with a worn path, a cantilevered steel nosing,
    ///   rails on both sides and knee braces to the hull. Its edge stops 0.4 m short of the deck (the bridging plate spans the
    ///   gap) and the thick slab 0.7 m short, because every platform part slides +X (0.244 m per metre of drop) under it.
    /// - Platform (root at AbyssConst.LIFT_TOP, identity rotation, unit scale, carrying the <see cref="LiftController"/>; every
    ///   visual is a child so it rides along): 20 x 20 x 1.2 deck = steel body, 9 x 9 chamfered tread plates (top = 0), a kerb,
    ///   underside girders; 1.1 m rails on four sides with owned corner posts (no duplicated or co-planar members); the -Z gate
    ///   and the +X boarding section as sliding open rail sections driven by GateOpen01 / BoardingRailClosed01; the two 0.4 m
    ///   bridging plates (PlateExtend01); four 0.8 x 0.8 x 12 pylons at (+/-7, +/-7) with base plates, bolts, collars and
    ///   inboard lamp heads (cold lenses and glows); four O 0.12 cables; two guide shoes clamping the polished running rails.
    /// - Guide rails (Beat05_Lift.Rails.cs): two 4 x 6 m lattice towers (48 instanced 12 m segments each, ThinLine members,
    ///   polished running rails, ties between the twins), 40.97 m past each dock, hung from a head frame tied to the hull and
    ///   ending in counterweights. They cast no shadows: a lattice shadow sweeping the riding deck would break the 14.10
    ///   "deck difference at most 1 level" ride sequence (the deck keeps the static shadows of its own pylons and rails).
    /// - City (Beat05_Lift.City.cs): shader-tier M_Facade quads (one shared-vertex grid per face and 200 m chunk, persistent:
    ///   the faint far city seen from the bridge), near-tier module meshes (48 windows with surrounds, chamfered reveals and
    ///   recessed glass, V joints) instanced per 2 x 3 module tile with a DetailTier each (130-170 m dithered fade over the
    ///   always-present quad), 200 protruding blocks with window panels and girder frames, eight freight cars of three
    ///   WIN_COLD glows climbing the left wall at 3-6 m/s on their tracks.
    /// Simplified tier: no blocks, the 400 modules nearest the walked line with a 70-100 m fade. Graybox tier
    /// (Beat05_Lift.Graybox.cs): the phase-3 platform, rails, guide boxes and window-shader quads.
    ///
    /// Colliders are final and identical in every tier: dock floor, dock edge walls, the static rail walls on the deck's +X edge
    /// outside the 4 m boarding opening (z 259-267, 271-279) and a gap wall across the opening that is enabled only while the
    /// platform is away from the top dock. The deck itself has no colliders: the player rides kinematically (14.7.1) and leaves
    /// through the gate corridor onto the gantry (Beat06), so nothing overlaps the gate corridor. The four pylons stand inside
    /// the 18 x 18 walk square; Beat05Layout.DeckKeepOut / KeepOut give the capsule-centre keep-out squares (1.0 m half-size)
    /// and the ClampSoft-style push-out for PlayerController.MoveOnLift to apply after its rectangle clamp (the runtime hook
    /// belongs to the lead: Runtime cannot reference this assembly, so LiftController needs a static the beat sets in Build).
    /// </summary>
    public sealed partial class Beat05_Lift : BeatBase
    {
        public override int Index => 5;

        /// <summary>Generated city modules (14.5.5, exported for the tests): 3 x 17 x 24 minus the fan zone and mouth window.</summary>
        public static int CITY_MODULES => Beat05Layout.CITY_MODULES;

        /// <summary>Generated city windows: 48 x CITY_MODULES.</summary>
        public static int CITY_WINDOWS => Beat05Layout.CITY_WINDOWS;

        // ---------------------------------------------------------------- beat-specific constants (shared by all tiers)
        const float DOCK_L = 10f, DOCK_T = 1f;
        const float DECK_EDGE_WALL_T = 0.6f;
        const float RAIL_TUBE = 0.06f, RAIL_POST_SPACING = 2f, RAIL_INSET = 0.1f;
        const float OPENING = 4f;            // boarding opening (+X) and gate (-Z) width
        const float SLIDE_INSET = 0.12f;     // sliding sections run inside the fixed rail line (no co-planar overlap)
        const float PLATE_DEPTH = 0.4f, PLATE_T = 0.03f, PLATE_TOP = -0.008f, PLATE_HOME = 9.75f, PLATE_TRAVEL = 0.45f;
        const float PYLON_SIZE = 0.8f, PYLON_H = 12f, PYLON_OFFSET = Beat05Layout.PYLON_OFFSET;
        const float CABLE_R = 0.06f;
        const float SPOT_I = 1500f, SPOT_RANGE = 90f, SPOT_ANGLE = 75f;
        const float LAMP_Y = 12.14f;         // pylon lamp head centre above the deck (the head sits on the 12 m column)
        const float LAMP_INBOARD = 0.15f;    // lamp heads sit inboard on the pylon tops (they pass 6 cm under the dock nosing)

        // ---------------------------------------------------------------- state
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<InstancedBatch> _batches = new List<InstancedBatch>();
        readonly List<DetailTier> _tiers = new List<DetailTier>();
        readonly List<MeshRenderer> _facades = new List<MeshRenderer>();
        Transform _geo, _platform, _gate, _boarding, _plateX, _plateZ;
        MeshRenderer _deck, _deckTop, _rails, _pylons, _dock, _gateR, _plateXR, _cables;
        LiftController _lift;
        BoxCollider _dockGap;
        int _guideSegments;
        bool _editMode;
        BeatTier _tier;
        uint _seed;
        int _modulesBuilt, _blocksBuilt;
        Mesh _moduleMesh;

        float DeckHalf => AbyssConst.LIFT_SIZE * 0.5f;

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat05");
            _editMode = ctx.EditMode;
            _tier = ctx.Tier;
            _seed = ctx.Seed;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            BuildColliders(ctx);
            if (ctx.Tier == BeatTier.Graybox) BuildGraybox(ctx);
            else BuildFull(ctx);
            BuildLights(ctx);
            // last: the controller's Awake (play mode) snaps the root to the top dock and publishes the lift state
            if (_platform != null) _lift = _platform.gameObject.AddComponent<LiftController>();
            LiftController.DeckKeepOut = Beat05Layout.DeckKeepOut(); // pylon keep-outs enforced by PlayerController.MoveOnLift
            ApplyLiftVisuals();
            UpdateBridgeVoid(GameState.D);
            UpdateFreight(SceneClock.Time);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            Vector3 top = AbyssConst.LIFT_TOP;
            float y = top.y, zc = top.z, half = DeckHalf, open = OPENING * 0.5f;
            float xa = AbyssConst.HALL_X1, xb = AbyssConst.HALL_X1 - DOCK_L;   // -142 .. -152 (the deck's +X edge)

            CorridorKit.Floor(c, "DockFloor", new Vector3((xa + xb) * 0.5f, y, zc), new Vector2(DOCK_L, PathGuide.DOCK_APRON_W));
            BeatKit.Wall(c, "DockEdgeS", new Vector2(xa, zc - half), new Vector2(xb, zc - half), y - 1f, 4f, new Vector2(0f, -1f));
            BeatKit.Wall(c, "DockEdgeN", new Vector2(xa, zc + half), new Vector2(xb, zc + half), y - 1f, 4f, new Vector2(0f, 1f));
            // the deck's +X rail outside the boarding opening (static: the platform carries no colliders). Thin bodies: the
            // kinematic player's eye may stand 1.35 m inside the deck edge, and CameraRig flags a camera inside any collider.
            BeatKit.Wall(c, "DeckRailS", new Vector2(xb, zc - half), new Vector2(xb, zc - open), y - 1f, 4f, new Vector2(-1f, 0f), 0f, DECK_EDGE_WALL_T);
            BeatKit.Wall(c, "DeckRailN", new Vector2(xb, zc + open), new Vector2(xb, zc + half), y - 1f, 4f, new Vector2(-1f, 0f), 0f, DECK_EDGE_WALL_T);
            _dockGap = BeatKit.Wall(c, "DockGap", new Vector2(xb, zc - open), new Vector2(xb, zc + open), y - 1f, 4f, new Vector2(-1f, 0f), 0f, DECK_EDGE_WALL_T);
            _dockGap.enabled = false;
            CorridorKit.SafetyBelow(c, "SafetyDock", new Vector3((xa + xb) * 0.5f, y, zc), new Vector2(DOCK_L, PathGuide.DOCK_APRON_W));
        }

        /// <summary>Full and Simplified tiers (Fallbacks 14.5.5: Simplified = no blocks, 400 modules, near tier within 100 m).</summary>
        void BuildFull(BeatContext ctx)
        {
            bool full = ctx.Tier == BeatTier.Full;
            MakeMaterials(ctx.Mats);
            MakeCityMaterials(ctx.Mats);
            var mb = new MeshBuilder();
            BuildDockFull(ctx, mb);
            BuildPlatformFull(ctx, mb);
            BuildGuideRailsFull(ctx, mb);
            Beat05Layout.PlaceFreight(_seed, _freight);   // before the blocks: they keep clear of the tracks
            BuildCityFar(ctx, mb);
            BuildCityModules(ctx, mb, full);
            if (full) BuildBlocks(ctx, mb);
            BuildFreight(ctx, mb);
        }

        /// <summary>Seal and register an instanced batch (play mode only; EditMode builds keep it unregistered).</summary>
        InstancedBatch Register(InstancedBatch b)
        {
            b.Seal();
            _batches.Add(b);
            if (!_editMode) InstancedRegistry.Register(b);
            return b;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _batches.Count; i++) InstancedRegistry.Unregister(_batches[i]);
        }

        // ---------------------------------------------------------------- lights

        /// <summary>
        /// Four pylon spots (14.2: 75 degrees, range 90, I 1500, #D5E4F2, no shadows), children of the platform. "Outward and down",
        /// steep enough that each throws a pool around its pylon base on the deck; the +Z pair leans toward the guide rails, the
        /// -Z pair toward the left wall and the blocks within range.
        /// </summary>
        void BuildLights(BeatContext ctx)
        {
            if (_platform == null) return;
            Transform lights = BeatKit.Group(_platform, "Lights");
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                float inb = PYLON_OFFSET - LAMP_INBOARD;
                var pos = new Vector3(sx * inb, LAMP_Y, sz * inb);
                Vector3 dir = sz > 0f ? new Vector3(sx * 0.25f, -1f, 0.6f) : new Vector3(sx * 0.45f, -1f, -0.45f);
                BeatKit.Spot(lights, "PylonSpot" + i, pos, dir, AbyssPalette.FIXTURE_LIGHT, SPOT_I, SPOT_RANGE, SPOT_ANGLE);
            }
        }

        // ================================================================================================ per frame

        public override void Tick(in BeatFrame f)
        {
            ApplyLiftVisuals();
            UpdateCityTiers(f.CameraPos);
            UpdateBridgeVoid(f.D);
            UpdateFreight(f.Time);
        }

        public override void PrepareCapture(double sceneTime)
        {
            ApplyLiftVisuals();
            UpdateBridgeVoid(GameState.D);
            UpdateFreight(sceneTime);
        }

        void ApplyLiftVisuals()
        {
            if (_lift == null) return;
            LiftPhase ph = _lift.Phase;
            float ri = DeckHalf - RAIL_INSET;
            if (_gate != null) _gate.localPosition = new Vector3(-OPENING * _lift.GateOpen01, 0f, -ri + SLIDE_INSET);
            if (_boarding != null) _boarding.localPosition = new Vector3(ri - SLIDE_INSET, 0f, OPENING * (1f - _lift.BoardingRailClosed01));
            float ex = ph <= LiftPhase.Dwell ? _lift.PlateExtend01 : 0f;
            float ez = ph >= LiftPhase.Arrived ? _lift.PlateExtend01 : 0f;
            if (_plateX != null) _plateX.localPosition = new Vector3(PLATE_HOME + PLATE_TRAVEL * ex, PLATE_TOP - PLATE_T * 0.5f, 0f);
            if (_plateZ != null) _plateZ.localPosition = new Vector3(0f, PLATE_TOP - PLATE_T * 0.5f, -(PLATE_HOME + PLATE_TRAVEL * ez));
            bool gap = ph != LiftPhase.DockedTop;
            if (_dockGap != null && _dockGap.enabled != gap) _dockGap.enabled = gap;
        }

        // ================================================================================================ QA hooks

        /// <summary>World position of a platform-local point at ride second t (analytic, independent of the live lift state).</summary>
        static Vector3 PlatformPointAt(double rideSeconds, Vector3 local)
            => LiftController.PositionAtS(AbyssConst.LiftS(rideSeconds)) + local;

        static Bounds LocalBoxAt(double rideSeconds, Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(PlatformPointAt(rideSeconds, min), PlatformPointAt(rideSeconds, max));
            return b;
        }

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            float ri = DeckHalf - RAIL_INSET, h = AbyssConst.RAIL_H, hp = 0.5f * PYLON_SIZE;
            if (keyShotTag == "KS5a_lift_city")
            {
                // KS5a (ride 60 s): the deck rail runs in front of the eye and the (-7, -7) pylon. The pose comes from the key-shot
                // table, so a re-posed KS5a (R1-25) keeps its subjects honest.
                KeyShot ks = FindKeyShot(keyShotTag, new KeyShot(keyShotTag, 5, new Vector3(-103.5f, -245.3f, 269f), 240f, 3f, 66f, true, 60f));
                AddRailRuns(subjects, 60.0, ks.LiftOffset, ks.Yaw);
                subjects.Add(new KeyShotSubject("pylon", LocalBoxAt(60.0, new Vector3(-PYLON_OFFSET - hp, 0f, -PYLON_OFFSET - hp),
                    new Vector3(-PYLON_OFFSET + hp, PYLON_H, -PYLON_OFFSET + hp))));
                Vector3 eye = ks.Eye;
                if (TryBlocksInView(eye, ks.Yaw, out Bounds blocks))
                    subjects.Add(new KeyShotSubject("protruding blocks", blocks));
                // the face-210 facade around the view axis about 190 m out
                Vector3 p = eye + AbyssConst.Dir(ks.Yaw, ks.Pitch) * 190f;
                subjects.Add(new KeyShotSubject("facade 190 m", new Bounds(p, new Vector3(120f, 120f, 120f)), 0.2f));
            }
            else if (keyShotTag == "KS5b_lift_fan")
            {
                float r = AbyssConst.FAN_R_OUT;
                subjects.Add(new KeyShotSubject("blade ring", new Bounds(AbyssConst.FAN_CENTRE, new Vector3(2f * r, 2f * r, 14f)), 0.5f));
            }
            else if (keyShotTag == "KS5c_lift_down")
            {
                float zg = -ri + SLIDE_INSET;
                subjects.Add(new KeyShotSubject("front gate", LocalBoxAt(100.0, new Vector3(-OPENING * 0.5f, 0f, zg - 0.05f), new Vector3(OPENING * 0.5f, h, zg + 0.05f))));
                float gy = AbyssConst.GANTRY_Y, gz = AbyssConst.GANTRY_Z0;
                subjects.Add(new KeyShotSubject("gantry below", new Bounds(new Vector3(AbyssConst.W[14].x, gy - 2f, gz - 40f), new Vector3(AbyssConst.GANTRY_W, 4f, 80f))));
            }
        }

        /// <summary>Rail runs within this bearing of the KS5a yaw are its deck rail subjects (inside the 16:10 half-FOV of 46 degrees).</summary>
        const float RAIL_SUBJECT_HALF_ANGLE = 38f;

        static KeyShot FindKeyShot(string tag, KeyShot fallback)
        {
            for (int i = 0; i < KeyShots.All.Length; i++) if (KeyShots.All[i].Tag == tag) return KeyShots.All[i];
            return fallback;
        }

        /// <summary>
        /// The deck rail subjects of a lift key shot (R1-39): one box per deck side (rail line, 0 to 1.1 m), clipped to the part
        /// whose bearing from the eye lies within +/-<see cref="RAIL_SUBJECT_HALF_ANGLE"/> of the yaw. A single box around an
        /// L-shaped selection has its inner corner beside the eye (the old "deck rail" box had a corner 0.2 m in front of it
        /// and projected to x 0.001-11.4, failing the lint in all four variants although the rail is in frame).
        /// </summary>
        void AddRailRuns(List<KeyShotSubject> subjects, double rideSeconds, Vector3 eyeLocal, float yaw)
        {
            float ri = DeckHalf - RAIL_INSET, h = AbyssConst.RAIL_H;
            for (int side = 0; side < 4; side++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (float t = -ri; t <= ri + 1e-3f; t += 0.25f)
                {
                    Vector3 q = RailPoint(side, ri, t);
                    float bearing = Mathf.Atan2(q.x - eyeLocal.x, q.z - eyeLocal.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(bearing, yaw)) > RAIL_SUBJECT_HALF_ANGLE) continue;
                    lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t);
                }
                if (hi - lo < 2f) continue;
                Vector3 a = RailPoint(side, ri, lo), b = RailPoint(side, ri, hi);
                var min = new Vector3(Mathf.Min(a.x, b.x) - 0.03f, 0f, Mathf.Min(a.z, b.z) - 0.03f);
                var max = new Vector3(Mathf.Max(a.x, b.x) + 0.03f, h, Mathf.Max(a.z, b.z) + 0.03f);
                subjects.Add(new KeyShotSubject(RailSideNames[side], LocalBoxAt(rideSeconds, min, max)));
            }
        }

        static readonly string[] RailSideNames = { "deck rail -X", "deck rail +X", "deck rail -Z", "deck rail +Z" };

        /// <summary>Deck-local point at parameter t (-ri..ri) along the rail of a side (0 -X, 1 +X, 2 -Z, 3 +Z).</summary>
        static Vector3 RailPoint(int side, float ri, float t)
        {
            switch (side)
            {
                case 0: return new Vector3(-ri, 0f, t);
                case 1: return new Vector3(ri, 0f, t);
                case 2: return new Vector3(t, 0f, -ri);
                default: return new Vector3(t, 0f, ri);
            }
        }

        public override void ReportScale(ScaleReport report)
        {
            if (_deck != null)
            {
                Bounds b = _deck.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("lift.size", b.size.x);
                report.Add("lift.size.z", b.size.z);
                float top = _deckTop != null ? _deckTop.GetComponent<MeshFilter>().sharedMesh.bounds.max.y : b.max.y;
                report.Add("lift.thick", top - b.min.y);
                report.Add("lift.deck.top", top);   // 0: the platform origin is the deck top (14.7.1)
            }
            if (_rails != null) report.Add("lift.rail.height", _rails.GetComponent<MeshFilter>().sharedMesh.bounds.max.y);
            if (_pylons != null)
            {
                Bounds pb = _pylons.GetComponent<MeshFilter>().sharedMesh.bounds;
                // graybox: plain 12 m boxes; full: column top above the deck top (base plates, collars and lugs lie below it)
                report.Add("lift.pylon.height", _tier == BeatTier.Graybox ? pb.size.y : pb.max.y);
                report.Add("lift.pylon.offset", PYLON_OFFSET);
                report.Add("lift.pylon.size", PYLON_SIZE);
                // capsule-centre keep-out around each pylon (Beat05Layout.DeckKeepOut; enforced by the player once wired)
                report.Add("lift.pylon.keepout.half", Beat05Layout.PYLON_KEEP_OUT_HALF);
            }
            if (_cables != null) report.Add("lift.cable.diameter", 2f * CABLE_R);
            if (_dock != null)
            {
                Bounds b = _dock.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("dock.width", b.size.z);
                // walkable dock from the hall exit to the deck edge: the apron (full tier: slab + nosing, then the extended
                // bridging plate of the platform, which closes the 0.4 m gap the descending platform needs)
                float xEnd = b.min.x;
                if (_tier != BeatTier.Graybox && _plateXR != null)
                {
                    float pw = _plateXR.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                    xEnd = AbyssConst.LIFT_TOP.x + PLATE_HOME + PLATE_TRAVEL - 0.5f * pw;
                }
                report.Add("dock.length", b.max.x - xEnd);
            }
            if (_plateXR != null) report.Add("lift.plate.depth", _plateXR.GetComponent<MeshFilter>().sharedMesh.bounds.size.x);
            report.Add("lift.opening.width", OPENING);
            report.Add("guide.rail.segments", _guideSegments);
            report.Add("guide.rail.length", Beat05Layout.GuideRailLength);
            report.Add("guide.rail.extension", Beat05Layout.GuideRailExtension);
            report.Add("guide.rail.markers", _markerCount);
            Vector3 travel = LiftController.PositionAtS(AbyssConst.LIFT_DROP) - LiftController.PositionAtS(0.0);
            report.Add("lift.drop", -travel.y);
            report.Add("lift.travel.x", travel.x);
            report.Add("lift.ride.seconds", RideDuration());
            report.Add("lift.ride.travel", AbyssConst.LiftS(AbyssConst.LIFT_DURATION) - AbyssConst.LiftS(0.0));
            report.Add("city.facade.pieces", _facades.Count);
            if (_tier != BeatTier.Graybox)
            {
                report.Add("lift.hull.clearance", HullClearance());
                report.Add("city.modules", _modulesBuilt);
                report.Add("city.windows", _modulesBuilt * Beat05Layout.WindowsPerModule);
                report.Add("city.module.windows", ModuleWindowCount());
                report.Add("city.overhang", Beat05Layout.Overhang);
                if (_moduleMesh != null)
                {
                    report.Add("city.module.width", _moduleMesh.bounds.size.x);
                    report.Add("city.module.height", _moduleMesh.bounds.size.y);
                    report.Add("city.module.triangles", _moduleMesh.triangles.Length / 3);
                }
                report.Add("city.modules.exported", Beat05Layout.CITY_MODULES);
                report.Add("city.windows.exported", Beat05Layout.CITY_WINDOWS);
                report.Add("city.fan.zone.modules", Beat05Layout.CITY_FAN_ZONE_MODULES);
                report.Add("city.fan.zone.modules.spec", Beat05Layout.SpecCentreRuleFanModules());
                report.Add("city.mouth.modules", Beat05Layout.CITY_MOUTH_MODULES);
                report.Add("city.bridge.zone.modules", Beat05Layout.CITY_BRIDGE_ZONE_MODULES);
                report.Add("city.top.270", Beat05Layout.CITY_TOP_270);
                float worstT = 0f;
                for (int i = 0; i < _blocks.Count; i++) worstT = Mathf.Max(worstT, Beat05Layout.BlockBridgeTransmittance(_blocks[i]));
                report.Add("city.blocks.bridge.view.max", worstT);
                report.Add("city.near.tier", _tier == BeatTier.Full ? MODULE_NEAR : MODULE_NEAR_SIMPLIFIED);
                report.Add("city.near.fade.start", _tier == BeatTier.Full ? MODULE_NEAR - MODULE_FADE : MODULE_NEAR_SIMPLIFIED - MODULE_FADE_SIMPLIFIED);
                report.Add("city.near.hysteresis", MODULE_HYSTERESIS);
                report.Add("city.tiles", _tiers.Count);
                report.Add("city.blocks", _blocksBuilt);
                report.Add("city.block.width", Beat05Layout.BLOCK_W);
                report.Add("city.block.depth", Beat05Layout.BLOCK_D);
                report.Add("city.block.height", Beat05Layout.BLOCK_H);
                float maxOut = 0f;
                for (int i = 0; i < _blocks.Count; i++) maxOut = Mathf.Max(maxOut, _blocks[i].StandoffM + Beat05Layout.BLOCK_D);
                report.Add("city.block.max.from.wall", maxOut);
                report.Add("freight.groups", _freight.Count);
                report.Add("freight.lights", _freightLights.Count);
                report.Add("freight.loop", Beat05Layout.FREIGHT_LOOP);
                float vMin = float.MaxValue, vMax = 0f;
                for (int i = 0; i < _freight.Count; i++) { vMin = Mathf.Min(vMin, _freight[i].Speed); vMax = Mathf.Max(vMax, _freight[i].Speed); }
                if (_freight.Count > 0) { report.Add("freight.speed.min", vMin); report.Add("freight.speed.max", vMax); }
            }
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);

        /// <summary>Ride seconds at which s(t) first reaches the full drop (14.5.5 acceptance: 120.00 +/- 0.05 s).</summary>
        static double RideDuration()
        {
            double t = 0.0;
            while (t < 1000.0 && AbyssConst.LiftS(t) < AbyssConst.LIFT_DROP - 1e-9) t += 0.001;
            return t;
        }
    }
}
