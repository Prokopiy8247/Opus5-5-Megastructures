using System.Threading;

namespace Megastructure.Audio
{
    /// <summary>
    /// One snapshot of everything the audio thread needs, written by the main thread (AudioDirector, or the offline
    /// renderer) once per frame and read by the ProceduralAudioEngine at control rate. Plain value type, no references,
    /// so publishing and reading never allocate.
    ///
    /// Gains are linear. For bed layers 1.0 means a per-channel RMS of 0 dBFS; for event layers (pulse, groan, bell,
    /// rail tick, footsteps) 1.0 means a dry event peak of 0 dBFS. The engine applies the calibrated normalisation.
    /// Clocks are extrapolated by the engine between snapshots (value + rate * elapsed), so events land on the right
    /// sample even though snapshots arrive at the frame rate.
    /// </summary>
    public struct AudioParams
    {
        /// <summary>Incremented by the writer on every publish (diagnostics).</summary>
        public int Version;

        // ------------------------------------------------------------------ clocks
        /// <summary>SceneClock.Time at publish; drives GROAN / BELL schedules.</summary>
        public double SceneTime;
        /// <summary>1 while the scene clock advances, 0 when frozen or paused.</summary>
        public float ClockRate;
        /// <summary>SceneClock.RealTime at publish (title timeline: door thud).</summary>
        public double RealTime;
        /// <summary>
        /// True once the walk clock has begun (ClockDriver.Running): only from then on is <see cref="RealTime"/> the walk's
        /// real time (it also runs from 0 during the black load, and ClockDriver.Begin resets it). Gates the door thud.
        /// </summary>
        public bool WalkStarted;
        /// <summary>Inner-left crank angle in degrees (AbyssConst.CrankThetaIL) and its rate; a dead centre every 45 degrees.</summary>
        public double CrankTheta;
        public float CrankRate;
        /// <summary>Stereo pan of the four crank banks IL, IR, OR, OL (bank v / 96: the player walks -X, +v is to the right).</summary>
        public float PulsePan0, PulsePan1, PulsePan2, PulsePan3;
        /// <summary>Fan blade-pass phase in cycles (scene time / 3.33 s) and its rate in cycles per second.</summary>
        public double FanPhase;
        public float FanRate;
        /// <summary>Lift travel s (m) and speed v (m/s); a rail tick every 12 m while <see cref="LiftTicking"/>.</summary>
        public double LiftS;
        public float LiftV;
        public bool LiftTicking;

        // ------------------------------------------------------------------ layer gains (linear)
        public float Sub;
        public float Air;
        public float Hum;
        public float Pulse;
        public float Groan;
        /// <summary>Dry multiplier of GROAN (1 near, lower for the far groans of beat 7 outside; the send stays).</summary>
        public float GroanDry;
        public float Bell;
        public float LiftRumble;
        public float LiftWhine;
        public float LiftTick;
        public float Fan;
        public float Airflow;
        public float Wind;
        public float Footsteps;
        /// <summary>Ending SUB tone (31 Hz, 14.3 d), added after the master fade.</summary>
        public float EndTone;

        // ------------------------------------------------------------------ reverb target (blended by the engine)
        public float RevRt60;
        public float RevPreDelay;
        public float RevDamping;
        public float RevWetDb;
        /// <summary>Duration of the blend toward this preset when it becomes the target (4 s; 5 s for the hall exit).</summary>
        public float RevBlendSeconds;
        /// <summary>Multiplier of the reverb return (1; follows the master fade during the ending so the tail dies too).</summary>
        public float TailGain;

        // ------------------------------------------------------------------ master
        /// <summary>Master fade with the picture (GameState.PictureFade), applied before the reverb send.</summary>
        public float MasterFade;
        /// <summary>Pause duck target (1, or -15 dB while paused); smoothed by the engine over about 0.25 s.</summary>
        public float Duck;
        /// <summary>Per-beat master trim T_b in dB (AudioMix.MasterTrimDb, blended across beat transitions).</summary>
        public float TrimDb;
        /// <summary>Walk real time of the start door thud (0.8 s, once per run once <see cref="WalkStarted"/>); negative disables it.</summary>
        public float ThudTime;
    }

    /// <summary>
    /// Lock-free single-producer / single-consumer exchange of <see cref="AudioParams"/> ("double-buffered parameters",
    /// implemented as the classic three-slot buffer so neither side ever waits or reads a half-written snapshot).
    /// The writer owns one slot, the reader owns one slot, the middle slot is swapped with Interlocked.Exchange (a full
    /// fence) together with a dirty bit. No locks, no allocation.
    /// </summary>
    public sealed class AudioParamsExchange
    {
        const int DirtyBit = 4;
        const int IndexMask = 3;

        readonly AudioParams[] _slots = new AudioParams[3];
        int _back;        // writer-owned slot index
        int _middle = 1;  // shared: slot index | dirty bit
        int _front = 2;   // reader-owned slot index

        /// <summary>Main thread: publish the latest snapshot (overwrites an unread older one).</summary>
        public void Publish(in AudioParams p)
        {
            _slots[_back] = p;
            int prev = Interlocked.Exchange(ref _middle, _back | DirtyBit);
            _back = prev & IndexMask;
        }

        /// <summary>Audio thread: if a newer snapshot exists, copy it into <paramref name="latest"/> and return true.</summary>
        public bool TryAcquire(ref AudioParams latest)
        {
            if ((Volatile.Read(ref _middle) & DirtyBit) == 0) return false;
            int prev = Interlocked.Exchange(ref _middle, _front);
            _front = prev & IndexMask;
            latest = _slots[_front];
            return true;
        }
    }
}
