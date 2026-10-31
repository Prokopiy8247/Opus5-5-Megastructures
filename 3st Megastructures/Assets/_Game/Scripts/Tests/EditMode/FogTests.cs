// FogTests: EditMode NUnit checks of the C# fog mirror FogMath (MASTER_PROMPT 14.7.6, 14.10.1) and of the
// deck / ground decisions D-006 and D-007:
//   T-BANK      bank optical depth with 16 fixed midpoint samples vs a 256-sample reference over 2 000
//               deterministic rays from cameras along the path s 0..345 and from the sky directions
//               (tolerance 0.01 absolute or 1.5 percent of tau), plus the bank density profile G, P, Y;
//   deck        closed-form slab optical depth vs a 4 096-sample numeric integral (0.5 percent);
//   distance    closed-form height-fog integral vs a 4 096-sample numeric integral (0.5 percent);
//   D-006       deck gate 0 at s = 560 and 1 from s = 578;
//   D-007       ground group deactivated at s = 590 while the camera is inside the deck (A 1.8..3.0 km);
//   atmosphere  (fix wave 3) hue-preserving haze (C-09), deck-pass horizon (C-06 / A12 / B8 / PAC-r2-1), horizon toward
//               the bank (C-r2-3 / E-r2-3) and the bank footprint filter, on the C# mirror of ThreadFogMix /
//               ThreadFogApplySky / ThreadHazeMix / ThreadTauBankFootprint.
// Pure maths, no scene. Shared helpers (Checks, Rng, Inv, Log) live in CoreTests.
using System;
using NUnit.Framework;

namespace Megastructure.Tests
{
    public class FogTests
    {
        static string Inv(FormattableString f) => CoreTests.Inv(f);
        static bool IsFinite(double v) => CoreTests.IsFinite(v);
        static string V(D3 p) => CoreTests.V(p);
        static double Deg => ThreadMath.Deg;

        // ================================================================================================
        // T-BANK (14.7.6, 14.10.1)
        // ================================================================================================

        const int BankRays = 2000;          // 14.7.6 "over 2 000 random rays"
        const int BankReference = 256;      // 14.7.6 "256-sample reference"
        const double BankAbsTol = 0.01;     // 14.7.6 "tolerance 0.01 absolute ..."
        const double BankRelTol = 0.015;    // 14.7.6 "... or 1.5 percent of tau"
        const double SkyBankReach = 2000.0; // ThreadFogApplySky clips the bank ray to 2 000 m (ThreadCommon.hlsl)

        [Test]
        public void TBank_DensityProfile()
        {
            var c = new CoreTests.Checks("bank density rho G(x) P(z) Y(y) (14.7.6; D-030 rho = 0.16, D-025 flat-topped G, D-011 8 m Y transitions)");
            const double xc = 3.5;
            // 14.7.6 (line 860) says peak density 0.11 /m; D-030 raised it to 0.16 /m (the literal is asserted in
            // CoreTests.Constants_Table1412); the profile checks below are fractions of the contract's peak
            double rho = ThreadConstants.BankDensity;
            // peak inside the core and the lowest slab
            c.Near(FogMath.BankDensityLocal(xc, 10.0, 285.0, xc), rho, 1e-12, "peak density (x = x_c, y = 10, z = 285)");
            // G: 14.7.6 writes exp(-((x - x_c) / 90)^2); D-025 uses the flat-topped exp(-((x - x_c) / 220)^4)
            double sx = ThreadConstants.BankSigmaX;
            c.Near(FogMath.BankDensityLocal(xc + sx, 10.0, 285.0, xc) / rho, Math.Exp(-1.0), 1e-12, "G at x_c + 220 (exp(-1))");
            c.Near(FogMath.BankDensityLocal(xc - sx, 10.0, 285.0, xc) / rho, Math.Exp(-1.0), 1e-12, "G at x_c - 220 (exp(-1), symmetric)");
            c.Near(FogMath.BankDensityLocal(xc + 0.5 * sx, 10.0, 285.0, xc) / rho, Math.Exp(-1.0 / 16.0), 1e-12, "G at x_c + 110 (exponent 4: exp(-1/16), flat top)");
            c.Near(FogMath.BankDensityLocal(xc - 2.0 * sx, 10.0, 285.0, xc) / rho, Math.Exp(-16.0), 1e-12, "G at x_c - 440 (exponent 4: exp(-16))");
            // D-025: the flat-topped profile still falls to about 0.03 at the box edges x = +-300 (x_c = 0)
            c.Near(FogMath.BankDensityLocal(ThreadConstants.BankBoxXMax, 10.0, 285.0, 0.0) / rho, 0.03, 0.005, "G at the box edge x = +300 (D-025: 0.03)");
            c.Near(FogMath.BankDensityLocal(ThreadConstants.BankBoxXMin, 10.0, 285.0, 0.0) / rho, 0.03, 0.005, "G at the box edge x = -300 (D-025: 0.03)");
            // P: 0 at z = 225, 1 at 265..305, 0 at 345
            double[,] pz = { { 200, 0 }, { 225, 0 }, { 245, 0.5 }, { 265, 1 }, { 285, 1 }, { 305, 1 }, { 325, 0.5 }, { 345, 0 }, { 370, 0 } };
            for (int i = 0; i < pz.GetLength(0); i++)
                c.Near(FogMath.BankDensityLocal(xc, 10.0, pz[i, 0], xc) / rho, pz[i, 1], 1e-12, Inv($"P(z = {pz[i, 0]:F0})"));
            // Y: 1 - 0.25 sum_i smoothstep(top_i - e, top_i + e, y), slab tops 60 / 70 / 80 / 90 m. The spec writes
            // e = 2 m (smoothstep(58, 62, y)) and names e = 4 m (8 m transitions) as the remedy if T-BANK fails, so the
            // edges are taken from the contract and only those two widths are accepted.
            double e = ThreadConstants.BankSlabHalfEdge;
            c.That(e == 2.0 || e == 4.0, Inv($"BankSlabHalfEdge = {e:G4}: 14.7.6 allows 4 m transitions (2) or the 8 m remedy (4)"));
            double[] tops = { 60, 70, 80, 90 };
            for (int i = 0; i < tops.Length; i++)
            {
                double above = 1.0 - 0.25 * i;   // density fraction below slab top i
                c.Near(FogMath.BankDensityLocal(xc, tops[i] - e, 285.0, xc) / rho, above, 1e-12, Inv($"Y below the slab top {tops[i]:F0} (y = {tops[i] - e:F0})"));
                c.Near(FogMath.BankDensityLocal(xc, tops[i], 285.0, xc) / rho, above - 0.125, 1e-12, Inv($"Y at the slab top {tops[i]:F0}"));
                c.Near(FogMath.BankDensityLocal(xc, tops[i] + e, 285.0, xc) / rho, above - 0.25, 1e-12, Inv($"Y above the slab top {tops[i]:F0} (y = {tops[i] + e:F0})"));
                c.Near(FogMath.BankDensityLocal(xc, tops[i] + 5.0, 285.0, xc) / rho, above - 0.25, 1e-12, Inv($"Y on the plateau above {tops[i]:F0} (y = {tops[i] + 5:F0})"));
            }
            c.Near(FogMath.BankDensityLocal(xc, 0.0, 285.0, xc) / rho, 1.0, 1e-12, "Y at the ground");
            c.Near(FogMath.BankDensityLocal(xc, 99.0, 285.0, xc) / rho, 0.0, 1e-12, "Y at the box top");
            // the two contract implementations (FogMath and ThreadMath) agree
            var rng = new CoreTests.Rng(0xB4A1C0DE5EEDUL);
            double worst = 0;
            for (int i = 0; i < 4000; i++)
            {
                double x = rng.Range(-320, 320), y = rng.Range(-5, 105), z = rng.Range(190, 390), dc = rng.Range(-15, 15);
                worst = Math.Max(worst, Math.Abs(FogMath.BankDensityLocal(x, y, z, dc) - ThreadMath.BankDensity(x, y, z, dc)));
            }
            c.That(worst < 1e-14, Inv($"FogMath.BankDensityLocal and ThreadMath.BankDensity differ by up to {worst:G3}"));
            c.AssertAll();
        }

