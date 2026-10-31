using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 5 full tier: the two inclined lattice guide rails (14.5.5): 4 x 6 m box lattices along the lift line at z = 283 and
    /// 289, 48 instanced 12 m segments each (persistent skeleton, 14.6), polished running rails on the platform-side chords,
    /// ties between the twin rails at every joint, a head frame tied into the hall hull above the top dock and counterweights
    /// hanging 41 m below the bottom dock.
    /// Rail-local frame (<see cref="Beat05Layout.RailRotation"/>): z along the line (down), y along the in-plane perpendicular
    /// (toward +x), x = world -z, so the platform side of a rail is +x.
    /// </summary>
    public sealed partial class Beat05_Lift
    {
        const float RAIL_CHORD_X = 1.7f, RAIL_CHORD_Y = 2.7f, RAIL_CHORD = 0.6f;   // chord centres: section 4 x 6 m overall
        const float RAIL_CHORD_END = 5.98f;                                          // chords stop 2 cm short of the 12 m segment ends
        const float RAIL_NODE = 5.7f;                                                // end nodes of the lacing
        const float RUN_X0 = 1.99f, RUN_DEPTH = 0.22f, RUN_HALF_W = 0.06f;          // polished running rail on the near chord face
        const float SHOE_CLEAR = 0.03f;
        const float SHOE_JAW_X0 = RAIL_CHORD_X + 0.5f * RAIL_CHORD + SHOE_CLEAR;   // 2.03
        const float SHOE_JAW_DEPTH = 0.27f;

        // ---------------------------------------------------------------- marker lamps (R2-11)
        /// <summary>
        /// Guide-rail marker lamps (R2-11, "the shaft bursts into view as a mini reveal", 14.6 4 to 5): one small cold lamp at the
        /// upper batten node of every 12 m segment of both rails (segment 0's node is inside the head frame: 2 x 47 lamps; the
        /// critique's "every second segment" left one lamp in the walk-on frame), on the chord corner that faces the platform and the shaft
        /// (rail-local +x, +y), 0.45 m out on a short arm. Glow sprites (no light sources, instanced like the freight cars),
        /// true diameter 0.35 m, at least 2.5 px, COLD_LAMP x <see cref="MARKER_STRENGTH"/>: two dotted lines that plunge from
        /// the dock into the fog 13.7 degrees from vertical, a lit subject at eye height on the walk-on (from the dock eye at
        /// x -150 the nearest lamp of rail 0 is 3.7 m up, 16 m away; about four lamps of the two rails lie in the yaw 322 /
        /// pitch +2 walk-on frame of GazeCues.Beat5Walk) that the ride's 0-10 s look up the guide rail continues. The other
        /// half of the mini reveal is the dock freight group straight ahead on face 210 (Beat05Layout.FreightDockGroup). Dimmed
        /// with the city's beacons while the walk is on the bridge (BRIDGE_GLOW_DIM), so the bridge's void stays empty.
        /// </summary>
        const float MARKER_DIAMETER = 0.35f, MARKER_STRENGTH = 2.5f;
        const float MARKER_X = 2.2f, MARKER_Y = 3.4f, MARKER_GLOW_OUT = 0.08f;
        InstancedBatch _markerGlow;
        int _markerCount;

        /// <summary>Rail-local position (segment frame) of a segment's marker lamp centre.</summary>
        static Vector3 MarkerLocal => new Vector3(MARKER_X + MARKER_GLOW_OUT, MARKER_Y + MARKER_GLOW_OUT, -RAIL_NODE);

        /// <summary>World centre of the marker lamp of segment i of a rail (QA / tests).</summary>
        public static Vector3 MarkerWorld(int rail, int i) => Beat05Layout.GuideSegmentCentre(rail, i) + Beat05Layout.RailRotation * MarkerLocal;

        /// <summary>Number of guide-rail marker lamps built (0 in Graybox).</summary>
        public int GuideMarkerCount => _markerCount;

        void BuildGuideRailsFull(BeatContext ctx, MeshBuilder mb)
        {
            MaterialLibrary mats = ctx.Mats;
            Transform rails = BeatKit.Group(_geo, "GuideRails");
            Quaternion rot = Beat05Layout.RailRotation;
            Vector3 d = Beat05Layout.LiftDir;
            int n = AbyssConst.GUIDE_RAIL_SEGMENTS;

            // ---- segment lattice (one mesh, instanced 2 x 48)
            BuildRailSegment(mb);
            Mesh seg = BeatKit.Bake(mb, "GuideRailSegment", _meshes);
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferMember(mb, new Vector3(RUN_X0 + 0.5f * RUN_DEPTH, s * RAIL_CHORD_Y, -5.995f), new Vector3(RUN_X0 + 0.5f * RUN_DEPTH, s * RAIL_CHORD_Y, 5.995f),
                    RUN_DEPTH, 2f * RUN_HALF_W, 0.01f, Vector3.up, true);
            Mesh run = BeatKit.Bake(mb, "GuideRunningRail", _meshes);
            // ties between the twin rails (rail 1 sits at x = -6 in rail 0's frame): two members per joint
            float x1 = -(AbyssConst.GUIDE_RAIL_Z1 - AbyssConst.GUIDE_RAIL_Z0) + RAIL_CHORD_X;
            for (int s = -1; s <= 1; s += 2)
                MeshKit.ChamferMember(mb, new Vector3(-RAIL_CHORD_X, s * RAIL_CHORD_Y, 0f), new Vector3(x1, s * RAIL_CHORD_Y, 0f), 0.34f, 0.34f, 0.02f, Vector3.up, true);
            Mesh tie = BeatKit.Bake(mb, "GuideRailTie", _meshes);

            var segB = new InstancedBatch("B05GuideSegments", seg, _steelThin, ShadowCastingMode.Off, true, 0, 200f);
            var runB = new InstancedBatch("B05GuideRunning", run, _rodThin, ShadowCastingMode.Off, true, 0, 200f);
            var tieB = new InstancedBatch("B05GuideTies", tie, _steelThin, ShadowCastingMode.Off, true, 0, 200f);
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < n; i++)
                {
                    Matrix4x4 m = Matrix4x4.TRS(Beat05Layout.GuideSegmentCentre(r, i), rot, Vector3.one);
                    segB.Add(m);
                    if (r == 0) runB.Add(m);   // the shoes run on the first rail only
                }
            // ties at the lower batten node of every segment but the last (0.3 m above each joint)
            for (int i = 1; i < n; i++)
                tieB.Add(Matrix4x4.TRS(Beat05Layout.GuideRailTop(0) + d * (Beat05Layout.GUIDE_SEG * i - (6f - RAIL_NODE)), rot, Vector3.one));
            Register(segB);
            Register(runB);
            Register(tieB);
            _guideSegments = n;

            // ---- marker lamps (R2-11): arm and housing (ThinLine steel) instanced with the segments, Glow sprites per lamp
            {
                float z = -RAIL_NODE;
                var corner = new Vector3(RAIL_CHORD_X + 0.5f * RAIL_CHORD - 0.05f, RAIL_CHORD_Y + 0.5f * RAIL_CHORD - 0.05f, z);
                var lamp = new Vector3(MARKER_X, MARKER_Y, z);
                MeshKit.ChamferMember(mb, corner, lamp, 0.08f, 0.08f, 0.01f, Vector3.forward, true);
                MeshKit.ChamferMember(mb, lamp - new Vector3(0.11f, 0f, 0f), lamp + new Vector3(0.11f, 0f, 0f), 0.2f, 0.2f, 0.02f, Vector3.forward, true);
                Mesh marker = BeatKit.Bake(mb, "GuideMarker", _meshes);
                var markB = new InstancedBatch("B05GuideMarkers", marker, _steelThin, ShadowCastingMode.Off, true, 0, 200f);
                _markerGlow = new InstancedBatch("B05GuideMarkerGlows", GlowQuad(), _glowMarker, ShadowCastingMode.Off, false, 0, 200f);
                var glowScale = new Vector3(MARKER_DIAMETER, MARKER_DIAMETER, MARKER_DIAMETER);
                for (int r = 0; r < 2; r++)
                    for (int i = 1; i < n; i++)   // segment 0's upper node lies inside the head frame
                    {
                        markB.Add(Matrix4x4.TRS(Beat05Layout.GuideSegmentCentre(r, i), rot, Vector3.one));
                        _markerGlow.Add(Matrix4x4.TRS(MarkerWorld(r, i), Quaternion.identity, glowScale));
                    }
                Register(markB);
                Register(_markerGlow);
                _markerCount = _markerGlow.Count;
            }

            // ---- head frame above the top dock (the rails' upper ends are buried in it; two girders tie it into the hull's -X wall)
            Vector3 t0 = Beat05Layout.GuideRailTop(0), t1 = Beat05Layout.GuideRailTop(1);
            Vector3 head = 0.5f * (t0 + t1) + Vector3.up * 0.4f;
            MeshKit.ChamferBox(mb, head, new Vector3(8.5f, 3.4f, 13.5f), 0.05f);
            float hx = head.x + 4.25f - 0.3f;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = head.z + s * 4.5f;
                MeshKit.IBeam(mb, new Vector3(hx, head.y + 0.4f, z), new Vector3(AbyssConst.HALL_X1 + 0.4f, head.y + 0.4f, z), 2.0f, 0.9f, 0.06f, 0.08f, Vector3.up);
                // diagonal hanger from the hull wall up to the girder (reads as a cantilever bracket)
                MeshKit.IBeam(mb, new Vector3(AbyssConst.HALL_X1 + 0.3f, head.y - 9f, z), new Vector3(AbyssConst.HALL_X1 - 9f, head.y - 0.4f, z), 0.9f, 0.5f, 0.04f, 0.06f, Vector3.up);
            }
            BeatKit.Emit(mb, rails, "GuideHeadFrame", _steelDeck, _meshes, ShadowCastingMode.On);

            // ---- counterweights hanging below the bottom ends (concrete blocks with a steel yoke)
            Concrete(mb, Beat05Layout.GuideRailTop(0), d, rot);
            Concrete(mb, Beat05Layout.GuideRailTop(1), d, rot);
            BeatKit.Emit(mb, rails, "GuideCounterweights", mats[MatId.ConcreteBoard], _meshes, ShadowCastingMode.Off);
            for (int r = 0; r < 2; r++)
            {
                Vector3 end = Beat05Layout.GuideRailTop(r) + d * Beat05Layout.GuideRailLength;
                MeshKit.ChamferBox(mb, end + d * 0.45f, new Vector3(4.6f, 6.6f, 1.3f), 0.04f, rot);
            }
            BeatKit.Emit(mb, rails, "GuideYokes", _steelDeck, _meshes, ShadowCastingMode.Off);
        }

        static void Concrete(MeshBuilder mb, Vector3 railTop, Vector3 d, Quaternion rot)
        {
            Vector3 end = railTop + d * Beat05Layout.GuideRailLength;
            MeshKit.ChamferBox(mb, end + d * 5.5f, new Vector3(5.5f, 7.5f, 9f), 0.08f, rot);
        }

        /// <summary>
        /// One 12 m lattice segment centred on its midpoint: four 0.6 m chords, batten rings at the end nodes, Warren lacing on all
        /// four faces (alternating 0.22 / 0.20 sections so two diagonals never share a face plane at a node) and one diaphragm
        /// diagonal. 29 closed members (1 276 triangles), all ThinLine.
        /// </summary>
        static void BuildRailSegment(MeshBuilder mb)
        {
            Vector3 up = Vector3.up;
            float cx = RAIL_CHORD_X, cy = RAIL_CHORD_Y, e = RAIL_CHORD_END;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    MeshKit.ChamferMember(mb, new Vector3(sx * cx, sy * cy, -e), new Vector3(sx * cx, sy * cy, e), RAIL_CHORD, RAIL_CHORD, 0.02f, up, true);

            // battens at both end nodes
            for (int k = -1; k <= 1; k += 2)
            {
                float z = k * RAIL_NODE;
                MeshKit.ChamferMember(mb, new Vector3(-cx, cy, z), new Vector3(cx, cy, z), 0.25f, 0.25f, 0.01f, Vector3.forward, true);
                MeshKit.ChamferMember(mb, new Vector3(-cx, -cy, z), new Vector3(cx, -cy, z), 0.25f, 0.25f, 0.01f, Vector3.forward, true);
                MeshKit.ChamferMember(mb, new Vector3(cx, -cy, z), new Vector3(cx, cy, z), 0.25f, 0.25f, 0.01f, Vector3.forward, true);
                MeshKit.ChamferMember(mb, new Vector3(-cx, -cy, z), new Vector3(-cx, cy, z), 0.25f, 0.25f, 0.01f, Vector3.forward, true);
            }

            // Warren lacing, 4 bays between the end nodes
            const int bays = 4;
            float bay = 2f * RAIL_NODE / bays;
            for (int i = 0; i < bays; i++)
            {
                float z0 = -RAIL_NODE + bay * i, z1 = z0 + bay;
                float w = (i & 1) == 0 ? 0.22f : 0.20f;
                float flip = (i & 1) == 0 ? 1f : -1f;
                // faces y = +/-cy (4 m wide, across x)
                MeshKit.ChamferMember(mb, new Vector3(-flip * cx, cy, z0), new Vector3(flip * cx, cy, z1), w, w, 0.01f, up, true);
                MeshKit.ChamferMember(mb, new Vector3(-flip * cx, -cy, z0), new Vector3(flip * cx, -cy, z1), w, w, 0.01f, up, true);
                // faces x = +/-cx (6 m deep, across y)
                MeshKit.ChamferMember(mb, new Vector3(cx, -flip * cy, z0), new Vector3(cx, flip * cy, z1), w, w, 0.01f, Vector3.right, true);
                MeshKit.ChamferMember(mb, new Vector3(-cx, -flip * cy, z0), new Vector3(-cx, flip * cy, z1), w, w, 0.01f, Vector3.right, true);
            }
            MeshKit.ChamferMember(mb, new Vector3(-cx, -cy, 0f), new Vector3(cx, cy, 0f), 0.18f, 0.18f, 0.01f, Vector3.forward, true);
        }
    }
}
