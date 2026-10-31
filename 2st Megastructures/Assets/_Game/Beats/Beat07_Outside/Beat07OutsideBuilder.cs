using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 7 - outside platform, outer face, hull, Dyson swarm, beacon (MASTER_PROMPT 14.5.7, 14.6 boundary 6 -> 7,
    /// 14.7 H3 / H6, DEVELOPMENT.md ADR-008 and ADR-011). Everything is generated at load into RingSet.Outside:
    /// - Deck (ADR-011 pier): a 72 x 12 x 1.2 m slab (x 234..306, z 654..666, top y 0 = tunnel floor; 2 m tiles from
    ///   the Deck material), 2.5 cm chamfers, 1 m strips along z for a smooth bend; 6 tapering cantilever beams; rails
    ///   (posts 0.08 m every <= 3 m: 25 per long side and 5 on the front, corners shared; 0.10 m round top and mid rails
    ///   with rounded corners at z 660 +- 5.5 and x 305.5; kick plates); 6 lamp bollards with WarmLamp bands and light
    ///   pools; casts shadows (row 7 of 14.2.3: 3 cascades over 90 m, the deck assembly is the only caster outside).
    /// - Outer face: the plane x 234 facing +X (+-60 km, polar structured mesh round the tunnel-profile hole, no
    ///   T-junction) plus the PaleGold mouth frame (RingOuterFace, RING_NO_BEND, darkened to SpaceBlack by 55 km).
    /// - Hull: a static 7-level nested clipmap (2..128 m, half-extent 8 192 m) displaced into the convex spherical cap
    ///   (R 30 000, top y -200 under (300, 660), ADR-011) by RingHull (M09, RING_NO_BEND), with ridges, seams, streaks,
    ///   analytic horizon self-shadow and analytic gantry shadows; 24 gantries 40 x 3 m on the cap (same material),
    ///   placed in the band of hull visible beyond the side rails (712 m .. 3.5 km).
    /// - Swarm: <see cref="SwarmField"/> (three indirect draws, M10).
    /// - Beacon: lathe pylon 1.3 m with the amber lens (RingBeaconLens, own global _BeaconPulse), base disc r 1.0, point
    ///   light range 6 m / 3.0 BeaconAmber (no shadows), capsule collider r 0.5; pulse by <see cref="BeaconPulse"/>.
    /// The star is a sky element of RingSky (no mesh). Colliders of the deck floor, rails and front come from
    /// PathGuide; this builder adds the bollard and beacon colliders.
    /// Registry: Platform.Deck, Platform.Rail, Platform.Beams, Platform.Bollards, OuterFace, Hull, Hull.Cap (marker of the
    /// GPU-displaced cap), Hull.Gantries, Swarm, Beacon, Beacon.Lens, Beacon.Light, Subject.Beacon, Subject.Star,
    /// Subject.Swarm.
    /// </summary>
    public sealed class Beat07OutsideBuilder : IBeatBuilder
    {
        public int Order => 700;
        public string Name => "Beat07Outside";

        const RingSet Set = RingSet.Outside;

        public IEnumerator Build(BuildContext ctx)
        {
            int tris0 = ctx.Triangles;
            BeaconPulse.ResetStatics();
            var ci = CultureInfo.InvariantCulture;

            // ------------------------------------------------------------------ deck
            int deckTris0 = ctx.Triangles;
            BuildDeck(ctx);
            int deckTris = ctx.Triangles - deckTris0;
            yield return null;

            // ------------------------------------------------------------------ beacon
            int beaconTris0 = ctx.Triangles;
            BuildBeacon(ctx);
            int beaconTris = ctx.Triangles - beaconTris0;
            yield return null;

            // ------------------------------------------------------------------ outer face
            var face = new MeshBuilder();
            OutsideMeshes.OuterFace(face);
            OutsideMeshes.MouthFrame(face);
            var faceMesh = face.Build("OuterFace", true);
            Check(faceMesh);
            var faceGo = ctx.AddMesh("OuterFace", faceMesh, RingMat.OuterFace, Set, false, 0f);
            ctx.Register("OuterFace", faceGo);
            yield return null;

            // ------------------------------------------------------------------ hull + gantries
            var hullMesh = OutsideMeshes.HullClipmap(out int hullTris);
            Check(hullMesh);
            var hullGo = ctx.AddMesh("Hull", hullMesh, RingMat.Hull, Set, false, 0f);
            NoShadows(hullGo);
            // "Hull" is the renderer; its readable mesh is the flat CPU grid (y -3..0) that RingHull displaces on the
            // GPU, so a vertex measurement of it gives the plan extent only. "Hull.Cap" is a marker of the displaced
            // cap (x, z +-8 192 m about (300, 660), y about -2 930..-582). Not a "Subject." key on purpose: registry
            // subjects are bent (and clamped to y >= -50) by CompositionCheck, which is wrong for RING_NO_BEND content.
            ctx.Register("Hull", hullGo);
            var cap = OutsideMeshes.HullCapBounds();
            Marker(ctx, "Hull.Cap", cap.center, cap.size);
            yield return null;

            var gantryMesh = OutsideMeshes.Gantries(out int gantryTris);
            Check(gantryMesh);
            var gantryGo = ctx.AddMesh("HullGantries", gantryMesh, RingMat.Hull, Set, false, 0f);
            NoShadows(gantryGo);
            ctx.Register("Hull.Gantries", gantryGo);
            yield return null;

            // ------------------------------------------------------------------ swarm
            var swarmGo = new GameObject("Swarm");
            swarmGo.transform.SetParent(ctx.SetRoot(Set), false);
            var swarm = swarmGo.AddComponent<SwarmField>();
            swarm.Init(ctx);
            ctx.Register("Swarm", swarmGo);
            Debug.Log(swarm.FactsLine());

            // Subject.Swarm: star-frame box (x = a right, y = b up, z = w) round the star holding every band
            SwarmField.StarFrame(out Vector3 w, out Vector3 a, out Vector3 b);
            Vector3 star = OutsideLayout.StarPos;
            var sb = swarm.StarFrameBounds;
            Vector3 sc = star + a * sb.center.x + b * sb.center.y + w * sb.center.z;
            var swarmMarker = Marker(ctx, "Subject.Swarm", sc, sb.size);
            swarmMarker.transform.localRotation = Quaternion.LookRotation(w, b);

            // Subject.Star: 40 km along L7 from the KS12 eye, a 838 m radius bounds helper (marker of 2 x 838 m)
            float starD = 2f * OutsideLayout.StarDiscRadiusM;
            var starMarker = Marker(ctx, "Subject.Star", star, new Vector3(starD, starD, starD));
            starMarker.transform.localRotation = Quaternion.LookRotation(w, b);
            yield return null;

            OutsideLayout.HullHorizon(OutsideLayout.DeckTopY + RingConstants.Eye - OutsideLayout.HullTopY, out float horizon, out float dip);
            Debug.Log("[Ring] Beat07Outside built: " + (ctx.Triangles - tris0) + " triangles (deck " + deckTris + ", beacon " + beaconTris
                      + ", hull " + hullTris + ", gantries " + gantryTris + ", face " + faceMesh.GetIndexCount(0) / 3
                      + "; swarm " + swarm.PanelCount + " instanced quads = " + (2 * swarm.PanelCount) + " triangles); hull horizon "
                      + horizon.ToString("0", ci) + " m, dip " + dip.ToString("0.00", ci) + " deg from the eye (ADR-011: 3 470 m, 6.6 deg)");

            // ADR-011 visibility check: over a deck edge at horizontal distance e the hull shows where atan(Eye / e) > dip,
            // i.e. e < Eye / tan(dip) (14.7 m); the side edges are 6 m from the walk line, so beyond both rails at azimuth
            // >= asin(6 / 14.7) off the walk, from 201.7 x 6 / 1.7 = 712 m lateral out to the horizon.
            float halfW = 0.5f * (RingConstants.PlatformZ1 - RingConstants.PlatformZ0);
            float reach = RingConstants.Eye / Mathf.Max(Mathf.Tan(dip * Mathf.Deg2Rad), 1e-3f);
            float az = Mathf.Asin(Mathf.Clamp01(halfW / Mathf.Max(reach, 1e-3f))) * Mathf.Rad2Deg;
            float near = (RingConstants.Eye - OutsideLayout.HullTopY) * halfW / RingConstants.Eye;
            Debug.Log("[Ring] Beat07Outside hull view: the side edges (" + halfW.ToString("0.0", ci) + " m from the walk line) dip "
                      + (Mathf.Atan2(RingConstants.Eye, halfW) * Mathf.Rad2Deg).ToString("0.0", ci) + " deg; the hull shows beyond both rails "
                      + "where the edge is within " + reach.ToString("0.0", ci) + " m (azimuth >= " + az.ToString("0.0", ci)
                      + " deg off the walk), from " + near.ToString("0", ci) + " m lateral to the horizon");
        }

        // ====================================================================================================== deck
        void BuildDeck(BuildContext ctx)
        {
            float x0 = RingConstants.PlatformX0, x1 = RingConstants.PlatformX1;
            float z0 = RingConstants.PlatformZ0, z1 = RingConstants.PlatformZ1;
            float top = OutsideLayout.DeckTopY, bottom = OutsideLayout.DeckBottomY;
            float bend = OutsideLayout.DeckBendDistance;

            // slab: exactly x 234..306, z 654..666 (ADR-011), y -1.2..0 (Platform.Deck is measured by its vertices)
            var slab = new MeshBuilder();
            OutsideMeshes.DeckSlab(slab, x0, x1, bottom, top, z0, z1, OutsideLayout.DeckChamfer, OutsideLayout.DeckStrips);
            var slabMesh = slab.Build("Platform.Deck", true);
            Check(slabMesh);
            ctx.Register("Platform.Deck", ctx.AddMesh("PlatformDeck", slabMesh, RingMat.Deck, Set, true, bend));

            // cantilever beams under the slab (tops 5 cm inside it), CeramicShade, plus the kick plates
            var beams = new MeshBuilder();
            float beamTop = bottom + OutsideLayout.BeamEmbed;
            for (int k = 0; k < OutsideLayout.Beams; k++)
            {
                float zc = z0 + (z1 - z0) * (k + 0.5f) / OutsideLayout.Beams;
                int i0 = beams.I.Count;
                OutsideMeshes.TaperBeam(beams, x0, OutsideLayout.BeamTipX, zc, OutsideLayout.BeamWidth, beamTop,
                    OutsideLayout.BeamRootDepth, OutsideLayout.BeamTipDepth, OutsideLayout.BeamChamfer);
                OutsideMeshes.OrientToNormals(beams, i0);
            }
            OutsideMeshes.KickPlates(beams);
            var beamMesh = beams.Build("Platform.Beams", true);
            Check(beamMesh);
            ctx.Register("Platform.Beams", ctx.AddMesh("PlatformBeams", beamMesh, RingMat.CeramicShade, Set, true, bend));

            // hand rail (PaleGold, top of the rail at 1.0 m)
            var rail = new MeshBuilder();
            rail.Tube(OutsideMeshes.RailPath(OutsideLayout.TopRailY), OutsideLayout.RailRadius, OutsideLayout.RailSegments, true);
            var railMesh = rail.Build("Platform.Rail", true);
            Check(railMesh);
            ctx.Register("Platform.Rail", ctx.AddMesh("PlatformRail", railMesh, RingMat.PaleGold, Set, true, bend));

            // posts and mid rail (Metal)
            var posts = new MeshBuilder();
            foreach (var p in OutsideMeshes.PostPositions()) OutsideMeshes.Post(posts, p);
            posts.Tube(OutsideMeshes.RailPath(OutsideLayout.MidRailY), OutsideLayout.RailRadius, OutsideLayout.RailSegments, true);
            var postMesh = posts.Build("Platform.Posts", true);
            Check(postMesh);
            ctx.Register("Platform.Posts", ctx.AddMesh("PlatformPosts", postMesh, RingMat.Metal, Set, true, bend));

            // bollards: ceramic bodies (cast), WarmLamp bands (Emissive, lamp level), light pools on the deck
            var body = new MeshBuilder();
            var lamps = new MeshBuilder();
            var pools = new MeshBuilder();
            float zs = z0 + OutsideLayout.RailInset + OutsideLayout.BollardInset;   // 646.8
            float zn = z1 - OutsideLayout.RailInset - OutsideLayout.BollardInset;   // 673.2
            float h = OutsideLayout.BollardColliderHalf;
            int n = 0;
            foreach (float bx in OutsideLayout.BollardX)
            {
                for (int side = 0; side < 2; side++)
                {
                    float bz = side == 0 ? zs : zn;
                    var c = new Vector3(bx, top, bz);
                    OutsideMeshes.BollardBody(body, c);
                    OutsideMeshes.BollardLamp(lamps, c);
                    // pool clipped to the deck (5 cm inside its edges); v runs along -z (b = n x t = up x right)
                    float r = LightPoolKit.PoolRadius;
                    float vMin = bz - (z1 - 0.05f), vMax = bz - (z0 + 0.05f);
                    LightPoolKit.AddLightPoolRect(pools, c, Vector3.up, Vector3.right, -r, r, Mathf.Max(vMin, -r), Mathf.Min(vMax, r),
                        LightPoolKit.WarmLamp, OutsideLayout.BollardPoolIntensity, LampChannel.Lamp);
                    ctx.AddBoxCollider("Platform.Bollard" + n, new Vector3(bx, 0.5f * OutsideLayout.BollardHeight, bz),
                        new Vector3(2f * h, OutsideLayout.BollardHeight, 2f * h), Quaternion.identity);
                    n++;
                }
            }
            var bodyMesh = body.Build("Platform.Bollards", true);
            Check(bodyMesh);
            ctx.Register("Platform.Bollards", ctx.AddMesh("PlatformBollards", bodyMesh, RingMat.Ceramic, Set, true, bend));
            var lampMesh = lamps.Build("Platform.BollardLamps", true);
            Check(lampMesh);
            ctx.AddMesh("PlatformBollardLamps", lampMesh, RingMat.Emissive, Set, false, bend);
            var poolMesh = pools.Build("Platform.Pools", false);
            ctx.AddMesh("PlatformPools", poolMesh, RingMat.LightPool, Set, false, bend);
        }

        // ====================================================================================================== beacon
        void BuildBeacon(BuildContext ctx)
        {
            var c = new Vector3(RingConstants.BeaconXZ.x, OutsideLayout.DeckTopY, RingConstants.BeaconXZ.y);
            float bend = OutsideLayout.DeckBendDistance;
            var root = new GameObject("Beacon");
            root.transform.SetParent(ctx.SetRoot(Set), false);

            var lens = new MeshBuilder();
            var pylon = OutsideMeshes.Beacon(c, lens);
            var pylonMesh = pylon.Build("Beacon.Pylon", true);
            Check(pylonMesh);
            ctx.AddMesh("BeaconPylon", pylonMesh, RingMat.Ceramic, Set, true, bend, root.transform);
            var lensMesh = lens.Build("Beacon.Lens", true);
            Check(lensMesh);
            var lensGo = ctx.AddMesh("BeaconLens", lensMesh, RingMat.Beacon, Set, false, bend, root.transform);
            ctx.Register("Beacon.Lens", lensGo);

            // point light: range 6 m, 3.0 x BeaconAmber x pulse, no shadows (14.2.3 row 7)
            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(c.x, OutsideLayout.BeaconLightY, c.z);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = OutsideLayout.BeaconLightRange;
            lamp.color = RingPalette.Lin("BeaconAmber").gamma;   // Light.color is gamma; Unity linearises it (as AtmosphereDriver)
            lamp.intensity = OutsideLayout.BeaconLightIntensity * OutsideLayout.PulseBase * RingConstants.LightCal;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.Auto;
            lamp.bounceIntensity = 0f;
            ctx.Register("Beacon.Light", lightGo);

            // collider r 0.5 (capsule over the 1.3 m height); colliders live under the Always root (never toggled)
            var colGo = new GameObject("Beacon.Collider");
            colGo.transform.SetParent(ctx.SetRoot(RingSet.Always), false);
            colGo.transform.localPosition = new Vector3(c.x, 0.5f * RingConstants.BeaconH, c.z);
            var cap = colGo.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = OutsideLayout.BeaconColliderR;
            cap.height = RingConstants.BeaconH;
            cap.center = Vector3.zero;

            var pulse = root.AddComponent<BeaconPulse>();
            pulse.Lamp = lamp;
            ctx.Animated.Add(pulse);
            ctx.Drawables.Add(pulse);

            // "Beacon" = pylon + lens (children mesh vertices: y -0.005 .. 1.30, centre (301, 660))
            ctx.Register("Beacon", root);
            Marker(ctx, "Subject.Beacon", new Vector3(c.x, 0.5f * RingConstants.BeaconH, c.z),
                new Vector3(2f * OutsideLayout.BeaconBaseR, RingConstants.BeaconH, 2f * OutsideLayout.BeaconBaseR));
        }

        // ====================================================================================================== helpers
        /// <summary>Renderer-less registry marker: position = centre, localScale = size (RegistryMeasure rule 3).</summary>
        static GameObject Marker(BuildContext ctx, string key, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Marker_" + key);
            go.transform.SetParent(ctx.SetRoot(Set), false);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f), Mathf.Max(size.z, 0.01f));
            ctx.Register(key, go);
            return go;
        }

        static void NoShadows(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;   // the hull lies 200 m below, far beyond the 90 m cascades; its shadows are analytic
        }

        /// <summary>Load-time mesh audit (Part 8.7, 10 #29): one warning per failing mesh.</summary>
        static void Check(Mesh m)
        {
            if (m == null) return;
            var r = MeshValidation.Validate(m);
            float wind = MeshValidation.WindingAgreement(m);
            if (!r.ok || wind < 0.999f)
                Debug.LogWarning("[Ring] Beat07Outside mesh " + m.name + ": degenerate " + r.degenerate + ", bad index " + r.badIndex
                                 + ", NaN " + r.nanVerts + ", zero normals " + r.zeroNormals + ", winding agreement "
                                 + wind.ToString("0.000", CultureInfo.InvariantCulture));
        }
    }
}
