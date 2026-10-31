using UnityEngine;

namespace Megastructure
{
    /// <summary>Holds the player still with an absolute look (captures: no human input can move the player or the view).</summary>
    public sealed class QaHoldDriver : IPlayerDriver
    {
        public bool Active = true;
        public float Yaw, Pitch;

        public bool WantsControl => Active;

        public void Drive(ref PlayerInputFrame input, float dt)
        {
            input.Clear();
            input.AbsoluteLook = true;
            input.Yaw = Yaw;
            input.Pitch = Mathf.Clamp(Pitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
        }
    }

    /// <summary>
    /// Deterministic path follower for the sequences the AutoWalker does not cover (brisk 2.5 m/s transition walks, the
    /// look-back walks of the deactivation test): a planned point advances along the walked curve (PathGuide.WalkS) with a
    /// 1 s smoothstep start ramp; the player is steered with the AutoWalker's feed-forward plus bounded correction through
    /// the real CharacterController. Gaze: the GazeCues tables of the beat (critically damped, omega 2.5 rad/s, no sway), or
    /// straight back along the path. Stops at the lift centre before the ride and at <see cref="EndD"/>.
    /// </summary>
    public sealed class QaPathDriver : IPlayerDriver
    {
        public bool Running;
        public bool Finished;
        public float Speed = AbyssConst.V_BRISK;
        public bool LookBack;
        public bool AfterRide;
        public float EndD = AbyssConst.POLYLINE_LENGTH - 2f;

        public float PlannedD => _d;

        const float RampTime = 1f, Omega = 2.5f, Gain = 1.5f, MaxCorrection = 0.6f;
        float _s, _d, _t;
        float _yaw, _pitch, _yawVel, _pitchVel;

        public bool WantsControl => Running;

        public void Begin(PlayerController pc, float speed, bool lookBack, float endD)
        {
            Speed = speed;
            LookBack = lookBack;
            AfterRide = pc.AfterRide;
            _d = pc.PathD;
            _s = PathGuide.WalkS(_d);
            float liftStop = AbyssConst.D_LIFT - 0.05f;
            EndD = (!AfterRide && endD > liftStop) ? liftStop : endD;
            _t = 0f;
            _yaw = pc.Yaw; _pitch = pc.Pitch; _yawVel = 0f; _pitchVel = 0f;
            Finished = false;
            Running = true;
            pc.Driver = this;
        }

        public void Drive(ref PlayerInputFrame input, float dt)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            _t += dt;
            float v = Finished ? 0f : Speed * GazeCues.SmoothStep01(_t / RampTime);
            _s += v * dt;
            _d = PathGuide.WalkDAtS(_s);
            if (_d >= EndD) { _d = EndD; _s = PathGuide.WalkS(EndD); Finished = true; v = 0f; }

            Vector3 target = PathGuide.WalkPoint(_d, AfterRide);
            Vector3 tan = PathGuide.WalkTangent(_d, AfterRide);
            Vector3 f = pc.FeetPosition;
            Vector2 err = new Vector2(target.x - f.x, target.z - f.z);
            float m = err.magnitude;
            Vector2 corr = Vector2.zero;
            if (m > 0.005f)
            {
                corr = err * Gain;
                float cm = m * Gain;
                if (cm > MaxCorrection) corr *= MaxCorrection / cm;
            }
            Vector2 desired = new Vector2(tan.x, tan.z) * v + corr;
            float sp = desired.magnitude;
            input.Clear();
            if (sp > 1e-4f) { input.Move = desired / sp; input.SpeedOverride = sp; }
            input.MoveIsWorld = true;

