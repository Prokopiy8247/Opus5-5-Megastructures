using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Megastructure.Audio;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Megastructure
{
    /// <summary>
    /// Main-thread side of the procedural score (14.3). Each frame (execution order 20, after ProgressDirector) it reads
    /// GameState / SceneClock, evaluates the score (<see cref="AbyssScore.Evaluate"/>: per-beat layer targets with the
    /// 14.6 audio transitions, keyed on d or ride seconds) and publishes the snapshot to the engine.
    ///
    /// Audio thread: <see cref="OnAudioFilterRead"/> on this GameObject's AudioSource, which plays a silent looping
    /// stereo dummy clip so the filter always runs. Threading rules (Unity manual, MonoBehaviour.OnAudioFilterRead): the
    /// callback runs on the audio thread, roughly every DSP buffer (about 20 ms), receives interleaved floats with the
    /// output channel count, and must not call Unity APIs; it only touches the engine, preallocated arrays and plain
    /// fields. The source ignores AudioListener.pause, so pausing never cuts the sound: the engine ducks it smoothly.
    ///
    /// Command line (parsed here): <c>-render-audio &lt;wav&gt;</c> renders the whole nominal walk plus the ending to a
    /// 48 kHz float WAV before play starts (blocking, logged), then the game continues; <c>-audio-dump &lt;wav&gt; &lt;seconds&gt;</c>
    /// records the real callback output for N seconds and writes it from the main thread when full.
    /// </summary>
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioDirector : MonoBehaviour
    {
        public const int PreferredSampleRate = 48000;
        const float H7RampSeconds = 2f;

        public static AudioDirector Instance { get; private set; }

        AudioSource _source;
        AudioClip _dummy;
        ProceduralAudioEngine _engine;          // read by the audio thread through Volatile.Read
        AudioParams _params;
        ScoreInput _input;
        float _dwell = -1f;
        float _h7;
        int _rate;
        double _prevSceneTime = double.NaN;

        // diagnostics written by the audio thread
        int _callbacks, _lastFrames, _lastChannels;

        // -audio-dump
        string _dumpPath;
        float _dumpSeconds;
        float[] _dumpBuf;
        int _dumpFrames;                        // frames captured (audio thread writes, main thread reads)
        int _dumpFull;                          // 1 when the buffer is full
        bool _dumpSaved;
        double _dumpStartRealTime = -1.0;

        // -render-audio
        string _renderPath;

        /// <summary>Output sample rate the engine renders at (48 kHz unless the device refused it).</summary>
        public int OutputSampleRate => _rate;
        /// <summary>Number of OnAudioFilterRead callbacks so far (proves the callback runs).</summary>
        public int CallbackCount => Volatile.Read(ref _callbacks);
        public int LastCallbackFrames => _lastFrames;
        public int LastCallbackChannels => _lastChannels;
        public ProceduralAudioEngine Engine => Volatile.Read(ref _engine);
        /// <summary>True once an -audio-dump file has been written.</summary>
        public bool DumpSaved => _dumpSaved;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            ParseCommandLine();
            RequestSampleRate();
            _source = GetComponent<AudioSource>();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(_renderPath)) RenderOffline(_renderPath);

            _rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : PreferredSampleRate;
            var engine = new ProceduralAudioEngine(_rate, GameState.Seed) { DrainAudioEvents = true };
            PublishTo(engine);
            Volatile.Write(ref _engine, engine);

            if (!string.IsNullOrEmpty(_dumpPath))
            {
                int frames = Mathf.Max(1, Mathf.RoundToInt(_dumpSeconds * _rate));
                _dumpBuf = new float[frames * 2];
            }

            SetupSource();
            AudioSettings.GetDSPBufferSize(out int bufLen, out int bufCount);
            Debug.Log("[ABYSS] audio: " + _rate + " Hz, DSP buffer " + bufLen + " x " + bufCount + ", speaker mode " +
                      AudioSettings.speakerMode + ", seed " + GameState.Seed);
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            Volatile.Write(ref _engine, null);
            if (Instance == this) Instance = null;
            if (_dummy != null) Destroy(_dummy);
        }

        // ------------------------------------------------------------------ main thread

        void Update()
        {
            ProceduralAudioEngine engine = _engine;
            if (engine != null) PublishTo(engine);
            if (_dumpBuf != null) ServiceDump();
        }

        void PublishTo(ProceduralAudioEngine engine)
        {
            float dt = Time.unscaledDeltaTime;
            if (GameState.Lift == LiftPhase.Dwell) _dwell = _dwell < 0f ? 0f : _dwell + dt;
            else _dwell = -1f;
            _h7 = Mathf.MoveTowards(_h7, GameState.HoldH7 ? 1f : 0f, dt / H7RampSeconds);

            _input.D = GameState.D;
            _input.RideSeconds = GameState.RideSeconds;
            _input.RideStarted = GameState.RideStarted;
            _input.DwellSeconds = _dwell;
            // the scene clock may or may not stop while paused (ClockDriver option): extrapolate only if it really advances
            double st = SceneClock.Time;
            bool running = double.IsNaN(_prevSceneTime) ? !SceneClock.Frozen && !GameState.Paused : st > _prevSceneTime;
            _prevSceneTime = st;
            _input.SceneTime = st;
            _input.ClockRunning = running;
            _input.RealTime = SceneClock.RealTime;
            // RealTime also runs during the black load; it is the walk's real time (title timeline, door thud at 0.8 s)
            // only once ClockDriver.Begin has started the walk clock (GameBootstrap holds it while loading)
            _input.WalkStarted = ClockDriver.Running;
            _input.PictureFade = GameState.PictureFade;
            _input.Paused = GameState.Paused;
            _input.GoalReached = GameState.GoalReached;
            _input.GoalRealTime = GameState.GoalRealTime;
            _input.HoldH7 = _h7;
            _input.DisableTrim = false;

            AbyssScore.Evaluate(in _input, ref _params);
            _params.Version++;
            engine.Exchange.Publish(in _params);
        }

        void SetupSource()
        {
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            if (_dummy == null)
            {
                int len = Mathf.Max(256, _rate);
                _dummy = AudioClip.Create("AbyssSilence", len, 2, _rate, false);
                _dummy.SetData(new float[len * 2], 0);
            }
            _source.clip = _dummy;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 1f;
            _source.pitch = 1f;
            _source.panStereo = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = 0;
            _source.bypassReverbZones = true;
            _source.ignoreListenerPause = true;
            if (!_source.isPlaying) _source.Play();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : PreferredSampleRate;
            ProceduralAudioEngine current = _engine;
            if (current == null) return; // Start has not run yet; it reads the rate itself
            if (rate != current.SampleRate)
            {
                var engine = new ProceduralAudioEngine(rate, GameState.Seed) { DrainAudioEvents = true };
                PublishTo(engine);
                _rate = rate;
                Volatile.Write(ref _engine, engine);
                Debug.Log("[ABYSS] audio: output reconfigured to " + rate + " Hz (device changed: " + deviceWasChanged + ")");
            }
            if (_source != null && !_source.isPlaying) _source.Play();
        }

        static void RequestSampleRate()
        {
            AudioConfiguration cfg = AudioSettings.GetConfiguration();
            if (cfg.sampleRate == PreferredSampleRate) return;
            cfg.sampleRate = PreferredSampleRate;
            if (!AudioSettings.Reset(cfg))
                Debug.Log("[ABYSS] audio: the device refused " + PreferredSampleRate + " Hz; rendering at " + AudioSettings.outputSampleRate + " Hz");
        }

        // ------------------------------------------------------------------ audio thread

        void OnAudioFilterRead(float[] data, int channels)
        {
            ProceduralAudioEngine engine = Volatile.Read(ref _engine);
            if (engine == null) { Array.Clear(data, 0, data.Length); return; }
            engine.RenderInterleaved(data, channels);
            _lastFrames = channels > 0 ? data.Length / channels : 0;
            _lastChannels = channels;
            Volatile.Write(ref _callbacks, _callbacks + 1);

            float[] dump = _dumpBuf;
            if (dump != null && _dumpFull == 0 && channels > 0)
            {
                int cap = dump.Length / 2;
                int at = _dumpFrames;
                int frames = data.Length / channels;
                for (int i = 0; i < frames && at < cap; i++, at++)
                {
                    float l = data[i * channels];
                    float r = channels > 1 ? data[i * channels + 1] : l;
                    dump[2 * at] = l;
                    dump[2 * at + 1] = r;
                }
                Volatile.Write(ref _dumpFrames, at);
                if (at >= cap) Volatile.Write(ref _dumpFull, 1);
            }
        }

        // ------------------------------------------------------------------ QA: dump and offline render

        void ServiceDump()
        {
            if (_dumpStartRealTime < 0.0 && Volatile.Read(ref _dumpFrames) > 0) _dumpStartRealTime = SceneClock.RealTime;
            if (_dumpSaved || Volatile.Read(ref _dumpFull) == 0) return;
            _dumpSaved = true;
            try
            {
                WavWriter.WriteFloat32(_dumpPath, _dumpBuf, 2, _rate);
                AudioAnalysis.Report rep = AudioAnalysis.Analyze(_dumpBuf, _rate);
                Debug.Log("[ABYSS] audio dump: " + _dumpPath + " (" + _dumpSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s of callback output at " +
                          _rate + " Hz, first callback near real time " + _dumpStartRealTime.ToString("F2", CultureInfo.InvariantCulture) +
                          " s, " + CallbackCount + " callbacks of " + _lastFrames + " frames x " + _lastChannels + " ch) peak " +
                          rep.PeakDb.ToString("F2", CultureInfo.InvariantCulture) + " dBFS, true peak " + rep.TruePeakDb.ToString("F2", CultureInfo.InvariantCulture) +
                          " dBFS, RMS " + rep.RmsDb.ToString("F2", CultureInfo.InvariantCulture) + " dBFS, max step " + rep.MaxStep.ToString("F4", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Debug.LogError("[ABYSS] audio dump failed: " + _dumpPath + ": " + ex.Message);
            }
        }

        static void RenderOffline(string path)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                ProceduralAudioEngine eng = AbyssScore.RenderWalkToWav(path, GameState.Seed);
                Debug.Log("[ABYSS] audio render: " + path + " (" + AbyssScore.WalkEndTime.ToString("F1", CultureInfo.InvariantCulture) +
                          " s at " + AbyssScore.SampleRate + " Hz, seed " + GameState.Seed + ") in " + sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) +
                          " s; peak " + DspMath.LinToDb(eng.PeakAbs).ToString("F2", CultureInfo.InvariantCulture) + " dBFS; events: pulses " + eng.PulsesFired +
                          ", ticks " + eng.TicksFired + ", groans " + eng.GroansFired + ", bells " + eng.BellsFired + ", footsteps " + eng.FootstepsPlayed +
                          ", thud " + eng.ThudsFired);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ABYSS] audio render failed: " + path + ": " + ex.Message);
            }
        }

        void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-render-audio" && i + 1 < args.Length) _renderPath = args[++i];
                else if (args[i] == "-audio-dump" && i + 1 < args.Length)
                {
                    _dumpPath = args[++i];
                    _dumpSeconds = 10f;
                    if (i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sec))
                    {
                        _dumpSeconds = Mathf.Clamp(sec, 0.1f, 600f);
                        i++;
                    }
                }
            }
        }
    }
}
