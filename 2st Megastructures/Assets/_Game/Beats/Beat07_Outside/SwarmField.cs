using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// The Dyson swarm of beat 7 (Part 14.5.7, 14.7 H6, DEVELOPMENT.md ADR-008 as amended for ADR-011): 24 000 panels
    /// in three bands (2 600 / 6 600 / 14 800 at 5 / 8 / 12 km round the star, 40 km along L7), drawn by three
    /// <see cref="InstancedField"/>s in Indirect mode (three Graphics.RenderMeshIndirect calls, one per band) of one
    /// unit quad with the M10 RingSwarmPanel material. Per-panel orbit parameters live in one StructuredBuffer per band
    /// (built once at load, seeded PCG hashes, seed 1); orbits, orientation, the soft 2.2 px points, the analytic star
    /// occlusion and the 9-14 km radial remap (behind the hull, whose horizon is 3.5 km away) are all in the vertex
    /// shader, so nothing is computed or allocated per frame. Each band is a broad, structured ring: a Gaussian sheet
    /// (sigma 0.09 r) plus three thin ringlets, the 0.12 r thickness, and 8 % of the panels in two 10 deg density arcs.
    /// The fields have a fixed culling box that contains every outside camera and its 14 km remap shell.
    /// Submission, set toggling (IRingDrawable) and capture-mode submission (InstancedField.SubmitAllFor) are the shared
    /// InstancedField machinery. The star sits 40 km along L7 from the CAMERA (it is a sky element, 14.7 H3), so the
    /// swarm stays centred on the sky disc for every camera (no 0.07 degree parallax error across the platform).
    /// </summary>
    [ExecuteAlways]   // OnDestroy also runs for edit-mode builds (WorldBuildTests): the GPU buffers are always released
    public sealed class SwarmField : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PanelData
        {
            public Vector4 a;   // x phase (turns), y radial offset (r), z normal offset (r), w size scale
            public Vector4 b;   // xy base tilt (rad), z libration direction (rad), w libration cycles per 13 200 s wrap (integer)
            public Vector4 c;   // x libration phase (rad), y albedo variation, z band, w unused
        }

        public const int Stride = 48;
        public const string BufferName = "_SwarmPanels";

        // ---- facts for tests and the composition checks (filled at build time) ----
        /// <summary>Total panel count (WorldBuildTests "Swarm count=24000").</summary>
        public int PanelCount;
        public readonly int[] BandCounts = new int[3];
        /// <summary>Angular radius of the node line (the band points at the star's depth) = atan(r / 40 km).</summary>
        public readonly float[] BandRadiiDeg = new float[3];
        /// <summary>Largest apparent angular radius (perspective: the near side of a tilted band looks larger).</summary>
        public readonly float[] BandMaxDeg = new float[3];
        public readonly float[] BandMinorDeg = new float[3];
        public float MinCrossingDeg = 180f;
        public readonly List<float> CrossingAnglesDeg = new List<float>();
        public float Ks12MaxNdcX, Ks12MaxNdcY;
        public bool InsideKs12;
        /// <summary>Star-frame box (x = a right, y = b up, z = w line of sight; origin = star) holding every band.</summary>
        public Bounds StarFrameBounds;
        public readonly InstancedField[] Fields = new InstancedField[3];
        public bool GpuReady { get; private set; }

        Mesh _quad;

        /// <summary>Band frame k in the star frame of 14.5.7: w = line of sight to the star, a = right, b = up; the
        /// band normal is w rotated by tilt_k about a, then by node_k about w; e1 = the node line, e2 = n x e1.</summary>
        public static void BandFrame(int k, out Vector3 n, out Vector3 e1, out Vector3 e2)
        {
            StarFrame(out Vector3 w, out Vector3 a, out _);
            Quaternion node = Quaternion.AngleAxis(RingConstants.SwarmNode[k], w);
            n = node * (Quaternion.AngleAxis(RingConstants.SwarmTilt[k], a) * w);
            n.Normalize();
            e1 = node * a;
            e1 = (e1 - n * Vector3.Dot(e1, n)).normalized;
            e2 = Vector3.Cross(n, e1);
        }

        public static void StarFrame(out Vector3 w, out Vector3 a, out Vector3 b)
        {
            w = RingConstants.StarL7.normalized;
            a = Vector3.Cross(Vector3.up, w).normalized;
            b = Vector3.Cross(w, a);
        }

        /// <summary>Builds the panel data, GPU buffers and the three indirect fields. Safe without a GPU (batch mode,
        /// tests): the analytic facts are still computed, only the fields are skipped (with a warning).</summary>
        public void Init(BuildContext ctx)
        {
            ComputeGeometryFacts();
            var counts = RingConstants.SwarmCounts;
            PanelCount = 0;
            for (int k = 0; k < 3; k++) { BandCounts[k] = counts[k]; PanelCount += counts[k]; }

            bool device = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsIndirectArgumentsBuffer;
            Material mat = ctx != null && ctx.Assets != null ? ctx.Assets.Get(RingMat.SwarmPanel) : null;
            if (!device || mat == null)
            {
                Debug.LogWarning("[Ring] SwarmField: " + (device ? "material " + RingMat.SwarmPanel + " missing" : "no graphics device / indirect args")
                                 + "; the swarm is not drawn (facts still computed).");
                return;
            }

            _quad = BuildQuad();
            Vector3 starRel = RingConstants.StarL7.normalized * RingConstants.StarOutRange;
            var bounds = new Bounds(OutsideLayout.SwarmBoundsCentre, OutsideLayout.SwarmBoundsSize);
            for (int k = 0; k < 3; k++)
            {
                // RING_NO_BEND content: no bend inflation (bendMaxDistance 0); no shadows (Part 8.5)
                var f = InstancedField.Create(ctx, "Swarm.Band" + k, RingSet.Outside, _quad, RingMat.SwarmPanel, counts[k], bounds, 0f,
                    false, InstancedField.DrawMode.Indirect, false);
                f.SetInstanceData(GeneratePanels(k, counts[k]), BufferName);
                BandFrame(k, out Vector3 n, out Vector3 e1, out Vector3 e2);
                float rate = RingConstants.SwarmSense[k] / RingConstants.SwarmPeriods[k];   // turns / s, signed by the orbit sense
                f.SetVector("_BandC", new Vector4(starRel.x, starRel.y, starRel.z, RingConstants.SwarmRadii[k]));
                f.SetVector("_BandE1", new Vector4(e1.x, e1.y, e1.z, rate));
                f.SetVector("_BandE2", new Vector4(e2.x, e2.y, e2.z, 0f));
                f.SetVector("_BandN", new Vector4(n.x, n.y, n.z, k));
                Fields[k] = f;
                if (f.Holder != null) f.Holder.transform.SetParent(transform, false);
            }
            GpuReady = true;
        }

        static float Hash(int i, int k, int salt) => RingMath.U01(RingMath.Pcg3(i, k + 16 * OutsideLayout.SwarmSeed, salt));

        /// <summary>Deterministic panels of band k (seed 1): 92 % uniform in phase, 8 % in two 10-degree arcs
        /// (local density x 2.5); radial structure = 55 % Gaussian sheet (sigma 0.09 r) + 45 % in three ringlets
        /// (sigma 0.012 r, offsets rotated per band), plus triangular radial / normal offsets within the 0.12 r
        /// thickness, clamped to +-0.3 r; size +-15 %; Gaussian tilt sigma 18 deg; libration whose peak normal speed is
        /// the spin 0.02-0.08 rad/s, its angular frequency quantised so the swarm time can wrap at 13 200 s.</summary>
        public static PanelData[] GeneratePanels(int k, int count)
        {
            var d = new PanelData[count];
            int nArc = Mathf.RoundToInt(count * OutsideLayout.SwarmArcFraction);
            int nUni = count - nArc;
            float arc0 = Hash(0, k, 9001), arc1 = Hash(1, k, 9001);
            float arcW = OutsideLayout.SwarmArcDeg / 360f;
            float half = 0.5f * OutsideLayout.SwarmThickness;
            float sigma = OutsideLayout.SwarmTiltSigmaDeg * Mathf.Deg2Rad;
            double step = 2.0 * System.Math.PI / OutsideLayout.SwarmTimeWrap;
            for (int i = 0; i < count; i++)
            {
                float phase;
                if (i < nUni) phase = Hash(i, k, 1);
                else
                {
                    float c = ((i - nUni) & 1) == 0 ? arc0 : arc1;
                    phase = c + (Hash(i, k, 1) - 0.5f) * arcW;
                    phase -= Mathf.Floor(phase);
                }
                float radial = (Hash(i, k, 2) + Hash(i, k, 3) - 1f) * half;
                float normal = (Hash(i, k, 4) + Hash(i, k, 5) - 1f) * half;
                // broad, structured band: Gaussian sheet (with the 0.12 r thickness) or one of three thin ringlets
                // (radial sigma 0.012 r, a quarter of the thickness, so they stay crisp lanes; Box-Muller, own hashes)
                float gu1 = Mathf.Max(Hash(i, k, 13), 1e-7f), gu2 = Hash(i, k, 14);
                float gauss = Mathf.Sqrt(-2f * Mathf.Log(gu1)) * Mathf.Cos(2f * Mathf.PI * gu2);
                float pick = Hash(i, k, 15);
                if (pick < OutsideLayout.SwarmSheetFraction) radial += gauss * OutsideLayout.SwarmRadialSigma;
                else
                {
                    int nr = OutsideLayout.SwarmRinglets.Length;
                    int ri = Mathf.Min(nr - 1, (int)((pick - OutsideLayout.SwarmSheetFraction) / (1f - OutsideLayout.SwarmSheetFraction) * nr));
                    radial = OutsideLayout.SwarmRinglets[(ri + k) % nr] + gauss * OutsideLayout.SwarmRingletSigma;
                    normal *= 0.25f;
                }
                radial = Mathf.Clamp(radial, -OutsideLayout.SwarmRadialClamp, OutsideLayout.SwarmRadialClamp);
                float size = 0.85f + 0.3f * Hash(i, k, 6);
                float u1 = Mathf.Max(Hash(i, k, 7), 1e-7f), u2 = Hash(i, k, 8);
                float g = Mathf.Sqrt(-2f * Mathf.Log(u1)) * sigma;
                float tx = g * Mathf.Cos(2f * Mathf.PI * u2), ty = g * Mathf.Sin(2f * Mathf.PI * u2);
                float libDir = 2f * Mathf.PI * Hash(i, k, 9);
                float spin = Mathf.Lerp(OutsideLayout.SwarmSpinMin, OutsideLayout.SwarmSpinMax, Hash(i, k, 10));
                double omega = spin / OutsideLayout.SwarmLibAmp;
                double q = System.Math.Max(1.0, System.Math.Round(omega / step));
                float libPhase = 2f * Mathf.PI * Hash(i, k, 11);
                d[i].a = new Vector4(phase, radial, normal, size);
                d[i].b = new Vector4(tx, ty, libDir, (float)q);   // angular frequency q x 2 pi / 13 200 s
                d[i].c = new Vector4(libPhase, Hash(i, k, 12), k, 0f);
            }
            return d;
        }

        static Mesh BuildQuad()
        {
            var b = new MeshBuilder();
            int i0 = b.Vtx(new Vector3(-0.5f, -0.5f, 0f), Vector3.back, new Vector2(0f, 0f));
            int i1 = b.Vtx(new Vector3(-0.5f, 0.5f, 0f), Vector3.back, new Vector2(0f, 1f));
            int i2 = b.Vtx(new Vector3(0.5f, 0.5f, 0f), Vector3.back, new Vector2(1f, 1f));
            int i3 = b.Vtx(new Vector3(0.5f, -0.5f, 0f), Vector3.back, new Vector2(1f, 0f));
            b.Quad(i0, i1, i2, i3);
            var m = b.Build("Swarm.Quad", true);
            m.bounds = new Bounds(Vector3.zero, Vector3.one);
            return m;
        }

        // ------------------------------------------------------------------ analytic facts (logged at build)
        static Vector2 SkyDeg(Vector3 p, Vector3 eye, Vector3 w, Vector3 a, Vector3 b)
        {
            Vector3 v = p - eye;
            float z = Vector3.Dot(v, w);
            return new Vector2(Mathf.Atan2(Vector3.Dot(v, a), z) * Mathf.Rad2Deg, Mathf.Atan2(Vector3.Dot(v, b), z) * Mathf.Rad2Deg);
        }

        /// <summary>Band radii, the pairwise crossing angles of the apparent band ellipses (14.5.7: >= 20 deg), whether
        /// every band (with its 0.12 r thickness) lies inside the KS12 frame, and the star-frame bounds of the swarm.</summary>
        void ComputeGeometryFacts()
        {
            const int S = 720;
            StarFrame(out Vector3 w, out Vector3 a, out Vector3 bb);
            Vector3 eye = OutsideLayout.StarEye, star = OutsideLayout.StarPos;
            var curves = new Vector2[3][];
            for (int k = 0; k < 3; k++)
            {
                BandFrame(k, out Vector3 n, out Vector3 e1, out Vector3 e2);
                float r = RingConstants.SwarmRadii[k];
                curves[k] = new Vector2[S];
                float maxR = 0f, minR = float.MaxValue;
                for (int i = 0; i < S; i++)
                {
                    float ph = 2f * Mathf.PI * i / S;
                    Vector3 p = star + r * (Mathf.Cos(ph) * e1 + Mathf.Sin(ph) * e2);
                    curves[k][i] = SkyDeg(p, eye, w, a, bb);
                    float m = curves[k][i].magnitude;
                    maxR = Mathf.Max(maxR, m); minR = Mathf.Min(minR, m);
                }
                BandRadiiDeg[k] = curves[k][0].magnitude;   // phase 0 = e1 = the node line, at the star's depth
                BandMaxDeg[k] = maxR;
                BandMinorDeg[k] = minR;
            }
            CrossingAnglesDeg.Clear();
            MinCrossingDeg = 180f;
            for (int p = 0; p < 3; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    var A = curves[p]; var B = curves[q];
                    for (int i = 0; i < S; i++)
                    {
                        Vector2 a0 = A[i], a1 = A[(i + 1) % S];
                        for (int j = 0; j < S; j++)
                        {
                            Vector2 b0 = B[j], b1 = B[(j + 1) % S];
                            if (Mathf.Max(a0.x, a1.x) < Mathf.Min(b0.x, b1.x) || Mathf.Max(b0.x, b1.x) < Mathf.Min(a0.x, a1.x)) continue;
                            if (Mathf.Max(a0.y, a1.y) < Mathf.Min(b0.y, b1.y) || Mathf.Max(b0.y, b1.y) < Mathf.Min(a0.y, a1.y)) continue;
                            Vector2 d1 = a1 - a0, d2 = b1 - b0;
                            float den = d1.x * d2.y - d1.y * d2.x;
                            if (Mathf.Abs(den) < 1e-12f) continue;
                            Vector2 o = b0 - a0;
                            float t = (o.x * d2.y - o.y * d2.x) / den, u = (o.x * d1.y - o.y * d1.x) / den;
                            if (t < 0f || t >= 1f || u < 0f || u >= 1f) continue;
                            float cos = Mathf.Abs(Vector2.Dot(d1, d2)) / Mathf.Max(d1.magnitude * d2.magnitude, 1e-12f);
                            float ang = Mathf.Acos(Mathf.Clamp01(cos)) * Mathf.Rad2Deg;
                            CrossingAnglesDeg.Add(ang);
                            MinCrossingDeg = Mathf.Min(MinCrossingDeg, ang);
                        }
                    }
                }

            // KS12 (yaw 92, pitch +9, vFOV 66): every band (with its 0.12 r thickness and the 2 sigma radial sheet)
            // inside the frame at 16:10 (the narrowest Hor+ aspect); also the star-frame box of all band points
            // (Subject.Swarm marker)
            Vector3 f = RingConstants.DirFromAzEl(92f, 9f);
            Vector3 right = Vector3.Cross(Vector3.up, f).normalized, up = Vector3.Cross(f, right);
            float ty = Mathf.Tan(33f * Mathf.Deg2Rad), tx = ty * 1.6f;
            Ks12MaxNdcX = 0f; Ks12MaxNdcY = 0f;
            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
            float hT = 0.5f * OutsideLayout.SwarmThickness;
            float hR = hT + 2f * OutsideLayout.SwarmRadialSigma;
            for (int k = 0; k < 3; k++)
            {
                BandFrame(k, out Vector3 n, out Vector3 e1, out Vector3 e2);
                float r = RingConstants.SwarmRadii[k];
                for (int i = 0; i < S; i++)
                {
                    float ph = 2f * Mathf.PI * i / S;
                    Vector3 radial = Mathf.Cos(ph) * e1 + Mathf.Sin(ph) * e2;
                    for (int c = 0; c < 4; c++)
                    {
                        float dr = (c & 1) == 0 ? -hR : hR, dn = (c & 2) == 0 ? -hT : hT;
                        Vector3 rel = r * (1f + dr) * radial + r * dn * n;
                        Vector3 sf = new Vector3(Vector3.Dot(rel, a), Vector3.Dot(rel, bb), Vector3.Dot(rel, w));
                        mn = Vector3.Min(mn, sf); mx = Vector3.Max(mx, sf);
                        Vector3 v = (star + rel - eye).normalized;
                        float z = Vector3.Dot(v, f);
                        if (z <= 0f) { Ks12MaxNdcX = Ks12MaxNdcY = 99f; continue; }
                        Ks12MaxNdcX = Mathf.Max(Ks12MaxNdcX, Mathf.Abs(Vector3.Dot(v, right) / z) / tx);
                        Ks12MaxNdcY = Mathf.Max(Ks12MaxNdcY, Mathf.Abs(Vector3.Dot(v, up) / z) / ty);
                    }
                }
            }
            StarFrameBounds = new Bounds();
            StarFrameBounds.SetMinMax(mn, mx);
            InsideKs12 = Ks12MaxNdcX < 0.95f && Ks12MaxNdcY < 0.95f;
        }

        public string FactsLine()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("[Ring] Beat7 swarm: ").Append(PanelCount).Append(" panels (")
              .Append(BandCounts[0]).Append(" / ").Append(BandCounts[1]).Append(" / ").Append(BandCounts[2]).Append("); band radii ");
            for (int k = 0; k < 3; k++)
                sb.Append(k > 0 ? " / " : "").Append(BandRadiiDeg[k].ToString("0.00", ci))
                  .Append(" (apparent ").Append(BandMaxDeg[k].ToString("0.0", ci)).Append(" x ").Append(BandMinorDeg[k].ToString("0.0", ci)).Append(')');
            sb.Append(" deg; crossing angles");
            foreach (float c in CrossingAnglesDeg) sb.Append(' ').Append(c.ToString("0.0", ci));
            sb.Append(" deg; min ").Append(MinCrossingDeg.ToString("0.0", ci))
              .Append(MinCrossingDeg >= OutsideLayout.SwarmMinCrossDeg ? " (>= 20 OK)" : " (< 20 DEFECT)")
              .Append("; KS12 max |ndc| x ").Append(Ks12MaxNdcX.ToString("0.00", ci)).Append(" y ").Append(Ks12MaxNdcY.ToString("0.00", ci))
              .Append(InsideKs12 ? " (inside)" : " (OUTSIDE FRAME)")
              .Append(GpuReady ? "; 3 indirect draws ready" : "; GPU path off");
            return sb.ToString();
        }

        void OnDestroy()
        {
            for (int k = 0; k < 3; k++)
            {
                if (Fields[k] != null) Fields[k].Release();
                Fields[k] = null;
            }
            if (_quad != null)
            {
                if (Application.isPlaying) Destroy(_quad); else DestroyImmediate(_quad);
                _quad = null;
            }
            GpuReady = false;
        }
    }
}
