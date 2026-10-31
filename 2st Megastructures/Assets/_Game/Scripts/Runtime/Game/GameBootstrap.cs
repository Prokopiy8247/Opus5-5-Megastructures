using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Megastructure
{
    /// <summary>
    /// Entry point of Main.unity (Part 7.3, 7.4, 14.6). Awake (execution order -1000, before every other system):
    /// resets RingState, RingRig and RingEvents and the module statics, publishes RingAssets.Instance, parses the
    /// command line and applies the QualityBootstrap. Start runs Boot(): one frame, the WorldBuilder (all geometry, timed,
    /// triangles logged per builder, budget 15 s), static tables and shader warm-up, the pre-warm of every set at its
    /// key pose (3 rendered frames each, behind the black fade), the player placement (-progress / -beat, else P0
    /// facing +Z), then RingRig.WorldReady = true and RingEvents.RaiseLoadComplete(). During loading RingState.Dt is 0.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] public RingAssets assets;

        const float LoadBudgetS = 15f;   // Part 7.4: load time at most 15 s
        const int PrewarmFrames = 3;     // 14.6: 3 rendered frames per set

        /// <summary>Wall-clock load time of the last boot (s).</summary>
        public static double LoadSeconds { get; private set; }

        /// <summary>Start the cinematic autowalk as soon as the next load completes (set by <see cref="Restart"/> callers,
        /// e.g. the QA soak: restart, then cinematic). Survives the scene reload; consumed by the PlayerController.</summary>
        public static bool CinematicOnLoad;
        /// <summary>True between <see cref="Restart"/> and the reloaded scene's Awake.</summary>
        public static bool Restarting { get; private set; }

        /// <summary>Restarts the walk from s 0 by reloading the gameplay scene (the same path as the pause menu's
        /// Restart: every static is reset by the reloaded bootstrap, the world is rebuilt behind the black fade).</summary>
        public static void Restart()
        {
            Restarting = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Debug.Log("[Ring] GameBootstrap: restart (scene reload)");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        struct Pose { public RingSet Set; public float S; public Vector3 Eye; public float Yaw, Pitch; public string Tag; }

        // 14.6 pre-warm: each set at its key pose (14.10 key-shot table)
        static readonly Pose[] PrewarmPoses =
        {
            new Pose { Set = RingSet.Capsule, S = 5.1f, Eye = new Vector3(-0.88f, 2.60f, 5.07f), Yaw = -45f, Pitch = 25f, Tag = "KS01" },
            new Pose { Set = RingSet.FarValley, S = 150f, Eye = new Vector3(0f, 4.7f, 150f), Yaw = 0f, Pitch = 14f, Tag = "KS02" },
            new Pose { Set = RingSet.NearValley, S = 150f, Eye = new Vector3(0f, 4.7f, 150f), Yaw = 0f, Pitch = 14f, Tag = "KS02" },
            new Pose { Set = RingSet.Ark, S = 364f, Eye = new Vector3(0f, 1.7f, 364f), Yaw = -12f, Pitch = 14f, Tag = "KS04" },
            new Pose { Set = RingSet.NearPlain, S = 606f, Eye = new Vector3(30f, 1.7f, 576f), Yaw = 80f, Pitch = 16f, Tag = "KS07" },
            new Pose { Set = RingSet.Tunnel, S = 800f, Eye = new Vector3(140f, 1.7f, 660f), Yaw = 90f, Pitch = 0f, Tag = "KS10" },
            new Pose { Set = RingSet.Outside, S = 912f, Eye = new Vector3(252f, 1.7f, 660f), Yaw = 92f, Pitch = 9f, Tag = "KS12" },
        };

        bool _booted;

        void Awake()
        {
            Restarting = false;
            RingState.Reset();
            RingRig.Reset();
            RingEvents.Reset();
            ResetModuleStatics();
            RingAssets.Instance = assets;
            if (assets == null) Debug.LogError("[Ring] GameBootstrap: RingAssets is not assigned");
            CliArgs.Parse();
            RingState.AutoTest = CliArgs.AutoTest;
            RingState.SetOverride = CliArgs.SetOverride;
            QualityBootstrap.Apply();
            EnsureSystems();
        }

        static void ResetModuleStatics()
        {
            QualityBootstrap.ResetStatics();
            WorldBuilder.ResetStatics();
            PlayerController.ResetStatics();
            CameraRig.ResetStatics();
            AutoWalker.ResetStatics();
            ProgressDirector.ResetStatics();
            BeatDirector.ResetStatics();
            AtmosphereDriver.ResetStatics();
            LoadSeconds = 0;
        }

        /// <summary>Adds any system the scene lacks (the editor build normally creates all of them).</summary>
        void EnsureSystems()
        {
            T Need<T>() where T : Component
            {
                var c = FindAnyObjectByType<T>();
                if (c != null) return c;
                Debug.LogWarning("[Ring] GameBootstrap: scene has no " + typeof(T).Name + "; adding one");
                return gameObject.AddComponent<T>();
            }
            Need<QualityBootstrap>();
            Need<WorldBuilder>();
            Need<ProgressDirector>();
            Need<BeatDirector>();
            Need<AtmosphereDriver>();

            if (FindAnyObjectByType<PlayerController>() == null)
            {
                Debug.LogWarning("[Ring] GameBootstrap: scene has no player rig; creating one");
                var p = new GameObject("[Player]");
                p.transform.position = new Vector3(0f, RingConstants.CapsuleFloorY, 0f);
                p.AddComponent<CharacterController>();
                Camera cam = Camera.main;
                GameObject camGo;
                if (cam != null) { camGo = cam.gameObject; camGo.transform.SetParent(p.transform, true); }
                else
                {
                    camGo = new GameObject("Camera");
                    camGo.tag = "MainCamera";
                    camGo.transform.SetParent(p.transform, false);
                    cam = camGo.AddComponent<Camera>();
                    var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing = true;
                    camGo.AddComponent<AudioListener>();
                }
                camGo.transform.localPosition = new Vector3(0f, RingConstants.Eye, 0f);
                if (camGo.GetComponent<CameraRig>() == null) camGo.AddComponent<CameraRig>();
                p.AddComponent<PlayerController>();
                p.AddComponent<AutoWalker>();
            }
            else
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc.GetComponent<AutoWalker>() == null) pc.gameObject.AddComponent<AutoWalker>();
                if (FindAnyObjectByType<CameraRig>() == null)
                {
                    var cam = pc.GetComponentInChildren<Camera>();
                    if (cam == null) cam = Camera.main;
                    if (cam != null) cam.gameObject.AddComponent<CameraRig>();
                    else Debug.LogError("[Ring] GameBootstrap: no camera for the CameraRig");
                }
            }
        }

        void Start()
        {
            if (_booted) return;
            _booted = true;
            StartCoroutine(Boot());
        }

        /// <summary>The boot never dies half-way: every non-yield step is guarded (C# allows no yield inside a try with a
        /// catch, so the yields stay outside), the nested coroutines are stepped through <see cref="Guarded"/>, and the
        /// last three statements (Dt 0, WorldReady, LoadComplete) always run, so a failed step logs an error instead of
        /// leaving the game black without input.</summary>
        IEnumerator Boot()
        {
            var load = Stopwatch.StartNew();
            yield return null;

            // static tables used during the walk (never computed on first use mid-walk)
            try
            {
                SunsetModel.V(0.5f);
                RingPalette.Lin("CeramicWhite");
                _ = RingWorld.Cumulative;
                _ = AutoWalker.Path.Length + ProgressDirector.Path.Length;
                RingSchedule.Evaluate(0f, -4f, 0);
            }
            catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: static table warm-up failed: " + e); }

            WorldBuilder wb = null;
            try
            {
                wb = GetComponent<WorldBuilder>();
                if (wb == null) wb = FindAnyObjectByType<WorldBuilder>();
                if (wb == null) wb = gameObject.AddComponent<WorldBuilder>();
            }
            catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: no WorldBuilder: " + e); }
            if (wb != null) yield return Guarded(wb.BuildAll(assets), "world build");

            // shader warm-up (8.6), behind black
            if (assets != null && assets.shaderVariants != null)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    assets.shaderVariants.WarmUp();
                    Debug.Log("[Ring] shader variant collection warmed in " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms");
                }
                catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: shader warm-up failed: " + e); }
                yield return null;
            }

            yield return Guarded(Prewarm(), "pre-warm");

            // player placement (-progress X / -beat N, else P0 facing +Z)
            float startS = 0f;
            try { startS = PlacePlayer(); }
            catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: player placement failed: " + e); }
            // leave the pre-warm state even when the placement failed (sets re-initialised from RingState.S)
            try
            {
                var bd = BeatDirector.Instance;
                if (bd != null) bd.PrewarmAll(false, RingState.S);
            }
            catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: set initialisation failed: " + e); }

            try
            {
                load.Stop();
                LoadSeconds = load.Elapsed.TotalSeconds;
                string msg = "[Ring] load complete in " + LoadSeconds.ToString("0.00") + " s (world " + (WorldBuilder.TotalMs / 1000.0).ToString("0.00")
                             + " s), start s " + startS.ToString("0.0");
                if (LoadSeconds > LoadBudgetS) Debug.LogWarning(msg + " - over the " + LoadBudgetS + " s budget (Part 7.4)");
                else Debug.Log(msg);
            }
            catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: load report failed: " + e); }

            RingState.Dt = 0f;
            RingRig.WorldReady = true;
            RingEvents.RaiseLoadComplete();
        }

        /// <summary>Places the player at -progress / -beat (on the filleted path, facing along it) or at P0 facing +Z and
        /// runs the director chain once (RingState.S is then the start progress). Returns the start progress.</summary>
        static float PlacePlayer()
        {
            float startS = CliArgs.StartS();
            Vector3 feet; float yaw;
            if (!float.IsNaN(startS))
            {
                startS = Mathf.Clamp(startS, 0f, RingConstants.PathLength);
                var path = AutoWalker.Path;
                float d = path.DAtSharp(startS);
                Vector2 xz = path.PointAt(d);
                feet = new Vector3(xz.x, PathGuide.WalkHeight(xz.x, xz.y), xz.y);
                yaw = path.HeadingAt(d);
            }
            else
            {
                startS = 0f;
                feet = new Vector3(0f, RingConstants.CapsuleFloorY, 0f);
                yaw = 0f;
            }
            var pc = PlayerController.Instance;
            if (pc != null) pc.Teleport(feet, yaw);
            var rig = CameraRig.Instance;
            if (rig != null) { rig.SetLook(yaw, 0f); rig.SnapEye(); }
            ProgressDirector.SnapTo(startS);
            ProgressDirector.RunChain(0f);                     // RingState.S is now the start progress
            return startS;
        }

        IEnumerator Prewarm()
        {
            var bd = BeatDirector.Instance;
            var rig = CameraRig.Instance;
            if (bd == null || rig == null) { Debug.LogWarning("[Ring] pre-warm skipped (no BeatDirector or CameraRig)"); yield break; }
            var sw = Stopwatch.StartNew();
            bool capture = RingState.Capture;
            try
            {
                try
                {
                    RingState.Capture = true;                  // phases equal their progress branches at each pose
                    bd.PrewarmAll(true);
                }
                catch (Exception e) { Debug.LogError("[Ring] pre-warm: enabling the sets failed: " + e); }
                for (int i = 0; i < PrewarmPoses.Length; i++)
                {
                    var pose = PrewarmPoses[i];
                    try
                    {
                        RingRig.ForcedS = pose.S;
                        rig.SetPose(pose.Eye, pose.Yaw, pose.Pitch);
                        ProgressDirector.RunChain(0f);         // globals, lights and animated objects for this pose
                    }
                    catch (Exception e) { Debug.LogError("[Ring] pre-warm pose " + pose.Tag + " failed: " + e); }
                    for (int f = 0; f < PrewarmFrames; f++) yield return null;
                }
            }
            finally
            {
                // always restored, even after a failure (the placement re-initialises the sets from the start progress)
                RingRig.ForcedS = float.NaN;
                RingState.Capture = capture;
                try { rig.ReleasePose(); }
                catch (Exception e) { Debug.LogError("[Ring] pre-warm: releasing the camera pose failed: " + e); }
            }
            Debug.Log("[Ring] pre-warm: " + PrewarmPoses.Length + " poses x " + PrewarmFrames + " frames in " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms");
        }

        /// <summary>Steps a load coroutine with every MoveNext guarded (nested IEnumerators are guarded too); an
        /// exception is logged and ends that routine only, never the boot.</summary>
        static IEnumerator Guarded(IEnumerator routine, string what)
        {
            while (routine != null)
            {
                bool moved = false;
                object cur = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved) cur = routine.Current;
                }
                catch (Exception e) { Debug.LogError("[Ring] GameBootstrap: " + what + " failed: " + e); }
                if (!moved) break;
                if (cur is IEnumerator nested) yield return Guarded(nested, what);
                else yield return cur;
            }
        }
    }
}
