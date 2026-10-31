using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2 - railless bridge over the abyss (14.5.2, d 60-216, money shot) and the persistent shaft skeleton it reveals
    /// (14.6, 14.7.2). Owns, for the whole walk: the hexagonal shaft shell S0 (6 faces x 22 vertical chunks of 200 m with
    /// chunk-local vertices; the near flat z = 60 has the gallery doorway, the tunnel mouth and the fan bore R 150), the ring
    /// ribs and belts (<see cref="Beat02Rings"/>: 55 + 19 levels at AbyssConst.RingY, instanced; a vertical hoist lane on the
    /// far flat and gapped near-flat segments around the bore), the banded columns C1-C4 (200 m lathe chunks; C1 128 segments so
    /// the beat-3 collar meets it) and dim cold marker lights on the ring corners and column bands above the walkway.
    /// The bridge: 8 x 6 landing on a stepped concrete console, 1.2 x 150 deck with pale 0.12 m edge lines that fade into the deck
    /// tone beyond 60 m, the 6 m Warren truss (25 bays, 75 I-section members, gussets, floor beams, bearings at both ends), five
    /// amber outrigger lamps (posts O 0.06 x 1.1, 0.9 m outside the right edge, downward spots I 5 range 9), the cold C1
    /// floodlight hung off the left fascia at z 180 (it grades the column face seen from the bridge; see FLOOD_*), the row of
    /// cold service lamps on the -30 belt of the right far wall and the falling bolt released from the first of them at d 82.5
    /// (Beat02_Bridge.Bolt, lead decision D3), and the cold washes that stage C3 against the far wall (Beat02_Bridge.FarLayer).
    /// The far hoist block runs on guide rails in the lane on the 583.3 s ping-pong (rising through the bridge crossing, see
    /// HOIST_U0), with two cold lamps (emissive quads plus min-3.5-px glow points), its cables and a cold wash light.
    ///
    /// Tiers (ctx.Tier): Full = all of the above with DetailTier swaps (bridge near detail within 150 m, fascia bolts within 40 m,
    /// 20 m hysteresis, dithered); Simplified (Fallbacks line) = ribs every 120 m, columns C1 and C3 only, no hoist; Graybox =
    /// shell, rings, four columns, landing, deck, truss, lamps and the hoist block without detail (tests build this tier).
    /// Persistent skeleton (14.6): shell, rings, columns, markers, deck, edge lines and lamps stay for the whole walk; at d 380
    /// (inside the hall, unseen) the truss, landing and hoist detail switch to low-poly far proxies and the local lights go off.
    /// Colliders are final: landing and deck floors, edge walls flush with the visible edges and safety volumes.
    /// </summary>
    public sealed partial class Beat02_Bridge : BeatBase
    {
        public override int Index => 2;

        // ---------------------------------------------------------------- beat-specific constants
        const float CHUNK = 200f;
        const float WALL_CELL = 50f;
        const int NEAR_FLAT = 4;      // face normal 270 degrees: the near flat z = 60
        const int FAR_FLAT = 1;       // face normal 90 degrees: the far flat z = 760
        const float SKELETON_D = 380f;          // 14.6: truss, landing and hoist detail off (proxies) from here on
        const float NEAR_TIER = AbyssConst.NEAR_TIER, TIER_HYST = 20f, TIER_FADE = 20f;
        // 14.2 values (I 5, range 9). Look-dev r01 had raised I to 7 so the warm pools read in KS2a; fix round 2 restores the
        // spec value: at 7 the lamp beside the KS2b eye lit the right edge line to luma 0.68, the arm below the globe to a brown
        // bar across the void strip and the player's shadow sepia (R2-23), and the first lamp's pool on the landing made
        // B2_start 0.8 % bright amber (R2-24). The pools still read where the Key does not reach them (B2_start, B2_end).
        public const float LAMP_I = 5f, LAMP_RANGE = 9f;
        /// <summary>
        /// Critic round 1 (R1-18, B2_end): as points the outrigger lamps also lit what is beside and above them; the last one
        /// (z 215.9, 6 m from C1) washed the column face and the ledge band amber (B2_end 9 % warm pixels), the first one the
        /// near flat around the doorway. Each is now a spot from the globe centre aimed straight down with a 160 degree cone and
        /// a 140 degree core. Fix round 2 (R2-23, lead note): inside the KS2b Key shadow the only light was this unshadowed
        /// amber spot (URP additional-light shadows are off), so the player's shadow on the deck read sepia (39, 32, 30). The
        /// cone is now 96 degrees with a 56 degree core: the deck left of x 0.1 (1.4 m or more from the lamp, 50 degrees or
        /// more from straight down, where the walker's shadow falls) is outside it, while the right edge
        /// beside the lamp (0.9 m, 38 degrees) keeps a warm strip, the landing under the first lamp keeps its pool, and
        /// nothing near the horizon is lit (C1, the ledge band, the near flat; Beat02Tests). I and range unchanged.
        /// </summary>
        public const float LAMP_SPOT_ANGLE = 96f, LAMP_SPOT_INNER = 56f;

        /// <summary>Axis of the outrigger lamp spots: straight down.</summary>
        public static Vector3 OutriggerSpotDirection => Vector3.down;

        /// <summary>Hoist travel: y(t) = 200 - 700 S(PingPong(u0 + t / 583.3)) (14.5.2 f).</summary>
        public const double HoistYTop = 200.0, HoistTravel = 700.0;
        public const double HoistYMin = HoistYTop - HoistTravel;

        // ---------------------------------------------------------------- state
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<InstancedBatch> _batches = new List<InstancedBatch>();
        bool _registered;
        BeatTier _tier;
        Transform _geo;
        Mesh _glowQuad;

        static readonly int RustId = Shader.PropertyToID("_RustAmount");
        static readonly int WearId = Shader.PropertyToID("_WearAmount");
        static readonly int FarTintId = Shader.PropertyToID("_FarTint");
        static readonly int FarRangeId = Shader.PropertyToID("_FarRange");
        static readonly int EnergyId = Shader.PropertyToID("_EnergyConserve");
        static readonly int CylRadiusId = Shader.PropertyToID("_CylRadius");
        static readonly int PanelCountId = Shader.PropertyToID("_PanelCount");

        // ================================================================================================ build

        public override void Build(BeatContext ctx)
        {
            BeatKit.CheckRoot(ctx, "Beat02");
            _tier = ctx.Tier;
            _geo = BeatKit.Group(ctx.Root, "Geometry");
            BuildColliders(ctx);

            var mb = new MeshBuilder();
            _glowQuad = BuildGlowQuad(mb);
            BuildShaftShell(ctx, mb);
            BuildRings(ctx);
            BuildColumns(ctx, mb);
            BuildBridge(ctx, mb);
            if (_tier != BeatTier.Simplified) BuildHoist(ctx, mb);
            BuildMarkers(ctx);
            BuildLights(ctx);
            BuildBolt(ctx, mb);
            BuildFarLayer(ctx);

            for (int i = 0; i < _batches.Count; i++) _batches[i].Seal();
            if (!ctx.EditMode)
            {
                for (int i = 0; i < _batches.Count; i++) InstancedRegistry.Register(_batches[i]);
                _registered = true;
            }
            ApplySkeleton(GameState.D >= SKELETON_D);
            ApplyHoist(SceneClock.Time);
            ApplyBolt(SceneClock.Time);
        }

        void BuildColliders(BeatContext ctx)
        {
            Transform c = ctx.Colliders;
            float y = AbyssConst.BRIDGE_Y;
            float lw = AbyssConst.LANDING_W * 0.5f, z0 = AbyssConst.SHAFT_NEAR_Z, z1 = AbyssConst.BRIDGE_Z0, z2 = AbyssConst.BRIDGE_Z1;
            float bw = AbyssConst.BRIDGE_W * 0.5f, dw = AbyssConst.EXIT_DOOR_W * 0.5f;
            float wb = y - 1f, wh = 4f;

            // landing 8 x 6 (z 60-66)
            CorridorKit.Floor(c, "LandingFloor", new Vector3(0f, y, (z0 + z1) * 0.5f), new Vector2(AbyssConst.LANDING_W, z1 - z0));
            BeatKit.Wall(c, "LandingWallL", new Vector2(-lw, z0), new Vector2(-lw, z1), wb, wh, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "LandingWallR", new Vector2(lw, z0), new Vector2(lw, z1), wb, wh, new Vector2(1f, 0f));
            BeatKit.Wall(c, "LandingFrontL", new Vector2(-lw, z1), new Vector2(-bw, z1), wb, wh, new Vector2(0f, 1f));
            BeatKit.Wall(c, "LandingFrontR", new Vector2(bw, z1), new Vector2(lw, z1), wb, wh, new Vector2(0f, 1f));
            BeatKit.Wall(c, "LandingBackL", new Vector2(-lw, z0), new Vector2(-dw, z0), wb, wh, new Vector2(0f, -1f), 0f, AbyssConst.EXIT_DEPTH);
            BeatKit.Wall(c, "LandingBackR", new Vector2(dw, z0), new Vector2(lw, z0), wb, wh, new Vector2(0f, -1f), 0f, AbyssConst.EXIT_DEPTH);

            // deck 1.2 x 150: the floor runs under the walls; the walls are flush with the visible edges and reach 5 cm onto
            // the ledge so they close against the ledge's outer wall opening (Beat03)
            CorridorKit.Floor(c, "BridgeFloor", new Vector3(0f, y, (z1 + z2) * 0.5f), new Vector2(AbyssConst.BRIDGE_W + 1f, z2 - z1));
            BeatKit.Wall(c, "BridgeEdgeL", new Vector2(-bw, z1), new Vector2(-bw, z2 + 0.05f), wb, wh, new Vector2(-1f, 0f));
            BeatKit.Wall(c, "BridgeEdgeR", new Vector2(bw, z1), new Vector2(bw, z2 + 0.05f), wb, wh, new Vector2(1f, 0f));

            CorridorKit.SafetyBelow(c, "SafetyLanding", new Vector3(0f, y, (z0 + z1) * 0.5f), new Vector2(AbyssConst.LANDING_W, z1 - z0));
            CorridorKit.SafetyBelow(c, "SafetyBridge", new Vector3(0f, y, (z1 + z2) * 0.5f), new Vector2(AbyssConst.BRIDGE_W, z2 - z1));
        }

        /// <summary>
        /// Glow sprite quad (Megastructure/Glow contract: positions +/-0.5 in xy, the object scale is the true diameter), with
        /// bounds inflated to +/-40 so the sprite's minimum-pixel growth at distance never falls outside the cull bounds.
        /// </summary>
        Mesh BuildGlowQuad(MeshBuilder mb)
        {
            Vector3 n = Vector3.back;
            int a = mb.Vertex(new Vector3(-0.5f, -0.5f, 0f), n), b = mb.Vertex(new Vector3(-0.5f, 0.5f, 0f), n);
            int c = mb.Vertex(new Vector3(0.5f, 0.5f, 0f), n), d = mb.Vertex(new Vector3(0.5f, -0.5f, 0f), n);
            BeatKit.Tri(mb, a, b, c);
            BeatKit.Tri(mb, a, c, d);
            Mesh m = BeatKit.Bake(mb, "B2_GlowQuad", _meshes);
            m.bounds = new Bounds(Vector3.zero, new Vector3(80f, 80f, 80f));
            return m;
        }

        Material GlowMaterial(MaterialLibrary mats, string key, Color srgb, float strength, float energyConserve, float minPixels = 2.5f)
        {
            return mats.Variant(MatId.Glow, key, m =>
            {
                m.SetColor(ShaderIds.EmissionColor, srgb.linear); // [HDR] colours are not converted
                m.SetFloat(ShaderIds.EmissionStrength, strength);
                m.SetFloat(ShaderIds.MinPixels, minPixels);
                m.SetFloat(EnergyId, energyConserve);
            });
        }

        // ================================================================================================ lights

        readonly Light[] _lampLights = new Light[5];

        void BuildLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            for (int i = 0; i < _lampHeads.Length; i++)
            {
                _lampLights[i] = BeatKit.Spot(lights, "OutriggerAmber" + i, _lampHeads[i], OutriggerSpotDirection, AbyssPalette.AMBER_LIGHT, LAMP_I,
                    LAMP_RANGE, LAMP_SPOT_ANGLE);
                _lampLights[i].innerSpotAngle = LAMP_SPOT_INNER;
            }
            // the C1 floodlight (see FLOOD_*): its light at the lens, never switched by the skeleton
            Vector3 dir = FloodDirection;
            Light flood = BeatKit.Spot(lights, "ColumnFlood", FLOOD_PIVOT + dir * FLOOD_LENS, dir, AbyssPalette.COLD_LAMP, FLOOD_I, FLOOD_RANGE,
                FLOOD_ANGLE);
            flood.innerSpotAngle = FLOOD_INNER;
        }

        // ================================================================================================ per frame

        int _skeletonState = -1;   // -1 unknown, 0 near detail live, 1 skeleton (d >= 380)

        public override void Tick(in BeatFrame f)
        {
            ApplyHoist(f.Time);
            bool skeleton = f.D >= SKELETON_D;
            if ((skeleton ? 1 : 0) != _skeletonState) ApplySkeleton(skeleton);
            if (!skeleton)
            {
                if (_bridgeTier != null) _bridgeTier.Update(f.CameraPos);
                for (int i = 0; i < _boltTiers.Count; i++) _boltTiers[i].Update(f.CameraPos);
            }
            TickBolt(f);
        }

        public override void PrepareCapture(double sceneTime)
        {
            ApplyHoist(sceneTime);
            ApplyBolt(sceneTime);
        }

        /// <summary>
        /// 14.6 persistent skeleton: from d 380 (inside the hall, the bridge is out of sight) the truss, landing and hoist detail
        /// switch to their far proxies and the local lights go off; walking back below 380 (teleports) restores them.
        /// </summary>
        void ApplySkeleton(bool skeleton)
        {
            _skeletonState = skeleton ? 1 : 0;
            // releasing: show the near tier now and let the next DetailTier.Update (same Tick) pick the coverage from scratch
            if (_bridgeTier != null) { _bridgeTier.ForceCoverage(skeleton ? 0f : 1f); if (!skeleton) _bridgeTier.ForceCoverage(-1f); }
            for (int i = 0; i < _boltTiers.Count; i++)
            {
                _boltTiers[i].ForceCoverage(skeleton ? 0f : 1f);
                if (!skeleton) _boltTiers[i].ForceCoverage(-1f);
            }
            for (int i = 0; i < _lampLights.Length; i++) if (_lampLights[i] != null) _lampLights[i].enabled = !skeleton;
            if (_hoistWash != null) _hoistWash.enabled = !skeleton;
            if (_hoistDetail != null) _hoistDetail.SetActive(!skeleton);
            if (_hoistProxy != null) _hoistProxy.SetActive(skeleton);
            if (_farPools != null) _farPools.Visible = !skeleton;
        }

        void OnDestroy()
        {
            if (!_registered) return;
            for (int i = 0; i < _batches.Count; i++) InstancedRegistry.Unregister(_batches[i]);
            _registered = false;
        }

        // ================================================================================================ QA hooks

        public override void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects)
        {
            float y = AbyssConst.BRIDGE_Y;
            Vector3 c1 = AbyssConst.COLUMNS[0], c3 = AbyssConst.COLUMNS[2];
            if (keyShotTag == "KS2a_bridge_ahead")
            {
                // the deck in view: from 4 m ahead of the KS2a eye (z 120; nearer lies below the frame) to the ledge
                subjects.Add(new KeyShotSubject("deck ahead", new Bounds(new Vector3(0f, y - DECK_T * 0.5f, 170f), new Vector3(AbyssConst.BRIDGE_W, DECK_T, 92f))));
                subjects.Add(new KeyShotSubject("lamp z 141", new Bounds(_lampHeads[2], Vector3.one * (2f * LAMP_GLOBE_R))));
                subjects.Add(new KeyShotSubject("lamp z 178.5", new Bounds(_lampHeads[3], Vector3.one * (2f * LAMP_GLOBE_R))));
                subjects.Add(new KeyShotSubject("C1", new Bounds(new Vector3(c1.x, -5f, c1.y), new Vector3(2f * c1.z, 60f, 2f * c1.z)), 0.1f));
                if (_columnChunks[2] != null)
                    subjects.Add(new KeyShotSubject("C3", new Bounds(new Vector3(c3.x, -25f, c3.y), new Vector3(2f * c3.z, 250f, 2f * c3.z))));
                if (_hoistBody != null) subjects.Add(new KeyShotSubject("hoist block", BeatKit.BoundsOf(_hoistBody)));
            }
            else if (keyShotTag == "KS2b_bridge_down")
            {
                subjects.Add(new KeyShotSubject("deck strip", new Bounds(new Vector3(0f, y - DECK_T * 0.5f, 141f), new Vector3(AbyssConst.BRIDGE_W, DECK_T, 20f)), 0.1f));
                // accepted as not visible at pitch -90 (DEVELOPMENT ledge row "KS2b 'truss below'": the 1.2 m deck hides the
                // single plane truss under its axis), so the lint waives it (R2-36 b)
                subjects.Add(new KeyShotSubject("truss" + CompositionLint.KnownConflictSuffix, new Bounds(new Vector3(0f, y - DECK_T - AbyssConst.BRIDGE_TRUSS_DEPTH * 0.5f, 141f),
                    new Vector3(TRUSS_MEMBER, AbyssConst.BRIDGE_TRUSS_DEPTH, 20f))));
            }
        }

        public override void ReportScale(ScaleReport report)
        {
            if (_deck != null)
            {
                Bounds b = _deck.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("bridge.width", b.size.x);
                report.Add("bridge.length", b.size.z);
                report.Add("bridge.thickness", b.size.y);
                report.Add("bridge.top.y", b.max.y);
            }
            if (_edgeLines != null)
            {
                Mesh m = _edgeLines.GetComponent<MeshFilter>().sharedMesh;
                Bounds b = m.bounds;
                report.Add("bridge.edgeline.width", b.size.x - 2f * EDGE_LINE_X);
                report.Add("bridge.edgeline.offset", b.max.y - AbyssConst.BRIDGE_Y);
            }
            if (_landing != null)
            {
                Bounds b = _landing.GetComponent<MeshFilter>().sharedMesh.bounds;
                report.Add("landing.width", b.size.x);
                report.Add("landing.length", b.size.z);
            }
            report.Add("bridge.lamp.count", _lampCount);
            if (_lampCount > 1)
                report.Add("bridge.lamp.spacing", (_lampHeads[_lampCount - 1].z - _lampHeads[0].z) / (_lampCount - 1));
            report.Add("bridge.lamp.spacing.inner", _lampHeads[2].z - _lampHeads[1].z);
            report.Add("bridge.lamp.outside", _lampHeads[0].x - AbyssConst.BRIDGE_W * 0.5f);
            report.Add("bridge.lamp.post.height", _postTopY - AbyssConst.BRIDGE_Y);
            report.Add("bridge.lamp.post.diameter", 2f * LAMP_POST_R);
            if (_truss != null)
            {
                report.Add("bridge.truss.depth", _truss.GetComponent<MeshFilter>().sharedMesh.bounds.size.y - TRUSS_MEMBER);
                report.Add("bridge.truss.members", _trussMembers);
                report.Add("bridge.truss.bays", TRUSS_BAYS);
            }
            report.Add("bolt.diameter", BOLT_D);
            report.Add("bolt.drop.d", BoltReleaseD);
            if (_bolt != null)
            {
                report.Add("bolt.lamp.x", _boltStart.x);
                report.Add("bolt.lamp.y", _boltStart.y);
            }

            string[] keys = { "c1.diameter", "c2.diameter", "c3.diameter", "c4.diameter" };
            for (int i = 0; i < 4; i++)
            {
                MeshRenderer[] chunks = _columnChunks[i];
                if (chunks == null || chunks.Length == 0 || chunks[0] == null) continue;
                Vector3 col = AbyssConst.COLUMNS[i];
                MeshRenderer mid = chunks[chunks.Length / 2];
                BeatKit.RadialRange(mid.GetComponent<MeshFilter>().sharedMesh, mid.transform.position, new Vector2(col.x, col.y), 1f,
                    out float rMin, out float rMax);
                report.Add(keys[i], 2f * rMin);
                report.Add("c" + (i + 1) + ".height", chunks[0].bounds.max.y - chunks[chunks.Length - 1].bounds.min.y);
                if (i == 0) report.Add("column.band.protrusion", rMax - rMin);
            }

            if (_farFlatChunk != null)
            {
                Mesh m = _farFlatChunk.GetComponent<MeshFilter>().sharedMesh;
                report.Add("shaft.apothem", m.bounds.center.z + _farFlatChunk.transform.position.z - AbyssConst.SHAFT_CENTRE.y);
            }
            if (_faceChunks.Count > 0)
            {
                float yMin = float.MaxValue, yMax = float.MinValue;
                for (int i = 0; i < _faceChunks.Count; i++)
                {
                    Bounds b = _faceChunks[i].bounds;
                    yMin = Mathf.Min(yMin, b.min.y);
                    yMax = Mathf.Max(yMax, b.max.y);
                }
                report.Add("shaft.height", yMax - yMin);
                report.Add("shaft.chunks", _faceChunks.Count);
            }
            if (_rings != null)
            {
                report.Add("shaft.ribs", _rings.Ribs);
                report.Add("shaft.belts", _rings.Belts);
                RingProportions(_rings.RibFull, out float rh, out float rp);
                RingProportions(_rings.BeltFull, out float bh, out float bp);
                report.Add("rib.height", rh);
                report.Add("rib.protrusion", rp);
                report.Add("belt.height", bh);
                report.Add("belt.protrusion", bp);
            }
            if (_hoistBody != null)
            {
                report.Add("hoist.size", _hoistBody.GetComponent<MeshFilter>().sharedMesh.bounds.size.x);
                report.Add("hoist.y0", HoistY(0.0));
            }
        }

        /// <summary>Ring height (mesh bounds) and protrusion (wall plane to the inner face, measured on the far side, x = 0).</summary>
        static void RingProportions(Mesh ring, out float height, out float protrusion)
        {
            height = 0f; protrusion = 0f;
            if (ring == null) return;
            height = ring.bounds.size.y;
            Vector3[] v = ring.vertices;
            float inner = float.MaxValue;
            for (int i = 0; i < v.Length; i++)
                if (Mathf.Abs(v[i].x) < 1f && v[i].z > 0f && v[i].z < inner) inner = v[i].z;
            protrusion = AbyssConst.SHAFT_APOTHEM - inner;
        }

        public override void CollectMeshes(List<Mesh> meshes) => meshes.AddRange(_meshes);
    }
}
