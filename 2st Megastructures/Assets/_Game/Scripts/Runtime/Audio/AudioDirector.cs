using System;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Procedural audio of The Ring (MASTER_PROMPT 14.3, Part 9 "Audio", 11.9). Lives on the "Audio" GameObject whose
    /// components are, in this order: AudioSource (no clip, 2D) -> AudioDirector (OnAudioFilterRead generator, the first
    /// filter) -> AudioReverbFilter (User preset). Never on the AudioListener's GameObject (research 05 F11, UUM-61733).
    /// At load it adds the post-reverb <see cref="RingMasterBus"/> to the AudioListener (second limiter, final capture).
    /// Main thread (LateUpdate): builds <see cref="AudioParams"/> from RingState / RingRig / RingEvents, publishes them
    /// through a seqlock, writes RingState.Gust (the W1 gust 0..1, the same pure function of game time the audio thread
    /// uses), blends the reverb zones over >= 2 s. Audio thread: OnAudioFilterRead renders the Unity-free
    /// <see cref="RingAudioEngine"/>. No allocations on either thread during the walk.
    /// Fades: follows UiDirector.FadeAudio (static field or property, bound once by reflection into a compiled getter)
    /// and falls back to the 14.11 timeline when it is absent. Beacon: B1 pings use the same time origin as the beat-7
    /// BeaconPulse.T0 (bound the same way; the same rule is computed here when the beacon does not tick), so every
    /// ping (t0 + 1 + 4 k) is a pulse peak.
    /// CLI: -audio-dump FILE [-audio-dump-seconds N] writes the first N s (default 8) after load of the real callback
    /// output (FILE: the generator output, comparable with the offline render; FILE_post.wav: the final mix after the
    /// reverb, captured by the master bus) plus FILE.json with the probe numbers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [DefaultExecutionOrder(800)]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        /// <summary>Game time (RingState.T) at which s first reached 905: pings and beacon pulse peaks are at t0 + 1 + 4 k
        /// (<see cref="RingAudioEngine.BeaconIntensity"/>, <see cref="RingAudioEngine.PingTime"/>, <see cref="RingAudioEngine.PingPhase"/>).
        /// NaN until reached.</summary>
        public static double BeaconT0 { get; private set; } = double.NaN;

        /// <summary>Lens intensity of the beacon for the current game time, locked to the pings (0.55..1.0).</summary>
        public static float BeaconIntensityNow => RingAudioEngine.BeaconIntensity(RingState.T, BeaconT0);

        /// <summary>Ping phase 0..1 (0 = ping and pulse peak) for the current game time; -1 before the first ping.</summary>
        public static double PingPhaseNow => RingAudioEngine.PingPhase(RingState.T, BeaconT0);

        const float DefaultDumpSeconds = 8f;

        // ---------------- engine and hand-off ----------------
        RingAudioEngine _engine;
        readonly AudioParamChannel _channel = new AudioParamChannel();
        AudioParams _main = AudioParams.Default();     // main thread only
        AudioParams _audio = AudioParams.Default();    // audio thread only
        AudioSource _src;
        AudioReverbFilter _rev;
        RingMasterBus _bus;
        volatile int _fs = 48000;
        int _callbacks, _lastChannels, _lastFrames;
        int _frame;
        uint _gustSeed;

        // ---------------- timeline ----------------
        bool _loaded;
        double _loadAt = double.NaN;
        double _endingClock = -1.0;                    // seconds since the goal (unscaled, frozen while paused)
        Func<float> _uiFade;                           // UiDirector.FadeAudio (static field or property)
        Func<double> _beaconOrigin;                    // Megastructure.Beats.BeaconPulse.T0 (static field)
        double _hatchT = -1.0;
        bool _firstUpdate = true;
        float _prevS;
        bool _sawArkCue;
        bool _pausedEvent;

        // ---------------- footsteps fallback ----------------
        double _lastStepEventT = -100.0;
        float _fallbackNextS = float.NaN;
        int _fallbackFoot;
        bool _fallbackLogged;
        float _movedSinceEvent;

        // ---------------- reverb ----------------
        ReverbSettings _revCur;
        bool _revInit;
        double _erGate;                                 // mB offset applied to reflectionsLevel while a delay change is hidden
        double _erDelayApplied = double.NaN;
        readonly float[] _revLast = new float[14];

        // ---------------- probe and dump ----------------
        double _playAt;
        bool _probeLogged;
        float[] _dump;
        int _dumpPos;                                   // audio thread writes, main reads after the ready flag
        int _dumpArmed, _dumpReady;
        bool _dumpWritten, _postWritten;
        string _dumpPath;
        float _dumpSeconds = DefaultDumpSeconds;
        double _dumpStartS, _dumpStartT;
        int _dumpCallbacksAtStart;

        // ---------------- event delegates (allocated once) ----------------
        Action _onHatch, _onGoal, _onLoad;
        Action<RingSurface, int, float> _onStep;
        Action<bool> _onPause;
        AudioSettings.AudioConfigurationChangeHandler _onConfig;

        public RingAudioEngine Engine => _engine;
        public RingMasterBus MasterBus => _bus;
        public int CallbackCount => Volatile.Read(ref _callbacks);
        public int LastChannels => Volatile.Read(ref _lastChannels);
        public int LastFrames => Volatile.Read(ref _lastFrames);
        public bool DumpWritten => _dumpWritten;

        /// <summary>Creates the "Audio" rig with the components in the required order (AudioSource, AudioDirector,
        /// AudioReverbFilter). Safe to call from the bootstrap; returns the existing director if there is one.</summary>
        public static AudioDirector CreateRig(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Audio");
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<AudioSource>();
            return go.AddComponent<AudioDirector>();     // Awake appends the AudioReverbFilter after itself
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureRig()
        {
            if (Instance != null || FindAnyObjectByType<AudioDirector>() != null) return;
            if (FindAnyObjectByType<AudioListener>() == null) return;
            CreateRig();
            Debug.Log("[Ring] Audio: no AudioDirector in the scene, created the 'Audio' rig (AudioSource -> AudioDirector -> AudioReverbFilter)");
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogWarning("[Ring] Audio: a second AudioDirector was disabled"); enabled = false; return; }
            if (GetComponent<AudioListener>() != null)
            {
                // UUM-61733: an OnAudioFilterRead component may serve either an AudioSource or the AudioListener, never both
                Debug.LogWarning("[Ring] Audio: AudioDirector must not share the AudioListener's GameObject; moving it to an 'Audio' rig");
                var src0 = GetComponent<AudioSource>(); if (src0 != null) src0.enabled = false;
                enabled = false;
                Destroy(this);
                if (Instance == null) CreateRig();
                return;
            }
            Instance = this;
            BeaconT0 = double.NaN;

            if (!Application.isBatchMode)
            {
                var cfg = AudioSettings.GetConfiguration();
                if (cfg.sampleRate != 48000)
                {
                    cfg.sampleRate = 48000;
                    bool ok = AudioSettings.Reset(cfg);
                    Debug.Log("[Ring] Audio: requested 48 kHz output (Reset " + (ok ? "applied" : "closest match") + ")");
                }
            }
            _fs = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _gustSeed = DspHash.Seed((uint)CliArgs.Seed, 0x6A09u);
            _main.GustSeed = _gustSeed;
            _engine = new RingAudioEngine(_fs, (uint)Math.Max(1, CliArgs.Seed));

            _src = GetComponent<AudioSource>();
            _src.clip = null; _src.playOnAwake = false; _src.loop = true;
            _src.spatialBlend = 0f; _src.volume = 1f; _src.panStereo = 0f; _src.dopplerLevel = 0f;
            _src.bypassReverbZones = true; _src.bypassEffects = false; _src.bypassListenerEffects = false; _src.priority = 0;

            _rev = EnsureReverbAfterMe();
            _rev.reverbPreset = AudioReverbPreset.User;     // User first, then the fields
            _revCur = ReverbZones.Zone[ReverbZones.Capsule];
            ApplyReverb(_revCur, true);

            AttachMasterBus();

            // -audio-dump: preallocated capture of the real callback output
            _dumpPath = CliArgs.AudioDump;
            if (!string.IsNullOrEmpty(_dumpPath))
            {
                _dumpSeconds = Mathf.Clamp(CliArgs.Float("audio-dump-seconds", DefaultDumpSeconds), 1f, 120f);
                _dump = new float[(int)(_dumpSeconds * Math.Max(_fs, 8000)) * 2];
                if (_bus != null) _bus.PrepareCapture(_dumpSeconds);
                Debug.Log("[Ring] Audio: dump armed for " + _dumpSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s to " + _dumpPath);
            }

            _onHatch = OnHatch; _onGoal = OnGoal; _onLoad = OnLoad; _onStep = OnStep; _onPause = OnPause; _onConfig = OnAudioConfigurationChanged;
            AudioSettings.OnAudioConfigurationChanged += _onConfig;
            _uiFade = BindStatic<float>("Megastructure.UiDirector", "FadeAudio");
            _beaconOrigin = BindStatic<double>("Megastructure.Beats.BeaconPulse", "T0");
            Debug.Log("[Ring] Audio: fade " + (_uiFade != null ? "follows UiDirector.FadeAudio" : "follows the 14.11 timeline (UiDirector.FadeAudio not found)")
                      + ", beacon origin " + (_beaconOrigin != null ? "BeaconPulse.T0" : "computed here"));

            _channel.Publish(in _main);
            _src.Play();                                     // generator detection happens here (research 05 F8)
            _playAt = Time.unscaledTimeAsDouble;
            Debug.Log("[Ring] Audio: engine started at " + _fs + " Hz, seed " + CliArgs.Seed);
        }

        AudioReverbFilter EnsureReverbAfterMe()
        {
            var comps = GetComponents<Component>();
            int me = Array.IndexOf(comps, this);
            var rev = GetComponent<AudioReverbFilter>();
            if (rev != null && Array.IndexOf(comps, rev) < me)
            {
                // component order is signal order: the reverb must follow the generator
                DestroyImmediate(rev);
                rev = null;
            }
            if (rev == null) rev = gameObject.AddComponent<AudioReverbFilter>();
            return rev;
        }

        void AttachMasterBus()
        {
            if (_bus != null) return;
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) return;
            _bus = RingMasterBus.Attach(listener);
            if (_bus != null)
            {
                _bus.SetSampleRate(_fs);
                if (_dump != null && _bus.CaptureBuffer == null) _bus.PrepareCapture(_dumpSeconds);
                Debug.Log("[Ring] Audio: post-reverb master bus on '" + listener.gameObject.name + "'");
            }
        }

        /// <summary>
        /// Binds a public static field or property of another system once (no compile-time dependency, so this module
        /// builds alone) into a compiled getter: a field read through FieldInfo.GetValue would box every frame.
        /// </summary>
        static Func<T> BindStatic<T>(string typeName, string member)
        {
            try
            {
                var t = typeof(AudioDirector).Assembly.GetType(typeName);
                if (t == null) return null;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
                Expression body = null;
                var f = t.GetField(member, flags);
                if (f != null && f.FieldType == typeof(T)) body = Expression.Field(null, f);
                else
                {
                    var p = t.GetProperty(member, flags);
                    if (p != null && p.PropertyType == typeof(T) && p.GetGetMethod() != null)
                        return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), p.GetGetMethod());
                }
                if (body == null) return null;
                return Expression.Lambda<Func<T>>(body).Compile();
            }
            catch (Exception e)
            {
                Debug.Log("[Ring] Audio: could not bind " + typeName + "." + member + " (" + e.GetType().Name + ")");
                return null;
            }
        }

        void OnEnable()
        {
            if (_engine == null) return;
            RingEvents.HatchTriggered += _onHatch; RingEvents.GoalReached += _onGoal; RingEvents.LoadComplete += _onLoad;
            RingEvents.Footstep += _onStep; RingEvents.PauseChanged += _onPause;
        }

        void OnDisable()
        {
            if (_engine == null) return;
            RingEvents.HatchTriggered -= _onHatch; RingEvents.GoalReached -= _onGoal; RingEvents.LoadComplete -= _onLoad;
            RingEvents.Footstep -= _onStep; RingEvents.PauseChanged -= _onPause;
        }

        void OnDestroy()
        {
            if (_onConfig != null) AudioSettings.OnAudioConfigurationChanged -= _onConfig;
            if (Instance == this) Instance = null;
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _fs = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            if (_bus != null) _bus.SetSampleRate(_fs);
            if (_src != null) _src.Play();                    // a reset or device change stops sources; the engine re-rates in Render
            Debug.Log("[Ring] Audio: configuration changed (device " + deviceWasChanged + "), " + _fs + " Hz, source re-played");
        }

        // ---------------- events (main thread) ----------------
        void OnHatch() { if (_hatchT < 0.0) _hatchT = RingState.T; }
        void OnGoal() { if (_endingClock < 0.0) _endingClock = 0.0; }
        void OnLoad() { MarkLoaded(); }
        void OnPause(bool p) { _pausedEvent = p; }

        void MarkLoaded()
        {
            if (_loaded) return;
            _loaded = true; _loadAt = Time.unscaledTimeAsDouble;
            AttachMasterBus();
            if (_dump != null) Volatile.Write(ref _dumpArmed, 1);
            if (_bus != null) _bus.ArmCapture();
        }

        void OnStep(RingSurface surface, int foot, float speed)
        {
            _lastStepEventT = RingState.T;
            _movedSinceEvent = 0f;
            Enqueue((int)surface, foot, speed);
        }

        void Enqueue(int surface, int foot, float speed)
        {
            // stamped 45 ms ahead (two DSP buffers) so the audio thread starts the step sample-accurately
            var e = new AudioEvent { Kind = AudioEventKind.Footstep, Surface = surface, Foot = foot, Speed = speed, T = RingState.T + 0.045 };
            _engine.Events.TryEnqueue(e);
        }

        // ---------------- main-thread update ----------------
        void LateUpdate()
        {
            if (_engine == null) return;
            double now = Time.unscaledTimeAsDouble;
            float dtu = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!_loaded && (RingRig.WorldReady || now - _playAt > 30.0)) MarkLoaded();
            float s = RingState.S;
            bool paused = RingState.Paused || _pausedEvent;

            bool first = _firstUpdate;
            if (_firstUpdate)
            {
                _firstUpdate = false; _prevS = s;
                if (s > AudioSpec.HatchTriggerS + 2.5f) _hatchT = -1.0;                 // started beyond the hatch: progress branch only
            }
            if (_hatchT < 0.0 && s >= AudioSpec.HatchTriggerS && _prevS < AudioSpec.HatchTriggerS) _hatchT = RingState.T;   // fallback if no event
            UpdateBeaconOrigin(s, first);
            if (_endingClock >= 0.0 && !paused) _endingClock += dtu;
            if (RingState.GoalReached && _endingClock < 0.0) _endingClock = 0.0;

            // fades: UiDirector.FadeAudio when it exists (it already contains the ending fade), otherwise the 14.11 timeline
            float picture, ending;
            if (_uiFade != null) { picture = Mathf.Clamp01(_uiFade()); ending = 1f; }
            else
            {
                if (!_loaded) picture = 0f;
                else if (CliArgs.NoFade || RingState.Capture) picture = 1f;
                else picture = (float)DspMath.Smoothstep01((now - _loadAt - AudioSpec.FadeInStart) / (AudioSpec.FadeInEnd - AudioSpec.FadeInStart));
                ending = _endingClock < 0.0 ? 1f : (float)(1.0 - DspMath.Smootherstep01((_endingClock - AudioSpec.EndDwell) / AudioSpec.EndFade));
            }

            // listener
            var cam = RingRig.Camera != null ? RingRig.Camera : Camera.main;
            Vector3 lp = cam != null ? cam.transform.position : new Vector3(0f, (float)AudioSpec.Eye, s);
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;

            _main.Frame = ++_frame;
            _main.S = s;
            _main.T = RingState.T;
            Vector3 v = RingRig.PlayerVelocity; v.y = 0f;
            _main.Speed = paused ? 0f : v.magnitude;
            _main.Beat = RingState.Beat;
            _main.SunsetU = RingState.SunsetU;
            _main.GustSeed = _gustSeed;
            _main.PauseDuck = paused ? 1f : 0f;
            _main.PictureFade = picture;
            _main.EndingFade = ending;
            _main.HatchT = _hatchT;
            _main.BeaconT0 = double.IsNaN(BeaconT0) ? -1.0 : BeaconT0;
            if (RingState.ArkCue > 0f) _sawArkCue = true;
            _main.ArkCue = _sawArkCue ? RingState.ArkCue : (float)AudioWalkSim.ArkCueAt(s);
            if (s >= AudioSpec.ArkTriggerS)
            {
                double xc = AudioSpec.ArkX0 + (AudioSpec.ArkX1 - AudioSpec.ArkX0) * Mathf.Clamp01(RingState.ArkU);
                AudioWalkSim.BentRangeAz(lp.x, lp.y, lp.z, yaw, xc, AudioSpec.ArkAltitude, AudioSpec.ArkTrackZ, out double rg, out double az);
                _main.ArkRange = (float)rg; _main.ArkAzimuth = (float)az;
            }
            else { _main.ArkRange = -1f; _main.ArkAzimuth = 0f; }
            RiverNearest(lp.x, lp.z, yaw, out float cd, out float raz);
            _main.RiverCentreDist = cd; _main.RiverDist = Mathf.Max(0f, cd - (float)AudioSpec.RiverHalfWidth); _main.RiverAzimuth = raz;
            _channel.Publish(in _main);

            // W1 gust published for the grass (_Gust): the same pure function of game time the engine uses, so it is
            // deterministic in capture mode and works without an audio device (the engine's own value: Engine.Gust01)
            RingState.Gust = RingAudioEngine.GustAt(_gustSeed, RingState.T);

            FootstepFallback(s, v.magnitude, paused);
            UpdateReverb(s, dtu);
            Probe(now);
            WriteDumpIfReady();
            _prevS = s;
        }

        /// <summary>Beacon origin (14.5.7 / 14.3 B1): the beat-7 BeaconPulse.T0 when it is set, otherwise the same rule
        /// computed here: a walk that starts beyond s 905 back-dates it at walking speed on the first update, else it is
        /// the game time of the first update with s >= 905; never reset during the walk.</summary>
        void UpdateBeaconOrigin(float s, bool firstUpdate)
        {
            double ext = _beaconOrigin != null ? _beaconOrigin() : double.NaN;
            if (!double.IsNaN(ext)) { BeaconT0 = ext; return; }
            if (!double.IsNaN(BeaconT0)) return;
            if (firstUpdate && s > AudioSpec.BeaconFirstPingS) BeaconT0 = RingState.T - (s - AudioSpec.BeaconFirstPingS) / AudioSpec.Walk;
            else if (s >= AudioSpec.BeaconFirstPingS) BeaconT0 = RingState.T;
        }

        void RiverNearest(float x, float z, float yawDeg, out float centreDist, out float azimuth)
        {
            var r = RingWorld.River;
            float best = float.MaxValue, bx = 0f, bz = 0f;
            for (int i = 0; i < r.Length - 1; i++)
            {
                float ax = r[i].x, az = r[i].y, ex = r[i + 1].x - ax, ez = r[i + 1].y - az;
                float t = Mathf.Clamp01(((x - ax) * ex + (z - az) * ez) / Mathf.Max(1e-6f, ex * ex + ez * ez));
                float qx = ax + ex * t, qz = az + ez * t, d = (qx - x) * (qx - x) + (qz - z) * (qz - z);
                if (d < best) { best = d; bx = qx; bz = qz; }
            }
            centreDist = Mathf.Sqrt(best);
            azimuth = (float)AudioWalkSim.WrapDeg(Mathf.Atan2(bx - x, bz - z) * Mathf.Rad2Deg - yawDeg);
        }

        /// <summary>If the PlayerController never raises RingEvents.Footstep, synthesise steps from progress (one per
        /// stride, alternate feet) so the walk is never silent underfoot.</summary>
        void FootstepFallback(float s, float speed, bool paused)
        {
            float ds = Mathf.Abs(s - _prevS);
            if (ds < 2f) _movedSinceEvent += ds;
            bool needFallback = _movedSinceEvent > 3f && RingState.T - _lastStepEventT > 2.0;
            if (!needFallback || paused || speed < 0.2f) { _fallbackNextS = float.NaN; return; }
            if (!_fallbackLogged) { _fallbackLogged = true; Debug.Log("[Ring] Audio: no Footstep events, synthesising steps from progress"); }
            float stride = Mathf.Lerp((float)AudioSpec.StrideWalk, (float)AudioSpec.StrideBrisk, Mathf.InverseLerp((float)AudioSpec.Walk, (float)AudioSpec.Brisk, speed));
            if (float.IsNaN(_fallbackNextS) || Mathf.Abs(_fallbackNextS - s) > 3f) _fallbackNextS = s + stride * 0.5f;
            if (s >= _fallbackNextS)
            {
                Enqueue(AudioSpec.SurfaceAt(s, false), _fallbackFoot, speed);
                _fallbackFoot ^= 1;
                _fallbackNextS += stride;
            }
        }

        // ---------------- reverb (blended over >= 2 s, delay changes hidden behind an ER fade) ----------------
        void UpdateReverb(float s, float dt)
        {
            double hatchE = _hatchT >= 0.0 ? RingState.T - _hatchT : (s > AudioSpec.HatchTriggerS + 2.5f ? 1000.0 : double.NegativeInfinity);
            var target = ReverbZones.Target(s, hatchE);
            bool jump = Mathf.Abs(s - _prevS) > 10f;
            if (!_revInit || jump || RingState.Capture)
            {
                _revCur = target; _revInit = true; _erDelayApplied = target.ReflectionsDelay; _erGate = 0.0;
            }
            else
            {
                double a = 1.0 - Math.Exp(-dt / ReverbZones.FollowTau);   // 95 % in 2.1 s on top of the s windows
                _revCur = ReverbSettings.Lerp(_revCur, target, a);
                _revCur.ReflectionsDelay = target.ReflectionsDelay;
                // a reflection-delay change happens only while the early reflections are faded below -6000 mB
                double rate = 16000.0 * dt;                              // 8000 mB in 0.5 s
                if (Math.Abs(target.ReflectionsDelay - _erDelayApplied) > 0.0005)
                {
                    _erGate = Math.Max(-8000.0, _erGate - rate);
                    if (_revCur.ReflectionsLevel + _erGate <= -6000.0 || _erGate <= -7999.0) _erDelayApplied = target.ReflectionsDelay;
                }
                else _erGate = Math.Min(0.0, _erGate + rate);
            }
            var applied = _revCur;
            applied.ReflectionsDelay = _erDelayApplied;
            applied.ReflectionsLevel = Math.Max(-10000.0, _revCur.ReflectionsLevel + _erGate);
            ApplyReverb(applied, false);
        }

        void ApplyReverb(in ReverbSettings p, bool force)
        {
            if (_rev == null) return;
            Set(0, (float)DspMath.Clamp(p.DryLevel, -10000, 0), force);
            Set(1, (float)DspMath.Clamp(p.Room, -10000, 0), force);
            Set(2, (float)DspMath.Clamp(p.RoomHF, -10000, 0), force);
            Set(3, (float)DspMath.Clamp(p.RoomLF, -10000, 0), force);
            Set(4, (float)DspMath.Clamp(p.DecayTime, 0.1, 20), force);
            Set(5, (float)DspMath.Clamp(p.DecayHFRatio, 0.1, 2), force);
            Set(6, (float)DspMath.Clamp(p.ReflectionsLevel, -10000, 1000), force);
            Set(7, (float)DspMath.Clamp(p.ReflectionsDelay, 0, 0.3), force);
            Set(8, (float)DspMath.Clamp(p.ReverbLevel, -10000, 2000), force);
            Set(9, (float)DspMath.Clamp(p.ReverbDelay, 0, 0.1), force);
            Set(10, (float)DspMath.Clamp(p.HfReference, 1000, 20000), force);
            Set(11, (float)DspMath.Clamp(p.LfReference, 20, 1000), force);
            Set(12, (float)DspMath.Clamp(p.Diffusion, 0, 100), force);
            Set(13, (float)DspMath.Clamp(p.Density, 0, 100), force);
        }

        // one switch instead of setter delegates: no allocation per frame, and a native call only when a value changed
        void Set(int i, float v, bool force)
        {
            float last = _revLast[i];
            if (!force && Mathf.Abs(v - last) <= 1e-3f * Mathf.Max(1f, Mathf.Abs(last))) return;
            _revLast[i] = v;
            switch (i)
            {
                case 0: _rev.dryLevel = v; break;
                case 1: _rev.room = v; break;
                case 2: _rev.roomHF = v; break;
                case 3: _rev.roomLF = v; break;
                case 4: _rev.decayTime = v; break;
                case 5: _rev.decayHFRatio = v; break;
                case 6: _rev.reflectionsLevel = v; break;
                case 7: _rev.reflectionsDelay = v; break;
                case 8: _rev.reverbLevel = v; break;
                case 9: _rev.reverbDelay = v; break;
                case 10: _rev.hfReference = v; break;
                case 11: _rev.lfReference = v; break;
                case 12: _rev.diffusion = v; break;
                default: _rev.density = v; break;
            }
        }

        // ---------------- probe and dump ----------------
        void Probe(double now)
        {
            if (_probeLogged || now - _playAt < 2.0) return;
            _probeLogged = true;
            var cfg = AudioSettings.GetConfiguration();
            AudioSettings.GetDSPBufferSize(out int len, out int num);
            string msg = string.Format(CultureInfo.InvariantCulture,
                "[Ring] Audio probe: callbacks {0}, channels {1}, frames/callback {2}, master bus callbacks {3}, outputSampleRate {4}, config {5} Hz / {6} / {7}, DSP buffer {8} x {9}, dspTime {10:0.00}",
                CallbackCount, LastChannels, LastFrames, _bus != null ? _bus.CallbackCount : -1, AudioSettings.outputSampleRate,
                cfg.sampleRate, cfg.dspBufferSize, cfg.speakerMode, len, num, AudioSettings.dspTime);
            if (CallbackCount > 0 || Application.isBatchMode) Debug.Log(msg);
            else Debug.LogWarning(msg + " (OnAudioFilterRead not running: no audio device?)");
        }

        void WriteDumpIfReady()
        {
            if (_dump == null) return;
            if (!_dumpWritten && Volatile.Read(ref _dumpReady) == 1)
            {
                _dumpWritten = true;
                try
                {
                    int frames = Volatile.Read(ref _dumpPos) / 2;
                    string path = Path.GetFullPath(_dumpPath);
                    DspWavWriter.WriteFile(path, _dump, frames, 2, _fs, 24);
                    var sb = new StringBuilder();
                    sb.Append("{\n");
                    sb.AppendFormat(CultureInfo.InvariantCulture, "  \"sampleRate\": {0},\n  \"frames\": {1},\n  \"seconds\": {2:0.000},\n  \"callbackChannels\": {3},\n  \"framesPerCallback\": {4},\n", _fs, frames, frames / (double)_fs, LastChannels, LastFrames);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "  \"callbacksAtStart\": {0},\n  \"callbacksAtEnd\": {1},\n  \"sStart\": {2:0.000},\n  \"tStart\": {3:0.000},\n  \"sEnd\": {4:0.000},\n  \"tEnd\": {5:0.000},\n", _dumpCallbacksAtStart, CallbackCount, _dumpStartS, _dumpStartT, RingState.S, RingState.T);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "  \"masterBusCallbacks\": {0},\n  \"masterBusLimiterMinGain\": {1:0.0000},\n  \"engineLimiterMinGain\": {2:0.0000},\n", _bus != null ? _bus.CallbackCount : -1, _bus != null ? _bus.LimiterMinGain : 1.0, _engine.LimiterMinGain);
                    sb.Append("  \"note\": \"FILE = output of AudioDirector.OnAudioFilterRead (the engine render, before the AudioReverbFilter; comparable with the offline render); FILE_post.wav = final mix after the reverb, captured by RingMasterBus on the AudioListener\"\n}\n");
                    File.WriteAllText(path + ".json", sb.ToString());
                    Debug.Log("[Ring] Audio dump written: " + path + " (" + frames + " frames at " + _fs + " Hz, " + CallbackCount + " callbacks)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ring] Audio dump failed: " + e.Message);
                }
            }
            if (!_postWritten && _bus != null && _bus.CaptureReady)
            {
                _postWritten = true;
                try
                {
                    string path = Path.GetFullPath(_dumpPath);
                    string post = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "_post.wav");
                    DspWavWriter.WriteFile(post, _bus.CaptureBuffer, _bus.CaptureFrames, 2, _bus.SampleRate, 24);
                    Debug.Log("[Ring] Audio post-reverb dump written: " + post + " (" + _bus.CaptureFrames + " frames, " + _bus.CallbackCount + " bus callbacks)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ring] Audio post-reverb dump failed: " + e.Message);
                }
            }
        }

        // ---------------- audio thread ----------------
        void OnAudioFilterRead(float[] data, int channels)
        {
            var eng = _engine;
            if (eng == null || channels <= 0) { Array.Clear(data, 0, data.Length); return; }
            _channel.TryRead(ref _audio);                    // on a torn read keep the previous snapshot
            eng.Render(data, channels, _fs, in _audio);
            Volatile.Write(ref _lastChannels, channels);
            Volatile.Write(ref _lastFrames, data.Length / channels);
            Interlocked.Increment(ref _callbacks);

            var dump = _dump;
            if (dump != null && Volatile.Read(ref _dumpArmed) == 1 && Volatile.Read(ref _dumpReady) == 0)
            {
                int pos = _dumpPos;
                if (pos == 0) { _dumpStartS = _audio.S; _dumpStartT = _audio.T; _dumpCallbacksAtStart = _callbacks; }
                int frames = data.Length / channels;
                for (int f = 0; f < frames && pos + 1 < dump.Length; f++)
                {
                    float l = data[f * channels];
                    float r = channels > 1 ? data[f * channels + 1] : l;
                    dump[pos++] = l; dump[pos++] = r;
                }
                Volatile.Write(ref _dumpPos, pos);
                if (pos + 1 >= dump.Length) Volatile.Write(ref _dumpReady, 1);
            }
        }
    }
}
