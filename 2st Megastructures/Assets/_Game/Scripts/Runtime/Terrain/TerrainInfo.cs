using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Measured numbers of a terrain object for tests and composition checks (registry keys "Terrain", "River.Ribbon",
    /// "Grass", "Flowers"). The clipmap, the ribbon and the fields are placed or bent in their vertex shaders, so their
    /// renderer bounds do not give these dimensions directly.
    /// </summary>
    public sealed class TerrainInfo : MonoBehaviour
    {
        public string Kind;
        /// <summary>Clipmap: levels, quads per side, finest spacing, half-extent of the last level.</summary>
        public int Levels, QuadsPerSide;
        public float Spacing0, Extent;
        /// <summary>Triangles in the mesh (or per instance x launched) and the triangles that actually raster.</summary>
        public int MeshTriangles, VisibleTriangles;
        /// <summary>River: ribbon width, visible (waterline) width, surface height.</summary>
        public float RibbonWidth, WaterlineWidth, SurfaceY;
        /// <summary>Fields: launched and expected drawn instances, grid cell, fade start / end.</summary>
        public int Instances, ExpectedDrawn;
        public float Cell, FadeStart, FadeEnd;
    }
}
