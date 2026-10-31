using System.Collections;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 3 (MASTER_PROMPT 14.5.3): the ark (set Ark, no shadow casting, bounds inflated for the 12 km bend) with its
    /// motion (ArkMotion, IRingAnimated) and the footbridge arch (set FarValley). The fake ship shadow and the felt cue
    /// are globals written by the AtmosphereDriver from RingState.ArkU; this builder only makes geometry and motion.
    /// Registry: Ark.Hull, Subject.Ark (markers under the translation-only root: exact dimensions), Ark.Root, Ark.Body,
    /// Ark.HullMesh, Ark.WindowBand, Footbridge.Opening, Footbridge.
    /// </summary>
    public sealed class Beat03ArkBuilder : IBeatBuilder
    {
        public int Order => 300;
        public string Name => "Beat03Ark";

        public IEnumerator Build(BuildContext ctx)
        {
            int tris0 = ctx.Triangles;

            // ---------------- the ark
            var root = new GameObject("Ark").transform;
            root.SetParent(ctx.SetRoot(RingSet.Ark), false);
            var body = new GameObject("Ark.Body").transform;
            body.SetParent(root, false);
            var anim = new ArkMotion(root, body);
            anim.Tick(RingState.S, RingState.T, 0f);

            const float bend = ArkSpec.BendMaxDistance;
            var hullGo = Beat34Geo.Add(ctx, "Ark.HullMesh", ArkMeshes.Hull(new MeshBuilder()), RingMat.ArkHull, RingSet.Ark, false, bend, body);
            yield return null;

            Beat34Geo.Add(ctx, "Ark.Engines", ArkMeshes.EngineInner(new MeshBuilder()), Beat34Mat.ArkEngine, RingSet.Ark, false, bend, body);

            ArkSpec.WindowRange(out int kMin, out int kMax);
            var band = new MeshBuilder();
            int windows = ArkMeshes.WindowCells(band, kMin, kMax);
            var bandGo = Beat34Geo.Add(ctx, "Ark.WindowBand", band, RingMat.ArkWindow, RingSet.Ark, false, bend, body);

            // billboards: the bounds grow to the drawn quads (minimum size at 12 km) before the bend inflation
            Beat34Additive.AddToWorld(ctx, ArkMeshes.Glow(new MeshBuilder()), "Ark.EngineGlow", RingMat.Glow, RingSet.Ark, bend, body);
            yield return null;

            ctx.Animated.Add(anim);

            // registry: exact dimensions (the renderers' bounds are inflated by thousands of metres for the bend)
            Beat34Geo.Marker(ctx, "Ark.Hull", root, Vector3.zero, new Vector3(ArkSpec.Length, ArkSpec.Height, ArkSpec.Beam));
            float xMin = -0.5f * ArkSpec.Length - ArkSpec.EngineLen, xMax = 0.5f * ArkSpec.Length;
            float yMin = -ArkSpec.HalfHeight - ArkSpec.KeelDepth, yMax = ArkSpec.HalfHeight;
            Beat34Geo.Marker(ctx, "Subject.Ark", root, new Vector3(0.5f * (xMin + xMax), 0.5f * (yMin + yMax), 0f),
                             new Vector3(xMax - xMin, yMax - yMin, ArkSpec.Beam));
            ctx.Register("Ark.Root", root.gameObject);
            ctx.Register("Ark.Body", body.gameObject);
            if (hullGo != null) ctx.Register("Ark.HullMesh", hullGo);
            if (bandGo != null) ctx.Register("Ark.WindowBand", bandGo);
            int arkTris = ctx.Triangles - tris0;

            // ---------------- the footbridge arch (FarValley, 14.5.3, cut order 3b)
            FootbridgeArch.Build(ctx);
            yield return null;

            Debug.Log("[Ring] Beat03ArkBuilder: ark " + arkTris + " triangles (" + windows + " of " + ArkSpec.WinRows * ArkSpec.WinPerRow +
                      " windows placed, columns " + kMin + ".." + kMax + ", mean level " + ArkSpec.WindowMeanLevel().ToString("0.000") +
                      "), footbridge " + (ctx.Triangles - tris0 - arkTris) + " triangles");
        }
    }
}
