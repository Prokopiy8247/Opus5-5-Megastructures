// World-building contract shared by every beat builder (Assets/_Game/Beats/BeatNN_*).
// GameBootstrap creates one WorldContext, calls each IBeatBuilder.Build once behind the black
// load fade, then ProgressDirector calls every registered IWorldUpdater once per frame (after the
// camera has moved, before rendering). Updaters must not allocate.
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Per-frame state handed to updaters (all derived from ThreadMath in double, then cast).</summary>
    public struct FrameState
    {
        public double s;              // progress (plan distance) of the player/camera
        public double ambientTime;    // ambient clock (frozen in static tests, stepped by captures)
        public double aVirt;          // virtual altitude A_virt (m)
        public double darkness;       // D
        public Vector3 cameraPos;
        public Vector3 cameraForward;
        public float renderHeight;    // pixel height of the current render target
        public float fovV;            // vertical FOV of the camera (deg)
        public int beat;              // 0..6
        public bool groundActive;     // ground group visible (false once hidden by the deck, s >= GroundOffS)
        public bool nearActive;       // near frame (cable, walkway, beads, pier) exists (s >= 262, switched inside the bank)
    }

    public interface IWorldUpdater
    {
        void OnFrame(in FrameState f);
    }

    public interface IBeatBuilder
    {
        /// <summary>Short name used in logs, e.g. "Beat04_Tower".</summary>
        string Name { get; }
        /// <summary>Builds all geometry of the beat (runs once at load). Register updaters on ctx.</summary>
        void Build(WorldContext ctx);
    }

    public sealed class WorldContext
    {
        public MaterialLibrary Materials;
        /// <summary>Root of static content in true world coordinates (cable, walkway, beads, pier).</summary>
        public Transform NearRoot;
        /// <summary>Root of the sinking ground group (salt flat, colossi, stairs, terrace); ProgressDirector sets y = -A_virt.</summary>
        public Transform GroundGroup;
        public Camera Camera;
        public int Seed;
        public readonly List<IWorldUpdater> Updaters = new List<IWorldUpdater>();
        public readonly List<string> BuildLog = new List<string>();
        /// <summary>Total triangles generated (for the budget log).</summary>
        public long Triangles;

        public void Register(IWorldUpdater u) { if (u != null && !Updaters.Contains(u)) Updaters.Add(u); }

        /// <summary>Creates a child GameObject with a MeshFilter + MeshRenderer.</summary>
        public MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material mat, bool castShadows = true, bool receiveShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            if (mesh != null) Triangles += MeshKit.TriangleCount(mesh);
            return mr;
        }

        public void Log(string msg) { BuildLog.Add(msg); Debug.Log("[THREAD] " + msg); }
    }
}
