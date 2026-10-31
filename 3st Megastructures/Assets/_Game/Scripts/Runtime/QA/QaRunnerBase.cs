// QaRunnerBase: shared skeleton of the bots that drive the player (AutoTestRunner, WallBot, SoakRunner;
// MASTER_PROMPT 11.8, 14.10.4). Each runner activates only for its QaMode, waits for the world, runs,
// writes a JSON report into QaPaths.ReportDir and exits through QaExit with code 0 (pass) or 1 (fail).
using System;
using System.Collections;
using UnityEngine;

namespace Megastructure.QA
{
    public abstract class QaRunnerBase : MonoBehaviour
    {
        const double WorldTimeoutSeconds = 600.0;

        protected abstract QaRunMode Mode { get; }
        /// <summary>Report base name, e.g. "autotest_walk".</summary>
        protected abstract string ReportName { get; }

        protected readonly QaChecks Checks = new QaChecks();
        protected bool WorldOk;
        protected bool Finished;
        protected double RealStart;
        protected string StartedIso;
        protected bool CoverageRan;
        protected int CoverageMisses = -1;
        protected string CoverageNote = "";
        protected string Devices = "";

        protected virtual void Awake()
        {
            if (QaMode.Current != Mode) { enabled = false; return; }
            QaLog.Install();
            Application.runInBackground = true;
            StartedIso = QaFiles.IsoNow();
            RealStart = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>Adds runner T next to GameBootstrap when its mode is selected and the scene does not contain it.</summary>
        protected static void EnsureRunner<T>(QaRunMode mode) where T : QaRunnerBase
        {
            if (QaMode.Current != mode) return;
            if (FindFirstObjectByType<T>() != null) return;
            var host = GameBootstrap.Instance != null ? GameBootstrap.Instance.gameObject : new GameObject("~QA");
            host.AddComponent<T>();
            Debug.Log("[THREAD] QA: " + typeof(T).Name + " added at runtime (not in the scene)");
        }

        /// <summary>Waits for GameBootstrap.Ready; sets WorldOk.</summary>
        protected IEnumerator WaitForWorld()
        {
            double t0 = Time.realtimeSinceStartupAsDouble;
            while (!GameBootstrap.Ready)
            {
                if (Time.realtimeSinceStartupAsDouble - t0 > WorldTimeoutSeconds) { WorldOk = false; yield break; }
                yield return null;
            }
            WorldOk = true;
        }

        /// <summary>PathGuide ray-cast coverage (once, after the colliders exist) and the input devices.</summary>
        protected void RunStaticChecks()
        {
            Physics.SyncTransforms();
            CoverageRan = PathGuideProbe.TryCoverage(out CoverageMisses, out CoverageNote);
            if (!CoverageRan) Debug.LogWarning("[THREAD] QA: PathGuide coverage check skipped: " + CoverageNote);
            Devices = QaDrive.Devices();
            Debug.Log("[THREAD] QA input devices: " + Devices);
        }

        /// <summary>The checks every runner shares: zero error logs, no kill-volume events, full collider coverage.</summary>
        protected void AddCommonChecks()
        {
            int errors = QaLog.ErrorCount;
            Checks.Add("noErrorLogs", errors == 0, errors + " error/exception/assert messages");
            if (PathGuideProbe.TryGetKillCount(out int kills, out string kn)) Checks.Add("noKillVolumeEvents", kills == 0, "PathGuide.KillCount = " + kills);
            else { Checks.Add("noKillVolumeEvents", true, "skipped: " + kn, false); Debug.LogWarning("[THREAD] QA: kill-volume check skipped: " + kn); }
            if (CoverageRan) Checks.Add("colliderCoverage", CoverageMisses == 0, CoverageNote);
            else Checks.Add("colliderCoverage", true, "skipped: " + CoverageNote, false);
            Checks.Add("inputDevices", true, Devices, false);
            var gb = GameBootstrap.Instance;
            if (gb != null) Checks.Add("loadUnder15s", gb.LoadSeconds < LoadBudgetSeconds, gb.LoadSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s (T-LOAD)", false);
        }

        /// <summary>T-LOAD (14.10.1): generation under 15 s behind the black fade.</summary>
        public const double LoadBudgetSeconds = 15.0;

        /// <summary>Writes the report (header, the runner's body, checks, errors) and quits with 0 / 1.</summary>
        protected void Complete(string summary, Action<QaJson> body)
        {
            if (Finished) return;
            Finished = true;
            bool pass = Checks.AllRequiredOk;
            var j = new QaJson();
            j.BeginObject();
            j.Prop("tool", GetType().FullName).Prop("report", ReportName).Prop("pass", pass);
            j.Prop("started", StartedIso).Prop("finished", QaFiles.IsoNow());
            j.Prop("realSeconds", Time.realtimeSinceStartupAsDouble - RealStart);
            j.Prop("summary", summary);
            if (!pass) j.Prop("failed", Checks.FailedNames());
            QaEnvironment.Write(j);
            try { body?.Invoke(j); }
            catch (Exception e) { j.Prop("bodyError", e.Message); }
            Checks.Write(j);
            j.Prop("errorsLogged", QaLog.ErrorCount).Prop("warningsLogged", QaLog.WarningCount);
            j.Prop("errorMessages", QaLog.Messages());
            j.EndObject();
            string path = QaPaths.ReportPath(ReportName + ".json");
            QaFiles.WriteText(path, j.ToString());
            string tag = ReportTag;
            if (pass) Debug.Log("[THREAD] " + tag + " PASS " + summary + " report=" + path);
            else Debug.LogError("[THREAD] " + tag + " FAIL " + summary + " failed: " + Checks.FailedNames() + " report=" + path);
            QaExit.Quit(pass ? 0 : 1, ReportName);
        }

        /// <summary>Upper-case tag of the PASS/FAIL log line ("AUTOTEST", "WALLBOT", "SOAK").</summary>
        protected abstract string ReportTag { get; }
    }
}
