// InstancedSet: draws repeated props of beats 5-7 (rail posts, bead window dots) with
// Graphics.RenderMeshInstanced from an IWorldUpdater (MASTER_PROMPT 14.7.9, research R3 3.7).
//  * Instances are added spatially sorted and split into chunks of at most MaxPerCall (500 < 511, the native
//    cap with object-to-world plus world-to-object matrices); every chunk has explicit world bounds.
//  * OnFrame queues the chunks for the current frame (all cameras). It allocates nothing: the matrices live in
//    one preallocated array and every chunk's RenderParams is built once at load. Calling it more than once
//    in a frame (capture harness) only queues identical opaque draws again.
//  * Optional distance culling skips chunks that lie wholly beyond the material's dithered fade-out
//    (posts: _FadeFar 67.5 m), so nothing visible is ever removed.
//  * If the platform cannot instance, MakeStatic bakes the same chunks into static MeshRenderers.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    public sealed class InstancedSet : IWorldUpdater
    {
        public const int MaxPerCall = 500;

        struct Chunk
        {
            public Mesh mesh;
            public MeshBuilder source;
            public Material material;
            public MaterialPropertyBlock props;
            public RenderParams rp;
            public int start, count;
            public Bounds bounds;
        }

        readonly string m_name;
        readonly List<Matrix4x4> m_pending = new List<Matrix4x4>(512);
        readonly List<Chunk> m_pendingChunks = new List<Chunk>(32);
        Matrix4x4[] m_matrices = new Matrix4x4[0];
        Chunk[] m_chunks = new Chunk[0];
        float m_cullDistSq;
        bool m_finished;

        public long Triangles { get; private set; }
        public int InstanceCount => m_finished ? m_matrices.Length : m_pending.Count;
        public int ChunkCount => m_finished ? m_chunks.Length : m_pendingChunks.Count;
        public string Name => m_name;

        /// <param name="cullDistance">Chunks whose bounds are farther than this from the camera are skipped (0 = never).</param>
        public InstancedSet(string name, float cullDistance)
        {
            m_name = name;
            m_cullDistSq = cullDistance > 0f ? cullDistance * cullDistance : 0f;
        }

        /// <summary>Adds instances of one mesh (already spatially sorted), chunked by chunkSize. Returns false if the material is missing.</summary>
        public bool Add(Mesh mesh, MeshBuilder source, Material material, MaterialPropertyBlock props, IList<Matrix4x4> instances,
                        int chunkSize, int layer, ShadowCastingMode shadows, bool receiveShadows)
        {
            if (m_finished) throw new InvalidOperationException("InstancedSet already finished");
            if (mesh == null || material == null || instances == null || instances.Count == 0) return false;
            if (!material.enableInstancing)
            {
                Debug.LogWarning($"[THREAD] {m_name}: material {material.name} had GPU instancing off; enabling it (RenderMeshInstanced requires it)");
                material.enableInstancing = true;
            }
            chunkSize = Mathf.Clamp(chunkSize, 1, MaxPerCall);
            long trisPerInstance = MeshKit.TriangleCount(mesh);
            for (int s = 0; s < instances.Count; s += chunkSize)
            {
                int count = Math.Min(chunkSize, instances.Count - s);
                int start = m_pending.Count;
                for (int i = 0; i < count; i++) m_pending.Add(instances[s + i]);
                Bounds b = ChunkBounds(mesh.bounds, m_pending, start, count);
                var rp = new RenderParams(material)
                {
                    layer = layer,
                    shadowCastingMode = shadows,
                    receiveShadows = receiveShadows,
                    worldBounds = b,
                    matProps = props,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                };
                m_pendingChunks.Add(new Chunk { mesh = mesh, source = source, material = material, props = props, rp = rp, start = start, count = count, bounds = b });
                Triangles += trisPerInstance * count;
            }
            return true;
        }

        /// <summary>Freezes the set into preallocated arrays (call once, before registering).</summary>
        public void Finish()
        {
            if (m_finished) return;
            m_matrices = m_pending.ToArray();
            m_chunks = m_pendingChunks.ToArray();
            m_pending.Clear();
            m_pendingChunks.Clear();
            m_finished = true;
        }

        public void OnFrame(in FrameState f)
        {
            if (!m_finished || !f.nearActive) return;   // near-frame content only exists from s = 262 (D-027)
            Vector3 cam = f.cameraPos;
            for (int i = 0; i < m_chunks.Length; i++)
            {
                if (m_cullDistSq > 0f && m_chunks[i].bounds.SqrDistance(cam) > m_cullDistSq) continue;
                Graphics.RenderMeshInstanced(in m_chunks[i].rp, m_chunks[i].mesh, 0, m_matrices, m_chunks[i].count, m_chunks[i].start);
            }
        }

        /// <summary>Fallback when instancing is unavailable: one static MeshRenderer per chunk (same bounds, same look).</summary>
        public void MakeStatic(WorldContext ctx, Transform parent)
        {
            Finish();
            for (int c = 0; c < m_chunks.Length; c++)
            {
                var ch = m_chunks[c];
                if (ch.source == null) continue;
                var mb = new MeshBuilder(ch.source.VertexCount * ch.count);
                for (int i = 0; i < ch.count; i++) mb.Append(ch.source, m_matrices[ch.start + i]);
                var mesh = mb.ToMesh($"{m_name}_static_{c}");
                var mr = ctx.AddMesh(parent, $"{m_name}_static_{c}", mesh, ch.material, ch.rp.shadowCastingMode != ShadowCastingMode.Off, ch.rp.receiveShadows);
                if (ch.props != null) mr.SetPropertyBlock(ch.props);
            }
            m_chunks = new Chunk[0];
            Triangles = 0;
        }

        static Bounds ChunkBounds(Bounds local, List<Matrix4x4> m, int start, int count)
        {
            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            for (int i = start; i < start + count; i++)
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    Vector3 w = m[i].MultiplyPoint3x4(corner);
                    mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w);
                }
            var b = new Bounds();
            b.SetMinMax(mn, mx);
            b.Expand(0.05f);
            return b;
        }
    }
}
