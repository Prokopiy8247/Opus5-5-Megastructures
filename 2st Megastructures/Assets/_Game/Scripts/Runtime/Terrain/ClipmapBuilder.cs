using System.Collections;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The one persistent terrain clipmap (RingSet.Always; MASTER_PROMPT 14.5.2, 14.7 H1/H7, DEVELOPMENT.md ADR-002):
    /// 9 levels x 128 x 128 quads with 4 vertices per quad and inward-facing 2 m skirts on every level edge, in ONE mesh
    /// with ONE material (SRP Batcher friendly: the level index and spacing travel in the vertex data). Placement,
    /// collapse, geomorph, heights, normals, the plain clip and the bend all happen in RingTerrain.shader around
    /// _RingCam, so every camera (capture cameras too) gets a correct clipmap and nothing is updated on the CPU.
    /// The quads of levels 1..8 that are inside the finer level for every camera are left out of the mesh.
    /// </summary>
    public sealed class ClipmapBuilder : IBeatBuilder
    {
        public int Order => 10;
        public string Name => "Clipmap";

        public IEnumerator Build(BuildContext ctx)
        {
            var mb = new MeshBuilder();
            for (int k = 0; k < ClipmapLayout.Levels; k++)
            {
                ClipmapLayout.AppendLevel(mb, k);
                if ((k % 3) == 2) yield return null;
            }

            Mesh mesh = mb.Build("RingClipmap", false);
            int meshTris = mb.TriangleCount;
            mesh.bounds = ClipmapLayout.PreBendBounds();   // the vertices are grid indices; bounds = every placement
            yield return null;

            GameObject go = ctx.AddMesh("Terrain", mesh, RingMat.Terrain, RingSet.Always, true, TerrainConfig.ClipBendDistance);
            var info = go.AddComponent<TerrainInfo>();
            info.Kind = "Clipmap";
            info.Levels = ClipmapLayout.Levels;
            info.QuadsPerSide = ClipmapLayout.Quads;
            info.Spacing0 = ClipmapLayout.Spacing(0);
            info.Extent = ClipmapLayout.HalfExtent(ClipmapLayout.Levels - 1);
            info.MeshTriangles = meshTris;
            info.VisibleTriangles = TerrainConfig.ClipVisibleTriangles;
            ctx.Register("Terrain", go);

            Debug.Log("[Ring] Terrain clipmap: " + ClipmapLayout.Levels + " levels x " + ClipmapLayout.Quads + "^2 quads, spacing " +
                      info.Spacing0.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture) + " .. " +
                      ClipmapLayout.Spacing(ClipmapLayout.Levels - 1).ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                      " m, extent " + info.Extent.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m, " +
                      mb.VertexCount + " vertices, " + meshTris + " mesh triangles (" + info.VisibleTriangles + " raster)");
        }
    }
}
