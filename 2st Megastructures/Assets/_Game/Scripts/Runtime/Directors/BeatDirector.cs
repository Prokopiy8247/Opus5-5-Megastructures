using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// World-set activation and the animated ticks (Part 7.3, 14.4 "World sets", 14.6).
    /// - Caches every Renderer under each set root and every IRingDrawable (from the WorldBuilder's BuildContext).
    /// - Activation ranges [A, B): FarValley [0, 810), Capsule [0, 810), NearValley [0, 576), NearPlain [576, 810),
    ///   Ark [231, 576), Outside [778, end); Tunnel and Always are always on. Walking forward a set switches exactly at
    ///   its range ends (the spec's switch points: Ark at 231, the sealed swap at 576, Outside at 778, the plain off at
    ///   810). Hysteresis on the way back: a set switched on at A stays on until s &lt; A - 12 m; a set switched off at
    ///   B comes back at s &lt; B - 8 m; the sealed swap (NearValley / NearPlain, and Ark's end) reverts below s 570
    ///   (14.6). Exception: the Ark spawns at s 231 (9.4 km away, fog 100 %) but its phase keeps its time branch, so
    ///   after the spawn the hull can be in plain view (km away, partly fogged) when the player walks back: once on,
    ///   it stays on until the sealed swap at 576 (never switched off in the frustum; a teleport re-initialises it).
    ///   Only state changes flip renderer.enabled / IRingDrawable.Visible; a drawable whose Visible setter throws is
    ///   logged once and skipped from then on (the set switching and RingState.Set keep running).
    /// - RingState.Set = RingSchedule.SetAt(s) with the same hysteresis (576 / 570, 810 / 802, 894 / 890), or the
    ///   -set N debug override (RingState.SetOverride).
    /// - Every activation change is logged in <see cref="ActivationLog"/> (frame, s, set, on) for the QA difference test.
    /// - Ticks every IRingAnimated at chain order 300 (after the AtmosphereDriver at 250).
    /// Never creates anything after load. Allocation-free per frame.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class BeatDirector : MonoBehaviour
    {
        public const int TickOrder = 200, AnimatedOrder = 300;
        const int SetCount = 8;
        const float BackStart = 12f;                    // 14.6 hysteresis: 12 m back for a set entered going forward
        const float BackEnd = 8f;                       // 14.6 hysteresis: 8 m back for a set left going forward
        const float SwapBack = RingConstants.SwapRevertS; // 14.6 (ADR-009): the sealed swap reverts below s 568
        const float PlainOffS = 810f;                   // 14.6: NearPlain and FarValley disabled at s 810
        const float OutsideOnS = 778f;                  // 14.6: SetOutside enabled at s >= 778

        public struct Activation
        {
            public int Frame; public float S; public RingSet Set; public bool On; public bool Initial;
        }

        /// <summary>Every activation change (and the initial state after load, Initial = true).</summary>
        public static readonly List<Activation> ActivationLog = new List<Activation>(1024);

        public static BeatDirector Instance { get; private set; }

        struct Range { public float A, ABack, B, BBack; public bool Always; }
        static readonly Range[] Ranges = BuildRanges();

        static Range[] BuildRanges()
        {
            var r = new Range[SetCount];
            float inf = float.PositiveInfinity, ninf = float.NegativeInfinity;
            r[(int)RingSet.Always] = new Range { Always = true };
            r[(int)RingSet.Tunnel] = new Range { Always = true };
            r[(int)RingSet.FarValley] = new Range { A = ninf, ABack = ninf, B = PlainOffS, BBack = PlainOffS - BackEnd };
            r[(int)RingSet.Capsule] = new Range { A = ninf, ABack = ninf, B = PlainOffS, BBack = PlainOffS - BackEnd };
            r[(int)RingSet.NearValley] = new Range { A = ninf, ABack = ninf, B = RingConstants.SwapTriggerS, BBack = SwapBack };
            r[(int)RingSet.NearPlain] = new Range { A = RingConstants.SwapTriggerS, ABack = SwapBack, B = PlainOffS, BBack = PlainOffS - BackEnd };
            // Ark: no backward switch-off after the spawn (14.6: nothing deactivates inside the frustum; the hull flies on
            // with the time branch of its phase, so it may be 3-4 km away and only ~50 % fogged when the player backs up)
            r[(int)RingSet.Ark] = new Range { A = RingConstants.ArkTriggerS, ABack = ninf, B = RingConstants.SwapTriggerS, BBack = SwapBack };
            r[(int)RingSet.Outside] = new Range { A = OutsideOnS, ABack = OutsideOnS - BackStart, B = inf, BBack = inf };
            return r;
        }

        static readonly float[] SetFwd = { RingConstants.SwapTriggerS, PlainOffS, RingConstants.BeatS[6] };     // 572, 810, 894
        static readonly float[] SetBack = { SwapBack, PlainOffS - BackEnd, RingConstants.BeatS[6] - 4f };      // 568, 802, 890

        Action<float> _tick, _tickAnim;
        Renderer[][] _renderers = new Renderer[SetCount][];
        bool[][] _initial = new bool[SetCount][];
        IRingDrawable[][] _drawables = new IRingDrawable[SetCount][];
        bool[][] _drawFailed = new bool[SetCount][];
        IRingAnimated[] _animated = new IRingAnimated[0];
        bool[] _animFailed = new bool[0];
        readonly bool[] _on = new bool[SetCount];
        bool _bound, _prewarm;
        int _set = -1;

        public bool Bound => _bound;

        public static void ResetStatics() { ActivationLog.Clear(); }

        /// <summary>True when the set is currently active (true for everything before the world is bound).</summary>
        public static bool IsOn(RingSet set)
        {
            var bd = Instance;
            if (bd == null || !bd._bound) return true;
            int i = (int)set;
            return i < 0 || i >= SetCount || Ranges[i].Always || bd._on[i];
        }

        void Awake()
        {
            Instance = this;
            _tick = Tick;
            _tickAnim = TickAnimated;
            RingRig.RegisterDirector(TickOrder, _tick);
            RingRig.RegisterDirector(AnimatedOrder, _tickAnim);
            for (int i = 0; i < SetCount; i++) { _renderers[i] = new Renderer[0]; _initial[i] = new bool[0]; _drawables[i] = new IRingDrawable[0]; _drawFailed[i] = new bool[0]; }
        }

        void LateUpdate()
        {
            if (!RingRig.Directors.ContainsKey(TickOrder)) RingRig.RegisterDirector(TickOrder, _tick);
            if (!RingRig.Directors.ContainsKey(AnimatedOrder)) RingRig.RegisterDirector(AnimatedOrder, _tickAnim);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Takes the world content from the WorldBuilder (set roots, drawables, animated).</summary>
        public void Bind(BuildContext ctx)
        {
            var drawLists = new List<IRingDrawable>[SetCount];
            for (int i = 0; i < SetCount; i++) drawLists[i] = new List<IRingDrawable>();
            foreach (var d in ctx.Drawables)
            {
                if (d == null) continue;
                int i;
                try { i = (int)d.Set; }
                catch (Exception e)
                {
                    // a drawable left behind by a failed builder: never switched, never ticked
                    Debug.LogError("[Ring] BeatDirector: drawable " + d.GetType().Name + " has no readable Set and is ignored: " + e);
                    continue;
                }
                if (i >= 0 && i < SetCount) drawLists[i].Add(d);
            }
            int rcount = 0;
            for (int i = 0; i < SetCount; i++)
            {
                var set = (RingSet)i;
                Renderer[] rs = new Renderer[0];
                if (ctx.SetRoots.TryGetValue(set, out var root) && root != null) rs = root.GetComponentsInChildren<Renderer>(true);
                _renderers[i] = rs;
                var init = new bool[rs.Length];
                for (int k = 0; k < rs.Length; k++) init[k] = rs[k] != null && rs[k].enabled;
                _initial[i] = init;
                _drawables[i] = drawLists[i].ToArray();
                _drawFailed[i] = new bool[_drawables[i].Length];
                rcount += rs.Length;
            }
            _animated = ctx.Animated.ToArray();
            _animFailed = new bool[_animated.Length];
            _bound = true;
            InitFrom(RingState.S, false);
            Debug.Log("[Ring] BeatDirector bound: " + rcount + " renderers, " + ctx.Drawables.Count + " drawables, " + _animated.Length + " animated");
        }

        /// <summary>Pre-warm (14.6): on = every set visible and activation frozen; off = state re-initialised from
        /// RingState.S (run the director chain once after placing the player so S is current).</summary>
        public void PrewarmAll(bool on) => PrewarmAll(on, RingState.S);

        public void PrewarmAll(bool on, float s)
        {
            if (on)
            {
                _prewarm = true;
                for (int i = 0; i < SetCount; i++) if (!Ranges[i].Always) { _on[i] = true; Apply(i, true); }
            }
            else
            {
                _prewarm = false;
                _lastS = s;
                InitFrom(s, true);
            }
        }

        /// <summary>Re-initialises the activation state from s without history (after load or a teleport).</summary>
        public void InitFrom(float s, bool log)
        {
            _set = RingSchedule.SetAt(s);
            WriteSet();
            if (!_bound) return;
            for (int i = 0; i < SetCount; i++)
            {
                var r = Ranges[i];
                bool on = r.Always || (s >= r.A && s < r.B);
                _on[i] = on;
                Apply(i, on);
                if (log && !r.Always) Log(i, on, s, true);
            }
        }

        void Apply(int i, bool on)
        {
            var rs = _renderers[i]; var init = _initial[i];
            for (int k = 0; k < rs.Length; k++)
            {
                var r = rs[k];
                if (r == null) continue;
                bool want = on && init[k];
                if (r.enabled != want) r.enabled = want;
            }
            var ds = _drawables[i]; var failed = _drawFailed[i];
            for (int k = 0; k < ds.Length; k++)
            {
                var d = ds[k];
                if (d == null || failed[k]) continue;
                try { if (d.Visible != on) d.Visible = on; }
                catch (Exception e)
                {
                    failed[k] = true;
                    Debug.LogError("[Ring] BeatDirector: drawable " + d.GetType().Name + " (" + (RingSet)i + ") failed to switch and is no longer switched: " + e);
                }
            }
        }

        static void Log(int set, bool on, float s, bool initial)
        {
            if (ActivationLog.Count < ActivationLog.Capacity)
                ActivationLog.Add(new Activation { Frame = Time.frameCount, S = s, Set = (RingSet)set, On = on, Initial = initial });
        }

        void WriteSet()
        {
            int ov = RingState.SetOverride;
            RingState.Set = ov >= 0 ? Mathf.Clamp(ov, 0, 3) : Mathf.Max(0, _set);
        }

        void Tick(float dt)
        {
            float s = RingState.S;
            if (_set < 0) _set = RingSchedule.SetAt(s);
            if (Mathf.Abs(s - _lastS) > 20f && _bound && !_prewarm) { InitFrom(s, true); _lastS = s; return; }   // teleport
            _lastS = s;
            while (_set < 3 && s >= SetFwd[_set]) _set++;
            while (_set > 0 && s < SetBack[_set - 1]) _set--;
            WriteSet();
            if (!_bound || _prewarm) return;

            for (int i = 0; i < SetCount; i++)
            {
                var r = Ranges[i];
                if (r.Always) continue;
                bool on = _on[i];
                if (!on && s >= r.A && s < r.BBack) on = true;
                else if (on && (s < r.ABack || s >= r.B)) on = false;
                if (on == _on[i]) continue;
                _on[i] = on;
                Apply(i, on);
                Log(i, on, s, false);
                Debug.Log("[Ring] set " + (RingSet)i + (on ? " on" : " off") + " at s " + s.ToString("0.00") + " (frame " + Time.frameCount + ")");
            }
        }

        float _lastS;

        void TickAnimated(float dt)
        {
            float s = RingState.S; double t = RingState.T; float d = RingState.Dt;
            var a = _animated;
            for (int i = 0; i < a.Length; i++)
            {
                if (_animFailed[i] || a[i] == null) continue;
                try { a[i].Tick(s, t, d); }
                catch (Exception e)
                {
                    _animFailed[i] = true;
                    Debug.LogError("[Ring] animated " + a[i].GetType().Name + " failed and is disabled: " + e);
                }
            }
        }
    }
}
