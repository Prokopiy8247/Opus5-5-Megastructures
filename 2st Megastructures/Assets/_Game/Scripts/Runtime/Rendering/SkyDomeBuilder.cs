using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// M05 sky dome object (14.2.6, ADR-003). A 64 x 32 unit sphere drawn with RingMat.Sky: the RingSky vertex
    /// shader normalises the object-space position, places the vertex at _DomeRadius around _WorldSpaceCameraPos
    /// and forces it onto the far plane (reversed-Z aware), so the mesh itself only needs directions. Outward faces
    /// (MeshBuilder.Sphere) are seen from inside through the shader's Cull Front. Always visible: RingSet.Always,
    /// no shadows, and bounds of 1e6 m so the camera-following dome is never frustum culled anywhere in the world.
    /// Registered as "Sky.Dome" (T3 / H10 tests disable this renderer to prove reflections never sample it).
    /// </summary>
    public sealed class SkyDomeBuilder : IBeatBuilder
    {
        public const string RegistryKey = "Sky.Dome";
        const int Segments = 64;        // task spec: 64 x 32 sphere
        const int Rings = 32;
        const float BoundsSize = 1e6f;  // never culled (the shader moves the dome with every camera)

        public int Order => 0;
        public string Name => "SkyDome";

        public IEnumerator Build(BuildContext ctx)
        {
            var mb = new MeshBuilder();
            mb.Sphere(Vector3.zero, 1f, Segments, Rings);
            Mesh mesh = mb.Build("SkyDome", false);
            GameObject go = ctx.AddMesh("SkyDome", mesh, RingMat.Sky, RingSet.Always, false, 0f);
            // AddMesh inflated the 1 m bounds for the bend; the dome needs bounds that contain every camera instead.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(BoundsSize, BoundsSize, BoundsSize));
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.receiveShadows = false;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            ctx.Register(RegistryKey, go);
            yield break;
        }
    }
}
