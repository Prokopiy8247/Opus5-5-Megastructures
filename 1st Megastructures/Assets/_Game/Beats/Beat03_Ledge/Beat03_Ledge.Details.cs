using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 3 near detail (human-scale cues, 14.5.3 Geometry and Scale cues): amber lamps, the closed hatch, the cold spot
    /// cans, the bridge landing plate, the expansion-joint cover plates, the column-foot conduit, drains and scuppers. All of
    /// it lies within 25 m of the path, so repeats (bolts, clips) are merged into per-group meshes (14.5 rules) and the groups
    /// switch with one <see cref="DetailTier"/> by camera distance; the lamp globes stay in the persistent tier.
    /// </summary>
    public sealed partial class Beat03_Ledge
    {
        // hatch, in a local frame on the column face at alpha 1.85: u along +tangent, v up from the floor at the hatch centre,
        // w outward from the column face (r 22)
        const float HATCH_SILL_TOP = 0.03f;            // level sill: the ramp falls 5.8 % across the hatch
        const float HATCH_GAP = 0.006f;                // leaf-to-frame gap at the sides
        const float HATCH_JAMB_W = 0.1f;
        const float HATCH_OPEN_HALF = 0.5f * AbyssConst.HATCH_W + HATCH_GAP;   // 0.456
        const float HATCH_OPEN_H = AbyssConst.HATCH_H + 0.006f;
        static readonly float[] HATCH_HINGE_V = { 0.32f, 1.68f };

        // conduit along the column foot, feeding the lamps (interrupted either side of the hatch)
        const float CONDUIT_R = 22.075f, CONDUIT_H = 0.32f, CONDUIT_RADIUS = 0.022f, CONDUIT_BOX_R = 22.055f;
        const double CONDUIT_A0 = 0.18, CONDUIT_A1 = 3.08;
        const float CLIP_SPACING = 1.1f;

        // drains at the kerb and scuppers through the fascia
        static readonly float[] DRAIN_ALPHA = { 0.35f, 0.8f, 1.25f, 1.85f, 2.45f, 2.85f };

        static readonly CrossSection s_tube = new CrossSection();

        static Material ThinSteel(MaterialLibrary mats) =>
            mats.Variant(MatId.SteelPainted, "b3thin", m => m.SetFloat(ShaderIds.ThinLine, AbyssConst.MIN_LINE_PX));

        static Vector3 HP(Vector3 o, Vector3 tan, Vector3 rad, float u, float v, float w) => o + tan * u + Vector3.up * v + rad * w;

        // ================================================================================================ lamps

        /// <summary>
        /// Amber lamps at alpha 0.6, 1.4, 2.2, 2.9 (Simplified: 0.6 and 2.2): a bolted base plate tilted with the ramp, a
        /// tapered pole inboard at r 22.25, a 1.5 m arm out to r 23.7 carried by a knee brace from a clamp collar, a stem and cup
        /// holding the O 0.5 globe 2.6 m above the floor. The brace's underside stays 0.29 m above the eye wherever a camera can
        /// reach (r 22.75 and out: column wall collider r 22.4 + capsule 0.35); the arm's is 0.37 m above it.
        /// </summary>
        void BuildLamps(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail, bool simplified)
        {
            var thin = new MeshBuilder();
            var arms = new MeshBuilder();
            _lampCount = 0;
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                if (simplified && Array.IndexOf(SIMPLIFIED_LAMPS, i) < 0) continue;
                double a = AbyssConst.LEDGE_LAMP_ALPHA[i];
                float fy = TopY(a);
                Quaternion q = FloorRot(a, LAMP_POLE_R);
                Vector3 foot = Ring(a, LAMP_POLE_R, fy);
                Vector3 up = Vector3.up;

                MeshKit.ChamferBox(mb, foot + q * new Vector3(0f, 0.004f, 0f), new Vector3(0.22f, 0.03f, 0.22f), 0.008f, q);
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    MeshKit.HexBolt(mb, foot + q * new Vector3(sx * 0.075f, 0.019f, sz * 0.075f), q * up, 0.028f, 15f);

                MeshKit.TaperedCylinder(thin, foot + up * 0.01f, foot + up * 2.13f, 0.055f, 0.042f, 16, true, true);
                // arm, knee brace, stem and cup in matte near-black paint (the lamp's own spot reaches them at E 20-40)
                MeshKit.ChamferMember(arms, Ring(a, LAMP_POLE_R - 0.04f, fy + 2.11f), Ring(a, LAMP_HEAD_R + 0.045f, fy + 2.11f),
                    0.05f, 0.07f, 0.008f, up, true);
                // knee brace: both ends buried in the pole (r 0.044 at 1.8 m) and in the arm (y 2.075-2.145, 5 cm wide)
                MeshKit.ChamferMember(arms, Ring(a, LAMP_POLE_R + 0.02f, fy + 1.80f), Ring(a, LAMP_POLE_R + 0.70f, fy + 2.10f),
                    0.032f, 0.032f, 0.005f, up, true);
                MeshKit.Cylinder(mb, foot + up * 1.765f, foot + up * 1.835f, 0.054f, 16);
                Vector3 head = Ring(a, LAMP_HEAD_R, fy + LAMP_HEAD_ABOVE_FLOOR);
                MeshKit.Cylinder(arms, head - up * 0.47f, head - up * 0.335f, 0.022f, 12, true, true);
                MeshKit.TaperedCylinder(arms, head - up * 0.345f, head - up * 0.215f, 0.045f, 0.1f, 20, true);

                _lampHead[i] = head;
                _lampBuilt[i] = true;
                _lampCount++;
            }
            detail.AddNear(BeatKit.Emit(mb, near, "LampFittings", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
            detail.AddNear(BeatKit.Emit(thin, near, "LampPoles", ThinSteel(mats), _meshes, ShadowCastingMode.On));
            detail.AddNear(BeatKit.Emit(arms, near, "LampArms", LampArmMaterial(mats), _meshes, ShadowCastingMode.On));
            // the globes and their halos are light sources seen from the bridge: persistent
            BuildGlobes(mats, mb);
        }

        /// <summary>
        /// One camera-facing globe renderer per built lamp (shared mesh from <see cref="GlobeMesh"/>, turned by
        /// <see cref="FaceGlobes"/>) and its amber halo sprite (Glow quad, scale = true diameter).
        /// </summary>
        void BuildGlobes(MaterialLibrary mats, MeshBuilder mb)
        {
            Transform g = BeatKit.Group(_geo, "LampGlobes");
            Mesh globe = GlobeMesh(mb, _meshes);
            Mesh quad = GlowQuad(mb, _meshes);
            Material gm = GlobeMaterial(mats), hm = HaloMaterial(mats);
            for (int i = 0; i < _lampBuilt.Length; i++)
            {
                if (!_lampBuilt[i]) continue;
                _globe[i] = BeatKit.Renderer(g, "LampGlobe" + i, globe, gm, _lampHead[i], Quaternion.identity, ShadowCastingMode.Off).transform;
                MeshRenderer halo = BeatKit.Renderer(g, "LampHalo" + i, quad, hm, _lampHead[i], Quaternion.identity, ShadowCastingMode.Off);
                halo.transform.localScale = Vector3.one * GLOBE_HALO_D;
                halo.receiveShadows = false;
            }
        }

        /// <summary>
        /// Lamp globe of radius LAMP_GLOBE_R, centred on the origin, pole on local +Z (turned toward the camera), GLOBE_LON
        /// longitude x GLOBE_LAT latitude segments (the silhouette seen along the pole is a 64-gon). UV0.y = GlobeCoreWeight of
        /// the angle from the pole (1 in the small white-hot core, a saturated amber band, 0 at the limb and behind it): the
        /// GradientCard core-to-limb weight.
        /// </summary>
        public static Mesh GlobeMesh(MeshBuilder mb, List<Mesh> meshes)
        {
            int lon = GLOBE_LON, lat = GLOBE_LAT;
            int first = mb.VertexCount;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float st = i == lat ? 0f : Mathf.Sin(th), ct = Mathf.Cos(th);
                float w = GlobeCoreWeight(ct);
                for (int k = 0; k <= lon; k++)
                {
                    float ph = 2f * Mathf.PI * k / lon;
                    var n = new Vector3(st * Mathf.Cos(ph), st * Mathf.Sin(ph), ct);
                    mb.Vertex(n * LAMP_GLOBE_R, n, new Vector2(0f, w));
                }
            }
            int row = lon + 1;
            for (int i = 0; i < lat; i++)
            for (int k = 0; k < lon; k++)
            {
                int a = first + i * row + k;
                BeatKit.Tri(mb, a, a + 1, a + row + 1);
                BeatKit.Tri(mb, a, a + row + 1, a + row);
            }
            return BeatKit.Bake(mb, "B3_LampGlobe", meshes);
        }

        /// <summary>Glow sprite quad (Megastructure/Glow contract: +/-0.5 in xy), bounds inflated so the min-pixel growth never culls.</summary>
        static Mesh GlowQuad(MeshBuilder mb, List<Mesh> meshes)
        {
            Vector3 n = Vector3.back;
            int a = mb.Vertex(new Vector3(-0.5f, -0.5f, 0f), n), b = mb.Vertex(new Vector3(-0.5f, 0.5f, 0f), n);
            int c = mb.Vertex(new Vector3(0.5f, 0.5f, 0f), n), d = mb.Vertex(new Vector3(0.5f, -0.5f, 0f), n);
            BeatKit.Tri(mb, a, b, c);
            BeatKit.Tri(mb, a, c, d);
            Mesh m = BeatKit.Bake(mb, "B3_GlowQuad", meshes);
            m.bounds = new Bounds(Vector3.zero, new Vector3(80f, 80f, 80f));
            return m;
        }

        // ================================================================================================ hatch

        /// <summary>
        /// Closed steel hatch 0.9 x 2.0 at alpha 1.85, (21.1, -5.36, 250.1): a level steel sill, a frame proud of the column
        /// face (jambs 5 cm, head 4.5 cm) with the leaf recessed 2.2 cm behind it, a dark backing showing in the 6 mm gaps, two
        /// barrel hinges, a lever handle with escutcheon and lock, frame bolts and a small cold status lamp above.
        /// </summary>
        void BuildHatch(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            double a = AbyssConst.HATCH_ALPHA;
            float fy = TopY(a);
            Frame(a, out Vector3 rad, out Vector3 tan);
            Vector3 o = Ring(a, R_FACE, fy);
            Quaternion q = Quaternion.LookRotation(rad, Vector3.up);   // local x = -tangent, y = up, z = outward
            const float S = HATCH_SILL_TOP;
            float jambU = HATCH_OPEN_HALF + 0.5f * HATCH_JAMB_W;
            float jambH = HATCH_OPEN_H + 0.01f + 0.1f;

            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, -0.015f, 0.055f), new Vector3(1.16f, 0.09f, 0.17f), 0.012f, q);
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, HP(o, tan, rad, s * jambU, S - 0.01f + 0.5f * jambH, 0.01f), new Vector3(HATCH_JAMB_W, jambH, 0.08f), 0.012f, q);
            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, S + HATCH_OPEN_H + 0.055f, 0.0075f), new Vector3(2f * (jambU + 0.04f), 0.11f, 0.075f), 0.012f, q);
            // dark backing behind the leaf (AO 0.25: it only shows in the gaps)
            Color32 saved = mb.Color;
            mb.Color = new Color32(64, 0, 0, 255);
            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, S + 0.5f * HATCH_OPEN_H, -0.0095f), new Vector3(2f * HATCH_OPEN_HALF + 0.004f, HATCH_OPEN_H + 0.004f, 0.031f), 0.004f, q);
            mb.Color = saved;

            // hinges on the low-alpha side (toward the KS3 camera): knuckle, pins, flaps on leaf and jamb
            for (int h = 0; h < HATCH_HINGE_V.Length; h++)
            {
                float v = S + HATCH_HINGE_V[h];
                float u = -HATCH_OPEN_HALF - 0.006f;
                MeshKit.Cylinder(mb, HP(o, tan, rad, u, v - 0.085f, 0.036f), HP(o, tan, rad, u, v + 0.085f, 0.036f), 0.02f, 12);
                MeshKit.Cylinder(mb, HP(o, tan, rad, u, v + 0.085f, 0.036f), HP(o, tan, rad, u, v + 0.102f, 0.036f), 0.011f, 8);
                MeshKit.Cylinder(mb, HP(o, tan, rad, u, v - 0.102f, 0.036f), HP(o, tan, rad, u, v - 0.085f, 0.036f), 0.011f, 8);
                MeshKit.ChamferBox(mb, HP(o, tan, rad, -0.395f, v, 0.0315f), new Vector3(0.11f, 0.15f, 0.008f), 0.002f, q);
                MeshKit.ChamferBox(mb, HP(o, tan, rad, -0.515f, v, 0.0535f), new Vector3(0.08f, 0.15f, 0.008f), 0.002f, q);
            }

            // lever handle, escutcheon and lock on the far side
            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0.37f, S + 1.02f, 0.033f), new Vector3(0.055f, 0.22f, 0.012f), 0.003f, q);
            MeshKit.Cylinder(mb, HP(o, tan, rad, 0.37f, S + 1.06f, 0.038f), HP(o, tan, rad, 0.37f, S + 1.06f, 0.07f), 0.011f, 10);
            MeshKit.ChamferMember(mb, HP(o, tan, rad, 0.378f, S + 1.06f, 0.068f), HP(o, tan, rad, 0.25f, S + 1.06f, 0.068f),
                0.022f, 0.016f, 0.004f, Vector3.up);
            MeshKit.Sphere(mb, HP(o, tan, rad, 0.247f, S + 1.06f, 0.068f), 0.017f, 10, 6);
            MeshKit.Cylinder(mb, HP(o, tan, rad, 0.37f, S + 0.95f, 0.038f), HP(o, tan, rad, 0.37f, S + 0.95f, 0.046f), 0.014f, 12);

            // status lamp housing above the head
            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, S + HATCH_OPEN_H + 0.264f, 0.0225f), new Vector3(0.18f, 0.09f, 0.085f), 0.008f, q);

            // frame bolts on the jambs and the head
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 4; k++)
                    MeshKit.HexBolt(mb, HP(o, tan, rad, s * jambU, S + 0.25f + 0.57f * k, 0.05f), rad, 0.024f, 30f * k);
                MeshKit.HexBolt(mb, HP(o, tan, rad, s * 0.3f, S + HATCH_OPEN_H + 0.055f, 0.045f), rad, 0.024f);
            }
            _hatchFrame = BeatKit.Emit(mb, near, "HatchFrame", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            detail.AddNear(_hatchFrame);

            // the leaf, exactly 0.9 x 2.0, recessed 2.2 cm behind the jamb faces
            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, S + 0.003f + 0.5f * AbyssConst.HATCH_H, 0.015f),
                new Vector3(AbyssConst.HATCH_W, AbyssConst.HATCH_H, 0.026f), 0.008f, q);
            _hatchLeaf = BeatKit.Emit(mb, near, "HatchLeaf", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            detail.AddNear(_hatchLeaf);

            MeshKit.ChamferBox(mb, HP(o, tan, rad, 0f, S + HATCH_OPEN_H + 0.249f, 0.0675f), new Vector3(0.13f, 0.035f, 0.008f), 0.002f, q);
            detail.AddNear(BeatKit.Emit(mb, near, "HatchLamp", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off));
        }

        // ================================================================================================ cold spots

        /// <summary>
        /// The cold spot can just outside the kerb (alpha 1.62, r 28.25; see SPOT_* in Beat03_Ledge): can, bezel
        /// and lens, a pivot pin (horizontal, square to the beam) in a yoke hung from an arm on a bolted plate on the fascia
        /// below the kerb.
        /// </summary>
        void BuildSpots(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            var lens = new MeshBuilder();
            for (int i = 0; i < SPOT_ALPHA.Length; i++)
            {
                double a = SPOT_ALPHA[i];
                float fy = TopY(a);
                Frame(a, out Vector3 rad, out Vector3 tan);
                SpotPose(i, out Vector3 pos, out Vector3 dir);
                // the pin is horizontal and square to the beam's heading (the tangent when the beam aims radially)
                var dirH = new Vector3(dir.x, 0f, dir.z);
                Vector3 pin = dirH.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, dirH).normalized : tan;
                if (Vector3.Dot(pin, tan) < 0f) pin = -pin;
                Vector3 armUp = Vector3.Cross(pin, Vector3.up);

                MeshKit.TaperedCylinder(mb, pos - dir * 0.2f, pos + dir * 0.165f, 0.105f, 0.12f, 20, true);
                MeshKit.Cylinder(mb, pos + dir * 0.16f, pos + dir * 0.205f, 0.14f, 20, true);
                MeshKit.Cylinder(mb, pos - dir * 0.235f, pos - dir * 0.195f, 0.06f, 12, true);
                MeshKit.Disc(lens, pos + dir * 0.207f, dir, 0.118f, 20);

                float yb = fy - 0.35f;
                Vector3 yoke = Ring(a, SPOT_R, yb);
                MeshKit.Cylinder(mb, pos - pin * 0.19f, pos + pin * 0.19f, 0.018f, 10);
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.ChamferMember(mb, yoke + pin * (s * 0.175f), pos + pin * (s * 0.175f), 0.03f, 0.012f, 0.003f, armUp);
                MeshKit.ChamferMember(mb, yoke - pin * 0.19f, yoke + pin * 0.19f, 0.04f, 0.04f, 0.006f, Vector3.up);
                MeshKit.ChamferMember(mb, Ring(a, R_OUT - 0.005f, yb), Ring(a, SPOT_R + 0.02f, yb), 0.05f, 0.05f, 0.006f, Vector3.up);
                Quaternion qp = Quaternion.LookRotation(tan, Vector3.up);   // local x = outward radial
                MeshKit.ChamferBox(mb, Ring(a, R_OUT + 0.004f, yb), new Vector3(0.02f, 0.2f, 0.2f), 0.004f, qp);
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sx = -1; sx <= 1; sx += 2)
                    MeshKit.HexBolt(mb, Ring(a, R_OUT + 0.014f, yb + sy * 0.07f) + tan * (sx * 0.07f), rad, 0.022f, 10f);
            }
            detail.AddNear(BeatKit.Emit(mb, near, "SpotHousings", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
            detail.AddNear(BeatKit.Emit(lens, near, "SpotLenses", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off));
        }

        // ================================================================================================ plates

        /// <summary>
        /// Steel landing plate where the bridge bears on the collar (|x| &lt;= 0.75, z 215.995-217.3, 12 mm proud): it spans the
        /// kerb opening, covers the deck-to-collar junction and is level although the helix starts to fall right of x = 0.
        /// </summary>
        void BuildLandingPlate(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            float y6 = TopY(0.0);
            float z0 = AbyssConst.BRIDGE_Z1 - 0.005f, z1 = AbyssConst.BRIDGE_Z1 + 1.3f;
            float top = y6 + 0.012f, bottom = y6 - 0.04f;
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (top + bottom), 0.5f * (z0 + z1)), new Vector3(2f * KERB_OPEN_HALF, top - bottom, z1 - z0), 0.008f);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 3; k++)
                    MeshKit.HexBolt(mb, new Vector3(s * 0.66f, top, AbyssConst.BRIDGE_Z1 + 0.25f + 0.4f * k), Vector3.up, 0.03f, 20f * k, -1f, 0.03f);
                MeshKit.HexBolt(mb, new Vector3(s * 0.3f, top, AbyssConst.BRIDGE_Z1 + 1.2f), Vector3.up, 0.03f, 0f, -1f, 0.03f);
            }
            detail.AddNear(BeatKit.Emit(mb, near, "LandingPlate", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
        }

        /// <summary>
        /// Expansion-joint cover plates across the walking surface (column face to the kerb) at alpha pi/2 (tilted with the
        /// ramp) and at alpha pi (level, over the joint between the collar and the apron), each with two rows of bolts.
        /// </summary>
        void BuildJointPlates(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            float r0 = R_FACE - 0.01f, r1 = R_WALK - 0.02f;
            float rc = 0.5f * (r0 + r1), len = r1 - r0;
            const int bolts = 9;

            double a = 0.5 * Math.PI;
            Quaternion q = FloorRot(a, rc);
            Vector3 up = q * Vector3.up;
            MeshKit.ChamferBox(mb, Ring(a, rc, TopY(a)) + up * 0.003f, new Vector3(len, 0.022f, 0.3f), 0.006f, q);
            for (int k = 0; k < bolts; k++)
            {
                float r = 22.3f + 0.65f * k;
                for (int s = -1; s <= 1; s += 2)
                    MeshKit.HexBolt(mb, Ring(a, r, TopY(a)) + q * new Vector3(0f, 0.014f, s * 0.11f), up, 0.026f, 13f * k);
            }

            float y8 = TopY(Math.PI);
            float zc = AbyssConst.ARC_CENTRE.y + rc;
            MeshKit.ChamferBox(mb, new Vector3(0f, y8 + 0.005f, zc), new Vector3(0.3f, 0.022f, len), 0.006f);
            for (int k = 0; k < bolts; k++)
            for (int s = -1; s <= 1; s += 2)
                MeshKit.HexBolt(mb, new Vector3(s * 0.11f, y8 + 0.016f, AbyssConst.ARC_CENTRE.y + 22.3f + 0.65f * k), Vector3.up, 0.026f, 17f * k);
            detail.AddNear(BeatKit.Emit(mb, near, "JointPlates", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
        }

        // ================================================================================================ conduit

        /// <summary>
        /// Conduit O 0.044 along the column foot 0.32 m above the ramp (a lofted helix, clipped every 1.1 m), junction boxes
        /// at both ends, either side of the hatch and behind every lamp pole (with a stub into the pole), risers into the floor.
        /// </summary>
        void BuildConduit(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            double gap = (HATCH_OPEN_HALF + HATCH_JAMB_W + 0.095 + 0.13) / CONDUIT_R;
            double h0 = AbyssConst.HATCH_ALPHA - gap, h1 = AbyssConst.HATCH_ALPHA + gap;
            MeshKit.Circle(CONDUIT_RADIUS, 8, s_tube);
            ConduitRun(mb, CONDUIT_A0, h0);
            ConduitRun(mb, h1, CONDUIT_A1);

            JunctionBox(mb, CONDUIT_A0);
            JunctionBox(mb, h0);
            JunctionBox(mb, h1);
            JunctionBox(mb, CONDUIT_A1);
            Riser(mb, CONDUIT_A0);
            Riser(mb, CONDUIT_A1);
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
            {
                double a = AbyssConst.LEDGE_LAMP_ALPHA[i];
                float y = TopY(a) + CONDUIT_H;
                JunctionBox(mb, a);
                MeshKit.Cylinder(mb, Ring(a, 22.115f, y), Ring(a, LAMP_POLE_R - 0.03f, y), 0.018f, 10);
            }

            // saddle clips, skipping the boxes
            double da = CLIP_SPACING / CONDUIT_R;
            for (double a = CONDUIT_A0 + da; a < CONDUIT_A1 - 0.5 * da; a += da)
            {
                if (a > h0 - 0.5 * da && a < h1 + 0.5 * da) continue;
                if (NearBox(a, h0, h1)) continue;
                MeshKit.ChamferBox(mb, Ring(a, 22.05f, TopY(a) + CONDUIT_H), new Vector3(0.12f, 0.056f, 0.035f), 0.004f, FloorRot(a, 22.05f));
            }
            detail.AddNear(BeatKit.Emit(mb, near, "Conduit", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
        }

        static bool NearBox(double a, double h0, double h1)
        {
            const double keep = 0.3 / CONDUIT_R;
            if (Math.Abs(a - CONDUIT_A0) < keep || Math.Abs(a - CONDUIT_A1) < keep || Math.Abs(a - h0) < keep || Math.Abs(a - h1) < keep) return true;
            for (int i = 0; i < AbyssConst.LEDGE_LAMP_ALPHA.Length; i++)
                if (Math.Abs(a - AbyssConst.LEDGE_LAMP_ALPHA[i]) < keep) return true;
            return false;
        }

        void ConduitRun(MeshBuilder mb, double a0, double a1)
        {
            var frames = new List<LoftFrame>(160);
            AddConduitFrame(frames, a0);
            for (int k = 0; k < _frames.Count; k++)
                if (_frames[k] > a0 + 1e-4 && _frames[k] < a1 - 1e-4) AddConduitFrame(frames, _frames[k]);
            AddConduitFrame(frames, a1);
            if (frames.Count >= 2) MeshKit.Loft(mb, s_tube, frames, true);
        }

        static void AddConduitFrame(List<LoftFrame> frames, double a)
        {
            SinCos(a, out double s, out double c);
            var t = new Vector3((float)(CONDUIT_R * c), (float)TopSlope(a), (float)(CONDUIT_R * s));
            frames.Add(new LoftFrame(Ring(a, CONDUIT_R, TopY(a) + CONDUIT_H), Quaternion.LookRotation(t.normalized, Vector3.up)));
        }

        static void JunctionBox(MeshBuilder mb, double a)
        {
            float y = TopY(a) + CONDUIT_H;
            Quaternion q = FloorRot(a, CONDUIT_BOX_R);
            MeshKit.ChamferBox(mb, Ring(a, CONDUIT_BOX_R, y), new Vector3(0.13f, 0.24f, 0.19f), 0.01f, q);
            MeshKit.ChamferBox(mb, Ring(a, CONDUIT_BOX_R + 0.069f, y), new Vector3(0.01f, 0.2f, 0.15f), 0.003f, q);   // lid
        }

        static void Riser(MeshBuilder mb, double a)
        {
            float fy = TopY(a);
            MeshKit.Cylinder(mb, Ring(a, CONDUIT_R, fy + CONDUIT_H - 0.11f), Ring(a, CONDUIT_R, fy - 0.05f), CONDUIT_RADIUS, 8);
        }

        // ================================================================================================ fascia anchors

        /// <summary>
        /// Post-tensioning anchor plates in a row along the fascia (every 2.4 m, 1.25 m below the walking surface, following
        /// the helix): a bolted square bearing plate, the anchor nut and a grease cap. A slow regular rhythm that reads from the
        /// bridge and when looking over the kerb; none near the bridge landing (|x| &lt; 1.1).
        /// </summary>
        void BuildAnchors(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            const float spacing = 2.4f, below = 1.25f;
            double da = spacing / R_OUT;
            double start = CutAlpha(R_OUT, PAD_END_X) + 0.5 * da;
            for (double a = start; a < Math.PI - 0.5 * da; a += da)
            {
                if (Math.Abs(a) < 1.1 / R_OUT) continue;
                float y = TopY(a) - below;
                Frame(a, out Vector3 rad, out Vector3 tan);
                Quaternion qp = Quaternion.LookRotation(tan, Vector3.up);   // local x = outward radial
                MeshKit.ChamferBox(mb, Ring(a, R_OUT + 0.005f, y), new Vector3(0.03f, 0.3f, 0.3f), 0.006f, qp);
                MeshKit.HexBolt(mb, Ring(a, R_OUT + 0.02f, y), rad, 0.09f, (float)(a * 57.29578), 0.05f, 0.03f);
                MeshKit.Cylinder(mb, Ring(a, R_OUT + 0.06f, y), Ring(a, R_OUT + 0.105f, y), 0.034f, 12);
            }
            detail.AddNear(BeatKit.Emit(mb, near, "FasciaAnchors", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
        }

        // ================================================================================================ drains

        /// <summary>Floor drains against the kerb (a slotted steel grate tilted with the ramp) and scupper spouts below them.</summary>
        void BuildDrains(MaterialLibrary mats, MeshBuilder mb, Transform near, DetailTier detail)
        {
            for (int i = 0; i < DRAIN_ALPHA.Length; i++)
            {
                double a = DRAIN_ALPHA[i];
                float fy = TopY(a);
                const float rg = 27.72f;
                Quaternion q = FloorRot(a, rg);
                Vector3 c = Ring(a, rg, fy);
                MeshKit.ChamferBox(mb, c + q * new Vector3(0f, 0.002f, 0f), new Vector3(0.2f, 0.012f, 0.3f), 0.003f, q);
                for (int b = 0; b < 4; b++)
                    MeshKit.ChamferBox(mb, c + q * new Vector3(-0.06f + 0.04f * b, 0.0095f, 0f), new Vector3(0.016f, 0.006f, 0.26f), 0.002f, q);
                MeshKit.Cylinder(mb, Ring(a, R_OUT - 0.01f, fy - 0.22f), Ring(a, R_OUT + 0.025f, fy - 0.22f), 0.085f, 16);
                MeshKit.Cylinder(mb, Ring(a, R_OUT, fy - 0.22f), Ring(a, R_OUT + 0.36f, fy - 0.25f), 0.05f, 12);
            }
            detail.AddNear(BeatKit.Emit(mb, near, "Drains", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On));
        }
    }
}
