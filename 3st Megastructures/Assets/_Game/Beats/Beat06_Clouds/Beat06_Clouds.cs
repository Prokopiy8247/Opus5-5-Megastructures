// Beat06_Clouds: the station beads climbing the cable (MASTER_PROMPT 14.5.6, M08), built at load.
//  * Bead_S1..S9: LensBead lathes at y = BeadY0 + BeadYStep (k - 1) (D-035: 230 .. 550 m, the whole column above
//    the pier), radius 62.0, half-height 12.0, 288 x 48 segments (critique run06 B1 / A9 / C-13 / E9: smooth
//    silhouettes), colours cycling BeadMint, BeadPeach, BeadLilac; no shadow casting (62 m giants, 14.2.4), they
//    receive the shadow map and the analytic cable shadow through their material. A bead whose equator the deck
//    passes carries the porch of its door in the same mesh (same material); under D-035 there is none.
//  * Bead_Doors: the INK door leaves (BeadDoor), only built when a bead has a door (none under D-035).
//  * Window dots: 3 rows of 48 per bead (minus those under a door porch or an arm root; none under D-035, so
//    9 x 144 = 1 296), instanced with Graphics.RenderMeshInstanced, one draw per bead and row (each row has its
//    own bent strip mesh), a MaterialPropertyBlock per bead sets _SecondColor = that bead's palette colour (the
//    far-fade colour).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    public sealed class Beat06_Clouds : IBeatBuilder
    {
        public string Name => "Beat06_Clouds";

        static readonly int s_secondColor = Shader.PropertyToID(ShaderIds.SecondColor);

        public static Material BeadMaterial(MaterialLibrary lib, int k)
        {
            if (lib == null) return null;
            int m = k % 3;
            return m == 0 ? lib.BeadMint : (m == 1 ? lib.BeadPeach : lib.BeadLilac);
        }

        public void Build(WorldContext ctx)
        {
            var root = new GameObject(Name).transform;
            root.SetParent(ctx.NearRoot, false);
            MaterialLibrary lib = ctx.Materials;
            int layer = ctx.NearRoot != null ? ctx.NearRoot.gameObject.layer : 0;

            // lenses (+ door porches)
            long lensTris = 0;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                var mb = new MeshBuilder((LensBead.SegmentsAzimuth + 1) * (LensBead.SegmentsProfile + 1) + 256);
                LensBead.BuildLens(mb, k);
                if (LensBead.HasDoor(k)) LensBead.BuildDoorPorch(mb, LensBead.Door(k));
                Mesh lens = mb.ToMesh($"Bead_S{k + 1}");
                ctx.AddMesh(root, $"Bead_S{k + 1}", lens, BeadMaterial(lib, k), false, true);
                lensTris += MeshKit.TriangleCount(lens);
            }

            // door leaves (only on beads whose equator the deck passes; none under D-035)
            int doorBeads = LensBead.DoorBeadCount;
            if (doorBeads > 0)
            {
                var db = new MeshBuilder(256);
                for (int k = 0; k < LensBead.DoorCount; k++) if (LensBead.HasDoor(k)) LensBead.BuildDoorLeaf(db, LensBead.Door(k));
                Mesh doors = db.ToMesh("Bead_Doors");
                ctx.AddMesh(root, "Bead_Doors", doors, lib != null ? lib.BeadDoor : null, false, true);
            }

            // window dots (instanced)
            int rows = ThreadConstants.WindowRingOffsets.Length;
            var rowMesh = new Mesh[rows];
            var rowSrc = new MeshBuilder[rows];
            var rowY0 = new double[rows];
            var rowY1 = new double[rows];
            for (int r = 0; r < rows; r++)
            {
                rowSrc[r] = new MeshBuilder(64);
                LensBead.BuildWindowRow(rowSrc[r], r, out rowY0[r], out rowY1[r]);
                rowMesh[r] = rowSrc[r].ToMesh($"WindowRow_{r}");
            }
            Material winMat = lib != null ? lib.Window : null;
            var windows = new InstancedSet("BeadWindows", 0f);
            var matrices = new List<Matrix4x4>(ThreadConstants.WindowsPerRing);
            int total = 0;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                var mpb = new MaterialPropertyBlock();
                // SetVector: the shader receives the linear palette value as-is (no sRGB conversion of SetColor)
                Color c = ThreadPalette.Linear(LensBead.BeadPal(k));
                mpb.SetVector(s_secondColor, new Vector4(c.r, c.g, c.b, 1f));
                for (int r = 0; r < rows; r++)
                {
                    var wins = LensBead.Windows(k, r, rowY0[r], rowY1[r]);
                    matrices.Clear();
                    for (int i = 0; i < wins.Count; i++) matrices.Add(wins[i].matrix);
                    total += matrices.Count;
                    windows.Add(rowMesh[r], rowSrc[r], winMat, mpb, matrices, InstancedSet.MaxPerCall, layer, ShadowCastingMode.Off, false);
                }
            }
            windows.Finish();
            if (winMat == null) ctx.Log($"{Name}: Window material missing, window dots not drawn");
            if (SystemInfo.supportsInstancing) ctx.Register(windows);
            else { windows.MakeStatic(ctx, root); ctx.Log($"{Name}: instancing unsupported, windows baked static"); }
            ctx.Triangles += windows.Triangles;

            ctx.Log($"{Name}: {ThreadConstants.BeadCount} beads ({LensBead.SegmentsAzimuth} x {LensBead.SegmentsProfile}) {lensTris} t, {doorBeads} doors, {total} window dots in {windows.ChunkCount} instanced draws");
        }
    }
}
