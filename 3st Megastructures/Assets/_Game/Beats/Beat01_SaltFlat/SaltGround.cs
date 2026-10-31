// SaltGround: the PolarGrid salt flat (MASTER_PROMPT 14.5.1 a, 14.2.3 M01) and the updater that keeps it under
// the camera. 64 sectors x 9 rings (r = 6 ... 14 000 m) plus the centre, split into four ring groups. Each group is
// its own mesh that follows the camera in XZ snapped to its own cell (2.4 / 24 / 240 / 2 400 m, the salt crack cell
// sizes of the level it carries), so nothing per-vertex can swim against the world-space crack pattern. Every outer
// group extends inward by one snap cell, which keeps the plane covered whatever the snapped offsets are; the overlap
// is identical colour and sits 1 mm lower per group so it can never z-fight. The ground colour reaching the fog
// colour at 14 km is the shader's job (14.7.2).
using UnityEngine;

namespace Megastructure.Beats
{
    public static class SaltGround
    {
        /// <summary>Sectors of the polar grid (14.5.1 a, MASTER_PROMPT.md line 643).</summary>
        public const int Sectors = 64;
        /// <summary>Ring radii in metres (14.5.1 a, line 643).</summary>
        public static readonly float[] RingRadii = { 6f, 15f, 40f, 100f, 250f, 600f, 1800f, 5000f, 14000f };
        /// <summary>Snap cell per group = crack cell of the matching level (14.12 line 1012, 14.5.1 a).</summary>
        public static readonly float[] GroupSnap = { 2.4f, 24f, 240f, 2400f };
        /// <summary>Index into RingRadii of the outermost ring of each group (groups: r <= 40, <= 600, <= 5 000, the outermost).</summary>
        public static readonly int[] GroupLastRing = { 2, 5, 7, 8 };
        /// <summary>Downward offset per group for the coplanar overlap (task B04: 1 mm).</summary>
        public const float GroupDrop = 0.001f;

        public static int GroupCount => GroupSnap.Length;

        /// <summary>Radii of the rings a group's mesh is made of (index 0 is its inner edge, 0 for the centre disc).</summary>
        public static float[] GroupRings(int g)
        {
            int first = g == 0 ? 0 : GroupLastRing[g - 1];
            int last = GroupLastRing[g];
            var r = new float[last - first + 2];
            // group 0 starts at the centre; every outer group starts one snap cell inside the previous group's edge
            r[0] = g == 0 ? 0f : RingRadii[first] - GroupSnap[g];
            for (int i = first; i <= last; i++) r[i - first + 1] = RingRadii[i];
            return r;
        }

        public static float GroupInnerRadius(int g) => GroupRings(g)[0];
        public static float GroupOuterRadius(int g) => RingRadii[GroupLastRing[g]];
        public static float GroupY(int g) => -GroupDrop * g;

        /// <summary>Flat polar grid of one group around the local origin, up-facing, vertex colour white with B = 0 (no sway).</summary>
        public static MeshBuilder BuildGroup(int g)
        {
            float[] radii = GroupRings(g);
            var mb = new MeshBuilder(radii.Length * Sectors + 1);
            var up = new Vector3(0, 1, 0);
            var col = new Color(1f, 1f, 0f, 1f);
            int ringStart = 0;
            int centre = -1;
            if (radii[0] <= 0f)
            {
                centre = mb.Add(Vector3.zero, up, Vector2.zero, col);
                ringStart = 1;
            }
            var firstIndex = new int[radii.Length];
            for (int i = ringStart; i < radii.Length; i++)
            {
                firstIndex[i] = mb.VertexCount;
                for (int j = 0; j < Sectors; j++)
                {
                    float a = j * Mathf.PI * 2f / Sectors;
                    var p = new Vector3(Mathf.Sin(a) * radii[i], 0f, Mathf.Cos(a) * radii[i]);
                    mb.Add(p, up, new Vector2(p.x, p.z), col);
                }
            }
            // angles grow from +Z toward +X; (centre, P_j, P_j+1) and (I_j, O_j, O_j+1) face +Y
            if (centre >= 0)
                for (int j = 0; j < Sectors; j++)
                    mb.Tri(centre, firstIndex[1] + j, firstIndex[1] + (j + 1) % Sectors);
            for (int i = ringStart; i + 1 < radii.Length; i++)
                for (int j = 0; j < Sectors; j++)
                {
                    int i0 = firstIndex[i] + j, i1 = firstIndex[i] + (j + 1) % Sectors;
                    int o0 = firstIndex[i + 1] + j, o1 = firstIndex[i + 1] + (j + 1) % Sectors;
                    mb.Tri(i0, o0, o1);
                    mb.Tri(i0, o1, i1);
                }
            return mb;
        }

        /// <summary>Snapped centre of a group for a camera position (nearest multiple of the group cell).</summary>
        public static float Snap(float v, float cell) => Mathf.Floor(v / cell + 0.5f) * cell;

        /// <summary>
        /// True when the XZ point p (ground-group local) is covered by the group mesh centred at c: inside the
        /// outer polygon and outside the inner hole (both are 64-gons; tested against their inscribed / circumscribed
        /// circles, which is conservative).
        /// </summary>
        public static bool Covers(int g, Vector2 c, Vector2 p)
        {
            float d = (p - c).magnitude;
            float inscribed = GroupOuterRadius(g) * Mathf.Cos(Mathf.PI / Sectors);
            float inner = GroupInnerRadius(g);
            return d <= inscribed && (inner <= 0f || d >= inner);
        }
    }

    /// <summary>Moves every ring group under the camera (XZ, snapped per group). Allocation free.</summary>
    public sealed class SaltGroundFollower : IWorldUpdater
    {
        readonly Transform[] m_groups;
        readonly float[] m_lastX, m_lastZ;
        readonly Transform m_space;

        public SaltGroundFollower(Transform space, Transform[] groups)
        {
            m_space = space;
            m_groups = groups;
            m_lastX = new float[groups.Length];
            m_lastZ = new float[groups.Length];
            for (int i = 0; i < groups.Length; i++) { m_lastX[i] = float.NaN; m_lastZ[i] = float.NaN; }
        }

        public void OnFrame(in FrameState f)
        {
            if (!f.groundActive) return;
            Vector3 cam = m_space != null ? m_space.InverseTransformPoint(f.cameraPos) : f.cameraPos;
            for (int i = 0; i < m_groups.Length; i++)
            {
                Transform t = m_groups[i];
                if (t == null) continue;
                float cell = SaltGround.GroupSnap[i];
                float x = SaltGround.Snap(cam.x, cell), z = SaltGround.Snap(cam.z, cell);
                if (x == m_lastX[i] && z == m_lastZ[i]) continue;
                m_lastX[i] = x; m_lastZ[i] = z;
                t.localPosition = new Vector3(x, SaltGround.GroupY(i), z);
            }
        }
    }
}
