using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Fallback ladder tier of a beat (Part 13).</summary>
    public enum BeatTier { Full = 0, Simplified = 1, Graybox = 2 }

    /// <summary>Everything a beat generator needs at build time.</summary>
    public sealed class BeatContext
    {
        /// <summary>Root transform owned by the beat (geometry goes under it).</summary>
        public Transform Root;

        /// <summary>Parent for invisible corridor colliders of this beat (layer Corridor).</summary>
        public Transform Colliders;

        public MaterialLibrary Mats;

        /// <summary>Deterministic seed for this beat (GameState.Seed mixed with the beat index).</summary>
        public uint Seed;

        public BeatTier Tier;

        /// <summary>True when called from an EditMode test or editor tool (no play mode, no camera).</summary>
        public bool EditMode;
    }

    /// <summary>Per-frame inputs for beats. Filled by BeatDirector in Update (after LiftController and PlayerController).</summary>
    public struct BeatFrame
    {
        public double Time;          // SceneClock.Time
        public float DeltaTime;      // frame delta (0 when frozen)
        public float D;              // GameState.D
        public float E;              // GameState.E
        public float RideSeconds;    // GameState.RideSeconds
        public float RideU;          // GameState.RideU
        public Vector3 CameraPos;    // camera position of this frame (predicted from the player eye)
        public Camera Camera;
    }

    /// <summary>A named object whose screen projection the composition lint checks for a key shot (14.10).</summary>
    public struct KeyShotSubject
    {
        public string Name;
        public Bounds WorldBounds;
        public float MinScreenFraction; // minimum size as a fraction of frame height (0 = no size requirement)

        public KeyShotSubject(string name, Bounds b, float minScreenFraction = 0f)
        {
            Name = name; WorldBounds = b; MinScreenFraction = minScreenFraction;
        }
    }

    /// <summary>Named measurements from generated geometry, compared against AbyssConst by EditMode scale tests.</summary>
    public sealed class ScaleReport
    {
        public readonly Dictionary<string, double> Values = new Dictionary<string, double>();
        public void Add(string key, double measured) { Values[key] = measured; }
    }

    /// <summary>
    /// Base class of the seven beat generators (Assets/_Game/Beats/BeatNN_Name/). A beat builds all of its geometry,
    /// lights and colliders in <see cref="Build"/> (behind the load fade, also callable in EditMode), then animates and
    /// switches its detail tiers in <see cref="Tick"/>. Beats never allocate per frame after Build.
    /// </summary>
    public abstract class BeatBase : MonoBehaviour
    {
        /// <summary>Beat index 1..7.</summary>
        public abstract int Index { get; }

        public BeatTier Tier = BeatTier.Full;

        /// <summary>Generate geometry, materials (variants via ctx.Mats), lights and colliders under ctx.Root / ctx.Colliders.</summary>
        public abstract void Build(BeatContext ctx);

        /// <summary>Per-frame update: animation from SceneClock, detail tiers by camera distance, instanced draws.</summary>
        public virtual void Tick(in BeatFrame f) { }

        /// <summary>World bounds of the named subjects of a key shot tag owned by this beat (e.g. "KS2a_bridge_ahead").</summary>
        public virtual void CollectSubjects(string keyShotTag, List<KeyShotSubject> subjects) { }

        /// <summary>Scale measurements from generated mesh bounds (EditMode tests, Part 14.10).</summary>
        public virtual void ReportScale(ScaleReport report) { }

        /// <summary>Every generated mesh, for automated mesh validation (normals, winding, degenerate triangles, index format).</summary>
        public virtual void CollectMeshes(List<Mesh> meshes) { }

        /// <summary>Called by the QA harness before a capture so time-driven content is exactly at the given clock.</summary>
        public virtual void PrepareCapture(double sceneTime) { }
    }
}
