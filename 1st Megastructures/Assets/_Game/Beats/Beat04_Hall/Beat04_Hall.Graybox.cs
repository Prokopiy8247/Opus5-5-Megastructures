using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Graybox tier of beat 4 (Phase 3): the hull box (x -142..-32, z 199..339, floor -21, hull top +39 with a 2 m roof slab
    /// under it like the full tier, hull bottom -45 with the 12 x 12 chamfer on the lower -X edge, 1 m walls) with the
    /// 20 x 24 portal and exit openings, roof beams under the slab, the 1.5 m catwalk at y -7 with rails and its 14 m support
    /// posts, and the four crank-slider assemblies as simple volumes moving on the exact crank equation. Lights: 8 ceiling
    /// spot cans on 14.5 m stems, 6 amber hazard points. The hull base, catwalk steel and roof beams are kept for ReportScale
    /// and the KS4 subjects.
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        void BuildGraybox(BeatContext ctx)
        {
            var mb = new MeshBuilder();
            BuildHull(ctx, mb);
            BuildCatwalk(ctx, mb);
            BuildCranks(ctx, mb);
        }

        // ---------------------------------------------------------------- hull

        void BuildHull(BeatContext ctx, MeshBuilder mb)
        {
            Material mat = ctx.Mats[MatId.ConcreteBoard];
            Transform hull = BeatKit.Group(_geo, "Hull");
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, z0 = AbyssConst.HALL_Z0, z1 = AbyssConst.HALL_Z1;
            float yf = AbyssConst.HALL_FLOOR_Y, yr = AbyssConst.HALL_ROOF_Y, yb = AbyssConst.HALL_HULL_BOTTOM_Y, ch = AbyssConst.HALL_CHAMFER;
            float t = HULL_WALL_T, zc = AbyssConst.W[8].z, ow = AbyssConst.HALL_OPENING_W * 0.5f;
            float oy0 = OPENING_Y0, oy1 = OPENING_Y0 + AbyssConst.HALL_OPENING_H;
            float yc = yr - ROOF_T;   // underside of the roof slab: the hull top is +39 (14.12), walls and beams end below it

            // solid base under the floor with the 12 x 12 chamfer on the lower -X edge (extruded along z; section x = world x)
            var sec = new CrossSection();
            sec.Clear(true);
            sec.Add(new Vector2(x0, yb), true);
            sec.Add(new Vector2(x0, yf), true);
            sec.Add(new Vector2(x1, yf), true);
            sec.Add(new Vector2(x1, yb + ch), true);
            sec.Add(new Vector2(x1 + ch, yb), true);
            MeshKit.Extrude(mb, sec, new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), Vector3.up, true);
            _hullBase = BeatKit.Emit(mb, hull, "HullBase", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            Box(mb, (x0 + x1) * 0.5f, yc + ROOF_T * 0.5f, (z0 + z1) * 0.5f, x0 - x1, ROOF_T, z1 - z0);
            _roof = BeatKit.Emit(mb, hull, "HullRoof", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            Box(mb, (x0 + x1) * 0.5f, (yf + yc) * 0.5f, z0 + t * 0.5f, x0 - x1 - 2f * t, yc - yf, t);
            Box(mb, (x0 + x1) * 0.5f, (yf + yc) * 0.5f, z1 - t * 0.5f, x0 - x1 - 2f * t, yc - yf, t);
            BeatKit.Emit(mb, hull, "HullSides", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);

            // end walls with the openings: below, above, left (-Z), right (+Z) pieces
            for (int e = 0; e < 2; e++)
            {
                float xw = e == 0 ? x0 - t * 0.5f : x1 + t * 0.5f;
                Box(mb, xw, (yf + oy0) * 0.5f, zc, t, oy0 - yf, 2f * ow);
                if (e == 1) _openBelow = BeatKit.Emit(mb, hull, "ExitBelow", mat, _meshes, ShadowCastingMode.On);
                Box(mb, xw, (oy1 + yc) * 0.5f, zc, t, yc - oy1, 2f * ow);
                if (e == 1) _openAbove = BeatKit.Emit(mb, hull, "ExitAbove", mat, _meshes, ShadowCastingMode.On);
                Box(mb, xw, (yf + yc) * 0.5f, (z0 + zc - ow) * 0.5f, t, yc - yf, zc - ow - z0);
                if (e == 1) _openLeft = BeatKit.Emit(mb, hull, "ExitLeft", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);
                Box(mb, xw, (yf + yc) * 0.5f, (zc + ow + z1) * 0.5f, t, yc - yf, z1 - zc - ow);
                if (e == 1) _openRight = BeatKit.Emit(mb, hull, "ExitRight", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);
                else BeatKit.Emit(mb, hull, "PortalWall", mat, _meshes, ShadowCastingMode.On, Vector3.zero, true);
            }

            // roof beams across the hall under the slab (graybox stand-in for the 12 trusses; the KS4 "trusses" subject)
            for (int i = 1; i <= ROOF_BEAMS; i++)
            {
                float u = 110f * i / (ROOF_BEAMS + 1);
                Box(mb, X(u), yc - BEAM_H * 0.5f, (z0 + z1) * 0.5f, BEAM_W, BEAM_H, z1 - z0 - 2f * t);
            }
            _trusses = BeatKit.Emit(mb, hull, "RoofBeams", ctx.Mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On, Vector3.zero, true);
        }

        // ---------------------------------------------------------------- catwalk

        void BuildCatwalk(BeatContext ctx, MeshBuilder mb)
        {
            Transform cw = BeatKit.Group(_geo, "Catwalk");
            float y = AbyssConst.CATWALK_Y, hw = AbyssConst.CATWALK_W * 0.5f, zc = AbyssConst.W[8].z;
            float x0 = AbyssConst.HALL_X0, x1 = AbyssConst.HALL_X1, len = x0 - x1, xm = (x0 + x1) * 0.5f;

            Box(mb, xm, y - CATWALK_GRATING_T * 0.5f, zc, len, CATWALK_GRATING_T, AbyssConst.CATWALK_W, 0.005f);
            _catwalk = BeatKit.Emit(mb, cw, "CatwalkGrating", ctx.Mats[MatId.Grating], _meshes, ShadowCastingMode.On);

            float rt = RAIL_TUBE, rh = AbyssConst.RAIL_H;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = zc + s * (hw - rt * 0.5f);
                Box(mb, xm, y + rh - rt * 0.5f, z, len, rt, rt, 0.005f);                 // top rail
                Box(mb, xm, y + rh * 0.5f, z, len, rt, rt, 0.005f);                       // mid rail
                Box(mb, xm, y + 0.05f, zc + s * (hw - 0.03f), len, 0.1f, 0.01f, 0.002f);  // kick plate (inside the post line)
                int posts = Mathf.RoundToInt(len / POST_SPACING);
                for (int p = 0; p <= posts; p++)
                    Box(mb, x0 - POST_SPACING * p, y + rh * 0.5f, z, rt, rh, rt, 0.005f);
            }
            // 14 m support posts from the floor every 5 m at v = +/-1.0, with cross beams under the grating
            int supports = Mathf.RoundToInt(len / SUPPORT_SPACING);
            float yf = AbyssConst.HALL_FLOOR_Y;
            for (int p = 1; p < supports; p++)
            {
                float x = x0 - SUPPORT_SPACING * p;
                for (int s = -1; s <= 1; s += 2)
                    Box(mb, x, (yf + y - 0.35f) * 0.5f, zc + s * SUPPORT_V, SUPPORT_SECTION, y - 0.35f - yf, SUPPORT_SECTION);
                Box(mb, x, y - 0.2f, zc, SUPPORT_SECTION, 0.3f, 2f * SUPPORT_V + SUPPORT_SECTION);
            }
            _grayCatwalkSteel = BeatKit.Emit(mb, cw, "CatwalkSteel", ctx.Mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
        }

        // ---------------------------------------------------------------- crank-slider assemblies

        void BuildCranks(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform cr = BeatKit.Group(_geo, "Cranks");
            float yf = AbyssConst.HALL_FLOOR_Y, r = AbyssConst.CRANK_R;
            float wr = AbyssConst.WHEEL_D * 0.5f;

            // shared meshes: wheel disc with its pin (two variants: pin toward the slider axis at theta = 0), rod, crosshead group, cylinder
            var wheelMesh = new Mesh[2];
            var crossMesh = new Mesh[2];
            for (int v = 0; v < 2; v++)
            {
                float ax = v == 0 ? -1f : 1f;
                MeshKit.Cylinder(mb, new Vector3(0f, -WHEEL_T * 0.5f, 0f), new Vector3(0f, WHEEL_T * 0.5f, 0f), wr, 48);
                MeshKit.Cylinder(mb, new Vector3(ax * r, WHEEL_T * 0.5f - 0.05f, 0f), new Vector3(ax * r, ROD_Y - WHEEL_PLANE_Y + ROD_SECTION * 0.5f, 0f), PIN_D * 0.5f, 16);
                MeshKit.ChamferBox(mb, new Vector3(ax * r * 0.5f, WHEEL_T * 0.5f + 0.2f, 0f), new Vector3(r, 0.4f, 1.2f), 0.03f);   // pin arm stripe (shows the turn)
                wheelMesh[v] = BeatKit.Bake(mb, v == 0 ? "CrankWheelA" : "CrankWheelB", _meshes);

                MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(CROSSHEAD_L, CROSSHEAD_H, CROSSHEAD_W), 0.05f);
                MeshKit.Cylinder(mb, new Vector3(ax * CROSSHEAD_L * 0.5f - ax * 0.05f, 0f, 0f), new Vector3(ax * (CROSSHEAD_L * 0.5f + PISTON_ROD_L), 0f, 0f),
                    AbyssConst.PISTON_ROD * 0.5f, 24);
                crossMesh[v] = BeatKit.Bake(mb, v == 0 ? "CrossheadA" : "CrossheadB", _meshes);
            }
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(ROD_SECTION, ROD_SECTION, AbyssConst.CRANK_L), 0.04f);
            Mesh rodMesh = BeatKit.Bake(mb, "ConnectingRod", _meshes);
            MeshKit.Cylinder(mb, new Vector3(-CYL_LEN * 0.5f, 0f, 0f), new Vector3(CYL_LEN * 0.5f, 0f, 0f), AbyssConst.PISTON_BORE * 0.5f, 64);
            Mesh cylMesh = BeatKit.Bake(mb, "PistonCylinder", _meshes);

            for (int k = 0; k < 4; k++)
            {
                bool a = AbyssConst.BANK_TYPE_A[k];
                float xc = X(CrankU(k)), zc = Z(AbyssConst.BANK_V[k]);
                Transform bank = BeatKit.Group(cr, "Bank" + k + (a ? "A" : "B"));

                // static: crankshaft down to the pit housing, pit, cylinder
                MeshKit.Cylinder(mb, new Vector3(xc, yf + PIT_H, zc), new Vector3(xc, WHEEL_PLANE_Y - WHEEL_T * 0.5f + 0.05f, zc), CRANKSHAFT_D * 0.5f, 24);
                BeatKit.Emit(mb, bank, "Crankshaft", mats[MatId.RodPolished], _meshes, ShadowCastingMode.On);
                MeshKit.ChamferBox(mb, new Vector3(xc, yf + PIT_H * 0.5f, zc), new Vector3(PIT_SIZE, PIT_H, PIT_SIZE), 0.05f);
                BeatKit.Emit(mb, bank, "CrankPit", mats[MatId.SteelWeathered], _meshes, ShadowCastingMode.On);
                float cu0 = a ? CYL_U0_A : CYL_U0_B;
                _cylinders[k] = BeatKit.Renderer(bank, "Cylinder", cylMesh, mats[MatId.SteelPainted], new Vector3(X(cu0 + CYL_LEN * 0.5f), ROD_Y, zc),
                    Quaternion.identity, ShadowCastingMode.On);

                // moving: wheel (rotates about Y at the crank centre), rod, crosshead with its piston rod
                _wheelR[k] = BeatKit.Renderer(bank, "Wheel", wheelMesh[a ? 0 : 1], mats[MatId.SteelPainted], new Vector3(xc, WHEEL_PLANE_Y, zc),
                    Quaternion.identity, ShadowCastingMode.On);
                _wheel[k] = _wheelR[k].transform;
                // moving rods do not cast (14.5.4 pitfalls: no flickering shadows)
                _rodR[k] = BeatKit.Renderer(bank, "Rod", rodMesh, mats[MatId.RodPolished], new Vector3(xc, ROD_Y, zc), Quaternion.identity, ShadowCastingMode.Off);
                _rod[k] = _rodR[k].transform;
                _crosshead[k] = BeatKit.Renderer(bank, "Crosshead", crossMesh[a ? 0 : 1], mats[MatId.RodPolished], new Vector3(xc, ROD_Y, zc),
                    Quaternion.identity, ShadowCastingMode.Off).transform;
            }
        }

        // ---------------------------------------------------------------- lights

        void BuildGrayboxLights(BeatContext ctx)
        {
            Transform lights = BeatKit.Group(ctx.Root, "Lights");
            var mb = new MeshBuilder();
            float yr = AbyssConst.HALL_ROOF_Y - ROOF_T;   // stems hang from the underside of the roof slab
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                float cu = (AbyssConst.BANK_TYPE_A[k] ? CYL_U0_A : CYL_U0_B) + CYL_LEN * 0.5f;
                float zc = Z(AbyssConst.BANK_V[k]);
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = X(cu + s * SPOT_OFFSET_U);
                    MeshKit.ChamferBox(mb, new Vector3(x, (SPOT_Y + 0.5f + yr) * 0.5f, zc), new Vector3(0.25f, yr - SPOT_Y - 0.5f, 0.25f), 0.02f);
                    MeshKit.ChamferBox(mb, new Vector3(x, SPOT_Y + 0.25f, zc), new Vector3(1f, 1f, 1f), 0.03f);
                    BeatKit.Spot(lights, "SpotCan" + n++, new Vector3(x, SPOT_Y - 0.3f, zc), Vector3.down, AbyssPalette.SPOT_CAN, SPOT_I, SPOT_RANGE, SPOT_ANGLE);
                }
            }
            BeatKit.Emit(mb, _geo, "SpotStems", ctx.Mats[MatId.SteelPainted], _meshes, ShadowCastingMode.Off);

            // amber hazard points: the four crank pits, the portal and the exit
            float yf = AbyssConst.HALL_FLOOR_Y;
            for (int k = 0; k < 4; k++)
            {
                var p = new Vector3(X(CrankU(k)) + PIT_SIZE * 0.5f + 0.2f, yf + PIT_H + 0.3f, Z(AbyssConst.BANK_V[k]));
                MeshKit.ChamferBox(mb, p, new Vector3(0.25f, 0.25f, 0.25f), 0.02f);
                BeatKit.Point(lights, "HazardPit" + k, p + new Vector3(0.3f, 0f, 0f), AbyssPalette.AMBER_LIGHT, HAZARD_I, HAZARD_RANGE);
            }
            float zw = AbyssConst.W[8].z, yc = AbyssConst.CATWALK_Y;
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? AbyssConst.HALL_X0 - HULL_WALL_T - 0.3f : AbyssConst.HALL_X1 + HULL_WALL_T + 0.3f;
                var p = new Vector3(x, yc + 2.6f, zw + AbyssConst.HALL_OPENING_W * 0.5f + 0.6f);   // on the wall pier beside the opening
                MeshKit.ChamferBox(mb, p, new Vector3(0.25f, 0.25f, 0.25f), 0.02f);
                BeatKit.Point(lights, "HazardOpening" + e, p + new Vector3(0f, -0.3f, 0f), AbyssPalette.AMBER_LIGHT, HAZARD_I, HAZARD_RANGE);
            }
            BeatKit.Emit(mb, _geo, "HazardLamps", ctx.Mats[MatId.LampAmber], _meshes, ShadowCastingMode.Off);
        }
    }
}