        [Test]
        public void TBank_SixteenSamplesMatchReference()
        {
            var c = new CoreTests.Checks("T-BANK (14.7.6: tau_bank with 16 samples vs 256 samples, tolerance 0.01 absolute or 1.5 % of tau, 2 000 rays)");
            var rng = new CoreTests.Rng(0x5448524541445F42UL);   // "THREAD_B"
            double worstRatio = 0, worstErr = 0, maxTau = 0;
            string worstCase = "none";
            int fails = 0, foggy = 0;
            for (int i = 0; i < BankRays; i++)
            {
                // camera on the walkable corridor between s = 0 and 345, eye height above the floor
                double s = rng.Range(0.0, 345.0);
                double lateral = rng.Range(-1.0, 1.0) * ThreadMath.HalfWidth(s);
                D3 cam = ThreadMath.CorridorToWorld(s, lateral, ThreadConstants.EyeHeight);
                double groupOffset = -ThreadMath.VirtualAltitude(s);   // the box moves with the ground group
                double xc = ThreadMath.BankDrift(rng.Range(0.0, 600.0), rng.Range(1.0, 2.0));

                D3 dir; double d; string kind;
                switch (i % 3)
                {
                    case 0:   // geometry ray: any direction, any length up to 3 km, stopped by the ground plane
                        dir = rng.UnitVector();
                        d = rng.LogRange(0.5, 3000.0);
                        if (dir.y < -1e-3) d = Math.Min(d, cam.y / -dir.y);
                        kind = "geometry";
                        break;
                    case 1:   // sky ray: upper hemisphere, clipped to the sky reach
                        dir = rng.AtElevation(Math.Asin(rng.Range(0.0, 1.0)));
                        d = SkyBankReach;
                        kind = "sky";
                        break;
                    default:  // horizon band: elevation -2 .. +10 deg (sky and far ground)
                        dir = rng.AtElevation(rng.Range(-2.0, 10.0) * Deg);
                        d = SkyBankReach;
                        if (dir.y < -1e-3) d = Math.Min(d, cam.y / -dir.y);
                        kind = "horizon";
                        break;
                }

                double t16 = FogMath.TauBank(cam, dir, d, xc, groupOffset, ThreadConstants.BankSamples);
                double tRef = FogMath.TauBank(cam, dir, d, xc, groupOffset, BankReference);
                double err = Math.Abs(t16 - tRef), allowed = Math.Max(BankAbsTol, BankRelTol * tRef);
                bool ok = IsFinite(t16) && IsFinite(tRef) && t16 >= 0.0 && err <= allowed;
                if (tRef > 0.01) foggy++;
                maxTau = Math.Max(maxTau, tRef);
                string desc = Inv($"{kind} ray {i}: s = {s:F2}, cam {V(cam)}, dir {V(dir)}, d = {d:F1} m, x_c = {xc:F2}: tau16 = {t16:F5}, tau256 = {tRef:F5}, error {err:F5} (allowed {allowed:F5})");
                if (!ok) fails++;
                double ratio = err / allowed;
                if (ratio > worstRatio) { worstRatio = ratio; worstErr = err; worstCase = desc; }
                c.That(ok, desc);
            }
            CoreTests.Log(Inv($"T-BANK: {BankRays} rays, {foggy} with tau > 0.01, max tau {maxTau:F3}, {fails} outside tolerance; worst error / allowed = {worstRatio:F3} (error {worstErr:F5}): {worstCase}"));
            if (c.Failed > 0)
                c.That(false, Inv($"WORST CASE (error / allowed = {worstRatio:F3}): {worstCase}. 14.7.6: if this fails, widen the Y transitions to 8 m (BankSlabHalfEdge 2 -> 4, also in ThreadMath.BankY and ThreadCommon.hlsl via TC_BankSlabHalfEdge) rather than adding noise (lead's decision). Measured offline on the same 2 000 rays: transition 4 m -> 4 failures, worst 2.46 x the allowance, transmission error up to 0.0137; 6 m -> 0 failures, worst 0.83; 8 m -> 0 failures, worst 0.56, transmission error up to 0.0044"));
            c.AssertAll();
        }