            float ty, tp;
            float heading = PathGuide.HeadingYaw(_d, AfterRide);
            if (LookBack) { ty = heading + 180f; tp = 0f; }
            else
            {
                int beat = PathGuide.BeatAt(_d);
                var ctx = new GazeCues.Context { Eye = pc.EyePosition, HeadingYaw = heading, SceneTime = SceneClock.Time };
                if (beat == 7) GazeCues.Evaluate(GazeCues.Beat7, GazeCues.Beat7Clock(_d, false, 0f), in ctx, out ty, out tp);
                else GazeCues.Evaluate(GazeCues.ForBeat(beat), GazeCues.BeatTime(beat, _d), in ctx, out ty, out tp);
            }
            tp = Mathf.Clamp(tp, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
            float gy = _yaw + Mathf.DeltaAngle(_yaw, ty);
            CritDamp(ref _yaw, ref _yawVel, gy, dt);
            CritDamp(ref _pitch, ref _pitchVel, tp, dt);
            if (_yaw >= 360f || _yaw < 0f) _yaw = Mathf.Repeat(_yaw, 360f);
            input.AbsoluteLook = true;
            input.Yaw = _yaw;
            input.Pitch = Mathf.Clamp(_pitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
        }

        static void CritDamp(ref float x, ref float vel, float target, float dt)
        {
            float delta = x - target;
            float j = (vel + Omega * delta) * dt;
            float e = Mathf.Exp(-Omega * dt);
            x = target + (delta + j) * e;
            vel = (vel - Omega * j) * e;
        }

        public void Stop()
        {
            Running = false;
            var pc = PlayerController.Instance;
            if (pc != null && ReferenceEquals(pc.Driver, this)) pc.Driver = null;
        }
    }

    /// <summary>
    /// Wall-hugging bot (Part 10 item 9, 11.8, 14.10 functional): walks along one edge of a walkable region pushing outward at
    /// 45 degrees into the invisible edge wall, so the CharacterController slides along it at speed x cos 45. Path runs follow
    /// the walked curve's tangent at the player's d (right = cross(up, tangent), side +1 right / -1 left); segment runs use a
    /// fixed direction and outward normal (balcony front edges).
    ///
    /// Narrowings: path runs look ahead along the walkable half-width profile (<see cref="PathGuide.HalfWidth"/>). When a step
    /// to a narrower half-width that the run must pass (before <see cref="EndD"/>) lies within <see cref="PursuitDistance"/> +
    /// <see cref="NarrowLead"/> metres of d ahead, the push is replaced by a pure-pursuit steer toward the new edge (the narrower
    /// capsule-centre limit minus <see cref="NarrowInset"/>): lateral command (target - lateral) / PursuitDistance, clamped to
    /// 45 degrees either way. Far outside the target that is 45 degrees inward, so a bot that meets the transverse wall of the
    /// wide region (hall wall, balcony front wall) presses into it and slides inward along it to the narrow entrance, then
    /// presses outward again once past the step. Everywhere else the push is the plain 45 degrees outward.
    /// <see cref="InwardTravel"/> lets the runner count that slide as progress for its stall test; a bot that really cannot
    /// pass still stalls. <see cref="Flip"/> turns the push inward for the stall recovery at door frames (the runner sets it).
    /// Allocation-free per frame.
    /// </summary>
    public sealed class QaWallHugDriver : IPlayerDriver
    {
        /// <summary>Pure-pursuit distance (metres of d) of the narrowing steer.</summary>
        public const float PursuitDistance = 0.75f;
        /// <summary>
        /// The narrower target applies from this far before the HalfWidth step (plus <see cref="PursuitDistance"/>): edge walls may
        /// stand inside the visible edge (balcony front wall 0.6 m before the pier step), so with the capsule radius the transverse
        /// wall can be met up to about 1 m before the step.
        /// </summary>
        public const float NarrowLead = 1.0f;
        /// <summary>The steer aims this far inside the narrower capsule-centre limit (HalfWidth - CAPSULE_R) to clear the entrance corner.</summary>
        public const float NarrowInset = 0.10f;
        /// <summary>A HalfWidth step is a narrowing when the half-width drops by more than this.</summary>
        public const float NarrowStep = 0.05f;

        public bool Running;
        public float Speed = AbyssConst.V_WALK;
        public int Side = 1;
        public bool AfterRide;
        public bool Segment;
        public Vector3 SegDir = Vector3.forward, SegOut = Vector3.right;
        public bool Flip;
        /// <summary>Path runs: the d the run must reach. Only narrowings before it are steered for, so a region run keeps hugging its edge up to its end.</summary>
        public float EndD = AbyssConst.POLYLINE_LENGTH;

