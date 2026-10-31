using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The path polyline W0..W18 of 14.4 as an analytic curve parametrised by path distance d (metres of unfilleted horizontal
    /// length, PolylineLength = 802.54). Straight pieces between waypoints (the gallery stairs W1-W2 are a straight ramp in y),
    /// one circular arc W6-W8 around C1 (radius 25, alpha 0..pi, y falling 4 m linearly) and the lift W12-W13, which has zero
    /// length in d: at d = D_LIFT the floor point is W12 before the ride and W13 after it (<c>afterRide</c> overloads).
    ///
    /// The walked curve (<see cref="WalkPoint(float)"/>) is the same polyline with one 3 m fillet at W6, the only free-path corner.
    /// The fillet is tangent to the incoming straight W5-W6 (heading +Z) and to the ledge arc. Because the arc curves back toward
    /// C1, the fillet and the arc form an S-bend: the fillet circle is externally tangent to the arc circle (|F - C1| = 25 + 3),
    /// it starts 2.84 m before W6 and joins the arc at alpha 0.107 rad, turning 83.85 degrees. The fillet is parametrised
    /// linearly in d, so <see cref="WalkS"/> converts d to walked metres and <see cref="WalkDAtS"/> back.
    ///
    /// Everything is static, deterministic and allocation-free; the tables are built once in the static constructor.
    /// </summary>
    public static class PathGuide
    {
        // ---------------------------------------------------------------- beat-specific widths not in AbyssConst (14.4)
        /// <summary>Dock apron at the top lift dock: x -142..-152, z 259-279 (14.5.5).</summary>
        public const float DOCK_APRON_W = 20f;
        /// <summary>Balcony edge walls stand 0.6 m inside the visible edge (14.4): walkable 18.8 x 12.8.</summary>
        public const float BALCONY_EDGE_INSET = 0.6f;
        /// <summary>Pier edge walls stand 0.5 m inside the visible edge (14.4): corridor 2.0.</summary>
        public const float PIER_EDGE_INSET = 0.5f;
        /// <summary>Search window (metres of d either side of the hint) of <see cref="Project"/>.</summary>
        public const float PROJECT_WINDOW = 40f;

        enum PieceKind : byte { Line = 0, Arc = 1 }

        struct Piece
        {
            public float D0, D1;
            public PieceKind Kind;
            public Vector3 A, B;      // floor points at D0 and D1
            public bool AfterRide;    // piece lies after the lift (d >= D_LIFT, lower level)
            public float LenH;        // horizontal geometric length
        }

        static readonly Piece[] Pieces;

        /// <summary>Geometric horizontal length of the polyline (arc as a true circle), 802.54.</summary>
        public static readonly float PolylineLength;

        /// <summary>Geometric horizontal length of the walked curve (polyline with the W6 fillet).</summary>
        public static readonly float WalkLength;

        /// <summary>Geometric length of the ledge arc W6-W8 (25 pi).</summary>
        public static readonly float ArcLength;

        /// <summary>d range of the fillet at W6: it starts on the straight W5-W6 and ends on the arc.</summary>
        public static readonly float FilletD0, FilletD1;

        /// <summary>Fillet turn angle in radians and its walked length (FILLET_R * turn).</summary>
        public static readonly float FilletTurn, FilletLength;

        static readonly Vector2 FilletCentre;     // (x, z)
        static readonly float FilletY0, FilletY1;
        static readonly float ArcD0, ArcD1;

        /// <summary>Last d of the path (POLYLINE_LENGTH in the d parametrisation).</summary>
        public static float EndD => AbyssConst.WD[AbyssConst.WD.Length - 1];

        static PathGuide()
        {
            var W = AbyssConst.W;
            var WD = AbyssConst.WD;
            ArcD0 = WD[6];
            ArcD1 = WD[8];

            // pieces: 0-1, 1-2, 2-3, 3-4, 4-5, 5-6, arc 6-8, 8-9, 9-10, 10-11, 11-12 | lift 12-13 | 13-14 ... 17-18
            Pieces = new Piece[16];
            int n = 0;
            for (int i = 0; i < 6; i++) Pieces[n++] = MakeLine(i, i + 1, false);
            Pieces[n++] = new Piece
            {
                D0 = WD[6], D1 = WD[8], Kind = PieceKind.Arc, A = W[6], B = W[8], AfterRide = false,
                LenH = AbyssConst.ARC_RADIUS * Mathf.PI
            };
            for (int i = 8; i < 12; i++) Pieces[n++] = MakeLine(i, i + 1, false);
            for (int i = 13; i < 18; i++) Pieces[n++] = MakeLine(i, i + 1, true);

            float len = 0f;
            for (int i = 0; i < Pieces.Length; i++) len += Pieces[i].LenH;
            PolylineLength = len;
            ArcLength = AbyssConst.ARC_RADIUS * Mathf.PI;

            // ---- fillet at W6 (S-bend: fillet circle externally tangent to the C1 arc circle)
            double r = AbyssConst.FILLET_R, R = AbyssConst.ARC_RADIUS;
            double cx = AbyssConst.ARC_CENTRE.x, cz = AbyssConst.ARC_CENTRE.y;
            double fx = W[6].x + r;                                  // right of the +Z heading
            double dzF = System.Math.Sqrt((R + r) * (R + r) - (fx - cx) * (fx - cx));
            double fz = cz - dzF;
            double tx = cx + R * (fx - cx) / (R + r), tz = cz + R * (fz - cz) / (R + r);
            double alphaT = System.Math.Atan2(tx - cx, cz - tz);
            FilletCentre = new Vector2((float)fx, (float)fz);
            FilletD0 = (float)(WD[6] - (W[6].z - fz));
            FilletD1 = (float)(ArcD0 + alphaT * (ArcD1 - ArcD0) / System.Math.PI);
            FilletTurn = (float)(System.Math.PI * 0.5 - alphaT);
            FilletLength = (float)(r * FilletTurn);
            FilletY0 = W[5].y + (W[6].y - W[5].y) * Mathf.InverseLerp(WD[5], WD[6], FilletD0);
            FilletY1 = (float)(W[6].y + (W[8].y - W[6].y) * alphaT / System.Math.PI);

            WalkLength = PolylineLength - ((float)(W[6].z - fz) + (float)(R * alphaT)) + FilletLength;
        }

        static Piece MakeLine(int a, int b, bool after)
        {
            Vector3 A = AbyssConst.W[a], B = AbyssConst.W[b];
            return new Piece
            {
                D0 = AbyssConst.WD[a], D1 = AbyssConst.WD[b], Kind = PieceKind.Line, A = A, B = B, AfterRide = after,
                LenH = new Vector2(B.x - A.x, B.z - A.z).magnitude
            };
        }

        // ---------------------------------------------------------------- piece lookup

        static int FindPiece(float d, bool afterRide)
        {
            // only d == D_LIFT is ambiguous; d below it is always before the ride, above it always after
            bool after = d > AbyssConst.D_LIFT || (afterRide && d >= AbyssConst.D_LIFT);
            int last = -1;
            for (int i = 0; i < Pieces.Length; i++)
            {
                if (Pieces[i].AfterRide != after) continue;
                last = i;
                if (d <= Pieces[i].D1) return i;
            }
            return last;
        }

        static float ClampD(float d) => d < 0f ? 0f : (d > EndD ? EndD : d);

        static Vector3 PiecePoint(in Piece p, float d)
        {
            float t = p.D1 > p.D0 ? Mathf.Clamp01((d - p.D0) / (p.D1 - p.D0)) : 0f;
            if (p.Kind == PieceKind.Line) return Vector3.LerpUnclamped(p.A, p.B, t);
            float a = Mathf.PI * t;
            var c = AbyssConst.ARC_CENTRE;
            float R = AbyssConst.ARC_RADIUS;
            return new Vector3(c.x + R * Mathf.Sin(a), p.A.y + (p.B.y - p.A.y) * t, c.y - R * Mathf.Cos(a));
        }

        static Vector3 PieceTangent(in Piece p, float d)
        {
            if (p.Kind == PieceKind.Line)
            {
                var h = new Vector3(p.B.x - p.A.x, 0f, p.B.z - p.A.z);
                float m = h.magnitude;
                return m > 1e-6f ? h / m : Vector3.forward;
            }
            float t = p.D1 > p.D0 ? Mathf.Clamp01((d - p.D0) / (p.D1 - p.D0)) : 0f;
            float a = Mathf.PI * t;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // ---------------------------------------------------------------- public API (CONTRACTS section 11)

        /// <summary>Floor point on the polyline (unfilleted) at d. At d = D_LIFT this is W12 (before the ride).</summary>
        public static Vector3 FloorPoint(float d) => FloorPoint(d, false);

        /// <summary>Floor point on the polyline at d; at d = D_LIFT, afterRide selects W13 (bottom dock) instead of W12.</summary>
        public static Vector3 FloorPoint(float d, bool afterRide)
        {
            d = ClampD(d);
            int i = FindPiece(d, afterRide);
            return PiecePoint(Pieces[i], d);
        }

        /// <summary>Point on the walked curve (polyline with the 3 m fillet at W6). At d = D_LIFT this is W12.</summary>
        public static Vector3 WalkPoint(float d) => WalkPoint(d, false);

        /// <summary>Point on the walked curve; at d = D_LIFT, afterRide selects W13.</summary>
        public static Vector3 WalkPoint(float d, bool afterRide)
        {
            d = ClampD(d);
            if (d > FilletD0 && d < FilletD1)
            {
                float u = (d - FilletD0) / (FilletD1 - FilletD0);
                float phi = u * FilletTurn;
                float r = AbyssConst.FILLET_R;
                return new Vector3(FilletCentre.x - r * Mathf.Cos(phi), Mathf.Lerp(FilletY0, FilletY1, u), FilletCentre.y + r * Mathf.Sin(phi));
            }
            return FloorPoint(d, afterRide);
        }

        /// <summary>Horizontal unit tangent of the polyline at d (direction of travel).</summary>
        public static Vector3 Tangent(float d) => Tangent(d, false);

        public static Vector3 Tangent(float d, bool afterRide)
        {
            d = ClampD(d);
            int i = FindPiece(d, afterRide);
            return PieceTangent(Pieces[i], d);
        }

        /// <summary>Horizontal unit tangent of the walked curve (continuous through the fillet).</summary>
        public static Vector3 WalkTangent(float d, bool afterRide = false)
        {
            d = ClampD(d);
            if (d > FilletD0 && d < FilletD1)
            {
                float phi = (d - FilletD0) / (FilletD1 - FilletD0) * FilletTurn;
                return new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
            }
            return Tangent(d, afterRide);
        }

        /// <summary>Heading of the walked curve at d as a yaw in degrees (from +Z toward +X), in [0, 360).</summary>
        public static float HeadingYaw(float d, bool afterRide = false)
        {
            var t = WalkTangent(d, afterRide);
            float y = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
            return y < 0f ? y + 360f : y;
        }

        /// <summary>Walked metres from the start to d (geometric length of the walked curve, horizontal).</summary>
        public static float WalkS(float d)
        {
            d = ClampD(d);
            if (d <= FilletD0) return d;
            if (d >= FilletD1) return d - (FilletD1 - FilletD0) + FilletLength;
            return FilletD0 + (d - FilletD0) / (FilletD1 - FilletD0) * FilletLength;
        }

        /// <summary>Inverse of <see cref="WalkS"/>.</summary>
        public static float WalkDAtS(float s)
        {
            if (s <= FilletD0) return Mathf.Max(0f, s);
            float s1 = FilletD0 + FilletLength;
            if (s >= s1) return Mathf.Min(EndD, s - FilletLength + (FilletD1 - FilletD0));
            return FilletD0 + (s - FilletD0) / FilletLength * (FilletD1 - FilletD0);
        }

        /// <summary>d per walked metre at d (1 except inside the fillet, where d advances faster than the walker).</summary>
        public static float DPerWalkMetre(float d)
        {
            return (d > FilletD0 && d < FilletD1) ? (FilletD1 - FilletD0) / FilletLength : 1f;
        }

        /// <summary>
        /// Nearest d to a (feet) position by horizontal distance, searched over the pieces within <see cref="PROJECT_WINDOW"/>
        /// metres of hintD (all pieces when hintD &lt; 0). A vertical mismatch above 3 m is penalised, which separates the hall
        /// catwalk (y -7) from the bottom dock and gantry (y -487) that lie on the same x = -45 line in plan.
        /// </summary>
        public static float Project(Vector3 pos, float hintD)
        {
            bool global = hintD < 0f;
            float lo = hintD - PROJECT_WINDOW, hi = hintD + PROJECT_WINDOW;
            float bestCost = float.MaxValue;
            float bestD = global ? 0f : ClampD(hintD);
            var c = AbyssConst.ARC_CENTRE;
            for (int i = 0; i < Pieces.Length; i++)
            {
                ref readonly Piece p = ref Pieces[i];
                if (!global && (p.D1 < lo || p.D0 > hi)) continue;
                float t;
                if (p.Kind == PieceKind.Line)
                {
                    float ax = p.B.x - p.A.x, az = p.B.z - p.A.z;
                    float l2 = ax * ax + az * az;
                    t = l2 > 1e-9f ? Mathf.Clamp01(((pos.x - p.A.x) * ax + (pos.z - p.A.z) * az) / l2) : 0f;
                }
                else
                {
                    float a = Mathf.Atan2(pos.x - c.x, c.y - pos.z);   // alpha of the position around C1
                    if (a < -0.5f * Mathf.PI) a += 2f * Mathf.PI;          // just past W8 (alpha slightly above pi)
                    t = Mathf.Clamp01(a / Mathf.PI);
                }
                float d = p.D0 + t * (p.D1 - p.D0);
                if (!global) d = d < lo ? lo : (d > hi ? hi : d);    // stay inside the search window (convex clamp)
                d = ClampD(d);
                Vector3 q = PiecePoint(p, d);
                float dx = pos.x - q.x, dz = pos.z - q.z;
                float cost = dx * dx + dz * dz;
                float dy = Mathf.Abs(pos.y - q.y) - 3f;
                if (dy > 0f) cost += 10f * dy * dy;
                if (cost < bestCost) { bestCost = cost; bestD = d; }
            }
            return bestD;
        }

        /// <summary>True when the position projects onto a piece after the lift (lower level).</summary>
        public static bool IsAfterRideD(float d) => d > AbyssConst.D_LIFT;

        /// <summary>
        /// Walkable corridor half-width at d: the lateral distance from the centreline to the invisible edge walls
        /// (capsule centre limit = HalfWidth - CAPSULE_R). Equal to the visible half-width everywhere except the lift deck
        /// (walkable 18 of 20), the balcony (edge walls 0.6 m inside) and the pier (edge walls 0.5 m inside, corridor 2.0).
        /// On the ledge (annulus 22-28) the inner collider sits at r = 22.4 (AbyssConst.COLLIDER_INNER_R).
        /// </summary>
        public static float HalfWidth(float d)
        {
            var WD = AbyssConst.WD;
            if (d < WD[3]) return AbyssConst.GALLERY_W * 0.5f;
            if (d < WD[4]) return AbyssConst.LANDING_W * 0.5f;
            if (d < WD[5]) return AbyssConst.BRIDGE_W * 0.5f;
            if (d < WD[6])
            {
                // W5-W6 straight (x = 0, z 216-219) runs into the ledge ring around C1: the walkable chord is bounded by the
                // ring's outer edge circle (R_OUT about (0, 244)), widening from the bridge half-width to the ledge half-width.
                float dz = AbyssConst.ARC_CENTRE.y - (AbyssConst.WD[5] + (d - WD[5]));
                float chord = Mathf.Sqrt(Mathf.Max(0f, AbyssConst.LEDGE_R_OUT * AbyssConst.LEDGE_R_OUT - dz * dz));
                return Mathf.Clamp(chord, AbyssConst.BRIDGE_W * 0.5f, AbyssConst.LEDGE_W * 0.5f);
            }
            if (d < WD[8]) return AbyssConst.LEDGE_W * 0.5f;
            if (d < WD[9]) return AbyssConst.APRON_W * 0.5f;
            if (d < WD[10]) return AbyssConst.CATWALK_W * 0.5f;
            if (d < WD[11]) return DOCK_APRON_W * 0.5f;
            if (d < WD[14]) return AbyssConst.LIFT_WALKABLE * 0.5f;
            if (d < WD[15]) return AbyssConst.GANTRY_W * 0.5f;
            if (d < WD[16]) return AbyssConst.TUNNEL_W * 0.5f;
            if (d < WD[17]) return AbyssConst.BALCONY_W * 0.5f - BALCONY_EDGE_INSET;
            return AbyssConst.PIER_W * 0.5f - PIER_EDGE_INSET;
        }

        /// <summary>Visible deck half-width at d (the edge of the visible walking surface).</summary>
        public static float VisibleHalfWidth(float d)
        {
            var WD = AbyssConst.WD;
            if (d >= WD[11] && d < WD[14]) return AbyssConst.LIFT_SIZE * 0.5f;
            if (d >= WD[16] && d < WD[17]) return AbyssConst.BALCONY_W * 0.5f;
            if (d >= WD[17]) return AbyssConst.PIER_W * 0.5f;
            return HalfWidth(d);
        }

        /// <summary>
        /// Footstep surface at d (14.3): stairs W1-W2 Stair, bridge W4-W5 SteelPlate, hall catwalk and gantry Grating,
        /// the lift deck (W11 to W14) LiftDeck, everything else (gallery, landing, ledge, aprons, tunnel, balcony, pier) Concrete.
        /// PlayerController additionally reports LiftDeck whenever the player is aboard the platform.
        /// </summary>
        public static SurfaceKind Surface(float d)
        {
            var WD = AbyssConst.WD;
            if (d >= WD[1] && d < WD[2]) return SurfaceKind.Stair;
            if (d >= WD[4] && d < WD[5]) return SurfaceKind.SteelPlate;
            if (d >= WD[9] && d < WD[10]) return SurfaceKind.Grating;
            if (d >= WD[11] && d < WD[14]) return SurfaceKind.LiftDeck;
            if (d >= WD[14] && d < WD[15]) return SurfaceKind.Grating;
            return SurfaceKind.Concrete;
        }

        /// <summary>Beat 1..7 at d from AbyssConst.BEAT_START_D (no hysteresis).</summary>
        public static int BeatAt(float d)
        {
            var s = AbyssConst.BEAT_START_D;
            for (int i = s.Length - 1; i > 0; i--)
                if (d >= s[i]) return i + 1;
            return 1;
        }

        /// <summary>Gallery stair tread index 0..15 at d, or -1 off the stairs (treads of STAIR_RUN from W1).</summary>
        public static int StairTread(float d)
        {
            if (d < AbyssConst.WD[1] || d >= AbyssConst.WD[2]) return -1;
            int k = Mathf.FloorToInt((d - AbyssConst.WD[1]) / AbyssConst.STAIR_RUN);
            return Mathf.Clamp(k, 0, AbyssConst.STAIR_COUNT - 1);
        }

        /// <summary>World z of a d on the beat-7 line (tunnel, balcony, pier run along x = -45 toward -Z).</summary>
        public static float Beat7Z(float d) => AbyssConst.W[15].z - (d - AbyssConst.WD[15]);

        /// <summary>d of a world z on the beat-7 line.</summary>
        public static float Beat7D(float z) => AbyssConst.WD[15] + (AbyssConst.W[15].z - z);
    }
}
