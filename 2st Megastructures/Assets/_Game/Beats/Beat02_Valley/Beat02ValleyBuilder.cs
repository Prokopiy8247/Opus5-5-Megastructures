using System.Collections;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 2 (MASTER_PROMPT 14.5.2; P2 from 14.5.4): the path strip with its kerbs and the two spurs (FarValley), the 20
    /// valley bollards and the path benches (NearValley), the arch bridge with lamp posts, light pools and its two
    /// benches, the belvedere P1 on the hill, the pavilion P2 and the 80-arch aqueduct (FarValley). Geometry comes from
    /// <see cref="ValleyGeo"/>; this builder creates the scene objects at load: renderers (shadows only within 220 m;
    /// the aqueduct never casts), the bridge deck MeshCollider from the visible deck top (14.4 colliders (2)), the
    /// bollard colliders and the registry. The cross path over the footbridge arch (PathSegments 11) is built by the
    /// beat-3 footbridge (it rides over its mound), so it is not duplicated here.
    /// Registry: Bridge.Deck (the deck slab and walkway), Bridge.Rail (posts and rails), Bridge.Crest (marker at the
    /// crest point), Subject.Bridge, Belvedere, Subject.Belvedere, Pavilion, Pavilion.P2, Aqueduct, plus Path.Strip,
    /// Path.Kerbs, Bridge.Arch, Bridge.Lamps, Valley.Bollards.
    /// </summary>
    public sealed class Beat02ValleyBuilder : IBeatBuilder
    {
        public int Order => 200;
        public string Name => "Beat02Valley";

        // bend inflation: the largest z distance from which each part is seen (camera anywhere on s 0..810)
        const float BendPath = 620f, BendBridge = 700f, BendRotunda = 820f, BendAqueduct = 1300f, BendNear = 420f;
        const int TriangleBudget = 590000 - 229376;   // 14.5.2: beat 2 <= 590 k including the 229 k terrain clipmap
        // bollard collider, as the beat-4 bollards: a little larger than the 0.19 m cap, 1.1 m tall
        static readonly Vector3 BollardCollider = new Vector3(0.26f, 1.1f, 0.26f);

        public IEnumerator Build(BuildContext ctx)
        {
            int tris0 = ctx.Triangles;

            // ---------------- path strip, kerbs, spurs (FarValley)
            var pathRoot = Root(ctx, "Path", RingSet.FarValley);
            var strip = new MeshBuilder(); var kerbs = new MeshBuilder();
            ValleyGeo.PathStrip(strip, kerbs);
            var stripGo = Add(ctx, "Path.Strip", strip, RingMat.PathStrip, RingSet.FarValley, false, BendPath, pathRoot);
            var kerbGo = Add(ctx, "Path.Kerbs", kerbs, RingMat.PathStrip, RingSet.FarValley, true, BendPath, pathRoot);
            Reg(ctx, "Path.Strip", stripGo); Reg(ctx, "Path.Kerbs", kerbGo);
            yield return null;

            // ---------------- bollards and path benches (NearValley)
            var nearRoot = Root(ctx, "ValleyNear", RingSet.NearValley);
            var shaft = new MeshBuilder(); var lens = new MeshBuilder(); var cap = new MeshBuilder();
            ValleyGeo.Bollards(shaft, lens, cap);
            var bollards = new GameObject("Valley.Bollards").transform;
            bollards.SetParent(nearRoot, false);
            Add(ctx, "Valley.Bollards.Shafts", shaft, Beat12Mat.Trim, RingSet.NearValley, true, BendNear, bollards);
            Add(ctx, "Valley.Bollards.Lenses", lens, RingMat.Emissive, RingSet.NearValley, false, BendNear, bollards);
            Add(ctx, "Valley.Bollards.Caps", cap, RingMat.PaleGold, RingSet.NearValley, true, BendNear, bollards);
            for (int i = 0; i < ValleyGeo.BollardCount; i++)
            {
                var p = ValleyGeo.BollardPos(i);
                ctx.AddBoxCollider("Valley.Bollard" + i, p + new Vector3(0f, 0.5f * BollardCollider.y, 0f), BollardCollider, Quaternion.identity);
            }
            ctx.Register("Valley.Bollards", bollards.gameObject);
            var benches = new MeshBuilder();
            foreach (var b in ValleyGeo.PathBenches) ValleyGeo.Bench(benches, b);
            Add(ctx, "Valley.Benches", benches, Beat12Mat.Trim, RingSet.NearValley, true, BendNear, nearRoot);
            yield return null;

            // ---------------- bridge (FarValley)
            var bridge = Root(ctx, "Bridge", RingSet.FarValley);
            var deckRoot = new GameObject("Bridge.Deck").transform;
            deckRoot.SetParent(bridge, false);
            var top = new MeshBuilder(); var slab = new MeshBuilder(); var col = new MeshBuilder();
            ValleyGeo.Deck(top, slab, col);
            Add(ctx, "Bridge.Deck.Walkway", top, RingMat.PathStrip, RingSet.FarValley, true, BendBridge, deckRoot);
            Add(ctx, "Bridge.Deck.Slab", slab, RingMat.Ceramic, RingSet.FarValley, true, BendBridge, deckRoot);
            ctx.AddMeshCollider("Bridge.DeckCollider", col.Build("Bridge.DeckCollider", false));
            ctx.Register("Bridge.Deck", deckRoot.gameObject);

            var structure = new MeshBuilder();
            ValleyGeo.Abutments(structure);
            ValleyGeo.Arch(structure);
            var archGo = Add(ctx, "Bridge.Arch", structure, RingMat.Ceramic, RingSet.FarValley, true, BendBridge, bridge);
            Reg(ctx, "Bridge.Arch", archGo);

            var rails = new MeshBuilder();
            ValleyGeo.Rails(rails);
            var railGo = Add(ctx, "Bridge.Rail", rails, Beat12Mat.Trim, RingSet.FarValley, true, BendBridge, bridge);
            Reg(ctx, "Bridge.Rail", railGo);
            yield return null;

            var lamps = new GameObject("Bridge.Lamps").transform;
            lamps.SetParent(bridge, false);
            var post = new MeshBuilder(); var gold = new MeshBuilder(); var lensB = new MeshBuilder(); var pools = new MeshBuilder(); var glows = new MeshBuilder();
            ValleyGeo.Lamps(post, gold, lensB, pools, glows);
            Add(ctx, "Bridge.Lamps.Posts", post, Beat12Mat.Trim, RingSet.FarValley, true, BendBridge, lamps);
            Add(ctx, "Bridge.Lamps.Caps", gold, RingMat.PaleGold, RingSet.FarValley, true, BendBridge, lamps);
            Add(ctx, "Bridge.Lamps.Lenses", lensB, RingMat.Emissive, RingSet.FarValley, false, BendBridge, lamps);
            Additive(ctx, "Bridge.Lamps.Pools", pools, RingMat.LightPool, BendBridge, lamps, 0f);
            Additive(ctx, "Bridge.Lamps.Glows", glows, RingMat.Glow, BendBridge, lamps, BendBridge);
            ctx.Register("Bridge.Lamps", lamps.gameObject);

            var bBench = new MeshBuilder();
            foreach (var b in ValleyGeo.BridgeBenches) ValleyGeo.Bench(bBench, b);
            Add(ctx, "Bridge.Benches", bBench, Beat12Mat.Trim, RingSet.FarValley, true, BendBridge, bridge);

            var crest = new GameObject("Bridge.Crest");
            crest.transform.SetParent(bridge, false);
            crest.transform.localPosition = new Vector3(0f, ValleyGeo.DeckY(RingConstants.BridgeCrestZ), RingConstants.BridgeCrestZ);
            ctx.Register("Bridge.Crest", crest);
            ctx.Register("Subject.Bridge", bridge.gameObject);
            yield return null;

            // ---------------- belvedere P1 and pavilion P2 (FarValley, one rotunda generator)
            var bel = ValleyGeo.Belvedere();
            var belRoot = RotundaObject(ctx, "Belvedere", bel, RingMat.Ceramic);
            ctx.Register("Belvedere", belRoot);
            ctx.Register("Subject.Belvedere", belRoot);
            var pav = ValleyGeo.Pavilion();
            var pavRoot = RotundaObject(ctx, "Pavilion", pav, RingMat.Ceramic);
            ctx.Register("Pavilion", pavRoot);
            ctx.Register("Pavilion.P2", pavRoot);
            yield return null;

            // ---------------- aqueduct (FarValley, 1.15 km away: no shadows)
            var aq = new MeshBuilder();
            int module = ValleyGeo.Aqueduct(aq);
            var aqGo = Add(ctx, "Aqueduct", aq, Beat12Mat.Aqueduct, RingSet.FarValley, false, BendAqueduct, Root(ctx, "AqueductRoot", RingSet.FarValley));
            Reg(ctx, "Aqueduct", aqGo);

            int tris = ctx.Triangles - tris0;
            Debug.Log("[Ring] Beat02ValleyBuilder: " + tris + " triangles (budget " + TriangleBudget + " beside the terrain), belvedere floor y "
                      + bel.FloorY.ToString("0.00") + " (hill " + RingWorld.Height(bel.XZ.x, bel.XZ.y, 0f).ToString("0.00") + "), pavilion floor y "
                      + pav.FloorY.ToString("0.00") + ", aqueduct " + ValleyGeo.AqueductBays + " bays x " + module + " triangles"
                      + (tris > TriangleBudget ? " OVER BUDGET" : ""));
            yield return null;
        }

        static GameObject RotundaObject(BuildContext ctx, string name, ValleyGeo.Rotunda r, string mat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(ctx.SetRoot(RingSet.FarValley), false);
            var body = new MeshBuilder(); var gold = new MeshBuilder(); var floor = new MeshBuilder();
            ValleyGeo.BuildRotunda(r, body, gold, floor);
            Add(ctx, name + ".Body", body, mat, RingSet.FarValley, true, BendRotunda, root.transform);
            Add(ctx, name + ".Gold", gold, RingMat.PaleGold, RingSet.FarValley, true, BendRotunda, root.transform);
            Add(ctx, name + ".Floor", floor, RingMat.PathStrip, RingSet.FarValley, true, BendRotunda, root.transform);
            return root;
        }

        static Transform Root(BuildContext ctx, string name, RingSet set)
        {
            var t = new GameObject(name).transform;
            t.SetParent(ctx.SetRoot(set), false);
            return t;
        }

        static GameObject Add(BuildContext ctx, string name, MeshBuilder mb, string mat, RingSet set, bool shadows, float bend, Transform parent)
        {
            if (mb.VertexCount == 0) return null;
            return ctx.AddMesh(name, mb.Build(name), mat, set, shadows, bend, parent);
        }

        static void Additive(BuildContext ctx, string name, MeshBuilder mb, string mat, float bend, Transform parent, float billboardDistance)
        {
            if (mb.VertexCount == 0) return;
            var mesh = mb.Build(name, false);
            if (billboardDistance > 0f) mesh.bounds = B12Additive.GrowForBillboards(mesh.bounds, billboardDistance);
            var go = ctx.AddMesh(name, mesh, mat, RingSet.FarValley, false, bend, parent);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.receiveShadows = false;
        }

        static void Reg(BuildContext ctx, string key, GameObject go)
        {
            if (go != null) ctx.Register(key, go);
        }
    }
}
