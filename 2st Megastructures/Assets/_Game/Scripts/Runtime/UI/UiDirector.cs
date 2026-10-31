using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Megastructure
{
    /// <summary>Which font the UI text uses (14.11, research 06 section 1.5).</summary>
    public enum UiFontMode
    {
        /// <summary>The PanelSettings theme's default font (unity-theme://default, probably Inter).</summary>
        Theme = 0,
        /// <summary>Explicit FontDefinition.FromFont(RingAssets.font, else LegacyRuntime.ttf). The default: the safest choice in the player.</summary>
        Legacy = 1,
    }

    /// <summary>
    /// Title card, controls hint, picture fade, pause overlay and ending (Part 9 Start / End / Pause / Cursor, 14.11, Part 6.5).
    /// Lives on [Game] next to a UIDocument that stays enabled (research 06 1.1: enabling or disabling a UIDocument wipes a code-built
    /// tree). The whole visual tree is built in code in OnEnable (UIDocument runs at order -100, so its root exists), so the very first
    /// rendered frame is already black. Elements are cached; afterwards only style values change (no per-frame allocations). Elements are
    /// hidden with opacity + visibility, never by disabling the document.
    ///
    /// Contract for other systems:
    /// <list type="bullet">
    /// <item><see cref="FadePicture"/> / <see cref="FadeAudio"/> (static float properties, 0 = black / silent, 1 = full). The AudioDirector binds
    /// FadeAudio by reflection as a static property and then takes the whole intro and ending envelope from it. FadePicture is display-referred
    /// (the shown sRGB level scales by it); <see cref="PictureMul"/> is the matching linear-light multiplier (FadePicture^2.2 x the pause dim).</item>
    /// <item>Shader fade (Part 10 #5, 14.7 H5): the black overlay is drawn after the final dither pass. If the final pass ever multiplies its colour by
    /// PictureMul before dithering, set <see cref="ShaderFade"/> = true: the overlay then stays opaque only until LoadComplete (frame-0 black) and
    /// the pause dim moves into the shader too.</item>
    /// <item>Font: <see cref="FontMode"/> (default Legacy = LegacyRuntime.ttf through FontDefinition.FromFont; -uifont theme|legacy; <see cref="SetFontMode"/>
    /// at runtime). After every font change the title is measured once the style pass has run; a blank measure switches to the other mode once;
    /// both blank is a Debug.LogError. A successful check also measures every other string once so the dynamic glyph atlas fills while the
    /// screen is still black.</item>
    /// <item>Pause: <see cref="RingEvents.PauseChanged"/> is authoritative (the PlayerController owns Esc and RingState.Paused). RESUME raises
    /// RingEvents.RaisePause(false). The UiDirector toggles the pause itself on gamepad Start (<see cref="HandleGamepadStart"/>) and, only when
    /// <see cref="HandleEscape"/> is set, on Esc; both skip a frame in which a PauseChanged already arrived, so nothing toggles twice.</item>
    /// <item>Ending: on GoalReached, 3.0 s dwell, 4.0 s smootherstep fade, 1.5 s black, end line 2 + 6 + 2 s, then RingEvents.RaiseEndingComplete();
    /// the game stays on black, the pause menu still works.</item>
    /// <item>-nofade and capture mode (-capture or RingState.Capture at LoadComplete): the screen is black while loading, then the picture is
    /// visible at once (audio ramps over 50 ms), no title, no hint. Overlay UI is never in a camera RenderTexture, so for KS00 (title at 3.0 s):
    /// render the camera into rt, call <see cref="SetOverlayTarget"/>(rt) and <see cref="ForceIntroTime"/>(3f), wait one frame (WaitForEndOfFrame)
    /// without re-rendering the camera into rt, read back, then SetOverlayTarget(null) and ForceIntroTime(float.NaN). <see cref="PlayIntro"/> plays
    /// the intro for first-frames sequences.</item>
    /// <item>QA: <see cref="Restart"/> (static, no arguments) reloads scene 0 at once.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class UiDirector : MonoBehaviour
    {
        [SerializeField] public UIDocument document;

        // ---------------- typography (14.11; reference 1920 x 1080; every size scales with the panel height) ----------------
        // These 14.11 numbers are not in RingConstants (contract request: move them into a UI block there).
        /// <summary>Reference panel height of the 14.11 sizes (PanelSettings: Scale With Screen Size 1920 x 1080, match height).</summary>
        public const float RefHeight = 1080f;
        /// <summary>14.11: cap-height ratio 0.72 (title cap 4.5 % -> 67 px, end line 3.4 % -> 51 px).</summary>
        public const float CapHeightRatio = 0.72f;
        /// <summary>14.11: title font size 67 px at the 1080 reference.</summary>
        public const float TitleFontPx = 67f;
        /// <summary>14.11: +12 % tracking. USS letter-spacing accepts only px or % (research 06 1.4), so px = 0.12 x font size (8.04 px for the title).</summary>
        public const float Tracking = 0.12f;
        /// <summary>14.11: title centre 40 % from the top.</summary>
        public const float TitleCentre = 0.40f;
        /// <summary>14.11: TitleText at alpha 0.92 (title, end line, selected menu item).</summary>
        public const float TitleAlphaMax = 0.92f;
        /// <summary>14.11: hint "height 1.8 %" of the screen.</summary>
        public const float HintHeight = 0.018f;
        /// <summary>14.11: hint bottom edge 6 % above the bottom of the screen.</summary>
        public const float HintBottom = 0.06f;
        /// <summary>14.11: hint alpha 0.7.</summary>
        public const float HintAlphaMax = 0.70f;
        /// <summary>Spec silent: light tracking so the hint reads as the title's family.</summary>
        public const float HintTracking = 0.06f;
        /// <summary>14.11: end line 51 px at the 1080 reference (cap height 3.4 %).</summary>
        public const float EndFontPx = 51f;
        /// <summary>Spec silent on the end line's position: optical centre of the black frame (slightly above the middle).</summary>
        public const float EndCentre = 0.46f;
        /// <summary>Part 9 "same typography as the title", smaller: 30 px (cap height 2.0 %).</summary>
        public const float MenuFontPx = 30f;
        /// <summary>Menu tracking (a little wider than the title at the smaller size).</summary>
        public const float MenuTracking = 0.16f;
        /// <summary>Menu row pitch in font sizes (the middle row is centred on the screen).</summary>
        public const float MenuPitch = 2.4f;
        public const float MenuSelAlpha = 0.92f, MenuIdleAlpha = 0.40f;
        /// <summary>Part 9 "dim the picture slightly": black at 0.45 in linear light (the picture keeps about 76 % of its displayed level).</summary>
        public const float PauseDim = 0.45f;
        public const float MenuFadeTime = 0.25f;
        /// <summary>Restart / quit from the menu first fade picture and audio to black so nothing flashes or clicks.</summary>
        public const float ExitFadeTime = 0.40f;
        /// <summary>Part 10 #25: when the intro is skipped the picture appears at once, but FadeAudio ramps over 50 ms so nothing clicks.</summary>
        public const float SkipAudioRamp = 0.05f;
        /// <summary>sRGB is close to a 2.2 power law: the 14.11 envelopes act on the displayed level (a linear-light multiply would front-load
        /// the fade-in, about 43 % brightness a quarter of the way in).</summary>
        const float FadeGamma = 2.2f;
        /// <summary>RingRig.WorldReady without RingEvents.LoadComplete for this long is a bootstrap bug: log an error and start the intro anyway,
        /// so the game is never stuck on black.</summary>
        const float LoadWatchdog = 10f;
        /// <summary>Fallback for a missed PlayerMoved event: ground speed above 0.3 m/s (squared) after load counts as the first movement.</summary>
        const float MoveSpeedSq = 0.3f * 0.3f;
        const float SelectEase = 0.15f;
        const float RuleAlpha = 0.6f, RuleWidthEm = 1.2f, RuleDropEm = 0.62f, RuleThickPx = 2f;
        const float MaxDt = 0.1f;
        const float AlphaEps = 0.5f / 255f;
        /// <summary>Frames to wait after a font change before measuring: inline styles reach the computed style at the next panel update.</summary>
        const int FontSettleFrames = 2;
        const string LegacyFontName = "LegacyRuntime.ttf";
        static readonly string[] MenuText = { "RESUME", "RESTART", "QUIT" };

        // ---------------- public static surface ----------------
        static float _fadePicture, _fadeAudio, _pictureMul;

        /// <summary>Picture visibility 0..1 (0 = black), display-referred. Without a live UiDirector: 1 once the world is ready, else 0.</summary>
        public static float FadePicture => _inst != null ? _fadePicture : (RingRig.WorldReady ? 1f : 0f);
        /// <summary>Audio master gain 0..1 for the AudioDirector: intro fade-in, ending fade-out and the restart / quit fade (14.11 "picture and
        /// audio"; a 50 ms ramp when the intro is skipped). Pause ducking is the AudioDirector's own. Without a live UiDirector: 1 once the world
        /// is ready, else 0.</summary>
        public static float FadeAudio => _inst != null ? _fadeAudio : (RingRig.WorldReady ? 1f : 0f);
        /// <summary>Linear-light picture multiplier for a shader-side fade: FadePicture^2.2 x (1 - PauseDim x menu visibility).</summary>
        public static float PictureMul => _inst != null ? _pictureMul : (RingRig.WorldReady ? 1f : 0f);

        /// <summary>True once the final pass multiplies its colour by <see cref="PictureMul"/> before dithering: the black overlay is then only the
        /// frame-0 black (until LoadComplete) and the pause dim is left to the shader. Default false (the UI overlay does the fade).</summary>
        public static bool ShaderFade;
        /// <summary>Font choice; switch after testing in the built player. Also settable with -uifont theme|legacy.</summary>
        public static UiFontMode FontMode = UiFontMode.Legacy;
        /// <summary>When true the UiDirector also toggles the pause on Esc. Keep false while the PlayerController owns Esc.</summary>
        public static bool HandleEscape;
        /// <summary>Gamepad Start toggles the pause (Part 9: gamepad is secondary; the PlayerController binds only Esc).</summary>
        public static bool HandleGamepadStart = true;
        /// <summary>Cursor locked and hidden in play, visible in the pause overlay (Part 9). Set false if another system owns the cursor alone.</summary>
        public static bool ManageCursor = true;
        /// <summary>Hint size convention. false (default, task BD): "height 1.8 %" is the font size, 0.018 x 1080 = 19.4 px. true: the 14.11 cap-height
        /// convention of the title and the end line ("3.4 % height (51 px)"), 0.018 / 0.72 x 1080 = 27 px. Call <see cref="Relayout"/> after a change.</summary>
        public static bool HintSizeIsCapHeight;
        /// <summary>Fraction of the letter spacing added as left padding to the tracked labels. 0 by default (TextCore centres on the glyph
        /// extents); set 1 if player captures show tracked text left of centre by half a letter spacing. Call <see cref="Relayout"/> after a change.</summary>
        public static float TrackingCentreBias;

        /// <summary>Current final alphas (QA readback; no rendering needed).</summary>
        public static float TitleAlpha { get; private set; }
        public static float HintAlpha { get; private set; }
        public static float EndLineAlpha { get; private set; }
        public static float MenuAlpha { get; private set; }
        /// <summary>Seconds since LoadComplete (-1 before) and since GoalReached (-1 before).</summary>
        public static float IntroTime { get; private set; } = -1f;
        public static float EndingTime { get; private set; } = -1f;
        /// <summary>The ending line has faded out and RingEvents.EndingComplete was raised.</summary>
        public static bool EndingDone { get; private set; }
        /// <summary>The pause overlay is open (the UiDirector's view of RingEvents.PauseChanged).</summary>
        public static bool MenuOpen => _inst != null && _inst._paused;
        /// <summary>Hint font size at the 1080 reference for the current <see cref="HintSizeIsCapHeight"/> convention.</summary>
        public static float HintFontPx => HintSizeIsCapHeight ? HintHeight / CapHeightRatio * RefHeight : HintHeight * RefHeight;
        /// <summary>The document the UI is built in. Captures use <see cref="SetOverlayTarget"/> rather than changing it directly.</summary>
        public static UIDocument Document => _inst != null ? _inst.document : null;

        /// <summary>Capture override of the intro clock (seconds since LoadComplete); NaN releases it. Title and fade follow it even in capture
        /// mode (KS00 = 3.0 s); the hint stays off in capture mode.</summary>
        public static void ForceIntroTime(float t) { _forcedIntroT = t; Refresh(); }

        /// <summary>Skip the intro (as -nofade): picture visible, no title, no hint.</summary>
        public static void SkipIntro()
        {
            if (_inst == null) return;
            _inst._introOverride = 1; _inst._introSkip = true; _inst._hintKilled = true;
        }

        /// <summary>Play the intro from t = 0 even in capture mode (first-frames sequences). Starts at LoadComplete if not loaded yet.</summary>
        public static void PlayIntro()
        {
            if (_inst == null) return;
            _inst._introOverride = 0; _inst._introSkip = false; _inst._introT = 0f;
        }

        /// <summary>Re-evaluates and writes every style now without advancing any clock (captures call it after forcing state).</summary>
        public static void Refresh()
        {
            if (_inst == null) return;
            _inst.Evaluate(0f, 0f);
            _inst.Apply();
        }

        /// <summary>Switches the font at runtime (for the lead's player test) and re-applies it to every label; the automatic blank check starts
        /// again from this mode.</summary>
        public static void SetFontMode(UiFontMode mode)
        {
            FontMode = mode;
            if (_inst != null) { _inst._fontBlank = 0; _inst.ApplyFont(); }
        }

        /// <summary>Re-applies the sizes (after changing <see cref="HintSizeIsCapHeight"/> or <see cref="TrackingCentreBias"/>).</summary>
        public static void Relayout()
        {
            if (_inst != null && _inst._layer != null) _inst.ApplyLayout(_inst._lastH > 1f ? _inst._lastH : RefHeight);
        }

        /// <summary>QA / soak entry point: restarts the walk at once by reloading scene 0. GameBootstrap.Awake of the reloaded scene resets
        /// RingState, RingRig and RingEvents. The menu's RESTART does the same after a 0.4 s fade to black.</summary>
        public static void Restart()
        {
            Debug.Log("[Ring] UI: restart (reload scene 0)");
            _fadePicture = 0f; _fadeAudio = 0f; _pictureMul = 0f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(0);
        }

        /// <summary>
        /// Renders the UI panel into <paramref name="rt"/> on top of its content (clearColor off), so a camera-RT capture can include the title
        /// (KS00): overlay UI is never part of a camera RenderTexture (research 06 1.6). Uses a runtime clone of the PanelSettings asset
        /// (Instantiate keeps its serialized shader references; the asset itself is never modified). Null restores the original PanelSettings.
        /// With Scale With Screen Size (match height) the panel is 1080 high in any RT, so every 14.11 size holds. Returns false when there is no
        /// UIDocument or PanelSettings to clone.
        /// </summary>
        public static bool SetOverlayTarget(RenderTexture rt)
        {
            var ui = _inst;
            if (ui == null || ui.document == null) return false;
            var doc = ui.document;
            if (rt == null)
            {
                if (ui._basePanel != null && doc.panelSettings != ui._basePanel) doc.panelSettings = ui._basePanel;
                if (ui._capturePanel != null) ui._capturePanel.targetTexture = null;
                ui.Attach();
                Refresh();
                return true;
            }
            var cur = doc.panelSettings;
            if (cur != null && cur != ui._capturePanel) ui._basePanel = cur;
            if (ui._basePanel == null)
            {
                Debug.LogWarning("[Ring] UI: SetOverlayTarget: the UIDocument has no PanelSettings to clone");
                return false;
            }
            if (ui._capturePanel == null)
            {
                ui._capturePanel = Instantiate(ui._basePanel);
                ui._capturePanel.name = ui._basePanel.name + " (capture)";
            }
            var cp = ui._capturePanel;
            cp.clearColor = false;
            cp.targetTexture = rt;
            if (doc.panelSettings != cp) doc.panelSettings = cp;
            ui.Attach();                    // re-attach the layer if the panel change rebuilt the root
            Refresh();
            return true;
        }

        // ---------------- state ----------------
        static UiDirector _inst;
        static float _forcedIntroT = float.NaN;
        static Font _legacyFont;

        VisualElement _layer, _dim, _black, _menu;
        Label _title, _hint, _end;
        readonly VisualElement[] _rows = new VisualElement[3];
        readonly Label[] _items = new Label[3];
        readonly VisualElement[] _rules = new VisualElement[3];
        Color _ink;
        float _lastH = -1f;
        PanelSettings _basePanel, _capturePanel;
        EventCallback<GeometryChangedEvent> _onGeometry;

        Action _onLoad, _onGoal, _onMoved;
        Action<bool> _onPause, _onCine;
        bool _started, _subscribed, _warnedNoDoc, _warnedMeasure;

        // flow
        bool _loaded, _introSkip, _ending, _endDone, _paused, _fontChecked, _exitDone, _statePaused;
        bool _inputBlocked;               // -autotest / -capture / -walltest: no pause keys, cursor stays locked (as the PlayerController)
        bool _cliNoHint;                  // -autotest / -capture / -nofade / -cinematic: the hint never appears
        bool _hintKilled;                 // first movement or cinematic: fade out within 0.4 s (soft)
        bool _hintBlocked;                // hard block latched after load (RingState.AutoTest / Capture): never shown again
        int _introOverride = -1;          // -1 auto, 0 play, 1 skip
        int _exitAction;                  // 0 none, 1 restart, 2 quit
        int _fontCheckFrame;              // FontCheck waits until this frame (the style pass after the last font change)
        int _fontBlank;                   // bit (1 << UiFontMode) set for every mode that measured blank
        int _pauseEventFrame = -1;        // frame of the last PauseChanged (guards against a double toggle)
        float _introT, _endT, _hintGate = 1f, _menuA, _exitFade = 1f, _skipAudio, _readyWait;
        string _fontLog;

        // evaluated values (Evaluate -> Apply)
        float _vBlack, _vDim;

        // menu input
        int _sel, _pressItem = -1, _mouseSettle;
        bool _mouseEngaged, _stickArmed;
        float _mouseTravel;
        Vector2 _lastMouse;
        readonly float[] _itemA = new float[3], _ruleA = new float[3];

        // last written alphas (-1 = unknown); styles are only written when a value changes
        float _cBlack = -1f, _cDim = -1f, _cTitle = -1f, _cHint = -1f, _cEnd = -1f, _cMenu = -1f;
        readonly float[] _cItem = { -1f, -1f, -1f }, _cRule = { -1f, -1f, -1f };

        // ---------------- lifecycle ----------------

        void Awake()
        {
            _inst = this;
            _fadePicture = 0f; _fadeAudio = 0f; _pictureMul = 0f;
            TitleAlpha = 0f; HintAlpha = 0f; EndLineAlpha = 0f; MenuAlpha = 0f;
            IntroTime = -1f; EndingTime = -1f; EndingDone = false;
            _forcedIntroT = float.NaN;
            _onLoad = OnLoadComplete; _onGoal = OnGoal; _onMoved = OnPlayerMoved; _onPause = OnPause; _onCine = OnCinematic;
            _onGeometry = OnGeometry;
            string f = CliArgs.Str("uifont");
            if (f != null)
            {
                if (string.Equals(f, "theme", StringComparison.OrdinalIgnoreCase)) FontMode = UiFontMode.Theme;
                else if (string.Equals(f, "legacy", StringComparison.OrdinalIgnoreCase)) FontMode = UiFontMode.Legacy;
            }
            _inputBlocked = CliArgs.AutoTest || CliArgs.Capturing || CliArgs.WallTest;
            // 14.11 "never in cinematic or -autotest" (+ capture, -nofade): -cinematic is a hard blocker too, the autowalk starts at LoadComplete.
            _cliNoHint = CliArgs.AutoTest || CliArgs.Capturing || CliArgs.NoFade || CliArgs.Cinematic;
            if (document == null) document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            if (_inst == null) _inst = this;
            if (document == null) document = GetComponent<UIDocument>();
            EnsurePanelSettings();
            Attach();                       // frame 0 is black: the overlay exists before the first rendered frame
            if (_started) Subscribe();
        }

        void Start()
        {
            // Subscribe here, not in OnEnable: GameBootstrap.Awake (order -1000) calls RingEvents.Reset(), and every Awake runs before any Start.
            _started = true;
            Subscribe();
            EnsurePanelSettings();
            Attach();
            ApplyFont();                    // RingAssets.Instance is set by GameBootstrap.Awake by now
            _statePaused = RingState.Paused;
            ApplyCursor();
            if (RingRig.WorldReady) OnLoadComplete();   // LoadComplete raised before this component subscribed
        }

        void OnDisable()
        {
            Unsubscribe();
            if (_layer != null) _layer.RemoveFromHierarchy();
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (_capturePanel != null)
            {
                if (document != null && _basePanel != null && document.panelSettings == _capturePanel) document.panelSettings = _basePanel;
                Destroy(_capturePanel);
                _capturePanel = null;
            }
            if (_inst == this) _inst = null;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) ApplyCursor();
        }

        void Subscribe()
        {
            Unsubscribe();
            RingEvents.LoadComplete += _onLoad;
            RingEvents.GoalReached += _onGoal;
            RingEvents.PlayerMoved += _onMoved;
            RingEvents.PauseChanged += _onPause;
            RingEvents.CinematicChanged += _onCine;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            RingEvents.LoadComplete -= _onLoad;
            RingEvents.GoalReached -= _onGoal;
            RingEvents.PlayerMoved -= _onMoved;
            RingEvents.PauseChanged -= _onPause;
            RingEvents.CinematicChanged -= _onCine;
            _subscribed = false;
        }

        void Update()
        {
            float rdt = Time.unscaledDeltaTime;
            if (!(rdt > 0f)) rdt = 0f; else if (rdt > MaxDt) rdt = MaxDt;   // a load hitch never skips a fade

            if (_layer == null || (document != null && _layer.parent != document.rootVisualElement)) { EnsurePanelSettings(); Attach(); }

            Poll(rdt);
            PauseKeys();
            if (_paused && _exitAction == 0 && _layer != null) MenuInput();
            Evaluate(rdt, GameDt(rdt));
            Apply();
            FontCheck();
        }

        // ---------------- events ----------------

        void OnLoadComplete()
        {
            if (_loaded) return;
            _loaded = true;
            _introT = 0f;
            bool capture = CliArgs.Capturing || RingState.Capture;
            _introSkip = _introOverride == 1 || (_introOverride == -1 && (CliArgs.NoFade || capture));
            if (_introSkip) _hintKilled = true;
            Debug.Log(_introSkip ? "[Ring] UI: load complete, intro skipped (nofade or capture)" : "[Ring] UI: load complete, intro started");
        }

        void OnGoal()
        {
            if (_ending) return;
            _ending = true;
            _endT = 0f;
            _hintKilled = true;
            Debug.Log("[Ring] UI: goal reached, ending started");
        }

        void OnPlayerMoved() { _hintKilled = true; }

        void OnCinematic(bool on) { if (on) _hintKilled = true; }

        void OnPause(bool p)
        {
            _pauseEventFrame = Time.frameCount;
            if (_exitAction != 0 || p == _paused) return;
            _paused = p;
            if (p)
            {
                _sel = 0; _pressItem = -1;
                _mouseEngaged = false; _mouseTravel = 0f; _mouseSettle = 2;   // the cursor jumps when it unlocks: ignore it
                _stickArmed = false;                                          // a held stick must return to centre first
                for (int i = 0; i < 3; i++) { _itemA[i] = i == 0 ? MenuSelAlpha : MenuIdleAlpha; _ruleA[i] = i == 0 ? RuleAlpha : 0f; }
            }
            ApplyCursor();
        }

        void Poll(float rdt)
        {
            // LoadComplete arrives by event (Start covers one raised before the subscription). RingRig.WorldReady only feeds a watchdog, so a
            // missing event fails loudly instead of leaving the game on black.
            if (!_loaded && RingRig.WorldReady)
            {
                _readyWait += rdt;
                if (_readyWait >= LoadWatchdog)
                {
                    Debug.LogError("[Ring] UI: RingRig.WorldReady has been true for 10 s without RingEvents.LoadComplete; starting the intro anyway");
                    OnLoadComplete();
                }
            }
            if (!_ending && RingState.GoalReached) OnGoal();

            // Read-only, edge-triggered mirror of RingState.Paused (PauseChanged stays authoritative): follows a writer that flipped the flag
            // without raising the event, and never fights a pause owner that does not write the flag at all.
            bool sp = RingState.Paused;
            if (sp != _statePaused)
            {
                _statePaused = sp;
                if (sp != _paused && _exitAction == 0) OnPause(sp);
            }

            if (!_hintKilled)
            {
                if (RingState.Cinematic) _hintKilled = true;
                else if (_loaded && RingRig.PlayerVelocity.sqrMagnitude > MoveSpeedSq) _hintKilled = true;
            }
            // Hard blockers are read only after load: the GameBootstrap pre-warm sets RingState.Capture temporarily while loading.
            if (_loaded && !_hintBlocked && (RingState.AutoTest || RingState.Capture)) _hintBlocked = true;
        }

        float GameDt(float rdt)
        {
            if (_paused) return 0f;                                                  // the intro and the ending hold while paused
            if (_loaded && RingState.Capture) return RingState.Dt > 0f ? RingState.Dt : 0f;   // capture mode: frozen or stepped clock
            return rdt;
        }

        // ---------------- evaluation (no style writes) ----------------

        void Evaluate(float rdt, float gdt)
        {
            if (_loaded) _introT += gdt;
            if (_ending) _endT += gdt;

            // intro
            bool forced = !float.IsNaN(_forcedIntroT);
            float t = forced ? _forcedIntroT : _introT;
            float pic, title, hint, audioEnv = 1f;
            if (forced || (_loaded && !_introSkip)) { pic = UiTimeline.Picture(t); title = UiTimeline.Title(t); hint = UiTimeline.Hint(t); }
            else if (_loaded)
            {
                pic = 1f; title = 0f; hint = 0f;
                _skipAudio = Mathf.MoveTowards(_skipAudio, 1f, rdt / SkipAudioRamp);   // picture at once, audio over 50 ms (no click)
                audioEnv = _skipAudio;
            }
            else { pic = 0f; title = 0f; hint = 0f; }

            // hint: never in cinematic / autotest / capture / nofade; gone within 0.4 s of the first movement
            if (_cliNoHint || _introSkip || _hintBlocked) _hintGate = 0f;
            else if (_hintKilled) _hintGate = Mathf.MoveTowards(_hintGate, 0f, rdt / UiTimeline.HintKill);
            hint *= RingMath.Smoothstep(0f, 1f, _hintGate);

            // ending
            float endPic = 1f, endLine = 0f;
            if (_ending)
            {
                endPic = UiTimeline.EndPicture(_endT);
                endLine = UiTimeline.EndLine(_endT);
                if (!_endDone && _endT >= UiTimeline.EndTotal)
                {
                    _endDone = true;
                    EndingDone = true;
                    Debug.Log("[Ring] UI: ending complete");
                    RingEvents.RaiseEndingComplete();
                }
            }

            // pause menu, restart / quit fade
            _menuA = Mathf.MoveTowards(_menuA, _paused || _exitAction != 0 ? 1f : 0f, rdt / MenuFadeTime);
            if (_exitAction != 0)
            {
                _exitFade = Mathf.MoveTowards(_exitFade, 0f, rdt / ExitFadeTime);
                if (_exitFade <= 0f && !_exitDone) { _exitDone = true; DoExit(); }
            }
            float menuVis = RingMath.Smoothstep(0f, 1f, _menuA);
            float exitVis = RingMath.Smoothstep(0f, 1f, _exitFade);

            float picture = pic * endPic * exitVis;
            float linPicture = DisplayToLinear(picture);
            float dim = PauseDim * menuVis;
            _fadePicture = picture;
            _fadeAudio = picture * audioEnv;
            _pictureMul = linPicture * (1f - dim);

            float textVis = 1f - menuVis;            // the menu never overlaps the title, hint or end line
            TitleAlpha = TitleAlphaMax * title * textVis;
            HintAlpha = HintAlphaMax * hint * textVis;
            EndLineAlpha = TitleAlphaMax * endLine * textVis;
            MenuAlpha = menuVis * exitVis;
            IntroTime = _loaded || forced ? t : -1f;
            EndingTime = _ending ? _endT : -1f;

            for (int i = 0; i < 3; i++)
            {
                bool s = i == _sel;
                _itemA[i] = Mathf.MoveTowards(_itemA[i], s ? MenuSelAlpha : MenuIdleAlpha, rdt / SelectEase);
                _ruleA[i] = Mathf.MoveTowards(_ruleA[i], s ? RuleAlpha : 0f, rdt / SelectEase);
            }

            if (ShaderFade)
            {
                // The final pass applies PictureMul before its dither; the overlay only keeps the frame-0 black until LoadComplete.
                _vBlack = _loaded || forced ? 0f : 1f;
                _vDim = 0f;
            }
            else
            {
                // Black over the picture in linear light: 1 - picture^2.2 leaves the displayed level at about picture x its value.
                _vBlack = 1f - linPicture;
                _vDim = dim;
            }
        }

        void Apply()
        {
            if (_layer == null) return;
            Write(_black, ref _cBlack, _vBlack);
            Write(_dim, ref _cDim, _vDim);
            Write(_title, ref _cTitle, TitleAlpha);
            Write(_hint, ref _cHint, HintAlpha);
            Write(_end, ref _cEnd, EndLineAlpha);
            Write(_menu, ref _cMenu, MenuAlpha);
            if (MenuAlpha > 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    WriteOpacity(_items[i], ref _cItem[i], _itemA[i]);
                    WriteOpacity(_rules[i], ref _cRule[i], _ruleA[i]);
                }
            }
        }

        /// <summary>Display-referred envelope (the shown sRGB level scales by it) to the linear-light multiplier of the overlay or shader fade.</summary>
        static float DisplayToLinear(float p)
        {
            if (!(p > 0f)) return 0f;      // also NaN
            if (p >= 1f) return 1f;
            return Mathf.Pow(p, FadeGamma);
        }

        static float Snap(float a)
        {
            if (!(a > AlphaEps)) return 0f;      // also NaN
            return a >= 1f - AlphaEps ? 1f : a;
        }

        /// <summary>Top-level element: opacity plus visibility (hidden elements are not drawn).</summary>
        static void Write(VisualElement e, ref float cache, float a)
        {
            a = Snap(a);
            if (a == cache) return;
            bool show = a > 0f;
            if (cache < 0f || show != (cache > 0f)) e.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            e.style.opacity = a;
            cache = a;
        }

        /// <summary>Child of the menu: opacity only (it inherits the menu's visibility).</summary>
        static void WriteOpacity(VisualElement e, ref float cache, float a)
        {
            a = Snap(a);
            if (a == cache) return;
            e.style.opacity = a;
            cache = a;
        }

        // ---------------- pause keys and menu input (mouse, keyboard, gamepad; own hit testing, no UI Toolkit focus) ----------------

        void PauseKeys()
        {
            // The PlayerController (order -100) runs first: if it toggled the pause this frame, a second toggle here would undo it.
            if (_inputBlocked || _exitAction != 0 || _pauseEventFrame == Time.frameCount) return;
            bool toggle = false;
            if (HandleEscape)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) toggle = true;
            }
            if (HandleGamepadStart)
            {
                var gp = Gamepad.current;
                if (gp != null && gp.startButton.wasPressedThisFrame) toggle = true;
            }
            if (toggle) RingEvents.RaisePause(!_paused);
        }

        void MenuInput()
        {
            var ms = Mouse.current;
            if (Time.frameCount == _pauseEventFrame)
            {
                if (ms != null) _lastMouse = ms.position.ReadValue();
                return;                              // the key that opened the menu never activates an item
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Step(-1);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Step(1);
                // Alt+Enter is the fullscreen toggle (allowFullscreenSwitch / PlayerController): it must never activate an item.
                bool enter = (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !kb.altKey.isPressed;
                if (enter || kb.spaceKey.wasPressedThisFrame) { Activate(_sel); return; }
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) Step(-1);
                if (gp.dpad.down.wasPressedThisFrame) Step(1);
                float y = gp.leftStick.ReadValue().y;
                if (_stickArmed)
                {
                    if (y > 0.6f) { Step(-1); _stickArmed = false; }
                    else if (y < -0.6f) { Step(1); _stickArmed = false; }
                }
                else if (y > -0.3f && y < 0.3f) _stickArmed = true;
                if (gp.buttonSouth.wasPressedThisFrame) { Activate(_sel); return; }
                if (gp.buttonEast.wasPressedThisFrame) { Activate(0); return; }
            }

            if (ms != null)
            {
                Vector2 mp = ms.position.ReadValue();
                if (_mouseSettle > 0) { _mouseSettle--; _lastMouse = mp; return; }
                float d = (mp - _lastMouse).magnitude;
                _lastMouse = mp;
                if (!_mouseEngaged)
                {
                    _mouseTravel += d;
                    if (_mouseTravel < 6f) return;   // a click without moving the mouse never restarts or quits
                    _mouseEngaged = true;
                }
                int hit = HitRow(mp);
                if (hit >= 0 && d > 0.5f) _sel = hit;
                if (ms.leftButton.wasPressedThisFrame) _pressItem = hit;
                if (ms.leftButton.wasReleasedThisFrame)
                {
                    int p = _pressItem;
                    _pressItem = -1;
                    if (hit >= 0 && hit == p) Activate(hit);
                }
            }
        }

        void Step(int d)
        {
            _sel = (_sel + d + 3) % 3;
            _pressItem = -1;
        }

        /// <summary>Row under the mouse. The layer covers the whole panel, so the screen maps linearly onto its world bound (independent of the
        /// PanelSettings scale mode); Input System screen y is up, panel y is down.</summary>
        int HitRow(Vector2 mp)
        {
            float sw = Screen.width, sh = Screen.height;
            if (sw < 1f || sh < 1f) return -1;
            Rect lb = _layer.worldBound;
            if (!(lb.width > 0f) || !(lb.height > 0f)) return -1;
            var pp = new Vector2(lb.x + mp.x / sw * lb.width, lb.y + (1f - mp.y / sh) * lb.height);
            for (int i = 0; i < 3; i++) if (_rows[i].worldBound.Contains(pp)) return i;
            return -1;
        }

        void Activate(int item)
        {
            if (_exitAction != 0) return;
            if (item == 0) { RingEvents.RaisePause(false); return; }
            _exitAction = item == 1 ? 1 : 2;
            _exitFade = 1f;
            _exitDone = false;
            ApplyCursor();
            Debug.Log(item == 1 ? "[Ring] UI: restart requested" : "[Ring] UI: quit requested");
        }

        void DoExit()
        {
            _fadePicture = 0f; _fadeAudio = 0f; _pictureMul = 0f;
            if (_exitAction == 1)
            {
                Restart();
                return;
            }
            Debug.Log("[Ring] UI: quit");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void ApplyCursor()
        {
            if (!ManageCursor) return;
            // Same rule as the PlayerController: free only in the pause overlay, never in -autotest / -capture / -walltest.
            bool free = _paused && _exitAction == 0 && !_inputBlocked;
            UnityEngine.Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = free;
        }

        // ---------------- visual tree ----------------

        void EnsurePanelSettings()
        {
            if (document == null || document.panelSettings != null) return;
            var a = RingAssets.Instance;
            if (a != null && a.panelSettings != null) document.panelSettings = a.panelSettings;
        }

        void Attach()
        {
            if (document == null)
            {
                if (!_warnedNoDoc) { _warnedNoDoc = true; Debug.LogError("[Ring] UiDirector: no UIDocument on " + gameObject.name + "; the fade and the title cannot be shown"); }
                return;
            }
            var root = document.rootVisualElement;
            if (root == null) return;
            if (_layer == null) Create();
            if (_layer.parent == root) return;
            _layer.RemoveFromHierarchy();
            // Make sure the document root fills the panel even if the theme does not style it (the layer is relative to it).
            var rs = root.style;
            rs.position = Position.Absolute;
            rs.left = 0f; rs.top = 0f; rs.right = 0f; rs.bottom = 0f;
            root.pickingMode = PickingMode.Ignore;
            root.Add(_layer);
        }

        void Create()
        {
            _ink = PaletteSrgb("TitleText");

            _layer = new VisualElement { name = "ring-ui", pickingMode = PickingMode.Ignore };
            Stretch(_layer);
            _layer.RegisterCallback(_onGeometry);

            _dim = Overlay("ring-pause-dim");
            _black = Overlay("ring-fade");
            _title = MakeLabel("ring-title", RingConstants.Title);
            _hint = MakeLabel("ring-hint", RingConstants.Hint);
            _end = MakeLabel("ring-end", RingConstants.EndLine);

            _title.style.top = Length.Percent(TitleCentre * 100f);
            _end.style.top = Length.Percent(EndCentre * 100f);
            _hint.style.bottom = Length.Percent(HintBottom * 100f);

            _menu = new VisualElement { name = "ring-pause-menu", pickingMode = PickingMode.Ignore };
            Stretch(_menu);
            HideInitially(_menu);
            for (int i = 0; i < 3; i++)
            {
                var row = new VisualElement { name = "ring-pause-row" + i, pickingMode = PickingMode.Ignore };
                var rs = row.style;
                rs.position = Position.Absolute;
                rs.left = Length.Percent(30f); rs.right = Length.Percent(30f); rs.top = Length.Percent(50f);
                var item = MakeLabel("ring-pause-item" + i, MenuText[i]);
                item.style.top = 0f; item.style.bottom = 0f;
                item.style.visibility = StyleKeyword.Null;      // inherit the menu's visibility
                var rule = new VisualElement { name = "ring-pause-rule" + i, pickingMode = PickingMode.Ignore };
                var us = rule.style;
                us.position = Position.Absolute;
                us.left = Length.Percent(50f);
                us.backgroundColor = _ink;
                us.opacity = 0f;
                row.Add(item);
                row.Add(rule);
                _menu.Add(row);
                _rows[i] = row; _items[i] = item; _rules[i] = rule;
                _cItem[i] = 0f; _cRule[i] = 0f;
            }

            // draw order: dim, fade black, title, hint, end line, menu (the menu stays usable on the final black)
            _layer.Add(_dim);
            _layer.Add(_black);
            _layer.Add(_title);
            _layer.Add(_hint);
            _layer.Add(_end);
            _layer.Add(_menu);

            // frame 0 is black
            _black.style.opacity = 1f;
            _black.style.visibility = Visibility.Visible;
            _cBlack = 1f; _cDim = 0f; _cTitle = 0f; _cHint = 0f; _cEnd = 0f; _cMenu = 0f;
            _vBlack = 1f; _vDim = 0f;

            ApplyLayout(RefHeight);
            ApplyFont();
        }

        void OnGeometry(GeometryChangedEvent e)
        {
            float h = e.newRect.height;
            if (h > 1f && Mathf.Abs(h - _lastH) > 0.5f) ApplyLayout(h);
        }

        /// <summary>Sizes in panel pixels from the panel height, so every 14.11 percentage holds whatever the PanelSettings scale mode is
        /// (with Scale With Screen Size 1920 x 1080 match height the height is always 1080 and k = 1).</summary>
        void ApplyLayout(float h)
        {
            if (!(h > 1f)) return;
            _lastH = h;
            float k = h / RefHeight;

            float tf = TitleFontPx * k;
            SetType(_title, tf, Tracking);
            _title.style.height = 2f * tf;
            _title.style.marginTop = -tf;           // the label box is centred on its top (40 %)

            float ef = EndFontPx * k;
            SetType(_end, ef, Tracking);
            _end.style.height = 2f * ef;
            _end.style.marginTop = -ef;

            SetType(_hint, HintFontPx * k, HintTracking);

            float mf = MenuFontPx * k, pitch = MenuPitch * mf;
            float rw = RuleWidthEm * mf, rh = Mathf.Max(1f, RuleThickPx * k);
            for (int i = 0; i < 3; i++)
            {
                var rs = _rows[i].style;
                rs.height = pitch;
                rs.marginTop = (i - 1) * pitch - 0.5f * pitch;
                SetType(_items[i], mf, MenuTracking);
                var us = _rules[i].style;
                us.width = rw;
                us.height = rh;
                us.marginLeft = -0.5f * rw;
                us.top = 0.5f * pitch + RuleDropEm * mf;
            }
        }

        static void SetType(Label l, float px, float track)
        {
            float ls = track * px;                  // letter-spacing in px (no em in USS)
            l.style.fontSize = px;
            l.style.letterSpacing = ls;
            l.style.paddingLeft = TrackingCentreBias * ls;
        }

        void ApplyFont()
        {
            if (_layer == null) return;
            StyleFontDefinition sfd = new StyleFontDefinition(StyleKeyword.Null);
            string src = "theme default";
            if (FontMode == UiFontMode.Legacy)
            {
                Font f = null;
                var a = RingAssets.Instance;
                if (a != null && a.font != null) f = a.font;
                if (f == null)
                {
                    if (_legacyFont == null) _legacyFont = Resources.GetBuiltinResource<Font>(LegacyFontName);
                    f = _legacyFont;
                }
                if (f != null) { sfd = new StyleFontDefinition(FontDefinition.FromFont(f)); src = f.name; }
                else src = "theme default (" + LegacyFontName + " not found)";
            }
            _layer.style.unityFontDefinition = sfd;
            _title.style.unityFontDefinition = sfd;
            _hint.style.unityFontDefinition = sfd;
            _end.style.unityFontDefinition = sfd;
            for (int i = 0; i < 3; i++) _items[i].style.unityFontDefinition = sfd;
            // MeasureTextSize reads the computed style, which changes at the next panel style pass: re-check after it.
            _fontChecked = false;
            _fontCheckFrame = Time.frameCount + FontSettleFrames;
            if (src != _fontLog) { _fontLog = src; Debug.Log("[Ring] UI font: " + src + " (mode " + FontMode + ")"); }
        }

        /// <summary>
        /// After every font change (once the style pass has run): measure the title. Blank -> switch to the other font mode once and check
        /// again; both blank -> Debug.LogError. On success measure every other string too, so the dynamic SDF atlas gets its glyphs while the
        /// screen is still black (during the load, or inside the 0.6 s black after it) instead of at the first show of each label.
        /// </summary>
        void FontCheck()
        {
            if (_fontChecked || !_started || _title == null || _title.panel == null || Time.frameCount < _fontCheckFrame) return;
            _fontChecked = true;
            if (!Measure(_title, RingConstants.Title, out Vector2 sz)) return;
            if (sz.x >= 1f)                      // false for NaN too
            {
                Measure(_hint, RingConstants.Hint, out _);
                Measure(_end, RingConstants.EndLine, out _);
                for (int i = 0; i < 3; i++) Measure(_items[i], MenuText[i], out _);
                Debug.Log("[Ring] UI font in use: " + _fontLog + " (mode " + FontMode + "), title " +
                          sz.x.ToString("0", CultureInfo.InvariantCulture) + " x " + sz.y.ToString("0", CultureInfo.InvariantCulture) + " px; glyphs warmed");
                return;
            }
            _fontBlank |= 1 << (int)FontMode;
            var other = FontMode == UiFontMode.Legacy ? UiFontMode.Theme : UiFontMode.Legacy;
            if ((_fontBlank & (1 << (int)other)) == 0)
            {
                Debug.LogWarning("[Ring] UI: the title measures blank with " + _fontLog + " (mode " + FontMode + "); switching to mode " + other);
                FontMode = other;
                ApplyFont();                     // re-arms the check after the next style pass
                return;
            }
            const string msg = "[Ring] UI: the title measures blank with both the theme font and " + LegacyFontName +
                               "; no UI text can render (check the theme .tss and the TextCore SDF shader in the build)";
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Debug.LogWarning(msg + " [null graphics device]");
            else Debug.LogError(msg);
        }

        /// <summary>One-off text measure (never per frame). False if it threw (logged once).</summary>
        bool Measure(Label l, string text, out Vector2 size)
        {
            size = Vector2.zero;
            try
            {
                size = l.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                return true;
            }
            catch (Exception ex)
            {
                if (!_warnedMeasure) { _warnedMeasure = true; Debug.LogWarning("[Ring] UI: could not measure " + l.name + " (" + ex.GetType().Name + ")"); }
                return false;
            }
        }

        static VisualElement Overlay(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Stretch(e);
            e.style.backgroundColor = Color.black;
            HideInitially(e);
            return e;
        }

        Label MakeLabel(string name, string text)
        {
            var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore, enableRichText = false };
            var s = l.style;
            s.position = Position.Absolute;
            s.left = 0f; s.right = 0f;
            s.marginLeft = 0f; s.marginRight = 0f; s.marginTop = 0f; s.marginBottom = 0f;
            s.paddingLeft = 0f; s.paddingRight = 0f; s.paddingTop = 0f; s.paddingBottom = 0f;
            s.borderLeftWidth = 0f; s.borderRightWidth = 0f; s.borderTopWidth = 0f; s.borderBottomWidth = 0f;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            // Pre keeps the hint's runs of 2 and 4 spaces (NoWrap collapses whitespace); no string has a newline, so nothing ever wraps.
            s.whiteSpace = WhiteSpace.Pre;
            s.unityFontStyleAndWeight = FontStyle.Normal;   // 14.11: no thin weight exists; regular at alpha 0.92
            s.unityTextOutlineWidth = 0f;
            s.overflow = Overflow.Visible;
            s.color = _ink;
            HideInitially(l);
            return l;
        }

        static void HideInitially(VisualElement e)
        {
            e.style.opacity = 0f;
            e.style.visibility = Visibility.Hidden;
        }

        static void Stretch(VisualElement e)
        {
            var s = e.style;
            s.position = Position.Absolute;
            s.left = 0f; s.top = 0f; s.right = 0f; s.bottom = 0f;
            s.marginLeft = 0f; s.marginRight = 0f; s.marginTop = 0f; s.marginBottom = 0f;
        }

        /// <summary>UI Toolkit colours are sRGB (gamma) values: use the palette hex directly, not the linear value. RingPalette is the only colour
        /// source: a missing token is an error, never a silent literal.</summary>
        static Color PaletteSrgb(string token)
        {
            var e = RingPalette.Entries;
            for (int i = 0; i < e.Length; i++) if (e[i].name == token) return RingPalette.HexToSrgb(e[i].hex);
            Debug.LogError("[Ring] UI: palette token missing: " + token);
            return Color.white;
        }
    }
}
