using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Per-frame functional monitors for the walking QA modes (autotest, soak, perf), ticked in the harness LateUpdate
    /// after CameraRig (+100), so the camera is final for the frame. Allocation-free:
    /// camera inside a corridor collider (Physics.CheckSphere r 0.05 on layer Corridor, Part 10 item 8), camera roll (item 18),
    /// beat changes and backward beat steps, lift departure / arrival on the scene clock and the 14.7.1 camera offset error
    /// at arrival, safety returns.
    /// </summary>
    public sealed class QaMonitor
    {
        public const float CameraProbeRadius = 0.05f;
        const int CorridorMask = 1 << CorridorKit.LayerCorridor;

        public int Frames;
        public int CameraInsideFrames;
        public Vector3 FirstInsidePos;
        public float FirstInsideD = -1f;
        public double FirstInsideTime = -1;
        public float MaxAbsRollDeg;
        public int BeatChanges, BeatDecreases;
        public double LiftDwell = -1, LiftStart = -1, LiftEnd = -1;
        public float LiftMaxOffsetErrorM = -1f;
        public int LiftOffsetSamples;
        public int LiftRides;
        public int MaxSafetyReturns;
        public double FirstSafetyReturnTime = -1;

        int _lastBeat = -1;
        LiftPhase _lastLift = LiftPhase.DockedTop;
        bool _haveLift;

        public void LateTick()
        {
            if (!GameBootstrap.Loaded) return;
            Frames++;

            Vector3 cam = GameState.CameraPos;
            if (Physics.CheckSphere(cam, CameraProbeRadius, CorridorMask, QueryTriggerInteraction.Ignore))
            {
                if (CameraInsideFrames == 0)
                {
                    FirstInsidePos = cam;
                    FirstInsideD = GameState.D;
                    FirstInsideTime = SceneClock.Time;
                }
                CameraInsideFrames++;
            }

            Vector3 right = GameState.CameraRot * Vector3.right;
            float roll = Mathf.Abs(Mathf.Asin(Mathf.Clamp(right.y, -1f, 1f)) * Mathf.Rad2Deg);
            if (roll > MaxAbsRollDeg) MaxAbsRollDeg = roll;

            int beat = GameState.Beat;
            if (_lastBeat >= 0 && beat != _lastBeat)
            {
                BeatChanges++;
                if (beat < _lastBeat) BeatDecreases++;
            }
            _lastBeat = beat;

            LiftPhase lp = GameState.Lift;
            if (_haveLift && lp != _lastLift)
            {
                if (lp == LiftPhase.Dwell) LiftDwell = SceneClock.Time;
                else if (lp == LiftPhase.Riding && _lastLift != LiftPhase.Riding) { LiftStart = SceneClock.Time; LiftRides++; }
                else if (lp == LiftPhase.Arrived && _lastLift == LiftPhase.Riding)
                {
                    LiftEnd = SceneClock.Time;
                    var lift = LiftController.Instance;
                    if (lift != null)
                    {
                        if (lift.MaxOffsetError > LiftMaxOffsetErrorM) LiftMaxOffsetErrorM = lift.MaxOffsetError;
                        LiftOffsetSamples += lift.OffsetSamples;
                    }
                }
            }
            _lastLift = lp;
            _haveLift = true;

            var pc = PlayerController.Instance;
            if (pc != null && pc.SafetyReturns > MaxSafetyReturns)
            {
                if (MaxSafetyReturns == 0) FirstSafetyReturnTime = SceneClock.Time;
                MaxSafetyReturns = pc.SafetyReturns;
            }
        }

        public void Write(QaJson j)
        {
            j.Obj("camera_inside_collider");
            j.Prop("frames", CameraInsideFrames);
            j.Prop("probe_radius", CameraProbeRadius);
            if (CameraInsideFrames > 0)
            {
                j.Prop("first_pos", FirstInsidePos);
                j.Prop("first_d", FirstInsideD);
                j.Prop("first_time", FirstInsideTime);
            }
            j.Prop("pass", CameraInsideFrames == 0);
            j.EndObject();
            j.Prop("camera_roll_max_deg", MaxAbsRollDeg);
            j.Prop("monitored_frames", Frames);
            j.Prop("beat_changes", BeatChanges);
            j.Prop("beat_decreases", BeatDecreases);
            j.Prop("safety_returns", MaxSafetyReturns);
            if (MaxSafetyReturns > 0) j.Prop("first_safety_return_time", FirstSafetyReturnTime);
        }
    }
}
