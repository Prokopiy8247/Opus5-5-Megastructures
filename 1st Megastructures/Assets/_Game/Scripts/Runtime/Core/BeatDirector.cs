using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Beat bookkeeping and the per-frame beat tick (Part 7.3, CONTRACTS sections 3 and 11). Execution order 0.
    ///
    /// Every frame: asks ProgressDirector for this frame's d (so beats never see last frame's progress), derives the current
    /// beat index with 20 m hysteresis (forward the beat changes at its start d, backward only 20 m before it), records the
    /// first activation time of every beat in <see cref="Fired"/> (the autotest checks beats 1-7 fire once and in order) and
    /// calls <see cref="BeatBase.Tick"/> of every built beat. Beats stay built for the whole walk (persistent skeleton, 14.6);
    /// only their detail tiers switch, by camera distance, inside their own Tick. Zero GC per frame.
    /// A beat whose Build failed is never ticked; a beat whose Tick throws is logged once ([ABYSS-ERROR]) and dropped from
    /// the tick, so one broken beat never stops the rest of the walk.
    /// </summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class BeatDirector : MonoBehaviour
    {
        public static BeatDirector Instance;

        /// <summary>Index 0..6 = beats 1..7 (assigned by GameBootstrap from its serialized list).</summary>
        public BeatBase[] Beats;

        /// <summary>First activation of each beat, in order: (beat 1..7, SceneClock.Time).</summary>
        public readonly List<(int beat, double time)> Fired = new List<(int beat, double time)>(8);

        /// <summary>True once GameBootstrap has built the beats; before that nothing is ticked.</summary>
        public bool Running { get; private set; }

        /// <summary>The frame data passed to the beats this frame.</summary>
        public BeatFrame LastFrame => _frame;

        bool[] _built = new bool[7];
        bool[] _fired = new bool[8];
        BeatFrame _frame;
        Vector3 _prevPlayerPos;
        bool _havePrev;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by GameBootstrap after the load: which beats built successfully, and the starting beat.</summary>
        public void Begin(bool[] built, int startBeat)
        {
            int n = Beats != null ? Beats.Length : 0;
            _built = new bool[n];
            for (int i = 0; i < n; i++) _built[i] = built != null && i < built.Length && built[i];
            Fired.Clear();
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
            GameState.Beat = Mathf.Clamp(startBeat, 1, 7);
            MarkFired(GameState.Beat);
            _havePrev = false;
            Running = true;
        }

        /// <summary>Re-derive the beat without hysteresis (after teleports / captures).</summary>
        public void SnapBeat()
        {
            int b = RawBeat(GameState.D);
            if (b != GameState.Beat) { GameState.Beat = b; MarkFired(b); }
        }

        void Update()
        {
            if (!Running) return;
            ProgressDirector pd = ProgressDirector.Instance;
            if (pd != null) pd.UpdateProgress();

            float d = GameState.D;
            int beat = HysteresisBeat(GameState.Beat, d);
            if (beat != GameState.Beat)
            {
                GameState.Beat = beat;
                MarkFired(beat);
            }

            // Camera position of this frame, predicted: last frame's final camera plus this frame's player displacement
            // (exact for a static capture pose; on the lift the platform motion is already in the player position).
            Vector3 pp = GameState.PlayerPos;
            Vector3 cam = GameState.CameraPos;
            if (_havePrev) cam += pp - _prevPlayerPos;
            _prevPlayerPos = pp;
            _havePrev = true;

            _frame.Time = SceneClock.Time;
            _frame.DeltaTime = SceneClock.Frozen ? 0f : Time.deltaTime;
            _frame.D = d;
            _frame.E = GameState.E;
            _frame.RideSeconds = GameState.RideSeconds;
            _frame.RideU = GameState.RideU;
            _frame.CameraPos = cam;
            _frame.Camera = GameState.MainCamera;

            BeatBase[] beats = Beats;
            if (beats == null) return;
            for (int i = 0; i < beats.Length; i++)
            {
                if (i >= _built.Length || !_built[i] || beats[i] == null) continue;
                // try/catch costs nothing without an exception (no allocation). A throwing beat is logged once and no longer
                // ticked, so it neither stops the beats after it this frame nor floods the log every frame.
                try { beats[i].Tick(in _frame); }
                catch (Exception ex)
                {
                    _built[i] = false;
                    Debug.LogError("[ABYSS-ERROR] beat " + (i + 1) + " Tick failed; beat no longer ticked: " + ex);
                }
            }
        }

        /// <summary>True when beat 1..7 built successfully and is ticked (false after a failed Build or Tick).</summary>
        public bool IsActive(int beat)
        {
            int i = beat - 1;
            return Running && i >= 0 && i < _built.Length && _built[i] && Beats != null && i < Beats.Length && Beats[i] != null;
        }

        void MarkFired(int beat)
        {
            if (beat < 1 || beat > 7 || _fired[beat]) return;
            _fired[beat] = true;
            Fired.Add((beat, SceneClock.Time));
            Debug.Log("[ABYSS] beat " + beat + " (" + AbyssConst.BEAT_NAMES[beat - 1] + ") first active at t = "
                      + SceneClock.Time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s, d = "
                      + GameState.D.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Beat 1..7 for d without hysteresis (AbyssConst.BEAT_START_D).</summary>
        public static int RawBeat(float d)
        {
            float[] s = AbyssConst.BEAT_START_D;
            int b = 1;
            for (int i = 1; i < s.Length; i++) if (d >= s[i]) b = i + 1;
            return b;
        }

        /// <summary>
        /// Beat with hysteresis: moving forward the index rises at the next beat's start d; moving back it falls only once d
        /// is BEAT_HYSTERESIS (20 m) below the current beat's start.
        /// </summary>
        public static int HysteresisBeat(int current, float d)
        {
            float[] s = AbyssConst.BEAT_START_D;
            current = Mathf.Clamp(current, 1, 7);
            while (current < 7 && d >= s[current]) current++;
            while (current > 1 && d < s[current - 1] - AbyssConst.BEAT_HYSTERESIS) current--;
            return current;
        }
    }
}
