using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Corridor guarantee of Part 9 / 14.4 "Lanes" and "Colliders" (1)-(8): builds every invisible corridor collider
    /// at load (discovered by the WorldBuilder as an IBeatBuilder) and exposes the static lane model used by the
    /// PlayerController (safety volume), the bots (LaneHalfWidth) and the ray-cast grid test (LaneSamplePoints).
    ///
    /// Conventions: walls are 1 m thick boxes whose INNER face sits at the collider-gap half width (lane + 0.35 m),
    /// 6 m high; floors are 1 m thick boxes with their top at the walk height; every join overlaps by 1 m on the solid
    /// side (never into a walkable area). Not built here (owned by other builders): the gangway ramp MeshCollider, the
    /// bridge deck MeshCollider, the hatch leaves and jambs (x = +-1.0), the beacon collider. Under the ramp and the
    /// bridge deck this class adds thin fallback floors 3 cm below the visible deck function, so a missing deck
    /// collider can never drop the player.
    /// </summary>
    public sealed class PathGuide : IBeatBuilder
    {
        public int Order => 5;
        public string Name => "PathGuide";

        // ---------------- lane numbers (14.4 Lanes; collider gap = lane + 0.70 m) ----------------
        const float R = RingConstants.PlayerRadius;           // 0.35
        const float Thick = 1f;                                // wall / floor thickness (14.4 Colliders (1))
        const float WallTop = 6f;                              // walls 6 m high (14.4 Colliders (1))
        const float CapHalf = RingConstants.CapsuleInnerW * 0.5f;   // 1.3 (clear interior 2.6)
        const float HatchHalf = RingConstants.HatchW * 0.5f;        // 1.0 (jambs at +-1.0, 14.4 (8))
        const float GangHalf = RingConstants.RampW * 0.5f;          // 1.2 (gangway 2.4)
        const float PadHalf = RingConstants.PadHalfX;               // 3.0
        const float MeadowHalf = 5.0f;                              // 14.4: meadow colliders x = +-5.0
        const float BridgeHalf = RingConstants.BridgeDeckW * 0.5f;  // 2.5 (rails 5.0 apart)
        const float ArchHalf = RingConstants.FootbridgeW * 0.5f;    // 2.1 (opening 4.2)
        const float UnderHalf = RingConstants.UnderpassW * 0.5f;    // 1.4 (mouth 2.8)
        const float PlainHalf = 7.0f;                               // 14.4: plain = polyline buffered 7 m
        const float DoorHalf = RingConstants.DoorW * 0.5f;          // 1.3 (door = tunnel profile 2.6)
        const float RailHalf = 5.5f;                                // ADR-011: rail colliders at z 660 +- 5.5 (12 m pier, lane 10.3)
        const float PlatformFront = 305.5f;                         // front rail 0.5 m inside the deck end x 306 (same inset as the side rails)
        const float FunnelS0 = 538f, FunnelS1 = 558f;               // 14.4: last 20 m before s 558 funnel to the 2.8 m mouth
        const int FunnelSegments = 5;
        const float DoorFunnelX0 = RingConstants.LaneCapX;          // 111.5 (2.5 m from the wall)
        const float DoorFunnelX1 = RingConstants.WallX;             // funnel reaches the door half width +-1.3 at the wall face x 114
        const float DoorFunnelMouth = 2.5f;                         // funnel mouth half width at the cap
        const float ChamferDist = 7.5f;                             // outer plain corners chamfered at 7.5 m from the corner point
        const float FallbackDrop = 0.03f;                           // fallback floors 3 cm below the visible deck
        const float SwapBack = 570f;                                // 14.6: the swap reverts below s 570

        // ---------------- analytic floors ----------------
        /// <summary>Visible bridge deck function y = 1.5 (1 + cos(pi (z - 150) / 30)) for z 120..180 (14.4 P2).</summary>
        public static float BridgeY(float z)
        {
            float half = 0.5f * (RingConstants.BridgeZ1 - RingConstants.BridgeZ0);
            float x = Mathf.Clamp((z - RingConstants.BridgeCrestZ) / half, -1f, 1f);
            return 0.5f * RingConstants.BridgeCrest * (1f + Mathf.Cos(Mathf.PI * x));
        }

        /// <summary>Gangway ramp top y 0.90 -> 0.03 over z 10..24 (14.5.1).</summary>
        public static float RampY(float z)
        {
            float t = Mathf.Clamp01((z - RingConstants.RampZ0) / (RingConstants.RampZ1 - RingConstants.RampZ0));
            return RingConstants.RampY0 + (RingConstants.RampY1 - RingConstants.RampY0) * t;
        }

        /// <summary>Walk surface height at (x, z): capsule floor 0.9, ramp, pad 0.03, bridge deck, else 0.</summary>
        public static float WalkHeight(float x, float z)
        {
            float ax = Mathf.Abs(x);
            if (z >= RingConstants.BridgeZ0 && z <= RingConstants.BridgeZ1 && ax <= BridgeHalf + 1f) return BridgeY(z);
            if (z > RingConstants.CapsuleZ0 - 1f && z < RingConstants.RampZ0 && ax < CapHalf + 1f) return RingConstants.CapsuleFloorY;
            if (z >= RingConstants.RampZ0 && z < RingConstants.RampZ1 && ax <= GangHalf + 0.1f) return RampY(z);
            if (z >= RingConstants.PadZ0 && z < RingConstants.PadZ1 && ax <= PadHalf + 0.05f) return RingConstants.PadTopY;
            return 0f;
        }

        /// <summary>C0-continuous version of <see cref="WalkHeight"/> used as the eye feed-forward (the 3 cm pad edge is
        /// spread over 0.6 m so the eye smoothing absorbs it).</summary>
        public static float SmoothFloor(float x, float z)
        {
            float ax = Mathf.Abs(x);
            if (z >= RingConstants.BridgeZ0 && z <= RingConstants.BridgeZ1 && ax <= BridgeHalf + 1f) return BridgeY(z);
            if (z < RingConstants.RampZ0 && ax < CapHalf + 1f && z > RingConstants.CapsuleZ0 - 1f) return RingConstants.CapsuleFloorY;
            if (z >= RingConstants.RampZ0 && z < RingConstants.RampZ1 && ax <= GangHalf + 0.1f) return RampY(z);
            if (z >= RingConstants.PadZ0 && z < RingConstants.PadZ1 + 0.6f && ax <= PadHalf + 0.05f)
                return RingConstants.PadTopY * (1f - Mathf.Clamp01((z - RingConstants.PadZ1) / 0.6f));
            return 0f;
        }

        // ---------------- lane model (player-centre reachable region) ----------------
        struct Shape
        {
            public bool AlongZ;     // true: lateral = x - C, along = z ; false: lateral = z - C, along = x
            public float C, A0, A1, H0, H1;
            public int Profile;     // 0 = linear H0 -> H1, 1 = smoothstep funnel
        }

        static readonly Shape[] Shapes = BuildShapes();

        static float FunnelWall(float t) => MeadowHalf + (UnderHalf - MeadowHalf) * RingMath.Smoothstep(0f, 1f, t);

        static Shape[] BuildShapes()
        {
            const float slant = 0.41f; // perpendicular player offset measured along the lateral axis on the door funnel
            return new[]
            {
                Z(0f, RingConstants.CapsuleZ0 + R, RingConstants.HatchZ0, CapHalf - R),                 // capsule interior
                Z(0f, RingConstants.HatchZ0, RingConstants.HatchZ1, HatchHalf - R),                     // hatch ring
                Z(0f, RingConstants.RampZ0, RingConstants.RampZ1, GangHalf - R),                        // gangway
                Z(0f, RingConstants.PadZ0, RingConstants.PadZ1, PadHalf - R),                           // landing pad
                Z(0f, RingConstants.PadZ1, RingConstants.BridgeZ0, MeadowHalf - R),                     // meadow 1
                Z(0f, RingConstants.BridgeZ0, RingConstants.BridgeZ1, BridgeHalf - R),                  // bridge
                Z(0f, RingConstants.BridgeZ1, RingConstants.FootbridgeS0, MeadowHalf - R),              // meadow 2
                Z(0f, RingConstants.FootbridgeS0, RingConstants.FootbridgeS1, ArchHalf - R),            // footbridge arch
                Z(0f, RingConstants.FootbridgeS1, FunnelS0, MeadowHalf - R),                            // meadow 3
                new Shape { AlongZ = true, C = 0f, A0 = FunnelS0, A1 = FunnelS1, H0 = MeadowHalf - 0.36f, H1 = UnderHalf - 0.36f, Profile = 1 },
                Z(0f, FunnelS1, RingConstants.SwapS + UnderHalf - R, UnderHalf - R),                    // underpass leg 1
                X(576f, -UnderHalf + R, 18f, UnderHalf - R),                                            // underpass leg 2
                X(576f, 18f, 100f + PlainHalf - R, PlainHalf - R),                                      // plain, leg (18,576)-(100,576)
                Z(100f, 576f - PlainHalf + R, 660f + PlainHalf - R, PlainHalf - R),                     // plain, leg (100,576)-(100,660)
                X(660f, 100f - PlainHalf + R, DoorFunnelX0 - R, PlainHalf - R),                         // plain, leg (100,660)-(111.5,660)
                new Shape { AlongZ = false, C = 660f, A0 = DoorFunnelX0 - R, A1 = DoorFunnelX1, H0 = DoorFunnelMouth - slant, H1 = DoorHalf - slant, Profile = 0 },
                X(660f, DoorFunnelX1, RingConstants.WallOuterX, DoorHalf - R),                          // door and tunnel
                X(660f, RingConstants.WallOuterX, PlatformFront - R, RailHalf - R),                     // platform
            };
        }

        static Shape Z(float c, float a0, float a1, float h) => new Shape { AlongZ = true, C = c, A0 = a0, A1 = a1, H0 = h, H1 = h };
        static Shape X(float c, float a0, float a1, float h) => new Shape { AlongZ = false, C = c, A0 = a0, A1 = a1, H0 = h, H1 = h };

        static float HalfAt(in Shape sh, float a)
        {
            float t = Mathf.Clamp01((a - sh.A0) / Mathf.Max(1e-6f, sh.A1 - sh.A0));
            if (sh.Profile == 1) t = RingMath.Smoothstep(0f, 1f, t);
            return Mathf.Max(0f, sh.H0 + (sh.H1 - sh.H0) * t);
        }

        /// <summary>Distance (m, xz) from p to the union of the reachable lane regions (0 inside). Allocation-free.</summary>
        public static float DistanceOutsideLanes(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Shapes.Length; i++)
            {
                var sh = Shapes[i];
                float a = sh.AlongZ ? p.z : p.x;
                float lat = sh.AlongZ ? p.x - sh.C : p.z - sh.C;
                float da = Mathf.Max(sh.A0 - a, Mathf.Max(a - sh.A1, 0f));
                float dl = Mathf.Max(Mathf.Abs(lat) - HalfAt(sh, Mathf.Clamp(a, sh.A0, sh.A1)), 0f);
                float d = Mathf.Sqrt(da * da + dl * dl);
                if (d < best) best = d;
                if (best <= 0f) return 0f;
            }
            return best;
        }

        /// <summary>Half width of the reachable lane (player centre; = collider gap / 2 - 0.35 m) at progress s.</summary>
        public static float LaneHalfWidth(float s)
        {
            if (s < RingConstants.HatchZ0) return CapHalf - R;
            if (s < RingConstants.HatchZ1) return HatchHalf - R;
            if (s < RingConstants.RampZ1) return GangHalf - R;
            if (s < RingConstants.PadZ1) return PadHalf - R;
            if (s < RingConstants.BridgeZ0) return MeadowHalf - R;
            if (s < RingConstants.BridgeZ1) return BridgeHalf - R;
            if (s < RingConstants.FootbridgeS0) return MeadowHalf - R;
            if (s < RingConstants.FootbridgeS1) return ArchHalf - R;
            if (s < FunnelS0) return MeadowHalf - R;
            if (s < FunnelS1) return FunnelWall((s - FunnelS0) / (FunnelS1 - FunnelS0)) - 0.36f;
            if (s < RingConstants.BeatS[4]) return UnderHalf - R;                       // underpass to s 594
            float capS = 760f + (DoorFunnelX0 - 100f) - R;                              // s 771.15
            if (s < capS) return PlainHalf - R;
            float funnelEndS = 760f + (DoorFunnelX1 - 100f);                            // s 774 (the door)
            if (s < funnelEndS) return Mathf.Lerp(DoorFunnelMouth - 0.41f, DoorHalf - 0.41f, (s - capS) / (funnelEndS - capS));
            if (s < RingConstants.BeatS[6]) return DoorHalf - R;                        // tunnel to s 894
            return RailHalf - R;
        }

        /// <summary>Grid of points (0.5 m by default) over every lane, at the walk-surface height, for the ray-cast
        /// coverage test (14.4 Colliders (7)). Points of overlapping lanes may repeat; the two chamfered outer corners
        /// of the plain are excluded (they lie behind the chamfer walls).</summary>
        public static IEnumerable<Vector3> LaneSamplePoints(float step = 0.5f)
        {
            step = Mathf.Max(0.05f, step);
            for (int i = 0; i < Shapes.Length; i++)
            {
                var sh = Shapes[i];
                int na = Mathf.Max(1, Mathf.CeilToInt((sh.A1 - sh.A0) / step));
                for (int ia = 0; ia <= na; ia++)
                {
                    float a = Mathf.Lerp(sh.A0, sh.A1, ia / (float)na);
                    float h = HalfAt(sh, a);
                    int nl = Mathf.Max(1, Mathf.CeilToInt(2f * h / step));
                    for (int il = 0; il <= nl; il++)
                    {
                        float lat = Mathf.Lerp(-h, h, il / (float)nl);
                        float x = sh.AlongZ ? sh.C + lat : a;
                        float z = sh.AlongZ ? a : sh.C + lat;
                        if (BeyondChamfer(x, z)) continue;
                        yield return new Vector3(x, WalkHeight(x, z), z);
                    }
                }
            }
        }

        // outer plain corners (corner point, outward bisector) chamfered at ChamferDist (see BuildUnderpassAndPlain)
        static readonly Vector2 ChamferCornerB = new Vector2(100f, 576f), ChamferDirB = new Vector2(0.70710678f, -0.70710678f);
        static readonly Vector2 ChamferCornerC = new Vector2(100f, 660f), ChamferDirC = new Vector2(-0.70710678f, 0.70710678f);

        /// <summary>True for player-centre positions behind one of the two plain chamfer walls (unreachable).</summary>
        public static bool BeyondChamfer(float x, float z)
        {
            var p = new Vector2(x, z);
            float lim = ChamferDist - R;
            return Vector2.Dot(p - ChamferCornerB, ChamferDirB) > lim || Vector2.Dot(p - ChamferCornerC, ChamferDirC) > lim;
        }

        /// <summary>Lane centre point (feet) at progress s, on the walk surface.</summary>
        public static Vector3 CentreAt(float s)
        {
            Vector2 p = RingWorld.PointAt(Mathf.Clamp(s, 0f, RingConstants.PathLength));
            return new Vector3(p.x, WalkHeight(p.x, p.y), p.y);
        }

        // ---------------- collider build ----------------
        Transform _root;
        BuildContext _ctx;
        int _count;

        public IEnumerator Build(BuildContext ctx)
        {
            _ctx = ctx;
            var go = new GameObject("Corridor");
            go.transform.SetParent(ctx.SetRoot(RingSet.Always), false);
            _root = go.transform;
            ctx.Register("Corridor.Root", go);

            BuildCapsuleAndGangway();
            BuildValley();
            yield return null;
            BuildUnderpassAndPlain();
            BuildTunnelAndPlatform();
            Debug.Log("[Ring] PathGuide: " + _count + " corridor colliders built");
        }

        GameObject Box(string name, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            _count++;
            return _ctx.AddBoxCollider(name,
                new Vector3(0.5f * (x0 + x1), 0.5f * (y0 + y1), 0.5f * (z0 + z1)),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)),
                Quaternion.identity, _root);
        }

        /// <summary>Mirrored pair of boxes about x = 0 (left first). x0 &lt; x1 are the right-side extents.</summary>
        void PairX(string name, float x0, float x1, float y0, float y1, float z0, float z1, string regL = null, string regR = null)
        {
            var l = Box(name + "L", -x1, -x0, y0, y1, z0, z1);
            var r = Box(name + "R", x0, x1, y0, y1, z0, z1);
            if (regL != null) _ctx.Register(regL, l);
            if (regR != null) _ctx.Register(regR, r);
        }

        /// <summary>A 1 m thick wall whose inner face runs from p0 to p1 (xz), on the side of outwardHint, extended by ext at both ends.</summary>
        void SlantWall(string name, Vector2 p0, Vector2 p1, Vector2 outwardHint, float y0, float y1, float ext)
        {
            Vector2 d = p1 - p0;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            d /= len;
            Vector2 n = new Vector2(d.y, -d.x);
            if (Vector2.Dot(n, outwardHint) < 0f) n = -n;
            Vector2 c = 0.5f * (p0 + p1) + n * (0.5f * Thick);
            var rot = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up);
            _count++;
            _ctx.AddBoxCollider(name, new Vector3(c.x, 0.5f * (y0 + y1), c.y), new Vector3(Thick, y1 - y0, len + 2f * ext), rot, _root);
        }

        /// <summary>Thin sloped fallback floor under a deck line (z0, y0) -> (z1, y1), top FallbackDrop below the line.</summary>
        void SlopeFloor(string name, float halfX, float z0, float y0, float z1, float y1)
        {
            var dir = new Vector3(0f, y1 - y0, z1 - z0);
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            Vector3 up = rot * Vector3.up;
            const float th = 0.3f;
            Vector3 mid = new Vector3(0f, 0.5f * (y0 + y1) - FallbackDrop, 0.5f * (z0 + z1));
            _count++;
            _ctx.AddBoxCollider(name, mid - up * (0.5f * th), new Vector3(2f * halfX, th, len + 0.02f), rot, _root);
        }

        void BuildCapsuleAndGangway()
        {
            float fy = RingConstants.CapsuleFloorY, cy = RingConstants.CapsuleCeilY;
            float z0 = RingConstants.CapsuleZ0, z1 = RingConstants.CapsuleZ1, hz1 = RingConstants.HatchZ1;
            float outer = CapHalf + Thick;
            // floor top y 0.9 for z -1.5..9.5, extended under the rear wall and through the hatch ring to z 10
            _ctx.Register("Corridor.Capsule.Floor", Box("Capsule.Floor", -outer, outer, fy - Thick, fy, z0 - Thick, hz1));
            PairX("Capsule.Wall", CapHalf, outer, fy - Thick, cy + Thick, z0 - Thick, z1, "Corridor.Capsule.WallL", "Corridor.Capsule.WallR");
            _ctx.Register("Corridor.Capsule.Rear", Box("Capsule.Rear", -outer, outer, fy - Thick, cy + Thick, z0 - Thick, z0));
            _ctx.Register("Corridor.Capsule.Ceiling", Box("Capsule.Ceiling", -outer, outer, cy, cy + Thick, z0 - Thick, z1));
            // hatch ring z 9.5..10: solid outside the 2.0 m opening (the jambs at +-1.0 belong to the capsule builder;
            // these blocks overlap them and close the corner between the capsule and gangway walls)
            PairX("Hatch.Side", HatchHalf, outer, fy - Thick, cy + Thick, z1, hz1);

            // gangway z 10..24: walls at +-1.2, the ramp MeshCollider is the capsule builder's; fallback floor 3 cm below it
            PairX("Gangway.Wall", GangHalf, GangHalf + Thick, -Thick, fy + WallTop, z1, RingConstants.RampZ1, "Corridor.Gangway.WallL", "Corridor.Gangway.WallR");
            SlopeFloor("Gangway.FallbackFloor", GangHalf, RingConstants.RampZ0, RingConstants.RampY0, RingConstants.RampZ1, RingConstants.RampY1);

            // landing pad z 24..30 (top 0.03, walls +-3); cross walls where the lane widens (solid side of the narrow part)
            float pz0 = RingConstants.PadZ0, pz1 = RingConstants.PadZ1, pt = RingConstants.PadTopY;
            Box("Pad.Floor", -(PadHalf + Thick), PadHalf + Thick, pt - Thick, pt, pz0, pz1);
            PairX("Pad.Back", GangHalf, PadHalf + Thick, -Thick, WallTop, pz0 - Thick, pz0);
            PairX("Pad.Wall", PadHalf, PadHalf + Thick, -Thick, WallTop, pz0 - Thick, pz1);
            PairX("Pad.Front", PadHalf, MeadowHalf + Thick, -Thick, WallTop, pz1 - Thick, pz1);
        }

        void BuildValley()
        {
            float mo = MeadowHalf + Thick;
            float bz0 = RingConstants.BridgeZ0, bz1 = RingConstants.BridgeZ1;
            float a0 = RingConstants.FootbridgeS0, a1 = RingConstants.FootbridgeS1;

            // meadow 1 z 30..120
            Box("Meadow1.Floor", -mo, mo, -Thick, 0f, RingConstants.PadZ1, bz0);
            PairX("Meadow1.Wall", MeadowHalf, mo, -Thick, WallTop, RingConstants.PadZ1 - Thick, bz0 + Thick);

            // bridge z 120..180: walls (rails) at +-2.5; the deck MeshCollider is the valley builder's
            PairX("Bridge.EndSouth", BridgeHalf, mo, -Thick, WallTop, bz0, bz0 + Thick);
            PairX("Bridge.Wall", BridgeHalf, BridgeHalf + Thick, -Thick, RingConstants.BridgeCrest + WallTop, bz0, bz1, "Corridor.Bridge.WallL", "Corridor.Bridge.WallR");
            PairX("Bridge.EndNorth", BridgeHalf, mo, -Thick, WallTop, bz1 - Thick, bz1);
            // fallback deck: 1 m chords of the cos profile (max chord sag 2 mm < 30 mm drop)
            for (float z = bz0; z < bz1 - 1e-3f; z += 1f)
                SlopeFloor("Bridge.FallbackDeck", BridgeHalf, z, BridgeY(z), z + 1f, BridgeY(z + 1f));

            // meadow 2 z 180..396
            Box("Meadow2.Floor", -mo, mo, -Thick, 0f, bz1, a0);
            PairX("Meadow2.Wall", MeadowHalf, mo, -Thick, WallTop, bz1 - Thick, a0 + Thick);

            // footbridge arch s 396..404: walls +-2.1, soffit y 3.2, mound faces at z 396 / 404
            Box("Arch.Floor", -mo, mo, -Thick, 0f, a0, a1);
            PairX("Arch.South", ArchHalf, mo, -Thick, WallTop, a0, a0 + Thick);
            PairX("Arch.Wall", ArchHalf, ArchHalf + Thick, -Thick, WallTop, a0, a1, "Corridor.Arch.WallL", "Corridor.Arch.WallR");
            PairX("Arch.North", ArchHalf, mo, -Thick, WallTop, a1 - Thick, a1);
            _ctx.Register("Corridor.Arch.Soffit", Box("Arch.Soffit", -(ArchHalf + Thick), ArchHalf + Thick, RingConstants.FootbridgeH, RingConstants.FootbridgeH + Thick, a0, a1));

            // meadow 3 z 404..538 and the funnel 538..558 (5.0 -> 1.4, smoothstep, several segments)
            Box("Meadow3.Floor", -mo, mo, -Thick, 0f, a1, FunnelS1);
            PairX("Meadow3.Wall", MeadowHalf, mo, -Thick, WallTop, a1 - Thick, FunnelS0);
            for (int k = 0; k < FunnelSegments; k++)
            {
                float t0 = k / (float)FunnelSegments, t1 = (k + 1) / (float)FunnelSegments;
                float za = Mathf.Lerp(FunnelS0, FunnelS1, t0), zb = Mathf.Lerp(FunnelS0, FunnelS1, t1);
                float wa = FunnelWall(t0), wb = FunnelWall(t1);
                SlantWall("Funnel.WallR" + k, new Vector2(wa, za), new Vector2(wb, zb), Vector2.right, -Thick, WallTop, 0.15f);
                SlantWall("Funnel.WallL" + k, new Vector2(-wa, za), new Vector2(-wb, zb), Vector2.left, -Thick, WallTop, 0.15f);
            }
        }

        void BuildUnderpassAndPlain()
        {
            float zc = RingConstants.SwapS;                 // corner (0, 576)
            float uo = UnderHalf + Thick;                   // 2.4
            float ceil = RingConstants.UnderpassH;          // 2.9
            const float exitX = 18f;                        // underpass exit (18, 576) = levee east face
            // leg 1 (+Z) z 558..576: right turn -> inner side +X
            Box("Under1.Floor", -uo, uo, -Thick, 0f, FunnelS1, zc + uo);
            Box("Under1.WallOuter", -uo, -UnderHalf, -Thick, WallTop, FunnelS1, zc + uo);
            Box("Under1.WallInner", UnderHalf, uo, -Thick, WallTop, FunnelS1, zc - UnderHalf);
            _ctx.Register("Corridor.Underpass.Ceiling", Box("Under1.Ceiling", -uo, uo, ceil, ceil + Thick, FunnelS1, zc + uo));
            // leg 2 (+X) x 0..18: inner side -Z
            Box("Under2.Floor", -uo, exitX, -Thick, 0f, zc - uo, zc + uo);
            Box("Under2.WallInner", UnderHalf, exitX, -Thick, WallTop, zc - uo, zc - UnderHalf);
            Box("Under2.WallOuter", -uo, exitX, -Thick, WallTop, zc + UnderHalf, zc + uo);
            Box("Under2.Ceiling", -uo, exitX, ceil, ceil + Thick, zc - uo, zc + uo);
            // exit into the plain: cross walls on the solid side (levee face x 17..18)
            float po = PlainHalf + Thick;
            Box("Under2.ExitS", exitX - Thick, exitX, -Thick, WallTop, zc - po, zc - UnderHalf);
            Box("Under2.ExitN", exitX - Thick, exitX, -Thick, WallTop, zc + UnderHalf, zc + po);

            // plain: polyline (18,576)-(100,576)-(100,660)-(114,660) buffered 7 m, right side capped at x <= 111.5
            const float bx = 100f, cz = 660f;
            float cap = DoorFunnelX0;
            Box("Plain.Floor", exitX - Thick, cap + Thick, -Thick, 0f, zc - po, cz + po);
            // leg B (+X along z 576), left turn at (100, 576): inner side +Z
            Box("PlainB.WallInner", exitX - Thick, bx - PlainHalf, -Thick, WallTop, zc + PlainHalf, zc + po);
            Box("PlainB.WallOuter", exitX - Thick, bx + po, -Thick, WallTop, zc - po, zc - PlainHalf);
            // leg C (+Z along x 100), right turn at (100, 660): inner side +X
            Box("PlainC.WallLeft", bx - po, bx - PlainHalf, -Thick, WallTop, zc + PlainHalf, cz + po);
            Box("PlainC.WallRight", bx + PlainHalf, bx + po, -Thick, WallTop, zc - po, cz - PlainHalf);
            // leg D (+X along z 660) up to the cap
            Box("PlainD.WallLeft", bx - po, cap + Thick, -Thick, WallTop, cz + PlainHalf, cz + po);
            Box("PlainD.WallRight", bx + PlainHalf, cap + Thick, -Thick, WallTop, cz - po, cz - PlainHalf);
            // outer corners chamfered at 7.5 m from the corner point (keeps the walkable area inside the flattened terrain)
            Chamfer("PlainB.Chamfer", new Vector2(bx, zc), new Vector2(1f, -1f));
            Chamfer("PlainC.Chamfer", new Vector2(bx, cz), new Vector2(-1f, 1f));
            // lane cap x 111.5 (2.5 m from the wall) except the door funnel mouth
            Box("Plain.CapS", cap, cap + Thick, -Thick, WallTop, cz - po, cz - DoorFunnelMouth);
            Box("Plain.CapN", cap, cap + Thick, -Thick, WallTop, cz + DoorFunnelMouth, cz + po);
            // door funnel: +-2.5 at the cap to the door half width +-1.3 at the wall face x 114
            SlantWall("Door.FunnelN", new Vector2(cap, cz + DoorFunnelMouth), new Vector2(DoorFunnelX1, cz + DoorHalf), Vector2.up, -Thick, WallTop, 0.15f);
            SlantWall("Door.FunnelS", new Vector2(cap, cz - DoorFunnelMouth), new Vector2(DoorFunnelX1, cz - DoorHalf), Vector2.down, -Thick, WallTop, 0.15f);
        }

        void Chamfer(string name, Vector2 corner, Vector2 bisector)
        {
            Vector2 b = bisector.normalized;
            // points on the two outer wall faces at distance ChamferDist from the corner along the bisector
            float a = ChamferDist * Mathf.Sqrt(2f) - PlainHalf;
            Vector2 sx = new Vector2(Mathf.Sign(b.x), 0f), sz = new Vector2(0f, Mathf.Sign(b.y));
            Vector2 p0 = corner + sz * PlainHalf + sx * a;
            Vector2 p1 = corner + sx * PlainHalf + sz * a;
            SlantWall(name, p0, p1, b, -Thick, WallTop, 0.5f);
        }

        void BuildTunnelAndPlatform()
        {
            const float cz = 660f;
            float x0 = DoorFunnelX1 - 0.1f, x1 = RingConstants.WallOuterX;   // 113.9 .. 234 (the funnel walls end at x 114)
            float to = DoorHalf + Thick;
            Box("Tunnel.Floor", DoorFunnelX0 - 0.5f, x1, -Thick, 0f, cz - to, cz + to);
            _ctx.Register("Corridor.Tunnel.WallL", Box("Tunnel.WallN", x0, x1, -Thick, RingConstants.DoorH + Thick, cz + DoorHalf, cz + to));
            _ctx.Register("Corridor.Tunnel.WallR", Box("Tunnel.WallS", x0, x1, -Thick, RingConstants.DoorH + Thick, cz - to, cz - DoorHalf));
            _ctx.Register("Corridor.Tunnel.Ceiling", Box("Tunnel.Ceiling", x0, x1, RingConstants.DoorH, RingConstants.DoorH + Thick, cz - to, cz + to));

            // platform x 234..306, floor top 0, rails z 660 +- 14.5, front at x 305.5
            float ro = RailHalf + Thick;
            Box("Platform.Floor", x1 - Thick, PlatformFront + Thick, -Thick, 0f, cz - ro, cz + ro);
            Box("Platform.BackN", x1 - Thick, x1, -Thick, WallTop, cz + DoorHalf, cz + ro);
            Box("Platform.BackS", x1 - Thick, x1, -Thick, WallTop, cz - ro, cz - DoorHalf);
            _ctx.Register("Corridor.Platform.RailL", Box("Platform.RailN", x1 - Thick, PlatformFront + Thick, -Thick, WallTop, cz + RailHalf, cz + ro));
            _ctx.Register("Corridor.Platform.RailR", Box("Platform.RailS", x1 - Thick, PlatformFront + Thick, -Thick, WallTop, cz - ro, cz - RailHalf));
            _ctx.Register("Corridor.Platform.Front", Box("Platform.Front", PlatformFront, PlatformFront + Thick, -Thick, WallTop, cz - ro, cz + ro));
        }

        /// <summary>The s below which the NearValley / NearPlain swap reverts (14.6).</summary>
        public static float SwapRevertS => SwapBack;
    }
}
