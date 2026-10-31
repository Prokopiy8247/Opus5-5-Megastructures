using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 1 (MASTER_PROMPT 14.5.1): the docking capsule, its hatch, the gangway and the landing pad, all in
    /// RingSet.Capsule. Geometry comes from <see cref="CapsuleGeo"/> (pure maths); this builder only creates the scene
    /// objects at load: renderers (every shell part casts shadows, so the key enters only through the porthole), the
    /// hatch leaves with their colliders and the <see cref="CapsuleHatch"/> animator, the static jamb colliders at
    /// x = +-1.0 (14.4 colliders (8)), the ramp MeshCollider from the visible deck (14.4 colliders (2)), the two
    /// Practical2700 point lights (range 5 m, 1.2, no shadows) and the registry markers.
    /// Registry: Capsule.Interior (marker 2.6 x 2.5 x 11.0), Capsule.Porthole (marker 0.8 m at the porthole centre on its
    /// axis), Capsule.Hatch (marker = the 2.0 x 1.9 opening), Subject.Porthole (the porthole frame meshes), plus
    /// Capsule.Root, Capsule.LeafLeft, Capsule.LeafRight, Gangway.Deck, Gangway.Rails, LandingPad.
    /// </summary>
    public sealed class Beat01CapsuleBuilder : IBeatBuilder
    {
        public int Order => 100;
        public string Name => "Beat01Capsule";

        // 14.2.3 row "1": 2 strips + 2 points (range 5 m, intensity 1.2), capsule strips 2700 K
        const float LightRange = 5f, LightIntensity = 1.2f;
        // the capsule is seen from at most ~300 m (looking back from the valley); bend inflation for that range
        const float Bend = 320f;
        const int TriangleBudget = 60000;   // 14.5.1: limit 60 k

        public IEnumerator Build(BuildContext ctx)
        {
            int tris0 = ctx.Triangles;
            var m = CapsuleGeo.Build();
            yield return null;

            var set = RingSet.Capsule;
            var root = new GameObject("Capsule").transform;
            root.SetParent(ctx.SetRoot(set), false);
            ctx.Register("Capsule.Root", root.gameObject);

            Add(ctx, "Capsule.Interior.Shell", m.Interior, Beat12Mat.CapsuleInterior, root, true);
            Add(ctx, "Capsule.Floor", m.Floor, Beat12Mat.CapsuleFloor, root, true);
            Add(ctx, "Capsule.Ribs", m.Rib, Beat12Mat.CapsuleRib, root, true);
            Add(ctx, "Capsule.PortholeBay", m.Bay, Beat12Mat.CapsuleBay, root, true);
            Add(ctx, "Capsule.Strips", m.Strip, Beat12Mat.CapsuleStrip, root, false);
            Add(ctx, "Capsule.Exterior", m.Exterior, RingMat.Ceramic, root, true);
            Add(ctx, "Capsule.Cradle", m.Cradle, RingMat.Metal, root, true);
            var porthole = new GameObject("Capsule.PortholeFrame").transform;
            porthole.SetParent(root, false);
            Add(ctx, "Capsule.PortholePanel", m.PortFrame, Beat12Mat.CapsuleFrame, porthole, true);
            Add(ctx, "Capsule.Fittings", m.Metal, RingMat.Metal, root, true);
            Add(ctx, "Capsule.PortholeRing", m.Gold, Beat12Mat.CapsuleGold, porthole, true);
            yield return null;

            // ---------------- hatch: leaves (Metal, no world-space panel lines that would swim), colliders, animator
            var hatchGo = new GameObject("Capsule.Hatch.Leaves");
            hatchGo.transform.SetParent(root, false);
            float leafY = 0.5f * (CapsuleGeo.LeafY0 + CapsuleGeo.LeafY1);
            var leafL = AddLeaf(ctx, "Capsule.LeafLeft", m.LeafLeft, hatchGo.transform, leafY, out var colL);
            var leafR = AddLeaf(ctx, "Capsule.LeafRight", m.LeafRight, hatchGo.transform, leafY, out var colR);
            var hatch = hatchGo.AddComponent<CapsuleHatch>();
            hatch.LeafLeft = leafL; hatch.LeafRight = leafR;
            hatch.ColliderLeft = colL; hatch.ColliderRight = colR;
            hatch.Apply(0f);
            ctx.Register("Capsule.LeafLeft", leafL.gameObject);
            ctx.Register("Capsule.LeafRight", leafR.gameObject);

            // static jamb colliders (inner faces at x = +-1.0, 1 m thick, the full ring depth)
            for (int s = -1; s <= 1; s += 2)
            {
                var c = new Vector3(s * (CapsuleGeo.OpenX + 0.5f), 2.0f, 0.5f * (RingConstants.HatchZ0 + RingConstants.HatchZ1));
                ctx.AddBoxCollider(s < 0 ? "Capsule.JambLeft" : "Capsule.JambRight", c,
                    new Vector3(1f, 4.0f, RingConstants.HatchZ1 - RingConstants.HatchZ0), Quaternion.identity);
            }

            // ---------------- lights inside (no shadows)
            var lights = new Light[2];
            Color lc = StripLightColour().gamma;
            // The strips throw their light downward: each point sits 0.45 m under the ceiling (a virtual source for the
            // strip pair, not a hot spot on the ceiling), at z 1.5 and 7.0, so the porthole bay (z 5.65) stays the dim
            // frame of KS01 and the key's warm patch on the right wall near z 4.5 falls between the two pools.
            float[] lz = { 1.5f, 7.0f };
            for (int i = 0; i < 2; i++)
            {
                var lg = new GameObject("Capsule.Light" + i);
                lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3(0f, RingConstants.CapsuleCeilY - 0.45f, lz[i]);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = LightRange;
                l.intensity = LightIntensity * RingConstants.LightCal;   // 14.2.3 light calibration
                l.color = lc;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.Auto;
                lights[i] = l;
            }
            hatch.Lights = lights;
            ctx.Animated.Add(hatch);

            // ---------------- gangway, pad
            var gang = new GameObject("Gangway").transform;
            gang.SetParent(root, false);
            var deck = Add(ctx, "Gangway.Deck", m.RampDeck, Beat12Mat.CapsuleFloor, gang, true);
            Add(ctx, "Gangway.Frame", m.RampFrame, RingMat.Metal, gang, true);
            var rails = Add(ctx, "Gangway.Rails", m.Rails, RingMat.Metal, gang, true);
            var pad = Add(ctx, "LandingPad", m.Pad, RingMat.Deck, gang, true);
            var colMesh = m.RampCollider.Build("Gangway.DeckCollider", false);
            ctx.AddMeshCollider("Gangway.DeckCollider", colMesh);
            if (deck != null) ctx.Register("Gangway.Deck", deck);
            if (rails != null) ctx.Register("Gangway.Rails", rails);
            if (pad != null) ctx.Register("LandingPad", pad);

            // ---------------- motes (additive, no shadows; the drift and turbulence are animated in the shader)
            if (m.Motes.VertexCount > 0)
            {
                var motes = m.Motes.Build("Capsule.Motes", false);
                motes.bounds = B12Additive.GrowForBillboards(motes.bounds, 30f);
                var b = motes.bounds; b.Expand(0.4f); motes.bounds = b;     // shader drift amplitude
                var go = ctx.AddMesh("Capsule.Motes", motes, RingMat.Mote, set, false, 30f, root);
                Lightless(go);
            }

            // ---------------- registry markers (14.10 numeric tests, composition)
            Marker(ctx, "Capsule.Interior", root, new Vector3(0f, 0.5f * (RingConstants.CapsuleFloorY + RingConstants.CapsuleCeilY), 0.5f * (RingConstants.CapsuleZ0 + RingConstants.CapsuleZ1)),
                Quaternion.identity, new Vector3(RingConstants.CapsuleInnerW, RingConstants.CapsuleInnerH, RingConstants.CapsuleZ1 - RingConstants.CapsuleZ0));
            Vector3 axis = m.Porthole.N;
            Marker(ctx, "Capsule.Porthole", root, RingConstants.PortholeCentre, Quaternion.LookRotation(axis, Vector3.up),
                new Vector3(RingConstants.PortholeFrame, RingConstants.PortholeFrame, 0.06f));
            Marker(ctx, "Capsule.Hatch", root, new Vector3(0f, CapsuleGeo.OpenY0 + 0.5f * RingConstants.HatchH, 0.5f * (RingConstants.HatchZ0 + RingConstants.HatchZ1)),
                Quaternion.identity, new Vector3(RingConstants.HatchW, RingConstants.HatchH, RingConstants.HatchZ1 - RingConstants.HatchZ0));
            ctx.Register("Subject.Porthole", porthole.gameObject);

            int tris = ctx.Triangles - tris0;
            Debug.Log("[Ring] Beat01CapsuleBuilder: " + tris + " triangles (budget " + TriangleBudget + "), porthole generators "
                      + m.Porthole.Phi.Count + (tris > TriangleBudget ? " OVER BUDGET" : ""));
            yield return null;
        }

        /// <summary>
        /// Linear colour of the two strip point lights. The strips themselves glow Practical2700 (#FFC78A, 14.2.1); the light
        /// they throw is that colour as the beat-1 camera sees it on a white wall: halfway to white, with the luminance of
        /// Practical2700 (so the photometric output of the spec's 1.2 is unchanged). Full Practical2700 on CeramicWhite
        /// under the warm beat-1 grade gave saturation 0.37-0.43 (khaki cardboard, critique run04 C18); this gives about
        /// 0.15-0.2 (warm white). If the lead balances the beat-1 white balance toward 3 200 K, set StripChroma back to 1.
        /// </summary>
        const float StripChroma = 0.5f;
        internal static Color StripLightColour()
        {
            Color p = RingPalette.Lin("Practical2700");
            Color c = Color.Lerp(Color.white, p, StripChroma);
            float lp = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
            float lc = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            float k = lp / Mathf.Max(lc, 1e-4f);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        static GameObject Add(BuildContext ctx, string name, MeshBuilder mb, string mat, Transform parent, bool shadows)
        {
            if (mb.VertexCount == 0) return null;
            var mesh = mb.Build(name);
            var go = ctx.AddMesh(name, mesh, mat, RingSet.Capsule, shadows, Bend, parent);
            return go;
        }

        static Transform AddLeaf(BuildContext ctx, string name, MeshBuilder mb, Transform parent, float y, out BoxCollider col)
        {
            var mesh = mb.Build(name);
            var go = ctx.AddMesh(name, mesh, RingMat.Metal, RingSet.Capsule, true, Bend, parent);
            go.transform.localPosition = new Vector3(0f, y, CapsuleGeo.LeafZ);
            col = go.AddComponent<BoxCollider>();
            // the visible leaf, 10 cm deep (the full slot) so a fast player cannot tunnel through it
            col.size = new Vector3(CapsuleGeo.LeafW, CapsuleGeo.LeafY1 - CapsuleGeo.LeafY0, CapsuleGeo.SlotZ1 - CapsuleGeo.SlotZ0);
            col.center = Vector3.zero;
            return go.transform;
        }

        static void Marker(BuildContext ctx, string key, Transform parent, Vector3 pos, Quaternion rot, Vector3 size)
        {
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = size;
            ctx.Register(key, go);
        }

        static void Lightless(GameObject go)
        {
            if (go == null) return;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) { r.receiveShadows = false; r.shadowCastingMode = ShadowCastingMode.Off; }
        }
    }
}
