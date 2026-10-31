using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Advances the global <see cref="SceneClock"/> once per frame, first of all game scripts (execution order -1000), and
    /// publishes the <c>_AbTime</c> shader global (ShaderIds.AbTime = (SceneClock mod 3600, RealTime mod 3600, 0, 0)).
    ///
    /// - <see cref="SceneClock.Time"/> advances by <c>Time.deltaTime</c> (which follows <c>Time.captureDeltaTime</c> in capture
    ///   sequences) unless the clock is frozen (<c>-freeze-clock</c>, capture mode), the game is paused, or the walk clock is held.
    /// - <see cref="SceneClock.RealTime"/> always advances, by the unscaled frame time, or by <c>Time.captureDeltaTime</c> while a
    ///   capture rate is set so that title fades stay deterministic in captured sequences.
    /// - <c>-clock T</c> sets the start time of the scene clock; <c>-freeze-clock</c> freezes it.
    ///
    /// GameBootstrap may call <see cref="Hold"/> during loading and <see cref="Begin"/> when the walk starts (t = 0 is the first
    /// frame after the load fade begins). Without those calls the clock simply runs from the first frame.
    /// (<c>-quit-after</c> is handled by QaHarness, which runs the QA finalizers and sets the exit code.)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class ClockDriver : MonoBehaviour
    {
        public static ClockDriver Instance { get; private set; }

        /// <summary>False while the walk clock is held (loading). RealTime keeps running.</summary>
        public static bool Running { get; private set; } = true;

        /// <summary>Frame counter since Begin (for dither phase and diagnostics).</summary>
        public static long Frame { get; private set; }

        /// <summary>When true SceneClock does not advance while GameState.Paused (the pause overlay freezes the machines).</summary>
        public bool FreezeOnPause = true;

        /// <summary>Statics survive play-mode entry when domain reload is disabled: start every session running.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Running = true;
            Frame = 0;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ABYSS] ClockDriver: second instance ignored on " + name);
                enabled = false;
                return;
            }
            Instance = this;
            CliArgs.Parse();
            ApplyCliFlags(0.0);
            Publish();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Stop advancing SceneClock.Time (loading). RealTime keeps running.</summary>
        public static void Hold()
        {
            Running = false;
        }

        /// <summary>
        /// Start the walk clock at t0 (seconds). A <c>-clock T</c> argument overrides t0. RealTime restarts at 0.
        /// <c>-freeze-clock</c> is re-applied.
        /// </summary>
        public static void Begin(double t0)
        {
            ApplyCliFlags(t0);
            SceneClock.RealTime = 0.0;
            Frame = 0;
            Running = true;
            Publish();
        }

        static void ApplyCliFlags(double t0)
        {
            double cli = CliArgs.Clock;
            SceneClock.Reset(double.IsNaN(cli) ? t0 : cli);
            if (CliArgs.FreezeClock) SceneClock.Frozen = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float capture = Time.captureDeltaTime;
            double realDt = capture > 0f ? capture : Time.unscaledDeltaTime;

            SceneClock.RealTime += realDt;

            bool paused = FreezeOnPause && GameState.Paused;
            if (Running && !SceneClock.Frozen && !paused && dt > 0f)
                SceneClock.Time += dt;

            if (Running) Frame++;
            Publish();
        }

        /// <summary>Write the _AbTime global (also callable after a capture harness sets the clock directly).</summary>
        public static void Publish()
        {
            double t = SceneClock.Time;
            double r = SceneClock.RealTime;
            var v = new Vector4(
                (float)(t - 3600.0 * System.Math.Floor(t / 3600.0)),
                (float)(r - 3600.0 * System.Math.Floor(r / 3600.0)),
                0f, 0f);
            Shader.SetGlobalVector(ShaderIds.AbTime, v);
        }
    }
}
