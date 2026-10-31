using System.Threading;

namespace Megastructure
{
    /// <summary>
    /// Implementation of the footstep queue behind <see cref="AudioEvents.Footstep"/>: a lock-free single-producer
    /// (main thread, PlayerController) / single-consumer (audio thread, ProceduralAudioEngine) ring of 64 entries.
    /// The producer only writes <c>_head</c>, the consumer only writes <c>_tail</c>; Volatile publishes the slot before
    /// the index. A full ring drops the newest step (cannot happen at walking speed: the engine drains every ~0.7 ms).
    /// </summary>
    public static partial class AudioEvents
    {
        const int Capacity = 64;
        const int Mask = Capacity - 1;
        static readonly byte[] _ring = new byte[Capacity];
        static int _head;
        static int _tail;
        static int _dropped;

        /// <summary>Footsteps dropped because the ring was full (diagnostics; stays 0 in a healthy run).</summary>
        public static int DroppedFootsteps => Volatile.Read(ref _dropped);

        static partial void EnqueueFootstep(SurfaceKind surface)
        {
            int h = _head;
            int next = (h + 1) & Mask;
            if (next == Volatile.Read(ref _tail)) { _dropped++; return; }
            _ring[h] = (byte)surface;
            Volatile.Write(ref _head, next);
        }

        /// <summary>Audio thread: pop the oldest queued footstep.</summary>
        public static bool TryDequeueFootstep(out SurfaceKind surface)
        {
            int t = _tail;
            if (t == Volatile.Read(ref _head)) { surface = SurfaceKind.Concrete; return false; }
            surface = (SurfaceKind)_ring[t];
            Volatile.Write(ref _tail, (t + 1) & Mask);
            return true;
        }

        /// <summary>Consumer side: discard everything queued (engine start, so stale steps from loading never play).</summary>
        public static void DrainFootsteps()
        {
            Volatile.Write(ref _tail, Volatile.Read(ref _head));
        }
    }
}
