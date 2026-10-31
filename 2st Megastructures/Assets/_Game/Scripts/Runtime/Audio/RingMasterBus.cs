using System;
using System.Threading;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Post-reverb master bus (docs/research/05 sections 0 and 6.9, pitfall 7): an OnAudioFilterRead on the
    /// AudioListener's GameObject (added by the AudioDirector at load, so it is the last component and processes the
    /// final mix after the AudioReverbFilter). It runs a second look-ahead limiter with the master settings of 14.3
    /// (threshold -3 dB, ceiling -1.2 dBFS internal, 5 ms, 80 ms release): transparent below -3 dBFS, so it only acts
    /// if the native reverb ever pushes a peak up. It also captures the first seconds of the final output for the
    /// -audio-dump proof (FILE_post.wav). Read-modify-write, no allocation, no Unity API on the audio thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RingMasterBus : MonoBehaviour
    {
        readonly DspLimiter _lim = new DspLimiter();
        volatile int _fs = 48000;
        int _configuredFs;
        int _callbacks, _lastChannels;
        float _peak;                        // block peak of the final output (diagnostics)

        float[] _cap;
        int _capPos, _capArmed, _capReady;

        public int CallbackCount => Volatile.Read(ref _callbacks);
        public int LastChannels => Volatile.Read(ref _lastChannels);
        public float LastBlockPeak => Volatile.Read(ref _peak);
        public double LimiterMinGain => _lim.MinGain;
        public int SampleRate => _fs;

        /// <summary>Adds the bus to the listener's GameObject (once). Returns the existing bus if there is one.</summary>
        public static RingMasterBus Attach(AudioListener listener)
        {
            if (listener == null) return null;
            var bus = listener.GetComponent<RingMasterBus>();
            if (bus == null) bus = listener.gameObject.AddComponent<RingMasterBus>();
            return bus;
        }

        void Awake()
        {
            _fs = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _configuredFs = _fs;
            _lim.Configure(_fs, AudioSpec.LimiterThresholdDb, AudioSpec.LimiterCeilingDb, AudioSpec.LimiterLookaheadMs, AudioSpec.LimiterReleaseMs);
        }

        /// <summary>Main thread: the output rate changed (the audio thread re-configures the limiter itself).</summary>
        public void SetSampleRate(int fs) { if (fs > 1000) _fs = fs; }

        /// <summary>Main thread, at load: preallocates the capture of the first <paramref name="seconds"/> of the final mix.</summary>
        public void PrepareCapture(float seconds)
        {
            _cap = new float[(int)(Mathf.Clamp(seconds, 1f, 120f) * Math.Max(_fs, 8000)) * 2];
            _capPos = 0; _capReady = 0; _capArmed = 0;
        }

        public void ArmCapture() { if (_cap != null) Volatile.Write(ref _capArmed, 1); }
        public bool CaptureReady => Volatile.Read(ref _capReady) == 1;
        public float[] CaptureBuffer => _cap;
        public int CaptureFrames => Volatile.Read(ref _capPos) / 2;

        // ---------------- audio thread ----------------
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0) return;
            int fs = _fs;
            if (fs != _configuredFs)
            {
                _configuredFs = fs;
                _lim.Configure(fs, AudioSpec.LimiterThresholdDb, AudioSpec.LimiterCeilingDb, AudioSpec.LimiterLookaheadMs, AudioSpec.LimiterReleaseMs);
            }
            int frames = data.Length / channels;
            float pk = 0f;
            for (int f = 0, i = 0; f < frames; f++, i += channels)
            {
                double l = data[i];
                double r = channels > 1 ? data[i + 1] : l;
                if (double.IsNaN(l) || double.IsInfinity(l)) l = 0.0;
                if (double.IsNaN(r) || double.IsInfinity(r)) r = 0.0;
                _lim.Process(ref l, ref r);
                data[i] = (float)l;
                if (channels > 1) data[i + 1] = (float)r;
                float a = (float)(l < 0 ? -l : l), b = (float)(r < 0 ? -r : r);
                if (a > pk) pk = a; if (b > pk) pk = b;
            }
            Volatile.Write(ref _peak, pk);
            Volatile.Write(ref _lastChannels, channels);
            Interlocked.Increment(ref _callbacks);

            var cap = _cap;
            if (cap != null && Volatile.Read(ref _capArmed) == 1 && Volatile.Read(ref _capReady) == 0)
            {
                int pos = _capPos;
                for (int f = 0; f < frames && pos + 1 < cap.Length; f++)
                {
                    float l = data[f * channels];
                    cap[pos++] = l;
                    cap[pos++] = channels > 1 ? data[f * channels + 1] : l;
                }
                Volatile.Write(ref _capPos, pos);
                if (pos + 1 >= cap.Length) Volatile.Write(ref _capReady, 1);
            }
        }
    }
}
