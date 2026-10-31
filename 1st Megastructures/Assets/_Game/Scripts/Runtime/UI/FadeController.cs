using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Megastructure
{
    /// <summary>
    /// Picture fade, title card, controls hint, pause overlay and ending (Part 9, 14.11). Lives on the "UI" GameObject.
    ///
    /// The picture fade is published as <see cref="GameState.PictureFade"/> and as the global float _AbPictureFade, which the
    /// final dither pass multiplies the picture by (before dithering, so slow fades never band). A full-screen black UI layer
    /// covers the screen while the picture is fully black (load, end of the ending, restart), so the screen is black even
    /// before the first shader is ready; it is never drawn half-transparent over the picture. Text is drawn above it.
    ///
    /// Time base: SceneClock.RealTime (never freezes; GameState.GoalRealTime uses it too). If no ClockDriver advances it,
    /// the controller falls back to its own unscaled clock after 2 s and says so once in the log.
    /// Execution order -1900: Awake builds the black layer before anything renders; Update publishes the fade before
    /// AudioDirector (+20) reads it.
    /// </summary>
    [DefaultExecutionOrder(-1900)]
    [DisallowMultipleComponent]
    public sealed class FadeController : MonoBehaviour
    {
        /// <summary>Global shader float multiplying the final picture (read by the final dither shader as "_AbPictureFade").</summary>
        public static readonly int AbPictureFade = Shader.PropertyToID("_AbPictureFade");

        public static FadeController Instance { get; private set; }

        [Tooltip("PanelSettings asset (Assets/_Game/Settings/AbyssPanelSettings.asset, theme UnityDefaultRuntimeTheme.tss). Null = UGUI fallback.")]
        public PanelSettings panelSettings;

        public enum Phase { Loading = 0, Title = 1, Restarting = 2 }

        /// <summary>Current phase (Title covers the whole walk after the load, including the ending).</summary>
        public Phase CurrentPhase { get; private set; } = Phase.Loading;

        /// <summary>Seconds since the title timeline started (0 while loading).</summary>
        public float TitleTime { get; private set; }

        /// <summary>True once the ending line has faded out (the screen stays black; Esc still opens the menu).</summary>
        public bool EndingComplete { get; private set; }

        /// <summary>True while the pause menu is open.</summary>
        public bool IsPaused => _pause.IsOpen;

        IUiView _view;
        readonly PauseOverlay _pause = new PauseOverlay();

        bool _skipTitle;           // -nofade / capture mode: picture on at once, no title, no hint
        double _titleStart;
        double _ownClock;          // unscaled fallback clock
        bool _useOwnClock;
        double _clockProbeReal = double.NaN, _clockProbeOwn;
        float _restartT;
        float _savedTimeScale = 1f;
        Vector3 _startPos;
        double _moveTime = -1;     // title-time of the first movement (-1 = none yet)
        double _goalStart = -1;    // clock time of the goal (ending reference)
        float _lastFade = -1f;

        void Awake()
        {
            Instance = this;
            GameState.PictureFade = 0f;
            Shader.SetGlobalFloat(AbPictureFade, 0f);
            if (panelSettings != null) _view = new ToolkitUiView(transform, panelSettings);
            else
            {
#if ABYSS_UGUI
                Debug.Log("[ABYSS] FadeController: no PanelSettings assigned, using the UGUI fallback");
                _view = new UguiUiView(transform);
#else
                Debug.Log("[ABYSS] FadeController: no PanelSettings assigned, using a runtime PanelSettings");
                _runtimePanel = UiShared.CreateRuntimePanelSettings();
                _view = new ToolkitUiView(transform, _runtimePanel);
#endif
            }
            _view.SetOverlay(1f);
        }

        PanelSettings _runtimePanel;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _view?.Dispose();
            if (_runtimePanel != null) Destroy(_runtimePanel);
            if (_pause.IsOpen) Time.timeScale = _savedTimeScale;
        }

        /// <summary>
        /// Called by GameBootstrap when the load is complete: starts the 14.11 timeline. skip = -nofade or capture mode
        /// (picture on at once, no title, no hint).
        /// </summary>
        public void BeginTitle(bool skip)
        {
            _skipTitle = skip;
            _ownClock = 0;
            _titleStart = Now();
            _startPos = GameState.PlayerPos;
            _moveTime = -1;
            _goalStart = -1;
            EndingComplete = false;
            CurrentPhase = Phase.Title;
            Debug.Log(skip ? "[ABYSS] title skipped (picture on)" : "[ABYSS] title timeline started");
        }

        double Now() => _useOwnClock ? _ownClock : SceneClock.RealTime;

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            _ownClock += udt;
            ProbeClock();

            if (CurrentPhase == Phase.Loading)
            {
                Publish(0f);
                return;
            }

            // ---------------------------------------------------------------- pause menu
            if (CurrentPhase == Phase.Title)
            {
                if (PauseOverlay.TogglePressed())
                {
                    if (_pause.IsOpen) Resume(); else OpenPause();
                }
                else if (_pause.IsOpen)
                {
                    PauseAction a = _pause.Poll(_view);
                    if (a == PauseAction.Resume) Resume();
                    else if (a == PauseAction.Restart) BeginRestart();
                    else if (a == PauseAction.Quit) Quit();
                }
            }
            _view.SetPause(_pause.IsOpen, _pause.Selected);

            // ---------------------------------------------------------------- timelines
            double now = Now();
            double t = now - _titleStart;
            TitleTime = (float)t;
            float picture = _skipTitle ? 1f : TitleCard.PictureFade(t);
            float title = _skipTitle ? 0f : TitleCard.TitleAlpha(t);

            bool cinematic = GameState.Cinematic || GameState.AutoTest || GameState.CaptureMode
                             || (AutoWalker.Instance != null && AutoWalker.Instance.IsRunning);
            // cinematic (from the start, or toggled with C while the hint shows) counts as the first movement: the hint never
            // appears, or fades out over 0.4 s instead of vanishing
            if (_moveTime < 0 && (cinematic || PlayerMoved())) _moveTime = t;
            float hint = _skipTitle ? 0f : TitleCard.HintAlpha(t, _moveTime);

            float endLine = 0f;
            if (GameState.GoalReached)
            {
                if (_goalStart < 0) _goalStart = !_useOwnClock && GameState.GoalRealTime >= 0 ? GameState.GoalRealTime : now;
                double tg = now - _goalStart;
                picture *= TitleCard.EndingPicture(tg);
                endLine = TitleCard.EndingLine(tg);
                if (!GameState.Ended && tg >= TitleCard.END_FADE_START + TitleCard.END_FADE_DURATION)
                {
                    GameState.Ended = true;
                    Debug.Log("[ABYSS] ending: picture black");
                }
                if (!EndingComplete && tg >= TitleCard.EndingDuration)
                {
                    EndingComplete = true;
                    Debug.Log("[ABYSS] ending complete");
                }
            }

            if (CurrentPhase == Phase.Restarting)
            {
                _restartT += udt;
                picture *= 1f - ProgressTables.Smootherstep(_restartT / RestartFadeSeconds);
                title = 0f; hint = 0f; endLine = 0f;
                if (_restartT >= RestartFadeSeconds + 0.05f) ReloadScene();
            }

            _view.SetTitle(title);
            _view.SetHint(hint);
            _view.SetEndLine(endLine);
            Publish(picture);
        }

        const float RestartFadeSeconds = 0.6f;

        void Publish(float picture)
        {
            picture = Mathf.Clamp01(picture);
            GameState.PictureFade = picture;
            if (picture != _lastFade)
            {
                _lastFade = picture;
                Shader.SetGlobalFloat(AbPictureFade, picture);
            }
            _view.SetOverlay(picture <= 0f ? 1f : 0f);
        }

        bool PlayerMoved()
        {
            Vector3 d = GameState.PlayerPos - _startPos;
            if (d.x * d.x + d.z * d.z > 0.01f) return true;
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed
                               || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed))
                return !_pause.IsOpen;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.09f && !_pause.IsOpen;
        }

        void ProbeClock()
        {
            if (_useOwnClock || CurrentPhase == Phase.Loading) return;
            if (double.IsNaN(_clockProbeReal) || SceneClock.RealTime != _clockProbeReal || _pause.IsOpen)
            {
                _clockProbeReal = SceneClock.RealTime;
                _clockProbeOwn = _ownClock;
                return;
            }
            if (_ownClock - _clockProbeOwn > 2.0)
            {
                // SceneClock.RealTime has not moved for 2 s of real time: continue the timeline on our own clock from where it stood.
                double elapsed = SceneClock.RealTime - _titleStart;
                _useOwnClock = true;
                _titleStart = _ownClock - System.Math.Max(0.0, elapsed);
                if (_goalStart >= 0) _goalStart = _ownClock - (SceneClock.RealTime - _goalStart);
                Debug.LogWarning("[ABYSS] FadeController: SceneClock.RealTime is not advancing; using the unscaled clock");
            }
        }

        // ---------------------------------------------------------------- pause actions
        void OpenPause()
        {
            _pause.Open();
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            GameState.Paused = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Debug.Log("[ABYSS] paused");
        }

        void Resume()
        {
            if (!_pause.IsOpen) return;
            _pause.Close();
            Time.timeScale = _savedTimeScale;
            GameState.Paused = false;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Debug.Log("[ABYSS] resumed");
        }

        void BeginRestart()
        {
            _pause.Close();
            _view.SetPause(false, 0);
            CurrentPhase = Phase.Restarting;
            _restartT = 0f;
            Debug.Log("[ABYSS] restart requested");
        }

        void ReloadScene()
        {
            Time.timeScale = _savedTimeScale;
            GameState.Paused = false;
            Scene s = SceneManager.GetActiveScene();
            if (s.buildIndex >= 0) SceneManager.LoadScene(s.buildIndex);
            else SceneManager.LoadScene(s.name);
            enabled = false;
        }

        void Quit()
        {
            Debug.Log("[ABYSS] quit from the pause menu");
            Time.timeScale = _savedTimeScale;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }
    }
}
