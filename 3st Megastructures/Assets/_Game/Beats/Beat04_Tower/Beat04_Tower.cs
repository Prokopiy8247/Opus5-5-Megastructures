// Beat04_Tower: the thread becomes a tower (MASTER_PROMPT 14.5.4). Builds
//  * the terrace slab (ground group, sinks with A_virt): top face with the mandala (Terrace) and the
//    chamfers / walls (Stringer = M04 without the mandala); casts and receives shadow maps;
//  * the slab's flat ground shadow (ground group, SaltShadow, no shadow maps; critique run06 A2): the projection
//    of the slab along -S of the beat-5 sun onto the salt, so the terrace keeps its shadow once the sinking
//    ground is beyond the shadow distance (TerraceBuilder.BuildGroundShadow);
//  * the cable (near root, true world coordinates, never sinks): LOD0/LOD1/LOD2 bands, collars and the two
//    far ribbons. No shadow casting (D-005, the analytic cable shadow replaces it); the cable and collars
//    receive shadow maps (the walkway's shadow can fall on them), the ribbons receive nothing.
// The whole cable root is switched on at the near-cable activation s = 262 (inside the bank, 14.5.4 / 14.7.3)
// and off again only below s = 258 (hysteresis; both points are deep inside the bank, optical depth > 5),
// so the true cable can never be seen next to the analytic far thread of beats 1 to 3.
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class Beat04_Tower : IBeatBuilder
    {
        public string Name => "Beat04_Tower";

        public void Build(WorldContext ctx)
        {
            MaterialLibrary mats = ctx.Materials;

            // ---------------------------------------------------------------- terrace (ground group)
            if (ctx.GroundGroup == null) Debug.LogError("[THREAD-FAIL] Beat04_Tower: ctx.GroundGroup is null; terrace built at the scene root");
            var terraceRoot = new GameObject("Beat04_Terrace").transform;
            terraceRoot.SetParent(ctx.GroundGroup, false);
            Mesh top = TerraceBuilder.BuildTop().ToMesh("Terrace_Top");
            Mesh sides = TerraceBuilder.BuildSides().ToMesh("Terrace_Sides");
            ctx.AddMesh(terraceRoot, top.name, top, Pick(mats != null ? mats.Terrace : null, "Terrace"), true, true);
            ctx.AddMesh(terraceRoot, sides.name, sides, Pick(mats != null ? mats.Stringer : null, "Stringer"), true, true);
            // flat ground shadow on the salt (shade mode of the salt shader), sinks with the ground group
            Mesh shadow = TerraceBuilder.BuildGroundShadow().ToMesh("Terrace_GroundShadow");
            ctx.AddMesh(terraceRoot, shadow.name, shadow, Pick(mats != null ? mats.SaltShadow : null, "SaltShadow"), false, false);

            // ---------------------------------------------------------------- cable (near root)
            if (ctx.NearRoot == null) Debug.LogError("[THREAD-FAIL] Beat04_Tower: ctx.NearRoot is null; cable built at the scene root");
            var cableGo = new GameObject("Beat04_Cable");
            Transform cableRoot = cableGo.transform;
            cableRoot.SetParent(ctx.NearRoot, false);
            cableRoot.localPosition = new Vector3((float)ThreadConstants.CableAxisX, 0f, (float)ThreadConstants.CableAxisZ);
            cableRoot.localRotation = Quaternion.identity;
            cableRoot.localScale = Vector3.one;

            CableSet set = CableBuilder.Build();
            Material cable = Pick(mats != null ? mats.Cable : null, "Cable");
            Material collar = Pick(mats != null ? mats.Collar : null, "Collar");
            Material ribbon = Pick(mats != null ? mats.ThreadRibbon : null, "ThreadRibbon");
            var lateral = set.Lateral();
            for (int i = 0; i < lateral.Count; i++) ctx.AddMesh(cableRoot, lateral[i].name, lateral[i], cable, false, true);
            for (int i = 0; i < set.Collars.Count; i++) ctx.AddMesh(cableRoot, set.Collars[i].name, set.Collars[i], collar, false, true);
            for (int i = 0; i < set.Ribbons.Count; i++) ctx.AddMesh(cableRoot, set.Ribbons[i].name, set.Ribbons[i], ribbon, false, false);

            var sw = new NearCableSwitch(cableGo, ProgressDirector.S);
            ctx.Register(sw);

            ctx.Log($"Beat04_Tower: terrace {MeshKit.TriangleCount(top) + MeshKit.TriangleCount(sides)} tris, ground shadow {MeshKit.TriangleCount(shadow)} tris; cable LOD0 {CableSet.Triangles(set.Lod0)} " +
                    $"({set.Lod0.Count} chunks), LOD1 {CableSet.Triangles(set.Lod1)} ({set.Lod1.Count}), LOD2 {CableSet.Triangles(set.Lod2)}, " +
                    $"collars {CableSet.Triangles(set.Collars)} ({set.Collars.Count}), ribbons {CableSet.Triangles(set.Ribbons)}; total {set.TotalTriangles}; " +
                    $"near cable {(sw.On ? "on" : "off")} at s = {ProgressDirector.S:F1}");
        }

        static Material Pick(Material m, string name)
        {
            if (m == null) Debug.LogError($"[THREAD-FAIL] Beat04_Tower: MaterialLibrary.{name} is missing");
            return m;
        }
    }

    /// <summary>
    /// Activates the near cable at s >= 262 and deactivates it below s = 258 (hysteresis 4 m, both inside
    /// the bank). Allocation-free; SetActive only on a state change.
    /// </summary>
    public sealed class NearCableSwitch : IWorldUpdater
    {
        public const double Hysteresis = 4.0;
        readonly GameObject m_target;
        bool m_on;

        public NearCableSwitch(GameObject target, double s)
        {
            m_target = target;
            m_on = s >= ThreadConstants.NearCableOnS;
            if (m_target != null) m_target.SetActive(m_on);
        }

        public bool On => m_on;

        public void OnFrame(in FrameState f) { Apply(f.s); }

        public void Apply(double s)
        {
            bool want = m_on ? s >= ThreadConstants.NearCableOnS - Hysteresis : s >= ThreadConstants.NearCableOnS;
            if (want == m_on) return;
            m_on = want;
            if (m_target != null) m_target.SetActive(want);
        }
    }
}
