using System.Collections;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 4 (MASTER_PROMPT 14.5.4): the terraced levee (x -70..18, z 558..640, 3 terraces of 14/3 m, 3 m setbacks on
    /// every side, turf berms and top, coursed limestone risers, ceramic copings, the zigzag stair with cheek walls and
    /// eight sconces with wall washes on the south face), its lit mouth (PaleGold lintel, a WarmLamp lamp on a bracket in
    /// front of the lintel, its light pool on the strip, the ground, the kerb tops, the threshold and the tube floor),
    /// the sealed L-shaped underpass (rounded rectangle 2.8 x 2.9, legs 18 + 18 m, mitred corner at (0, 576), ribs every
    /// 3 m, amber strips, exit portal at (18, 576) facing +X) - all in FarValley, so they persist after the swap - and
    /// the 12 beat-4 bollards (s 411-552, 12.82 m apart) with their light pools in NearValley (lamps and pools follow
    /// _Lamps.x, 0 -> 3.0 over u 0.30-0.60). The squares, the terminator and the city lights live in RingSky; the lane
    /// (2.1 m) and every corridor collider are PathGuide's; this builder adds only the bollard colliders.
    /// Registry: Underpass (the clear interior, lining vertices: 2.8 x 2.9, x -1.4..18, z 558..577.4), Underpass.Mouth,
    /// Levee (marker: the 88 x 14 x 82 m mound), Subject.Levee (the levee meshes), Bollard4_0..11 (markers 0.19 x 1.0).
    /// </summary>
    public sealed class Beat04SunsetBuilder : IBeatBuilder
    {
        public int Order => 400;
        public string Name => "Beat04Sunset";

        // Bend inflation: the levee and its lit mouth are the visible goal from s 24 (534 m away) and stay visible from
        // the plain looking back (up to ~1 km); the bollards are seen within ~300 m.
        const float LeveeBendDistance = 1200f;
        const float BollardBendDistance = 400f;
        // Bollard collider (the lane is 9.3 m wide here, PathGuide): a little larger than the 0.19 m cap, 1.1 m tall.
        static readonly Vector3 BollardCollider = new Vector3(0.26f, 1.1f, 0.26f);

        public IEnumerator Build(BuildContext ctx)
        {
            int tris0 = ctx.Triangles;
            var far = ctx.SetRoot(RingSet.FarValley);

            // ---------------- levee (FarValley)
            var levee = new GameObject("Levee").transform;
            levee.SetParent(far, false);
            var risers = new MeshBuilder();
            var copings = new MeshBuilder();
            var turf = new MeshBuilder();
            LeveeMeshes.Levee(risers, copings, turf);
            Beat34Geo.Add(ctx, "Levee.Risers", risers, Beat34Mat.LeveeStone, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Levee.Copings", copings, Beat34Mat.LeveeTrim, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Levee.Turf", turf, Beat34Mat.Turf, RingSet.FarValley, true, LeveeBendDistance, levee);
            // stair (treads in the levee stone, ceramic cheek walls, PaleGold rail caps) and the terrace sconces
            var stairStone = new MeshBuilder();
            var stairCheek = new MeshBuilder();
            var leveeGold = new MeshBuilder();
            var sconceLens = new MeshBuilder();
            LeveeMeshes.Stairs(stairStone, stairCheek, leveeGold);
            LeveeMeshes.SconceLamps(leveeGold, sconceLens);
            Beat34Geo.Add(ctx, "Levee.Stair", stairStone, Beat34Mat.LeveeStone, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Levee.StairCheeks", stairCheek, Beat34Mat.LeveeTrim, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Levee.Gold", leveeGold, RingMat.PaleGold, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Levee.SconceLenses", sconceLens, Beat34Mat.Lamp, RingSet.FarValley, false, LeveeBendDistance, levee);
            var sconcePools = new MeshBuilder();
            LeveeMeshes.SconcePools(sconcePools);
            Beat34Additive.AddToWorld(ctx, sconcePools, "Levee.SconcePools", RingMat.LightPool, RingSet.FarValley, LeveeBendDistance, levee);
            var stepLights = new MeshBuilder();
            LeveeMeshes.StepLights(stepLights);
            Beat34Additive.AddToWorld(ctx, stepLights, "Levee.StepLights", RingMat.Glow, RingSet.FarValley, LeveeBendDistance, levee);
            int leveeTris = ctx.Triangles - tris0;
            yield return null;

            // ---------------- portals, mouth lamp and its pool (FarValley)
            var portalCeramic = new MeshBuilder();
            var portalGold = new MeshBuilder();
            var lens = new MeshBuilder();
            LeveeMeshes.Portals(portalCeramic, portalGold, lens);
            Beat34Geo.Add(ctx, "Underpass.Portals", portalCeramic, RingMat.Ceramic, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Underpass.Lintels", portalGold, RingMat.PaleGold, RingSet.FarValley, true, LeveeBendDistance, levee);
            Beat34Geo.Add(ctx, "Underpass.MouthLamp", lens, Beat34Mat.Lamp, RingSet.FarValley, false, LeveeBendDistance, levee);
            var mouthPool = new MeshBuilder();
            LeveeMeshes.MouthPools(mouthPool);
            Beat34Additive.AddToWorld(ctx, mouthPool, "Underpass.MouthPool", RingMat.LightPool, RingSet.FarValley, LeveeBendDistance, levee);

            // ---------------- underpass (FarValley): lining, ribs, amber strips
            var under = new GameObject("Underpass").transform;
            under.SetParent(far, false);
            int t1 = ctx.Triangles;
            var lining = LeveeMeshes.Lining(new MeshBuilder());
            var liningGo = Beat34Geo.Add(ctx, "Underpass.Lining", lining, Beat34Mat.UnderpassLining, RingSet.FarValley, false, LeveeBendDistance, under);
            Beat34Geo.Add(ctx, "Underpass.Ribs", LeveeMeshes.Ribs(new MeshBuilder()), Beat34Mat.UnderpassRib, RingSet.FarValley, false, LeveeBendDistance, under);
            Beat34Geo.Add(ctx, "Underpass.Strips", LeveeMeshes.Strips(new MeshBuilder()), RingMat.EmissiveAmber, RingSet.FarValley, false, LeveeBendDistance, under);
            int underTris = ctx.Triangles - t1;
            yield return null;

            // ---------------- bollards and pools (NearValley)
            var near = ctx.SetRoot(RingSet.NearValley);
            var bollards = new GameObject("Bollards4").transform;
            bollards.SetParent(near, false);
            var shaft = new MeshBuilder();
            var bLens = new MeshBuilder();
            var cap = new MeshBuilder();
            LeveeMeshes.Bollards(shaft, bLens, cap);
            Beat34Geo.Add(ctx, "Bollards4.Shafts", shaft, RingMat.Ceramic, RingSet.NearValley, true, BollardBendDistance, bollards);
            Beat34Geo.Add(ctx, "Bollards4.Lenses", bLens, Beat34Mat.Lamp, RingSet.NearValley, false, BollardBendDistance, bollards);
            Beat34Geo.Add(ctx, "Bollards4.Caps", cap, RingMat.PaleGold, RingSet.NearValley, true, BollardBendDistance, bollards);
            var pools = new MeshBuilder();
            LeveeMeshes.BollardPools(pools);
            Beat34Additive.AddToWorld(ctx, pools, "Bollards4.Pools", RingMat.LightPool, RingSet.NearValley, BollardBendDistance, bollards);
            for (int i = 0; i < LeveeMeshes.BollardCount; i++)
            {
                float z = LeveeMeshes.BollardZ(i);
                ctx.AddBoxCollider("Bollard4.Collider" + i, new Vector3(LeveeMeshes.BollardX, 0.5f * BollardCollider.y, z), BollardCollider, Quaternion.identity);
                Beat34Geo.Marker(ctx, "Bollard4_" + i, bollards, new Vector3(LeveeMeshes.BollardX, 0.5f * LeveeMeshes.BollardH, z),
                                 new Vector3(LeveeMeshes.BollardTop, LeveeMeshes.BollardH, LeveeMeshes.BollardTop));
            }

            // ---------------- registry
            if (liningGo != null) ctx.Register("Underpass", liningGo);
            float hw = 0.5f * RingConstants.UnderpassW, h = RingConstants.UnderpassH;
            Beat34Geo.Marker(ctx, "Underpass.Mouth", under, new Vector3(LeveeMeshes.Corner.x, LeveeMeshes.FloorY + 0.5f * h, LeveeMeshes.MouthZ),
                             new Vector3(2f * hw, h, 0.2f));
            float lx0 = RingConstants.LeveeX0, lx1 = RingConstants.LeveeX1, lz0 = RingConstants.LeveeZ0, lz1 = RingConstants.LeveeZ1;
            Beat34Geo.Marker(ctx, "Levee", levee, new Vector3(0.5f * (lx0 + lx1), 0.5f * RingConstants.LeveeH, 0.5f * (lz0 + lz1)),
                             new Vector3(lx1 - lx0, RingConstants.LeveeH, lz1 - lz0));
            ctx.Register("Subject.Levee", levee.gameObject);
            ctx.Register("Underpass.Root", under.gameObject);
            yield return null;

            Debug.Log("[Ring] Beat04SunsetBuilder: levee + portals " + leveeTris + " triangles, underpass " + underTris +
                      " triangles, total " + (ctx.Triangles - tris0) + " (" + LeveeMeshes.BollardCount + " bollards s " +
                      LeveeMeshes.BollardS0 + ".." + LeveeMeshes.BollardS1 + ")");
        }
    }
}