        [Test]
        public void TBank_ZeroOutsideTheBox()
        {
            var c = new CoreTests.Checks("T-BANK sanity (rays that miss the box have zero optical depth; tau is linear in the ray clip)");
            D3 cam = new D3(0.0, ThreadConstants.EyeHeight, 0.0);
            c.Near(FogMath.TauBank(cam, new D3(0, 0, -1), 5000.0, 0.0, 0.0), 0.0, 0.0, "spawn looking back (-Z)");
            c.Near(FogMath.TauBank(cam, new D3(0, 0, 1), 150.0, 0.0, 0.0), 0.0, 0.0, "spawn looking ahead, ray ends before the box (z < 200)");
            D3 core = new D3(0.0, 15.2, 285.0);
            c.Near(FogMath.TauBank(core, new D3(0, 1, 0), 1e-9, 0.0, 0.0), 0.0, 1e-9, "zero-length ray");
            // a ray inside the uniform core (y < 56, z 265..305, x = x_c): tau = peak density x length
            // (14.7.6 says 0.11 x 30 = 3.3; D-030 raised the peak to 0.16 /m: 0.16 x 30 = 4.8)
            double t = FogMath.TauBank(new D3(0.0, 10.0, 270.0), new D3(0, 0, 1), 30.0, 0.0, 0.0, ThreadConstants.BankSamples);
            c.Near(t, ThreadConstants.BankDensity * 30.0, 1e-9, "30 m through the uniform core");
            // the box moves down with the ground group (offset -A): a camera 1 km above the sunk box sees nothing horizontally
            c.Near(FogMath.TauBank(new D3(0.0, 10.0, 270.0), new D3(0, 0, 1), 30.0, 0.0, -1000.0), 0.0, 0.0, "box sunk by 1 km");
            c.AssertAll();
        }

        // ================================================================================================
        // Deck slab (14.7.6 item 3, D-006)
        // ================================================================================================

        const int DeckRays = 2000;
        const int DeckReferenceSamples = 4096;   // task B12: 4 096-sample numeric integral
        const double DeckRelTol = 0.005;         // within 0.5 percent

        /// <summary>Midpoint integral of the deck profile along the ray, restricted to the slab (profile is 0 outside).</summary>
        static double NumericDeckColumn(double y0, double dy, double d, double top, double bot, int n)
        {
            double t0 = 0.0, t1 = d;
            if (Math.Abs(dy) > 1e-15)
            {
                double ta = (bot - y0) / dy, tb = (top - y0) / dy;
                t0 = Math.Max(0.0, Math.Min(ta, tb));
                t1 = Math.Min(d, Math.Max(ta, tb));
                if (t1 <= t0) return 0.0;
            }
            double h = (t1 - t0) / n, sum = 0.0;
            for (int i = 0; i < n; i++) sum += FogMath.DeckProfile(y0 + dy * (t0 + (i + 0.5) * h), top, bot);
            return sum * h;
        }

        [Test]
        public void TDeck_ClosedFormMatchesNumericIntegral()
        {
            var c = new CoreTests.Checks("deck optical depth (14.7.6: closed form vs a 4 096-sample numeric integral within 0.5 %)");
            var rng = new CoreTests.Rng(0x4445434B5F534C42UL);   // "DECK_SLB"
            double worstRel = 0;
            string worstCase = "none";
            int through = 0;
            for (int i = 0; i < DeckRays; i++)
            {
                // cameras on the walkway around the deck pass: below (s < 578), inside, and above (s > 595) the slab
                double s = rng.Range(560.0, 620.0);
                double lateral = rng.Range(-1.0, 1.0) * ThreadConstants.HalfWidthDeck;
                D3 cam = ThreadMath.CorridorToWorld(s, lateral, ThreadConstants.EyeHeight);
                double A = ThreadMath.VirtualAltitude(s);
                FogMath.DeckSlab(cam.y, A, out double top, out double bot);

                D3 dir = rng.UnitVector();
                // the |r_y| < 1e-4 guard of TauDeck shifts the slab edge by 0.006 deg (0.1 px) where the true
                // optical depth is itself discontinuous (r_y = 0 separates rays that reach the slab from rays
                // that never do); those directions are checked separately with exactly horizontal rays
                if (Math.Abs(dir.y) < 2e-4) dir = new D3(dir.x, dir.y < 0 ? -2e-4 : 2e-4, dir.z).Normalized;
                double d = rng.LogRange(1.0, 2.0e5);
                if (i % 4 == 0)
                {
                    // aimed through the whole slab when the camera is outside it
                    if (cam.y < bot && dir.y < 0) dir = new D3(dir.x, -dir.y, dir.z);
                    if (cam.y > top && dir.y > 0) dir = new D3(dir.x, -dir.y, dir.z);
                    double far = dir.y > 0 ? top - cam.y : cam.y - bot;
                    d = Math.Max(d, 1.2 * far / Math.Abs(dir.y));
                }

                double closed = FogMath.TauDeck(cam, dir, d, 1.0, top, bot);
                double numeric = ThreadConstants.DeckDensity * NumericDeckColumn(cam.y, dir.y, d, top, bot, DeckReferenceSamples);
                double err = Math.Abs(closed - numeric), allowed = Math.Max(DeckRelTol * numeric, 1e-9);
                if (numeric > 1e-3) through++;
                double rel = numeric > 0 ? err / numeric : (err > 0 ? double.PositiveInfinity : 0.0);
                string desc = Inv($"ray {i}: s = {s:F2} (A = {A:F0} m), cam y {cam.y:F2}, slab {bot:F1}..{top:F1}, dir {V(dir)}, d = {d:F1} m: closed {closed:G7}, numeric {numeric:G7} ({rel * 100:F3} %)");
                if (numeric > 0 && rel > worstRel) { worstRel = rel; worstCase = desc; }
                c.That(IsFinite(closed) && closed >= 0.0 && err <= allowed, desc);
            }
            c.That(through > DeckRays / 4, Inv($"only {through} of {DeckRays} rays went through the slab"));
            CoreTests.Log(Inv($"deck: {through} of {DeckRays} rays through the slab, worst relative error {worstRel * 100:F4} %: {worstCase}"));
            c.AssertAll();
        }

