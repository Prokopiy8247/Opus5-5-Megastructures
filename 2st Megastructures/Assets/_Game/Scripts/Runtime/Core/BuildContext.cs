using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// A generator of one beat's (or one system's) content. Implementations are discovered by reflection in the
    /// Megastructure.Runtime assembly and run in ascending <see cref="Order"/> by the WorldBuilder at load, behind
    /// the black fade. Build may yield (null) to spread work over frames. Nothing may be created after load.
    /// </summary>
    public interface IBeatBuilder
    {
        int Order { get; }
        string Name { get; }
        IEnumerator Build(BuildContext ctx);
    }

    /// <summary>Something drawn by code (RenderMeshIndirect etc.) that the BeatDirector toggles with its set.</summary>
    public interface IRingDrawable
    {
        RingSet Set { get; }
        bool Visible { get; set; }
    }

    /// <summary>A behaviour that animates from <see cref="RingState"/> (hatch, ark, beacon, climber ...).</summary>
    public interface IRingAnimated
    {
        /// <summary>Called every frame by the BeatDirector after the directors (and synchronously by captures).</summary>
        void Tick(float s, double t, float dt);
    }

    /// <summary>Services handed to every IBeatBuilder.</summary>
    public sealed class BuildContext
    {
        public RingAssets Assets;
        public Transform Root;
        public readonly Dictionary<RingSet, Transform> SetRoots = new Dictionary<RingSet, Transform>();
        public readonly Dictionary<string, Object> Registry = new Dictionary<string, Object>();
        public readonly List<IRingDrawable> Drawables = new List<IRingDrawable>();
        public readonly List<IRingAnimated> Animated = new List<IRingAnimated>();
        public int Triangles;
        public readonly Dictionary<string, int> TrianglesByBuilder = new Dictionary<string, int>();
        public string CurrentBuilder = "";

        public static BuildContext Current { get; private set; }

        public BuildContext(RingAssets assets, Transform root)
        {
            Assets = assets;
            Root = root;
            Current = this;
            foreach (RingSet s in System.Enum.GetValues(typeof(RingSet)))
            {
                var go = new GameObject("Set_" + s);
                go.transform.SetParent(root, false);
                SetRoots[s] = go.transform;
            }
        }

        public Transform SetRoot(RingSet set) => SetRoots[set];

        /// <summary>Creates a static mesh object. bendMaxDistance = the largest camera distance from which the object
        /// is visible (bounds are inflated for the bend, RingBend.InflateBounds).</summary>
        public GameObject AddMesh(string name, Mesh mesh, string matId, RingSet set, bool castShadows, float bendMaxDistance, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : SetRoot(set), false);
            var mf = go.AddComponent<MeshFilter>();
            mesh.bounds = RingBend.InflateBounds(mesh.bounds, bendMaxDistance);
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Assets != null ? Assets.Get(matId) : null;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            int tris = (int)mesh.GetIndexCount(0) / 3;
            for (int i = 1; i < mesh.subMeshCount; i++) tris += (int)mesh.GetIndexCount(i) / 3;
            Triangles += tris;
            TrianglesByBuilder.TryGetValue(CurrentBuilder, out int t0);
            TrianglesByBuilder[CurrentBuilder] = t0 + tris;
            return go;
        }

        /// <summary>Adds a collider-only object (never rendered). Colliders are always active (not part of a set toggle).</summary>
        public GameObject AddBoxCollider(string name, Vector3 centre, Vector3 size, Quaternion rotation, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : SetRoot(RingSet.Always), false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return go;
        }

        public GameObject AddMeshCollider(string name, Mesh mesh, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : SetRoot(RingSet.Always), false);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            return go;
        }

        /// <summary>Registers a named object for tests and captures (e.g. "Bridge.Deck", "Door.Opening").</summary>
        public void Register(string key, Object obj) => Registry[key] = obj;

        public T Get<T>(string key) where T : Object => Registry.TryGetValue(key, out var o) ? o as T : null;
    }
}
