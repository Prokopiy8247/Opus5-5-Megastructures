using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// GPU-instanced draw of one mesh with one material (CONTRACTS section 7, 14.5 rules).
    ///
    /// Build time: <see cref="Add"/> the object-to-world matrices, then <see cref="Seal"/>. Sealing sorts the instances into
    /// spatial chunks of chunkSize metres (3D grid on the instance positions, deterministic order) and splits every chunk into
    /// draws of at most <see cref="MaxPerDraw"/> = 511 instances (both object matrices kept; docs/research/mesh-instancing.md).
    /// Each draw has its own preallocated <see cref="RenderParams"/> with explicit world bounds = union of the transformed mesh
    /// bounds inflated by 10 % (Unity culls the whole call with one bound), shadow casting / receiving set explicitly
    /// (RenderParams defaults to no shadows), light and reflection probes off, camera null (all cameras).
    ///
    /// Run time: <see cref="InstancedRegistryDriver"/> calls <see cref="Draw"/> once per frame in LateUpdate (+200) for every
    /// registered, visible batch. One MaterialPropertyBlock per batch carries only _AbFade (detail-tier cross-fades); at
    /// Fade == 1 no block is bound. Zero GC allocation per frame.
    /// </summary>
    public sealed class InstancedBatch
    {
        /// <summary>Instances per Graphics.RenderMeshInstanced call (objectToWorld + worldToObject in the instancing cbuffer).</summary>
        public const int MaxPerDraw = 511;

        /// <summary>Relative inflation of every draw's world bounds (cull spheres inflated 10 %, 14.5.5 pitfalls).</summary>
        public const float BoundsInflation = 0.10f;

        public readonly string Name;
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly ShadowCastingMode Shadows;
        public readonly bool ReceiveShadows;
        public readonly int Layer;
        public readonly float ChunkSize;

        List<Matrix4x4> _pending = new List<Matrix4x4>();
        Matrix4x4[] _matrices;          // all instances, sorted by chunk
        RenderParams[] _params;         // one per draw
        int[] _drawStart, _drawCount;
        Bounds[] _drawBounds;
        int _drawCalls;
        bool _sealed;

        MaterialPropertyBlock _mpb;
        float _fade = 1f;

        /// <summary>When false the batch issues no draws (default true).</summary>
        public bool Visible { get; set; } = true;

        /// <summary>_AbFade for the whole batch: 1 = opaque; see the CONTRACTS section 7 convention for negative values.</summary>
        public float Fade
        {
            get => _fade;
            set
            {
                if (value == _fade) return;
                _fade = value;
                ApplyFade();
            }
        }

        /// <summary>Number of instances (added so far before Seal, sealed after).</summary>
        public int Count => _sealed ? _matrices.Length : _pending.Count;

        /// <summary>Number of RenderMeshInstanced calls per frame (per submesh) after Seal.</summary>
        public int DrawCallCount => _drawCalls;

        public bool IsSealed => _sealed;

        /// <summary>Union of all draw bounds (inflated) after Seal.</summary>
        public Bounds WorldBounds { get; private set; }

        public InstancedBatch(string name, Mesh mesh, Material material, ShadowCastingMode shadows = ShadowCastingMode.Off,
                              bool receiveShadows = true, int layer = 0, float chunkSize = 200f)
        {
            Name = name;
            Mesh = mesh;
            Material = material;
            Shadows = shadows;
            ReceiveShadows = receiveShadows;
            Layer = layer;
            ChunkSize = Mathf.Max(1f, chunkSize);
            if (mesh == null) Debug.LogError("[ABYSS-ERROR] InstancedBatch " + name + ": mesh is null");
            if (material == null) Debug.LogError("[ABYSS-ERROR] InstancedBatch " + name + ": material is null");
        }

        /// <summary>Add one instance (build time only, before Seal).</summary>
        public void Add(Matrix4x4 objectToWorld)
        {
            if (_sealed)
            {
                Debug.LogError("[ABYSS-ERROR] InstancedBatch " + Name + ": Add after Seal");
                return;
            }
            _pending.Add(objectToWorld);
        }

        /// <summary>Build the chunked draw lists (build time). Safe to call in EditMode.</summary>
        public void Seal()
        {
            if (_sealed) return;
            _sealed = true;
            int n = _pending.Count;

            // ---- bucket instances by chunk cell (deterministic: sorted by cell key, then insertion order)
            var cellOf = new long[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = _pending[i].GetColumn(3);
                cellOf[i] = CellKey(Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.y / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
                order[i] = i;
            }
            System.Array.Sort(order, (a, b) =>
            {
                int c = cellOf[a].CompareTo(cellOf[b]);
                return c != 0 ? c : a.CompareTo(b);
            });

            _matrices = new Matrix4x4[n];
            for (int i = 0; i < n; i++) _matrices[i] = _pending[order[i]];

            // ---- split into draws: a new draw at every chunk change or after MaxPerDraw instances
            var starts = new List<int>();
            var counts = new List<int>();
            int runStart = 0;
            for (int i = 1; i <= n; i++)
            {
                bool cut = i == n || cellOf[order[i]] != cellOf[order[i - 1]] || i - runStart >= MaxPerDraw;
                if (!cut) continue;
                starts.Add(runStart);
                counts.Add(i - runStart);
                runStart = i;
            }
            _drawCalls = starts.Count;
            _drawStart = starts.ToArray();
            _drawCount = counts.ToArray();
            _drawBounds = new Bounds[_drawCalls];
            _params = new RenderParams[_drawCalls];

            Bounds local = Mesh != null ? Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            bool any = false;
            Bounds all = default;
            for (int d = 0; d < _drawCalls; d++)
            {
                Bounds b = TransformBounds(_matrices[_drawStart[d]], local);
                for (int k = 1; k < _drawCount[d]; k++) b.Encapsulate(TransformBounds(_matrices[_drawStart[d] + k], local));
                b.Expand(b.size * BoundsInflation);
                _drawBounds[d] = b;
                if (!any) { all = b; any = true; } else all.Encapsulate(b);

                var rp = new RenderParams(Material)
                {
                    layer = Layer,
                    shadowCastingMode = Shadows,
                    receiveShadows = ReceiveShadows,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                    worldBounds = b,
                    camera = null,
                    matProps = null,
                };
                _params[d] = rp;
            }
            WorldBounds = all;
            _pending = null;

            if (Material != null && !Material.enableInstancing)
            {
                // RenderMeshInstanced refuses materials without instancing; enable once at build time.
                Material.enableInstancing = true;
                Debug.Log("[ABYSS] InstancedBatch " + Name + ": enabled GPU instancing on material " + Material.name);
            }
            ApplyFade();
        }

        /// <summary>Issue this frame's draws (called by InstancedRegistryDriver; no allocation).</summary>
        public void Draw()
        {
            if (!_sealed || !Visible || Mesh == null || Material == null || _drawCalls == 0) return;
            int subMeshes = Mesh.subMeshCount;
            for (int d = 0; d < _drawCalls; d++)
            {
                for (int s = 0; s < subMeshes; s++)
                    Graphics.RenderMeshInstanced(in _params[d], Mesh, s, _matrices, _drawCount[d], _drawStart[d]);
            }
        }

        /// <summary>World bounds of draw i (QA, composition lint).</summary>
        public Bounds GetDrawBounds(int i) => _drawBounds[i];

        /// <summary>Matrix of sealed instance i (QA / warm-up).</summary>
        public Matrix4x4 GetMatrix(int i) => _matrices[i];

        void ApplyFade()
        {
            if (_params == null) return;
            MaterialPropertyBlock block = null;
            if (_fade != 1f)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _mpb.SetFloat(ShaderIds.AbFade, _fade);
                block = _mpb;
            }
            for (int d = 0; d < _params.Length; d++) _params[d].matProps = block;
        }

        static long CellKey(int x, int y, int z)
        {
            // 21 bits per axis (+/- 1 M chunks), sign-offset so the sort order is spatially coherent.
            const long off = 1 << 20;
            return ((x + off) << 42) | ((y + off) << 21) | (z + off);
        }

        /// <summary>Axis-aligned bounds of a local box under a matrix (8 corners).</summary>
        public static Bounds TransformBounds(in Matrix4x4 m, in Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            Vector3 p0 = m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z));
            var b = new Bounds(p0, Vector3.zero);
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(+e.x, -e.y, -e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, +e.y, -e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(+e.x, +e.y, -e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, +e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(+e.x, -e.y, +e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, +e.y, +e.z)));
            b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(+e.x, +e.y, +e.z)));
            return b;
        }
    }
}
