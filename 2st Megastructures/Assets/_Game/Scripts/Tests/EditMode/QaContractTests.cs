using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Megastructure.Tests
{
    /// <summary>
    /// The QA folder reaches modules owned by other builders only through reflection (no compile dependency), so a
    /// renamed member would silently degrade the autotest, the wall test or the captures. These tests pin the members
    /// QA resolves today (DEVELOPMENT.md, QA contract): AutoWalker.Request(WalkMode, float) with AutoTest / Cinematic,
    /// PathGuide.LaneHalfWidth / LaneSamplePoints, GazeSchedule.Evaluate, CameraRig.Violations, PlayerController.SafetySnaps,
    /// AtmosphereDriver.FovOverride and the static SubmitAllFor(Camera) methods of the code-drawn fields.
    /// </summary>
    public class QaContractTests
    {
        static Type Need(string name)
        {
            var t = QaUtil.FindType(name);
            if (t == null) Assert.Ignore(name + " does not exist yet");
            return t;
        }

        [Test]
        public void AutoWalker_RequestAcceptsQaModes()
        {
            var t = Need("AutoWalker");
            MethodInfo req = null;
            foreach (var m in t.GetMethods(QaUtil.AnyStatic))
                if (m.Name == "Request" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.IsEnum) req = m;
            Assert.IsNotNull(req, "static AutoWalker.Request(WalkMode mode, float speed)");
            var e = req.GetParameters()[0].ParameterType;
            Assert.IsTrue(Enum.IsDefined(e, "AutoTest"), e.Name + ".AutoTest");
            Assert.IsTrue(Enum.IsDefined(e, "Cinematic"), e.Name + ".Cinematic");
            var p = t.GetProperty("Active", QaUtil.AnyStatic);
            Assert.IsTrue(p != null && p.PropertyType == typeof(bool), "static bool AutoWalker.Active");
        }

        [Test]
        public void PathGuide_LaneWidthsAndSamples()
        {
            Need("PathGuide");
            foreach (float s in new[] { 1f, 9.7f, 17f, 60f, 150f, 400f, 550f, 585f, 650f, 772f, 800f, 930f })
            {
                Assert.IsTrue(RingQaHooks.TryLaneHalfWidth(s, out float hw), "PathGuide.LaneHalfWidth(" + s + ")");
                Assert.Greater(hw, 0.3f, "lane half width at s " + s);
                Assert.Less(hw, 15f, "lane half width at s " + s);
            }
            StringAssert.Contains("PathGuide", RingQaHooks.LaneSource);
            var pts = RingQaHooks.LaneSamplePoints(0.5f, out string src);
            Assert.IsNotNull(pts, "PathGuide.LaneSamplePoints(float) returning IEnumerable<Vector3>");
            int n = 0;
            foreach (var p in pts) { if (++n > 10) break; Assert.IsFalse(float.IsNaN(p.y)); }
            Assert.Greater(n, 10, "lane sample grid has points");
        }

        [Test]
        public void GazeSchedule_Resolves()
        {
            Need("GazeSchedule");
            Assert.IsTrue(RingQaHooks.TryGaze(150f, out float yaw, out float pitch), "GazeSchedule.Evaluate resolved by reflection");
            Assert.IsFalse(float.IsNaN(yaw) || float.IsNaN(pitch));
            Assert.LessOrEqual(Mathf.Abs(pitch), 89f);
        }

        [Test]
        public void CameraRig_PlayerController_AtmosphereDriver_Statics()
        {
            Need("CameraRig");
            int v = RingQaHooks.SafetyViolations(out string src);
            Assert.GreaterOrEqual(v, 0, "a static camera-violation counter (" + src + ")");
            StringAssert.Contains("CameraRig", src);
            Need("PlayerController");
            Assert.GreaterOrEqual(RingQaHooks.SafetySnaps(out string snapSrc), 0, "PlayerController.SafetySnaps (" + snapSrc + ")");
            Need("AtmosphereDriver");
            Assert.IsTrue(RingQaHooks.SetFovOverride(float.NaN), "static float AtmosphereDriver.FovOverride");
        }

        [Test]
        public void CaptureHooks_FindTheFieldSubmitters()
        {
            var ms = RingCaptureHooks.Submitters();
            if (ms.Length == 0) Assert.Ignore("no SubmitAllFor(Camera) exists yet");
            foreach (var m in ms)
            {
                Assert.IsTrue(m.IsStatic && m.ReturnType == typeof(void), m.DeclaringType.Name + ".SubmitAllFor is static void");
                var ps = m.GetParameters();
                Assert.AreEqual(1, ps.Length); Assert.AreEqual(typeof(Camera), ps[0].ParameterType);
            }
            TestContext.WriteLine(RingCaptureHooks.Method);
        }

        [Test]
        public void QaJson_WritesValidJson()
        {
            var j = new QaJson();
            j.Obj().KV("a", 1).KV("b", "x\"y\n").KV("c", float.NaN).Key("d").Arr().V(1.5f).V(true).EndArr().KV("e", new Vector3(1, 2, 3)).EndObj();
            Assert.AreEqual("{\"a\":1,\"b\":\"x\\\"y\\n\",\"c\":null,\"d\":[1.5,true],\"e\":[1,2,3]}", j.ToString());
        }
    }
}
