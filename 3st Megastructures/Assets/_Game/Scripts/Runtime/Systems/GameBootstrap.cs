// GameBootstrap: builds the whole world behind the black load fade (MASTER_PROMPT 7.3, 7.4).
// Beat builders are discovered by reflection (every non-abstract IBeatBuilder in this assembly,
// ordered by Name), so builders compile and ship independently. Raises WorldReady when done.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Megastructure
{
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        public MaterialLibrary materials;
        public ThreadPalette palette;
        public Transform nearRoot;
        public Transform groundGroup;
        public Light sun;
        public Camera mainCamera;

        public static GameBootstrap Instance { get; private set; }
        public static bool Ready { get; private set; }
        public static event Action WorldReady;
        public WorldContext Context { get; private set; }
        public double LoadSeconds { get; private set; }

        void Awake()
        {
            Instance = this;
            Ready = false;
            GradeState.FadeBlack = 1f;
            ThreadClock.Reset();
            ThreadClock.FreezeDither = CliArgs.FreezeDither;
            ThreadClock.Frozen = CliArgs.FreezeAmbient;
            if (mainCamera == null) mainCamera = Camera.main;
            Debug.Log("[THREAD] args: " + string.Join(" ", CliArgs.Args));
            if (CliArgs.Has("-grade-off")) { GradeState.Enabled = false; Debug.Log("[THREAD] diagnostics: ThreadGrade disabled (-grade-off)"); }
        }

        IEnumerator Start()
        {
            yield return null;   // let every system Awake/OnEnable and register its service
            var sw = Stopwatch.StartNew();
            var ctx = new WorldContext
            {
                Materials = materials,
                NearRoot = nearRoot,
                GroundGroup = groundGroup,
                Camera = mainCamera,
                Seed = CliArgs.Seed,
            };
            Context = ctx;

            var builders = DiscoverBuilders();
            ctx.Log($"bootstrap: {builders.Count} builders: {string.Join(", ", builders.ConvertAll(b => b.Name))}");
            foreach (var b in builders)
            {
                var bsw = Stopwatch.StartNew();
                long tris0 = ctx.Triangles;
                try { b.Build(ctx); }
                catch (Exception e) { Debug.LogError($"[THREAD-FAIL] builder {b.Name} threw: {e}"); }
                ctx.Log($"built {b.Name} in {bsw.Elapsed.TotalMilliseconds:F0} ms, {ctx.Triangles - tris0} triangles");
            }
            ctx.Log($"world built in {sw.Elapsed.TotalMilliseconds:F0} ms, {ctx.Triangles} triangles total, {ctx.Updaters.Count} updaters");

            var pd = ProgressDirector.Instance;
            if (pd != null) pd.Init(ctx);

            // Shader warm-up behind black: render every material once (WarmupLoader may extend this).
            Shader.WarmupAllShaders();
            yield return null;
            yield return null;
            LoadSeconds = sw.Elapsed.TotalSeconds;
            ctx.Log($"load finished in {LoadSeconds:F2} s");
            Ready = true;
            try { WorldReady?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[THREAD-FAIL] WorldReady handler threw: {e}"); }
        }

        static List<IBeatBuilder> DiscoverBuilders()
        {
            var list = new List<IBeatBuilder>();
            foreach (var t in typeof(IBeatBuilder).Assembly.GetTypes())
            {
                if (t.IsAbstract || t.IsInterface || !typeof(IBeatBuilder).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                try { list.Add((IBeatBuilder)Activator.CreateInstance(t)); }
                catch (Exception e) { Debug.LogError($"[THREAD-FAIL] cannot create builder {t.Name}: {e.Message}"); }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Ready = false; }
        }
    }
}