        [Test]
        public void TDeck_HorizontalRaysAndGateScaling()
        {
            var c = new CoreTests.Checks("deck optical depth: horizontal rays, gate scaling, slab placement (14.7.6, D-006)");
            double camY = 100.0;
            // slab placement: top = y_cam - (A - 3000), 1 200 m thick, the camera sits at altitude A inside it
            double[] altitudes = { 1000, 1800, 2400, 2990, 3000, 3200, 10000 };
            for (int i = 0; i < altitudes.Length; i++)
            {
                double A = altitudes[i];
                FogMath.DeckSlab(camY, A, out double top, out double bot);
                c.Near(top, camY - (A - 3000.0), 1e-9, Inv($"deck top for A = {A:F0}"));
                c.Near(top - bot, 1200.0, 1e-9, Inv($"deck thickness for A = {A:F0}"));
                c.Near(camY - bot, A - 1800.0, 1e-9, Inv($"camera height above the deck bottom = A - 1.8 km for A = {A:F0}"));
                // horizontal rays: the profile at the camera times the length, matched by the numeric integral
                D3 cam = new D3(10.0, camY, 20.0), dir = new D3(0.6, 0.0, 0.8);
                double d = 5000.0;
                double closed = FogMath.TauDeck(cam, dir, d, 1.0, top, bot);
                double numeric = ThreadConstants.DeckDensity * NumericDeckColumn(cam.y, 0.0, d, top, bot, 64);
                c.Near(closed, numeric, 1e-9 * Math.Max(1.0, numeric), Inv($"horizontal ray at A = {A:F0}"));
                // the gate scales the optical depth linearly and 0 switches it off
                D3 up = new D3(0.0, 1.0, 0.0);
                double full = FogMath.TauDeck(cam, up, 1e7, 1.0, top, bot);
                c.Near(FogMath.TauDeck(cam, up, 1e7, 0.37, top, bot), 0.37 * full, 1e-9 * Math.Max(1.0, full), Inv($"gate 0.37 at A = {A:F0}"));
                c.Near(FogMath.TauDeck(cam, up, 1e7, 0.0, top, bot), 0.0, 0.0, Inv($"gate 0 at A = {A:F0}"));
            }
            // full vertical crossing. 14.7.6 item 3 (line 860) tapers the top 150 m over three stacked slabs, which with the
            // 100 m bottom skin of D-006 gave 0.09 x (1 200 - 100 / 2 - 150 / 2) = 96.75; D-031 widens the top skin to 3 x 200 m.
            // A smoothstep skin of width w removes w / 2 of the column; top sub-slab i (start DeckSkin - i w below the top)
            // removes a third of (DeckSkin - i w - w / 2). With the decided skins: 0.09 x (1 200 - 50 - 300) = 76.5.
            double thickness = ThreadConstants.CloudTop - ThreadConstants.CloudBottom, topLoss = 0.0;
            for (int i = 0; i < 3; i++)
                topLoss += (ThreadConstants.DeckSkin - ThreadConstants.DeckTopSkinStep * i - 0.5 * ThreadConstants.DeckTopSkinStep) / 3.0;
            double column = ThreadConstants.DeckDensity * (thickness - 0.5 * ThreadConstants.DeckBottomSkin - topLoss);
            FogMath.DeckSlab(camY, 1000.0, out double t0, out double b0);
            c.Near(FogMath.TauDeck(new D3(0, camY, 0), new D3(0, 1, 0), 1e7, 1.0, t0, b0), column, 1e-6, "vertical optical depth of the whole slab");
            // the same column as a dense numeric integral of the FogMath profile (independent of the closed-form antiderivative)
            c.Near(ThreadConstants.DeckDensity * NumericDeckColumn(camY, 1.0, 1e7, t0, b0, 1 << 16), column, 1e-6, "vertical optical depth of the whole slab (numeric integral of DeckProfile)");
            CoreTests.Log(Inv($"deck: vertical optical depth of the whole slab {column:F4} (D-031 top skin {ThreadConstants.DeckSkin:F0} m in {ThreadConstants.DeckTopSkinStep:F0} m steps)"));
            c.AssertAll();
        }

        [Test]
        public void TDeck_GateD006()
        {
            var c = new CoreTests.Checks("deck gate (D-006: smoothstep(1.2 km, 1.8 km, A): 0 at s = 560, 1 from s = 578)");
            double g560 = FogMath.DeckGate(ThreadMath.VirtualAltitude(560.0));
            c.That(g560 == 0.0, Inv($"gate at s = 560 (A = {ThreadMath.VirtualAltitude(560.0):F1} m) is {g560:G6}, expected 0"));
            // A(578) = 1 792 m, 8 m short of 1.8 km: the gate is 0.9995 there (optically identical to 1)
            for (double s = 578.0; s <= ThreadConstants.PathLength + 1e-9; s += 0.25)
            {
                double A = ThreadMath.VirtualAltitude(s), g = FogMath.DeckGate(A);
                c.That(g >= 0.999, Inv($"gate at s = {s:F2} (A = {A:F1} m) is {g:F6}, expected 1"));
                if (s >= 579.0) c.That(g == 1.0, Inv($"gate at s = {s:F2} (A = {A:F1} m) is {g:F9}, expected exactly 1"));
            }
            double prev = -1.0;
            for (double s = 0.0; s <= ThreadConstants.PathLength + 1e-9; s += 0.05)
            {
                double g = FogMath.DeckGate(ThreadMath.VirtualAltitude(s));
                c.That(g >= prev, Inv($"gate decreases at s = {s:F2}"));
                if (s <= 560.0) c.That(g == 0.0, Inv($"gate at s = {s:F2} is {g:G6} before the approach (expected 0)"));
                prev = g;
            }
            CoreTests.Log(Inv($"D-006: A(560) = {ThreadMath.VirtualAltitude(560.0):F1} m, A(578) = {ThreadMath.VirtualAltitude(578.0):F1} m, gate(578) = {FogMath.DeckGate(ThreadMath.VirtualAltitude(578.0)):F6}"));
            c.AssertAll();
        }

