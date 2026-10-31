// ThreadSynth: the OnAudioFilterRead host of ThreadDsp (MASTER_PROMPT 14.3.1). It lives on the AudioListener
// GameObject, so it receives the listener mix (the footstep AudioSources) and the synth's layers, reverb and
// limiter process the whole mix. It only forwards: the main thread publishes ThreadAudioParams through a
// double-buffered mailbox with a volatile index (a torn read only mixes two consecutive frames' targets, which
// the per-sample smoothing hides); the audio thread copies the targets and calls ThreadDsp.Process.
// Audio-thread rules (R5 section 2): no UnityEngine calls, no allocation, no locks, no logging; exceptions are
// stored and logged once by the main thread; a heartbeat counts callbacks; an optional preallocated capture
// records the real output for -audio-dump.
// Fallback (R5 decision 1): if the listener filter never runs, AudioDirector puts a second ThreadSynth in
// generator mode on a clip-less AudioSource.
using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Megastructure.Audio
{
    /// <summary>Single-producer single-consumer linear capture: the audio thread writes, the main thread reads once full.</summary>
    public sealed class AudioCapture
    {
        readonly float[] m_buf;
        int m_count;
        volatile int m_state;          // 0 idle, 1 armed, 2 recording, 3 full
        public int Channels { get; private set; }
        public readonly int SampleRate;

        public AudioCapture(double seconds, int sampleRate, int maxChannels)
        {
            SampleRate = sampleRate;
            m_buf = new float[Math.Max(1024, (int)(seconds * sampleRate) * Math.Max(1, maxChannels))];
        }

        public void Arm() { m_count = 0; m_state = 1; }
        public bool IsFull { get { return m_state == 3; } }
        public bool IsRecording { get { int s = m_state; return s == 1 || s == 2; } }
        public int Count { get { return m_count; } }
        public float[] Buffer { get { return m_buf; } }

        /// <summary>Audio thread; allocation-free.</summary>
        public void Write(float[] data, int channels)
        {
            int st = m_state;
            if (st == 1) { Channels = channels; st = 2; }
            if (st != 2) return;
            int n = Math.Min(data.Length, m_buf.Length - m_count);
            n -= n % Math.Max(1, channels);
            if (n > 0) Array.Copy(data, 0, m_buf, m_count, n);
            m_count += n;
            m_state = (m_buf.Length - m_count < channels || n <= 0) ? 3 : 2;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ThreadSynth : MonoBehaviour
    {
        ThreadDsp m_dsp;                       // swapped atomically (Volatile) on a device change
        ThreadAudioParams m_a, m_b;
        volatile int m_front;
        int m_callbacks;
        long m_lastTicks, m_maxTicks;
        Exception m_lastError;
        AudioCapture m_capture;
        volatile bool m_bypass;

        /// <summary>True on the generator fallback (a clip-less AudioSource): the input is silence, not the listener mix.</summary>
        public bool GeneratorMode { get; private set; }
        public int SampleRate { get; private set; }

        /// <summary>Callbacks so far (heartbeat).</summary>
        public int Callbacks { get { return Volatile.Read(ref m_callbacks); } }
        /// <summary>Last and worst callback cost in milliseconds.</summary>
        public double LastCallbackMs { get { return Interlocked.Read(ref m_lastTicks) * 1000.0 / Stopwatch.Frequency; } }
        public double MaxCallbackMs { get { return Interlocked.Read(ref m_maxTicks) * 1000.0 / Stopwatch.Frequency; } }
        public int NanResets { get { var d = Volatile.Read(ref m_dsp); return d != null ? d.NanResets : 0; } }

        /// <summary>When true the callback leaves the buffer untouched (the generator fallback took over).</summary>
        public bool Bypass { get { return m_bypass; } set { m_bypass = value; } }

        /// <summary>Capture of the real output (-audio-dump); null when idle.</summary>
        public AudioCapture Capture
        {
            get { return Volatile.Read(ref m_capture); }
            set { Volatile.Write(ref m_capture, value); }
        }

        /// <summary>Builds the DSP on the main thread (calibration allocates; never on the audio thread).</summary>
        public void Init(int sampleRate, int seed, bool generatorMode)
        {
            SineTable.Warm();
            GeneratorMode = generatorMode;
            SampleRate = sampleRate;
            Volatile.Write(ref m_dsp, new ThreadDsp(sampleRate, seed));
        }

        /// <summary>Rebuilds the DSP at a new output rate (OnAudioConfigurationChanged) and snaps it to the current targets.</summary>
        public void Rebuild(int sampleRate, int seed, in ThreadAudioParams current)
        {
            var d = new ThreadDsp(sampleRate, seed);
            d.SnapTo(in current);
            SampleRate = sampleRate;
            Volatile.Write(ref m_dsp, d);
        }

        /// <summary>Main thread: publish this frame's targets.</summary>
        public void Publish(in ThreadAudioParams p)
        {
            if (m_front == 0) { m_b = p; m_front = 1; }
            else { m_a = p; m_front = 0; }
        }

        /// <summary>Main thread: returns (and clears) an exception raised on the audio thread.</summary>
        public Exception TakeError() { return Interlocked.Exchange(ref m_lastError, null); }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (m_bypass) return;
            var dsp = Volatile.Read(ref m_dsp);
            if (dsp == null) return;
            try
            {
                long t0 = Stopwatch.GetTimestamp();
                ThreadAudioParams p = m_front == 0 ? m_a : m_b;
                dsp.Process(data, channels, in p);
                var cap = Volatile.Read(ref m_capture);
                if (cap != null) cap.Write(data, channels);
                Interlocked.Increment(ref m_callbacks);
                long dt = Stopwatch.GetTimestamp() - t0;
                Interlocked.Exchange(ref m_lastTicks, dt);
                if (dt > Interlocked.Read(ref m_maxTicks)) Interlocked.Exchange(ref m_maxTicks, dt);
            }
            catch (Exception e)
            {
                m_lastError = e;   // storing a reference does not allocate; the main thread logs it once
            }
        }
    }
}
