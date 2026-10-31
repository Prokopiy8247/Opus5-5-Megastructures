using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Hooks the capture harness calls around every capture render (research 03: RenderMesh* draws are per frame and
    /// per camera, and a draw submitted for a specific camera is consumed by its next render, so code-drawn fields
    /// must be re-submitted for the capture camera right before EACH render, warm-ups included).
    /// The lead may wire <see cref="BeforeRender"/>; when it is wired it is the only thing called (the lead then owns
    /// the complete list). While it is unwired, every static method SubmitAllFor(Camera) declared by a type in the
    /// Megastructure assemblies is found once by reflection and called (today: InstancedField, TerrainIndirectField,
    /// SwarmField; all of them skip their own per-frame submission in capture mode).
    /// Camera-dependent C# state (clipmap origins, grass windows, camera-following domes) must be computed per camera
    /// in RenderPipelineManager.beginCameraRendering or in the shader from _RingCam; <see cref="ActiveCamera"/> is the
    /// capture camera during a capture render and null otherwise.
    /// </summary>
    public static class RingCaptureHooks
    {
        /// <summary>Called right before each capture render with the capture camera (multicast).</summary>
        public static Action<Camera> BeforeRender;
        /// <summary>Called right after each capture render (restore per-camera state if needed).</summary>
        public static Action<Camera> AfterRender;

        /// <summary>Orbital stills: skip the code-drawn fields (they are camera-relative and belong to the walk).</summary>
        public static bool SuppressFieldDraws;

        /// <summary>The capture camera while a capture render is being prepared or submitted; null otherwise.</summary>
        public static Camera ActiveCamera { get; private set; }

        static MethodInfo[] _submits;
        static readonly object[] _arg = new object[1];

        /// <summary>What InvokeBefore calls (for report.json).</summary>
        public static string Method { get; private set; } = "none";

        /// <summary>Every static SubmitAllFor(Camera) in the Megastructure assemblies (cached).</summary>
        public static MethodInfo[] Submitters()
        {
            if (_submits != null) return _submits;
            var list = new List<MethodInfo>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (!an.StartsWith("Megastructure") && !an.StartsWith("Assembly-CSharp")) continue;
                if (an.Contains("Tests") || an.Contains("Editor")) continue;
                Type[] ts;
                try { ts = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { ts = e.Types; }
                if (ts == null) continue;
                foreach (var t in ts)
                {
                    if (t == null || t == typeof(RingCaptureHooks) || t.ContainsGenericParameters) continue;
                    MethodInfo m = null;
                    try { m = t.GetMethod("SubmitAllFor", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, null, new[] { typeof(Camera) }, null); }
                    catch (AmbiguousMatchException) { }
                    if (m != null && m.ReturnType == typeof(void)) list.Add(m);
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.DeclaringType.FullName, b.DeclaringType.FullName));
            _submits = list.ToArray();
            var names = new System.Text.StringBuilder();
            foreach (var m in _submits) { if (names.Length > 0) names.Append(','); names.Append(m.DeclaringType.Name); }
            Method = _submits.Length > 0 ? "reflection:SubmitAllFor[" + names + "]" : "none (no SubmitAllFor found)";
            Debug.Log("[Ring] QA capture hooks: " + Method);
            return _submits;
        }

        public static void InvokeBefore(Camera cam)
        {
            ActiveCamera = cam;
            if (SuppressFieldDraws) return;
            if (BeforeRender != null)
            {
                Method = "RingCaptureHooks.BeforeRender";
                try { BeforeRender(cam); }
                catch (Exception e) { Debug.LogWarning("[Ring] QA BeforeRender hook threw: " + e.Message); }
                return;
            }
            var ms = Submitters();
            _arg[0] = cam;
            for (int i = 0; i < ms.Length; i++)
            {
                try { ms[i].Invoke(null, _arg); }
                catch (Exception e) { Debug.LogWarning("[Ring] QA " + ms[i].DeclaringType.Name + ".SubmitAllFor threw: " + (e.InnerException ?? e).Message); }
            }
            _arg[0] = null;
        }

        public static void InvokeAfter(Camera cam)
        {
            try { AfterRender?.Invoke(cam); }
            catch (Exception e) { Debug.LogWarning("[Ring] QA AfterRender hook threw: " + e.Message); }
            ActiveCamera = null;
        }
    }

    /// <summary>Gaze sample: yaw (deg, 0 = +Z, +90 = +X) and pitch (deg, up positive) for progress s.</summary>
    public delegate bool QaGazeFn(float s, out float yaw, out float pitch);

    /// <summary>
    /// What the QA tests need from modules owned by other builders. The lead may wire these delegates; until then each
    /// resolver falls back to reflection with the names that exist in the runtime today:
    /// AutoWalker.Request(WalkMode mode, float speed) with WalkMode.AutoTest / WalkMode.Cinematic (older candidates
    /// BeginQa / StartQa / BeginAutoTest / StartAutoTest are still tried), PathGuide.LaneHalfWidth(float s) and
    /// PathGuide.LaneSamplePoints(float step) (static), GazeSchedule.Evaluate(float s, float heading, out float yaw,
    /// out float pitch) (or the 3-argument form), CameraRig.VfovForAspect(float vfov169, float aspect) when it exists
    /// (otherwise Hor+), the static int CameraRig.Violations, PlayerController.SafetySnaps, AtmosphereDriver.FovOverride,
    /// and a Restart() method on GameBootstrap / UiDirector (otherwise a scene reload, which is what the pause-menu
    /// restart does).
    /// </summary>
    public static class RingQaHooks
    {
        public static Func<float, int, float, bool> StartAutoWalk;     // speed, seed, fromS -> started
        public static Func<float, float> LaneHalfWidth;                // s -> walkable half width (m)
        public static Func<int> CameraSafetyViolations;                // frames the camera was found inside a collider
        public static Action Restart;                                   // restart the walk from s 0
        public static Action StartCinematic;                            // start the cinematic autowalk
        public static QaGazeFn Gaze;                                    // gaze schedule of the cinematic walk
        public static Func<float, float, float> VfovForAspect;         // (vFOV at 16:9, aspect) -> vertical FOV

        static readonly string[] WalkNames = { "BeginQa", "StartQa", "BeginAutoTest", "StartAutoTest", "StartQaWalk", "BeginAutoWalk", "StartAutoWalk" };

        /// <summary>Starts the AutoWalker in QA mode (constant speed, fillets, no holds, no sway, seeded).</summary>
        public static bool TryStartAutoWalk(float speed, int seed, float fromS, out string how)
        {
            if (StartAutoWalk != null)
            {
                how = "RingQaHooks.StartAutoWalk";
                try { return StartAutoWalk(speed, seed, fromS); }
                catch (Exception e) { how += " threw " + e.Message; return false; }
            }
            var comp = QaUtil.FindComponent("AutoWalker");
            var t = comp != null ? comp.GetType() : QaUtil.FindType("AutoWalker");
            if (t == null) { how = "AutoWalker type not found"; return false; }
            if (TryRequest(t, "AutoTest", speed)) { how = "reflection:AutoWalker.Request(WalkMode.AutoTest, " + speed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ") (seed from -seed)"; return true; }
            foreach (var n in WalkNames)
            {
                if (TryCall(t, comp, n, new[] { typeof(float), typeof(int), typeof(float) }, new object[] { speed, seed, fromS })) { how = "reflection:" + n + "(speed, seed, fromS)"; return true; }
                if (TryCall(t, comp, n, new[] { typeof(float), typeof(int) }, new object[] { speed, seed })) { how = "reflection:" + n + "(speed, seed)"; return true; }
                if (TryCall(t, comp, n, new[] { typeof(float) }, new object[] { speed })) { how = "reflection:" + n + "(speed)"; return true; }
                if (TryCall(t, comp, n, Type.EmptyTypes, new object[0])) { how = "reflection:" + n + "()"; return true; }
            }
            if (comp != null)
            {
                bool any = QaUtil.TrySet(comp, "QaMode", true) | QaUtil.TrySet(comp, "AutoTest", true);
                QaUtil.TrySet(comp, "Speed", speed); QaUtil.TrySet(comp, "Seed", seed);
                if (comp is Behaviour b) b.enabled = true;
                if (any) { how = "reflection:fields QaMode/AutoTest/Speed/Seed"; return true; }
            }
            how = "no AutoWalker entry point found (relying on its own -autotest handling)";
            return false;
        }

        /// <summary>Calls a static Request(EnumMode mode[, float speed]) with the named enum value.</summary>
        static bool TryRequest(Type t, string modeName, float speed)
        {
            foreach (var m in t.GetMethods(QaUtil.AnyStatic))
            {
                if (m.Name != "Request") continue;
                var ps = m.GetParameters();
                if (ps.Length < 1 || ps.Length > 2 || !ps[0].ParameterType.IsEnum) continue;
                if (ps.Length == 2 && ps[1].ParameterType != typeof(float)) continue;
                object mode;
                try { mode = Enum.Parse(ps[0].ParameterType, modeName, true); }
                catch (Exception) { continue; }
                try
                {
                    m.Invoke(null, ps.Length == 2 ? new object[] { mode, speed } : new object[] { mode });
                    return true;
                }
                catch (Exception e) { Debug.LogWarning("[Ring] QA " + t.Name + ".Request threw: " + (e.InnerException ?? e).Message); }
            }
            return false;
        }

        /// <summary>True when the AutoWalker reports that it drives the player (static Active), null if unknown.</summary>
        public static bool? AutoWalkerActive()
        {
            var t = QaUtil.FindType("AutoWalker");
            if (t == null) return null;
            var p = t.GetProperty("Active", QaUtil.AnyStatic);
            if (p != null && p.PropertyType == typeof(bool)) { try { return (bool)p.GetValue(null); } catch (Exception) { } }
            var f = t.GetField("Active", QaUtil.AnyStatic);
            if (f != null && f.FieldType == typeof(bool)) { try { return (bool)f.GetValue(null); } catch (Exception) { } }
            return null;
        }

        static bool TryCall(Type t, Component comp, string name, Type[] sig, object[] args)
        {
            var ms = QaUtil.FindMethod(t, name, QaUtil.AnyStatic, sig);
            object target = null;
            if (ms == null && comp != null) { ms = QaUtil.FindMethod(t, name, QaUtil.AnyInstance, sig); target = comp; }
            if (ms == null) return false;
            try
            {
                var r = ms.Invoke(target, args);
                return !(r is bool ok) || ok;
            }
            catch (Exception e) { Debug.LogWarning("[Ring] QA " + t.Name + "." + name + " threw: " + (e.InnerException ?? e).Message); return false; }
        }

        static Func<float, float> _laneResolved;
        static bool _laneSearched;

        /// <summary>Lane half width at s from PathGuide (wired or reflected); false if unavailable.</summary>
        public static bool TryLaneHalfWidth(float s, out float hw)
        {
            var f = LaneHalfWidth ?? ResolveLane();
            if (f == null) { hw = 0f; return false; }
            hw = f(s);
            return !float.IsNaN(hw) && hw > 0f;
        }

        public static string LaneSource => LaneHalfWidth != null ? "RingQaHooks.LaneHalfWidth" : (ResolveLane() != null ? "reflection:PathGuide.LaneHalfWidth" : "fallback table (QaPath)");

        static Func<float, float> ResolveLane()
        {
            if (_laneSearched) return _laneResolved;
            _laneSearched = true;
            var t = QaUtil.FindType("PathGuide");
            if (t == null) return null;
            var m = QaUtil.FindMethod(t, "LaneHalfWidth", QaUtil.AnyStatic, typeof(float));
            try
            {
                if (m != null && m.ReturnType == typeof(float)) _laneResolved = (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>), m);
                else
                {
                    m = QaUtil.FindMethod(t, "LaneHalfWidth", QaUtil.AnyInstance, typeof(float));
                    var comp = QaUtil.FindComponent("PathGuide");
                    if (m != null && m.ReturnType == typeof(float) && comp != null)
                        _laneResolved = (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>), comp, m);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Ring] QA PathGuide.LaneHalfWidth bind failed: " + e.Message); }
            return _laneResolved;
        }

        /// <summary>Frames with the camera inside a collider as counted by the CameraRig (-1 if no counter exists).</summary>
        public static int SafetyViolations(out string source)
        {
            if (CameraSafetyViolations != null) { source = "RingQaHooks.CameraSafetyViolations"; return CameraSafetyViolations(); }
            foreach (var tn in new[] { "CameraRig", "PathGuide", "PlayerController" })
            {
                var t = QaUtil.FindType(tn);
                if (QaUtil.TryReadStaticNumber(t, new[] { "Violation", "InsideCollider", "PushOut" }, out double v, out string member))
                { source = "reflection:" + member; return (int)v; }
            }
            source = "none";
            return -1;
        }

        static FieldInfo _fovField;
        static bool _fovFieldSearched;

        /// <summary>Sets the static AtmosphereDriver.FovOverride (NaN = schedule) so the director writes the capture vFOV
        /// to the gameplay camera too; returns false when the field does not exist.</summary>
        public static bool SetFovOverride(float vfov)
        {
            if (!_fovFieldSearched)
            {
                _fovFieldSearched = true;
                var t = QaUtil.FindType("AtmosphereDriver");
                var f = t != null ? t.GetField("FovOverride", QaUtil.AnyStatic) : null;
                if (f != null && f.FieldType == typeof(float) && !f.IsLiteral && !f.IsInitOnly) _fovField = f;
            }
            if (_fovField == null) return false;
            try { _fovField.SetValue(null, vfov); return true; }
            catch (Exception) { return false; }
        }

        /// <summary>The lane sample grid of PathGuide.LaneSamplePoints(step) (points on the walk surface), or null.</summary>
        public static IEnumerable<Vector3> LaneSamplePoints(float step, out string source)
        {
            source = "none";
            var t = QaUtil.FindType("PathGuide");
            var m = QaUtil.FindMethod(t, "LaneSamplePoints", QaUtil.AnyStatic, typeof(float));
            if (m == null || !typeof(IEnumerable<Vector3>).IsAssignableFrom(m.ReturnType)) return null;
            try
            {
                var r = m.Invoke(null, new object[] { step }) as IEnumerable<Vector3>;
                if (r != null) source = "reflection:PathGuide.LaneSamplePoints";
                return r;
            }
            catch (Exception e) { Debug.LogWarning("[Ring] QA PathGuide.LaneSamplePoints threw: " + (e.InnerException ?? e).Message); return null; }
        }

        /// <summary>Safety-volume snaps counted by the PlayerController (14.4 Colliders (5)); -1 if no counter exists.</summary>
        public static int SafetySnaps(out string source)
        {
            var t = QaUtil.FindType("PlayerController");
            if (QaUtil.TryReadStaticNumber(t, new[] { "SafetySnap", "Snaps" }, out double v, out string member)) { source = "reflection:" + member; return (int)v; }
            source = "none";
            return -1;
        }

        public static bool TryRestart(out string how)
        {
            if (Restart != null) { how = "RingQaHooks.Restart"; Restart(); return true; }
            foreach (var tn in new[] { "GameBootstrap", "UiDirector", "PauseOverlay", "PauseMenu", "FadeController" })
            {
                var comp = QaUtil.FindComponent(tn);
                var t = comp != null ? comp.GetType() : QaUtil.FindType(tn);
                if (t != null && TryCall(t, comp, "Restart", Type.EmptyTypes, new object[0])) { how = "reflection:" + tn + ".Restart()"; return true; }
            }
            how = "none";
            return false;
        }

        public static bool TryStartCinematic(out string how)
        {
            if (StartCinematic != null) { how = "RingQaHooks.StartCinematic"; StartCinematic(); return true; }
            // the AutoWalker follows RingState.Cinematic (the C key / -cinematic path); raise the event for the UI and audio
            if (!RingState.Cinematic)
            {
                RingState.Cinematic = true;
                try { RingEvents.RaiseCinematic(true); }
                catch (Exception e) { Debug.LogWarning("[Ring] QA CinematicChanged listener threw: " + e.Message); }
            }
            var comp = QaUtil.FindComponent("AutoWalker");
            var t = comp != null ? comp.GetType() : QaUtil.FindType("AutoWalker");
            if (t != null)
            {
                if (TryRequest(t, "Cinematic", RingConstants.Walk)) { how = "reflection:AutoWalker.Request(WalkMode.Cinematic) + RingState.Cinematic"; return true; }
                foreach (var n in new[] { "StartCinematic", "BeginCinematic" })
                    if (TryCall(t, comp, n, Type.EmptyTypes, new object[0])) { how = "reflection:AutoWalker." + n + "()"; return true; }
                if (TryCall(t, comp, "SetCinematic", new[] { typeof(bool) }, new object[] { true })) { how = "reflection:AutoWalker.SetCinematic(true)"; return true; }
            }
            how = "RingState.Cinematic = true";
            return t != null;
        }

        static QaGazeFn _gazeResolved;
        static bool _gazeSearched;

        /// <summary>Gaze of the cinematic schedule at s; false when no schedule is available.</summary>
        public static bool TryGaze(float s, out float yaw, out float pitch)
        {
            var g = Gaze ?? ResolveGaze();
            if (g == null) { yaw = 0f; pitch = 0f; return false; }
            try { return g(s, out yaw, out pitch); }
            catch (Exception) { yaw = 0f; pitch = 0f; return false; }
        }

        static QaGazeFn ResolveGaze()
        {
            if (_gazeSearched) return _gazeResolved;
            _gazeSearched = true;
            var t = QaUtil.FindType("GazeSchedule");
            if (t == null) return null;
            var outF = typeof(float).MakeByRefType();
            foreach (var n in new[] { "Evaluate", "Sample", "At" })
            {
                // Evaluate(float s, float heading, out float yaw, out float pitch): heading of the rounded QA path at s
                var m4 = QaUtil.FindMethod(t, n, QaUtil.AnyStatic, typeof(float), typeof(float), outF, outF);
                if (m4 != null && m4.ReturnType == typeof(void))
                {
                    var args4 = new object[4];
                    _gazeResolved = (float s, out float y, out float p) =>
                    {
                        QaPath.EyeAt(s, out float heading);
                        args4[0] = s; args4[1] = heading;
                        m4.Invoke(null, args4);
                        y = (float)args4[2]; p = (float)args4[3];
                        return true;
                    };
                    return _gazeResolved;
                }
                var m = QaUtil.FindMethod(t, n, QaUtil.AnyStatic, typeof(float), outF, outF);
                if (m != null)
                {
                    try
                    {
                        if (m.ReturnType == typeof(bool)) { _gazeResolved = (QaGazeFn)Delegate.CreateDelegate(typeof(QaGazeFn), m); return _gazeResolved; }
                        if (m.ReturnType == typeof(void))
                        {
                            var args = new object[3];
                            _gazeResolved = (float s, out float y, out float p) =>
                            {
                                args[0] = s; m.Invoke(null, args); y = (float)args[1]; p = (float)args[2]; return true;
                            };
                            return _gazeResolved;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("[Ring] QA GazeSchedule bind failed: " + e.Message); }
                }
                var mv = QaUtil.FindMethod(t, n, QaUtil.AnyStatic, typeof(float));
                if (mv != null && mv.ReturnType == typeof(Vector2))
                {
                    var a1 = new object[1];
                    _gazeResolved = (float s, out float y, out float p) =>
                    {
                        a1[0] = s; var v = (Vector2)mv.Invoke(null, a1); y = v.x; p = v.y; return true;
                    };
                    return _gazeResolved;
                }
            }
            return null;
        }

        static Func<float, float, float> _vfovResolved;
        static bool _vfovSearched;

        /// <summary>Vertical FOV for an aspect from the 16:9 reference vFOV. Uses the CameraRig policy when it exposes
        /// one, otherwise pure Hor+ (Part 8.10: the vertical FOV is constant, the horizontal FOV follows the aspect).</summary>
        public static float VfovFor(float vfov169, float aspect)
        {
            var f = VfovForAspect ?? ResolveVfov();
            if (f != null)
            {
                try { float v = f(vfov169, aspect); if (v > 1f && v < 179f) return v; }
                catch (Exception) { }
            }
            return vfov169;
        }

        public static string VfovPolicy => VfovForAspect != null ? "RingQaHooks.VfovForAspect" : (ResolveVfov() != null ? "reflection:CameraRig.VfovForAspect" : "Hor+ (vertical constant)");

        static Func<float, float, float> ResolveVfov()
        {
            if (_vfovSearched) return _vfovResolved;
            _vfovSearched = true;
            var t = QaUtil.FindType("CameraRig");
            foreach (var n in new[] { "VfovForAspect", "HorPlusVfov", "VerticalFovFor" })
            {
                var m = QaUtil.FindMethod(t, n, QaUtil.AnyStatic, typeof(float), typeof(float));
                if (m != null && m.ReturnType == typeof(float))
                {
                    try { _vfovResolved = (Func<float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float>), m); break; }
                    catch (Exception) { }
                }
            }
            return _vfovResolved;
        }
    }
}