        // ================================================================================================
        // Ground group deactivation inside the deck (D-007)
        // ================================================================================================

        [Test]
        public void GroundDeactivation_D007()
        {
            var c = new CoreTests.Checks("ground deactivation (D-007: s = 590 inside the deck slab, A between 1.8 and 3.0 km, hidden by optical depth above 5)");
            c.Near(ThreadConstants.GroundOffS, 590.0, 1e-12, "GroundOffS (D-007)");
            c.That(ThreadConstants.GroundOffS <= ThreadConstants.GroundDeactivateS, Inv($"GroundOffS {ThreadConstants.GroundOffS:F0} must not be later than the 14.6 deactivation at s = {ThreadConstants.GroundDeactivateS:F0}"));
            // the switch (590) and the re-activation point of the hysteresis (586, D-007) both lie inside the slab
            double[] points = { ThreadConstants.GroundOffS, 586.0 };
            for (int i = 0; i < points.Length; i++)
            {
                double s = points[i];
                double A = ThreadMath.VirtualAltitude(s);
                c.That(A >= 1800.0 && A <= 3000.0, Inv($"virtual altitude at s = {s:F0} is {A / 1000.0:F3} km, expected 1.8..3.0 km"));
                D3 cam = ThreadMath.CorridorToWorld(s, 0.0, ThreadConstants.EyeHeight);
                FogMath.DeckSlab(cam.y, A, out double top, out double bot);
                c.That(cam.y > bot && cam.y < top, Inv($"camera y {cam.y:F2} at s = {s:F0} outside the slab {bot:F1}..{top:F1}"));
                double gate = FogMath.DeckGate(A);
                double up = FogMath.TauDeck(cam, new D3(0, 1, 0), 1e7, gate, top, bot);
                double down = FogMath.TauDeck(cam, new D3(0, -1, 0), cam.y - bot + 1000.0, gate, top, bot);
                // vertical rays are the shortest way out of a horizontal slab, so every direction has at least min(up, down)
                c.That(up > 5.0, Inv($"optical depth straight up from s = {s:F0} is {up:F2} (14.6: activation hidden by optical depth above 5)"));
                c.That(down > 5.0, Inv($"optical depth straight down from s = {s:F0} is {down:F2} (14.6: activation hidden by optical depth above 5)"));
                CoreTests.Log(Inv($"D-007: s = {s:F0}, A = {A:F1} m, gate {gate:F4}, optical depth up {up:F2}, down {down:F2}"));
            }
            c.AssertAll();
        }

        // ================================================================================================
        // Distance fog closed form (14.7.6 item 1)
        // ================================================================================================

        [Test]
        public void TDistance_ClosedFormMatchesNumericIntegral()
        {
            var c = new CoreTests.Checks("distance fog (14.7.6: tau = k integral exp(-y / H_f) ds in closed form vs a 4 096-sample integral within 0.5 %)");
            var rng = new CoreTests.Rng(0x444953545F464F47UL);   // "DIST_FOG"
            const int rays = 2000, n = 4096;
            double worstRel = 0;
            string worstCase = "none";
            for (int i = 0; i < rays; i++)
            {
                double s = rng.Range(0.0, ThreadConstants.PathLength);
                D3 cam = ThreadMath.CorridorToWorld(s, rng.Range(-1.0, 1.0) * ThreadMath.HalfWidth(s), ThreadConstants.EyeHeight);
                double k = ThreadMath.FogK(s), H = ThreadMath.FogHeightScale(s), A = ThreadMath.VirtualAltitude(s);
                D3 dir = i % 5 == 0 ? rng.AtElevation(rng.Range(-0.05, 0.05) * Deg) : rng.UnitVector();
                double d = rng.LogRange(1.0, 2.0e4);
                double closed = FogMath.TauDistance(cam, dir, d, k, H, A);
                // numeric reference: k x integral over the ray of exp(-(y_cam + A + r_y t) / H)
                double y0 = cam.y + A, h = d / n, sum = 0.0;
                for (int j = 0; j < n; j++) sum += Math.Exp(-(y0 + dir.y * (j + 0.5) * h) / H);
                double numeric = k * sum * h;
                if (k <= 0)
                {
                    c.That(closed == 0.0, Inv($"ray {i}: no distance fog at s = {s:F1} but tau = {closed:G6}"));
                    continue;
                }
                double rel = numeric > 0 ? Math.Abs(closed - numeric) / numeric : 0.0;
                string desc = Inv($"ray {i}: s = {s:F1}, k = {k:G4}, H = {H:F0}, A = {A:F0}, cam y {cam.y:F2}, dir {V(dir)}, d = {d:F1}: closed {closed:G7}, numeric {numeric:G7} ({rel * 100:F4} %)");
                if (rel > worstRel) { worstRel = rel; worstCase = desc; }
                c.That(IsFinite(closed) && closed >= 0.0 && rel <= 0.005, desc);
            }
            CoreTests.Log(Inv($"distance fog: worst relative error {worstRel * 100:F5} %: {worstCase}"));
            c.AssertAll();
        }

        // ================================================================================================
        // Atmosphere (fix wave 3): hue-preserving haze, ordered composite, deck veil, bank footprint
        // ================================================================================================

