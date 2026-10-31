using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// The crank-slider assemblies of beat 4 (14.5.4 Geometry and Kinematics). Every static part is built once in a canonical
    /// bank frame (origin = crank centre at y 0, +X = slider axis toward the cylinder, +Z lateral, y = world y) and placed per
    /// bank with the bank matrix (type A: rotated 180 degrees about Y, so its axis points -X). Moving parts are built once in
    /// their own frames and posed every frame from the scene clock:
    /// <list type="bullet">
    /// <item>wheel: rim O30 x 4 (y -1..+3) with 8 tapered spokes, hub, counterweight opposite the pin, crank boss; the pin
    /// O2.5 at r 12, its nut, the O3 crankshaft down to the pit housing and its bolted coupling rotate with it ("shaft").</item>
    /// <item>connecting rod: 32 m between eye centres, tapered rounded section 1.5 x 1.1 to 0.9 x 0.7, big eye around the pin,
    /// small eye inside the crosshead fork; M_RodPolished, casts no shadow.</item>
    /// <item>crosshead: 4 x 3 x 3 envelope, forked toward the crank (the small eye swings in a 1.1 m slot), solid toward the
    /// cylinder; sliding between an upper and a lower guide bar (2 cm clearance) carried by two portal frames at the bar
    /// ends (dark matte paint, X-braced only in the leg plane facing away from the catwalk: critique R1-20).</item>
    /// <item>piston rod O1.6 from the crosshead front to the piston 27 m away (inside the cylinder).</item>
    /// <item>cylinder O12 x 28 (lathe, 64 segments): 1 m flanged heads (O14) with 24 bolts per flange, three stiffening bands,
    /// a 0.6 m gland on the rear head; two concrete piers with steel saddles; two O2.5 pipes rising to the ceiling.</item>
    /// <item>crank pit: weathered housing on a steel floor plate (rust), guard ring, amber hazard lamp.</item>
    /// </list>
    /// Bolts: within 25 m of the path (inner banks) merged into one mesh, beyond (outer banks) GPU-instanced; both are near
    /// detail of a per-bank DetailTier (70 m, 20 m hysteresis, dithered).
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        // wheel
        const float RIM_R_IN = 11f, HUB_R = 3.6f;
        // rod
        const float ROD_EYE_R = 2.2f, ROD_EYE_H = 1.8f, ROD_SMALL_EYE_R = 1.3f, ROD_SMALL_EYE_H = 1.0f;
        // guides and frames (bank frame)
        /// <summary>
        /// Two portal frames per bank, at the guide-bar ends (critique R1-20: four X-braced frames per bank formed 27 m long
        /// lattice walls 9 m beside the catwalk that hid the cylinders and wheels in KS4 and B4). The guide bars span the 27 m
        /// between them (0.8 x 3.2 m steel); the only diagonal bracing is one X in the OUTBOARD leg plane (away from the
        /// catwalk), so the catwalk-facing plane is two legs and a top tie.
        /// </summary>
        static readonly float[] FRAME_S = { 19f, 46f };
        /// <summary>Bank-frame lateral side (+1 / -1 of FRAME_LEG_L) of the leg plane facing away from the catwalk.</summary>
        static int OutboardSide(int k) => (AbyssConst.BANK_V[k] > 0f) == AbyssConst.BANK_TYPE_A[k] ? -1 : 1;
        const float FRAME_LEG_L = 8f, FRAME_TOP = 8.5f, GUIDE_S0 = 17.6f, GUIDE_S1 = 47f, GUIDE_W = 3.2f, GUIDE_T = 0.8f, GUIDE_GAP = 0.02f;
        // cylinder dressing (bank frame)
        static readonly float[] PIER_S = { 53.5f, 70.5f };
        static readonly float[] PIPE_S = { 53f, 71f };
        static readonly float[] PIPE_FLANGE_Y = { 16f, 22f, 28f, 34f };
        const float PIPE_R = 1.25f, CYL_BOLT_AF = 0.12f, PIPE_BOLT_AF = 0.085f;
        const int CYL_FLANGE_BOLTS = 24;
        // pit
        static readonly Vector3 PIT_LAMP = new Vector3(-5.4f, -19.6f, -5.4f);
        /// <summary>Banks whose bolts lie within 25 m of the walk line (merged); the others are instanced.</summary>
        static bool InnerBank(int k) => Mathf.Abs(AbyssConst.BANK_V[k]) < 25f;

        readonly List<BoltSpec> _bolts = new List<BoltSpec>(256);

        void BuildMachines(BeatContext ctx, Transform interior)
        {
            Transform root = BeatKit.Group(interior, "Machines");
            Transform moving = BeatKit.Group(root, "Moving");
            Material machine = MachineMat(), rodMat = Nokey(MatId.RodPolished), steel = Nokey(MatId.SteelPainted);
            Material conc = Nokey(MatId.ConcreteBoard), pitMat = WeatheredMat(), thin = ThinMat(), frameMat = FrameMat();
            var mb = new MeshBuilder();

            // ---- moving meshes
            Mesh wheel = BuildWheelMesh(mb);
            Mesh shaft = BuildShaftMesh(mb);
            Mesh conrod = BuildRodMesh(mb);
            Mesh crosshead = BuildCrossheadMesh(mb);
            Mesh xrods = BuildCrossheadRodsMesh(mb);

            // ---- static meshes (canonical bank frame) and the bolt list
            _bolts.Clear();
            Mesh cyl = BuildCylinderMesh(mb, _bolts);
            // one frame mesh per outboard side (the bracing faces away from the catwalk); the base-plate bolts are listed once
            Mesh framesPlus = BuildFramesMesh(mb, _bolts, 1, "GuideFramesBracedPlusZ");
            Mesh framesMinus = BuildFramesMesh(mb, null, -1, "GuideFramesBracedMinusZ");
            Mesh piers = BuildPiersMesh(mb);
            Mesh pit = BuildPitMesh(mb, _bolts);
            Mesh guard = BuildPitGuardMesh(mb);
            Mesh pipes = BuildBankPipesMesh(mb, _bolts);
            MeshKit.ChamferBox(mb, PIT_LAMP, new Vector3(0.32f, 0.16f, 0.32f), 0.01f);
            Mesh pitLens = BeatKit.Bake(mb, "PitLampLens", _meshes);
            EmitBolts(mb, _bolts);
            Mesh boltMesh = BeatKit.Bake(mb, "BankBolts", _meshes);
            MeshKit.HexBolt(mb, Vector3.zero, Vector3.up, 1f);
            Mesh unitBolt = BeatKit.Bake(mb, "UnitHexBolt", _meshes);

            for (int k = 0; k < 4; k++)
            {
                if (!_bankBuilt[k]) continue;
                bool a = AbyssConst.BANK_TYPE_A[k];
                Vector3 o = BankOrigin(k);
                Quaternion q = BankRotation(k);
                Transform bank = BeatKit.Group(root, "Bank" + k + (a ? "A" : "B"), o, q);

                _cylinders[k] = BeatKit.Renderer(bank, "Cylinder", cyl, machine, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                MeshRenderer framesR = BeatKit.Renderer(bank, "GuideFrames", OutboardSide(k) > 0 ? framesPlus : framesMinus, frameMat,
                    Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                BeatKit.Renderer(bank, "Piers", piers, conc, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                MeshRenderer pitR = BeatKit.Renderer(bank, "CrankPit", pit, pitMat, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                MeshRenderer guardR = BeatKit.Renderer(bank, "PitGuard", guard, thin, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                BeatKit.Renderer(bank, "Pipes", pipes, steel, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                BeatKit.Renderer(bank, "PitLampLens", pitLens, AmberLensMat(), Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);

                // near-detail tier around the bank (cylinder, frames and pit)
                Bounds bb = MeshBounds(_cylinders[k]);
                bb.Encapsulate(MeshBounds(framesR));
                bb.Encapsulate(MeshBounds(pitR));
                var tier = new DetailTier("Beat04Bank" + k, bb, DETAIL_NEAR, DETAIL_HYST, DETAIL_FADE);
                tier.AddNear(guardR);
                if (InnerBank(k))
                {
                    tier.AddNear(BeatKit.Renderer(bank, "Bolts", boltMesh, machine, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off));
                }
                else
                {
                    var batch = new InstancedBatch("Beat04Bolts" + k, unitBolt, machine, ShadowCastingMode.Off, true);
                    Matrix4x4 bm = BankMatrix(k);
                    for (int i = 0; i < _bolts.Count; i++) batch.Add(BoltMatrix(bm, _bolts[i]));
                    batch.Seal();
                    batch.Fade = 0.5f;   // allocate the fade block now, never during the walk
                    batch.Fade = 1f;
                    if (!_editMode) InstancedRegistry.Register(batch);
                    _bankBoltBatch[k] = batch;
                }
                _bankTier[k] = tier;

                // moving parts (world-space poses under an identity parent)
                float xc = X(CrankU(k)), zc = Z(AbyssConst.BANK_V[k]);
                _wheelR[k] = BeatKit.Renderer(moving, "Wheel" + k, wheel, machine, new Vector3(xc, WHEEL_PLANE_Y, zc), Quaternion.identity, ShadowCastingMode.Off);
                _wheel[k] = _wheelR[k].transform;
                _shaftR[k] = BeatKit.Renderer(_wheel[k], "CrankShaft", shaft, rodMat, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
                _rodR[k] = BeatKit.Renderer(moving, "ConnectingRod" + k, conrod, rodMat, new Vector3(xc, ROD_Y, zc), Quaternion.identity, ShadowCastingMode.Off);
                _rod[k] = _rodR[k].transform;
                _crossheadR[k] = BeatKit.Renderer(moving, "Crosshead" + k, crosshead, machine, new Vector3(xc, ROD_Y, zc), q, ShadowCastingMode.Off);
                _crosshead[k] = _crossheadR[k].transform;
                _pistonRodR[k] = BeatKit.Renderer(_crosshead[k], "PistonRod", xrods, rodMat, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
            }
            _flangeBolts = CYL_FLANGE_BOLTS;
        }

        /// <summary>World boxes of one bank's parts standing on the floor (piers, frame legs, pit), for the floor AO bake.</summary>
        static void CollectBankFloorBoxes(Matrix4x4 m, List<Bounds> dst)
        {
            for (int i = 0; i < PIER_S.Length; i++) dst.Add(BankBox(m, new Vector3(PIER_S[i], -12f, 0f), new Vector3(4.2f, 18f, 11.2f)));
            for (int i = 0; i < FRAME_S.Length; i++)
                for (int s = -1; s <= 1; s += 2)
                    dst.Add(BankBox(m, new Vector3(FRAME_S[i], -6f, s * FRAME_LEG_L), new Vector3(0.8f, 30f, 0.8f)));
            dst.Add(BankBox(m, new Vector3(0f, -19.75f, 0f), new Vector3(10f, 2.5f, 10f)));
            dst.Add(BankBox(m, new Vector3(0f, -19.75f, 0f), new Vector3(7.4f, 3.0f, 7.4f)));
        }

        static Bounds BankBox(Matrix4x4 m, Vector3 c, Vector3 size) => InstancedBatch.TransformBounds(m, new Bounds(c, size));

        // ================================================================================================ moving meshes

        /// <summary>Crank wheel in its own frame (origin at the wheel centre, world y = 1 + local y): pin direction = local +X.</summary>
        Mesh BuildWheelMesh(MeshBuilder mb)
        {
            float h = WHEEL_T * 0.5f, R = AbyssConst.WHEEL_D * 0.5f;
            // rim (ring, 8 cm chamfers)
            s_prof.Clear();
            P(RIM_R_IN + 0.06f, -h); P(R - 0.08f, -h); P(R, -h + 0.08f); P(R, h - 0.08f); P(R - 0.08f, h); P(RIM_R_IN + 0.06f, h);
            P(RIM_R_IN, h - 0.06f); P(RIM_R_IN, -h + 0.06f); P(RIM_R_IN + 0.06f, -h);
            MeshKit.Lathe(mb, s_prof, 128);
            // hub, slightly deeper below the rim, and its cap (top at local 2.4: 1.2 m under the rod)
            s_prof.Clear();
            P(HUB_R - 0.08f, -h - 0.6f); P(HUB_R, -h - 0.52f); P(HUB_R, h + 0.12f); P(HUB_R - 0.08f, h + 0.2f);
            MeshKit.Lathe(mb, s_prof, 48, MeshKit.HardEdgeAngleDeg, 0f, 360f, true);
            s_prof.Clear();
            P(2.0f, h + 0.1f); P(2.0f, h + 0.34f); P(1.94f, h + 0.4f);
            MeshKit.Lathe(mb, s_prof, 40, MeshKit.HardEdgeAngleDeg, 0f, 360f, true);
            // eight tapered spokes (spoke 0 points at the pin)
            MeshKit.RoundedRectangle(1.6f, 2.6f, 0.32f, 2, s_sec);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                s_frames.Clear();
                s_frames.Add(new LoftFrame(dir * (HUB_R - 0.15f), rot, Vector2.one));
                s_frames.Add(new LoftFrame(dir * (RIM_R_IN + 0.05f), rot, new Vector2(1.1f / 1.6f, 2.0f / 2.6f)));
                MeshKit.Loft(mb, s_sec, s_frames, true);
            }
            // counterweight opposite the pin (150..210 degrees, clear of the 135 / 225 spokes)
            s_prof.Clear();
            P(5.26f, -1.7f); P(RIM_R_IN + 0.3f, -1.7f); P(RIM_R_IN + 0.3f, 1.7f); P(5.26f, 1.7f); P(5.2f, 1.64f); P(5.2f, -1.64f); P(5.26f, -1.7f);
            MeshKit.Lathe(mb, s_prof, 24, MeshKit.HardEdgeAngleDeg, 150f, 210f, false, false, false, true);
            // crank boss under the pin
            s_prof.Clear();
            P(2.22f, -h + 0.05f); P(2.3f, -h + 0.13f); P(2.3f, h + 0.07f); P(2.22f, h + 0.15f);
            LatheAt(mb, new Vector3(AbyssConst.CRANK_R, 0f, 0f), Vector3.up, 40, true);
            // hub bolts
            var bolts = new List<BoltSpec>(8);
            BoltRing(bolts, new Vector3(0f, h + 0.2f, 0f), Vector3.up, 2.75f, 8, 0.2f, 22.5f);
            EmitBolts(mb, bolts);
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "CrankWheel", _meshes);
        }

        /// <summary>Pin (O2.5 at r 12) with its nut, crankshaft (O3) down to the pit cap and its bolted coupling, wheel frame.</summary>
        Mesh BuildShaftMesh(MeshBuilder mb)
        {
            float h = WHEEL_T * 0.5f;
            float eyeTop = ROD_Y + ROD_EYE_H * 0.5f - WHEEL_PLANE_Y;   // 4.4: top of the rod's big eye in wheel coordinates
            s_prof.Clear();
            P(PIN_D * 0.5f, h + 0.05f); P(PIN_D * 0.5f, eyeTop + 0.05f); P(1.55f, eyeTop + 0.05f); P(1.6f, eyeTop + 0.1f);
            P(1.6f, eyeTop + 0.4f); P(1.55f, eyeTop + 0.45f);
            LatheAt(mb, new Vector3(AbyssConst.CRANK_R, 0f, 0f), Vector3.up, 32, true);
            float yPit = AbyssConst.HALL_FLOOR_Y + PIT_H + 1f - WHEEL_PLANE_Y;   // pit cap top (-18) in wheel coordinates
            s_prof.Clear();
            P(CRANKSHAFT_D * 0.5f, yPit - 0.1f); P(CRANKSHAFT_D * 0.5f, -h - 0.5f);
            LatheAt(mb, Vector3.zero, Vector3.up, 48, true);
            float yc = -10f;
            FlangeDisc(mb, new Vector3(0f, yc, 0f), Vector3.up, 2.6f, 0.8f, 0.05f, 48);
            FlangeDisc(mb, new Vector3(0f, yPit + 1.2f, 0f), Vector3.up, 2.0f, 0.5f, 0.04f, 40);
            var bolts = new List<BoltSpec>(12);
            BoltRing(bolts, new Vector3(0f, yc + 0.4f, 0f), Vector3.up, 2.1f, 12, 0.16f, 15f);
            EmitBolts(mb, bolts);
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "CrankShaftAndPin", _meshes);
        }

        /// <summary>Connecting rod in its own frame: origin on the pin axis at the rod height, +Z toward the crosshead pin (32 m).</summary>
        Mesh BuildRodMesh(MeshBuilder mb)
        {
            float L = AbyssConst.CRANK_L, eh = ROD_EYE_H * 0.5f, sh = ROD_SMALL_EYE_H * 0.5f;
            float pinR = PIN_D * 0.5f + 0.02f;
            s_prof.Clear();
            P(pinR, -eh); P(ROD_EYE_R - 0.06f, -eh); P(ROD_EYE_R, -eh + 0.06f); P(ROD_EYE_R, eh - 0.06f); P(ROD_EYE_R - 0.06f, eh); P(pinR, eh); P(pinR, -eh);
            MeshKit.Lathe(mb, s_prof, 40);
            s_prof.Clear();
            P(0.52f, -sh); P(ROD_SMALL_EYE_R - 0.04f, -sh); P(ROD_SMALL_EYE_R, -sh + 0.04f); P(ROD_SMALL_EYE_R, sh - 0.04f);
            P(ROD_SMALL_EYE_R - 0.04f, sh); P(0.52f, sh); P(0.52f, -sh);
            LatheAt(mb, new Vector3(0f, 0f, L), Vector3.up, 32, false);
            MeshKit.RoundedRectangle(1.1f, 1.5f, 0.25f, 2, s_sec);
            s_frames.Clear();
            s_frames.Add(new LoftFrame(new Vector3(0f, 0f, ROD_EYE_R - 0.25f), Quaternion.identity, Vector2.one));
            s_frames.Add(new LoftFrame(new Vector3(0f, 0f, L - ROD_SMALL_EYE_R + 0.25f), Quaternion.identity, new Vector2(0.7f / 1.1f, 0.9f / 1.5f)));
            MeshKit.Loft(mb, s_sec, s_frames, true);
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "ConnectingRod", _meshes);
        }

        /// <summary>Crosshead body (4 x 3 x 3 envelope) in its own frame: origin at the crosshead pin, +X toward the cylinder.</summary>
        Mesh BuildCrossheadMesh(MeshBuilder mb)
        {
            float hl = CROSSHEAD_L * 0.5f, hh = CROSSHEAD_H * 0.5f, w = CROSSHEAD_W - 0.1f;
            float front0 = ROD_SMALL_EYE_R + 0.1f;                          // the small eye swings clear of the solid front
            float slot = ROD_SMALL_EYE_H * 0.5f + 0.05f;                     // fork slot half height
            MeshKit.ChamferBox(mb, new Vector3((front0 + hl) * 0.5f, 0f, 0f), new Vector3(hl - front0, CROSSHEAD_H, w), 0.03f);
            float cheekH = hh - slot, cheekL = hl + front0 + 0.1f;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(-hl + cheekL * 0.5f, s * (slot + cheekH * 0.5f), 0f), new Vector3(cheekL, cheekH, w), 0.03f);
            var bolts = new List<BoltSpec>(8);
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 2; i++)
                    for (int j = -1; j <= 1; j += 2)
                        bolts.Add(new BoltSpec(new Vector3(i == 0 ? -1.3f : 0.4f, j * (slot + cheekH * 0.5f), s * w * 0.5f), new Vector3(0f, 0f, s), 0.12f, 0f));
            EmitBolts(mb, bolts);
            return BeatKit.Bake(mb, "Crosshead", _meshes);
        }

        /// <summary>Crosshead pin (in the fork slot), collar and the O1.6 piston rod to the piston 27 m from the crosshead front.</summary>
        Mesh BuildCrossheadRodsMesh(MeshBuilder mb)
        {
            float hl = CROSSHEAD_L * 0.5f, slot = ROD_SMALL_EYE_H * 0.5f + 0.05f;
            s_prof.Clear();
            P(0.5f, -slot - 0.1f); P(0.5f, slot + 0.1f);
            LatheAt(mb, Vector3.zero, Vector3.up, 24, true);
            s_prof.Clear();
            P(1.0f, hl - 0.05f); P(1.05f, hl); P(1.05f, hl + 0.3f); P(1.0f, hl + 0.35f);
            LatheAt(mb, Vector3.zero, Vector3.right, 40, true);
            s_prof.Clear();
            P(AbyssConst.PISTON_ROD * 0.5f, hl + 0.3f); P(AbyssConst.PISTON_ROD * 0.5f, hl + PISTON_ROD_L);
            LatheAt(mb, Vector3.zero, Vector3.right, 32, true);
            return BeatKit.Bake(mb, "CrossheadPinAndPistonRod", _meshes);
        }

        // ================================================================================================ static meshes

        /// <summary>Cylinder O12 x 28 from s 48 to 76 with flanged heads, gland and bands; records the measured bore and length.</summary>
        Mesh BuildCylinderMesh(MeshBuilder mb, List<BoltSpec> bolts)
        {
            float R = AbyssConst.PISTON_BORE * 0.5f, L = CYL_LEN;
            mb.Push(new Vector3(CYL_S0, ROD_Y, 0f), Quaternion.FromToRotation(Vector3.up, Vector3.right));
            // rear head with the gland (profile: radius, axial distance from the rear face)
            s_prof.Clear();
            P(1.54f, -0.6f); P(1.6f, -0.54f); P(1.6f, 0f); P(5.52f, 0f); P(5.6f, 0.08f); P(5.6f, 0.35f); P(6.94f, 0.35f); P(7f, 0.41f);
            P(7f, 0.95f); P(6.9f, 1f); P(7f, 1.05f); P(7f, 1.36f); P(6.94f, 1.42f); P(R, 1.42f);
            MeshKit.Lathe(mb, s_prof, 64, MeshKit.HardEdgeAngleDeg, 0f, 360f, true);
            // barrel with three stiffening bands
            int barrel = mb.VertexCount;
            s_prof.Clear();
            P(R, 1.42f);
            for (int i = 1; i <= 3; i++)
            {
                float c = 1.42f + (L - 2.84f) * i / 4f;
                P(R, c - 0.35f); P(R + 0.13f, c - 0.35f); P(R + 0.18f, c - 0.3f); P(R + 0.18f, c + 0.3f); P(R + 0.13f, c + 0.35f); P(R, c + 0.35f);
            }
            P(R, L - 1.42f);
            MeshKit.Lathe(mb, s_prof, 64);
            int barrelEnd = mb.VertexCount;
            // front head
            s_prof.Clear();
            P(R, L - 1.42f); P(6.94f, L - 1.42f); P(7f, L - 1.36f); P(7f, L - 1.05f); P(6.9f, L - 1f); P(7f, L - 0.95f); P(7f, L - 0.41f);
            P(6.94f, L - 0.35f); P(5.6f, L - 0.35f); P(5.6f, L - 0.08f); P(5.52f, L);
            MeshKit.Lathe(mb, s_prof, 64, MeshKit.HardEdgeAngleDeg, 0f, 360f, true);
            mb.Pop();

            // measurements on the generated vertices: bore = barrel diameter between the head and the first band, length = heads
            float rMax = 0f, xMin = float.MaxValue, xMax = float.MinValue;
            for (int i = barrel; i < barrelEnd; i++)
            {
                Vector3 p = mb.Positions[i];
                if (p.x < CYL_S0 + 1f || p.x > CYL_S0 + 2f) continue;   // the plain barrel ring next to the rear head (a = 1.42)
                rMax = Mathf.Max(rMax, Mathf.Sqrt((p.y - ROD_Y) * (p.y - ROD_Y) + p.z * p.z));
            }
            for (int i = 0; i < mb.VertexCount; i++)
            {
                float x = mb.Positions[i].x;
                if (x < CYL_S0 - 1e-3f) continue;   // the gland is not part of the 28 m
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
            }
            _measuredBore = 2f * rMax;
            _measuredCylLength = xMax - xMin;

            // 24 bolts per flange face, the gland nuts
            BoltRing(bolts, new Vector3(CYL_S0 + 0.35f, ROD_Y, 0f), Vector3.left, 6.27f, CYL_FLANGE_BOLTS, CYL_BOLT_AF, 7.5f);
            BoltRing(bolts, new Vector3(CYL_S0 + L - 0.35f, ROD_Y, 0f), Vector3.right, 6.27f, CYL_FLANGE_BOLTS, CYL_BOLT_AF, 7.5f);
            BoltRing(bolts, new Vector3(CYL_S0 - 0.6f, ROD_Y, 0f), Vector3.left, 1.22f, 6, 0.1f, 0f);
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "PistonCylinder", _meshes);
        }

        /// <summary>
        /// Guide bars above and below the crosshead, the two portal frames carrying them, one X-brace in the outboard leg plane
        /// (bank-frame lateral side <paramref name="braceSide"/>), base plates (their bolts go to <paramref name="bolts"/> when
        /// given), cylinder saddles.
        /// </summary>
        Mesh BuildFramesMesh(MeshBuilder mb, List<BoltSpec> bolts, int braceSide, string name)
        {
            float yf = AbyssConst.HALL_FLOOR_Y, hh = CROSSHEAD_H * 0.5f;
            float upperY = ROD_Y + hh + GUIDE_GAP + GUIDE_T * 0.5f, lowerY = ROD_Y - hh - GUIDE_GAP - GUIDE_T * 0.5f;
            float upperTop = upperY + GUIDE_T * 0.5f, lowerBottom = lowerY - GUIDE_T * 0.5f;
            float beamY0 = upperTop + 0.48f;    // upper crossbeam bottom (hangers bridge the 0.48 m)
            for (int i = 0; i < FRAME_S.Length; i++)
            {
                float s = FRAME_S[i];
                for (int l = -1; l <= 1; l += 2)
                {
                    MeshKit.ChamferBox(mb, new Vector3(s, (yf - 0.05f + FRAME_TOP) * 0.5f, l * FRAME_LEG_L), new Vector3(0.8f, FRAME_TOP - yf + 0.05f, 0.8f), 0.03f);
                    MeshKit.ChamferBox(mb, new Vector3(s, yf + 0.05f, l * FRAME_LEG_L), new Vector3(1.4f, 0.12f, 1.4f), 0.01f);
                    for (int b = 0; b < 4 && bolts != null; b++)
                        bolts.Add(new BoltSpec(new Vector3(s + ((b & 1) == 0 ? -0.5f : 0.5f), yf + 0.11f, l * FRAME_LEG_L + ((b & 2) == 0 ? -0.5f : 0.5f)),
                            Vector3.up, 0.09f, 15f * b));
                    // upper knee braces (above the rod band) and lower knee braces (below the lower crossbeam)
                    MeshKit.ChamferMember(mb, new Vector3(s, beamY0 - 1.3f, l * (FRAME_LEG_L - 0.4f)), new Vector3(s, beamY0 + 0.05f, l * (FRAME_LEG_L - 1.8f)),
                        0.4f, 0.4f, 0.02f, Vector3.right);
                    MeshKit.ChamferMember(mb, new Vector3(s, lowerBottom - 2.4f, l * (FRAME_LEG_L - 0.4f)), new Vector3(s, lowerBottom - 0.6f, l * (FRAME_LEG_L - 1.9f)),
                        0.4f, 0.4f, 0.02f, Vector3.right);
                }
                MeshKit.ChamferBox(mb, new Vector3(s, beamY0 + 0.6f, 0f), new Vector3(0.8f, 1.2f, 2f * FRAME_LEG_L + 1.6f), 0.03f);
                MeshKit.ChamferBox(mb, new Vector3(s, lowerBottom - 0.6f + 0.06f, 0f), new Vector3(0.8f, 1.3f, 2f * FRAME_LEG_L - 0.4f), 0.03f);
                for (int l = -1; l <= 1; l += 2)
                    MeshKit.ChamferBox(mb, new Vector3(s, (upperTop + beamY0) * 0.5f, l * 1.1f), new Vector3(0.6f, beamY0 - upperTop + 0.12f, 0.5f), 0.02f);
            }
            float s0 = FRAME_S[0], s1 = FRAME_S[FRAME_S.Length - 1];
            for (int l = -1; l <= 1; l += 2)
            {
                // top ties along the frames (both leg planes)
                MeshKit.ChamferBox(mb, new Vector3((s0 + s1) * 0.5f, FRAME_TOP - 0.3f, l * FRAME_LEG_L), new Vector3(s1 - s0 - 0.2f, 0.5f, 0.5f), 0.02f);
                if (l != braceSide) continue;
                // X-bracing only in the outboard plane (the two diagonals offset 0.1 m: never co-planar)
                for (int i = 0; i + 1 < FRAME_S.Length; i++)
                {
                    float sa = FRAME_S[i] + 0.4f, sb = FRAME_S[i + 1] - 0.4f;
                    MeshKit.ChamferMember(mb, new Vector3(sa, yf + 1.5f, l * (FRAME_LEG_L - 0.1f)), new Vector3(sb, -2.5f, l * (FRAME_LEG_L - 0.1f)),
                        0.3f, 0.3f, 0.02f, Vector3.up);
                    MeshKit.ChamferMember(mb, new Vector3(sa, -2.5f, l * (FRAME_LEG_L + 0.1f)), new Vector3(sb, yf + 1.5f, l * (FRAME_LEG_L + 0.1f)),
                        0.3f, 0.3f, 0.02f, Vector3.up);
                }
            }
            // guide bars (the crosshead slides between them with 2 cm clearance)
            float gm = (GUIDE_S0 + GUIDE_S1) * 0.5f, gl = GUIDE_S1 - GUIDE_S0;
            MeshKit.ChamferBox(mb, new Vector3(gm, upperY, 0f), new Vector3(gl, GUIDE_T, GUIDE_W), 0.03f);
            MeshKit.ChamferBox(mb, new Vector3(gm, lowerY, 0f), new Vector3(gl, GUIDE_T, GUIDE_W), 0.03f);
            // cylinder saddles: block 0.5 m into the barrel bottom and two cheeks
            for (int i = 0; i < PIER_S.Length; i++)
            {
                float s = PIER_S[i];
                MeshKit.ChamferBox(mb, new Vector3(s, -2f, 0f), new Vector3(3.4f, 2f, 11f), 0.03f);
                for (int l = -1; l <= 1; l += 2)
                    MeshKit.ChamferBox(mb, new Vector3(s, 0.6f, l * 5.95f), new Vector3(3.4f, 3.2f, 0.7f), 0.03f);
            }
            return BeatKit.Bake(mb, name, _meshes);
        }

        /// <summary>Two concrete piers under the cylinder saddles, with a wider plinth at the floor; AO from the floor and saddles.</summary>
        Mesh BuildPiersMesh(MeshBuilder mb)
        {
            float yf = AbyssConst.HALL_FLOOR_Y;
            _occ.Clear();
            for (int i = 0; i < PIER_S.Length; i++)
            {
                float s = PIER_S[i];
                MeshKit.ChamferBox(mb, new Vector3(s, (yf - 0.05f - 2.95f) * 0.5f, 0f), new Vector3(3f, -2.95f - yf + 0.05f, 10f), 0.05f);
                MeshKit.ChamferBox(mb, new Vector3(s, yf + 0.55f, 0f), new Vector3(4.2f, 1.2f, 11.2f), 0.05f);
                _occ.Add(new Bounds(new Vector3(s, -2f, 0f), new Vector3(3.4f, 2f, 11f)));
            }
            _occ.Add(new Bounds(new Vector3(60f, yf - 10f, 0f), new Vector3(200f, 20f, 200f)));
            VertexBake.BakeAO(mb, _occ, 4f, 16, _seed ^ 0x91E5u, 0.8f, 0);
            return BeatKit.Bake(mb, "CylinderPiers", _meshes);
        }

        /// <summary>Crank pit: steel floor plate, bearing housing under the crankshaft, hazard lamp post and housing (weathered steel).</summary>
        Mesh BuildPitMesh(MeshBuilder mb, List<BoltSpec> bolts)
        {
            float yf = AbyssConst.HALL_FLOOR_Y;
            MeshKit.ChamferBox(mb, new Vector3(0f, yf + 0.01f, 0f), new Vector3(12f, 0.08f, 12f), 0.01f);
            s_prof.Clear();
            P(4.92f, yf + 0.04f); P(5f, yf + 0.12f); P(5f, yf + 0.5f); P(3.7f, yf + 0.5f); P(3.7f, yf + 2.3f); P(2.35f, yf + 2.3f);
            P(2.35f, yf + 2.95f); P(2.3f, yf + 3f);
            LatheAt(mb, Vector3.zero, Vector3.up, 48, true);
            BoltRing(bolts, new Vector3(0f, yf + 0.5f, 0f), Vector3.up, 4.4f, 16, 0.14f, 11.25f);
            BoltRing(bolts, new Vector3(0f, yf + 2.3f, 0f), Vector3.up, 3.0f, 8, 0.12f, 22.5f);
            MeshKit.ChamferBox(mb, new Vector3(PIT_LAMP.x, (yf + 0.05f + PIT_LAMP.y - 0.15f) * 0.5f, PIT_LAMP.z), new Vector3(0.14f, PIT_LAMP.y - 0.15f - yf - 0.05f, 0.14f), 0.01f);
            MeshKit.ChamferBox(mb, PIT_LAMP, new Vector3(0.3f, 0.3f, 0.3f), 0.02f);
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "CrankPit", _meshes);
        }

        /// <summary>Guard ring around the pit: eight O0.06 posts and two rings at 0.55 and 1.07 m (ThinLine steel).</summary>
        Mesh BuildPitGuardMesh(MeshBuilder mb)
        {
            float yf = AbyssConst.HALL_FLOOR_Y + 0.05f, R = 6.4f;
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
                var p = new Vector3(R * Mathf.Cos(a), 0f, R * Mathf.Sin(a));
                MeshKit.Cylinder(mb, p + Vector3.up * (yf - 0.04f), p + Vector3.up * (yf + 1.07f), 0.03f, 8, true, true);
            }
            MeshKit.Torus(mb, new Vector3(0f, yf + 1.07f, 0f), Vector3.up, R, 0.04f, 96, 8);
            MeshKit.Torus(mb, new Vector3(0f, yf + 0.55f, 0f), Vector3.up, R, 0.035f, 96, 8);
            return BeatKit.Bake(mb, "PitGuard", _meshes);
        }

        /// <summary>Two O2.5 pipes from bosses on the barrel top to the ceiling, flanges every 6 m with 12 bolts each.</summary>
        Mesh BuildBankPipesMesh(MeshBuilder mb, List<BoltSpec> bolts)
        {
            float top = ROD_Y + AbyssConst.PISTON_BORE * 0.5f;   // barrel top, 10.5
            for (int i = 0; i < PIPE_S.Length; i++)
            {
                var c = new Vector3(PIPE_S[i], 0f, 0f);
                s_prof.Clear();
                P(1.65f, top - 0.5f); P(1.65f, top + 0.45f); P(1.6f, top + 0.5f);
                LatheAt(mb, c, Vector3.up, 32, true);
                s_prof.Clear();
                P(PIPE_R, top + 0.4f); P(PIPE_R, CEILING_Y + 0.1f);
                LatheAt(mb, c, Vector3.up, 32, true);
                for (int f = 0; f < PIPE_FLANGE_Y.Length; f++)
                {
                    float y = PIPE_FLANGE_Y[f];
                    FlangeDisc(mb, c + Vector3.up * y, Vector3.up, 1.6f, 0.3f, 0.03f, 40);
                    BoltRing(bolts, c + Vector3.up * (y + 0.15f), Vector3.up, 1.43f, 12, PIPE_BOLT_AF, 15f);
                }
            }
            VertexBake.EdgeWearFromNormals(mb);
            return BeatKit.Bake(mb, "CylinderPipes", _meshes);
        }
    }
}
