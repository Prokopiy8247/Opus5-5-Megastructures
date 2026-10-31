using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// -firstlast [DIR] (Part 10 item 22: first and last frame flashes). Window captures (ScreenCapture after
    /// WaitForEndOfFrame: the composed screen with post-processing and UI) of the first 60 frames from start-up (load, must be
    /// black), the first 60 frames after the load (the title fade-in, must start black and rise smoothly) and the last 60
    /// frames before the harness quits (autotest finish + 3 s, -quit-after, perf end). Outputs DIR/first, DIR/fadein,
    /// DIR/last (frame_000.png ...), each a valid sequence for Tools/qa/sequence_metrics.py. Default DIR: QA/firstlast/RUN.
    /// Not used in capture mode (capture mode skips the title). Use with -autotest or -quit-after.
    /// </summary>
    public sealed class QaFirstLast
    {
        public const int Frames = 60;
        public const float LastWindowSeconds = 1.6f;
        /// <summary>Title-card stills (DIR/title/title_2s.png, title_4s.png; critique R1-40).</summary>
        public static readonly float[] TitleMarks = { 2f, 4f };

        readonly QaHarness _h;
        readonly string _dir;
        int _lastIndex;
        readonly List<string> _lastFiles = new List<string>(256);

        public QaFirstLast(QaHarness h)
        {
            _h = h;
            string d = CliArgs.String(QaHarness.FirstLastFlag, null);
            _dir = string.IsNullOrEmpty(d) ? Path.Combine(QaPaths.OutDir, "firstlast", h.RunId) : QaPaths.Resolve(d);
        }

        public IEnumerator Run()
        {
            QaPaths.EnsureDir(_dir);
            string first = QaPaths.EnsureDir(Path.Combine(_dir, "first"));
            for (int i = 0; i < Frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Grab(Path.Combine(first, "frame_" + i.ToString("000") + ".png"));
            }
            while (!GameBootstrap.Loaded) yield return null;
            string fade = QaPaths.EnsureDir(Path.Combine(_dir, "fadein"));
            for (int i = 0; i < Frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Grab(Path.Combine(fade, "frame_" + i.ToString("000") + ".png"));
            }
            // R1-40: the title card (14.11) at 2 s and 4 s of the title timeline
            string title = QaPaths.EnsureDir(Path.Combine(_dir, "title"));
            for (int m = 0; m < TitleMarks.Length; m++)
            {
                float waited = 0f;
                FadeController fc = FadeController.Instance;
                while (fc != null && fc.TitleTime < TitleMarks[m] && waited < 30f)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }
                yield return new WaitForEndOfFrame();
                Grab(Path.Combine(title, "title_" + TitleMarks[m].ToString("0") + "s.png"));
            }
            string last = QaPaths.EnsureDir(Path.Combine(_dir, "last"));
            float qa = CliArgs.QuitAfter;
            while (true)
            {
                bool near = _h.QuitRemaining <= LastWindowSeconds || (qa > 0f && Time.realtimeSinceStartup >= qa - LastWindowSeconds);
                if (near) break;
                yield return null;
            }
            while (true)
            {
                yield return new WaitForEndOfFrame();
                string p = Path.Combine(last, "raw_" + _lastIndex.ToString("00000") + ".png");
                _lastIndex++;
                if (Grab(p)) _lastFiles.Add(p);
            }
        }

        static bool Grab(string path)
        {
            Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
            if (t == null) return false;
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.Destroy(t);
            return true;
        }

        /// <summary>Finalizer: keep the last 60 frames of the tail as last/frame_000..059.png.</summary>
        public int Finish(bool aborted)
        {
            int n = _lastFiles.Count;
            int keepFrom = Mathf.Max(0, n - Frames);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    if (i < keepFrom) File.Delete(_lastFiles[i]);
                    else
                    {
                        string dst = Path.Combine(Path.GetDirectoryName(_lastFiles[i]) ?? _dir, "frame_" + (i - keepFrom).ToString("000") + ".png");
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(_lastFiles[i], dst);
                    }
                }
                catch (IOException) { }
            }
            Debug.Log("[ABYSS] QA first/last frames -> " + _dir + " (last " + Mathf.Min(n, Frames) + " frames)");
            return 0;
        }
    }
}
