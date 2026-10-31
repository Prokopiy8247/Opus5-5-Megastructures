using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Progress and phases (Part 14.4, 14.5.3, 14.5.4, 14.7 H8). Single writer of RingState's time and progress fields.
    /// - s: projection of RingRig.Player onto the progress path (the sharp polyline with wide fillets, so the projection
    ///   is continuous for every reachable point; exact on all straight legs), searched within +-20 m of the previous
    ///   position (global search after a teleport), or RingRig.ForcedS when set.
    /// - t: RingState.T += dt unless paused or loading (double); RingRig.ForcedT overrides.
    /// - Ark: u = smax((t - t231) / 190, (s - 231) / 285) -> critically damped follower omega 0.8 -> RingState.ArkU (0..1).
    /// - Sunset: u = smax((t - t411) / 70, (s - 411) / 105) -> follower -> RingState.SunsetU (unclamped below 0, &lt;= 1);
    ///   RingState.Night / StarV from SunsetModel.
    /// - Felt cue (14.5.3), cloud shadows (14.5.2), ship shadow (14.5.3) -> RingShadowParams.
    /// - Events once each and in order: BeatEntered 0..6, Hatch (s 6.5), Goal (s 959).
    /// Capture mode: every time branch equals its progress branch (u = progress branch) and every follower equals its
    /// target, so stills are reproducible.
    /// This component also drives the whole director chain (RingRig.Directors, in order) from its LateUpdate
    /// (execution order 1000, after the player and the camera rig), so the globals of a frame see its final pose.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class ProgressDirector : MonoBehaviour
    {
        public const int TickOrder = 100;
        const float ProjectWindow = 20f;     // task: +-20 m window around the previous s
        const float TeleportDist = 12f;      // farther than this from the path -> global search
        const float JumpS = 20f;             // progress jump treated as a teleport (followers snap)
        const float FollowOmega = 0.8f;      // 14.7 H8
        const float CueS = 375f, CueHalfS = 2.25f, CueDur = 3f;   // 14.5.3 felt cue, centred at s 375, 3 s
        const float CloudFadeS0 = 399f, CloudFadeS1 = 423f;       // cloud shadows end with beat 3 (Rule P window round 411)
        const float CloudRampS = 15f;                             // ramped in over the first 15 m after s 90
        // sunset lead window (u above its progress branch): a forward walk at 1.5 m/s keeps u - b near smax's 0.015 minus
        // the follower lag, so 0.02 .. 0.15 starts only when the time branch truly leads (14.5.4, 14.7 H4 one atmosphere)
        const float SunsetLeadLo = 0.02f, SunsetLeadHi = 0.15f;

        /// <summary>0..1: how far the sunset phase u leads its progress branch (s - 411) / 105 (0 before s 411 is first
        /// reached and in capture mode). Read by the AtmosphereDriver.</summary>
        public static float SunsetLead;

        /// <summary>Progress path: the polyline with fillets 1.6 / 8 / 8 m (wider than the lane half widths 1.05 / 6.65, so
        /// no reachable point is ambiguous); s is exact outside the corner spans [576 +- 1.6], [676 +- 8], [760 +- 8].</summary>
        public static readonly FilletPath Path = new FilletPath(1.6f, 8f, 8f);

        public struct BeatRecord { public int Beat; public float S; public double T; public int Frame; }
        /// <summary>Every event raised (beat index 0..6; 100 = hatch; 200 = goal), in order.</summary>
        public static readonly List<BeatRecord> EventLog = new List<BeatRecord>(32);

        static ProgressDirector _inst;
        Action<float> _tick;
        float _d; bool _hasD;
        float _prevS;
        float _maxS; int _nextBeat; bool _hatch, _goal;
        double _t90 = double.NaN, _t231 = double.NaN, _t411 = double.NaN, _tCue = double.NaN;
        float _ark, _arkV, _sun, _sunV; bool _followInit;
        bool _wasReady;
        int _lastFrame = -1;
        static bool[] _failed = new bool[32];

        public static void ResetStatics()
        {
            EventLog.Clear();
            SunsetLead = 0f;
            RingShadowParams.Reset();
            for (int i = 0; i < _failed.Length; i++) _failed[i] = false;
        }

        void Awake()
        {
            _inst = this;
            _tick = Tick;
            RingRig.RegisterDirector(TickOrder, _tick);
        }

        void OnDestroy() { if (_inst == this) _inst = null; }

        /// <summary>Re-anchors the projection at s (after a teleport); followers snap on the next tick.</summary>
        public static void SnapTo(float s)
        {
            if (_inst == null) return;
            _inst._d = Path.DAtSharp(s);
            _inst._hasD = true;
            _inst._followInit = false;
        }

        /// <summary>Progress s of a world position (windowed around hintS; global if far from the path).</summary>
        public static float ProjectS(Vector3 p, float hintS)
        {
            var xz = new Vector2(p.x, p.z);
            float d = Path.Project(xz, Path.DAtSharp(hintS), ProjectWindow, out float dist);
            if (dist > TeleportDist) d = Path.Project(xz, 0f, -1f, out dist);
            return Path.SharpS(d);
        }

        void LateUpdate()
        {
            if (!RingRig.Directors.ContainsKey(TickOrder)) RingRig.RegisterDirector(TickOrder, _tick);
            RunChain(Time.deltaTime);
        }

        /// <summary>Runs every registered director once, in order (allocation-free: SortedList.Values is cached).</summary>
        public static void RunChain(float dt)
        {
            IList<Action<float>> vals = RingRig.Directors.Values;
            int n = vals.Count;
            if (_failed.Length < n) _failed = new bool[n + 8];
            for (int i = 0; i < n; i++)
            {
                if (_failed[i]) continue;
                try { vals[i](dt); }
                catch (Exception e)
                {
                    _failed[i] = true;
                    Debug.LogError("[Ring] director tick " + RingRig.Directors.Keys[i] + " failed and is disabled: " + e);
                }
            }
        }

        static float Bump01(float x)
        {
            if (x <= 0f || x >= 1f) return 0f;
            return x < 0.5f ? RingMath.Smootherstep(0f, 0.5f, x) : 1f - RingMath.Smootherstep(0.5f, 1f, x);
        }

        float ComputeS()
        {
            if (!float.IsNaN(RingRig.ForcedS))
            {
                float fs = RingRig.ForcedS;
                _d = Path.DAtSharp(fs); _hasD = true;
                return fs;
            }
            if (RingRig.Player == null) return _prevS;
            Vector3 p = RingRig.Player.position;
            var xz = new Vector2(p.x, p.z);
            float d = Path.Project(xz, _hasD ? _d : 0f, _hasD ? ProjectWindow : -1f, out float dist);
            if (dist > TeleportDist) d = Path.Project(xz, 0f, -1f, out dist);
            _d = d; _hasD = true;
            return Path.SharpS(d);
        }

        void Tick(float dt)
        {
            bool ready = RingRig.WorldReady;
            bool cap = RingState.Capture;
            float dtEff = (!ready || RingState.Paused) ? 0f : Mathf.Max(0f, dt);
            RingState.Dt = dtEff;
            if (!double.IsNaN(RingRig.ForcedT)) RingState.T = RingRig.ForcedT;
            else RingState.T += dtEff;
            double T = RingState.T;
            RingState.AnimT = (float)(T % 7200.0);
            int frame = Time.frameCount;
            if (ready && frame != _lastFrame) { RingState.FrameIndex++; _lastFrame = frame; }   // once per Unity frame (captures tick the chain extra times)

            float s = ComputeS();
            bool jumped = Mathf.Abs(s - _prevS) > JumpS;
            RingState.S = s;
            RingState.Beat = RingConstants.BeatAt(s);
            bool snap = cap || !_followInit || jumped || (ready && !_wasReady);

            // first-reach times of the time branches (live play only)
            if (ready && !cap)
            {
                if (double.IsNaN(_t90) && s >= RingConstants.CloudShadowFromS) _t90 = T;
                if (double.IsNaN(_t231) && s >= RingConstants.ArkTriggerS) _t231 = T;
                if (double.IsNaN(_t411) && s >= RingConstants.SunsetS0) _t411 = T;
                // the felt cue plays only when s is crossed while walking (a start beyond it, e.g. -progress 500, skips it)
                if (double.IsNaN(_tCue) && s >= CueS - CueHalfS) _tCue = s <= CueS + CueHalfS + 2f ? T : T - 10.0 * CueDur;
            }

            // ark phase (H8): a = (t - t231) / 190 (-inf before s 231), b = (s - 231) / 285
            float bA = ArkPhase.ProgressBranch(s);
            float uA = (cap || double.IsNaN(_t231)) ? bA : RingMath.Smax(ArkPhase.TimeBranch(T, _t231), bA);
            float tA = Mathf.Clamp(uA, -0.1f, 1f);
            if (snap) { _ark = tA; _arkV = 0f; }
            else _ark = RingMath.CriticalDamp(_ark, ref _arkV, tA, FollowOmega, dtEff);
            RingState.ArkU = Mathf.Clamp01(_ark);

            // sunset phase (14.5.4)
            float bS = (s - RingConstants.SunsetS0) / RingConstants.SunsetD;
            float uS = (cap || double.IsNaN(_t411)) ? bS : RingMath.Smax((float)((T - _t411) / RingConstants.SunsetT), bS);
            float tS = Mathf.Min(uS, 1f);
            if (snap) { _sun = tS; _sunV = 0f; }
            else _sun = RingMath.CriticalDamp(_sun, ref _sunV, tS, FollowOmega, dtEff);
            float u = Mathf.Min(_sun, 1f);
            RingState.SunsetU = u;
            RingState.Night = SunsetModel.Night(u);
            RingState.StarV = SunsetModel.V(u);
            // how far the sunset time branch leads its progress branch (0 on a forward walk at 1.5 m/s or faster and in
            // capture mode; up to 1 when the player lingers or backs up after s 411): the AtmosphereDriver uses it to
            // light the scene for u instead of s below s 423 (the schedule's beat 1-3 rows ignore u)
            SunsetLead = (cap || double.IsNaN(_t411)) ? 0f : RingMath.Smoothstep(SunsetLeadLo, SunsetLeadHi, u - bS);

            // felt cue (14.5.3): 3 s envelope centred when s crosses 375 (capture: from s, half-width 2.25 m)
            float cue;
            if (cap) cue = Bump01((s - (CueS - CueHalfS)) / (2f * CueHalfS));
            else cue = double.IsNaN(_tCue) ? 0f : Bump01((float)((T - _tCue) / CueDur));
            RingState.ArkCue = cue;
            RingShadowParams.KeyDip = 0.2f * cue;

            // ship shadow (14.5.3); the AtmosphereDriver gates the strength with the Ark set state of this frame.
            // Gated on "the ark has spawned" (first reach of s 231), not on the current s: after the spawn the phase keeps
            // its time branch, so a player backing up below 231 must keep the shadow (it is km ahead on visible ground).
            // Capture: ArkU = clamp01((s - 231) / 285) is 0 below 231, so the strength is 0 there as well.
            RingShadowParams.ShipCentre = ArkPhase.ShadowCentre(RingState.ArkU);
            RingShadowParams.ShipStrength = (!cap && double.IsNaN(_t231)) ? 0f : RingMath.Smoothstep(0f, 0.06f, RingState.ArkU);

            // cloud shadows (14.5.2): t = smax(t since s 90, (s - 90) / 1.5), drifting +X 12 m/s
            float bC = (s - RingConstants.CloudShadowFromS) / RingConstants.Walk;
            float tc = (cap || double.IsNaN(_t90)) ? bC : RingMath.Smax((float)(T - _t90), bC);
            float drift = RingConstants.CloudShadowSpeed * Mathf.Max(0f, tc);
            RingShadowParams.CloudA = RingConstants.CloudShadowA0 + new Vector2(drift, 0f);
            RingShadowParams.CloudB = RingConstants.CloudShadowB0 + new Vector2(drift, 0f);
            RingShadowParams.CloudStrength = RingConstants.CloudShadowStrength
                * RingMath.Smootherstep(RingConstants.CloudShadowFromS, RingConstants.CloudShadowFromS + CloudRampS, s)
                * (1f - RingMath.Smootherstep(CloudFadeS0, CloudFadeS1, s));

            // events: once each, in order (after load only)
            if (ready)
            {
                _maxS = Mathf.Max(_maxS, s);
                var beatS = RingConstants.BeatS;
                // beats and the hatch interleaved by their trigger s, so a start beyond them still fires in order
                while (true)
                {
                    float nextBeat = _nextBeat <= 6 ? beatS[_nextBeat] : float.MaxValue;
                    float hatchS = _hatch ? float.MaxValue : RingConstants.HatchTriggerS;
                    if (hatchS <= nextBeat && _maxS >= hatchS)
                    {
                        _hatch = true;
                        Record(100, s, T);
                        Debug.Log("[Ring] hatch triggered at s " + s.ToString("0.00") + ", t " + T.ToString("0.00"));
                        RingEvents.RaiseHatch();
                        continue;
                    }
                    if (_maxS >= nextBeat)
                    {
                        int b = _nextBeat++;
                        Record(b, s, T);
                        Debug.Log("[Ring] beat " + (b + 1) + " " + RingConstants.BeatNames[b] + " entered at s " + s.ToString("0.00") + ", t " + T.ToString("0.00"));
                        RingEvents.RaiseBeatEntered(b);
                        continue;
                    }
                    break;
                }
                if (!_goal && _maxS >= RingConstants.GoalS)
                {
                    _goal = true;
                    RingState.GoalReached = true;
                    Record(200, s, T);
                    Debug.Log("[Ring] goal reached at s " + s.ToString("0.00") + ", t " + T.ToString("0.00"));
                    RingEvents.RaiseGoal();
                }
            }

            _prevS = s;
            _wasReady = ready;
            _followInit = true;
        }

        static void Record(int ev, float s, double t)
        {
            if (EventLog.Count < EventLog.Capacity) EventLog.Add(new BeatRecord { Beat = ev, S = s, T = t, Frame = Time.frameCount });
        }
    }
}
