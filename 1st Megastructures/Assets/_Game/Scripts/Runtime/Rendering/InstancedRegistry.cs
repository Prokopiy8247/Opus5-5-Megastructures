using System.Collections.Generic;

namespace Megastructure
{
    /// <summary>
    /// Every live <see cref="InstancedBatch"/> (CONTRACTS section 7). Beats register sealed batches in Build;
    /// <see cref="InstancedRegistryDriver"/> (LateUpdate, execution order +200) draws them each frame.
    /// GameBootstrap clears the registry at the start of every load (scene restart keeps statics alive).
    /// </summary>
    public static class InstancedRegistry
    {
        static readonly List<InstancedBatch> s_batches = new List<InstancedBatch>(256);

        /// <summary>Registered batches (read-only use: QA statistics, warm-up).</summary>
        public static IReadOnlyList<InstancedBatch> Batches => s_batches;

        public static int Count => s_batches.Count;

        /// <summary>Instances drawn and RenderMeshInstanced calls issued in the last driver pass (PerfMonitor, QA).</summary>
        public static int LastInstancesDrawn { get; private set; }
        public static int LastDrawCalls { get; private set; }

        /// <summary>Register a batch (seals it if the owner forgot). Registering twice is ignored.</summary>
        public static void Register(InstancedBatch b)
        {
            if (b == null || s_batches.Contains(b)) return;
            if (!b.IsSealed) b.Seal();
            s_batches.Add(b);
        }

        public static void Unregister(InstancedBatch b)
        {
            if (b == null) return;
            s_batches.Remove(b);
        }

        /// <summary>Forget every batch (start of a load).</summary>
        public static void Clear()
        {
            s_batches.Clear();
            LastInstancesDrawn = 0;
            LastDrawCalls = 0;
        }

        /// <summary>Draw every visible batch for the current frame. Allocation-free.</summary>
        public static void DrawAll()
        {
            int instances = 0, calls = 0;
            for (int i = 0; i < s_batches.Count; i++)
            {
                InstancedBatch b = s_batches[i];
                if (b == null || !b.Visible || b.Mesh == null) continue;
                b.Draw();
                instances += b.Count;
                calls += b.DrawCallCount * b.Mesh.subMeshCount;
            }
            LastInstancesDrawn = instances;
            LastDrawCalls = calls;
        }
    }
}
