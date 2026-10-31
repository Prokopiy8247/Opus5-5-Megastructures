using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Result of one subject at one aspect ratio.</summary>
    public struct SubjectResult
    {
        public string Name, Source;
        public bool MustBeInside, Resolved, Inside, Visible;
        public Vector2 Min, Max;   // viewport extents of the samples in front of the camera (0..1, y up)
    }

    /// <summary>
    /// Key-frame composition check (Part 10 #35, Part 11.2): projects the subjects of a key shot through the shot
    /// camera at 16:9, 16:10 and 21:9 and records whether each lies inside the frame with a 3 % margin. Geometry is
    /// bent relative to the camera exactly like the vertex shaders (RingBend, 14.7 H1); sky subjects (star, swarm
    /// bands, terrain-edge direction) are directions. Projection is analytic (Hor+ unless the CameraRig exposes a
    /// policy), so it is reproducible in EditMode tests.
    /// </summary>
    public static class CompositionCheck
    {
        public const float Margin = 0.03f;
        public static readonly string[] AspectNames = { "16:9", "16:10", "21:9" };
        public static readonly float[] Aspects = { 1920f / 1080f, 1680f / 1050f, 2560f / 1080f };

        struct Subject
        {
            public string Name, Source;
            public bool Box, Bend, Resolved, Disc;
            public Bounds Bounds;
            public Vector3 Dir; public float RadiusDeg;
            public Vector3 Centre, Normal; public float Radius;   // world disc (an opening facing the camera)
        }

        // ---------------- projection ----------------
        /// <summary>Viewport position (x right, y up, 0..1) and depth of a world point for a camera at eye with
        /// yaw/pitch (no roll), vertical FOV vfov and the given aspect. z &lt;= 0 means behind the camera.</summary>
        public static Vector3 Project(Vector3 eye, float yaw, float pitch, float vfov, float aspect, Vector3 p)
        {
            var inv = Quaternion.Inverse(Quaternion.Euler(-pitch, yaw, 0f));
            Vector3 c = inv * (p - eye);
            if (c.z <= 1e-6f) return new Vector3(0f, 0f, c.z);
            float ty = Mathf.Tan(0.5f * vfov * Mathf.Deg2Rad);
            float tx = ty * aspect;
            return new Vector3(0.5f + 0.5f * c.x / (c.z * tx), 0.5f + 0.5f * c.y / (c.z * ty), c.z);
        }

        // ---------------- subjects ----------------
        /// <summary>Resolves a subject. name may list alternatives "A|B|C": the first registry key found wins (each
        /// alternative is tried as is, as "Subject." + it, and as "Subject." + it without dots), then the first analytic
        /// definition among the alternatives.</summary>
        static Subject Resolve(string name, BuildContext ctx)
        {
            var alts = name.Split('|');
            if (ctx != null)
                foreach (var a in alts)
                    foreach (var key in new[] { a, "Subject." + a, "Subject." + a.Replace(".", "") })
                    {
                        if (!ctx.Registry.TryGetValue(key, out Object o) || o == null) continue;
                        if (!RegistryMeasure.TryWorldBounds(o, out var b, out string how)) continue;
                        return new Subject { Name = alts[0], Box = true, Bend = true, Bounds = b, Resolved = true, Source = "registry " + key + " (" + how + ")" };
                    }
            foreach (var a in alts)
            {
                var s = Analytic(a);
                if (s.Resolved) { s.Name = alts[0]; return s; }
            }
            return new Subject { Name = alts[0], Resolved = false, Source = "unresolved" };
        }

        static Bounds MinMax(Vector3 mn, Vector3 mx) { var b = new Bounds(); b.SetMinMax(mn, mx); return b; }

        /// <summary>Analytic fallbacks from the 14.5 / 14.12 numbers.</summary>
        static Subject Analytic(string name)
        {
            var s = new Subject { Name = name, Resolved = true, Source = "analytic", Box = true, Bend = true };
            switch (name)
            {
                case "Capsule.Porthole":
                    s.Bounds = new Bounds(RingConstants.PortholeCentre, Vector3.one * RingConstants.PortholeFrame); break;
                case "Capsule.Hatch":
                    // the 2.0 x 1.9 m opening in the 0.5 m end wall at z 9.5..10 (14.5.1)
                    s.Bounds = MinMax(new Vector3(-0.5f * RingConstants.HatchW, RingConstants.CapsuleFloorY, RingConstants.HatchZ0),
                                      new Vector3(0.5f * RingConstants.HatchW, RingConstants.CapsuleFloorY + RingConstants.HatchH, RingConstants.HatchZ1)); break;
                case "Wall":
                    // the wall face near the door, as the Subject.Wall marker: x 114, y 0..120, z 510..810
                    s.Bounds = MinMax(new Vector3(RingConstants.WallX - 0.005f, 0f, 510f), new Vector3(RingConstants.WallX + 0.005f, 120f, 810f)); break;
                case "Hull.Horizon":
                    // the hull horizon seen from the deck: 11.4 deg below the eye line along +X (14.5.7)
                    s.Box = false; s.Bend = false; s.Dir = RingConstants.DirFromAzEl(90f, -RingConstants.HullDip); s.RadiusDeg = 0f; break;
                case "Porthole.Opening":
                    // the 0.55 m opening as a disc on the porthole axis (yaw -45, pitch +25, 14.5.1)
                    s.Box = false; s.Disc = true; s.Centre = RingConstants.PortholeCentre;
                    s.Normal = RingConstants.DirFromAzEl(RingConstants.PortholeYaw, RingConstants.PortholePitch);
                    s.Radius = 0.5f * RingConstants.PortholeOpening; break;
                case "Footbridge.Opening":
                    s.Bounds = MinMax(new Vector3(-0.5f * RingConstants.FootbridgeW, 0f, RingConstants.FootbridgeS0),
                                      new Vector3(0.5f * RingConstants.FootbridgeW, RingConstants.FootbridgeH, RingConstants.FootbridgeS1)); break;
                case "Pavilion.P2":
                    // lathe dome on 8 columns, 11 m tall; the plan radius is not specified, 6 m assumed
                    s.Bounds = MinMax(RingConstants.PavilionP2 + new Vector3(-6f, 0f, -6f), RingConstants.PavilionP2 + new Vector3(6f, RingConstants.PavilionH, 6f)); break;
                case "Underpass.Mouth":
                    s.Bounds = MinMax(new Vector3(-0.5f * RingConstants.UnderpassW, 0f, RingConstants.LeveeZ0 - 0.1f),
                                      new Vector3(0.5f * RingConstants.UnderpassW, RingConstants.UnderpassH, RingConstants.LeveeZ0 + 0.1f)); break;
                case "Door.Opening":
                    s.Bounds = MinMax(new Vector3(RingConstants.WallX - 0.1f, 0f, 660f - 0.5f * RingConstants.DoorW),
                                      new Vector3(RingConstants.WallX + 0.1f, RingConstants.DoorH, 660f + 0.5f * RingConstants.DoorW)); break;
                case "Tunnel.Mouth":
                    s.Bounds = MinMax(new Vector3(RingConstants.WallOuterX - 0.1f, 0f, 660f - 0.5f * RingConstants.DoorW),
                                      new Vector3(RingConstants.WallOuterX + 0.1f, RingConstants.DoorH, 660f + 0.5f * RingConstants.DoorW)); break;
                case "Beacon":
                    s.Bounds = MinMax(new Vector3(RingConstants.BeaconXZ.x - 1f, 0f, RingConstants.BeaconXZ.y - 1f),
                                      new Vector3(RingConstants.BeaconXZ.x + 1f, RingConstants.BeaconH + RingConstants.BeaconLens, RingConstants.BeaconXZ.y + 1f)); break;
                case "Ark.Hull":
                {
                    // x_c(u) = -9 000 + 15 000 u (14.5.3); the hull centre is at the altitude, track z
                    float xc = RingConstants.ArkX0 + (RingConstants.ArkX1 - RingConstants.ArkX0) * Mathf.Clamp01(RingState.ArkU);
                    s.Bounds = new Bounds(new Vector3(xc, RingConstants.ArkAltitude, RingConstants.ArkTrackZ),
                                          new Vector3(RingConstants.ArkLength, RingConstants.ArkHeight, RingConstants.ArkBeam));
                    s.Source = "analytic (ArkU " + QaUtil.F(RingState.ArkU) + ")";
                    break;
                }
                case "Star":
                    s.Box = false; s.Bend = false; s.Dir = RingConstants.StarL; s.RadiusDeg = RingConstants.StarRadiusDeg; break;
                case "StarOut":
                    s.Box = false; s.Bend = false; s.Dir = RingConstants.StarL7; s.RadiusDeg = RingConstants.StarRadiusDeg; break;
                case "Swarm.Bands":
                    // every band ellipse lies inside the circle of the largest angular radius (16.7 deg, 14.5.7)
                    s.Box = false; s.Bend = false; s.Dir = RingConstants.StarL7; s.RadiusDeg = 16.7f; break;
                case "Terrain.EdgeAhead":
                    s.Box = false; s.Bend = false; s.Dir = RingConstants.DirFromAzEl(0f, RingConstants.ClipEdgeElevation); s.RadiusDeg = 0f; break;
                default:
                    s.Resolved = false; s.Source = "unresolved"; break;
            }
            return s;
        }

        // ---------------- evaluation ----------------
        static readonly List<Vector3> _pts = new List<Vector3>(256);

        static void Samples(Subject s, Vector3 eye, List<Vector3> pts)
        {
            pts.Clear();
            if (s.Disc)
            {
                Vector3 n = s.Normal.normalized;
                Vector3 a = Vector3.Cross(Vector3.up, n);
                if (a.sqrMagnitude < 1e-8f) a = Vector3.right;
                a.Normalize();
                Vector3 b = Vector3.Cross(n, a);
                pts.Add(s.Bend ? RingBend.Bend(s.Centre, eye) : s.Centre);
                for (int i = 0; i < 32; i++)
                {
                    float ang = i * (2f * Mathf.PI / 32f);
                    var p = s.Centre + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * s.Radius;
                    pts.Add(s.Bend ? RingBend.Bend(p, eye) : p);
                }
            }
            else if (s.Box)
            {
                Vector3 mn = s.Bounds.min, mx = s.Bounds.max;
                const int n = 4; // 5 x 5 x 5 samples so a bent box keeps its curvature
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n; j++)
                        for (int k = 0; k <= n; k++)
                        {
                            var p = new Vector3(Mathf.Lerp(mn.x, mx.x, i / (float)n), Mathf.Lerp(mn.y, mx.y, j / (float)n), Mathf.Lerp(mn.z, mx.z, k / (float)n));
                            pts.Add(s.Bend ? RingBend.Bend(p, eye) : p);
                        }
            }
            else
            {
                const float dist = 1000f;
                Vector3 d = s.Dir.normalized;
                pts.Add(eye + d * dist);
                if (s.RadiusDeg > 0f)
                {
                    Vector3 a = Vector3.Cross(Vector3.up, d);
                    if (a.sqrMagnitude < 1e-8f) a = Vector3.right;
                    a.Normalize();
                    Vector3 b = Vector3.Cross(d, a);
                    float r = Mathf.Tan(s.RadiusDeg * Mathf.Deg2Rad);
                    for (int i = 0; i < 24; i++)
                    {
                        float ang = i * (2f * Mathf.PI / 24f);
                        pts.Add(eye + (d + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * r).normalized * dist);
                    }
                }
            }
        }

        /// <summary>Evaluates the subjects of a shot at one aspect. Names with a leading '~' only need to be visible.</summary>
        public static List<SubjectResult> Evaluate(Vector3 eye, float yaw, float pitch, float vfov169, float aspect,
                                                   IList<string> names, BuildContext ctx)
        {
            var res = new List<SubjectResult>();
            float vfov = RingQaHooks.VfovFor(vfov169, aspect);
            foreach (var raw in names)
            {
                bool inside = !raw.StartsWith("~");
                string name = inside ? raw : raw.Substring(1);
                var s = Resolve(name, ctx);
                var r = new SubjectResult { Name = s.Name ?? name, Source = s.Source, MustBeInside = inside, Resolved = s.Resolved };
                if (s.Resolved)
                {
                    Samples(s, eye, _pts);
                    bool allIn = true, anyVis = false;
                    Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = -mn;
                    foreach (var p in _pts)
                    {
                        var v = Project(eye, yaw, pitch, vfov, aspect, p);
                        if (v.z <= 0f) { allIn = false; continue; }
                        mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v);
                        bool inMargin = v.x >= Margin && v.x <= 1f - Margin && v.y >= Margin && v.y <= 1f - Margin;
                        if (!inMargin) allIn = false;
                        if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) anyVis = true;
                    }
                    r.Inside = allIn && _pts.Count > 0;
                    r.Visible = anyVis;
                    r.Min = mn; r.Max = mx;
                }
                res.Add(r);
            }
            return res;
        }

        /// <summary>The subject list of a key shot: the 14.5 list plus registry keys "Subject.KSnn.*".</summary>
        public static List<string> SubjectsFor(KeyShot k, BuildContext ctx)
        {
            var list = new List<string>(KeyShots.SubjectsFor(k));
            if (ctx != null)
            {
                string prefix = "Subject." + k.Id + ".";
                foreach (var kv in ctx.Registry)
                    if (kv.Key.StartsWith(prefix)) list.Add(kv.Key);
            }
            return list;
        }

        /// <summary>True if every subject passes (inside, or visible for '~' subjects) and every one resolved.</summary>
        public static bool AllOk(List<SubjectResult> r)
        {
            foreach (var x in r)
                if (!x.Resolved || (x.MustBeInside ? !x.Inside : !x.Visible)) return false;
            return true;
        }

        /// <summary>Registry keys "Subject.*" that are not tied to one shot ("Subject.KSnn.*" keys are): they are recorded
        /// for every key shot in which they are at least partly visible at 16:9, informational (not part of "ok").</summary>
        public static List<string> RegistrySubjects(BuildContext ctx)
        {
            var list = new List<string>();
            if (ctx == null) return list;
            foreach (var kv in ctx.Registry)
                if (kv.Key.StartsWith("Subject.") && !(kv.Key.Length > 12 && kv.Key.StartsWith("Subject.KS") && char.IsDigit(kv.Key[10])))
                    list.Add(kv.Key);
            list.Sort(string.CompareOrdinal);
            return list;
        }

        static void WriteSubjects(QaJson j, List<SubjectResult> r)
        {
            foreach (var x in r)
            {
                j.Obj().KV("name", x.Name).KV("source", x.Source).KV("must_be_inside", x.MustBeInside).KV("resolved", x.Resolved)
                 .KV("inside", x.Inside).KV("visible", x.Visible);
                if (x.Resolved && x.Max.x >= x.Min.x)
                    j.Key("viewport").Arr().V(x.Min.x).V(x.Min.y).V(x.Max.x).V(x.Max.y).EndArr();
                j.EndObj();
            }
        }

        /// <summary>JSON of a shot's composition at the three aspects (16:9, 16:10, 21:9; 3 % margin).</summary>
        public static void WriteJson(QaJson j, KeyShot k, BuildContext ctx, out bool allOk)
        {
            allOk = true;
            var names = SubjectsFor(k, ctx);
            var extra = RegistrySubjects(ctx);
            // keep only the registry subjects that are partly in frame at 16:9 for this shot
            var shown = new List<string>();
            if (extra.Count > 0)
            {
                var r169 = Evaluate(k.Eye, k.Yaw, k.Pitch, k.Vfov, Aspects[0], extra, ctx);
                for (int i = 0; i < r169.Count; i++) if (r169[i].Visible) shown.Add("~" + extra[i]);
            }
            j.Obj().KV("shot", k.Id).KV("margin", Margin).KV("vfov_policy", RingQaHooks.VfovPolicy).Key("aspects").Obj();
            for (int a = 0; a < Aspects.Length; a++)
            {
                var r = Evaluate(k.Eye, k.Yaw, k.Pitch, k.Vfov, Aspects[a], names, ctx);
                bool ok = AllOk(r);
                allOk &= ok;
                j.Key(AspectNames[a]).Obj().KV("ok", ok).KV("vfov", RingQaHooks.VfovFor(k.Vfov, Aspects[a])).Key("subjects").Arr();
                WriteSubjects(j, r);
                j.EndArr();
                if (shown.Count > 0)
                {
                    j.Key("registry_subjects").Arr();
                    WriteSubjects(j, Evaluate(k.Eye, k.Yaw, k.Pitch, k.Vfov, Aspects[a], shown, ctx));
                    j.EndArr();
                }
                j.EndObj();
            }
            j.EndObj().KV("ok", allOk).EndObj();
        }
    }
}
