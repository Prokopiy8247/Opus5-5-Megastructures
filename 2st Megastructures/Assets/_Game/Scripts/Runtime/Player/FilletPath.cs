using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The sharp path polyline of Part 14.4 with circular fillets at its corners (xz plane, Vector2 = (x, z)).
    /// Path distance d runs along the filleted curve; every piece maps d monotonically and continuously onto the
    /// sharp progress s: a straight piece maps 1:1, an arc of radius r and turn theta maps its length r theta onto the
    /// sharp span [sC - t, sC + t] with t = r tan(theta / 2). So s is exact on every straight leg (all beat
    /// boundaries, the goal trigger) and continuous through the corners.
    /// Users: AutoWalker (walk fillets 1.2 / 4 / 4 m, 14.1) and ProgressDirector (wider progress fillets, so the
    /// projection of any reachable point is continuous: the fillet centre lies outside the lane).
    /// Allocation-free after construction.
    /// </summary>
    public sealed class FilletPath
    {
        struct Piece
        {
            public bool Arc;
            public Vector2 A, B;          // line end points (arc: start and end points)
            public Vector2 C;             // arc centre
            public float R, Ang0, Sweep;  // arc radius, start angle (rad), signed sweep (rad)
            public float D0, Len;         // path distance at the piece start, piece length
            public float S0, S1;          // sharp progress at the piece start and end
        }

        readonly Piece[] _p;

        /// <summary>Total length of the filleted path (m).</summary>
        public readonly float Length;

        /// <summary>Sharp progress at the path end (960).</summary>
        public float SharpEnd => _p[_p.Length - 1].S1;

        /// <summary>Path with fillet radii at the three real corners (0, 576), (100, 576) and (100, 660).</summary>
        public FilletPath(float r576, float r676, float r760)
            : this(RingConstants.Waypoints, RingWorld.Cumulative, CornerRadii(r576, r676, r760)) { }

        static float[] CornerRadii(float r576, float r676, float r760)
        {
            var w = RingConstants.Waypoints;
            var r = new float[w.Length];
            for (int i = 0; i < w.Length; i++)
            {
                Vector2 p = w[i];
                if (Near(p, new Vector2(0f, 576f))) r[i] = r576;
                else if (Near(p, new Vector2(100f, 576f))) r[i] = r676;
                else if (Near(p, new Vector2(100f, 660f))) r[i] = r760;
            }
            return r;
        }

        static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-4f;

        public FilletPath(Vector2[] w, float[] cum, float[] radius)
        {
            int n = w.Length;
            var t = new float[n];
            var tmp = new Piece[2 * n];
            int count = 0;
            // tangent lengths at the corners (collinear waypoints get none)
            for (int k = 1; k < n - 1; k++)
            {
                Vector2 din = (w[k] - w[k - 1]).normalized, dout = (w[k + 1] - w[k]).normalized;
                float theta = Mathf.Acos(Mathf.Clamp(Vector2.Dot(din, dout), -1f, 1f));
                if (theta < 1e-4f || radius[k] <= 0f) continue;
                float tk = radius[k] * Mathf.Tan(0.5f * theta);
                // never longer than half of either adjacent leg
                float lim = 0.5f * Mathf.Min((w[k] - w[k - 1]).magnitude, (w[k + 1] - w[k]).magnitude);
                t[k] = Mathf.Min(tk, lim);
            }
            float d = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 dir = (w[i + 1] - w[i]).normalized;
                Vector2 a = w[i] + dir * t[i];
                Vector2 b = w[i + 1] - dir * t[i + 1];
                float len = (b - a).magnitude;
                if (len > 1e-5f)
                {
                    tmp[count++] = new Piece { Arc = false, A = a, B = b, D0 = d, Len = len, S0 = cum[i] + t[i], S1 = cum[i + 1] - t[i + 1] };
                    d += len;
                }
                int k = i + 1;
                if (k < n - 1 && t[k] > 0f)
                {
                    Vector2 din = dir, dout = (w[k + 1] - w[k]).normalized;
                    float theta = Mathf.Acos(Mathf.Clamp(Vector2.Dot(din, dout), -1f, 1f));
                    float r = t[k] / Mathf.Max(1e-6f, Mathf.Tan(0.5f * theta));
                    float cross = din.x * dout.y - din.y * dout.x;
                    Vector2 nrm = cross < 0f ? new Vector2(din.y, -din.x) : new Vector2(-din.y, din.x);
                    Vector2 ps = w[k] - din * t[k], pe = w[k] + dout * t[k];
                    Vector2 c = ps + nrm * r;
                    float ang0 = Mathf.Atan2(ps.y - c.y, ps.x - c.x);
                    float sweep = cross < 0f ? -theta : theta;
                    float alen = r * theta;
                    tmp[count++] = new Piece { Arc = true, A = ps, B = pe, C = c, R = r, Ang0 = ang0, Sweep = sweep, D0 = d, Len = alen, S0 = cum[k] - t[k], S1 = cum[k] + t[k] };
                    d += alen;
                }
            }
            _p = new Piece[count];
            for (int i = 0; i < count; i++) _p[i] = tmp[i];
            Length = d;
        }

        int PieceAtD(float d)
        {
            for (int i = 0; i < _p.Length; i++) if (d < _p[i].D0 + _p[i].Len) return i;
            return _p.Length - 1;
        }

        int PieceAtS(float s)
        {
            for (int i = 0; i < _p.Length; i++) if (s < _p[i].S1) return i;
            return _p.Length - 1;
        }

        static Vector2 ArcPoint(in Piece p, float u)
        {
            float a = p.Ang0 + p.Sweep * (u / Mathf.Max(1e-6f, p.Len));
            return p.C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * p.R;
        }

        /// <summary>Point (x, z) at path distance d (clamped to the path).</summary>
        public Vector2 PointAt(float d)
        {
            d = Mathf.Clamp(d, 0f, Length);
            var p = _p[PieceAtD(d)];
            float u = Mathf.Clamp(d - p.D0, 0f, p.Len);
            if (!p.Arc) return Vector2.Lerp(p.A, p.B, u / Mathf.Max(1e-6f, p.Len));
            return ArcPoint(p, u);
        }

        /// <summary>Unit tangent (x, z) at path distance d.</summary>
        public Vector2 TangentAt(float d)
        {
            d = Mathf.Clamp(d, 0f, Length);
            var p = _p[PieceAtD(d)];
            if (!p.Arc) return (p.B - p.A) / Mathf.Max(1e-6f, p.Len);
            float u = Mathf.Clamp(d - p.D0, 0f, p.Len);
            float a = p.Ang0 + p.Sweep * (u / Mathf.Max(1e-6f, p.Len));
            float sg = p.Sweep < 0f ? -1f : 1f;
            return new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * sg;
        }

        /// <summary>Right-hand normal (x, z) at d (walking +Z the right is +X).</summary>
        public Vector2 RightAt(float d)
        {
            Vector2 t = TangentAt(d);
            return new Vector2(t.y, -t.x);
        }

        /// <summary>Heading in degrees (0 = +Z, 90 = +X) at d.</summary>
        public float HeadingAt(float d)
        {
            Vector2 t = TangentAt(d);
            return Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg;
        }

        /// <summary>Sharp progress s at path distance d.</summary>
        public float SharpS(float d)
        {
            if (d <= 0f) return _p[0].S0;
            if (d >= Length) return SharpEnd;
            var p = _p[PieceAtD(d)];
            float u = Mathf.Clamp01((d - p.D0) / Mathf.Max(1e-6f, p.Len));
            return p.S0 + (p.S1 - p.S0) * u;
        }

        /// <summary>Path distance d at sharp progress s (inverse of <see cref="SharpS"/>).</summary>
        public float DAtSharp(float s)
        {
            if (s <= _p[0].S0) return 0f;
            if (s >= SharpEnd) return Length;
            var p = _p[PieceAtS(s)];
            float u = Mathf.Clamp01((s - p.S0) / Mathf.Max(1e-6f, p.S1 - p.S0));
            return p.D0 + p.Len * u;
        }

        static float WrapPi(float a)
        {
            const float twoPi = 6.28318531f;
            a = a - twoPi * Mathf.Floor((a + Mathf.PI) / twoPi);
            return a;
        }

        /// <summary>Nearest path distance to xz. Only pieces within [hintD - window, hintD + window] are searched
        /// (window &lt; 0 = whole path). dist = distance from xz to the path.</summary>
        public float Project(Vector2 xz, float hintD, float window, out float dist)
        {
            float best = float.MaxValue, bestD = Mathf.Clamp(hintD, 0f, Length);
            for (int i = 0; i < _p.Length; i++)
            {
                var p = _p[i];
                if (window >= 0f && (p.D0 + p.Len < hintD - window || p.D0 > hintD + window)) continue;
                float u, dd;
                if (!p.Arc)
                {
                    Vector2 ab = p.B - p.A;
                    float tt = Mathf.Clamp01(Vector2.Dot(xz - p.A, ab) / Mathf.Max(1e-8f, p.Len * p.Len));
                    Vector2 q = p.A + ab * tt;
                    dd = (xz - q).sqrMagnitude;
                    u = tt * p.Len;
                }
                else
                {
                    Vector2 v = xz - p.C;
                    float ang = v.sqrMagnitude > 1e-10f ? Mathf.Atan2(v.y, v.x) : p.Ang0;
                    float sg = p.Sweep < 0f ? -1f : 1f;
                    float rel = WrapPi(ang - p.Ang0) * sg;
                    float sw = Mathf.Abs(p.Sweep);
                    // outside the sector: snap to the nearer end
                    if (rel < 0f || rel > sw)
                    {
                        float toStart = Mathf.Abs(WrapPi(ang - p.Ang0));
                        float toEnd = Mathf.Abs(WrapPi(ang - (p.Ang0 + p.Sweep)));
                        rel = toStart <= toEnd ? 0f : sw;
                    }
                    u = p.Len * rel / Mathf.Max(1e-6f, sw);
                    Vector2 q = ArcPoint(p, u);
                    dd = (xz - q).sqrMagnitude;
                }
                if (dd < best - 1e-7f) { best = dd; bestD = p.D0 + u; }
            }
            dist = Mathf.Sqrt(best);
            return bestD;
        }

        /// <summary>Signed lateral offset of xz from the path point at d (positive = right of travel).</summary>
        public float Lateral(Vector2 xz, float d) => Vector2.Dot(xz - PointAt(d), RightAt(d));
    }
}
