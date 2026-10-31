using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 5 Graybox tier (Fallbacks 14.5.5: "platform, rails and a window-shader quad"): the phase-3 geometry, kept unchanged.
    /// The 10 x 20 dock apron with rails, the 20 x 20 x 1.2 platform with rails, the -Z gate and the +X boarding section, the two
    /// bridging plates, four 12 m pylons with lamps, cables and guide shoes; the two inclined guide rails as boxes; the city as
    /// shader-tier M_Facade quads (face 270 keeps the fan bore and the tunnel mouth clear and starts at y -333, under the bridge void).
    /// </summary>
    public sealed partial class Beat05_Lift
    {
        const float GUIDE_GAP = 0.1f;
        const float WALL_CELL = 50f;

        void BuildGraybox(BeatContext ctx)
        {
            var mb = new MeshBuilder();
            GrayDock(ctx, mb);
            GrayPlatform(ctx, mb);
            GrayGuideRails(ctx, mb);
            GrayCity(ctx, mb);
        }

        void GrayDock(BeatContext ctx, MeshBuilder mb)
        {
            Vector3 top = AbyssConst.LIFT_TOP;
            float xa = AbyssConst.HALL_X1, xb = xa - DOCK_L, half = DeckHalf;
            MeshKit.ChamferBox(mb, new Vector3((xa + xb) * 0.5f, top.y - DOCK_T * 0.5f, top.z), new Vector3(DOCK_L, DOCK_T, PathGuide.DOCK_APRON_W), 0.02f);
            BeatKit.FloorUVAlongX(mb, 0, top.z);
            _dock = BeatKit.Emit(mb, _geo, "DockApron", ctx.Mats[MatId.ConcreteFloor], _meshes, ShadowCastingMode.On);
            for (int s = -1; s <= 1; s += 2)
            {
                float z = top.z + s * (half - RAIL_INSET);
                GrayRail(mb, new Vector3(xa - 0.3f, top.y, z), new Vector3(xb + 0.05f, top.y, z));
            }
            BeatKit.Emit(mb, _geo, "DockRails", ctx.Mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
        }

        /// <summary>A straight 1.1 m rail between two floor points: top and mid tubes and posts every 2 m.</summary>
        static void GrayRail(MeshBuilder mb, Vector3 a, Vector3 b) => BeatKit.Rail(mb, a, b, AbyssConst.RAIL_H, RAIL_TUBE, RAIL_POST_SPACING);

        void GrayPlatform(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            _platform = BeatKit.Group(_geo, "Platform", AbyssConst.LIFT_TOP, Quaternion.identity);
            float half = DeckHalf, ri = half - RAIL_INSET, open = OPENING * 0.5f;

            MeshKit.ChamferBox(mb, new Vector3(0f, -AbyssConst.LIFT_THICK * 0.5f, 0f), new Vector3(AbyssConst.LIFT_SIZE, AbyssConst.LIFT_THICK, AbyssConst.LIFT_SIZE), 0.03f);
            _deck = BeatKit.Emit(mb, _platform, "Deck", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);

            GrayRail(mb, new Vector3(-ri, 0f, -ri), new Vector3(-ri, 0f, ri));
            GrayRail(mb, new Vector3(-ri, 0f, ri), new Vector3(ri, 0f, ri));
            GrayRail(mb, new Vector3(-ri, 0f, -ri), new Vector3(-open, 0f, -ri));
            GrayRail(mb, new Vector3(open, 0f, -ri), new Vector3(ri, 0f, -ri));
            GrayRail(mb, new Vector3(ri, 0f, -ri), new Vector3(ri, 0f, -open));
            GrayRail(mb, new Vector3(ri, 0f, open), new Vector3(ri, 0f, ri));
            _rails = BeatKit.Emit(mb, _platform, "DeckRails", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);

            GrayRail(mb, new Vector3(-open, 0f, 0f), new Vector3(open, 0f, 0f));
            Mesh gateMesh = BeatKit.Bake(mb, "GateRail", _meshes);
            _gateR = BeatKit.Renderer(_platform, "Gate", gateMesh, mats[MatId.SteelPainted], new Vector3(0f, 0f, -ri + SLIDE_INSET), Quaternion.identity, ShadowCastingMode.On);
            _gate = _gateR.transform;
            GrayRail(mb, new Vector3(0f, 0f, -open), new Vector3(0f, 0f, open));
            Mesh boardMesh = BeatKit.Bake(mb, "BoardingRail", _meshes);
            _boarding = BeatKit.Renderer(_platform, "BoardingRail", boardMesh, mats[MatId.SteelPainted], new Vector3(ri - SLIDE_INSET, 0f, OPENING),
                Quaternion.identity, ShadowCastingMode.On).transform;

            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(PLATE_DEPTH, PLATE_T, OPENING), 0.005f);
            Mesh plateX = BeatKit.Bake(mb, "PlateX", _meshes);
            _plateXR = BeatKit.Renderer(_platform, "PlateX", plateX, mats[MatId.SteelPainted], new Vector3(PLATE_HOME + PLATE_TRAVEL, PLATE_TOP - PLATE_T * 0.5f, 0f),
                Quaternion.identity, ShadowCastingMode.Off);
            _plateX = _plateXR.transform;
            MeshKit.ChamferBox(mb, Vector3.zero, new Vector3(OPENING, PLATE_T, PLATE_DEPTH), 0.005f);
            Mesh plateZ = BeatKit.Bake(mb, "PlateZ", _meshes);
            _plateZ = BeatKit.Renderer(_platform, "PlateZ", plateZ, mats[MatId.SteelPainted], new Vector3(0f, PLATE_TOP - PLATE_T * 0.5f, -PLATE_HOME),
                Quaternion.identity, ShadowCastingMode.Off).transform;

            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                MeshKit.ChamferBox(mb, new Vector3(sx * PYLON_OFFSET, PYLON_H * 0.5f, sz * PYLON_OFFSET), new Vector3(PYLON_SIZE, PYLON_H, PYLON_SIZE), 0.03f);
            }
            _pylons = BeatKit.Emit(mb, _platform, "Pylons", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                MeshKit.ChamferBox(mb, new Vector3(sx * PYLON_OFFSET, PYLON_H + 0.2f, sz * PYLON_OFFSET), new Vector3(0.6f, 0.4f, 0.6f), 0.02f);
            }
            BeatKit.Emit(mb, _platform, "PylonLamps", mats[MatId.LampCold], _meshes, ShadowCastingMode.Off);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                MeshKit.Cylinder(mb, new Vector3(sx * PYLON_OFFSET, PYLON_H - 0.2f, sz * PYLON_OFFSET), new Vector3(sx * ri, 0.05f, sz * ri), CABLE_R, 8);
            }
            _cables = BeatKit.Emit(mb, _platform, "Cables", mats[MatId.Cable], _meshes, ShadowCastingMode.Off);
            float shoeZ0 = half - 0.05f, shoeZ1 = AbyssConst.GUIDE_RAIL_Z0 - Beat05Layout.GUIDE_W * 0.5f - AbyssConst.LIFT_TOP.z + 0.05f;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferBox(mb, new Vector3(s * 6f, -AbyssConst.LIFT_THICK * 0.5f, (shoeZ0 + shoeZ1) * 0.5f), new Vector3(1f, 0.8f, shoeZ1 - shoeZ0), 0.03f);
            BeatKit.Emit(mb, _platform, "GuideShoes", mats[MatId.SteelPainted], _meshes, ShadowCastingMode.On);
        }

        void GrayGuideRails(BeatContext ctx, MeshBuilder mb)
        {
            Vector3 top = AbyssConst.LIFT_TOP, bottom = AbyssConst.LIFT_BOTTOM;
            Quaternion rot = Beat05Layout.RailRotation;
            int n = AbyssConst.GUIDE_RAIL_SEGMENTS;
            float seg = Beat05Layout.GUIDE_SEG, total = n * seg;
            float[] zs = { AbyssConst.GUIDE_RAIL_Z0, AbyssConst.GUIDE_RAIL_Z1 };
            for (int r = 0; r < zs.Length; r++)
            {
                Vector3 mid = (top + bottom) * 0.5f;
                mid.z = zs[r];
                for (int i = 0; i < n; i++)
                {
                    float s = -total * 0.5f + seg * (i + 0.5f);
                    MeshKit.ChamferBox(mb, rot * new Vector3(0f, 0f, s), new Vector3(Beat05Layout.GUIDE_W, Beat05Layout.GUIDE_D, seg - GUIDE_GAP), 0.03f, rot);
                }
                BeatKit.Emit(mb, _geo, "GuideRail" + r, ctx.Mats[MatId.SteelPainted], _meshes, ShadowCastingMode.Off, mid, true);
            }
            _guideSegments = n;
        }

        void GrayCity(BeatContext ctx, MeshBuilder mb)
        {
            Material mat = ctx.Mats[MatId.Facade];
            Transform city = BeatKit.Group(_geo, "City");
            Vector2 c = AbyssConst.SHAFT_CENTRE;
            float a = AbyssConst.SHAFT_APOTHEM - Beat05Layout.FACADE_INSET;
            float uHalf = AbyssConst.CITY_COLUMNS * AbyssConst.CITY_MODULE_W * 0.5f;   // 204: outer modules overhang into the neighbours
            float yTop = AbyssConst.CITY_TOP_Y, yBot = AbyssConst.CITY_BOTTOM_Y;
            Vector3 fan = AbyssConst.FAN_CENTRE, mouth = AbyssConst.W[15];
            var holes = new List<BeatKit.Rect2>
            {
                new BeatKit.Rect2(fan.x - BeatKit.BORE_SQUARE_HALF, fan.y - BeatKit.BORE_SQUARE_HALF, fan.x + BeatKit.BORE_SQUARE_HALF, fan.y + BeatKit.BORE_SQUARE_HALF),
                new BeatKit.Rect2(mouth.x - AbyssConst.TUNNEL_W * 0.5f - 0.7f, mouth.y - 1.5f, mouth.x + AbyssConst.TUNNEL_W * 0.5f + 0.7f, mouth.y + AbyssConst.TUNNEL_H + 0.9f),
            };

            for (int fi = 0; fi < Beat05Layout.CityFaces.Length; fi++)
            {
                int k = Beat05Layout.CityFaces[fi];
                Vector2 n = MeshKit.HexFaceNormal(k);
                var t = new Vector3(-n.y, 0f, n.x);   // along the face, the facade's +u (SHADERS.md 8)
                Vector3 plane = new Vector3(c.x + n.x * a, 0f, c.y + n.y * a);
                // pieces aligned with the 200 m wall chunks (y = 1200 - 200 j); face 270 starts under the bridge void zone
                for (float y1 = k == Beat05Layout.FACE_270 ? Beat05Layout.CITY_TOP_270 : yTop; y1 > yBot + 1e-3f;)
                {
                    float chunkBottom = AbyssConst.SHAFT_TOP_Y - 200f * Mathf.Ceil((AbyssConst.SHAFT_TOP_Y - y1 + 1e-3f) / 200f);
                    float y0 = Mathf.Max(yBot, chunkBottom);
                    var origin = new Vector3(plane.x, y0, plane.z);
                    mb.Push(Matrix4x4.Translate(-origin));
                    if (k == Beat05Layout.FACE_270)
                        BeatKit.ZPlaneWithHoles(mb, plane.z, true, new BeatKit.Rect2(-uHalf, y0, uHalf, y1), holes, WALL_CELL);
                    else
                        MeshKit.GridQuad(mb, plane + t * uHalf + new Vector3(0f, y0, 0f), -t, Vector3.up, 2f * uHalf, y1 - y0,
                            Mathf.CeilToInt(2f * uHalf / WALL_CELL), Mathf.Max(1, Mathf.CeilToInt((y1 - y0) / WALL_CELL)));
                    mb.Pop();
                    MeshRenderer r = BeatKit.Emit(mb, city, "Facade" + k + "_" + Mathf.RoundToInt(-y0), mat, _meshes, ShadowCastingMode.Off, origin, true);
                    if (r != null) _facades.Add(r);
                    y1 = y0;
                }
            }
        }
    }
}
