using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Invisible corridor colliders (Part 9 corridor guarantee, 14.4 walkable widths). Visible meshes never carry colliders;
    /// all physics is these boxes on layer Corridor, generated from the same AbyssConst numbers as the visible geometry.
    /// Walls are thick (2 m outward) and tall (4 m) so the 0.35 m capsule can never squeeze through a gap.
    /// </summary>
    public static class CorridorKit
    {
        public const int LayerCorridor = 8;
        public const int LayerSafety = 9;
        public const float WallThickness = 2f;
        public const float WallHeight = 4f;

        static GameObject Make(Transform parent, string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Oriented box collider.</summary>
        public static BoxCollider Box(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            var go = Make(parent, name, LayerCorridor);
            go.transform.SetPositionAndRotation(centre, rotation);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return bc;
        }

        /// <summary>Flat walkable slab whose TOP surface is at topCentre.y. sizeXZ is (width across, length along) in the yaw frame.</summary>
        public static BoxCollider Floor(Transform parent, string name, Vector3 topCentre, Vector2 sizeXZ, float yawDeg = 0f, float thickness = 1f)
        {
            Quaternion r = Quaternion.Euler(0f, yawDeg, 0f);
            Vector3 c = topCentre - new Vector3(0f, thickness * 0.5f, 0f);
            return Box(parent, name, c, new Vector3(sizeXZ.x, thickness, sizeXZ.y), r);
        }

        /// <summary>Sloped walkable slab whose top surface runs from floor point a to floor point b (stairs, ramps).</summary>
        public static BoxCollider Ramp(Transform parent, string name, Vector3 a, Vector3 b, float width, float thickness = 1f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion r = Quaternion.LookRotation(d / Mathf.Max(len, 1e-6f), Vector3.up);
            Vector3 up = r * Vector3.up;
            Vector3 c = (a + b) * 0.5f - up * (thickness * 0.5f);
            return Box(parent, name, c, new Vector3(width, thickness, len), r);
        }

        /// <summary>
        /// Vertical wall whose inner face runs along the horizontal segment innerA -> innerB, from baseY up WallHeight,
        /// extending WallThickness away from the walkable side. outwardIsRight: the wall lies to the right of the direction a->b
        /// (right = cross(up, dir)). extendEnds lengthens the wall at both ends to close corner gaps.
        /// </summary>
        public static BoxCollider Wall(Transform parent, string name, Vector3 innerA, Vector3 innerB, float baseY, bool outwardIsRight,
            float height = WallHeight, float thickness = WallThickness, float extendEnds = 0f)
        {
            Vector3 a = new Vector3(innerA.x, 0f, innerA.z), b = new Vector3(innerB.x, 0f, innerB.z);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            dir = len > 1e-6f ? dir / len : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 outward = outwardIsRight ? right : -right;
            Vector3 c = (a + b) * 0.5f + outward * (thickness * 0.5f);
            c.y = baseY + height * 0.5f;
            return Box(parent, name, c, new Vector3(thickness, height, len + 2f * extendEnds), Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>Two walls flush at +/- halfWidth either side of the centreline a -> b (horizontal).</summary>
        public static void WallPair(Transform parent, string name, Vector3 a, Vector3 b, float halfWidth, float baseY,
            float height = WallHeight, float extendEnds = 0f)
        {
            Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Wall(parent, name + "_R", a + right * halfWidth, b + right * halfWidth, baseY, true, height, WallThickness, extendEnds);
            Wall(parent, name + "_L", a - right * halfWidth, b - right * halfWidth, baseY, false, height, WallThickness, extendEnds);
        }

        /// <summary>Static mesh collider (curved ramps such as the ledge helix). The mesh must be closed on its walkable side.</summary>
        public static MeshCollider MeshSurface(Transform parent, string name, Mesh mesh)
        {
            var go = Make(parent, name, LayerCorridor);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            return mc;
        }

        /// <summary>Trigger volume (layer Safety). The player returns to its last safe point when it enters one.</summary>
        public static BoxCollider SafetyVolume(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            var go = Make(parent, name, LayerSafety);
            go.transform.SetPositionAndRotation(centre, rotation);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.isTrigger = true;
            return bc;
        }

        /// <summary>Safety volume 3 m below a walkable floor rectangle (topCentre = floor top centre).</summary>
        public static BoxCollider SafetyBelow(Transform parent, string name, Vector3 floorTopCentre, Vector2 sizeXZ, float yawDeg = 0f)
        {
            return SafetyVolume(parent, name, floorTopCentre - new Vector3(0f, 3.5f, 0f), new Vector3(sizeXZ.x + 8f, 1f, sizeXZ.y + 8f), Quaternion.Euler(0f, yawDeg, 0f));
        }
    }
}