        /// <summary>d of the HalfWidth step steered for in the last frame, or -1 (plain outward push).</summary>
        public float NarrowD { get; private set; } = -1f;
        /// <summary>Lateral target of the narrowing steer in the last frame (capsule centre, metres from the centreline toward <see cref="Side"/>).</summary>
        public float TargetLateral { get; private set; }
        /// <summary>Path runs: feet offset from the polyline centreline toward <see cref="Side"/> at the start of the last frame.</summary>
        public float Lateral { get; private set; }
        /// <summary>Lateral metres travelled toward the narrowing target while outside it, accumulated over the run.</summary>
        public float InwardTravel { get; private set; }

        float _prevLateral;
        bool _prevSteer;

        public bool WantsControl => Running;

        /// <summary>Clears the per-run state (call before each run).</summary>
        public void ResetRun()
        {
            Flip = false;
            NarrowD = -1f;
            TargetLateral = 0f;
            Lateral = 0f;
            InwardTravel = 0f;
            _prevLateral = 0f;
            _prevSteer = false;
        }

        /// <summary>
        /// The HalfWidth narrowing the bot at d must steer for: the step in (d, d + PursuitDistance + NarrowLead] and before endD
        /// with the smallest target (narrower capsule-centre limit minus NarrowInset, at least 0). Returns its d, or -1 when none.
        /// </summary>
        public static float NarrowingAhead(float d, float endD, out float target)
        {
            var WD = AbyssConst.WD;
            float reach = d + PursuitDistance + NarrowLead;
            float found = -1f;
            target = 0f;
            for (int w = 1; w < WD.Length - 1; w++)
            {
                float b = WD[w];
                if (b <= d) continue;
                if (b > reach || b >= endD) break;   // WD is non-decreasing
                float before = PathGuide.HalfWidth(b - 0.01f), after = PathGuide.HalfWidth(b + 0.01f);
                if (after >= before - NarrowStep) continue;
                float tl = Mathf.Max(0f, after - AbyssConst.CAPSULE_R - NarrowInset);
                if (found < 0f || tl < target) { target = tl; found = b; }
            }
            return found;
        }

        public void Drive(ref PlayerInputFrame input, float dt)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            Vector3 t, outward;
            float push = 1f;   // outward component per unit of forward: +1 = 45 degrees into the edge wall, -1 = 45 degrees inward
            if (Segment)
            {
                t = SegDir; outward = SegOut;
                NarrowD = -1f;
                _prevSteer = false;
            }
            else
            {
                float d = pc.PathD;
                t = PathGuide.WalkTangent(d, AfterRide);
                outward = new Vector3(t.z, 0f, -t.x) * Side;

                // lateral offset against the polyline (the reference of HalfWidth and of the runner's limit check)
                Vector3 c = PathGuide.FloorPoint(d, AfterRide);
                Vector3 pt = PathGuide.Tangent(d, AfterRide);
                Vector3 f = pc.FeetPosition;
                float y = ((f.x - c.x) * pt.z - (f.z - c.z) * pt.x) * Side;
                Lateral = y;

                float nd = NarrowingAhead(d, EndD, out float target);
                NarrowD = nd;
                bool steer = nd >= 0f;
                if (steer)
                {
                    TargetLateral = target;
                    push = Mathf.Clamp((target - y) / PursuitDistance, -1f, 1f);
                    if (_prevSteer && _prevLateral > target) InwardTravel += Mathf.Max(0f, _prevLateral - Mathf.Max(y, target));
                }
                _prevSteer = steer;
                _prevLateral = y;
            }
            if (Flip) push = -1f;
            Vector3 dir = t + outward * push;
            dir.y = 0f;
            float m = dir.magnitude;
            input.Clear();
            if (m > 1e-5f)
            {
                dir /= m;
                input.Move = new Vector2(dir.x, dir.z);
                input.SpeedOverride = Speed;
            }
            input.MoveIsWorld = true;
            input.AbsoluteLook = true;
            input.Yaw = Mathf.Repeat(Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg, 360f);
            input.Pitch = 0f;
        }
    }
}
