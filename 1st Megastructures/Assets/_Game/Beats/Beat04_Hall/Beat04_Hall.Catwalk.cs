using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Catwalk of beat 4 (14.5.4): 1.5 x 110 m grating at y -7 (top), real bearer bars every 0.6 m and two edge stringers under
    /// it, rails 1.1 m of O0.05 round tube (top and mid rail, posts every 2.0 m, ThinLine), kick plates, carried by 14 m
    /// posts from the floor every 5 m at v = +/-1.0 on cross beams (no hangers). Dressing at human scale: four valve stations
    /// beside the rail (handwheels O1.2 at hip height, 0.4 m outside the rail) fed from two O2.5 floor pipes along the hall
    /// with flanges every 12 m (16 bolts O0.085 each, merged: within 25 m of the path).
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        const float RAIL_V = 0.725f;
        const float BEARER_SPACING = 0.6f;
        static readonly float[] VALVE_U = { 17.5f, 42.5f, 67.5f, 92.5f };
        const float VALVE_V = 1.75f, VALVE_WHEEL_R = 0.6f;
        const float FLOOR_PIPE_V = 4.5f, FLOOR_PIPE_R = 1.25f;
        static float FloorPipeY => AbyssConst.HALL_FLOOR_Y + 0.3f + FLOOR_PIPE_R;

        void BuildCatwalkFull(BeatContext ctx, Transform interior)
        {
            Transform cw = BeatKit.Group(interior, "Catwalk");
            Material steel = Nokey(MatId.SteelPainted), thin = ThinMat(), machine = MachineMat();
            var mb = new MeshBuilder();
            float y = AbyssConst.CATWALK_Y, zc = AbyssConst.W[8].z, hw = AbyssConst.CATWALK_W * 0.5f;
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, len = x0 - x1, xm = (x0 + x1) * 0.5f;
            float yf = AbyssConst.HALL_FLOOR_Y;

            // grating deck (top exactly at y -7, width exactly 1.5)
            Box(mb, xm, y - CATWALK_GRATING_T * 0.5f, zc, len, CATWALK_GRATING_T, AbyssConst.CATWALK_W, 0.005f);
            _catwalk = BeatKit.Emit(mb, cw, "CatwalkGrating", Nokey(MatId.Grating), _meshes, ShadowCastingMode.Off);

            // rails: round O0.05 tubes, posts every 2 m, kick plates (12 mm, 0.12 high, 2 cm into the grating)
            float rr = RAIL_TUBE * 0.5f, rh = AbyssConst.RAIL_H;
            int posts = Mathf.RoundToInt(len / POST_SPACING);
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zc + s * RAIL_V;
                RailTube(mb, x0, x1, y + rh - rr, z, rr, posts);
                RailTube(mb, x0, x1, y + rh * 0.5f, z, rr, posts);
                for (int p = 0; p <= posts; p++)
                {
                    float x = x0 - POST_SPACING * p;
                    MeshKit.Cylinder(mb, new Vector3(x, y - 0.02f, z), new Vector3(x, y + rh - rr, z), rr, 8, true, true);
                }
                MeshKit.ChamferBox(mb, new Vector3(xm, y + 0.04f, zc + s * (hw - 0.006f)), new Vector3(len, 0.12f, 0.012f), 0.002f);
            }
            _catwalkRails = BeatKit.Emit(mb, cw, "CatwalkRails", thin, _meshes, ShadowCastingMode.Off);

            // under the grating: bearer bars every 0.6 m (tops 15 mm below the grating) and the two edge stringers
            for (float x = x0 - 0.3f; x > x1 + 0.2f; x -= BEARER_SPACING)
                MeshKit.ChamferBox(mb, new Vector3(x, y - 0.0775f, zc), new Vector3(0.05f, 0.025f, AbyssConst.CATWALK_W - 0.04f), 0.004f);
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(xm, y - 0.18f, zc + s * (hw - 0.05f)), new Vector3(len, 0.25f, 0.08f), 0.01f);
            BeatKit.Emit(mb, cw, "CatwalkDeckSteel", steel, _meshes, ShadowCastingMode.Off);

            // 14 m support posts every 5 m at v = +/-1.0, cross beams under the stringers, base plates with anchors
            var bolts = new List<BoltSpec>(96);
            int supports = Mathf.RoundToInt(len / SUPPORT_SPACING);
            float beamTop = y - 0.30f, beamH = 0.24f;
            for (int p = 1; p < supports; p++)
            {
                float x = x0 - SUPPORT_SPACING * p;
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = zc + s * SUPPORT_V;
                    float top = beamTop - beamH + 0.1f;
                    MeshKit.ChamferBox(mb, new Vector3(x, (yf - 0.05f + top) * 0.5f, z), new Vector3(SUPPORT_SECTION, top - yf + 0.05f, SUPPORT_SECTION), 0.02f);
                    MeshKit.ChamferBox(mb, new Vector3(x, yf + 0.02f, z), new Vector3(0.6f, 0.06f, 0.6f), 0.01f);
                    for (int b = 0; b < 4; b++)
                        bolts.Add(new BoltSpec(new Vector3(x + ((b & 1) == 0 ? -0.2f : 0.2f), yf + 0.05f, z + ((b & 2) == 0 ? -0.2f : 0.2f)), Vector3.up, 0.05f, 10f * b));
                }
                MeshKit.ChamferBox(mb, new Vector3(x, beamTop - beamH * 0.5f, zc), new Vector3(0.25f, beamH, 2f * SUPPORT_V + 0.4f), 0.02f);
            }
            EmitBolts(mb, bolts);
            _catwalkPosts = BeatKit.Emit(mb, cw, "CatwalkSupports", steel, _meshes, ShadowCastingMode.Off);

            // floor pipes, valve stations
            bolts.Clear();
            BuildFloorPipes(mb, bolts);
            for (int i = 0; i < VALVE_U.Length; i++) ValveBody(mb, X(VALVE_U[i]), zc + ((i & 1) == 0 ? VALVE_V : -VALVE_V));
            EmitBolts(mb, bolts);
            VertexBake.EdgeWearFromNormals(mb);
            BeatKit.Emit(mb, cw, "PipesAndValves", steel, _meshes, ShadowCastingMode.Off);
            for (int i = 0; i < VALVE_U.Length; i++) Handwheel(mb, X(VALVE_U[i]), zc + ((i & 1) == 0 ? VALVE_V : -VALVE_V));
            BeatKit.Emit(mb, cw, "ValveHandwheels", machine, _meshes, ShadowCastingMode.Off);
        }

        /// <summary>
        /// One rail tube from x0 to x1 as 'pieces' collinear ThinLine cylinders (one per 2 m post bay). The ThinLine widening
        /// and its alpha-to-coverage are evaluated per vertex from the camera distance (14.7.9); a single 110 m cylinder has
        /// vertices only at its ends, so the far end's widening (e about 4 cm at 80 m) and coverage (about 0.4) were
        /// interpolated onto the part beside the eye: the near rail read as a thick translucent band in KS4. Only the two
        /// outer pieces carry caps (closing the rail ends at the hull faces); their inner caps lie inside the neighbouring
        /// uncapped tube, so no two faces share a plane.
        /// </summary>
        static void RailTube(MeshBuilder mb, float x0, float x1, float y, float z, float r, int pieces)
        {
            for (int i = 0; i < pieces; i++)
            {
                float xa = Mathf.Lerp(x0, x1, (float)i / pieces), xb = Mathf.Lerp(x0, x1, (float)(i + 1) / pieces);
                bool caps = i == 0 || i == pieces - 1;
                MeshKit.Cylinder(mb, new Vector3(xa, y, z), new Vector3(xb, y, z), r, 10, caps, true);
            }
        }

        /// <summary>Two O2.5 pipes along the hall at v = +/-4.5 on saddles every 6 m, flanges every 12 m with 16 bolts each.</summary>
        void BuildFloorPipes(MeshBuilder mb, List<BoltSpec> bolts)
        {
            float x0 = AbyssConst.HALL_X0 - 0.6f, x1 = AbyssConst.HALL_X1 + 0.6f, yp = FloorPipeY, yf = AbyssConst.HALL_FLOOR_Y;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = AbyssConst.W[8].z + s * FLOOR_PIPE_V;
                s_prof.Clear();
                P(FLOOR_PIPE_R, 0f); P(FLOOR_PIPE_R, x0 - x1);
                LatheAt(mb, new Vector3(x1, yp, z), Vector3.right, 40, true);
                for (float x = AbyssConst.HALL_X0 - 4f; x > x1 + 1f; x -= 6f)
                    MeshKit.ChamferBox(mb, new Vector3(x, yf + 0.24f, z), new Vector3(0.45f, 0.58f, 2.0f), 0.02f);
                for (float x = AbyssConst.HALL_X0 - 7f; x > x1 + 2f; x -= 12f)
                {
                    FlangeDisc(mb, new Vector3(x, yp, z), Vector3.right, 1.55f, 0.3f, 0.03f, 40);
                    BoltRing(bolts, new Vector3(x + 0.15f, yp, z), Vector3.right, 1.42f, 16, PIPE_BOLT_AF, 11.25f);
                }
            }
        }

        /// <summary>Valve station: branch from the floor pipe, riser, gate valve body under the deck level, bonnet and stem.</summary>
        void ValveBody(MeshBuilder mb, float x, float z)
        {
            float zc = AbyssConst.W[8].z, yp = FloorPipeY, side = Mathf.Sign(z - zc);
            float zPipe = zc + side * (FLOOR_PIPE_V - FLOOR_PIPE_R + 0.3f);
            float bodyY = AbyssConst.CATWALK_Y - 0.5f, wheelY = AbyssConst.CATWALK_Y + 1.0f;
            MeshKit.Sphere(mb, new Vector3(x, yp, z), 0.3f, 16, 8);
            MeshKit.Cylinder(mb, new Vector3(x, yp, z), new Vector3(x, yp, zPipe), 0.25f, 16, true);
            MeshKit.Cylinder(mb, new Vector3(x, yp, z), new Vector3(x, bodyY - 0.3f, z), 0.25f, 16, true);
            FlangeDisc(mb, new Vector3(x, bodyY - 0.36f, z), Vector3.up, 0.42f, 0.1f, 0.01f, 24);
            MeshKit.ChamferBox(mb, new Vector3(x, bodyY, z), new Vector3(0.7f, 0.6f, 0.7f), 0.02f);
            FlangeDisc(mb, new Vector3(x, bodyY + 0.36f, z), Vector3.up, 0.32f, 0.1f, 0.01f, 24);
            MeshKit.Cylinder(mb, new Vector3(x, bodyY + 0.3f, z), new Vector3(x, wheelY - 0.12f, z), 0.16f, 16, true);
            MeshKit.Cylinder(mb, new Vector3(x, wheelY - 0.15f, z), new Vector3(x, wheelY + 0.08f, z), 0.04f, 8, true);
        }

        /// <summary>Handwheel O1.2 (scale cue) at hip height: rim torus, hub and five spokes.</summary>
        static void Handwheel(MeshBuilder mb, float x, float z)
        {
            float wy = AbyssConst.CATWALK_Y + 1.0f, minor = 0.035f, major = VALVE_WHEEL_R - minor;
            var c = new Vector3(x, wy, z);
            MeshKit.Torus(mb, c, Vector3.up, major, minor, 48, 8);
            s_prof.Clear();
            P(0.075f, -0.06f); P(0.075f, 0.06f);
            LatheAt(mb, c, Vector3.up, 16, true);
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + 72f * i) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                MeshKit.ChamferMember(mb, c + d * 0.06f, c + d * (major - 0.01f), 0.04f, 0.03f, 0.005f, Vector3.up);
            }
        }

        /// <summary>World boxes of the catwalk parts standing on the floor (posts, pipes, valve risers), for the floor AO bake.</summary>
        static void CollectCatwalkFloorBoxes(List<Bounds> dst)
        {
            float zc = AbyssConst.W[8].z, x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, yf = AbyssConst.HALL_FLOOR_Y;
            int supports = Mathf.RoundToInt((x0 - x1) / SUPPORT_SPACING);
            for (int p = 1; p < supports; p++)
                for (int s = -1; s <= 1; s += 2)
                    dst.Add(Bx(x0 - SUPPORT_SPACING * p, yf + 7f, zc + s * SUPPORT_V, SUPPORT_SECTION, 14f, SUPPORT_SECTION));
            for (int s = -1; s <= 1; s += 2)
                dst.Add(Bx((x0 + x1) * 0.5f, FloorPipeY, zc + s * FLOOR_PIPE_V, x0 - x1, 2f * FLOOR_PIPE_R, 2f * FLOOR_PIPE_R));
            for (int i = 0; i < VALVE_U.Length; i++)
                dst.Add(Bx(X(VALVE_U[i]), yf + 7f, zc + ((i & 1) == 0 ? VALVE_V : -VALVE_V), 0.5f, 14f, 0.5f));
        }
    }
}