        static FogMath.Rgb Pal(string token) => FogMath.Pal(token);
        static FogMath.Rgb Mul(FogMath.Rgb a, FogMath.Rgb b) => new FogMath.Rgb(a.r * b.r, a.g * b.g, a.b * b.b);
        static string C(FogMath.Rgb c) => Inv($"({c.r:F4}, {c.g:F4}, {c.b:F4})");
        static double Ch(FogMath.Rgb c, int i) => i == 0 ? c.r : (i == 1 ? c.g : c.b);
        /// <summary>Largest per-channel difference relative to the reference channel (14.7.2 horizon rule: 1 percent).</summary>
        static double RelDiff(FogMath.Rgb a, FogMath.Rgb reference)
        {
            double m = 0;
            for (int i = 0; i < 3; i++) m = Math.Max(m, Math.Abs(Ch(a, i) - Ch(reference, i)) / Math.Max(Math.Abs(Ch(reference, i)), 1e-4));
            return m;
        }
        static void Lch(FogMath.Rgb c, out double C, out double h)
        {
            FogMath.ToOklab(c, out _, out double a, out double b);
            C = Math.Sqrt(a * a + b * b);
            h = Math.Atan2(b, a) / Deg;
        }
        static D3 Dir(double azimuthDeg, double elevationSin)
        {
            double ch = Math.Sqrt(Math.Max(1.0 - elevationSin * elevationSin, 0.0));
            return new D3(Math.Sin(azimuthDeg * Deg) * ch, elevationSin, Math.Cos(azimuthDeg * Deg) * ch);
        }
        // a lit salt plain (beat-1 radiance of the ground near the camera, run07 KF1 rows ~1000)
        static readonly FogMath.Rgb Salt = new FogMath.Rgb(0.70, 0.62, 0.50);
        // beat-1 side-face light: 0.4 x ambient side + 0.7 x sun (14.2.2 recipe)
        static readonly FogMath.Rgb SideLight = new FogMath.Rgb(1.02, 0.92, 0.77);

        [Test]
        public void THaze_LuminanceEndsAndHue()
        {
            var c = new CoreTests.Checks("hue-preserving haze (C-09): physical luminance, exact ends, D-015 contrast law, hue kept");
            string[] toks = { "TEAL", "TEAL_DEEP", "CORAL", "CORAL_DEEP", "INK", "SAND", "SAND_SHADE", "SALT_WHITE", "CREAM", "STATION_MINT", "STATION_LILAC", "STATION_PEACH" };
            var fogs = new[] { FogMath.SkyColor(0.0, 0.0), FogMath.SkyColor(Math.Sin(40 * Deg), 0.0), FogMath.SkyColor(0.0, 0.45),
                               FogMath.Min(Pal("CLOUD_TOP"), 0.97), FogMath.SkyColor(0.3, 0.9), Pal("CLOUD_SHADE") };
            var rng = new CoreTests.Rng(0x48415A455F4D4958UL);   // "HAZE_MIX"
            double worstY = 0, worstD015 = 0;
            for (int i = 0; i < 4000; i++)
            {
                var a = Pal(toks[i % toks.Length]);
                var b = Pal(toks[(i * 7 + 3) % toks.Length]);
                double g = rng.Range(0.2, 1.6);
                a = new FogMath.Rgb(a.r * g, a.g * g, a.b * g);
                var f = fogs[(i / toks.Length) % fogs.Length];
                double T = i % 50 == 0 ? 0.0 : (i % 50 == 1 ? 1.0 : rng.Range(0.0, 1.0));
                var h = FogMath.HazeMix(a, f, T);
                double Y = FogMath.Luma(f) + (FogMath.Luma(a) - FogMath.Luma(f)) * T;
                double eY = Math.Abs(FogMath.Luma(h) - Y);
                worstY = Math.Max(worstY, eY);
                c.That(eY <= 1e-9 + 1e-9 * Y, Inv($"luminance of HazeMix({C(a)}, {C(f)}, T = {T:F4}) = {FogMath.Luma(h):G9}, physical {Y:G9}"));
                c.That(h.r >= 0 && h.g >= 0 && h.b >= 0 && IsFinite(h.r + h.g + h.b), Inv($"HazeMix({C(a)}, {C(f)}, {T:F4}) = {C(h)} (negative or not finite)"));
                // D-015: the luminance contrast of two colours under the same haze is exactly T times the unfogged one
                var hb = FogMath.HazeMix(b, f, T);
                double eC = Math.Abs((FogMath.Luma(h) - FogMath.Luma(hb)) - T * (FogMath.Luma(a) - FogMath.Luma(b)));
                worstD015 = Math.Max(worstD015, eC);
                c.That(eC <= 1e-9, Inv($"contrast law: {C(a)} vs {C(b)} under {C(f)} at T = {T:F4} off by {eC:G3}"));
            }
            // exact ends and continuity at both ends (no seam where the fog saturates or vanishes)
            foreach (var f in fogs)
                for (int i = 0; i < toks.Length; i++)
                {
                    var a = Pal(toks[i]);
                    c.That(RelDiff(FogMath.HazeMix(a, f, 0.0), f) == 0.0, Inv($"T = 0 must give the fog colour exactly ({toks[i]})"));
                    c.That(RelDiff(FogMath.HazeMix(a, f, 1.0), a) == 0.0, Inv($"T = 1 must give the colour exactly ({toks[i]})"));
                    c.That(RelDiff(FogMath.HazeMix(a, f, 2e-6), f) < 1e-3, Inv($"T = 2e-6 within 0.1 % of the fog colour ({toks[i]} under {C(f)}: {C(FogMath.HazeMix(a, f, 2e-6))})"));
                    c.That(RelDiff(FogMath.HazeMix(a, f, 1.0 - 1e-6), a) < 1e-3, Inv($"T = 1 - 1e-6 within 0.1 % of the colour ({toks[i]})"));
                }
            // hue: teal under the warm horizon / CLOUD_TOP and coral under the blue sky keep their hue (within 10 deg in OKLab)
            // and more chroma than the linear mix for the light hazes of the key frames (T 0.5 .. 0.8), at least 1.4 x at T <= 0.6
            var sky67 = FogMath.SkyColor(Math.Sin(6.7 * Deg), 0.0);
            var sky40 = FogMath.SkyColor(Math.Sin(40 * Deg), 0.0);
            var cases = new[]
            {
                ("teal robe lit / sky 6.7 deg (KF1)", Mul(Pal("TEAL"), SideLight), sky67),
                ("teal cable / CLOUD_TOP (KF4a)", Mul(Pal("TEAL"), SideLight), FogMath.Min(Pal("CLOUD_TOP"), 0.97)),
                ("coral robe / sky 40 deg (KF1)", Mul(Pal("CORAL"), SideLight), sky40),
                ("coral deep / sky 40 deg (KF1)", Mul(Pal("CORAL_DEEP"), SideLight), sky40),
            };
            foreach (var (name, col, fog) in cases)
            {
                Lch(col, out double C0, out double h0);
                for (double T = 0.5; T <= 0.8 + 1e-9; T += 0.1)
                {
                    var h = FogMath.HazeMix(col, fog, T);
                    var lin = new FogMath.Rgb(fog.r + (col.r - fog.r) * T, fog.g + (col.g - fog.g) * T, fog.b + (col.b - fog.b) * T);
                    Lch(h, out double C1, out double h1);
                    Lch(lin, out double C2, out double h2);
                    double dh = Math.Abs(((h1 - h0) % 360 + 540) % 360 - 180);
                    c.That(dh <= 10.0, Inv($"{name}, T = {T:F1}: hue {h1:F1} deg drifts {dh:F1} deg from {h0:F1} (linear mix: {h2:F1})"));
                    double need = T <= 0.6 + 1e-9 ? 1.4 : 1.0;
                    c.That(C1 >= need * C2, Inv($"{name}, T = {T:F1}: chroma {C1:F4} below {need:F1} x the linear mix's {C2:F4}"));
                }
            }
            CoreTests.Log(Inv($"haze: worst luminance error {worstY:G3}, worst contrast-law error {worstD015:G3}"));
            c.AssertAll();
        }

