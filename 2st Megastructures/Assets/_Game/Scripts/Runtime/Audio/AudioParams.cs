using System.Threading;

namespace Megastructure
{
    /// <summary>
    /// Everything the audio engine needs for one instant, as pure values (no references). Built on the main thread by
    /// the AudioDirector each Update (or by AudioWalkSim offline) and handed to the audio thread through
    /// <see cref="AudioParamChannel"/>. Angles are degrees relative to the listener's view direction, + = right.
    /// One-shot events (footsteps) travel separately through the engine's lock-free <see cref="RingAudioEngine.Events"/>
    /// queue, stamped in game time T; rib ticks and beacon pings are scheduled inside the engine from S and BeaconT0.
    /// </summary>
    public struct AudioParams
    {
        /// <summary>Main-thread frame counter; a change marks a new snapshot (clock re-anchoring).</summary>
        public int Frame;
        /// <summary>Progress along the sharp polyline (m).</summary>
        public double S;
        /// <summary>Game time (s): stops while paused, equals RingState.T.</summary>
        public double T;
        /// <summary>Ground speed (m/s), used to extrapolate S between snapshots.</summary>
        public float Speed;
        /// <summary>Beat index 0..6 (informational; the engine derives its windows from S).</summary>
        public int Beat;
        /// <summary>Sunset phase u (14.5.4), unclamped, &lt; 0 before s 411.</summary>
        public float SunsetU;
        /// <summary>Seed of the W1 gust smooth-random (the same seed drives RingState.Gust on the main thread).</summary>
        public uint GustSeed;
        /// <summary>1 while paused: the master ducks 12 dB over 0.4 s.</summary>
        public float PauseDuck;
        /// <summary>Fade with the picture at the start (0..1).</summary>
        public float PictureFade;
        /// <summary>Ending fade (S5): 1 = full, 0 = silent.</summary>
        public float EndingFade;
        /// <summary>Game time of the hatch trigger (s 6.5); &lt; 0 = not triggered.</summary>
        public double HatchT;
        /// <summary>Game time t0 at which s first reached 905 (back-dated at 1.5 m/s when the walk starts beyond it): the
        /// beacon pulse eases in from t0 and peaks at t0 + 1 + 4 k, and every peak is a B1 ping (the beat-7
        /// BeaconPulse.T0, same origin). &lt; 0 = not yet.</summary>
        public double BeaconT0;
        /// <summary>Beat-3 felt cue envelope 0..1 (RingState.ArkCue): W1 ducks 4 dB.</summary>
        public float ArkCue;
        /// <summary>Bent-space range from the listener to the ark centre (m); &lt;= 0 = no ark.</summary>
        public float ArkRange;
        public float ArkAzimuth;
        /// <summary>Horizontal distance to the nearest water edge (m); &lt; 0 = unknown.</summary>
        public float RiverDist;
        /// <summary>Horizontal distance to the river centre line (m) (pan width when standing over the water).</summary>
        public float RiverCentreDist;
        public float RiverAzimuth;

        public static AudioParams Default()
        {
            var p = new AudioParams();
            p.Frame = 0; p.S = 0; p.T = 0; p.Speed = 0; p.Beat = 0; p.SunsetU = -4f; p.GustSeed = 0x6A09E667u;
            p.PauseDuck = 0f; p.PictureFade = 0f; p.EndingFade = 1f; p.HatchT = -1.0; p.BeaconT0 = -1.0;
            p.ArkCue = 0f; p.ArkRange = -1f; p.ArkAzimuth = 0f; p.RiverDist = -1f; p.RiverCentreDist = -1f; p.RiverAzimuth = 0f;
            return p;
        }
    }

    /// <summary>Kinds of <see cref="AudioEvent"/>.</summary>
    public static class AudioEventKind
    {
        public const int Footstep = 1;
    }

    /// <summary>One-shot sound event, timed in game time T (the engine starts it when its T clock reaches the stamp).</summary>
    public struct AudioEvent
    {
        public int Kind;
        public int Surface;
        public int Foot;
        public float Speed;
        public double T;
    }

    /// <summary>
    /// Single-producer single-consumer ring of events (main thread -> audio thread), wait-free and allocation-free.
    /// Capacity must be a power of two.
    /// </summary>
    public sealed class AudioEventQueue
    {
        readonly AudioEvent[] _buf;
        readonly int _mask;
        int _head, _tail;

        public AudioEventQueue(int capacityPow2)
        {
            int c = 1; while (c < capacityPow2) c <<= 1;
            _buf = new AudioEvent[c]; _mask = c - 1;
        }

        public bool TryEnqueue(in AudioEvent e)
        {
            int h = _head;
            if (h - Volatile.Read(ref _tail) >= _buf.Length) return false;
            _buf[h & _mask] = e;
            Volatile.Write(ref _head, h + 1);
            return true;
        }

        public bool TryDequeue(out AudioEvent e)
        {
            int t = _tail;
            if (t == Volatile.Read(ref _head)) { e = default; return false; }
            e = _buf[t & _mask];
            Volatile.Write(ref _tail, t + 1);
            return true;
        }

        /// <summary>Consumer-side reset (call only when the producer is idle).</summary>
        public void Clear() { Volatile.Write(ref _tail, Volatile.Read(ref _head)); }
    }

    /// <summary>
    /// Seqlock snapshot of <see cref="AudioParams"/> (research 05 section 7.1): the main thread publishes once per frame,
    /// the audio thread reads once per callback and keeps its previous snapshot if a write is in progress. No locks,
    /// no allocation, never spins.
    /// </summary>
    public sealed class AudioParamChannel
    {
        int _seq;
        AudioParams _data;

        public void Publish(in AudioParams p)
        {
            int s = _seq;
            Volatile.Write(ref _seq, s + 1);      // odd: writing
            Interlocked.MemoryBarrier();
            _data = p;
            Volatile.Write(ref _seq, s + 2);      // even: stable (release)
        }

        public bool TryRead(ref AudioParams dst)
        {
            int s1 = Volatile.Read(ref _seq);
            if ((s1 & 1) != 0) return false;
            AudioParams tmp = _data;
            Interlocked.MemoryBarrier();
            if (Volatile.Read(ref _seq) != s1) return false;
            dst = tmp;
            return true;
        }
    }
}
