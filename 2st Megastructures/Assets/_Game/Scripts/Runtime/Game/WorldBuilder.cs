using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Megastructure
{
    /// <summary>
    /// Runs every IBeatBuilder at load, behind the black fade (Part 7.4, ADR-001). Discovers every non-abstract
    /// IBeatBuilder in the Megastructure.Runtime assembly (Beats included through the asmref) by reflection, orders them
    /// by Order then Name, creates the BuildContext (root "World"), runs each Build as a guarded nested coroutine
    /// (an exception is logged and the next builder runs), logs time and triangles per builder, sets RingRig.Context and
    /// hands the content to the BeatDirector.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WorldBuilder : MonoBehaviour
    {
        public struct BuilderReport { public string Name; public int Order; public double Ms; public int Triangles; public bool Ok; }

        /// <summary>One entry per builder of the last build (QA, perf report).</summary>
        public static readonly List<BuilderReport> Reports = new List<BuilderReport>();
        public static double TotalMs { get; private set; }
        public static BuildContext Context { get; private set; }

        public static void ResetStatics() { Reports.Clear(); TotalMs = 0; Context = null; }

        static List<IBeatBuilder> Discover(Transform holder)
        {
            var list = new List<IBeatBuilder>();
            Type[] types;
            Assembly asm = typeof(WorldBuilder).Assembly;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null || !t.IsClass || t.IsAbstract || t.ContainsGenericParameters || !typeof(IBeatBuilder).IsAssignableFrom(t)) continue;
                try
                {
                    if (typeof(Component).IsAssignableFrom(t))
                    {
                        if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                        var go = new GameObject("Builder_" + t.Name);
                        go.transform.SetParent(holder, false);
                        list.Add((IBeatBuilder)go.AddComponent(t));
                    }
                    else if (typeof(ScriptableObject).IsAssignableFrom(t))
                    {
                        list.Add((IBeatBuilder)ScriptableObject.CreateInstance(t));
                    }
                    else if (t.GetConstructor(Type.EmptyTypes) != null)
                    {
                        list.Add((IBeatBuilder)Activator.CreateInstance(t));
                    }
                    else Debug.LogWarning("[Ring] WorldBuilder: " + t.FullName + " has no parameterless constructor; skipped");
                }
                catch (Exception e)
                {
                    Debug.LogError("[Ring] WorldBuilder: cannot create " + t.FullName + ": " + e);
                }
            }
            list.Sort((a, b) =>
            {
                int oa = SafeOrder(a), ob = SafeOrder(b);
                if (oa != ob) return oa.CompareTo(ob);
                return string.CompareOrdinal(SafeName(a), SafeName(b));
            });
            return list;
        }

        static int SafeOrder(IBeatBuilder b) { try { return b.Order; } catch { return int.MaxValue; } }
        static string SafeName(IBeatBuilder b) { try { return b.Name ?? b.GetType().Name; } catch { return b.GetType().Name; } }

        /// <summary>Builds the whole world. Yield it from a coroutine (it yields between and inside builders).</summary>
        public IEnumerator BuildAll(RingAssets assets)
        {
            Reports.Clear();
            var total = Stopwatch.StartNew();
            var rootGo = new GameObject("World");
            var ctx = new BuildContext(assets, rootGo.transform);
            Context = ctx;
            var holder = new GameObject("Builders");
            holder.transform.SetParent(rootGo.transform, false);
            List<IBeatBuilder> builders;
            try { builders = Discover(holder.transform); }
            catch (Exception e)
            {
                Debug.LogError("[Ring] WorldBuilder: builder discovery failed: " + e);
                builders = new List<IBeatBuilder>();
            }
            Debug.Log("[Ring] WorldBuilder: " + builders.Count + " builders");

            for (int i = 0; i < builders.Count; i++)
            {
                var b = builders[i];
                string name = SafeName(b);
                int order = SafeOrder(b);
                ctx.CurrentBuilder = name;
                var sw = Stopwatch.StartNew();
                bool ok = true;
                IEnumerator routine = null;
                try { routine = b.Build(ctx); }
                catch (Exception e) { ok = false; Debug.LogError("[Ring] builder " + name + " failed to start: " + e); }
                if (routine != null)
                {
                    var guard = new Guard();
                    yield return Run(routine, name, guard);
                    ok = !guard.Failed;
                }
                sw.Stop();
                ctx.TrianglesByBuilder.TryGetValue(name, out int tris);
                Reports.Add(new BuilderReport { Name = name, Order = order, Ms = sw.Elapsed.TotalMilliseconds, Triangles = tris, Ok = ok });
                Debug.Log("[Ring] builder " + name + " (order " + order + ") " + (ok ? "built" : "FAILED") + " in "
                          + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms, " + tris + " triangles (total " + ctx.Triangles + ")");
                yield return null;
            }
            ctx.CurrentBuilder = "";
            total.Stop();
            TotalMs = total.Elapsed.TotalMilliseconds;
            RingRig.Context = ctx;
            Debug.Log("[Ring] WorldBuilder: world built in " + TotalMs.ToString("0") + " ms, " + ctx.Triangles + " triangles, "
                      + ctx.Drawables.Count + " drawables, " + ctx.Animated.Count + " animated, " + ctx.Registry.Count + " registry keys");

            // a failed builder may have left a broken drawable behind: binding must never stop the boot (the walk would
            // stay black); the BeatDirector also guards every drawable switch itself
            var bd = BeatDirector.Instance;
            if (bd != null)
            {
                try { bd.Bind(ctx); }
                catch (Exception e) { Debug.LogError("[Ring] WorldBuilder: BeatDirector.Bind failed: " + e); }
            }
            else Debug.LogWarning("[Ring] WorldBuilder: no BeatDirector to bind the world sets");
        }

        sealed class Guard { public bool Failed; }

        /// <summary>Steps an IEnumerator (and nested IEnumerators it yields) with every MoveNext guarded.</summary>
        static IEnumerator Run(IEnumerator root, string name, Guard guard)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool moved;
                object cur = null;
                try
                {
                    moved = top.MoveNext();
                    if (moved) cur = top.Current;
                }
                catch (Exception e)
                {
                    guard.Failed = true;
                    Debug.LogError("[Ring] builder " + name + " threw: " + e);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                yield return cur;
            }
        }
    }
}
