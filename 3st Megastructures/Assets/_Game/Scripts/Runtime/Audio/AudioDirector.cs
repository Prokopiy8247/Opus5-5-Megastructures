// AudioDirector: the main-thread side of the procedural audio (MASTER_PROMPT 14.3, Part 9 "Audio").
// Lives on the camera GameObject next to the AudioListener (scene contract). In Awake it registers
// ThreadServices.Audio, adds ThreadSynth to the same GameObject (so the listener mix, i.e. the footsteps,
// passes through the synth's reverb and limiter), pre-renders the 6 x 5 footstep clips (AudioClip.Create +
// SetData) and creates a pool of 4 2D AudioSources on a child GameObject (a GameObject may not carry both an
// OnAudioFilterRead listener filter and AudioSources, R5). Each frame it computes ThreadAudioParams from the
// progress s (ProgressDirector.S), the lateral offset, the brisk flag, the intro clock, the pause ramp and the
// ending clock and publishes them to ThreadSynth.
// Footsteps: IAudioHooks.Footstep plays a step on request; if nobody calls it, the director runs the cadence
// itself (one step per 0.78 m walked, 0.80 m on the stairs) and stops doing so at the first external call.
// Metal steps get brighter with D: a second metal set is re-rendered in the background (one variant per
// frame, allocation-free) whenever the quantised D changes and the idle set has not played for 1.4 s.
// -audio-dump <file> [-audio-dump-seconds N]: records N seconds (default 10) of the real OnAudioFilterRead
// output after the intro and writes a 16-bit WAV plus a JSON line of stats from the main thread.
// Zero allocations per frame.
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Megastructure.Audio
{
    [DefaultExecutionOrder(700)]   // after ProgressDirector (500) and BeatDirector (600): s is final for the frame
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour, IAudioHooks
    {
        public static AudioDirector Instance { get; private set; }

        [Tooltip("Run the footstep cadence here when no other system calls IAudioHooks.Footstep.")]
        public bool autoFootsteps = true;
        [Tooltip("Seconds of the pause duck ramp.")]
        public float pauseRampSeconds = 0.6f;

        const double HeartbeatTimeout = 1.0;       // s of unscaled time without a callback -> generator fallback
        const int HeartbeatFrames = 20;            // ... and at least this many rendered frames
        const double TeleportDistance = 3.0;       // m in one frame: a teleport, not a step
        const double StillRearm = 0.5;             // s standing still: the next movement starts a new stride
        const double MetalIdleBeforeRender = 1.4;  // s: the idle metal set is silent (clips are 1.15 s)
        const double DumpAfterIntro = 4.0;         // s after the intro start (picture and wind are in)
        const double AutoEngageDistance = 1.7;     // m walked without an external Footstep call -> run the cadence here
        /// <summary>Clips are stored this much louder than their 14.3.5 peak so caller gains up to 1.25 fit in
        /// AudioSource.volume (0..1): volume = gain / ClipHeadroom gives the exact peak at gain 1.</summary>
        const float ClipHeadroom = 1.25f;

        ThreadSynth m_synth, m_fallback;
        GameObject m_fallbackGo;
        AudioSource m_fallbackSource;
        int m_fs, m_seed;
        ThreadAudioParams m_params;

        // footsteps
        AudioClip[][] m_clips;                     // [surface][variant]
        readonly AudioClip[][] m_metal = new AudioClip[2][];
        readonly int[] m_metalQ = new int[2];
        readonly double[] m_metalLastPlay = new double[2];
        int m_metalActive, m_renderJob = -1, m_renderQ;
        FootstepRenderer m_renderer;
        float[] m_metalScratch;
        AudioSource[] m_voices;
        double[] m_voiceStart;
        FootstepSequencer m_seq;
        bool m_externalSteps, m_haveEye, m_autoEngaged;
        Vector3 m_lastEye;
        double m_still, m_walkedNoExternal;

        // clocks
        double m_introStart = -1.0, m_endingStart = -1.0, m_pause, m_heartbeatT0;
        bool m_pauseHook, m_heartbeatChecked, m_silentLogged, m_errorLogged;
        int m_nanLogged, m_heartbeatFrames, m_heartbeatC0;

        // dump
        AudioCapture m_capture;
        string m_dumpPath;
        double m_dumpS0;
        bool m_dumpRequested, m_dumpDone;

        public ThreadSynth Synth { get { return m_fallback != null ? m_fallback : m_synth; } }
        public ThreadAudioParams CurrentParams { get { return m_params; } }
        public int SampleRate { get { return m_fs; } }

        void Awake()
        {
            Instance = this;
            ThreadServices.Audio = this;
            SineTable.Warm();
            if (ThreadAudioParams.BreathReference <= 0.0) Debug.LogWarning("[THREAD] audio: breath reference is zero");   // one-time table, computed here, never on a later frame
            m_fs = AudioSettings.outputSampleRate;
            if (m_fs <= 0) m_fs = (int)ThreadConstants.AudioSampleRate;
            m_seed = CliArgs.Seed;
            m_seq = FootstepSequencer.Create(m_seed);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            m_synth = GetComponent<ThreadSynth>();
            if (m_synth == null) m_synth = gameObject.AddComponent<ThreadSynth>();
            m_synth.Init(m_fs, m_seed, false);
            double dspMs = sw.Elapsed.TotalMilliseconds;
            BuildFootsteps();
            AudioSettings.GetDSPBufferSize(out int bufLen, out int bufCount);
            Debug.Log("[THREAD] audio: " + m_fs + " Hz, DSP buffer " + bufLen + " x " + bufCount + ", " + AudioSettings.speakerMode
                      + ", synth built in " + dspMs.ToString("F1", CultureInfo.InvariantCulture) + " ms, footsteps in "
                      + (sw.Elapsed.TotalMilliseconds - dspMs).ToString("F1", CultureInfo.InvariantCulture) + " ms");
            if (GetComponent<AudioListener>() == null)
                Debug.LogWarning("[THREAD] audio: AudioDirector is not on the AudioListener GameObject; using the generator fallback");

            m_dumpPath = CliArgs.AudioDump;
            if (!string.IsNullOrEmpty(m_dumpPath)) { m_dumpRequested = true; Application.runInBackground = true; }

            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            GameBootstrap.WorldReady += OnWorldReady;
        }

        void Start()
        {
            if (GameBootstrap.Ready) OnWorldReady();
            if (GetComponent<AudioListener>() == null) EnableFallback("no AudioListener on this GameObject");
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            GameBootstrap.WorldReady -= OnWorldReady;
            if (ReferenceEquals(ThreadServices.Audio, this)) ThreadServices.Audio = null;
            if (Instance == this) Instance = null;
            DestroyClips(m_clips);
            DestroyClips(m_metal);
        }

        static void DestroyClips(AudioClip[][] sets)
        {
            if (sets == null) return;
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] == null) continue;
                for (int j = 0; j < sets[i].Length; j++) if (sets[i][j] != null) Destroy(sets[i][j]);
            }
        }

        void OnWorldReady()
        {
            if (m_introStart >= 0.0) return;
            double now = Time.unscaledTimeAsDouble;
            // no title fade (or capture runs): the score starts fully in
            bool skipIntro = CliArgs.NoFade || !string.IsNullOrEmpty(CliArgs.CaptureDir);
            m_introStart = skipIntro ? now - 30.0 : now;
        }

        // ================================================================ footsteps

        void BuildFootsteps()
        {
            var bank = new FootstepBank(m_fs, m_seed, 0);
            m_clips = new AudioClip[FootstepRules.SurfaceCount][];
            for (int s = 0; s < FootstepRules.SurfaceCount; s++)
            {
                m_clips[s] = new AudioClip[AudioSpec.FootstepVariants];
                if ((FootSurface)s == FootSurface.Metal) continue;   // metal plays from the two brightness sets below
                for (int v = 0; v < AudioSpec.FootstepVariants; v++)
                {
                    float[] data = bank.Get((FootSurface)s, v);
                    for (int i = 0; i < data.Length; i++) data[i] *= ClipHeadroom;
                    var clip = AudioClip.Create("step_" + ((FootSurface)s).ToString().ToLowerInvariant() + "_" + v, data.Length, 1, m_fs, false);
                    clip.SetData(data, 0);
                    m_clips[s][v] = clip;
                }
            }
            // two metal sets (6 clips each) for the background brightness re-render
            m_renderer = new FootstepRenderer(m_fs);
            int metalFrames = FootstepRules.ClipFrames(FootSurface.Metal, m_fs);
            m_metalScratch = new float[metalFrames];
            int q0 = FootstepRules.MetalBrightIndex(ThreadMath.Darkness(CliArgs.StartS));
            for (int set = 0; set < 2; set++)
            {
                m_metal[set] = new AudioClip[AudioSpec.FootstepVariants];
                for (int v = 0; v < AudioSpec.FootstepVariants; v++)
                {
                    var clip = AudioClip.Create("step_metal_set" + set + "_" + v, metalFrames, 1, m_fs, false);
                    m_renderer.Render(FootSurface.Metal, v, m_seed, FootstepRules.MetalBrightDb(q0), m_metalScratch);
                    for (int i = 0; i < m_metalScratch.Length; i++) m_metalScratch[i] *= ClipHeadroom;
                    clip.SetData(m_metalScratch, 0);
                    m_metal[set][v] = clip;
                }
                m_metalQ[set] = q0;
                m_metalLastPlay[set] = -1e9;
            }
            m_metalActive = 0;

            var host = new GameObject("FootstepVoices");
            host.transform.SetParent(transform, false);
            m_voices = new AudioSource[AudioSpec.FootstepPool];
            m_voiceStart = new double[AudioSpec.FootstepPool];
            for (int i = 0; i < m_voices.Length; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.playOnAwake = false; src.loop = false;
                src.spatialBlend = 0f; src.dopplerLevel = 0f;
                src.bypassReverbZones = true; src.bypassListenerEffects = false; src.bypassEffects = true;
                src.priority = 64; src.volume = 1f;
                m_voices[i] = src;
                m_voiceStart[i] = -1e9;
            }
        }

        /// <summary>
        /// A footstep requested by the player (it owns the cadence and the speed-dependent gain; 1 = the 14.3.5 peak).
        /// The first call switches the director's own fallback cadence off for good.
        /// </summary>
        public void Footstep(FootSurface surface, float pan, float gain)
        {
            m_externalSteps = true;
            var e = m_seq.Next(surface);
            PlayStep(surface, e.variant, pan, gain, e.pitch);
        }

        void PlayStep(FootSurface surface, int variant, float pan, float volume, float pitch)
        {
            if (m_voices == null) return;
            double now = Time.unscaledTimeAsDouble;
            AudioClip clip;
            if (surface == FootSurface.Metal) { clip = m_metal[m_metalActive][variant]; m_metalLastPlay[m_metalActive] = now; }
            else clip = m_clips[(int)surface][variant];
            int best = 0;
            for (int i = 1; i < m_voices.Length; i++) if (m_voiceStart[i] < m_voiceStart[best]) best = i;   // steal the oldest
            var src = m_voices[best];
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume / ClipHeadroom);
            src.pitch = pitch;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.Play();
            m_voiceStart[best] = now;
        }

        void AutoCadence(IPlayerState player, double s, double dt)
        {
            if (!autoFootsteps || m_externalSteps || player == null) return;
            var cam = ThreadServices.CameraControl;
            if (player.Frozen || (cam != null && cam.PoseOverridden)) { m_haveEye = false; return; }
            Vector3 eye = player.EyePosition;
            if (!m_haveEye) { m_lastEye = eye; m_haveEye = true; return; }
            double dx = eye.x - m_lastEye.x, dz = eye.z - m_lastEye.z;
            m_lastEye = eye;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d > TeleportDistance) { m_seq.Rearm(); return; }
            if (d < 1e-4) { m_still += dt; if (m_still > StillRearm) m_seq.Rearm(); return; }
            m_still = 0.0;
            if (!m_autoEngaged)
            {
                // the player normally calls Footstep within one stride; only a silent walker hands the cadence to us
                m_walkedNoExternal += d;
                if (m_walkedNoExternal < AutoEngageDistance) return;
                m_autoEngaged = true;
                m_seq.Rearm();
                Debug.Log("[THREAD] audio: no footstep calls after " + AutoEngageDistance.ToString("F1", CultureInfo.InvariantCulture) + " m of walking; AudioDirector runs the step cadence");
            }
            if (m_seq.Advance(d, s, out StepEvent e))
                PlayStep(e.surface, e.variant, e.pan, m_params.footGain, e.pitch);
        }

        /// <summary>Re-renders the idle metal set when the quantised D changed (one variant per frame).</summary>
        void UpdateMetalBrightness(double s, double now)
        {
            if (m_renderer == null) return;
            int idle = 1 - m_metalActive;
            if (m_renderJob < 0)
            {
                int q = FootstepRules.MetalBrightIndex(ThreadMath.Darkness(s));
                if (q != m_metalQ[m_metalActive] && now - m_metalLastPlay[idle] > MetalIdleBeforeRender)
                {
                    if (q == m_metalQ[idle]) { m_metalActive = idle; return; }   // already rendered
                    m_renderJob = 0; m_renderQ = q;
                }
                return;
            }
            m_renderer.Render(FootSurface.Metal, m_renderJob, m_seed, FootstepRules.MetalBrightDb(m_renderQ), m_metalScratch);
            float[] sc = m_metalScratch;
            for (int i = 0; i < sc.Length; i++) sc[i] *= ClipHeadroom;
            m_metal[idle][m_renderJob].SetData(sc, 0);
            if (++m_renderJob >= AudioSpec.FootstepVariants)
            {
                m_metalQ[idle] = m_renderQ;
                m_metalActive = idle;
                m_renderJob = -1;
            }
        }

        // ================================================================ per frame

        void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;
            double dt = Math.Min(0.1, Time.unscaledDeltaTime);
            if (m_introStart < 0.0 && GameBootstrap.Instance == null) OnWorldReady();   // scenes without a bootstrap
            var player = ThreadServices.Player;
            double s = ProgressDirector.S;
            double lateral = player != null ? player.Lateral : 0.0;
            bool brisk = player != null && player.Brisk;
            if (player != null && player.Finished && m_endingStart < 0.0) m_endingStart = now;

            bool paused = m_pauseHook || (ThreadServices.Fade != null && ThreadServices.Fade.Paused);
            double step = dt / Math.Max(0.05, pauseRampSeconds);
            m_pause = paused ? Math.Min(1.0, m_pause + step) : Math.Max(0.0, m_pause - step);
            double pauseEased = m_pause * m_pause * (3.0 - 2.0 * m_pause);

            double intro = m_introStart >= 0.0 ? now - m_introStart : 0.0;
            double ending = m_endingStart >= 0.0 ? now - m_endingStart : -1.0;
            m_params = ThreadAudioParams.ComputeFull(s, lateral, intro, brisk, ending, pauseEased, AudioLayer.AllMask);
            m_synth.Publish(in m_params);
            if (m_fallback != null) m_fallback.Publish(in m_params);

            AutoCadence(player, s, dt);
            UpdateMetalBrightness(s, now);
            Watchdog(now);
            UpdateDump(now, s);
        }

        void Watchdog(double now)
        {
            // heartbeat: measured from the first rendered frame (the world build may block the first frame for seconds)
            if (m_heartbeatFrames == 0) { m_heartbeatT0 = now; m_heartbeatC0 = m_synth.Callbacks; }
            m_heartbeatFrames++;
            if (!m_heartbeatChecked && m_heartbeatFrames >= HeartbeatFrames && now - m_heartbeatT0 > HeartbeatTimeout)
            {
                m_heartbeatChecked = true;
                if (m_synth.Callbacks == m_heartbeatC0 && m_synth.Callbacks == 0 && m_fallback == null)
                    EnableFallback("listener filter silent for " + HeartbeatTimeout.ToString("F1", CultureInfo.InvariantCulture) + " s");
            }
            if (!m_silentLogged && m_fallback != null && now - m_heartbeatT0 > 3.0 * HeartbeatTimeout && m_fallback.Callbacks == 0 && m_synth.Callbacks == 0)
            {
                m_silentLogged = true;
                Debug.Log("[THREAD] audio: no audio callbacks at all (no audio device or audio disabled); the score is silent");
            }
            var synth = Synth;
            if (!m_errorLogged)
            {
                var e = synth.TakeError();
                if (e == null && m_fallback != null) e = m_synth.TakeError();
                if (e != null) { m_errorLogged = true; Debug.LogError("[THREAD-FAIL] audio thread exception: " + e); }
            }
            int nan = synth.NanResets;
            if (nan != m_nanLogged) { m_nanLogged = nan; Debug.LogWarning("[THREAD] audio: non-finite block, DSP reset (" + nan + ")"); }
        }

        void EnableFallback(string reason)
        {
            if (m_fallback != null) return;
            Debug.Log("[THREAD] audio: " + reason + "; using the AudioSource generator fallback");
            m_fallbackGo = new GameObject("ThreadSynthSource");
            m_fallbackGo.transform.SetParent(transform, false);
            m_fallbackGo.SetActive(false);
            m_fallbackSource = m_fallbackGo.AddComponent<AudioSource>();
            m_fallbackSource.playOnAwake = true; m_fallbackSource.loop = true;
            m_fallbackSource.spatialBlend = 0f; m_fallbackSource.priority = 0;
            m_fallbackSource.bypassListenerEffects = true; m_fallbackSource.bypassReverbZones = true;
            m_fallback = m_fallbackGo.AddComponent<ThreadSynth>();
            m_fallback.Init(m_fs, m_seed, true);
            m_fallback.Publish(in m_params);
            m_fallbackGo.SetActive(true);
            m_fallbackSource.Play();
            m_synth.Bypass = true;   // never two synths: the listener filter stays inert even if it wakes up later
            var cap = m_synth.Capture;
            if (cap != null) { m_synth.Capture = null; m_fallback.Capture = cap; }
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            int fs = AudioSettings.outputSampleRate;
            if (fs <= 0) return;
            if (fs != m_fs)
            {
                m_fs = fs;
                m_synth.Rebuild(fs, m_seed, in m_params);
                if (m_fallback != null) m_fallback.Rebuild(fs, m_seed, in m_params);
                Debug.Log("[THREAD] audio: output rate changed to " + fs + " Hz, synth rebuilt");
            }
            if (m_fallbackSource != null && !m_fallbackSource.isPlaying) m_fallbackSource.Play();
        }

        // ================================================================ IAudioHooks

        public void SetPauseDuck(bool paused) { m_pauseHook = paused; }

        public void BeginEnding() { if (m_endingStart < 0.0) m_endingStart = Time.unscaledTimeAsDouble; }

        public void StartDump(string path, float seconds)
        {
            if (m_capture != null || string.IsNullOrEmpty(path)) return;
            double secs = seconds > 0f ? seconds : 10.0;
            m_dumpPath = path;
            m_dumpRequested = false;
            m_capture = new AudioCapture(secs, m_fs, 2);   // allocated once, before arming
            m_capture.Arm();
            m_dumpS0 = ProgressDirector.S;
            Synth.Capture = m_capture;
            Application.runInBackground = true;
            Debug.Log("[THREAD] audio dump: recording " + secs.ToString("F1", CultureInfo.InvariantCulture) + " s of the OnAudioFilterRead output to " + path);
        }

        void UpdateDump(double now, double s)
        {
            if (m_dumpRequested && m_capture == null && m_introStart >= 0.0 && now - m_introStart >= DumpAfterIntro)
                StartDump(m_dumpPath, (float)CliArgs.GetDouble("-audio-dump-seconds", 10.0));
            if (m_capture == null || m_dumpDone || !m_capture.IsFull) return;
            m_dumpDone = true;
            Synth.Capture = null;
            try
            {
                int ch = Math.Max(1, m_capture.Channels);
                string full = Path.GetFullPath(m_dumpPath);
                WavWriter.WritePcm16(full, m_capture.Buffer, m_capture.Count, ch, m_fs);
                var meter = new AudioMeter();
                meter.Add(m_capture.Buffer, m_capture.Count / ch, ch);
                var ci = CultureInfo.InvariantCulture;
                string json = "{\"wav\": \"" + Path.GetFileName(full).Replace("\\", "/") + "\", \"sampleRate\": " + m_fs
                            + ", \"channels\": " + ch + ", \"frames\": " + (m_capture.Count / ch)
                            + ", \"s0\": " + m_dumpS0.ToString("F3", ci) + ", \"s1\": " + s.ToString("F3", ci)
                            + ", \"rmsDbfs\": " + meter.RmsDb.ToString("F2", ci) + ", \"peakDbfs\": " + meter.PeakDb.ToString("F2", ci)
                            + ", \"dcL\": " + meter.Dc(0).ToString("E3", ci) + ", \"dcR\": " + meter.Dc(1).ToString("E3", ci)
                            + ", \"maxStep\": " + meter.MaxStep.ToString("F5", ci) + ", \"nonFinite\": " + meter.NonFinite
                            + ", \"callbacks\": " + Synth.Callbacks + ", \"generatorFallback\": " + (m_fallback != null ? "true" : "false")
                            + ", \"maxCallbackMs\": " + Synth.MaxCallbackMs.ToString("F3", ci) + "}";
                File.WriteAllText(Path.ChangeExtension(full, ".json"), json + "\n");
                bool silent = meter.RmsDb < -80.0;
                string msg = "[THREAD] audio dump written: " + full + " " + json;
                if (silent) Debug.LogWarning(msg + " (SILENT)"); else Debug.Log(msg);
            }
            catch (Exception e)
            {
                Debug.LogError("[THREAD-FAIL] audio dump failed: " + e.Message);
            }
        }
    }
}