        [Test]
        public void TFog_DeckPassHorizonSeamless()
        {
            var c = new CoreTests.Checks("deck-pass horizon (C-06 / A12 / B8 / PAC-r2-1): overcast, below-horizon sky and ground meet within 1 % for s 560..594");
            double worstH = 0, worstRim = 0, worstStrip = 0;
            string worstHCase = "none";
            for (double s = 560.0; s <= 594.0 + 1e-9; s += 0.1)
            {
                D3 cam = ThreadMath.CorridorToWorld(s, 0.0, ThreadConstants.EyeHeight);
                var fr = FogMath.Frame.ForProgress(s, cam);
                for (double az = -60; az <= 60; az += 30)
                {
                    // (a) the horizon: the overcast just above against the sky just below
                    foreach (double e in new[] { 1e-5, 1e-4, 1e-3 })
                    {
                        var up = FogMath.ApplySky(fr, FogMath.SkyColor(e, fr.D), Dir(az, e));
                        var dn = FogMath.ApplySky(fr, FogMath.SkyColor(-e, fr.D), Dir(az, -e));
                        double r = RelDiff(dn, up);
                        if (r > worstH) { worstH = r; worstHCase = Inv($"s = {s:F1}, az {az:F0}, e {e:G2}: above {C(up)}, below {C(dn)}"); }
                        c.That(r <= 0.01, Inv($"horizon at s = {s:F1} (gate {fr.gate:F4}), az {az:F0}, +-{e:G2}: above {C(up)}, below {C(dn)} ({r * 100:F2} %)"));
                    }
                    // (b) the 14 km ground rim against the sky just beyond it, and (c) the strip between the horizon and the rim:
                    // a below-horizon sky ray has exactly the ground's distance colour
                    double hc = cam.y + fr.aVirt;
                    double sinRim = -hc / Math.Sqrt(hc * hc + 13980.0 * 13980.0);
                    D3 dRim = Dir(az, sinRim);
                    var sky = FogMath.ApplySky(fr, FogMath.SkyColor(sinRim, fr.D), dRim);
                    if (s < ThreadConstants.GroundOffS)
                    {
                        double d = hc / -sinRim;
                        FogMath.GeometryTaus(fr, dRim, d, 1.0 / d, out double tD, out double tC);
                        var ground = FogMath.FogMix(fr, Salt, tD, tC, dRim.y);
                        double r = RelDiff(ground, sky);
                        worstRim = Math.Max(worstRim, r);
                        c.That(r <= 0.01, Inv($"ground rim at s = {s:F1}, az {az:F0}: ground {C(ground)}, sky {C(sky)} ({r * 100:F2} %)"));
                    }
                    if (cam.y < fr.deckBot)
                    {
                        double sinStrip = 0.5 * sinRim;
                        var strip = FogMath.ApplySky(fr, FogMath.SkyColor(sinStrip, fr.D), Dir(az, sinStrip));
                        var dist = FogMath.FogDistanceColour(fr, sinStrip);
                        double r = RelDiff(strip, dist);
                        worstStrip = Math.Max(worstStrip, r);
                        c.That(r <= 1e-9, Inv($"strip at s = {s:F1}: below-horizon sky {C(strip)} differs from the ground's distance colour {C(dist)}"));
                    }
                }
            }
            CoreTests.Log(Inv($"deck pass: worst horizon difference {worstH * 100:F3} % ({worstHCase}), worst rim {worstRim * 100:F3} %, strip {worstStrip:G3}"));
            c.AssertAll();
        }

