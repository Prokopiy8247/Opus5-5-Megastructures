using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 6 - the wall tunnel (MASTER_PROMPT 14.5.6, 14.6 boundaries 5->6 and 6->7). Builds, behind the load fade:
    /// the TunnelLoft with the door profile from x 114.8 (where the beat-5 recess ends, rings bit-identical) to the
    /// mouth at x 234 (open to the real outside scene), 20 ribs at x = 117 + 6 k (0.25 m proud, 0.40 m thick, merged
    /// into the shell), vertex AO (R), a 1 m outward sleeve hidden inside the wall at the mouth (seals the junction
    /// with the outer face), two continuous 0.06 m amber floor strips and 40 markers (TunnelAmber), and the mouth
    /// spill (additive floor / wall / rib quads over x 222..234, intensity _Lamps.w). The shell casts shadows on
    /// both sides (the key L7 enters the mouth near the end); RingTunnel.shader also occludes every directional
    /// light analytically through the two openings, so nothing leaks into the tube.
    /// Variation along the 120 m (critic S3: start, KS10 and mid looked identical for ~60 s), restrained and inside the
    /// spec: every 5th rib (k = 4, 9, 14, 19: one per 30 m) is a section rib - lighter trim albedo (vertex colour B = 1,
    /// RingTunnel _TrimColor), numbered markers (n = 1..4 stacked amber bars instead of the single square: the section
    /// count from the door) and an amber floor rung between the strips; in the last 30 m (k >= 15) every rib has a
    /// rung, so the floor pattern turns into a ladder leading into the light.
    /// </summary>
    public sealed class Beat06TunnelBuilder : IBeatBuilder
    {
        public int Order => 600;
        public string Name => "Beat06Tunnel";

        // ---- 14.5.6 numbers that RingConstants does not carry ----
        const float RibProud = 0.25f;                      // "0.25 m proud" -> clear 2.1 m at the ribs
        const float RibHalf = 0.20f;                       // "0.40 m thick"
        const float RibChamfer = 0.02f;                    // 8.7: 1-3 cm chamfers within 3 m
        const float RibSeal = 0.01f;                       // rib sides reach 1 cm behind the shell (no crack at the junction)
        const float RibFootY = -0.01f;                     // rib feet 1 cm below the floor
        const int RibCrownSegments = 24;
        const float StripZ = 0.97f;                        // strip centre |z - 660|, just inside the rib line (continuous)
        const float StripW = 0.06f;                        // "continuous 0.06 m amber floor strips"
        const float StripTop = 0.006f, StripBottom = -0.01f;
        const float StripX0 = 114.85f, StripX1 = 233.95f;
        const float MarkerSize = 0.2f, MarkerY = 0.5f;     // "40 markers (0.2 m at 0.5 m height, one per rib per side)"
        const float MarkerProud = 0.012f, MarkerEmbed = 0.01f;
        const int SectionPeriod = 5, SectionPhase = 4;     // section ribs k = 4, 9, 14, 19 (every 30 m)
        const int LadderFrom = 15;                         // rungs at every rib from k 15 (x 207..231, the last 30 m)
        const float BarH = 0.05f, BarGap = 0.035f;         // numbered marker: n bars 0.2 x 0.05 m, 3.5 cm apart
        const float RungW = 0.06f, RungHalf = 0.93f;       // rung 0.06 m wide (like the strips), 1 cm short of each strip
        const float RungTop = 0.005f;                      // 1 mm below the strip tops (never co-planar with them)
        const float SpillLength = 12f;                     // "over the last 12 m (x 222..234)"
        const float SpillInset = 0.02f;                    // additive quads 2 cm in front of their surface
        const float SleeveLength = 1f;                     // "1 m overlap into the wall"
        const float BendDistance = 400f;                   // seen from the plain (door) and the platform (mouth)

        static float RibX(int k) => RingConstants.TunnelRibX0 + RingConstants.TunnelRibPitch * k;
        /// <summary>Section rib (one per 30 m): trim albedo, numbered marker, floor rung.</summary>
        public static bool IsSection(int k) => k % SectionPeriod == SectionPhase;
        /// <summary>Section number 1..4 counted from the door (0 for ordinary ribs).</summary>
        public static int SectionNumber(int k) => IsSection(k) ? k / SectionPeriod + 1 : 0;
        public static bool HasRung(int k) => IsSection(k) || k >= LadderFrom;
        static float SpillX0 => TunnelProfile.MouthX - SpillLength;
        static float SleeveX0 => TunnelProfile.MouthX - SleeveLength;

        public IEnumerator Build(BuildContext ctx)
        {
            const RingSet set = RingSet.Tunnel;
            int tris0 = ctx.Triangles;

            var shell = BuildShell();
            Beat56Kit.Check(shell);
            var shellGo = ctx.AddMesh("TunnelShell", shell, RingMat.Tunnel, set, true, BendDistance);
            var mr = shellGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = ShadowCastingMode.TwoSided;   // inside-out tube: both sides cast
            ctx.Register("Tunnel.Shell", shellGo);
            yield return null;

            var lights = BuildStripsAndMarkers();
            Beat56Kit.Check(lights);
            // TunnelAmber = EmissiveAmber with a black body: RingEmissive is lit by the plain shared model, so a lens albedo
            // would let the beat-7 key L7 (unshadowed beyond the 90 m shadow distance) brighten the strips along the tube.
            ctx.Register("Tunnel.Strips", ctx.AddMesh("TunnelStrips", lights, Beat56Ids.TunnelAmber, set, false, BendDistance));

            var spill = BuildSpill();
            Beat56Kit.Check(spill);
            ctx.Register("Tunnel.Spill", ctx.AddMesh("TunnelSpill", spill, Beat56Ids.TunnelSpill, set, false, BendDistance));

            // registry markers (exact dimensions; renderer bounds are inflated for the bend)
            float zc = TunnelProfile.AxisZ, h = RingConstants.DoorH, w = RingConstants.DoorW;
            Beat56Kit.Marker(ctx, set, "Tunnel",
                new Vector3(0.5f * (TunnelProfile.DoorX + TunnelProfile.MouthX), 0.5f * h, zc),
                new Vector3(TunnelProfile.MouthX - TunnelProfile.DoorX, h, w));
            for (int k = 0; k < RingConstants.TunnelRibs; k++)
                Beat56Kit.Marker(ctx, set, "Tunnel.Rib" + k, new Vector3(RibX(k), 0.5f * (h - RibProud), zc),
                    new Vector3(2f * RibHalf, h - RibProud, w - 2f * RibProud));
            Beat56Kit.Marker(ctx, set, "Subject.TunnelMouth", new Vector3(TunnelProfile.MouthX, 0.5f * h, zc), new Vector3(0.01f, h, w));

            Debug.Log("[Ring] Beat06Tunnel built: " + (ctx.Triangles - tris0) + " triangles (shell + ribs " + Beat56Kit.Triangles(shell)
                + ", " + RingConstants.TunnelRibs + " ribs, " + (2 * RingConstants.TunnelRibs) + " markers)");
        }

        // =====================================================================================================
        // Shell: floor and arch lofted separately (hard floor / wall corners), rings at every rib edge and its AO falloff.
        // =====================================================================================================
        static List<float> ShellRings()
        {
            var xs = new List<float> { TunnelProfile.LoftX0, SleeveX0, TunnelProfile.MouthX };
            float[] d = { -3f, -1f, -0.45f, -RibHalf, RibHalf, 0.45f, 1f };
            for (int k = 0; k < RingConstants.TunnelRibs; k++)
                for (int i = 0; i < d.Length; i++)
                {
                    float x = RibX(k) + d[i];
                    if (x > TunnelProfile.LoftX0 + 0.05f && x < TunnelProfile.MouthX - 0.05f) xs.Add(x);
                }
            xs.Sort();
            for (int i = xs.Count - 1; i > 0; i--) if (xs[i] - xs[i - 1] < 1e-3f) xs.RemoveAt(i);
            return xs;
        }

        static float ShellAO(Vector3 p, bool floor)
        {
            Vector2 q = TunnelProfile.Section(p);
            float dw = Mathf.Max(TunnelProfile.HalfWidth - Mathf.Abs(q.x), 0f);
            float dc = Mathf.Sqrt(dw * dw + q.y * q.y);                       // distance to the floor / wall corner
            float ao = 1f - 0.32f * Mathf.Exp(-dc / 0.22f);
            int k = Mathf.Clamp(Mathf.RoundToInt((p.x - RingConstants.TunnelRibX0) / RingConstants.TunnelRibPitch), 0, RingConstants.TunnelRibs - 1);
            float dr = Mathf.Max(Mathf.Abs(p.x - RibX(k)) - RibHalf, 0f);       // distance from the rib's side face
            float wgt = floor ? Mathf.Exp(-dw / 0.3f) : 1f;                    // ribs only shade the floor near their feet
            return ao * (1f - 0.28f * wgt * Mathf.Exp(-dr / 0.35f));
        }

        static float RibAO(Vector3 p)
        {
            Vector2 q = TunnelProfile.Section(p);
            float depth = Mathf.Clamp(-TunnelProfile.Sdf(q), 0f, RibProud);   // how far the point stands out of the shell
            float ao = Mathf.Lerp(0.66f, 1f, depth / RibProud);
            return ao * (1f - 0.3f * Mathf.Exp(-Mathf.Max(q.y, 0f) / 0.2f));
        }

        static Mesh BuildShell()
        {
            var mb = new MeshBuilder();
            var xs = ShellRings();
            var path = new Vector3[xs.Count];
            for (int i = 0; i < xs.Count; i++) path[i] = new Vector3(xs[i], 0f, TunnelProfile.AxisZ);
            var plain = new Color(1f, 0f, 0f, 0f);
            mb.Col = plain;
            int v0 = mb.VertexCount;
            mb.Loft(TunnelProfile.Floor(0f), path, Vector3.up, true, false);
            int v1 = mb.VertexCount;
            mb.Loft(TunnelProfile.Arch(0f, 0f, TunnelProfile.WallYs, TunnelProfile.CrownSegments), path, Vector3.up, true, false);
            int v2 = mb.VertexCount;
            Beat56Kit.SetAO(mb, v0, v1, p => ShellAO(p, true));
            Beat56Kit.SetAO(mb, v1, v2, p => ShellAO(p, false));

            for (int k = 0; k < RingConstants.TunnelRibs; k++) AddRib(mb, RibX(k), IsSection(k));

            // 1 m outward sleeve inside the wall at the mouth (x 233..234): any gap between the outer face's hole and the
            // tunnel end shows this sleeve, never the void between the wall faces. Back to back with the shell, never co-planar
            // with a same-facing surface.
            mb.Col = plain;
            mb.Loft(TunnelProfile.Closed(0f, TunnelProfile.WallYs, TunnelProfile.CrownSegments),
                new[] { new Vector3(SleeveX0, 0f, TunnelProfile.AxisZ), new Vector3(TunnelProfile.MouthX, 0f, TunnelProfile.AxisZ) },
                Vector3.up, false, true);
            return mb.Build("Beat06.TunnelShell");
        }

        static void AddRib(MeshBuilder mb, float xr, bool section)
        {
            var inner = TunnelProfile.Arch(-RibProud, RibFootY, TunnelProfile.WallYs, RibCrownSegments);
            var innerC = TunnelProfile.Arch(-RibProud + RibChamfer, RibFootY, TunnelProfile.WallYs, RibCrownSegments);
            var outer = TunnelProfile.Arch(RibSeal, RibFootY, TunnelProfile.WallYs, RibCrownSegments);
            float xa = xr - RibHalf, xb = xr + RibHalf;
            float trim = section ? 1f : 0f;                   // vertex colour B: section-rib trim (RingTunnel _TrimColor)
            var plain = new Color(1f, 0f, trim, 0f);
            var wear = new Color(1f, 1f, trim, 0f);
            // inner face (faces the axis)
            mb.Col = plain;
            int v0 = mb.VertexCount;
            mb.Loft(inner, new[] { new Vector3(xa + RibChamfer, 0f, TunnelProfile.AxisZ), new Vector3(xb - RibChamfer, 0f, TunnelProfile.AxisZ) },
                Vector3.up, true, false);
            Beat56Kit.SetAO(mb, v0, mb.VertexCount, RibAO);
            // 2 cm chamfers on both inner edges
            Beat56Kit.Band(mb, innerC, xa, inner, xa + RibChamfer,
                i => (Vector3.left - TunnelProfile.WorldDir(TunnelProfile.Outward(inner, i, false))).normalized, wear, RibAO);
            Beat56Kit.Band(mb, inner, xb - RibChamfer, innerC, xb,
                i => (Vector3.right - TunnelProfile.WorldDir(TunnelProfile.Outward(inner, i, false))).normalized, wear, RibAO);
            // side faces from 1 cm behind the shell to the chamfers
            Beat56Kit.Band(mb, outer, xa, innerC, xa, i => Vector3.left, plain, RibAO);
            Beat56Kit.Band(mb, innerC, xb, outer, xb, i => Vector3.right, plain, RibAO);
        }

        // =====================================================================================================
        // TunnelAmber: two continuous floor strips (6 mm proud, embedded 1 cm), 40 markers on the rib inner faces (the
        // section ribs carry n stacked bars, n = section number 1..4, centred on the marker height) and the floor rungs
        // between the strips (section ribs and the last-30 m ladder), 1 cm short of the strips, 1 mm lower than them.
        // =====================================================================================================
        static Mesh BuildStripsAndMarkers()
        {
            var mb = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                float zc = TunnelProfile.AxisZ + s * StripZ;
                mb.ChamferedBox(new Vector3(0.5f * (StripX0 + StripX1), 0.5f * (StripTop + StripBottom), zc),
                    new Vector3(StripX1 - StripX0, StripTop - StripBottom, StripW), 0.002f);
            }
            float face = TunnelProfile.HalfWidth - RibProud;   // rib inner face |z - 660| = 1.05
            for (int k = 0; k < RingConstants.TunnelRibs; k++)
            {
                int nBars = SectionNumber(k);
                for (int s = -1; s <= 1; s += 2)
                {
                    float zIn = TunnelProfile.AxisZ + s * (face - MarkerProud), zOut = TunnelProfile.AxisZ + s * (face + MarkerEmbed);
                    float zc = 0.5f * (zIn + zOut), dz = Mathf.Abs(zOut - zIn);
                    if (nBars == 0)
                    {
                        mb.ChamferedBox(new Vector3(RibX(k), MarkerY, zc), new Vector3(MarkerSize, MarkerSize, dz), 0.003f);
                        continue;
                    }
                    float stack = nBars * BarH + (nBars - 1) * BarGap;
                    for (int b = 0; b < nBars; b++)
                    {
                        float yb = MarkerY - 0.5f * stack + 0.5f * BarH + b * (BarH + BarGap);
                        mb.ChamferedBox(new Vector3(RibX(k), yb, zc), new Vector3(MarkerSize, BarH, dz), 0.003f);
                    }
                }
                if (HasRung(k))
                    mb.ChamferedBox(new Vector3(RibX(k), 0.5f * (RungTop + StripBottom), TunnelProfile.AxisZ),
                        new Vector3(RungW, RungTop - StripBottom, 2f * RungHalf), 0.002f);
            }
            return mb.Build("Beat06.TunnelLights");
        }

        // =====================================================================================================
        // Mouth spill (TunnelSpill, additive): floor, walls + crown and the rib faces of the last 12 m, each 2 cm in
        // front of its surface. uv0.x = (234 - x) / 12 drives the analytic falloff (0 at x 222); vertex colour = face
        // factor (floor and the rib faces that look at the mouth catch most of the light).
        // =====================================================================================================
        static Mesh BuildSpill()
        {
            var mb = new MeshBuilder();
            float x0 = SpillX0, x1 = TunnelProfile.MouthX, zc = TunnelProfile.AxisZ;
            float hw = TunnelProfile.HalfWidth - SpillInset;
            // floor
            var cf = new Color(1f, 1f, 1f, 1f);
            int a = mb.Vtx(new Vector3(x0, SpillInset, zc - hw), Vector3.up, Vector2.zero, cf);
            int b = mb.Vtx(new Vector3(x0, SpillInset, zc + hw), Vector3.up, Vector2.zero, cf);
            int c = mb.Vtx(new Vector3(x1, SpillInset, zc + hw), Vector3.up, Vector2.zero, cf);
            int d = mb.Vtx(new Vector3(x1, SpillInset, zc - hw), Vector3.up, Vector2.zero, cf);
            Beat56Kit.Quad(mb, a, b, c, d, Vector3.up);
            // walls and crown
            int v0 = mb.VertexCount;
            mb.Loft(TunnelProfile.Arch(-SpillInset, SpillInset, TunnelProfile.WallYs, RibCrownSegments),
                new[] { new Vector3(x0, 0f, zc), new Vector3(x1, 0f, zc) }, Vector3.up, true, false);
            for (int i = v0; i < mb.VertexCount; i++)
            {
                float ff = Mathf.Lerp(0.75f, 0.45f, Mathf.Clamp01((mb.V[i].y - 0.5f) / 2.5f));
                mb.C[i] = new Color(ff, ff, ff, 1f);
            }
            // ribs inside the spill zone: inner face (0.55) and the side that looks at the mouth (1.0)
            for (int k = 0; k < RingConstants.TunnelRibs; k++)
            {
                float xr = RibX(k);
                if (xr - RibHalf <= x0 || xr + RibHalf >= x1) continue;
                int r0 = mb.VertexCount;
                mb.Loft(TunnelProfile.Arch(-RibProud - SpillInset, SpillInset, TunnelProfile.WallYs, RibCrownSegments),
                    new[] { new Vector3(xr - RibHalf, 0f, zc), new Vector3(xr + RibHalf, 0f, zc) }, Vector3.up, true, false);
                for (int i = r0; i < mb.VertexCount; i++) mb.C[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
                float xs = xr + RibHalf + SpillInset;
                Beat56Kit.Band(mb, TunnelProfile.Arch(-RibProud, SpillInset, TunnelProfile.WallYs, RibCrownSegments), xs,
                    TunnelProfile.Arch(-SpillInset, SpillInset, TunnelProfile.WallYs, RibCrownSegments), xs,
                    i => Vector3.right, new Color(1f, 1f, 1f, 1f));
            }
            float inv = 1f / Mathf.Max(x1 - x0, 1e-3f);
            for (int i = 0; i < mb.VertexCount; i++) mb.UV[i] = new Vector2((x1 - mb.V[i].x) * inv, 0f);
            return mb.Build("Beat06.TunnelSpill", false);
        }
    }
}
