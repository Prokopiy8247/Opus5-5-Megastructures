using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Runs every EditMode test through TestRunnerApi and writes QA/tests/editmode.xml (NUnit-3-like) and
    /// QA/tests/editmode-summary.txt. The run is asynchronous: launch BatchTools.RunEditModeTests WITHOUT -quit; the
    /// Editor exits from RunFinished. Callbacks are re-registered after a domain reload (SessionState flag), and a
    /// watchdog exits with <see cref="ExitTimeout"/> if the run never finishes.
    /// Exit codes: 0 all passed (at least one test), 1 failures, 2 no test ran, 3 run error / compile error, 4 timeout.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerBatch
    {
        public const int ExitPassed = 0, ExitFailed = 1, ExitNoTests = 2, ExitRunError = 3, ExitTimeout = 4;
        const double TimeoutSeconds = 1800.0;   // 30 min watchdog for a hung run
        const string KeyActive = "Megastructure.TestRunnerBatch.Active";
        const string KeyStart = "Megastructure.TestRunnerBatch.Start";

        public static string XmlPath => Path.Combine(BatchTools.ProjectRoot, "QA", "tests", "editmode.xml");
        public static string SummaryPath => Path.Combine(BatchTools.ProjectRoot, "QA", "tests", "editmode-summary.txt");

        static Callbacks s_Callbacks;
        static bool s_Done;

        static TestRunnerBatch()
        {
            // After a domain reload during the run the in-memory callback registration is lost: register again.
            if (SessionState.GetBool(KeyActive, false)) EditorApplication.delayCall += Register;
        }

        public static void Start()
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                BatchLog.Error("TESTS: script compilation failed, no test run");
                WriteFailureSummary("script compilation failed");
                BatchLog.Finish("RunEditModeTests", ExitRunError);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(XmlPath));
            SessionState.SetBool(KeyActive, true);
            SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
            s_Done = false;
            Register();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var settings = new ExecutionSettings(new Filter { testMode = TestMode.EditMode }) { runSynchronously = false };
            string id = api.Execute(settings);
            BatchLog.Info("TESTS: EditMode run scheduled (job " + id + "); waiting for RunFinished (results -> " + XmlPath + ")");
        }

        static void Register()
        {
            if (s_Callbacks != null) return;
            s_Callbacks = new Callbacks();
            TestRunnerApi.RegisterTestCallback(s_Callbacks);
            EditorApplication.update -= Watchdog;
            EditorApplication.update += Watchdog;
        }

        static void Watchdog()
        {
            if (s_Done || !SessionState.GetBool(KeyActive, false)) return;
            double start = SessionState.GetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
            if (EditorApplication.timeSinceStartup - start < TimeoutSeconds) return;
            BatchLog.Error("TESTS: no RunFinished after " + TimeoutSeconds + " s");
            WriteFailureSummary("timeout after " + TimeoutSeconds + " s");
            Complete(ExitTimeout);
        }

        static void Complete(int code)
        {
            if (s_Done) return;
            s_Done = true;
            SessionState.EraseBool(KeyActive);
            EditorApplication.update -= Watchdog;
            if (s_Callbacks != null) { TestRunnerApi.UnregisterTestCallback(s_Callbacks); s_Callbacks = null; }
            BatchLog.Finish("RunEditModeTests", code);
        }

        public static void WriteFailureSummary(string reason)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
                File.WriteAllText(SummaryPath, "result=Error\nreason=" + reason + "\ntime=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + "\n", new UTF8Encoding(false));
            }
            catch (Exception e) { BatchLog.Exception("WriteFailureSummary", e); }
        }

        // ------------------------------------------------------------------------------------------------------
        sealed class Callbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                BatchLog.Info("TESTS: run started, " + (testsToRun != null ? testsToRun.TestCaseCount : 0) + " test cases");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int code;
                try { code = Report(result); }
                catch (Exception e) { BatchLog.Exception("TESTS RunFinished", e); code = ExitRunError; }
                Complete(code);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result == null || result.HasChildren) return;
                if (result.TestStatus == TestStatus.Failed)
                    BatchLog.Info("TEST FAILED " + result.FullName + ": " + FirstLine(result.Message));
            }

            public void OnError(string message)
            {
                BatchLog.Error("TESTS: run error: " + message);
                WriteFailureSummary("run error: " + message);
                Complete(ExitRunError);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        sealed class Counts { public int Total, Passed, Failed, Skipped, Inconclusive; public readonly List<ITestResultAdaptor> Failures = new List<ITestResultAdaptor>(); }

        static int Report(ITestResultAdaptor root)
        {
            var c = new Counts();
            if (root != null) Count(root, c);
            WriteXml(root, c);
            WriteSummary(root, c);
            BatchLog.Info("TESTS RESULT total=" + c.Total + " passed=" + c.Passed + " failed=" + c.Failed + " skipped=" + c.Skipped
                          + " inconclusive=" + c.Inconclusive + " duration=" + (root != null ? root.Duration.ToString("0.00", CultureInfo.InvariantCulture) : "0") + " s");
            foreach (var f in c.Failures) BatchLog.Info("TEST FAILED " + f.FullName + ": " + FirstLine(f.Message));
            if (c.Failed > 0) return ExitFailed;
            if (c.Total == 0) { BatchLog.Warn("TESTS: no EditMode test was run"); return ExitNoTests; }
            return ExitPassed;
        }

        static bool IsLeaf(ITestResultAdaptor r) => !r.HasChildren && (r.Test == null || !r.Test.IsSuite);

        static void Count(ITestResultAdaptor r, Counts c)
        {
            if (IsLeaf(r))
            {
                c.Total++;
                switch (r.TestStatus)
                {
                    case TestStatus.Passed: c.Passed++; break;
                    case TestStatus.Failed: c.Failed++; c.Failures.Add(r); break;
                    case TestStatus.Skipped: c.Skipped++; break;
                    default: c.Inconclusive++; break;
                }
                return;
            }
            if (r.Children == null) return;
            foreach (var ch in r.Children) Count(ch, c);
        }

        static void WriteXml(ITestResultAdaptor root, Counts c)
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (var w = XmlWriter.Create(XmlPath, settings))
            {
                w.WriteStartDocument();
                w.WriteStartElement("test-run");
                w.WriteAttributeString("id", "2");
                w.WriteAttributeString("testcasecount", c.Total.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("result", c.Failed > 0 ? "Failed" : (c.Total == 0 ? "Inconclusive" : "Passed"));
                w.WriteAttributeString("total", c.Total.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("passed", c.Passed.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("failed", c.Failed.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("inconclusive", c.Inconclusive.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("skipped", c.Skipped.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("engine-version", "3.5.0.0");
                w.WriteAttributeString("clr-version", Environment.Version.ToString());
                if (root != null)
                {
                    w.WriteAttributeString("start-time", root.StartTime.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture));
                    w.WriteAttributeString("end-time", root.EndTime.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture));
                    w.WriteAttributeString("duration", root.Duration.ToString("0.######", CultureInfo.InvariantCulture));
                    WriteNode(w, root);
                }
                w.WriteEndElement();
                w.WriteEndDocument();
            }
        }

        static void WriteNode(XmlWriter w, ITestResultAdaptor r)
        {
            bool leaf = IsLeaf(r);
            w.WriteStartElement(leaf ? "test-case" : "test-suite");
            if (!leaf) w.WriteAttributeString("type", r.Test != null && r.Test.IsTestAssembly ? "Assembly" : "TestSuite");
            w.WriteAttributeString("name", r.Name ?? "");
            w.WriteAttributeString("fullname", r.FullName ?? "");
            w.WriteAttributeString("result", r.TestStatus.ToString());
            if (!string.IsNullOrEmpty(r.ResultState)) w.WriteAttributeString("label", r.ResultState);
            w.WriteAttributeString("duration", r.Duration.ToString("0.######", CultureInfo.InvariantCulture));
            w.WriteAttributeString("asserts", r.AssertCount.ToString(CultureInfo.InvariantCulture));
            if (!leaf)
            {
                w.WriteAttributeString("total", (r.PassCount + r.FailCount + r.SkipCount + r.InconclusiveCount).ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("passed", r.PassCount.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("failed", r.FailCount.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("skipped", r.SkipCount.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("inconclusive", r.InconclusiveCount.ToString(CultureInfo.InvariantCulture));
            }
            if (r.TestStatus == TestStatus.Failed && (!string.IsNullOrEmpty(r.Message) || !string.IsNullOrEmpty(r.StackTrace)))
            {
                w.WriteStartElement("failure");
                w.WriteElementString("message", Clean(r.Message));
                w.WriteElementString("stack-trace", Clean(r.StackTrace));
                w.WriteEndElement();
            }
            else if (r.TestStatus == TestStatus.Skipped && !string.IsNullOrEmpty(r.Message))
            {
                w.WriteStartElement("reason");
                w.WriteElementString("message", Clean(r.Message));
                w.WriteEndElement();
            }
            if (leaf && !string.IsNullOrEmpty(r.Output)) w.WriteElementString("output", Clean(r.Output));
            if (!leaf && r.Children != null)
                foreach (var ch in r.Children) WriteNode(w, ch);
            w.WriteEndElement();
        }

        static void WriteSummary(ITestResultAdaptor root, Counts c)
        {
            var sb = new StringBuilder();
            sb.Append("result=").Append(c.Failed > 0 ? "Failed" : (c.Total == 0 ? "NoTests" : "Passed")).Append('\n');
            sb.Append("total=").Append(c.Total).Append('\n');
            sb.Append("passed=").Append(c.Passed).Append('\n');
            sb.Append("failed=").Append(c.Failed).Append('\n');
            sb.Append("skipped=").Append(c.Skipped).Append('\n');
            sb.Append("inconclusive=").Append(c.Inconclusive).Append('\n');
            sb.Append("duration_s=").Append(root != null ? root.Duration.ToString("0.00", CultureInfo.InvariantCulture) : "0").Append('\n');
            sb.Append("time=").Append(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            foreach (var f in c.Failures)
            {
                sb.Append("\nFAILED ").Append(f.FullName).Append('\n');
                sb.Append("  message: ").Append(Clean(f.Message).Replace("\n", "\n           ")).Append('\n');
                var st = Clean(f.StackTrace);
                if (st.Length > 0) sb.Append("  stack: ").Append(st.Replace("\n", "\n         ")).Append('\n');
            }
            File.WriteAllText(SummaryPath, sb.ToString(), new UTF8Encoding(false));
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n).TrimEnd('\r');
        }

        /// <summary>Removes characters XML 1.0 cannot hold and normalises line breaks.</summary>
        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Replace("\r\n", "\n"))
                if (ch == '\n' || ch == '\t' || (ch >= 0x20 && ch != 0xFFFE && ch != 0xFFFF)) sb.Append(ch);
            return sb.ToString().TrimEnd();
        }
    }
}
