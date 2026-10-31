using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Geometry of beat 2 (MASTER_PROMPT 14.5.2, 14.5.4 for P2): the path strip with kerbs and the two spurs, the 20
    /// valley bollards, the benches, the arch bridge (cos-profile deck, single-span arch, abutments, 82 posts, round
    /// rails, 8 lamp posts with light pools), the belvedere P1 and pavilion P2 (one rotunda generator) and the
    /// 80-arch aqueduct. Pure maths, also run by the headless geometry self-test.
    /// </summary>
    internal static class ValleyGeo
    {
        // ================================================================ path (14.5.2: SweepStrip 3.0 m, kerbs 0.14 m, +0.012 m)
        public const float PathTop = 0.012f;
        public const float StripHalf = 1.5f, KerbW = 0.14f, KerbRise = 0.05f;
        public const float SpurHalf = 1.0f, SpurKerbRise = 0.045f;     // spurs 2.0 m; kerb tops 5 mm lower: never co-planar
        const float KerbBury = 0.05f, KerbCh = 0.012f, KerbChOut = 0.01f;
        public const float PathStartZ = 29.95f;                          // emerges from under the pad's 0.12 m chamfer
        public const float PathEndZ = 558.2f;                            // runs 0.2 m into the levee riser (under its sill)
        const float Station = 4f;

        /// <summary>Main legs (z ranges of the straight path x = 0), not on the bridge.</summary>
        public static readonly Vector2[] MainRuns = { new Vector2(PathStartZ, RingConstants.BridgeZ0), new Vector2(RingConstants.BridgeZ1, PathEndZ) };

        static float Ground(float x, float z) => RingWorld.Height(x, z, 0f);

        public static void PathStrip(MeshBuilder strip, MeshBuilder kerbs)
        {
            // main strip surface and the right kerb, per run
            foreach (var run in MainRuns)
            {
                var zs = new List<float>(B12Geo.Range(run.x, run.y, Station));
                for (int i = 0; i < zs.Count - 1; i++)
                {
                    float za = zs[i], zb = zs[i + 1];
                    Vector3 a = new Vector3(-StripHalf, Ground(-StripHalf, za) + PathTop, za), b = new Vector3(StripHalf, Ground(StripHalf, za) + PathTop, za);
                    Vector3 c = new Vector3(StripHalf, Ground(StripHalf, zb) + PathTop, zb), d = new Vector3(-StripHalf, Ground(-StripHalf, zb) + PathTop, zb);
                    B12Geo.QuadN(strip, a, b, c, d, Vector3.up);
                }
                // end faces (so the 12 mm edge never shows the ground through a gap)
                foreach (float z in new[] { run.x, run.y })
                {
                    Vector3 n = z == run.x ? Vector3.back : Vector3.forward;
                    B12Geo.QuadN(strip, new Vector3(-StripHalf, -0.03f, z), new Vector3(StripHalf, -0.03f, z), new Vector3(StripHalf, PathTop, z), new Vector3(-StripHalf, PathTop, z), n);
                }
                KerbRun(kerbs, +1, run.x, run.y);
            }
            // left kerb with the junction gaps of the two spurs (PathSegments 12 and 13)
            KerbRun(kerbs, -1, MainRuns[0].x, MainRuns[0].y);
            float zl = MainRuns[1].x;
            foreach (int si in new[] { 12, 13 })
            {
                SpurGap(RingWorld.PathSegments[si], out float zA, out float zB);
                KerbRun(kerbs, -1, zl, zA);
                zl = zB;
            }
            KerbRun(kerbs, -1, zl, MainRuns[1].y);
            // spurs (2.0 m, kerbs)
            foreach (int si in new[] { 12, 13 }) Spur(strip, kerbs, RingWorld.PathSegments[si]);
        }

        static Vector2[] KerbProfile(float inner, float rise)
        {
            float o = inner + KerbW, top = PathTop + rise;
            // counter-clockwise in (side, up): outer bottom, outer face, chamfers, top, inner face down to the strip
            return new[]
            {
                new Vector2(o, -KerbBury), new Vector2(o, top - KerbChOut), new Vector2(o - KerbChOut, top),
                new Vector2(inner + KerbCh, top), new Vector2(inner, top - KerbCh), new Vector2(inner, PathTop)
            };
        }

        static void KerbRun(MeshBuilder mb, int side, float z0, float z1)
        {
            if (z1 - z0 < 0.05f) return;
            var zs = new List<float>(B12Geo.Range(z0, z1, Station));
            var path = new List<Vector3>(zs.Count);
            var sd = new List<Vector3>(zs.Count);
            foreach (var z in zs) { path.Add(new Vector3(0f, 0f, z)); sd.Add(new Vector3(side, 0f, 0f)); }
            B12Geo.Sweep(mb, path, sd, KerbProfile(StripHalf, KerbRise), false, true, true);
        }

        /// <summary>Spur frame: start A, unit direction d (xz), right r (xz), length L.</summary>
        static void SpurFrame(Vector4 seg, out Vector2 a, out Vector2 d, out Vector2 r, out float len)
        {
            a = new Vector2(seg.x, seg.y);
            Vector2 b = new Vector2(seg.z, seg.w);
            len = (b - a).magnitude;
            d = (b - a) / Mathf.Max(len, 1e-4f);
            r = new Vector2(d.y, -d.x);        // Cross(up, d) in (x, z)
        }

        /// <summary>Along-spur distance t where the line A + w r + t d reaches the main strip edge x = xe.</summary>
        static float TStart(Vector2 a, Vector2 d, Vector2 r, float w, float xe) => (xe - a.x - w * r.x) / (Mathf.Abs(d.x) > 1e-4f ? d.x : -1e-4f);

        /// <summary>
        /// z range of the gap in the main left kerb at a spur junction: from the spur's south outer kerb line to its north
        /// outer kerb line, evaluated on both faces of the main kerb (x -1.5 and -1.64) and taken 5 mm inside, so the main
        /// kerb runs a few millimetres into each spur kerb (its top is 5 mm higher: an overlap, never a slit).
        /// </summary>
        static void SpurGap(Vector4 seg, out float zA, out float zB)
        {
            SpurFrame(seg, out var a, out var d, out var r, out _);
            float wo = SpurHalf + KerbW;
            zA = float.MinValue; zB = float.MaxValue;
            foreach (float xe in new[] { -StripHalf, -(StripHalf + KerbW) })
            {
                float zs = a.y - wo * r.y + TStart(a, d, r, -wo, xe) * d.y;     // south outer line (w = -wo)
                float zn = a.y + wo * r.y + TStart(a, d, r, wo, xe) * d.y;      // north outer line (w = +wo)
                zA = Mathf.Max(zA, Mathf.Min(zs, zn));
                zB = Mathf.Min(zB, Mathf.Max(zs, zn));
            }
            zA += 0.005f; zB -= 0.005f;
        }

        static void Spur(MeshBuilder strip, MeshBuilder kerbs, Vector4 seg)
        {
            SpurFrame(seg, out var a, out var d, out var r, out float len);
            float xe = -StripHalf;
            float tmin = Mathf.Max(TStart(a, d, r, -SpurHalf - KerbW, xe), TStart(a, d, r, SpurHalf + KerbW, xe));
            var ts = new List<float>();
            foreach (var t in B12Geo.Range(tmin + 0.5f, len, Station)) ts.Add(t);
            // station 0 is skewed: every lateral line starts on x = -1.5
            System.Func<int, float, float> T = (k, w) => k == 0 ? TStart(a, d, r, w, xe) : ts[k - 1];
            int ns = ts.Count + 1;
            Vector3 P(int k, float w, float y)
            {
                float t = T(k, w);
                Vector2 q = a + r * w + d * t;
                return new Vector3(q.x, y, q.y);
            }
            // surface
            for (int k = 0; k < ns - 1; k++)
            {
                Vector3 p0 = P(k, -SpurHalf, 0f), p1 = P(k, SpurHalf, 0f), p2 = P(k + 1, SpurHalf, 0f), p3 = P(k + 1, -SpurHalf, 0f);
                p0.y = Ground(p0.x, p0.z) + PathTop; p1.y = Ground(p1.x, p1.z) + PathTop; p2.y = Ground(p2.x, p2.z) + PathTop; p3.y = Ground(p3.x, p3.z) + PathTop;
                B12Geo.QuadN(strip, p0, p1, p2, p3, Vector3.up);
            }
            {
                Vector3 e0 = P(ns - 1, -SpurHalf, -0.03f), e1 = P(ns - 1, SpurHalf, -0.03f);
                B12Geo.QuadN(strip, e0, e1, new Vector3(e1.x, PathTop, e1.z), new Vector3(e0.x, PathTop, e0.z), new Vector3(d.x, 0f, d.y));
            }
            // kerbs: right (+w) profile as the main kerb; left mirrored and reversed (keeps counter-clockwise)
            var right = KerbProfile(SpurHalf, SpurKerbRise);
            var left = new Vector2[right.Length];
            for (int i = 0; i < right.Length; i++) left[right.Length - 1 - i] = new Vector2(-right[i].x, right[i].y);
            Vector3 r3 = new Vector3(r.x, 0f, r.y), d3 = new Vector3(d.x, 0f, d.y);
            foreach (var prof in new[] { right, left })
            {
                int np = prof.Length;
                for (int e = 0; e < np - 1; e++)
                {
                    Vector2 p0 = prof[e], p1 = prof[e + 1];
                    Vector2 ed = p1 - p0;
                    Vector2 en = new Vector2(ed.y, -ed.x).normalized;
                    Vector3 n = (r3 * en.x + Vector3.up * en.y).normalized;
                    for (int k = 0; k < ns - 1; k++)
                        B12Geo.QuadN(kerbs, P(k, p0.x, p0.y), P(k, p1.x, p1.y), P(k + 1, p1.x, p1.y), P(k + 1, p0.x, p0.y), n);
                }
                var cap = new List<Vector3>(np);
                for (int i = 0; i < np; i++) cap.Add(P(0, prof[i].x, prof[i].y));
                B12Geo.Polygon(kerbs, cap, Vector3.right);
                cap.Clear();
                for (int i = 0; i < np; i++) cap.Add(P(ns - 1, prof[i].x, prof[i].y));
                B12Geo.Polygon(kerbs, cap, d3);
            }
        }

        // ================================================================ bollards (20, 1.0 m, every 12 m, alternate sides 2.2 m)
        public const int BollardCount = 20;
        const float BollardS0 = 30f, BollardPitch = 12f, BollardX = 2.2f;

        /// <summary>Bollard i: s 30 + 12 k, skipping the bridge span (z 116..184) and continuing the 12 m rhythm beyond
        /// it, sides alternating (even left).</summary>
        public static Vector3 BollardPos(int i)
        {
            int k = 0, n = -1;
            float z = 0f;
            while (n < i)
            {
                z = BollardS0 + BollardPitch * k++;
                if (z > RingConstants.BridgeZ0 - 4f && z < RingConstants.BridgeZ1 + 4f) continue;
                n++;
            }
            float x = (i % 2 == 0) ? -BollardX : BollardX;
            return new Vector3(x, Ground(x, z), z);
        }

        public static void Bollards(MeshBuilder shaft, MeshBuilder lens, MeshBuilder cap)
        {
            // same proportions as the beat-4 bollards (shaft 0.16, lens 0.15, PaleGold cap 0.19, top 1.0 m)
            for (int i = 0; i < BollardCount; i++)
            {
                var p = BollardPos(i);
                float g = p.y;
                B12Geo.Box(shaft, new Vector3(p.x, g + 0.38f, p.z), new Vector3(0.16f, 0.96f, 0.16f), 0.015f);
                B12Geo.Box(lens, new Vector3(p.x, g + 0.9035f, p.z), new Vector3(0.15f, 0.097f, 0.15f), 0.006f);
                B12Geo.Box(cap, new Vector3(p.x, g + 0.974f, p.z), new Vector3(0.19f, 0.052f, 0.19f), 0.012f);
            }
        }

        // ================================================================ benches (0.45 m)
        public const float BenchH = 0.45f;
        /// <summary>Bridge benches (2, at the bridge ends beside the corridor) and path benches; x beyond the meadow walls.</summary>
        public static readonly Vector2[] BridgeBenches = { new Vector2(-6.6f, 114f), new Vector2(6.6f, 186f) };
        public static readonly Vector2[] PathBenches = { new Vector2(-6.6f, 66f), new Vector2(6.6f, 100f), new Vector2(-6.6f, 234f), new Vector2(6.6f, 262f) };

        public static void Bench(MeshBuilder mb, Vector2 xz)
        {
            float g = Mathf.Min(Ground(xz.x, xz.y - 0.8f), Ground(xz.x, xz.y + 0.8f));
            float gTop = Mathf.Max(Ground(xz.x, xz.y - 0.8f), Ground(xz.x, xz.y + 0.8f));
            float seat = gTop + BenchH;
            B12Geo.Box(mb, new Vector3(xz.x, seat - 0.035f, xz.y), new Vector3(0.45f, 0.07f, 1.8f), 0.015f);
            for (int s = -1; s <= 1; s += 2)
            {
                float y0 = g - 0.08f, y1 = seat - 0.06f;
                B12Geo.Box(mb, new Vector3(xz.x, 0.5f * (y0 + y1), xz.y + s * 0.68f), new Vector3(0.38f, y1 - y0, 0.12f), 0.012f);
            }
        }

        // ================================================================ bridge (ArchBridgeBuilder, 14.5.2 / 14.4 P2)
        public const float DeckHalf = 0.5f * RingConstants.BridgeDeckW;         // 2.5
        public const float DeckThick = 0.5f;
        const float DeckLift = PathTop;                                          // 12 mm at the ends, 0 at the crest
        const float RailX = DeckHalf - 0.08f, PostW = 0.08f, PostPitch = 1.5f, RailR = 0.05f;
        public const int PostsPerSide = 41;                                      // 82 posts
        const float AbutHalfX = 3.0f, AbutLen = 6.5f, AbutBottom = -4.2f;
        const float ArchHalfX = 2.0f, ArchSpring = -1.0f, ArchCrown = 2.2f, ArchThick = 0.6f;
        public static readonly float[] LampZ = { 122f, 138f, 162f, 178f };
        const float LampX = DeckHalf + 0.28f;
        /// <summary>Lamp lens centre above the deck (m): middle of the 2.90..3.08 lens under the 3.2 m cap (14.5.2 lamp
        /// posts 3.2 m); the light-pool lamp height and the halo centre.</summary>
        const float LensCentreH = 2.99f;

        /// <summary>Analytic deck y = 1.5 (1 + cos(pi (z - 150) / 30)), crest 3.0 at z 150 (14.4 P2).</summary>
        public static float BridgeY(float z)
        {
            float half = 0.5f * (RingConstants.BridgeZ1 - RingConstants.BridgeZ0);
            float x = Mathf.Clamp((z - RingConstants.BridgeCrestZ) / half, -1f, 1f);
            return 0.5f * RingConstants.BridgeCrest * (1f + Mathf.Cos(Mathf.PI * x));
        }

        /// <summary>The visible deck top: the analytic profile plus a lift that is 12 mm at the ends (flush with the path
        /// strip, never within a millimetre of the flat ground) and exactly 0 at the crest (crest 3.000).</summary>
        public static float DeckY(float z) => BridgeY(z) + DeckLift * (1f - BridgeY(z) / RingConstants.BridgeCrest);

        static List<float> DeckStations(float z0, float z1, float step)
        {
            var l = new List<float>(B12Geo.Range(z0, z1, step));
            if (RingConstants.BridgeCrestZ > z0 && RingConstants.BridgeCrestZ < z1) l.Add(RingConstants.BridgeCrestZ);
            return new List<float>(B12Geo.Merge(l, 0.01f));
        }

        public static void Deck(MeshBuilder top, MeshBuilder slab, MeshBuilder collider)
        {
            var zs = DeckStations(RingConstants.BridgeZ0, RingConstants.BridgeZ1, 0.5f);
            const float ch = 0.025f;
            for (int i = 0; i < zs.Count - 1; i++)
            {
                float za = zs[i], zb = zs[i + 1], ta = DeckY(za), tb = DeckY(zb);
                Vector3 tn = new Vector3(0f, zb - za, -(tb - ta)).normalized;
                B12Geo.QuadN(top, new Vector3(-DeckHalf + ch, ta, za), new Vector3(DeckHalf - ch, ta, za), new Vector3(DeckHalf - ch, tb, zb), new Vector3(-DeckHalf + ch, tb, zb), tn);
            }
            // slab without its top (chamfers, sides, bottom, ends): a prism whose top is replaced by the walkway mesh
            var tmp = new MeshBuilder();
            B12Geo.TopFollowPrism(tmp, 0f, DeckHalf, zs, DeckY, z => DeckY(z) - DeckThick, ch, true, true);
            DropTop(slab, tmp, ch);
            B12Geo.TopFollowPrism(collider, 0f, DeckHalf, zs, DeckY, z => DeckY(z) - 0.3f, 0f, true, true);
        }

        /// <summary>Copies src without the triangles of the walkway top (all vertices on the top and |x| &lt;= half - ch).</summary>
        static void DropTop(MeshBuilder dst, MeshBuilder src, float ch)
        {
            for (int t = 0; t + 2 < src.I.Count; t += 3)
            {
                int a = src.I[t], b = src.I[t + 1], c = src.I[t + 2];
                bool onTop = true;
                foreach (int v in new[] { a, b, c })
                {
                    var p = src.V[v];
                    if (Mathf.Abs(p.y - DeckY(p.z)) > 1e-4f || Mathf.Abs(p.x) > DeckHalf - ch + 1e-4f) { onTop = false; break; }
                }
                if (onTop && src.N[a].y > 0.9f) continue;
                dst.Tri(dst.Vtx(src.V[a], src.N[a], src.UV[a], src.C[a]), dst.Vtx(src.V[b], src.N[b], src.UV[b], src.C[b]), dst.Vtx(src.V[c], src.N[c], src.UV[c], src.C[c]));
            }
        }

        public static void Abutments(MeshBuilder mb)
        {
            float a0 = RingConstants.BridgeCrestZ - 0.5f * RingConstants.BridgeSpan;   // 132
            float a1 = RingConstants.BridgeCrestZ + 0.5f * RingConstants.BridgeSpan;   // 168
            System.Func<float, float> top = z => DeckY(z) - DeckThick + 0.1f;
            System.Func<float, float> bot = z => AbutBottom;
            B12Geo.TopFollowPrism(mb, 0f, AbutHalfX, DeckStations(a0 - AbutLen, a0, 0.5f), top, bot, 0.03f, true, true);
            B12Geo.TopFollowPrism(mb, 0f, AbutHalfX, DeckStations(a1, a1 + AbutLen, 0.5f), top, bot, 0.03f, true, true);
        }

        static float ArchCentre(float z)
        {
            float u = (z - RingConstants.BridgeCrestZ) / (0.5f * RingConstants.BridgeSpan);
            return ArchSpring + (ArchCrown - ArchSpring) * (1f - u * u);
        }

        public static void Arch(MeshBuilder mb)
        {
            float a0 = RingConstants.BridgeCrestZ - 0.5f * RingConstants.BridgeSpan - 0.2f, a1 = RingConstants.BridgeCrestZ + 0.5f * RingConstants.BridgeSpan + 0.2f;
            System.Func<float, float> ext = z => Mathf.Min(ArchCentre(z) + 0.5f * ArchThick, DeckY(z) - DeckThick + 0.05f);
            System.Func<float, float> intr = z => ArchCentre(z) - 0.5f * ArchThick;
            B12Geo.TopFollowPrism(mb, 0f, ArchHalfX, DeckStations(a0, a1, 0.5f), ext, intr, 0.03f, true, true);
            // spandrel posts between the arch and the deck
            foreach (float z in new[] { 134.5f, 137.0f, 163.0f, 165.5f })
                for (int s = -1; s <= 1; s += 2)
                {
                    float y0 = ArchCentre(z) + 0.5f * ArchThick - 0.06f, y1 = DeckY(z) - DeckThick + 0.05f;
                    if (y1 - y0 < 0.2f) continue;
                    B12Geo.Box(mb, new Vector3(s * 1.4f, 0.5f * (y0 + y1), z), new Vector3(0.3f, y1 - y0, 0.3f), 0.02f);
                }
        }

        /// <summary>82 posts (0.08 m, every 1.5 m), round rails 0.10 m with the top at 1.0 m above the deck, plus a
        /// mid rail; one merged mesh (Bridge.Rail).</summary>
        public static void Rails(MeshBuilder mb)
        {
            float z0 = RingConstants.BridgeZ0 + 0.1f, z1 = RingConstants.BridgeZ1 - 0.1f;
            float topC = RingConstants.RailH - RailR;     // rail centre 0.95 above the deck: top exactly 1.00
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * RailX;
                for (int i = 0; i < PostsPerSide; i++)
                {
                    float z = Mathf.Lerp(z0, z1, i / (float)(PostsPerSide - 1));
                    float y0 = DeckY(z) - 0.03f, y1 = DeckY(z) + topC;
                    B12Geo.BoxMinMax(mb, new Vector3(x - 0.5f * PostW, y0, z - 0.5f * PostW), new Vector3(x + 0.5f * PostW, y1, z + 0.5f * PostW), 0.01f);
                }
                foreach (float h in new[] { topC, 0.45f })
                {
                    var path = new List<Vector3>();
                    foreach (var z in DeckStations(z0, z1, 1.0f)) path.Add(new Vector3(x, DeckY(z) + h, z));
                    B12Geo.Tube(mb, path, RailR, 12, true);
                }
            }
        }

        /// <summary>8 lamp posts 3.2 m on corbels outside the rails; heads: white ceramic post (B2_Trim), PaleGold cap, Emissive lens.</summary>
        public static void Lamps(MeshBuilder post, MeshBuilder gold, MeshBuilder lens, MeshBuilder pools, MeshBuilder glows)
        {
            Color warm = RingPalette.Lin("WarmLamp");
            foreach (float z in LampZ)
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * LampX, deck = DeckY(z);
                    // corbel from the deck fascia
                    // (top 6 cm under the deck, so the 0.157 slope never lifts it through the deck edge)
                    B12Geo.BoxMinMax(post, new Vector3(Mathf.Min(s * (DeckHalf - 0.05f), s * (LampX + 0.25f)), deck - 0.45f, z - 0.25f),
                        new Vector3(Mathf.Max(s * (DeckHalf - 0.05f), s * (LampX + 0.25f)), deck - 0.06f, z + 0.25f), 0.02f);
                    // plinth (sunk 2 cm into the corbel), shaft, lantern; lathe profiles top to bottom (outward)
                    B12Geo.Box(post, new Vector3(x, deck + 0.095f, z), new Vector3(0.24f, 0.35f, 0.24f), 0.02f);
                    B12Geo.LatheY(post, new Vector3(x, deck, z), new[]
                    {
                        new Vector2(0.07f, 2.90f), new Vector2(0.045f, 2.86f), new Vector2(0.045f, 0.40f), new Vector2(0.06f, 0.26f)
                    }, 12, true);
                    float l0 = deck + 2.90f, l1 = deck + 3.08f;
                    B12Geo.LatheY(lens, new Vector3(x, 0f, z), new[] { new Vector2(0.075f, l1), new Vector2(0.095f, 0.5f * (l0 + l1)), new Vector2(0.075f, l0) }, 16, true);
                    B12Geo.LatheY(gold, new Vector3(x, 0f, z), new[]
                    {
                        new Vector2(0.0001f, deck + RingConstants.LampH), new Vector2(0.05f, deck + 3.17f), new Vector2(0.13f, deck + 3.09f),
                        new Vector2(0.12f, deck + 3.06f), new Vector2(0.07f, deck + 3.07f)
                    }, 16, true);
                    // light pool on the deck between the rails, following the deck (shared RingMat.LightPool, lamp
                    // height per vertex: lens centre deck + LensCentreH above the pool's deck surface); halo at the lens
                    B12Additive.Pool(pools, new Vector3(x, deck, z), Vector3.forward, -B12Additive.PoolRadius, B12Additive.PoolRadius,
                        -RailX + 0.06f - x, RailX - 0.06f - x, warm, 0.25f, B12Additive.ChannelLamp, B12Additive.PoolRadius, 8, (px, pz) => DeckY(pz),
                        LensCentreH);
                    B12Additive.Glow(glows, new Vector3(x, deck + LensCentreH, z), 0.16f, warm, 1.2f, B12Additive.ChannelLamp);
                }
        }

        // ================================================================ rotunda (belvedere P1, pavilion P2)
        public struct Rotunda
        {
            public Vector2 XZ;
            public int Columns;
            public float PlinthR, ColCircle, ColRad, ColH, EntH, EntIn, EntOut, DomeR, DomeRise, DomeT, FinialH, TotalH;
            public bool GoldBand;
            public float FloorY, BottomY;
        }

        public static Rotunda Belvedere()
        {
            var r = new Rotunda
            {
                XZ = RingConstants.HillCentre, Columns = 12, PlinthR = 5.6f, ColCircle = 4.5f, ColRad = 0.26f, ColH = 7.6f,
                EntH = 0.8f, EntIn = 3.95f, EntOut = 5.05f, DomeR = 4.85f, DomeT = 0.25f, FinialH = 0.6f, TotalH = RingConstants.BelvedereH, GoldBand = false
            };
            r.DomeRise = r.TotalH - r.FinialH - (r.ColH + r.EntH);
            Site(ref r);
            return r;
        }

        public static Rotunda Pavilion()
        {
            var r = new Rotunda
            {
                XZ = new Vector2(RingConstants.PavilionP2.x, RingConstants.PavilionP2.z), Columns = 8, PlinthR = 4.1f, ColCircle = 3.2f, ColRad = 0.21f, ColH = 5.9f,
                EntH = 0.6f, EntIn = 2.8f, EntOut = 3.6f, DomeR = 3.45f, DomeT = 0.2f, FinialH = 0.5f, TotalH = RingConstants.PavilionH, GoldBand = true
            };
            r.DomeRise = r.TotalH - r.FinialH - (r.ColH + r.EntH);
            Site(ref r);
            return r;
        }

        /// <summary>Floor = the highest terrain under the plinth + 0.25 m (nothing buried), plinth foot 0.6 m below the
        /// lowest terrain around it (never floating); terrain from RingWorld.Height.</summary>
        static void Site(ref Rotunda r)
        {
            float hc = Ground(r.XZ.x, r.XZ.y);
            float mx = hc, mn = hc;
            for (int ring = 1; ring <= 4; ring++)
            {
                float rad = r.PlinthR * ring / 4f + (ring == 4 ? 0.3f : 0f);
                for (int i = 0; i < 32; i++)
                {
                    float a = 2f * Mathf.PI * i / 32f;
                    float h = Ground(r.XZ.x + rad * Mathf.Cos(a), r.XZ.y + rad * Mathf.Sin(a));
                    mx = Mathf.Max(mx, h); mn = Mathf.Min(mn, h);
                }
            }
            r.FloorY = mx + 0.25f;
            r.BottomY = mn - 0.6f;
        }

        public static void BuildRotunda(Rotunda r, MeshBuilder body, MeshBuilder gold, MeshBuilder floor)
        {
            var c = new Vector3(r.XZ.x, r.FloorY, r.XZ.y);
            const int seg = 64;
            var ang = B12Geo.Angles(seg);
            const float ch = 0.03f;
            // plinth drum (top chamfer) and the floor disc
            B12Geo.LatheAxis(body, c, Vector3.up, Vector3.right, Vector3.forward, new[]
            {
                new Vector2(r.PlinthR - ch, 0f), new Vector2(r.PlinthR, -ch), new Vector2(r.PlinthR, r.BottomY - r.FloorY)
            }, ang);
            B12Geo.DiscY(floor, c, r.PlinthR - ch, seg, true);
            // columns
            var colAng = B12Geo.Angles(16);
            float cr = r.ColRad, top = r.ColH + 0.03f;
            var colProf = new[]
            {
                new Vector2(cr * 1.32f, top), new Vector2(cr * 1.32f, r.ColH - 0.12f), new Vector2(cr * 1.06f, r.ColH - 0.26f),
                new Vector2(cr, r.ColH - 0.32f), new Vector2(cr, 0.36f), new Vector2(cr * 1.12f, 0.30f),
                new Vector2(cr * 1.30f, 0.22f), new Vector2(cr * 1.30f, -0.02f)
            };
            for (int i = 0; i < r.Columns; i++)
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / r.Columns;
                var cc = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r.ColCircle;
                B12Geo.LatheAxis(body, cc, Vector3.up, Vector3.right, Vector3.forward, colProf, colAng);
            }
            // entablature ring (closed annulus with chamfers)
            float y0 = r.ColH, y1 = r.ColH + r.EntH;
            B12Geo.LatheAxis(body, c, Vector3.up, Vector3.right, Vector3.forward, new[]
            {
                new Vector2(r.EntOut - ch, y1), new Vector2(r.EntOut, y1 - ch), new Vector2(r.EntOut, y0 + ch), new Vector2(r.EntOut - ch, y0),
                new Vector2(r.EntIn + ch, y0), new Vector2(r.EntIn, y0 + ch), new Vector2(r.EntIn, y1 - ch), new Vector2(r.EntIn + ch, y1),
                new Vector2(r.EntOut - ch, y1)
            }, ang);
            if (r.GoldBand)
            {
                // PaleGold ring on the entablature (14.5.4 P2), 2 cm proud, edges sunk into the frieze
                float g0 = y0 + 0.18f, g1 = y1 - 0.14f;
                B12Geo.LatheAxis(gold, c, Vector3.up, Vector3.right, Vector3.forward, new[]
                {
                    new Vector2(r.EntOut - 0.005f, g1), new Vector2(r.EntOut + 0.02f, g1 - 0.02f), new Vector2(r.EntOut + 0.02f, g0 + 0.02f), new Vector2(r.EntOut - 0.005f, g0)
                }, ang);
            }
            // dome shell: outer surface apex -> rim (visible outside), inner surface rim -> apex (the ceiling)
            const int rings = 14;
            float spring = y1 - 0.02f;
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            for (int k = rings; k >= 0; k--)
            {
                float th = 0.5f * Mathf.PI * k / rings;           // 0 at the rim, pi/2 at the apex
                outer.Add(new Vector2(r.DomeR * Mathf.Cos(th), spring + r.DomeRise * Mathf.Sin(th)));
            }
            for (int k = 0; k <= rings; k++)
            {
                float th = 0.5f * Mathf.PI * k / rings;
                inner.Add(new Vector2((r.DomeR - r.DomeT) * Mathf.Cos(th), spring + (r.DomeRise - r.DomeT) * Mathf.Sin(th)));
            }
            outer[0] = new Vector2(0f, outer[0].y);
            inner[rings] = new Vector2(0f, inner[rings].y);
            // rim ring under the outer shell (sits on the entablature; hidden) closes the shell
            outer.Add(new Vector2(r.DomeR - r.DomeT, spring));
            var ringOrder = new List<Vector2>(outer);
            B12Geo.LatheAxis(body, c, Vector3.up, Vector3.right, Vector3.forward, ringOrder, ang);
            B12Geo.LatheAxis(body, c, Vector3.up, Vector3.right, Vector3.forward, inner, ang);
            // finial (PaleGold)
            float ft = r.TotalH, fb = spring + r.DomeRise - 0.05f;
            B12Geo.LatheY(gold, c, new[]
            {
                new Vector2(0.0001f, ft), new Vector2(0.10f, ft - 0.12f), new Vector2(0.16f, ft - 0.28f), new Vector2(0.09f, ft - 0.42f),
                new Vector2(0.14f, fb + 0.06f), new Vector2(0.22f, fb)
            }, 16, true);
        }

        // ================================================================ aqueduct (80 arches, 20 m span, 24 m high)
        public const int AqueductBays = 80;
        const float AqZ = RingConstants.AqueductZ, AqHalfZ = 2.0f, AqPier = 2.4f, AqParapet = 1.0f, AqParW = 0.4f, AqArcSeg = 12;
        const float AqCornice = 0.3f, AqImpost = 0.15f;

        static float AqBase(float x)
        {
            float h = float.MaxValue;
            foreach (float dx in new[] { -0.5f * AqPier, 0f, 0.5f * AqPier })
                foreach (float dz in new[] { -AqHalfZ, AqHalfZ })
                    h = Mathf.Min(h, Ground(x + dx, AqZ + dz));
            return h;
        }

        /// <summary>One merged mesh of 80 bays; returns the triangles of one module (bay).</summary>
        public static int Aqueduct(MeshBuilder mb)
        {
            float span = RingConstants.AqueductSpan, height = RingConstants.AqueductH;
            int n = AqueductBays;
            var top = new float[n + 1];
            var bas = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float x = RingConstants.AqueductX0 + span * i;
                bas[i] = AqBase(x) - 1.0f;                   // piers sunk 1 m
                top[i] = Ground(x, AqZ) + height;            // 24 m above the terrain, following it
            }
            int module = 0;
            for (int i = 0; i < n; i++)
            {
                int t0 = mb.TriangleCount;
                Bay(mb, RingConstants.AqueductX0 + span * i, span, bas[i], bas[i + 1], top[i], top[i + 1]);
                if (i == 0) module = mb.TriangleCount - t0;
            }
            // end walls
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? 0 : n;
                float x = RingConstants.AqueductX0 + span * i;
                Vector3 nn = e == 0 ? Vector3.left : Vector3.right;
                B12Geo.QuadN(mb, new Vector3(x, bas[i], AqZ - AqHalfZ), new Vector3(x, bas[i], AqZ + AqHalfZ), new Vector3(x, top[i] + AqParapet, AqZ + AqHalfZ), new Vector3(x, top[i] + AqParapet, AqZ - AqHalfZ), nn);
            }
            return module;
        }

        static void Bay(MeshBuilder mb, float x0, float span, float b0, float b1, float t0, float t1)
        {
            float x1 = x0 + span, half = 0.5f * AqPier;
            float r = 0.5f * (span - AqPier);
            float spring = Mathf.Min(b0, b1) + 1.0f + (RingConstants.AqueductH - 2.7f - r);     // crown 2.7 m below the top
            float xa = x0 + half, xb = x1 - half, xc = 0.5f * (x0 + x1);
            System.Func<float, float> topAt = x => Mathf.Lerp(t0, t1, (x - x0) / span);
            // face outline (counter-clockwise in (x, y)), parapet included
            var outline = new List<Vector2> { new Vector2(x0, b0), new Vector2(xa, b0), new Vector2(xa, spring) };
            for (int k = 1; k < AqArcSeg; k++)
            {
                float a = Mathf.PI * (1f - k / (float)AqArcSeg);
                outline.Add(new Vector2(xc + r * Mathf.Cos(a), spring + r * Mathf.Sin(a)));
            }
            outline.Add(new Vector2(xb, spring)); outline.Add(new Vector2(xb, b1)); outline.Add(new Vector2(x1, b1));
            outline.Add(new Vector2(x1, t1 + AqParapet)); outline.Add(new Vector2(x0, t0 + AqParapet));
            var tris = new List<int>();
            B12Poly.Triangulate(outline, tris);
            for (int f = 0; f < 2; f++)
            {
                float z = f == 0 ? AqZ - AqHalfZ : AqZ + AqHalfZ;
                Vector3 nn = f == 0 ? Vector3.back : Vector3.forward;
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    Vector2 a = outline[tris[t]], b = outline[tris[t + 1]], c = outline[tris[t + 2]];
                    B12Geo.TriN(mb, new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, z), new Vector3(c.x, c.y, z), nn);
                }
            }
            // soffit (12 segments) and pier inner faces below the springing
            for (int k = 0; k < AqArcSeg; k++)
            {
                float a0 = Mathf.PI * (1f - k / (float)AqArcSeg), a1 = Mathf.PI * (1f - (k + 1) / (float)AqArcSeg);
                Vector3 p0 = new Vector3(xc + r * Mathf.Cos(a0), spring + r * Mathf.Sin(a0), 0f), p1 = new Vector3(xc + r * Mathf.Cos(a1), spring + r * Mathf.Sin(a1), 0f);
                Vector3 nm = -new Vector3(Mathf.Cos(0.5f * (a0 + a1)), Mathf.Sin(0.5f * (a0 + a1)), 0f);
                B12Geo.QuadN(mb, new Vector3(p0.x, p0.y, AqZ - AqHalfZ), new Vector3(p1.x, p1.y, AqZ - AqHalfZ), new Vector3(p1.x, p1.y, AqZ + AqHalfZ), new Vector3(p0.x, p0.y, AqZ + AqHalfZ), nm);
            }
            B12Geo.QuadN(mb, new Vector3(xa, b0, AqZ - AqHalfZ), new Vector3(xa, spring, AqZ - AqHalfZ), new Vector3(xa, spring, AqZ + AqHalfZ), new Vector3(xa, b0, AqZ + AqHalfZ), Vector3.right);
            B12Geo.QuadN(mb, new Vector3(xb, b1, AqZ - AqHalfZ), new Vector3(xb, spring, AqZ - AqHalfZ), new Vector3(xb, spring, AqZ + AqHalfZ), new Vector3(xb, b1, AqZ + AqHalfZ), Vector3.left);
            // imposts at the springing (0.15 m proud, 0.4 m tall) on both pier faces: front, top, bottom
            for (int s = -1; s <= 1; s += 2)
            {
                float xf = s < 0 ? xa : xb, xp = xf - s * AqImpost;
                Vector3 nf = s < 0 ? Vector3.right : Vector3.left;
                float ys0 = spring - 0.4f, ys1 = spring;
                B12Geo.QuadN(mb, new Vector3(xp, ys0, AqZ - AqHalfZ), new Vector3(xp, ys1, AqZ - AqHalfZ), new Vector3(xp, ys1, AqZ + AqHalfZ), new Vector3(xp, ys0, AqZ + AqHalfZ), nf);
                B12Geo.QuadN(mb, new Vector3(xf, ys1, AqZ - AqHalfZ), new Vector3(xp, ys1, AqZ - AqHalfZ), new Vector3(xp, ys1, AqZ + AqHalfZ), new Vector3(xf, ys1, AqZ + AqHalfZ), Vector3.up);
                B12Geo.QuadN(mb, new Vector3(xf, ys0, AqZ - AqHalfZ), new Vector3(xp, ys0, AqZ - AqHalfZ), new Vector3(xp, ys0, AqZ + AqHalfZ), new Vector3(xf, ys0, AqZ + AqHalfZ), Vector3.down);
            }
            // channel floor between the parapets, parapet tops and inner faces
            float zi0 = AqZ - AqHalfZ + AqParW, zi1 = AqZ + AqHalfZ - AqParW;
            B12Geo.QuadN(mb, new Vector3(x0, t0, zi0), new Vector3(x1, t1, zi0), new Vector3(x1, t1, zi1), new Vector3(x0, t0, zi1), Vector3.up);
            for (int s = 0; s < 2; s++)
            {
                float zo = s == 0 ? AqZ - AqHalfZ : AqZ + AqHalfZ, zi = s == 0 ? zi0 : zi1;
                B12Geo.QuadN(mb, new Vector3(x0, t0 + AqParapet, zo), new Vector3(x1, t1 + AqParapet, zo), new Vector3(x1, t1 + AqParapet, zi), new Vector3(x0, t0 + AqParapet, zi), Vector3.up);
                B12Geo.QuadN(mb, new Vector3(x0, t0, zi), new Vector3(x1, t1, zi), new Vector3(x1, t1 + AqParapet, zi), new Vector3(x0, t0 + AqParapet, zi), s == 0 ? Vector3.forward : Vector3.back);
                // cornice band 1.2 m below the channel, 0.3 m proud
                float zc = s == 0 ? zo - AqCornice : zo + AqCornice;
                float c0a = t0 - 1.6f, c1a = t0 - 1.2f, c0b = t1 - 1.6f, c1b = t1 - 1.2f;
                Vector3 outN = s == 0 ? Vector3.back : Vector3.forward;
                B12Geo.QuadN(mb, new Vector3(x0, c0a, zc), new Vector3(x1, c0b, zc), new Vector3(x1, c1b, zc), new Vector3(x0, c1a, zc), outN);
                B12Geo.QuadN(mb, new Vector3(x0, c1a, zo), new Vector3(x1, c1b, zo), new Vector3(x1, c1b, zc), new Vector3(x0, c1a, zc), Vector3.up);
                B12Geo.QuadN(mb, new Vector3(x0, c0a, zo), new Vector3(x1, c0b, zo), new Vector3(x1, c0b, zc), new Vector3(x0, c0a, zc), Vector3.down);
            }
        }

        // ================================================================ self-test entry
        internal static Dictionary<string, MeshBuilder> BuildForTest()
        {
            var d = new Dictionary<string, MeshBuilder>();
            MeshBuilder B(string k) { var m = new MeshBuilder(); d[k] = m; return m; }
            PathStrip(B("PathStrip"), B("Kerbs"));
            Bollards(B("BollardShaft"), B("BollardLens"), B("BollardCap"));
            var bench = B("Benches");
            foreach (var p in BridgeBenches) Bench(bench, p);
            foreach (var p in PathBenches) Bench(bench, p);
            Deck(B("DeckTop"), B("DeckSlab"), B("DeckCollider"));
            Abutments(B("Abutments"));
            Arch(B("Arch"));
            Rails(B("Rails"));
            Lamps(B("LampPost"), B("LampGold"), B("LampLens"), B("LampPools"), B("LampGlows"));
            var bel = Belvedere();
            BuildRotunda(bel, B("BelvedereBody"), B("BelvedereGold"), B("BelvedereFloor"));
            var pav = Pavilion();
            BuildRotunda(pav, B("PavilionBody"), B("PavilionGold"), B("PavilionFloor"));
            int module = Aqueduct(B("Aqueduct"));
            System.Console.WriteLine("  belvedere floor " + bel.FloorY.ToString("0.00") + " (H centre " + Ground(bel.XZ.x, bel.XZ.y).ToString("0.00") + ") foot " + bel.BottomY.ToString("0.00")
                + "; pavilion floor " + pav.FloorY.ToString("0.00") + " foot " + pav.BottomY.ToString("0.00") + "; aqueduct module " + module + " triangles");
            return d;
        }
    }
}
