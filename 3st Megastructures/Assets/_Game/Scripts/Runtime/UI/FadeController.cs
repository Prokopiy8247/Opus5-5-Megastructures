// FadeController: title card, controls hint, ending and pause (MASTER_PROMPT Part 9, 14.11).
// The picture fade is done INSIDE the ThreadGrade pass: this component composes GradeState.FadeBlack every frame
// (intro, ending, kill-volume blink, pause dim) and the overlay canvas only draws text, so the text stays CREAM on
// black regardless of tone mapping. Timings follow 14.11 exactly:
//   intro   0.0-0.5 s black, picture 0.5-3.0 s (ease in-out); title THREAD in 1.0-2.0, hold 2.0-4.0, out 4.0-5.0;
//           hint at 6.0 s for 6 s, hidden at the first input, never in cinematic, capture, autotest or wall-bot runs.
//   ending  0-4 s fade to black (ease in-out), 4.0-5.5 black, 5.5-7.0 "the thread goes on" in, hold to 12.0,
//           out 12.0-14.0, then black with Esc (pause overlay: Resume, Restart, Quit).
// -nofade skips the intro (FadeBlack 0 immediately). Captures (-capture) get no UI at all and FadeBlack = 0.
// Pause (Esc / gamepad start): Time.timeScale = 0, audio duck, cursor visible; all timers here run on unscaled time
// and stop while paused. Zero allocations per frame (text rows are rebuilt only when the resolution changes).
using System;
using Megastructure.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megastructure.UI
{
    [DefaultExecutionOrder(-800)]
    public class FadeController : MonoBehaviour, IFadeUI
    {
        public static FadeController Instance { get; private set; }

        // Presentation details not fixed by 14.11 (cited where they come from).
        public const double HintFadeIn = 0.5;          // the hint eases in at 6.0 s ...
        public const double HintFadeOut = 0.5;         // ... and out at 12.0 s
        public const double HintHideTime = 0.4;        // fade when the first input hides it
        public const double MaxUiStep = 1.0 / 30.0;    // a load hitch never skips part of a fade
        public const float PauseDim = 0.55f;           // picture dim behind the pause menu (inside the grade)
        public const double PauseFade = 0.25;          // menu and dim ease (s, unscaled)
        public const double BlinkHold = 0.15, BlinkOut = 0.45;   // kill-volume blink: instant black, hold, ease out
        public const int SortingOrder = 1000;

        bool m_capture, m_noFade;
        bool m_introStarted, m_introActive;
        double m_introT;
        bool m_showHint, m_hintCancelled, m_hintHiding;
        double m_hintHide = 1.0;
        bool m_ending;
        double m_endT;
        float m_base = 1f;       // black level while no intro/ending drives it (load: 1, -nofade: 0)
        float m_black = 1f;
        bool m_paused;
        float m_pauseBlend;
        float m_savedTimeScale = 1f;
        bool m_subscribed;

        static double s_blinkT = -1.0;

        Canvas m_canvas;
        RectTransform m_titleRow, m_endRow, m_hintRow;
        CanvasGroup m_titleGroup, m_endGroup, m_hintGroup;
        PauseOverlay m_pause;
        int m_lastW, m_lastH;

        // ---------------------------------------------------------------- IFadeUI
        public float Black => m_black;
        public bool Paused => m_paused;
        public bool EndingActive => m_ending;
        public double EndingTime => m_endT;
        public double IntroTime => m_introT;
        public bool HintVisible => m_hintGroup != null && m_hintGroup.alpha > 0f;

        void Awake()
        {
            Instance = this;
            ThreadServices.Fade = this;
            m_capture = CliArgs.CaptureDir != null;
            m_noFade = CliArgs.NoFade;
            if (m_capture || m_noFade) { m_base = 0f; m_black = 0f; GradeState.FadeBlack = 0f; }
            else GradeState.FadeBlack = 1f;
            if (!m_capture) BuildCanvas();
        }

        void OnEnable()
        {
            if (!m_subscribed) { GameBootstrap.WorldReady += OnWorldReady; m_subscribed = true; }
        }

        void OnDisable()
        {
            if (m_subscribed) { GameBootstrap.WorldReady -= OnWorldReady; m_subscribed = false; }
        }

        void Start()
        {
            if (GameBootstrap.Instance == null || GameBootstrap.Ready) OnWorldReady();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(ThreadServices.Fade, this)) ThreadServices.Fade = null;
            if (Instance == this) Instance = null;
            if (m_paused) Time.timeScale = m_savedTimeScale;
            if (m_canvas != null) Destroy(m_canvas.gameObject);
        }

        void OnWorldReady()
        {
            bool hint = !CliArgs.Cinematic && !CliArgs.AutoTest && !CliArgs.WallBot && !m_capture;
            BeginIntro(hint);
        }

        // ---------------------------------------------------------------- API
        /// <summary>Starts the intro once (later calls can only withdraw the hint).</summary>
        public void BeginIntro(bool showHint)
        {
            if (m_capture) return;
            if (m_introStarted) { m_showHint &= showHint; return; }
            m_introStarted = true;
            m_introT = 0;
            m_introActive = !m_noFade;
            m_showHint = showHint && !m_noFade;
        }

        public void BeginEnding()
        {
            if (m_capture || m_ending) return;
            m_ending = true;
            m_endT = 0;
            HideHint();
            Debug.Log("[THREAD] ending: fade to black");
        }

        public void HideHint()
        {
            if (m_hintCancelled) return;
            m_hintCancelled = true;
            m_hintHiding = true;
        }

        public void SetBlackImmediate(float black)
        {
            m_introActive = false;
            m_base = Mathf.Clamp01(black);
            Compose();
        }

        /// <summary>Kill-volume blink (PathGuide): instant black, short hold, ease back. Safe without an instance.</summary>
        public static void Blink() { s_blinkT = 0.0; }

        // ---------------------------------------------------------------- frame
        void Update()
        {
            double dt = Math.Min(Time.unscaledDeltaTime, MaxUiStep);

            if (!m_capture) HandlePause(dt);

            if (!m_paused)
            {
                if (m_introStarted) m_introT += dt;
                if (m_ending) m_endT += dt;
            }
            if (s_blinkT >= 0.0)
            {
                s_blinkT += dt;
                if (s_blinkT > BlinkHold + BlinkOut) s_blinkT = -1.0;
            }
            float target = m_paused ? 1f : 0f;
            m_pauseBlend = Mathf.MoveTowards(m_pauseBlend, target, (float)(dt / PauseFade));

            UpdateHint(dt);
            Compose();
            if (!m_capture) UpdateText();
        }

        void Compose()
        {
            float intro = m_introActive ? 1f - (float)ThreadMath.Smoothstep(ThreadConstants.FadeInStart, ThreadConstants.FadeInEnd, m_introT) : m_base;
            float end = m_ending ? (float)ThreadMath.Smoothstep(0.0, ThreadConstants.EndFade, m_endT) : 0f;
            float blink = 0f;
            if (s_blinkT >= 0.0) blink = s_blinkT <= BlinkHold ? 1f : 1f - (float)ThreadMath.Smoothstep(BlinkHold, BlinkHold + BlinkOut, s_blinkT);
            float dim = PauseDim * (float)ThreadMath.Smoothstep(0, 1, m_pauseBlend);
            float b = Mathf.Max(Mathf.Max(intro, end), Mathf.Max(blink, dim));
            m_black = b;
            if (!m_capture) GradeState.FadeBlack = b;
        }

        void UpdateHint(double dt)
        {
            if (!m_showHint || m_hintCancelled && !m_hintHiding) return;
            if (!m_hintCancelled)
            {
                var aw = AutoWalker.Instance;
                if (aw != null && aw.Active) { HideHint(); return; }
                if (m_introT > ThreadConstants.HintStart + ThreadConstants.HintDuration)
                {
                    m_hintCancelled = true; m_hintHiding = false;   // its 6 s are over: stop polling
                }
                else if (m_introT < ThreadConstants.HintStart)
                {
                    // walking before the hint appears: the player already knows, never show it
                    if (!m_paused && ThreadInput.MoveActive()) { m_hintCancelled = true; m_hintHiding = false; m_hintHide = 0; }
                }
                else if (!m_paused && (ThreadInput.MoveActive() || ThreadInput.AnyCancelInput(PlayerTuning.LookThresholdCounts, PlayerTuning.StickThreshold)))
                {
                    HideHint();   // the first input once it is visible
                }
            }
            if (m_hintHiding)
            {
                m_hintHide = Math.Max(0.0, m_hintHide - dt / HintHideTime);
                if (m_hintHide <= 0.0) m_hintHiding = false;
            }
        }

        void UpdateText()
        {
            if (m_canvas == null) return;
            if (Screen.width != m_lastW || Screen.height != m_lastH) RebuildText();

            double t = m_introT;
            float title = 0f, hint = 0f, end = 0f;
            if (m_introActive)
            {
                title = (float)(ThreadMath.Smoothstep(ThreadConstants.TitleIn0, ThreadConstants.TitleIn1, t) * (1.0 - ThreadMath.Smoothstep(ThreadConstants.TitleOut0, ThreadConstants.TitleOut1, t)));
                if (m_showHint)
                {
                    double h0 = ThreadConstants.HintStart, h1 = ThreadConstants.HintStart + ThreadConstants.HintDuration;
                    hint = (float)(ThreadMath.Smoothstep(h0, h0 + HintFadeIn, t) * (1.0 - ThreadMath.Smoothstep(h1 - HintFadeOut, h1, t)) * m_hintHide);
                }
            }
            if (m_ending)
            {
                title = 0f; hint = 0f;
                end = (float)(ThreadMath.Smoothstep(ThreadConstants.EndLineIn0, ThreadConstants.EndLineIn1, m_endT) * (1.0 - ThreadMath.Smoothstep(ThreadConstants.EndLineOut0, ThreadConstants.EndLineOut1, m_endT)));
            }
            // the pause menu shares the title's place on screen: the other lines step back while it is open
            float menu = (float)ThreadMath.Smoothstep(0, 1, m_pauseBlend);
            float under = 1f - menu;
            SetGroup(m_titleGroup, title * TitleAlpha * under);
            SetGroup(m_hintGroup, hint * HintAlpha * under);
            SetGroup(m_endGroup, end * TitleAlpha * under);
            if (m_pause != null) m_pause.SetAlpha(menu);
            if (m_pause != null && !m_paused && m_pauseBlend <= 0f) m_pause.Close();
        }

        // 14.11 (MASTER_PROMPT line 995): title and ending CREAM at 90 percent alpha, the hint at 70 percent.
        const float TitleAlpha = 0.9f;
        const float HintAlpha = 0.7f;

        static void SetGroup(CanvasGroup g, float a)
        {
            if (g == null) return;
            bool on = a > 0.0005f;
            if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
            if (on && g.alpha != a) g.alpha = a;
        }

        // ---------------------------------------------------------------- pause
        void HandlePause(double dt)
        {
            if (!m_paused)
            {
                if (ThreadInput.PausePressed()) SetPaused(true);
                return;
            }
            if (ThreadInput.PausePressed()) { SetPaused(false); return; }
            var action = m_pause != null ? m_pause.Tick((float)dt) : PauseOverlay.Action.None;
            switch (action)
            {
                case PauseOverlay.Action.Resume: SetPaused(false); break;
                case PauseOverlay.Action.Restart: Restart(); break;
                case PauseOverlay.Action.Quit: Quit(); break;
            }
        }

        void SetPaused(bool paused)
        {
            if (paused == m_paused) return;
            m_paused = paused;
            if (paused)
            {
                m_savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                ThreadServices.Audio?.SetPauseDuck(true);
                ThreadInput.SetCursorLocked(false);
                if (m_pause != null) m_pause.Open();
            }
            else
            {
                Time.timeScale = m_savedTimeScale;
                ThreadServices.Audio?.SetPauseDuck(false);
                ThreadInput.SetCursorLocked(true);
            }
        }

        void Restart()
        {
            Time.timeScale = m_savedTimeScale;
            m_paused = false;
            ThreadServices.Audio?.SetPauseDuck(false);
            ThreadInput.SetCursorLocked(true);
            var scene = SceneManager.GetActiveScene();
            Debug.Log("[THREAD] restart");
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        static void Quit()
        {
            Debug.Log("[THREAD] quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- canvas
        void BuildCanvas()
        {
            m_canvas = GlyphText.CreateOverlayCanvas("ThreadUI", SortingOrder);
            Transform root = m_canvas.transform;
            var titleLayer = GlyphText.CreateLayer(root, "Title", out m_titleGroup);
            m_titleRow = GlyphText.CreateRow(titleLayer, "row", new Vector2(0.5f, (float)(1.0 - ThreadConstants.TitleYFrac)));
            var endLayer = GlyphText.CreateLayer(root, "Ending", out m_endGroup);
            m_endRow = GlyphText.CreateRow(endLayer, "row", new Vector2(0.5f, 0.5f));
            var hintLayer = GlyphText.CreateLayer(root, "Hint", out m_hintGroup);
            m_hintRow = GlyphText.CreateRow(hintLayer, "row", new Vector2(0.5f, (float)ThreadConstants.HintYFracFromBottom));
            m_pause = new PauseOverlay();
            m_pause.Create(root);
            m_titleGroup.gameObject.SetActive(false);
            m_endGroup.gameObject.SetActive(false);
            m_hintGroup.gameObject.SetActive(false);
            RebuildText();
        }

        void RebuildText()
        {
            int w = Screen.width, h = Screen.height;
            m_lastW = w; m_lastH = h;
            if (w <= 0 || h <= 0) return;
            Color c = GlyphText.Cream;
            GlyphText.BuildRow(m_titleRow, ThreadConstants.TitleText, GlyphText.PixelSize(ThreadConstants.TitleHeightFrac, h), (float)ThreadConstants.TitleSpacingEm, c);
            GlyphText.BuildRow(m_endRow, ThreadConstants.EndText, GlyphText.PixelSize(ThreadConstants.EndHeightFrac, h), (float)ThreadConstants.EndSpacingEm, c);
            GlyphText.BuildLine(m_hintRow, ThreadConstants.HintText, GlyphText.PixelSize(ThreadConstants.HintHeightFrac, h), c, w);
            m_pause.Rebuild(h);
        }
    }
}
