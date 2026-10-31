using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Hatch of the docking capsule (14.5.1 Motion, 14.4 collider (8)). Triggers once at s &gt;= 6.5 and opens in 2.0 s
    /// with smootherstep: gap = 2.0 smootherstep(t / 2.0), so 0.70 m at 0.84 s, 1.0 m at 1.0 s, 2.0 m at 2.0 s.
    /// Live play is time-based; capture mode is progress-based (time since the trigger = (s - 6.5) / 1.5 m/s), frame
    /// sequences (SequenceMode) stay time-based. Both branches share one trigger time: the first time-based trigger
    /// back-dates _t0 by (s - 6.5) / 1.5 (so a debug start past the hatch opens at once and a live trigger is late by
    /// at most one frame of travel), and the capture branch keeps _triggered / _t0 in step with its progress gap, so
    /// a sequence that settles in capture mode and then switches to SequenceMode continues from the same gap (no pop
    /// between the settle and frame 0, e.g. the b12 boundary sequence from s 9.0). The two leaf BoxColliders switch off once the gap is &gt;= 0.5 m; the
    /// static jamb colliders at x = +-1.0 (built by Beat01CapsuleBuilder) take over. Also switches the two capsule point
    /// lights off once the player is far outside (they never light the valley). Zero allocations per frame.
    /// </summary>
    public sealed class CapsuleHatch : MonoBehaviour, IRingAnimated
    {
        /// <summary>Leaf colliders switch off at this gap (14.4 (8)).</summary>
        public const float ColliderOffGap = 0.5f;
        /// <summary>Hidden overlap of each leaf inside its pocket when closed (m): the leaf mesh is 1.0 + 0.10 wide, more
        /// than the 4.9 cm a key ray travels sideways through the 2 cm slot clearance, so the closed hatch is light-tight.</summary>
        public const float LeafOverlap = 0.10f;
        /// <summary>Lights off beyond this progress (m): 5 m range lights, the player is 30 m outside by then.</summary>
        const float LightsOffS = 40f;

        public Transform LeafLeft, LeafRight;
        public BoxCollider ColliderLeft, ColliderRight;
        public Light[] Lights;

        bool _triggered;
        double _t0;
        float _gap = -1f;
        bool _collidersOn = true;
        bool _lightsOn = true;

        /// <summary>Current gap between the leaves (m), 0 closed .. 2.0 open.</summary>
        public float Gap => Mathf.Max(_gap, 0f);
        public bool Triggered => _triggered;
        public bool CollidersOn => _collidersOn;

        /// <summary>Gap as a function of the time since the trigger (s).</summary>
        public static float GapAt(float tSinceTrigger)
            => tSinceTrigger <= 0f ? 0f : RingConstants.HatchW * RingMath.Smootherstep01(tSinceTrigger / RingConstants.HatchOpenTime);

        /// <summary>Capture-mode gap as a function of progress s (1.5 m/s after the trigger).</summary>
        public static float GapAtProgress(float s) => GapAt((s - RingConstants.HatchTriggerS) / RingConstants.Walk);

        /// <summary>Walk time since the trigger at progress s (s, &gt;= 0; 1.5 m/s).</summary>
        static double SinceTrigger(float s) => Mathf.Max(0f, (s - RingConstants.HatchTriggerS) / RingConstants.Walk);

        public void Tick(float s, double t, float dt)
        {
            float g;
            if (RingState.Capture && !RingState.SequenceMode)
            {
                g = GapAtProgress(s);
                // keep the time branch in step, so a following SequenceMode run continues from this gap
                _triggered = s >= RingConstants.HatchTriggerS;
                _t0 = t - SinceTrigger(s);
            }
            else
            {
                if (!_triggered && s >= RingConstants.HatchTriggerS)
                {
                    _triggered = true;
                    _t0 = t - SinceTrigger(s);   // back-dated by the walk time past the trigger (continuous with capture)
                }
                else if (_triggered && s < RingConstants.HatchTriggerS - 1f && RingState.Capture)
                {
                    _triggered = false;   // sequence restarted before the trigger
                }
                g = _triggered ? GapAt((float)(t - _t0)) : 0f;
            }
            Apply(g);

            bool lightsOn = s < LightsOffS;
            if (lightsOn != _lightsOn && Lights != null)
            {
                _lightsOn = lightsOn;
                for (int i = 0; i < Lights.Length; i++) if (Lights[i] != null) Lights[i].enabled = lightsOn;
            }
        }

        /// <summary>Places the leaves for a gap (used by Tick and by the builder for the closed state).</summary>
        public void Apply(float gap)
        {
            if (Mathf.Abs(gap - _gap) < 1e-5f) return;
            _gap = gap;
            float half = 0.5f * (RingConstants.HatchW * 0.5f + LeafOverlap);   // half leaf mesh width (0.55)
            float x = 0.5f * gap + half;
            if (LeafRight != null) LeafRight.localPosition = new Vector3(x, LeafRight.localPosition.y, LeafRight.localPosition.z);
            if (LeafLeft != null) LeafLeft.localPosition = new Vector3(-x, LeafLeft.localPosition.y, LeafLeft.localPosition.z);
            bool on = gap < ColliderOffGap;
            if (on != _collidersOn)
            {
                _collidersOn = on;
                if (ColliderLeft != null) ColliderLeft.enabled = on;
                if (ColliderRight != null) ColliderRight.enabled = on;
            }
        }
    }
}
