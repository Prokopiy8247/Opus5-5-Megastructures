using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 3 collar generator. The collar is swept over one shared list of frame angles (256 segments over alpha 0..pi plus
    /// the pad frames and the kerb break angles), so every strip, the walking surface and the walking-surface collider share
    /// their ring vertices bit for bit (no T-junctions, collider == visible surface). A profile point (r, dy) sits at
    /// (r sin a, TopY(a) + dy, 244 - r cos a). Pad: each point is clamped to the angle where its radius meets the cut plane
    /// (x = -3 for the body, x = -2.98 for the walking surface, whose end edge is chamfered), so the first ring of every strip
    /// lies exactly on the cut and the end faces are planar. Normals come from the swept surface itself (helicoid aware).
    /// UV0: walking surface (r - 25, 25 a) (ConcreteFloor worn path follows the curve); body (arc length, y) for ConcreteCyl.
    /// </summary>
    public sealed partial class Beat03_Ledge
    {
        struct PP
        {
            public float R, Dy, CutX;
            public byte Ao, Wear;
            public PP(float r, float dy, float cutX, byte ao = 255, byte wear = 0) { R = r; Dy = dy; CutX = cutX; Ao = ao; Wear = wear; }
        }

        const int UV_TOP = 0, UV_BODY = 1;
        const float CONE_TOP_R = R_OUT - SOFFIT_W;                                   // 27.5
        const float SOFFIT_DY = -AbyssConst.LEDGE_COLLAR_THICK;                      // -2.5
        const float CONE_BOTTOM_DY = SOFFIT_DY - AbyssConst.LEDGE_CORBEL;            // -10.5: tapers to the column over 8 m
        const float X = PAD_END_X;

        // walking surface: radial subdivisions carry the baked AO (dark in the corner at the column foot, a touch at the kerb)
        static readonly PP[] TOP =
        {
            new PP(R_COLLAR_IN, 0f, CUT_TOP, 100), new PP(22.15f, 0f, CUT_TOP, 138), new PP(22.45f, 0f, CUT_TOP, 176),
            new PP(22.9f, 0f, CUT_TOP, 210), new PP(23.6f, 0f, CUT_TOP, 238), new PP(24.4f, 0f, CUT_TOP), new PP(25f, 0f, CUT_TOP),
            new PP(25.6f, 0f, CUT_TOP), new PP(26.4f, 0f, CUT_TOP), new PP(27.2f, 0f, CUT_TOP, 248), new PP(27.6f, 0f, CUT_TOP, 232),
            new PP(R_WALK, 0f, CUT_TOP, 212), new PP(R_EDGE, 0f, CUT_TOP, 240),
        };

        // outer edge without kerb (bridge landing, pad end): 2 cm chamfer from the walking surface down to the fascia
        static readonly PP[] BARE = { new PP(R_EDGE, 0f, CUT_TOP, 255, 255), new PP(R_OUT, -EDGE_BEVEL, X, 255, 255) };

        // kerb 0.15 x 0.15 on the slab edge, its top edges chamfered 2 cm; its outer face continues the fascia
        static readonly PP[] KERB_IN = { new PP(R_WALK, 0f, X, 205), new PP(R_WALK, KERB_H - EDGE_BEVEL, X) };
        static readonly PP[] KERB_CH_IN = { new PP(R_WALK, KERB_H - EDGE_BEVEL, X, 255, 255), new PP(R_WALK + EDGE_BEVEL, KERB_H, X, 255, 255) };
        static readonly PP[] KERB_TOP = { new PP(R_WALK + EDGE_BEVEL, KERB_H, X, 255, 90), new PP(R_EDGE, KERB_H, X, 255, 90) };
        static readonly PP[] KERB_CH_OUT = { new PP(R_EDGE, KERB_H, X, 255, 255), new PP(R_OUT, KERB_H - EDGE_BEVEL, X, 255, 255) };
        static readonly PP[] KERB_OUT = { new PP(R_OUT, KERB_H - EDGE_BEVEL, X), new PP(R_OUT, -EDGE_BEVEL, X) };

        static readonly Vector2[] KERB_SECTION =
        {
            new Vector2(R_WALK, 0f), new Vector2(R_WALK, KERB_H - EDGE_BEVEL), new Vector2(R_WALK + EDGE_BEVEL, KERB_H),
            new Vector2(R_EDGE, KERB_H), new Vector2(R_OUT, KERB_H - EDGE_BEVEL), new Vector2(R_OUT, -EDGE_BEVEL), new Vector2(R_EDGE, 0f),
        };

        // fascia 2.5 m, its lower edge chamfered 3 cm, the soffit strip, the corbel cone to the column
        static readonly PP[] FASCIA = { new PP(R_OUT, -EDGE_BEVEL, X), new PP(R_OUT, SOFFIT_DY + FASCIA_BEVEL, X) };
        static readonly PP[] FASCIA_CH = { new PP(R_OUT, SOFFIT_DY + FASCIA_BEVEL, X, 255, 255), new PP(R_OUT - FASCIA_BEVEL, SOFFIT_DY, X, 255, 255) };
        static readonly PP[] SOFFIT = { new PP(R_OUT - FASCIA_BEVEL, SOFFIT_DY, X), new PP(CONE_TOP_R, SOFFIT_DY, X, 210) };
        static readonly PP[] CONE = MakeCone();
        static readonly Vector2 CONE_OUT = new Vector2(-(CONE_BOTTOM_DY - SOFFIT_DY), -(CONE_TOP_R - R_COLLAR_IN));

        // graybox: plain slab r 21.95-28, 2.5 m thick, cut at x = -3
        static readonly PP[] GB_TOP = { new PP(R_COLLAR_IN, 0f, X), new PP(AbyssConst.LEDGE_R_CENTRE, 0f, X), new PP(R_OUT, 0f, X) };
        static readonly PP[] GB_FASCIA = { new PP(R_OUT, 0f, X), new PP(R_OUT, SOFFIT_DY, X) };
        static readonly PP[] GB_BOTTOM = { new PP(R_OUT, SOFFIT_DY, X), new PP(R_COLLAR_IN, SOFFIT_DY, X) };

        // walking-surface collider radii: a superset of TOP (and of GB_TOP), so on alpha 0..pi the collider shares every
        // vertex of the visible walking surface and, for the full tier, its triangles exactly
        static readonly float[] COLLIDER_RADII =
        {
            COLLIDER_R_IN, R_COLLAR_IN, 22.15f, 22.45f, 22.9f, 23.6f, 24.4f, 25f, 25.6f, 26.4f, 27.2f, 27.6f, R_WALK, R_EDGE, R_OUT, 28.3f, COLLIDER_R_OUT,
        };

        static PP[] MakeCone()
        {
            float[] t = { 0f, 0.15f, 0.35f, 0.55f, 0.75f, 0.9f, 1f };
            byte[] ao = { 205, 238, 255, 255, 235, 185, 120 };
            var p = new PP[t.Length];
            for (int i = 0; i < t.Length; i++)
                p[i] = new PP(Mathf.Lerp(CONE_TOP_R, R_COLLAR_IN, t[i]), Mathf.Lerp(SOFFIT_DY, CONE_BOTTOM_DY, t[i]), X, ao[i]);
            return p;
        }

        // ---------------------------------------------------------------- frames
        readonly List<double> _frames = new List<double>(320);
        int _kKerbStart, _kOpenL, _kZero, _kOpenR, _kEnd;
        double _aKerbStart, _aOpen;

        void BuildFrames()
        {
            _frames.Clear();
            double step = Math.PI / ARC_SEGMENTS;
            double a0 = CutAlpha(R_COLLAR_IN, PAD_END_X);   // the most negative cut angle (inner edge)
            int pad = Math.Max(1, (int)Math.Ceiling(-a0 / step));
            for (int k = 0; k < pad; k++) _frames.Add(a0 * (pad - k) / pad);
            for (int k = 0; k < ARC_SEGMENTS; k++) _frames.Add(k * Math.PI / ARC_SEGMENTS);
            _frames.Add(Math.PI);
            _aOpen = Math.Asin(KERB_OPEN_HALF / R_OUT);
            _aKerbStart = -Math.Asin(-KERB_PAD_END_X / (R_OUT - 0.5 * KERB_W));
            InsertFrame(_aKerbStart, step);
            InsertFrame(-_aOpen, step);
            InsertFrame(_aOpen, step);
            _kKerbStart = _frames.IndexOf(_aKerbStart);
            _kOpenL = _frames.IndexOf(-_aOpen);
            _kZero = _frames.IndexOf(0.0);
            _kOpenR = _frames.IndexOf(_aOpen);
            _kEnd = _frames.Count - 1;
        }

        void InsertFrame(double a, double step)
        {
            int i = 0;
            while (i < _frames.Count && _frames[i] < a) i++;
            for (int k = i - 1; k <= i; k++)
            {
                if (k < 0 || k >= _frames.Count) continue;
                double f = _frames[k];
                if (Math.Abs(f - a) < 0.25 * step && f != 0.0 && f != 0.5 * Math.PI && f != Math.PI && k > 0) { _frames[k] = a; return; }
            }
            _frames.Insert(i, a);
        }

        // ---------------------------------------------------------------- sweep and caps

        /// <summary>
        /// Sweep a straight profile strip (points collinear in (r, dy)) over frames k0..k1. Every point is clamped to its own
        /// cut angle. Normal = cross(d/dt, d/dalpha) of the swept surface, turned toward the profile's outward side.
        /// </summary>
        static void Sweep(MeshBuilder mb, List<double> frames, PP[] p, Vector2 outward, int k0, int k1, int uvMode, float uvR = 0f)
        {
            int n = p.Length;
            if (k1 <= k0 || n < 2) return;
            Vector2 d = new Vector2(p[n - 1].R - p[0].R, p[n - 1].Dy - p[0].Dy).normalized;
            MeshKit.Reserve(mb, (k1 - k0 + 1) * n, (k1 - k0) * (n - 1) * 6);
            Color32 saved = mb.Color;
            int first = mb.VertexCount;
            for (int k = k0; k <= k1; k++)
            {
                double frame = frames[k];
                for (int j = 0; j < n; j++)
                {
                    PP q = p[j];
                    double al = Math.Max(frame, CutAlpha(q.R, q.CutX));
                    SinCos(al, out double s, out double c);
                    float sa = (float)s, ca = (float)c;
                    Vector3 pos = Ring(al, q.R, TopY(al) + q.Dy);
                    var su = new Vector3(d.x * sa, d.y, -d.x * ca);
                    var sAl = new Vector3(q.R * ca, (float)TopSlope(al), q.R * sa);
                    Vector3 nrm = Vector3.Cross(su, sAl);
                    var o3 = new Vector3(outward.x * sa, outward.y, -outward.x * ca);
                    if (Vector3.Dot(nrm, o3) < 0f) nrm = -nrm;
                    nrm.Normalize();
                    Vector2 uv;
                    if (uvMode == UV_TOP)
                    {
                        // ConcreteFloor worn path: lateral offset from the walking line r = 25; on the pad (alpha < 0, where
                        // nobody walks) the offset grows with |x| so the worn band fades out within half a metre of alpha 0
                        float u = q.R - AbyssConst.LEDGE_R_CENTRE;
                        if (al < 0.0) u = Mathf.Sqrt(u * u + 2.25f * pos.x * pos.x);
                        uv = new Vector2(u, (float)(AbyssConst.LEDGE_R_CENTRE * al));
                    }
                    else uv = new Vector2((float)((uvR > 0f ? uvR : q.R) * (al + 1.5 * Math.PI)), pos.y);
                    mb.Color = new Color32(q.Ao, q.Wear, 0, 255);
                    mb.Vertex(pos, nrm, uv);
                }
            }
            mb.Color = saved;
            for (int k = 0; k < k1 - k0; k++)
            for (int j = 0; j < n - 1; j++)
            {
                int a = first + k * n + j, b = a + 1, c = a + n + 1, e = a + n;
                BeatKit.Tri(mb, a, b, c);
                BeatKit.Tri(mb, a, c, e);
            }
        }

        static readonly List<Vector3> s_u = new List<Vector3>(32), s_l = new List<Vector3>(32);
        static readonly List<float> s_uk = new List<float>(32), s_lk = new List<float>(32);
        static readonly List<Vector2> s_poly = new List<Vector2>(16);
        static readonly List<int> s_tris = new List<int>(48);

        /// <summary>
        /// Planar face between two chains that are both monotone in a key (the radius): triangles zip along both chains, so
        /// every chain vertex is used (no T-junction with the strips that share them). UV0 = (key or z, y).
        /// </summary>
        static void Zipper(MeshBuilder mb, Vector3 normal, Color32 color, bool uvFromZ)
        {
            Color32 saved = mb.Color;
            mb.Color = color;
            int nu = s_u.Count, nl = s_l.Count;
            int u0 = mb.VertexCount;
            for (int i = 0; i < nu; i++) mb.Vertex(s_u[i], normal, new Vector2(uvFromZ ? s_u[i].z : s_uk[i], s_u[i].y));
            int l0 = mb.VertexCount;
            for (int i = 0; i < nl; i++) mb.Vertex(s_l[i], normal, new Vector2(uvFromZ ? s_l[i].z : s_lk[i], s_l[i].y));
            mb.Color = saved;
            int iu = 0, il = 0;
            while (iu < nu - 1 || il < nl - 1)
            {
                bool advanceU = il >= nl - 1 || (iu < nu - 1 && s_uk[iu + 1] <= s_lk[il + 1]);
                if (advanceU) { BeatKit.Tri(mb, u0 + iu, u0 + iu + 1, l0 + il); iu++; }
                else { BeatKit.Tri(mb, u0 + iu, l0 + il + 1, l0 + il); il++; }
            }
        }

        static void ChainAdd(List<Vector3> chain, List<float> keys, Vector3 p, float key) { chain.Add(p); keys.Add(key); }

        /// <summary>Point of a profile (r, dy) on the pad's end plane x = cutX.</summary>
        static Vector3 OnCut(float r, float dy, float cutX)
        {
            double a = CutAlpha(r, cutX);
            return Ring(a, r, TopY(a) + dy);
        }

        /// <summary>The body's lower outline from the column (bottom of the cone) up to the fascia top, ascending radius.</summary>
        static void LowerOutline(bool onPadCut, double alpha)
        {
            s_l.Clear(); s_lk.Clear();
            for (int i = CONE.Length - 1; i >= 0; i--) AddOutline(CONE[i].R, CONE[i].Dy, onPadCut, alpha);
            AddOutline(R_OUT - FASCIA_BEVEL, SOFFIT_DY, onPadCut, alpha);
            AddOutline(R_OUT, SOFFIT_DY + FASCIA_BEVEL, onPadCut, alpha);
            AddOutline(R_OUT, -EDGE_BEVEL, onPadCut, alpha);
        }

        static void AddOutline(float r, float dy, bool onPadCut, double alpha)
        {
            Vector3 p = onPadCut ? OnCut(r, dy, PAD_END_X) : Ring(alpha, r, TopY(alpha) + dy);
            ChainAdd(s_l, s_lk, p, r);
        }

        void BuildCollarTop(MeshBuilder mb, PP[] top, out Mesh pad, out Mesh helix)
        {
            Sweep(mb, _frames, top, Vector2.up, 0, _kZero, UV_TOP);
            pad = BeatKit.Bake(mb, top == GB_TOP ? "LedgePad" : "CollarPadTop", _meshes);
            Sweep(mb, _frames, top, Vector2.up, _kZero, _kEnd, UV_TOP);
            helix = BeatKit.Bake(mb, top == GB_TOP ? "LedgeHelix" : "CollarTop", _meshes);
        }

        void BuildCollarBody(MeshBuilder mb)
        {
            List<double> f = _frames;
            var bareOut = new Vector2(1f, 1f);
            // outer edge: bare chamfer where there is no kerb (pad end, bridge landing), the kerb elsewhere
            Sweep(mb, f, BARE, bareOut, 0, _kKerbStart, UV_BODY);
            Sweep(mb, f, BARE, bareOut, _kOpenL, _kOpenR, UV_BODY);
            KerbRun(mb, _kKerbStart, _kOpenL);
            KerbRun(mb, _kOpenR, _kEnd);
            KerbCap(mb, _aKerbStart, -1f);
            KerbCap(mb, -_aOpen, 1f);
            KerbCap(mb, _aOpen, -1f);
            KerbCap(mb, Math.PI, 1f);

            Sweep(mb, f, FASCIA, Vector2.right, 0, _kEnd, UV_BODY);
            Sweep(mb, f, FASCIA_CH, new Vector2(1f, -1f), 0, _kEnd, UV_BODY);
            Sweep(mb, f, SOFFIT, Vector2.down, 0, _kEnd, UV_BODY);
            Sweep(mb, f, CONE, CONE_OUT, 0, _kEnd, UV_BODY, R_OUT);   // u at the outer radius: radial formwork panel lines

            // pad end: 2 cm chamfer along the cut edge of the walking surface, then the planar end face on x = -3
            s_u.Clear(); s_uk.Clear(); s_l.Clear(); s_lk.Clear();
            for (int i = 0; i < TOP.Length; i++)
            {
                ChainAdd(s_u, s_uk, OnCut(TOP[i].R, 0f, CUT_TOP), TOP[i].R);
                ChainAdd(s_l, s_lk, OnCut(TOP[i].R, -EDGE_BEVEL, PAD_END_X), TOP[i].R);
            }
            ChainAdd(s_l, s_lk, OnCut(R_OUT, -EDGE_BEVEL, PAD_END_X), R_OUT);
            Zipper(mb, new Vector3(-1f, 1f, 0f).normalized, new Color32(255, 255, 0, 255), true);
            // the chamfer's lower chain is the end face's upper chain
            s_u.Clear(); s_uk.Clear();
            for (int i = 0; i < s_l.Count; i++) ChainAdd(s_u, s_uk, s_l[i], s_lk[i]);
            LowerOutline(true, 0.0);
            Zipper(mb, Vector3.left, new Color32(255, 0, 0, 255), true);

            // end face at alpha = pi under the walking surface (the apron's east face lies in the same plane, back to back)
            EndFaceAt(mb, Math.PI, TOP, true);
        }

        void KerbRun(MeshBuilder mb, int k0, int k1)
        {
            List<double> f = _frames;
            Sweep(mb, f, KERB_IN, Vector2.left, k0, k1, UV_BODY);
            Sweep(mb, f, KERB_CH_IN, new Vector2(-1f, 1f), k0, k1, UV_BODY);
            Sweep(mb, f, KERB_TOP, Vector2.up, k0, k1, UV_BODY);
            Sweep(mb, f, KERB_CH_OUT, new Vector2(1f, 1f), k0, k1, UV_BODY);
            Sweep(mb, f, KERB_OUT, Vector2.right, k0, k1, UV_BODY);
        }

        /// <summary>Planar end of the kerb at alpha (the section closed along the bare-edge chamfer), facing +/- tangent.</summary>
        static void KerbCap(MeshBuilder mb, double alpha, float facing)
        {
            Frame(alpha, out _, out Vector3 tan);
            Vector3 n = tan * facing;
            float y = TopY(alpha);
            s_poly.Clear();
            for (int i = 0; i < KERB_SECTION.Length; i++) s_poly.Add(KERB_SECTION[i]);
            MeshKit.Triangulate(s_poly, s_tris);
            Color32 saved = mb.Color;
            mb.Color = new Color32(255, 255, 0, 255);
            int b0 = mb.VertexCount;
            for (int i = 0; i < s_poly.Count; i++)
                mb.Vertex(Ring(alpha, s_poly[i].x, y + s_poly[i].y), n, new Vector2(s_poly[i].x, y + s_poly[i].y));
            mb.Color = saved;
            for (int t = 0; t + 2 < s_tris.Count; t += 3) BeatKit.Tri(mb, b0 + s_tris[t], b0 + s_tris[t + 1], b0 + s_tris[t + 2]);
        }

        /// <summary>End face of the body at a radial plane (alpha = pi), from the walking surface ring down the outline.</summary>
        static void EndFaceAt(MeshBuilder mb, double alpha, PP[] top, bool full)
        {
            Frame(alpha, out _, out Vector3 tan);
            float y = TopY(alpha);
            s_u.Clear(); s_uk.Clear();
            for (int i = 0; i < top.Length; i++) ChainAdd(s_u, s_uk, Ring(alpha, top[i].R, y + top[i].Dy), top[i].R);
            if (full)
            {
                ChainAdd(s_u, s_uk, Ring(alpha, R_OUT, y - EDGE_BEVEL), R_OUT);
                LowerOutline(false, alpha);
            }
            else
            {
                s_l.Clear(); s_lk.Clear();
                ChainAdd(s_l, s_lk, Ring(alpha, R_COLLAR_IN, y + SOFFIT_DY), R_COLLAR_IN);
                ChainAdd(s_l, s_lk, Ring(alpha, R_OUT, y + SOFFIT_DY), R_OUT);
                ChainAdd(s_l, s_lk, Ring(alpha, R_OUT, y), R_OUT);
            }
            Zipper(mb, tan, new Color32(255, 0, 0, 255), false);
        }

        void BuildGrayboxBody(MeshBuilder mb)
        {
            Sweep(mb, _frames, GB_FASCIA, Vector2.right, 0, _kEnd, UV_BODY);
            Sweep(mb, _frames, GB_BOTTOM, Vector2.down, 0, _kEnd, UV_BODY);
            // pad end face on x = -3
            s_u.Clear(); s_uk.Clear(); s_l.Clear(); s_lk.Clear();
            for (int i = 0; i < GB_TOP.Length; i++) ChainAdd(s_u, s_uk, OnCut(GB_TOP[i].R, 0f, PAD_END_X), GB_TOP[i].R);
            ChainAdd(s_l, s_lk, OnCut(R_COLLAR_IN, SOFFIT_DY, PAD_END_X), R_COLLAR_IN);
            ChainAdd(s_l, s_lk, OnCut(R_OUT, SOFFIT_DY, PAD_END_X), R_OUT);
            ChainAdd(s_l, s_lk, OnCut(R_OUT, 0f, PAD_END_X), R_OUT);
            Zipper(mb, Vector3.left, new Color32(255, 0, 0, 255), true);
            EndFaceAt(mb, Math.PI, GB_TOP, false);
        }

        // ---------------------------------------------------------------- walking-surface collider

        /// <summary>
        /// Closed 1 m slab whose top is the walking surface: r 21.5-28.6 (under the column wall and the outer wall), alpha from
        /// -0.25 (flat, under the pad end wall) through the helix (the collar's own frames) to pi + 0.06 (flat, onto the apron).
        /// </summary>
        Mesh BuildWalkColliderMesh()
        {
            var frames = new List<double>(_frames.Count + 32);
            double step = Math.PI / ARC_SEGMENTS;
            int pre = (int)Math.Ceiling(-COLLIDER_ALPHA_BEFORE / step);
            for (int k = 0; k < pre; k++) frames.Add(COLLIDER_ALPHA_BEFORE * (double)(pre - k) / pre);
            for (int k = 0; k < _frames.Count; k++) if (_frames[k] >= 0.0) frames.Add(_frames[k]);
            const int past = 5;
            for (int k = 1; k <= past; k++) frames.Add(Math.PI + COLLIDER_ALPHA_PAST * k / past);

            int nr = COLLIDER_RADII.Length;
            var top = new PP[nr];
            var bottom = new PP[nr];
            for (int i = 0; i < nr; i++)
            {
                top[i] = new PP(COLLIDER_RADII[i], 0f, 0f);
                bottom[i] = new PP(COLLIDER_RADII[i], -COLLIDER_THICK, 0f);
            }
            PP[] inner = { new PP(COLLIDER_R_IN, 0f, 0f), new PP(COLLIDER_R_IN, -COLLIDER_THICK, 0f) };
            PP[] outer = { new PP(COLLIDER_R_OUT, 0f, 0f), new PP(COLLIDER_R_OUT, -COLLIDER_THICK, 0f) };
            int last = frames.Count - 1;
            var mb = new MeshBuilder();
            Sweep(mb, frames, top, Vector2.up, 0, last, UV_TOP);
            Sweep(mb, frames, bottom, Vector2.down, 0, last, UV_TOP);
            Sweep(mb, frames, inner, Vector2.left, 0, last, UV_TOP);
            Sweep(mb, frames, outer, Vector2.right, 0, last, UV_TOP);
            for (int end = 0; end < 2; end++)
            {
                double a = frames[end == 0 ? 0 : last];
                Frame(a, out _, out Vector3 tan);
                float y = TopY(a);
                s_u.Clear(); s_uk.Clear(); s_l.Clear(); s_lk.Clear();
                for (int i = 0; i < nr; i++)
                {
                    ChainAdd(s_u, s_uk, Ring(a, COLLIDER_RADII[i], y), COLLIDER_RADII[i]);
                    ChainAdd(s_l, s_lk, Ring(a, COLLIDER_RADII[i], y - COLLIDER_THICK), COLLIDER_RADII[i]);
                }
                Zipper(mb, end == 0 ? -tan : tan, new Color32(255, 0, 0, 255), false);
            }
            return BeatKit.Bake(mb, "LedgeWalkSurface", _meshes);
        }

        // ---------------------------------------------------------------- apron and bridge bearing

        /// <summary>
        /// Apron slab 32 x 14 x 2.5 (board-formed, worn path along z = 269) from the collar end (x = 0) to the hall wall
        /// (x = -32), with 0.15 x 0.15 kerbs on its open edges (4 cm into the slab, 5 mm back from the slab faces). Its east
        /// face is back to back with the collar's end face at alpha = pi; its top meets the collar's last ring at x = 0.
        /// </summary>
        void BuildApron(MeshBuilder mb, MaterialLibrary mats)
        {
            float ax0 = AbyssConst.HALL_X0, az0 = AbyssConst.APRON_Z0, az1 = AbyssConst.APRON_Z1, y8 = AbyssConst.W[8].y;
            float t = AbyssConst.LEDGE_COLLAR_THICK;
            MeshKit.ChamferBox(mb, new Vector3(ax0 * 0.5f, y8 - t * 0.5f, (az0 + az1) * 0.5f), new Vector3(-ax0, t, az1 - az0), APRON_BEVEL);
            BeatKit.FloorUVAlongX(mb, 0, AbyssConst.W[8].z);

            const float embed = 0.04f, setBack = 0.005f;
            float ky = y8 + 0.5f * (KERB_H - embed), kh = KERB_H + embed;
            Vector2 c1 = AbyssConst.ARC_CENTRE;
            float zS = az0 + setBack + 0.5f * KERB_W;
            float colX = -Mathf.Sqrt(R_FACE * R_FACE - (zS - c1.y) * (zS - c1.y)) + 0.25f;   // the south kerb runs into C1
            float zN0 = az1 - setBack - KERB_W, zN1 = az1 - setBack;
            float zE0 = c1.y + R_WALK;
            KerbBox(mb, ax0 + setBack, -setBack, zN0, zN1, ky, kh);                     // north (+Z) edge
            KerbBox(mb, -setBack - KERB_W, -setBack, zE0, zN0, ky, kh);                 // east edge beyond the collar
            KerbBox(mb, ax0 + setBack, colX, az0 + setBack, az0 + setBack + KERB_W, ky, kh);   // south edge to the column
            _apron = BeatKit.Emit(mb, _geo, "Apron", mats[MatId.ConcreteBoard], _meshes, UnityEngine.Rendering.ShadowCastingMode.On);
        }

        static void KerbBox(MeshBuilder mb, float x0, float x1, float z0, float z1, float yc, float h)
        {
            MeshKit.ChamferBox(mb, new Vector3(0.5f * (x0 + x1), yc, 0.5f * (z0 + z1)), new Vector3(x1 - x0, h, z1 - z0), EDGE_BEVEL);
        }

        /// <summary>
        /// Concrete bearing nib under the bridge end: the deck's truss ends on z = 216 against it. 1.8 m wide, from the soffit
        /// (y -5.5, back to back with it) down to y -10.3, buried in the corbel cone at its back.
        /// </summary>
        static void BuildBridgeBearing(MeshBuilder mb)
        {
            float top = TopY(0.0) + SOFFIT_DY, bottom = top - 4.8f;
            float z0 = AbyssConst.BRIDGE_Z1, z1 = z0 + 6f;
            MeshKit.ChamferBox(mb, new Vector3(0f, 0.5f * (top + bottom), 0.5f * (z0 + z1)), new Vector3(1.8f, top - bottom, z1 - z0), FASCIA_BEVEL);
        }
    }
}
