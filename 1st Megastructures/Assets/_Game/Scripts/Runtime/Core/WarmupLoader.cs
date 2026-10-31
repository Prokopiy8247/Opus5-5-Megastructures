using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Shader / pipeline-state warm-up during the black load (Part 8.6: no first-use hitch). Every material that can be drawn
    /// - each MaterialLibrary base material, every cached variant (<see cref="MaterialLibrary.AllVariants"/>), every material
    /// on a MeshRenderer in the scene (active or not, so detail tiers that switch on later are included) and every registered
    /// InstancedBatch - is drawn for a few frames as a tiny object 1 m in front of the camera while the picture is black
    /// (_AbPictureFade = 0).
    ///
    /// Meshes: each renderer's own mesh (so the vertex layout, and with it the D3D12 pipeline state, matches the real draw),
    /// instanced batches through RenderMeshInstanced (instancing variants), library materials not used by any renderer on a
    /// generated quad. Lighting state: frames with the Key light on and shadowing (main-light shadow and cascade keywords),
    /// then frames with it off (the dark beats), so both keyword sets compile. Shadow casting is on, so ShadowCaster passes
    /// compile too. Runs before ProgressDirector becomes active, so nothing else drives the Key light meanwhile.
    ///
    /// Robustness: the warm-up is an optimisation and must never stop the load. Null or destroyed meshes / materials are
    /// skipped; an item whose collection or draw throws is logged once ([ABYSS-ERROR]) and dropped; the Key light is restored
    /// in a finally block even when the coroutine ends early. GameBootstrap steps this coroutine by hand inside try/catch.
    /// </summary>
    public sealed class WarmupLoader
    {
        /// <summary>Frames drawn per lighting state.</summary>
        public const int FRAMES_PER_STATE = 3;

        struct Item
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public bool Instanced;
        }

        readonly List<Item> _items = new List<Item>(512);
        Mesh _quad;
        readonly Matrix4x4[] _one = new Matrix4x4[1];
        int _dropped;

        /// <summary>Number of (mesh layout, submesh, material) draws per frame.</summary>
        public int ItemCount => _items.Count;

        /// <summary>Items dropped because their collection or draw threw (each logged once).</summary>
        public int DroppedCount => _dropped;

        /// <summary>Collect every drawable (load time; allocations are fine here).</summary>
        public void Collect(MaterialLibrary lib)
        {
            _items.Clear();
            _dropped = 0;
            var seen = new HashSet<string>();
            var usedMaterials = new HashSet<Material>();

            // ---- every MeshRenderer in the loaded scenes, including inactive and disabled ones
            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                Mesh mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || mesh.subMeshCount <= 0) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int s = 0; s < mats.Length; s++)
                {
                    Material m = mats[s];
                    if (m == null) continue;
                    int sub = Mathf.Min(s, mesh.subMeshCount - 1);
                    AddUnique(seen, mesh, sub, m, false);
                    usedMaterials.Add(m);
                }
            }

            // ---- instanced batches (instancing variants)
            IReadOnlyList<InstancedBatch> batches = InstancedRegistry.Batches;
            for (int i = 0; i < batches.Count; i++)
            {
                InstancedBatch b = batches[i];
                if (b == null || b.Mesh == null || b.Material == null) continue;
                for (int s = 0; s < b.Mesh.subMeshCount; s++) AddUnique(seen, b.Mesh, s, b.Material, true);
            }

            // ---- library materials (base + cached variants) that no renderer uses yet
            if (lib != null)
            {
                var libMats = new List<Material>();
                if (lib.materials != null) libMats.AddRange(lib.materials);
                foreach (Material v in lib.AllVariants) libMats.Add(v);
                for (int i = 0; i < libMats.Count; i++)
                {
                    Material m = libMats[i];
                    if (m == null || usedMaterials.Contains(m)) continue;
                    AddUnique(seen, Quad(), 0, m, false);
                }
            }
        }

        /// <summary>
        /// Coroutine: draw everything for FRAMES_PER_STATE frames with the key light on, then off. Restores the light, also
        /// when the coroutine stops early (exception, camera destroyed, enumerator disposed).
        /// </summary>
        public IEnumerator Run(Camera cam, Light key)
        {
            if (cam == null || _items.Count == 0) yield break;
            int collected = _items.Count;
            bool keyWasEnabled = key != null && key.enabled;
            float keyIntensity = key != null ? key.intensity : 0f;
            LightShadows keyShadows = key != null ? key.shadows : LightShadows.None;
            Quaternion keyRot = key != null ? key.transform.rotation : Quaternion.identity;

            try
            {
                for (int state = 0; state < 2; state++)
                {
                    if (key != null)
                    {
                        key.enabled = state == 0;
                        key.shadows = LightShadows.Soft;
                        key.intensity = 0.01f;
                        key.transform.rotation = AbyssConst.YawPitch(205f, -72f);
                    }
                    for (int f = 0; f < FRAMES_PER_STATE; f++)
                    {
                        if (cam == null) yield break;   // destroyed meanwhile (scene teardown): nothing left to warm
                        DrawAll(cam);
                        yield return null;
                    }
                }
            }
            finally
            {
                if (key != null)
                {
                    key.enabled = keyWasEnabled;
                    key.intensity = keyIntensity;
                    key.shadows = keyShadows;
                    key.transform.rotation = keyRot;
                }
            }
            Debug.Log("[ABYSS] shader warm-up: " + _items.Count + " of " + collected + " draws x " + (2 * FRAMES_PER_STATE)
                      + " frames" + (_dropped > 0 ? " (" + _dropped + " dropped)" : ""));
        }

        void DrawAll(Camera cam)
        {
            Transform ct = cam.transform;
            Vector3 centre = ct.position + ct.forward * 1.0f;
            Quaternion rot = ct.rotation;
            var bounds = new Bounds(ct.position, new Vector3(4f, 4f, 4f));   // contains the camera: never culled
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                // destroyed since Collect (Unity null): skip silently, it can no longer be drawn by anything
                if (it.Mesh == null || it.Material == null || it.SubMesh < 0 || it.SubMesh >= it.Mesh.subMeshCount)
                {
                    _items.RemoveAt(i--);
                    continue;
                }
                try
                {
                    Bounds mb = it.Mesh.bounds;
                    float size = Mathf.Max(mb.size.x, Mathf.Max(mb.size.y, mb.size.z));
                    float scale = size > 1e-6f ? 0.02f / size : 1f;
                    Matrix4x4 m = Matrix4x4.TRS(centre - rot * (mb.center * scale), rot, new Vector3(scale, scale, scale));
                    var rp = new RenderParams(it.Material)
                    {
                        shadowCastingMode = ShadowCastingMode.On,
                        receiveShadows = true,
                        lightProbeUsage = LightProbeUsage.Off,
                        reflectionProbeUsage = ReflectionProbeUsage.Off,
                        worldBounds = bounds,
                        layer = 0,
                    };
                    if (it.Instanced)
                    {
                        _one[0] = m;
                        if (!it.Material.enableInstancing) it.Material.enableInstancing = true;
                        Graphics.RenderMeshInstanced(in rp, it.Mesh, it.SubMesh, _one, 1, 0);
                    }
                    else
                    {
                        Graphics.RenderMesh(in rp, it.Mesh, it.SubMesh, m);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[ABYSS-ERROR] shader warm-up: draw of mesh '" + NameOf(it.Mesh) + "' submesh " + it.SubMesh
                                   + " with material '" + NameOf(it.Material) + "' failed, item dropped: " + ex);
                    _items.RemoveAt(i--);
                    _dropped++;
                }
            }
        }

        void AddUnique(HashSet<string> seen, Mesh mesh, int sub, Material m, bool instanced)
        {
            if (mesh == null || m == null || sub < 0 || sub >= mesh.subMeshCount) return;
            string key;
            try
            {
                key = LayoutKey(mesh) + "|" + m.GetEntityId() + "|" + (instanced ? "i" : "r") + "|" + mesh.GetSubMesh(sub).topology;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ABYSS-ERROR] shader warm-up: cannot collect mesh '" + NameOf(mesh) + "' submesh " + sub
                               + " with material '" + NameOf(m) + "', item dropped: " + ex);
                _dropped++;
                return;
            }
            if (!seen.Add(key)) return;
            _items.Add(new Item { Mesh = mesh, SubMesh = sub, Material = m, Instanced = instanced });
        }

        static string LayoutKey(Mesh mesh)
        {
            VertexAttributeDescriptor[] attrs = mesh.GetVertexAttributes();
            var sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < attrs.Length; i++)
            {
                VertexAttributeDescriptor a = attrs[i];
                sb.Append((int)a.attribute).Append(':').Append((int)a.format).Append('x').Append(a.dimension).Append('@').Append(a.stream).Append(';');
            }
            sb.Append(mesh.indexFormat == IndexFormat.UInt32 ? "i32" : "i16");
            return sb.ToString();
        }

        static string NameOf(Object o) => o != null ? o.name : "<destroyed>";

        Mesh Quad()
        {
            if (_quad != null) return _quad;
            // A unit quad with every attribute a Megastructure shader may read (normal, tangent, colour, uv0, uv1).
            _quad = new Mesh { name = "WarmupQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var t = new Vector4(1f, 0f, 0f, 1f);
            _quad.tangents = new[] { t, t, t, t };
            var c = new Color32(255, 0, 0, 255);
            _quad.colors32 = new[] { c, c, c, c };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            _quad.uv2 = new[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0) };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>Free the generated quad (after the warm-up).</summary>
        public void Dispose()
        {
            if (_quad != null) Object.Destroy(_quad);
            _quad = null;
            _items.Clear();
        }
    }
}
