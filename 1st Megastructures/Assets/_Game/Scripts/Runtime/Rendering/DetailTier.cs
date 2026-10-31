using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Near/far detail swap by camera distance with hysteresis and a dithered cross-fade (CONTRACTS section 7, 14.6: "only
    /// detail tiers switch, by camera distance to the object, never by d"; never alpha blending).
    ///
    /// Coverage c (1 = near tier fully shown) is a continuous function of the camera distance to <c>worldBounds</c>, held inside a
    /// hysteresis band: moving away, c falls from 1 to 0 across [near - fadeWidth, near]; moving closer, c rises from 0 to 1
    /// across [near - fadeWidth - hysteresis, near - hysteresis]. Between the two curves c keeps its value, so a camera that
    /// hovers or sways at the boundary never makes the pattern flicker. With the defaults (150, 20, 20) the whole cross-fade
    /// lies in 110-150 m; city modules (near 170) cross-fade in 130-170 m as 14.5.5 asks. Large jumps (teleports) land on the
    /// correct side automatically because c is clamped between the curves.
    ///
    /// Shader convention (_AbFade): near = c, far = -(1 - c), exact complements of one static interleaved-gradient noise.
    /// A renderer whose tier is fully hidden is disabled; at c = 0 or 1 the property blocks are cleared so the visible tier
    /// renders with its plain material (SRP Batcher path). Plain class; call <see cref="Update"/> from the beat's Tick.
    /// </summary>
    public sealed class DetailTier
    {
        public readonly string Name;
        public readonly Bounds WorldBounds;
        public readonly float NearDistance;
        public readonly float Hysteresis;
        public readonly float FadeWidth;

        readonly List<Renderer> _nearR = new List<Renderer>();
        readonly List<Renderer> _farR = new List<Renderer>();
        readonly List<InstancedBatch> _nearB = new List<InstancedBatch>();
        readonly List<InstancedBatch> _farB = new List<InstancedBatch>();

        readonly MaterialPropertyBlock _mpbNear = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _mpbFar = new MaterialPropertyBlock();

        float _c = -1f;              // -1 = not evaluated yet
        float _appliedC = -1f;
        bool _blocksBound;

        /// <summary>c: 1 = near tier fully shown, 0 = far tier fully shown.</summary>
        public float Coverage => _c < 0f ? 1f : _c;

        /// <summary>Camera distance to the bounds at the last Update (m).</summary>
        public float LastDistance { get; private set; }

        public DetailTier(string name, Bounds worldBounds, float nearDistance = 150f, float hysteresis = 20f, float fadeWidth = 20f)
        {
            Name = name;
            WorldBounds = worldBounds;
            NearDistance = nearDistance;
            Hysteresis = Mathf.Max(0f, hysteresis);
            FadeWidth = Mathf.Max(0.01f, fadeWidth);
        }

        public void AddNear(Renderer r) { if (r != null) _nearR.Add(r); }
        public void AddFar(Renderer r) { if (r != null) _farR.Add(r); }
        public void AddNear(InstancedBatch b) { if (b != null) _nearB.Add(b); }
        public void AddFar(InstancedBatch b) { if (b != null) _farB.Add(b); }

        /// <summary>Coverage while moving away (upper curve): 1 inside near - fadeWidth, 0 beyond near.</summary>
        public float RecedeCurve(float dist) => 1f - Smooth(NearDistance - FadeWidth, NearDistance, dist);

        /// <summary>Coverage while approaching (lower curve): the receding curve shifted inward by the hysteresis.</summary>
        public float ApproachCurve(float dist) => 1f - Smooth(NearDistance - FadeWidth - Hysteresis, NearDistance - Hysteresis, dist);

        /// <summary>Evaluate the tier for this frame's camera position and apply _AbFade / enabled states. Allocation-free.</summary>
        public void Update(Vector3 cameraPos)
        {
            float dist = Mathf.Sqrt(WorldBounds.SqrDistance(cameraPos));
            LastDistance = dist;
            if (QaHooks.TierOverride >= 0f)   // QA tier crossover test (14.10 b, c): render one tier only
            {
                float o = Mathf.Clamp01(QaHooks.TierOverride);
                if (o != _appliedC) Apply(o);
                return;
            }
            float hi = RecedeCurve(dist);
            float lo = ApproachCurve(dist);
            float c = _c < 0f ? hi : Mathf.Clamp(_c, lo, hi);
            _c = c;
            if (c != _appliedC) Apply(c);
        }

        /// <summary>Force a coverage (QA: tier crossover test renders only one tier). Pass a negative value to release.</summary>
        public void ForceCoverage(float c)
        {
            if (c < 0f) { _c = -1f; _appliedC = -1f; return; }
            _c = Mathf.Clamp01(c);
            Apply(_c);
        }

        void Apply(float c)
        {
            _appliedC = c;
            bool nearVisible = c > 0f;
            bool farVisible = c < 1f;
            bool fading = nearVisible && farVisible;

            if (fading)
            {
                _mpbNear.SetFloat(ShaderIds.AbFade, c);
                _mpbFar.SetFloat(ShaderIds.AbFade, -(1f - c));
            }

            for (int i = 0; i < _nearR.Count; i++) SetRenderer(_nearR[i], nearVisible, fading ? _mpbNear : null);
            for (int i = 0; i < _farR.Count; i++) SetRenderer(_farR[i], farVisible, fading ? _mpbFar : null);
            _blocksBound = fading;

            for (int i = 0; i < _nearB.Count; i++)
            {
                InstancedBatch b = _nearB[i];
                b.Visible = nearVisible;
                b.Fade = fading ? c : 1f;
            }
            for (int i = 0; i < _farB.Count; i++)
            {
                InstancedBatch b = _farB[i];
                b.Visible = farVisible;
                b.Fade = fading ? -(1f - c) : 1f;
            }
        }

        void SetRenderer(Renderer r, bool visible, MaterialPropertyBlock block)
        {
            if (r == null) return;
            if (r.enabled != visible) r.enabled = visible;
            if (block != null) r.SetPropertyBlock(block);
            else if (_blocksBound || r.HasPropertyBlock()) r.SetPropertyBlock(null);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = b > a ? (x - a) / (b - a) : (x >= b ? 1f : 0f);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
