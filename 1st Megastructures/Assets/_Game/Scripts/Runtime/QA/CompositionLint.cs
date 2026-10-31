using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Key-frame composition lint (14.10, Part 10 item 35, 11.2): projects the world bounds of every subject a beat names for
    /// a key shot (<see cref="BeatBase.CollectSubjects"/>) through the capture camera at the capture aspect, and checks that the
    /// centre lies inside 5-95 % of the frame and that the on-screen size reaches the subject's minimum. The 3 % margin of
    /// 11.2 (subject not clipped by the frame) is reported as <c>margin_ok</c>. Screen fractions use a top-left origin like
    /// the Python tools. Boxes crossing the near plane are clipped edge by edge before projection.
    ///
    /// Known spec conflicts: a subject whose name ends with <see cref="KnownConflictSuffix"/> (" [spec-conflict]") names an
    /// object the fixed 14.10 pose cannot frame, documented in DEVELOPMENT.md (KS4: the inner-right cylinder B and the
    /// inner-left crank wheel; KS1: the far doorway behind the sloped stair ceiling, should a beat name it). When it fails
    /// the lint it is waived: status "pass_with_known_conflict", not a failure, and
    /// listed under "known_conflicts" in the shot's lint block and in report.json's lint_summary. <see cref="SubjectResult.Pass"/>
    /// stays the plain geometric verdict (the beat tests assert that a tagged subject still fails, so a stale tag shows).
    ///
    /// Fix round R1 (critique R1-29, R1-39, R1-20; arbitration returned to the lead):
    /// <list type="bullet">
    /// <item><b>In-frame part of oversize subjects.</b> A subject whose projected box is larger than the frame in either axis or
    /// is cut by the near plane (a rail around the viewer, a 30 m crank wheel beside the catwalk) has no meaningful box centre;
    /// it is judged by the centre of its in-frame part (<see cref="SubjectResult.InFramePass"/>). Subjects that fit the frame
    /// keep the 14.10 box-centre rule.</item>
    /// <item><b>Occlusion.</b> In the player capture the harness reads the depth buffer of the shot and depth-tests an 8 x 8 grid
    /// of rays over the subject's in-frame rectangle against the subject's box (<see cref="Visibility"/>): the fraction of rays
    /// that reach the box unoccluded must be at least <see cref="MIN_VISIBLE"/> (30 %). A subject hidden behind a deck, a stair
    /// ceiling or a column fails even when its box projects into the frame.</item>
    /// </list>
    /// </summary>
    public static class CompositionLint
    {
        public const float CENTRE_MIN = 0.05f, CENTRE_MAX = 0.95f, MARGIN = 0.03f;

        /// <summary>Minimum unoccluded fraction of a subject's in-frame footprint (R1-29).</summary>
        public const float MIN_VISIBLE = 0.30f;
        /// <summary>Visibility sample grid per axis (8 x 8 = 64 rays).</summary>
        public const int VIS_GRID = 8;

        /// <summary>Name suffix of a documented spec conflict (same text as Beat04_Hall.SpecConflictTag).</summary>
        public const string KnownConflictSuffix = " [spec-conflict]";

        public const string StatusPass = "pass", StatusFail = "fail", StatusWaived = "pass_with_known_conflict", StatusNone = "no_subjects";

        public static bool IsKnownConflict(string subjectName) =>
            subjectName != null && subjectName.EndsWith(KnownConflictSuffix, System.StringComparison.Ordinal);

        /// <summary>
        /// Spec conflicts that DEVELOPMENT.md has logged for a subject its beat names without the tag (critique R2-36 b): base tag,
        /// subject name, DEVELOPMENT.md row. The lint treats them exactly like a tagged subject (name + " [spec-conflict]").
        /// </summary>
        public static readonly string[,] LoggedConflicts =
        {
            // ledge row "KS2b 'truss below'": the single plane Warren truss under the deck axis is hidden by the 1.2 m deck at pitch -90
            { "KS2b_bridge_down", "truss", "ledge: KS2b 'truss below' (accepted as not visible at pitch -90)" },
        };

        /// <summary>True when (baseTag, subjectName) is in <see cref="LoggedConflicts"/>.</summary>
        public static bool IsLoggedConflict(string baseTag, string subjectName)
        {
            for (int i = 0; i < LoggedConflicts.GetLength(0); i++)
            {
                if (string.Equals(LoggedConflicts[i, 0], baseTag, System.StringComparison.Ordinal)
                    && string.Equals(LoggedConflicts[i, 1], subjectName, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public struct SubjectResult
        {
            public string Name;
            public bool Behind;
            /// <summary>Projected rectangle (unclamped fractions, top-left origin).</summary>
            public float X0, Y0, X1, Y1;
            public float Cx, Cy;
            /// <summary>Visible size in fractions of the frame height: max(visible height, visible width x aspect).</summary>
            public float Size;
            public float MinSize;
            /// <summary>Pass = CentreOk and SizeOk (geometry only; a known conflict does not change it).</summary>
            public bool CentreOk, SizeOk, MarginOk, Pass;
            /// <summary>The subject carries <see cref="KnownConflictSuffix"/>.</summary>
            public bool KnownConflict;
            /// <summary>Projected box larger than the frame in an axis, or cut by the near plane.</summary>
            public bool Oversize;
            /// <summary>Centre of the in-frame part of the projected box (fractions).</summary>
            public float InCx, InCy;
            /// <summary>Oversize subject whose in-frame part is centred inside 5-95 % and large enough.</summary>
            public bool InFramePass;
            /// <summary>Unoccluded fraction of the in-frame footprint (0..1), or -1 when no depth buffer was available.</summary>
            public float Visible;
            /// <summary>Rays that hit the subject box (the denominator of <see cref="Visible"/>).</summary>
            public int VisSamples;
            /// <summary>Centre of the unoccluded samples (fractions), when any.</summary>
            public float VisCx, VisCy;
            /// <summary>Composition verdict of the harness: (box-centre rule or in-frame rule for oversize subjects) and size.</summary>
            public bool FramePass => Pass || InFramePass;
            /// <summary>Occlusion verdict: unknown (no depth) counts as pass; otherwise at least MIN_VISIBLE unoccluded.</summary>
            public bool VisibilityOk => Visible < 0f || Visible >= MIN_VISIBLE;
            /// <summary>Final lint verdict of a subject in the capture harness.</summary>
            public bool LintPass => FramePass && VisibilityOk;
            /// <summary>A known conflict that fails the lint: reported, not counted as a failure.</summary>
            public bool Waived => KnownConflict && !LintPass;
            /// <summary>"pass", "fail" or "pass_with_known_conflict".</summary>
            public string Status => LintPass ? StatusPass : (KnownConflict ? StatusWaived : StatusFail);
        }

        /// <summary>Project a world point (fractions, top-left origin). False when it lies behind the near plane.</summary>
        public static bool ProjectPoint(Vector3 p, Vector3 eye, Quaternion rot, float vfovDeg, float aspect, float near, out float u, out float v)
        {
            Vector3 c = Quaternion.Inverse(rot) * (p - eye);
            u = v = 0f;
            if (c.z < near) return false;
            float t = Mathf.Tan(0.5f * vfovDeg * Mathf.Deg2Rad);
            u = 0.5f + 0.5f * c.x / (c.z * t * Mathf.Max(1e-4f, aspect));
            v = 0.5f - 0.5f * c.y / (c.z * t);
            return true;
        }

        /// <summary>
        /// Depth-buffer occlusion test of one subject (R1-29): an 8 x 8 grid of rays over the in-frame part of the projected box;
        /// each ray that enters the box is visible when the scene's eye depth at that pixel is not in front of the box entry
        /// (tolerance 1 % + 5 cm). <paramref name="depth"/> is linear eye depth in metres per pixel, texture order (row 0 =
        /// bottom), <paramref name="w"/> x <paramref name="h"/>. Fills Visible, VisSamples, VisCx/VisCy.
        /// </summary>
        public static void Visibility(ref SubjectResult r, Bounds b, Vector3 eye, Quaternion rot, float vfovDeg, float aspect, float near,
                                      float[] depth, int w, int h)
        {
            r.Visible = -1f;
            r.VisSamples = 0;
            if (depth == null || w <= 0 || h <= 0 || depth.Length < w * h || r.Behind) return;
            float x0 = Mathf.Clamp01(r.X0), x1 = Mathf.Clamp01(r.X1), y0 = Mathf.Clamp01(r.Y0), y1 = Mathf.Clamp01(r.Y1);
            if (x1 <= x0 || y1 <= y0) { r.Visible = 0f; return; }
            float t = Mathf.Tan(0.5f * vfovDeg * Mathf.Deg2Rad), ta = t * Mathf.Max(1e-4f, aspect);
            Vector3 mn = b.min, mx = b.max;
            int hit = 0, vis = 0;
            float sx = 0f, sy = 0f;
            for (int iy = 0; iy < VIS_GRID; iy++)
            {
                for (int ix = 0; ix < VIS_GRID; ix++)
                {
                    float u = x0 + (x1 - x0) * (ix + 0.5f) / VIS_GRID;
                    float v = y0 + (y1 - y0) * (iy + 0.5f) / VIS_GRID;
                    // camera-space ray (x, y, 1): the parameter along it is the eye depth
                    Vector3 dir = rot * new Vector3((2f * u - 1f) * ta, (1f - 2f * v) * t, 1f);
                    if (!RayBox(eye, dir, mn, mx, out float tIn, out float tOut)) continue;
                    float entry = Mathf.Max(tIn, near);
                    if (tOut < entry) continue;
                    hit++;
                    int px = Mathf.Clamp((int)(u * w), 0, w - 1);
                    int py = Mathf.Clamp((int)((1f - v) * h), 0, h - 1);   // texture order: row 0 = bottom
                    float scene = depth[py * w + px];
                    if (scene >= entry * 0.99f - 0.05f)
                    {
                        vis++;
                        sx += u; sy += v;
                    }
                }
            }
            r.VisSamples = hit;
            r.Visible = hit > 0 ? (float)vis / hit : 0f;
            if (vis > 0) { r.VisCx = sx / vis; r.VisCy = sy / vis; }
        }

        /// <summary>Slab test of the ray o + t d against the box [mn, mx]: entry and exit parameters (t may be negative).</summary>
        static bool RayBox(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, out float tIn, out float tOut)
        {
            tIn = float.NegativeInfinity;
            tOut = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                float oa = o[a], da = d[a], lo = mn[a], hi = mx[a];
                if (Mathf.Abs(da) < 1e-9f)
                {
                    if (oa < lo || oa > hi) return false;
                    continue;
                }
                float t0 = (lo - oa) / da, t1 = (hi - oa) / da;
                if (t0 > t1) { float k = t0; t0 = t1; t1 = k; }
                if (t0 > tIn) tIn = t0;
                if (t1 < tOut) tOut = t1;
                if (tIn > tOut) return false;
            }
            return tOut >= 0f;
        }

        static readonly Vector3[] s_c = new Vector3[8];

        /// <summary>
        /// Project a world box through a pinhole camera (vertical FOV, aspect = width / height). Returns false when the box
        /// lies entirely behind the near plane. partlyBehind is true when the near plane cuts the box.
        /// </summary>
        public static bool ProjectBounds(Bounds b, Vector3 eye, Quaternion rot, float vfovDeg, float aspect, float near,
                                         out float x0, out float y0, out float x1, out float y1, out bool partlyBehind)
        {
            Quaternion inv = Quaternion.Inverse(rot);
            Vector3 mn = b.min, mx = b.max;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) != 0 ? mx.x : mn.x, (i & 2) != 0 ? mx.y : mn.y, (i & 4) != 0 ? mx.z : mn.z);
                s_c[i] = inv * (corner - eye);
            }
            float t = Mathf.Tan(0.5f * vfovDeg * Mathf.Deg2Rad);
            float ta = t * Mathf.Max(1e-4f, aspect);
            float u0 = float.MaxValue, v0 = float.MaxValue, u1 = float.MinValue, v1 = float.MinValue;
            bool any = false;
            partlyBehind = false;
            for (int i = 0; i < 8; i++)
            {
                if (s_c[i].z >= near) Add(s_c[i], t, ta, ref u0, ref v0, ref u1, ref v1, ref any);
                else partlyBehind = true;
            }
            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((i & bit) != 0) continue;
                    Vector3 a = s_c[i], c = s_c[i | bit];
                    bool fa = a.z >= near, fc = c.z >= near;
                    if (fa == fc) continue;
                    float k = (near - a.z) / (c.z - a.z);
                    Add(Vector3.LerpUnclamped(a, c, k), t, ta, ref u0, ref v0, ref u1, ref v1, ref any);
                }
            }
            x0 = u0; y0 = v0; x1 = u1; y1 = v1;
            if (!any) { x0 = y0 = x1 = y1 = 0f; partlyBehind = true; }
            return any;
        }

        static void Add(Vector3 p, float t, float ta, ref float u0, ref float v0, ref float u1, ref float v1, ref bool any)
        {
            float z = Mathf.Max(p.z, 1e-6f);
            float u = 0.5f + 0.5f * p.x / (z * ta);
            float v = 0.5f - 0.5f * p.y / (z * t);
            if (u < u0) u0 = u;
            if (u > u1) u1 = u;
            if (v < v0) v0 = v;
            if (v > v1) v1 = v;
            any = true;
        }

        public static SubjectResult Check(in KeyShotSubject s, Vector3 eye, Quaternion rot, float vfovDeg, float aspect, float near)
        {
            var r = new SubjectResult { Name = s.Name, MinSize = s.MinScreenFraction, KnownConflict = IsKnownConflict(s.Name), Visible = -1f };
            bool visible = ProjectBounds(s.WorldBounds, eye, rot, vfovDeg, aspect, near, out r.X0, out r.Y0, out r.X1, out r.Y1, out bool partly);
            r.Behind = !visible;
            if (!visible) return r;
            r.Cx = 0.5f * (r.X0 + r.X1);
            r.Cy = 0.5f * (r.Y0 + r.Y1);
            float vx0 = Mathf.Clamp01(r.X0), vx1 = Mathf.Clamp01(r.X1), vy0 = Mathf.Clamp01(r.Y0), vy1 = Mathf.Clamp01(r.Y1);
            r.Size = Mathf.Max(vy1 - vy0, (vx1 - vx0) * aspect);
            r.CentreOk = r.Cx >= CENTRE_MIN && r.Cx <= CENTRE_MAX && r.Cy >= CENTRE_MIN && r.Cy <= CENTRE_MAX;
            r.SizeOk = s.MinScreenFraction <= 0f || r.Size >= s.MinScreenFraction;
            r.MarginOk = !partly && r.X0 >= MARGIN && r.X1 <= 1f - MARGIN && r.Y0 >= MARGIN && r.Y1 <= 1f - MARGIN;
            r.Pass = r.CentreOk && r.SizeOk;
            // in-frame rule for subjects that cannot fit the frame (R1-39, R1-20)
            r.Oversize = partly || r.X1 - r.X0 > 1f || r.Y1 - r.Y0 > 1f;
            r.InCx = 0.5f * (vx0 + vx1);
            r.InCy = 0.5f * (vy0 + vy1);
            r.InFramePass = r.Oversize && vx1 > vx0 && vy1 > vy0 && r.SizeOk
                            && r.InCx >= CENTRE_MIN && r.InCx <= CENTRE_MAX && r.InCy >= CENTRE_MIN && r.InCy <= CENTRE_MAX;
            return r;
        }

        /// <summary>
        /// Lint one shot: collect the subjects of every built beat (plus the QA-owned subjects of KeyShots.CollectQaSubjects)
        /// for the base tag and check each; with a depth buffer (linear eye depth, texture order) also the occlusion test.
        /// Returns the subject count.
        /// </summary>
        public static int Run(string baseTag, Vector3 eye, Quaternion rot, float vfovDeg, float aspect, float near,
                              List<KeyShotSubject> scratch, List<SubjectResult> results, out int hookErrors,
                              float[] depth = null, int depthW = 0, int depthH = 0)
        {
            hookErrors = 0;
            scratch.Clear();
            results.Clear();
            BeatDirector bd = BeatDirector.Instance;
            BeatBase[] beats = bd != null ? bd.Beats : null;
            if (beats != null)
            {
                for (int i = 0; i < beats.Length; i++)
                {
                    if (beats[i] == null) continue;
                    try { beats[i].CollectSubjects(baseTag, scratch); }
                    catch (System.Exception ex)
                    {
                        hookErrors++;
                        Debug.LogWarning("[ABYSS] QA lint: beat " + (i + 1) + " CollectSubjects(" + baseTag + ") threw " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
            }
            KeyShots.CollectQaSubjects(baseTag, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                KeyShotSubject s = scratch[i];
                SubjectResult r = Check(in s, eye, rot, vfovDeg, aspect, near);
                if (!r.KnownConflict && IsLoggedConflict(baseTag, s.Name))
                {
                    r.KnownConflict = true;
                    r.Name = s.Name + KnownConflictSuffix;
                }
                if (depth != null) Visibility(ref r, s.WorldBounds, eye, rot, vfovDeg, aspect, near, depth, depthW, depthH);
                results.Add(r);
            }
            return scratch.Count;
        }

        /// <summary>
        /// Shot status: "no_subjects", "fail" (a subject fails that is not a known conflict), "pass_with_known_conflict" (only
        /// waived subjects fail) or "pass".
        /// </summary>
        public static string ShotStatus(List<SubjectResult> results)
        {
            if (results.Count == 0) return StatusNone;
            bool waived = false;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].LintPass) continue;
                if (!results[i].KnownConflict) return StatusFail;
                waived = true;
            }
            return waived ? StatusWaived : StatusPass;
        }

        /// <summary>
        /// Write the lint block of one shot: {"status", "aspect", "known_conflicts": [names of waived subjects], "subjects": [...]}.
        /// Per subject "status" is pass / fail / pass_with_known_conflict, "pass" is false only for a real failure (a waived
        /// subject has "pass": true, "waived": true) and "geometry_pass" keeps the plain lint verdict.
        /// </summary>
        public static void Write(QaJson j, List<SubjectResult> results, float aspect, int hookErrors, out string status, bool depthTested = false)
        {
            status = ShotStatus(results);
            j.Obj("lint");
            j.Prop("status", status);
            j.Prop("aspect", aspect);
            j.Prop("centre_range", "0.05-0.95");
            j.Prop("margin", MARGIN);
            j.Prop("in_frame_rule", "subjects larger than the frame or cut by the near plane: centre of the in-frame part");
            j.Prop("occlusion_test", depthTested ? "depth buffer, 8 x 8 rays over the in-frame box, at least 30 % unoccluded" : "not run (no depth buffer)");
            if (hookErrors > 0) j.Prop("hook_errors", hookErrors);
            j.Arr("known_conflicts");
            for (int i = 0; i < results.Count; i++) if (results[i].Waived) j.Value(results[i].Name);
            j.EndArray();
            j.Arr("subjects");
            for (int i = 0; i < results.Count; i++)
            {
                SubjectResult r = results[i];
                j.BeginObject();
                j.Prop("name", r.Name);
                j.Prop("status", r.Status);
                j.Prop("behind", r.Behind);
                j.PropRect("rect", r.X0, r.Y0, r.X1, r.Y1);
                j.Key("centre").Inline(r.Cx, r.Cy);
                j.Prop("size", r.Size);
                j.Prop("min_size", r.MinSize);
                j.Prop("centre_ok", r.CentreOk);
                j.Prop("size_ok", r.SizeOk);
                j.Prop("margin_ok", r.MarginOk);
                if (r.Oversize)
                {
                    j.Prop("oversize", true);
                    j.Key("in_frame_centre").Inline(r.InCx, r.InCy);
                    j.Prop("in_frame_pass", r.InFramePass);
                }
                if (r.Visible >= 0f)
                {
                    j.Prop("visible_fraction", r.Visible);
                    j.Prop("visible_samples", r.VisSamples);
                    j.Prop("min_visible", MIN_VISIBLE);
                    if (r.Visible > 0f) j.Key("visible_centre").Inline(r.VisCx, r.VisCy);
                }
                else j.Prop("visible_fraction", "unknown (no depth buffer)");
                j.Prop("visibility_ok", r.VisibilityOk);
                j.Prop("pass", r.LintPass || r.Waived);
                j.Prop("geometry_pass", r.Pass);
                j.Prop("frame_pass", r.FramePass);
                if (r.KnownConflict)
                {
                    j.Prop("known_conflict", true);
                    j.Prop("waived", r.Waived);
                    if (r.LintPass) j.Prop("note", "a known-conflict subject passes the lint: remove its [spec-conflict] tag");
                }
                j.EndObject();
            }
            j.EndArray();
            j.EndObject();
        }
    }
}