        [Test]
        public void TFog_HorizonTowardTheBank()
        {
            // the first pixel row under the horizon against the row above it (0.25 / 0.5 px each side); one row further down
            // the ground is no longer fully fogged at V = 1.6 km (the physical haze gradient, not a seam)
            var c = new CoreTests.Checks("horizon toward the bank (C-r2-3 / E-r2-3, 14.7.2): ground under the horizon meets the sky within 1 % per channel, bank on and off");
            const double f = 1080.0 / 2.0 / 0.6008606190;   // focal length in px at 1080p, FOV 62 (tan 31 deg)
            double worst = 0;
            string worstCase = "none";
            foreach (double s in new[] { 0.0, 30.0, 100.0, 200.0, 380.0, 440.0 })
            {
                D3 cam = ThreadMath.CorridorToWorld(s, 0.0, ThreadConstants.EyeHeight);
                var fr = FogMath.Frame.ForProgress(s, cam);
                double hc = cam.y + fr.aVirt;
                for (double az = -80; az <= 80; az += 10)
                    foreach (double px in new[] { 0.25, 0.5 })
                    {
                        double dep = px / f;
                        double d = Math.Min(hc / Math.Tan(dep), 13980.0) / Math.Cos(dep);
                        D3 dg = Dir(az, -Math.Sin(dep));
                        foreach (bool footprint in new[] { false, true })
                        {
                            double u = 1.0 / d, uLo = footprint ? u - FogMath.BankFootPx * Math.Cos(dep) / (hc * f) : u;
                            FogMath.GeometryTaus(fr, dg, d, uLo, out double tD, out double tC);
                            var ground = FogMath.FogMix(fr, Salt, tD, tC, dg.y);
                            var sky = FogMath.ApplySky(fr, FogMath.SkyColor(Math.Sin(dep), fr.D), Dir(az, Math.Sin(dep)));
                            double r = RelDiff(ground, sky);
                            if (r > worst) { worst = r; worstCase = Inv($"s = {s:F0}, az {az:F0}, {px:F1} px, footprint {footprint}: ground {C(ground)} sky {C(sky)}"); }
                            c.That(r <= 0.01, Inv($"s = {s:F0} (bank {(fr.bankOn ? "on" : "off")}), az {az:F0}, {px:F1} px under the horizon, footprint {footprint}: ground {C(ground)}, sky {C(sky)} ({r * 100:F2} %)"));
                        }
                    }
            }
            CoreTests.Log(Inv($"horizon toward the bank: worst ground / sky difference {worst * 100:F3} % ({worstCase})"));
            c.AssertAll();
        }

        [Test]
        public void TBank_FootprintFilter()
        {
            var c = new CoreTests.Checks("bank footprint filter (C-r2-3 / E-r2-3): exact without a window, 16 vs 256 samples, soft bank foot at KF1");
            var rng = new CoreTests.Rng(0x464F4F545052494EUL);   // "FOOTPRIN"
            const double f = 1080.0 / 2.0 / 0.6008606190;
            int fails = 0;
            double worstRatio = 0;
            for (int i = 0; i < 1500; i++)
            {
                double s = rng.Range(0.0, 340.0);
                D3 cam = ThreadMath.CorridorToWorld(s, rng.Range(-1.0, 1.0) * ThreadMath.HalfWidth(s), ThreadConstants.EyeHeight);
                double A = ThreadMath.VirtualAltitude(s), xc = ThreadMath.BankDrift(rng.Range(0.0, 600.0), 1.0);
                double hc = cam.y + A;
                double dep = rng.LogRange(0.3, 200.0) / f;                         // 0.3 .. 200 px under the horizon
                D3 dir = Dir(rng.Range(-70.0, 70.0), -Math.Sin(dep));
                double d = Math.Min(hc / Math.Tan(dep), 13980.0) / Math.Cos(dep);
                double u = 1.0 / d;
                // no window: exactly ThreadTauBank
                double t0 = FogMath.TauBank(cam, dir, d, xc, -A), tw0 = FogMath.TauBankFootprint(cam, dir, d, u, xc, -A);
                c.That(tw0 == t0, Inv($"ray {i}: window-less footprint {tw0:G9} differs from TauBank {t0:G9}"));
                // a 12 px window: 16 samples against a 256-sample reference (T-BANK tolerance)
                double uLo = u - FogMath.BankFootPx * Math.Cos(dep) / (hc * f);
                double t16 = FogMath.TauBankFootprint(cam, dir, d, uLo, xc, -A, 16), t256 = FogMath.TauBankFootprint(cam, dir, d, uLo, xc, -A, 256);
                double allowed = Math.Max(BankAbsTol, BankRelTol * t256), err = Math.Abs(t16 - t256);
                worstRatio = Math.Max(worstRatio, err / allowed);
                if (!(err <= allowed)) fails++;
                c.That(IsFinite(t16) && t16 >= t0 - 1e-12 && err <= allowed, Inv($"ray {i}: s = {s:F1}, {dep / Deg:F4} deg under the horizon, d = {d:F1}: tau16 {t16:F5}, tau256 {t256:F5}, window-less {t0:F5}"));
            }
            // the bank foot at KF1: per-row change of the bank transmittance on the ground under the horizon
            var kf1 = FogMath.Frame.ForProgress(0.0, new D3(0.0, ThreadConstants.EyeHeight, 0.0));
            double worstSharp = 0, worstSoft = 0;
            for (double az = -40; az <= 40; az += 10)
            {
                double prevSharp = -1, prevSoft = -1;
                for (double px = 0.5; px < 40.0; px += 1.0)
                {
                    double dep = px / f, hc = ThreadConstants.EyeHeight;
                    D3 dir = Dir(az, -Math.Sin(dep));
                    double d = Math.Min(hc / Math.Tan(dep), 13980.0) / Math.Cos(dep), u = 1.0 / d;
                    double sharp = Math.Exp(-FogMath.TauBank(kf1.cam, dir, d, 0.0, kf1.groupOffsetY, 16, kf1.bankScale));
                    double soft = Math.Exp(-FogMath.TauBankFootprint(kf1.cam, dir, d, u - FogMath.BankFootPx * Math.Cos(dep) / (hc * f), 0.0, kf1.groupOffsetY, 16, kf1.bankScale));
                    if (prevSharp >= 0) { worstSharp = Math.Max(worstSharp, Math.Abs(sharp - prevSharp)); worstSoft = Math.Max(worstSoft, Math.Abs(soft - prevSoft)); }
                    prevSharp = sharp; prevSoft = soft;
                }
            }
            c.That(worstSharp > 0.5, Inv($"scenario check: without the window the KF1 bank foot should be a one-row step (max per-row change {worstSharp:F3})"));
            c.That(worstSoft <= 0.1, Inv($"KF1 bank foot: max per-row change of the bank transmittance {worstSoft:F3} with the footprint window (limit 0.1)"));
            CoreTests.Log(Inv($"bank footprint: {fails} of 1500 windows outside the T-BANK tolerance (worst {worstRatio:F3} x); KF1 foot per-row change {worstSharp:F3} -> {worstSoft:F3}"));
            c.AssertAll();
        }
    }
}
