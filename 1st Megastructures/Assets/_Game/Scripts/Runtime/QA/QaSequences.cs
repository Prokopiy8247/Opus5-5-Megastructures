using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Kinds of QA frame sequences (Part 11.6, 14.10).</summary>
    public enum SeqKind
    {
        WalkAuto = 0,     // AutoWalker (1.5 m/s, GazeCues), player camera
        WalkDriver = 1,   // QaPathDriver at a set speed (brisk) or looking back, player camera
        Ride = 2,         // the whole ride: AutoWalker boards, recording from departure (ride seconds 0..120)
        RideFrom = 3,     // player aboard, ride set to RideT0, AutoWalker on the lift (and off through the gate)
        StaticPose = 4,   // capture pose fixed, clock running (or frozen), lift frozen
        LiftPose = 5,     // capture pose fixed relative to the moving platform
        PlayerStand = 6,  // player standing with a fixed look, player camera (head motion, hall vibration)
        Steps = 7,        // capture pose stepped sideways by StepM per frame, clock frozen (thin-line test)
        MovingPose = 8,   // capture pose moving at Velocity through the key pose (centred), clock running
        WalkPinned = 9,   // the player walks (QaPathDriver at Speed, so d-triggered events fire) while the camera holds a fixed pose
    }

    /// <summary>One named sequence with its defaults (length and fps overridable with -seq-seconds / -seq-fps).</summary>
    public sealed class SeqDef
    {
        public string Name, Group, Note, Judge;
        public SeqKind Kind;
        public float Seconds = 25f;
        public int Fps = 30;
        public float D0;
        public float Speed = AbyssConst.V_WALK;
        public bool LookBack;
        public int Key = -1;
        public float RideT0 = -1f;
        public bool ClockFrozen, LiftFrozen = true, CentreEye;
        public double Clock = double.NaN;
        public Vector3 Velocity;
        public float StepM;
        public float StandD, StandYaw, StandPitch;
        /// <summary>WalkPinned: the fixed camera pose (eye, yaw, pitch, vertical FOV).</summary>
        public Vector3 PinEye;
        public float PinYaw, PinPitch, PinFov = 66f;
        /// <summary>Capture the composed window (post, final dither and overlay UI) instead of the RenderTexture.</summary>
        public bool Window;
        /// <summary>Write the per-frame clear aperture of the gallery exit doorway to frames.csv (region_* columns, R1-07).</summary>
        public bool ApertureRegion;
        /// <summary>Render and write every Nth simulated frame only (cues: the walk keeps a small time step, the PNG count stays low).</summary>
        public int RecordEvery = 1;
        /// <summary>Reference event of a cue sequence (frames.csv cue_s = seconds since it; sequence.json cue_frames at CueMarks).</summary>
        public CueRef Cue = CueRef.None;
        /// <summary>Seconds after the cue reference whose nearest written frames sequence.json lists (R2-05, R2-09, R2-20).</summary>
        public double[] CueMarks;
        /// <summary>Count warm pixels ((R - B) &gt; 0.08 and R &gt; 0.12) below this row (fraction from the top) per frame; -1 = off.</summary>
        public float WarmFromRow = -1f;
    }

    /// <summary>Reference event of a cue sequence.</summary>
    public enum CueRef
    {
        None = 0,
        Bolt = 1,       // the falling bolt's release: d first reaching QaSequenceCatalog.BoltReleaseD by walking (scene clock)
        Ignition = 2,   // the honeycomb ignition: _AbLampIgnite.x set by beat 7 (scene clock)
        Goal = 3,       // the ending goal: GameState.GoalRealTime (real clock, as the ending timeline)
    }

    /// <summary>The catalogue of named sequences (docs/QA_RUNTIME.md lists them with the matching Python commands).</summary>
    public static class QaSequenceCatalog
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>First piston dead centre (any bank) at or after t: theta_IL = 60 + 3 (t - PORTAL_T) at a multiple of 45 degrees.</summary>
        public static double DeadCentreAfter(double t)
        {
            double t0 = AbyssConst.PORTAL_T + 10.0;
            double k = Math.Ceiling((t - t0) / AbyssConst.DEAD_CENTRE_INTERVAL);
            return t0 + k * AbyssConst.DEAD_CENTRE_INTERVAL;
        }

        static SeqDef Transition(string name, float centre, bool brisk, string note)
        {
            float v = brisk ? AbyssConst.V_BRISK : AbyssConst.V_WALK;
            return new SeqDef
            {
                Name = name + (brisk ? "_brisk" : "_walk"), Group = "transitions", Kind = brisk ? SeqKind.WalkDriver : SeqKind.WalkAuto,
                Seconds = 25f, Fps = 30, D0 = centre - v * 12.5f, Speed = v, LiftFrozen = false, Note = note,
                Judge = "--judge spikes,luma_step,ev_rate",
            };
        }

        public static List<SeqDef> All()
        {
            var l = new List<SeqDef>(48);
            // falling bolt first: it drops once per run when d first passes its release point, and the t12_* walks pass it.
            // R2-09 / lead decision D3: released from a cold lamp on the right-hand wall belt (about 300 m off the bridge axis)
            // at beat-2 time 15 s, during the gaze "pan right to the wall"; recorded from the AutoWalker's own pose and gaze
            // (the cinematic look; beat-2 gaze time is d-based, so the slowdowns do not move it), 10 fps written, fall seconds
            // 0-14 (the fog takes it over fall seconds 8-12); sequence.json cue_frames lists fall seconds 0, 3, 6, 9 and 12
            l.Add(new SeqDef
            {
                Name = "cue_bolt", Group = "cues", Kind = SeqKind.WalkAuto, Seconds = 16f, Fps = 20, RecordEvery = 2,
                D0 = BoltReleaseD - 2f * AbyssConst.V_WALK, Speed = AbyssConst.V_WALK, LiftFrozen = false,
                Cue = CueRef.Bolt, CueMarks = new[] { 0.0, 3.0, 6.0, 9.0, 12.0 },
                Note = "14.5.2 / D3 falling bolt: the AutoWalker walks through beat-2 time 15 s (d " + BoltReleaseD.ToString("0.0", Inv)
                       + ", the release) with its own gaze (pan right to the wall); frames.csv cue_s = fall seconds",
                Judge = "--judge spikes",
            });
            l.Add(Transition("t12", 62f, false, "14.6 1->2: blend d 52-72, doorway"));
            l.Add(Transition("t12", 62f, true, "14.6 1->2 at 2.5 m/s"));
            l.Add(Transition("t23", 215f, false, "14.6 2->3: d 205-225"));
            l.Add(Transition("t23", 215f, true, "14.6 2->3 at 2.5 m/s"));
            l.Add(Transition("t34", 297.5f, false, "14.6 3->4: hall blend d 285-310"));
            l.Add(Transition("t34", 297.5f, true, "14.6 3->4 at 2.5 m/s"));
            l.Add(Transition("t45", 432f, false, "14.6 4->5: hall exit d 420-440"));
            l.Add(Transition("t45", 432f, true, "14.6 4->5 at 2.5 m/s (stops at the deck centre)"));
            l.Add(new SeqDef
            {
                // from ride second 96 (no later than WalkPlanner.RAIL_PLAN_LATEST_BEGIN): the walker boards at the -Z rail stand
                // with the ride gaze of that second (the look down, D1), walks back to the centre at 109.5-120.5 as in a real run
                Name = "t56_ride", Group = "transitions", Kind = SeqKind.RideFrom, Seconds = 29f, Fps = 30, RideT0 = 96f, LiftFrozen = false,
                Note = "14.6 5->6: ride seconds 96-125 from the walker's rail stand: the look down, the walk back, ride seconds 110-120, arrival, gate at 121, walking off",
                Judge = "--judge spikes,luma_step,ev_rate",
            });
            l.Add(Transition("t67", 654f, false, "14.6 6->7: Face/lattice/sky at 640, sky blend 640-668.54"));
            l.Add(Transition("t67", 654f, true, "14.6 6->7 at 2.5 m/s"));
            // R1-07: the gallery-to-bridge exposure through the doorway, d 50-75, judged on the per-frame doorway aperture
            // (the whole frame once the eye passes the doorway): at most 0.35 EV/s (14.6) and 0.02 luma per 30 fps frame
            for (int b = 0; b < 2; b++)
            {
                bool brisk = b == 1;
                float v = brisk ? AbyssConst.V_BRISK : AbyssConst.V_WALK;
                l.Add(new SeqDef
                {
                    Name = brisk ? "t12_ev_brisk" : "t12_ev_walk", Group = "transitions", Kind = brisk ? SeqKind.WalkDriver : SeqKind.WalkAuto,
                    Seconds = (T12_EV_D1 - T12_EV_D0) / v + 0.5f, Fps = 30, D0 = T12_EV_D0, Speed = v, LiftFrozen = false, ApertureRegion = true,
                    Note = "R1-07 gallery to bridge, d 50-75 at " + v.ToString("0.0", Inv) + " m/s: doorway-aperture luma <= 0.35 EV/s and <= 0.02 per 30 fps frame",
                    Judge = "--judge spikes,luma_step,ev_rate --luma-region csv",
                });
            }
            l.Add(new SeqDef
            {
                Name = "ride", Group = "ride", Kind = SeqKind.Ride, Seconds = AbyssConst.LIFT_DURATION, Fps = 30, D0 = 447f, LiftFrozen = false,
                Note = "the whole ride, rideU 0..1 (frames.csv: lift_offset_mm <= 0.5)", Judge = "--judge spikes,static --static deck=0.30,0.88,0.70,1.0",
            });
            l.Add(new SeqDef
            {
                Name = "tunnel_mouth", Group = "transitions", Kind = SeqKind.WalkAuto, Seconds = 30f, Fps = 30, D0 = PathGuide.Beat7D(40f),
                Note = "z 40 -> -5 through the mouth: luma step <= 0.02 / frame, 0.35 EV/s, 4 sigma", Judge = "--judge spikes,luma_step,ev_rate",
            });
            // the piston impulse needs the eye inside the hall: the 14.10 KS4 spot (u 28), not the arbitrated KS4 key pose (u -2)
            float pistonD = PathGuide.Project(KeyShots.KS4_SPEC_EYE - new Vector3(0f, AbyssConst.EYE, 0f), -1f);
            l.Add(new SeqDef
            {
                Name = "pistons", Group = "machines", Kind = SeqKind.PlayerStand, Seconds = 20f, Fps = 30, StandD = pistonD,
                StandYaw = 270f, StandPitch = 4f, Clock = DeadCentreAfter(AbyssConst.NominalTimeAtD(pistonD, false)) - 2.0,
                Note = "dead centres every 15 s; frames.csv head_dy_mm = camera impulse (6 +/- 1 mm)", Judge = "--judge spikes",
            });
            l.Add(new SeqDef
            {
                Name = "fan_frozen_platform", Group = "machines", Kind = SeqKind.StaticPose, Key = 7, Seconds = 20f, Fps = 30,
                Note = "KS5b pose, lift frozen at ride 75 s, fan turning", Judge = "--judge spikes",
            });
            l.Add(new SeqDef
            {
                Name = "fan_frozen_clock", Group = "machines", Kind = SeqKind.LiftPose, Key = 7, Seconds = 20f, Fps = 30, RideT0 = 65f,
                ClockFrozen = true, LiftFrozen = false, Note = "KS5b offset on the moving platform (ride 65-85 s), scene clock frozen",
                Judge = "--judge spikes",
            });
            for (int b = 1; b <= 7; b++)
            {
                l.Add(new SeqDef
                {
                    Name = "static_b" + b.ToString(Inv), Group = "static", Kind = SeqKind.StaticPose, Key = KeyShots.BeatReference[b - 1],
                    Seconds = 100f, Fps = 30, Note = "100 s static camera at " + KeyShots.All[KeyShots.BeatReference[b - 1]].Tag + ", clock running",
                    Judge = "--judge spikes",
                });
            }
            for (int b = 1; b <= 7; b++)
            {
                l.Add(new SeqDef
                {
                    Name = "thin_b" + b.ToString(Inv), Group = "thin", Kind = SeqKind.Steps, Key = KeyShots.BeatReference[b - 1],
                    Seconds = 1f, Fps = 30, StepM = 0.02f, ClockFrozen = true,
                    Note = "30 frames, camera stepped 0.02 m to the right per frame, clock frozen (thin-line std <= 6/255)",
                    Judge = "--judge std --std-limit 6 --std thin=<x0,y0,x1,y1>",
                });
            }
            l.Add(new SeqDef
            {
                Name = "grad_KS2a", Group = "grad", Kind = SeqKind.MovingPose, Key = 2, Seconds = 10f, Fps = 60,
                Velocity = AbyssConst.Dir(KeyShots.All[2].Yaw, 0f) * AbyssConst.V_WALK,
                Note = "encode test: walking through KS2a at 1.5 m/s", Judge = "encode_test.py --fps 60 --beat 2",
            });
            l.Add(new SeqDef
            {
                Name = "grad_KS5a", Group = "grad", Kind = SeqKind.LiftPose, Key = 6, Seconds = 10f, Fps = 60, RideT0 = 55f, LiftFrozen = false,
                Note = "encode test: riding through KS5a (ride 55-65 s)", Judge = "encode_test.py --fps 60 --beat 5",
            });
            l.Add(new SeqDef
            {
                Name = "grad_KS7b", Group = "grad", Kind = SeqKind.MovingPose, Key = 11, Seconds = 10f, Fps = 60,
                Velocity = AbyssConst.Dir(KeyShots.All[11].Yaw, 0f) * WalkPlanner.BEAT7_SLOW_SPEED,
                Note = "encode test: walking through KS7b at 1.1 m/s", Judge = "encode_test.py --fps 60 --beat 7 --zone outside",
            });
            l.Add(new SeqDef
            {
                Name = "lookback_d90", Group = "lookback", Kind = SeqKind.WalkDriver, Seconds = 20f, Fps = 30, D0 = 75f, LookBack = true,
                LiftFrozen = false, Note = "14.10 d: walking away from the gallery deactivation at d 90, looking back", Judge = "--judge spikes",
            });
            l.Add(new SeqDef
            {
                Name = "lookback_d380", Group = "lookback", Kind = SeqKind.WalkDriver, Seconds = 20f, Fps = 30, D0 = 365f, LookBack = true,
                LiftFrozen = false, Note = "14.10 d: walking away from the d 380 deactivation, looking back", Judge = "--judge spikes",
            });
            l.Add(new SeqDef
            {
                Name = "beam_sweep", Group = "machines", Kind = SeqKind.LiftPose, Key = 7, CentreEye = true, Seconds = 50f, Fps = 30, RideT0 = 50f,
                LiftFrozen = false, Note = "14.10 e: the deck through the beam wedges, ride 50-100 s, looking at the fan", Judge = "--judge spikes",
            });
            // R1-40: specified visible cues that a still cannot show (group "cues"; the stills are -shots cues)
            KeyShot k7b = KeyShots.All[11];
            // R2-20: 10 fps written from the KS7b pose; cue_frames at ignition +0, +0.5, +1.5, +3, +4.5, +6, +12 s and the door
            // lamp ease at +12, +13.5, +14.5 s; warm pixels of the lower 66 % per frame and the on-screen sweep time
            l.Add(new SeqDef
            {
                Name = "cue_ignition", Group = "cues", Kind = SeqKind.WalkPinned, Seconds = 26f, Fps = 20, RecordEvery = 2, D0 = PathGuide.Beat7D(-2f),
                Speed = AbyssConst.V_WALK, LiftFrozen = false, PinEye = k7b.Eye, PinYaw = k7b.Yaw, PinPitch = k7b.Pitch, PinFov = k7b.Fov,
                Cue = CueRef.Ignition, CueMarks = new[] { 0.0, 0.5, 1.5, 3.0, 4.5, 6.0, 12.0, 13.5, 14.5 }, WarmFromRow = 0.34f,
                Note = "14.5.7 / R1-24 / R2-20 honeycomb lamp ignition seen in the KS7b frame while the player walks onto the balcony front; "
                       + "frames.csv cue_s = seconds since the ignition, warm_px = warm pixels of the lower 66 %",
                Judge = "--judge spikes",
            });
            // R2-05 / lead decision D2: the door trigger at AbyssConst.DOOR_TRIGGER_Z; 10 fps window captures (post, dither and the
            // end line) from z -62 until the end line; cue_frames at goal +0.5, +1, +3, +7 s; judged: warm pixels of the lower
            // half at goal +3 s at least 0.1 % (the amber sliver on the pier)
            l.Add(new SeqDef
            {
                Name = "cue_ending", Group = "cues", Kind = SeqKind.WalkAuto, Seconds = 24f, Fps = 20, RecordEvery = 2, D0 = PathGuide.Beat7D(-62f),
                LiftFrozen = false, Window = true, Cue = CueRef.Goal, CueMarks = new[] { 0.5, 1.0, 3.0, 7.0 }, WarmFromRow = 0.5f,
                Note = "14.11 ending: door reached (trigger z " + AbyssConst.DOOR_TRIGGER_Z.ToString("0.0", Inv) + "), leaf opening 12 degrees "
                       + "with the amber sliver, then the end line; window captures (UI included); cue_frames at goal +0.5, +1, +3 and +7 s",
                Judge = "--judge spikes",
            });
            return l;
        }

        /// <summary>R2-09 / D3: the bolt is released at beat-2 time 15 s; beat-2 gaze time is (d - BEAT_START_D[1]) / 1.5 m/s.</summary>
        public const float BoltBeat2Time = 15f;
        public static float BoltReleaseD => AbyssConst.BEAT_START_D[1] + BoltBeat2Time * AbyssConst.V_WALK;
        /// <summary>R2-05: warm pixels of the lower half at goal +3 s, at least this % of the frame (the amber sliver on the pier).</summary>
        public const double EndingWarmMinPct = 0.1;
        public const double EndingWarmMark = 3.0;
        /// <summary>R2-20: the on-screen ignition sweep (warm pixels from 2 % to 95 % of their final count) lasts at least this long.</summary>
        public const double IgnitionMinSweepSeconds = 6.0;
        /// <summary>R2-01: the t12_ev aperture region is sampled only before this d (the region grows to the full frame over d 59.3-59.8).</summary>
        public const float T12_EV_REGION_STOP_D = 59.3f;

        /// <summary>Gallery-to-bridge EV sequence range (R1-07).</summary>
        public const float T12_EV_D0 = 50f, T12_EV_D1 = 75f;

        /// <summary>Select by name, group (transitions, ride, machines, static, thin, grad, lookback) or "all"; comma-separated.</summary>
        public static List<SeqDef> Select(string list)
        {
            List<SeqDef> all = All();
            var outList = new List<SeqDef>();
            string[] tokens = (list ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < all.Count; i++)
            {
                for (int t = 0; t < tokens.Length; t++)
                {
                    string tok = tokens[t].Trim();
                    if (tok.Equals("all", StringComparison.OrdinalIgnoreCase) || tok.Equals(all[i].Name, StringComparison.OrdinalIgnoreCase)
                        || tok.Equals(all[i].Group, StringComparison.OrdinalIgnoreCase))
                    {
                        outList.Add(all[i]);
                        break;
                    }
                }
            }
            return outList;
        }
    }

    public sealed partial class QaCaptureRunner
    {
        readonly QaPathDriver _pathDriver = new QaPathDriver();
        readonly StringBuilder _csv = new StringBuilder(1 << 16);
        static readonly CultureInfo SInv = CultureInfo.InvariantCulture;

        IEnumerator RunSequences(string list)
        {
            List<SeqDef> defs = QaSequenceCatalog.Select(list);
            if (defs.Count == 0) Debug.LogWarning("[ABYSS] QA capture: no sequence matches '" + list + "'");
            bool haveRes = CliArgs.Res(out int w, out int h, KeyShots.W1080, KeyShots.H1080);
            if (!haveRes) { w = KeyShots.W1080; h = KeyShots.H1080; }
            var index = new QaJson();
            index.BeginObject();
            index.Prop("run", Path.GetFileName(_dir.TrimEnd('\\', '/')));
            index.Prop("date", QaPaths.IsoNow());
            index.Prop("build", QaPaths.BuildKind);
            index.Arr("sequences");
            for (int i = 0; i < defs.Count; i++)
            {
                SeqDef def = defs[i];
                float seconds = CliArgs.Float(QaHarness.SeqSecondsFlag, def.Seconds);
                int fps = Mathf.Clamp(CliArgs.Int(QaHarness.SeqFpsFlag, def.Fps), 1, 240);
                int frames = Mathf.Max(2, Mathf.RoundToInt(seconds * fps));
                yield return RunSequence(def, seconds, fps, frames, w, h, index);
                Restore();
                _h.InstallHold(0f, 0f);
            }
            index.EndArray();
            index.EndObject();
            index.Save(Path.Combine(_dir, "sequences.json"));
        }

        IEnumerator RunSequence(SeqDef def, float seconds, int fps, int frames, int w, int h, QaJson index)
        {
            string dir = QaPaths.EnsureDir(Path.Combine(_dir, def.Name));
            foreach (string old in Directory.GetFiles(dir, "frame_*.png")) File.Delete(old);
            Time.captureDeltaTime = 1f / fps;
            var pc = PlayerController.Instance;
            var rig = CameraRig.Instance;
            var lift = LiftController.Instance;
            var walker = AutoWalker.Instance;
            KeyShot key = def.Key >= 0 ? KeyShots.All[def.Key] : default;
            bool keyPose = def.Key >= 0;
            Vector3 liftOffset = keyPose ? (def.CentreEye ? new Vector3(0f, AbyssConst.EYE, 0f) : key.LiftOffset) : Vector3.zero;
            Vector3 right = keyPose ? key.Rotation * Vector3.right : Vector3.right;
            bool moving = def.Kind == SeqKind.MovingPose && !CliArgs.Has(QaHarness.SeqStaticFlag);
            double startClock = double.NaN;
            string setupError = null;
            Debug.Log("[ABYSS] QA sequence " + def.Name + ": " + frames + " frames at " + fps + " fps, " + w + "x" + h + " (" + def.Note + ")");

            // ---------------------------------------------------------------- setup
            switch (def.Kind)
            {
                case SeqKind.WalkAuto:
                case SeqKind.WalkDriver:
                case SeqKind.WalkPinned:
                {
                    bool after = def.D0 > AbyssConst.D_LIFT;
                    const float preroll = 2f;
                    float dTp = Mathf.Max(0f, def.D0 - def.Speed * (preroll - 0.5f));
                    if (rig != null) rig.ClearCapturePose();
                    LiftFor(-1f, after, true);
                    if (lift != null && !after) lift.Frozen = def.LiftFrozen;
                    float yaw = PathGuide.HeadingYaw(dTp, after);
                    _h.InstallHold(yaw, 0f);
                    if (pc != null) pc.TeleportToD(dTp, after);
                    startClock = AbyssConst.NominalTimeAtD(dTp, after);
                    SetClock(startClock, false);
                    PrepareBeats(startClock);
                    Snap();
                    yield return null;
                    yield return null;
                    if (def.Kind == SeqKind.WalkAuto)
                    {
                        if (walker != null) walker.Begin(false); else setupError = "no AutoWalker";
                    }
                    else if (pc != null) _pathDriver.Begin(pc, def.Speed, def.LookBack, def.D0 + def.Speed * (seconds + 5f));
                    yield return QaHarness.WaitSeconds(preroll);
                    // WalkPinned: the camera holds its pose from here on while the player keeps walking (d-triggered events fire)
                    if (def.Kind == SeqKind.WalkPinned && rig != null)
                        rig.SetCapturePose(def.PinEye, AbyssConst.YawPitch(def.PinYaw, def.PinPitch), def.PinFov);
                    break;
                }
                case SeqKind.Ride:
                {
                    if (rig != null) rig.ClearCapturePose();
                    LiftFor(-1f, false, true);
                    if (lift != null) lift.Frozen = false;
                    _h.InstallHold(PathGuide.HeadingYaw(def.D0), 0f);
                    if (pc != null) pc.TeleportToD(def.D0, false);
                    startClock = AbyssConst.NominalTimeAtD(def.D0, false);
                    SetClock(startClock, false);
                    PrepareBeats(startClock);
                    Snap();
                    yield return null;
                    if (walker != null) walker.Begin(false); else setupError = "no AutoWalker";
                    float waited = 0f;
                    while (lift != null && lift.Phase != LiftPhase.Riding && waited < 90f)
                    {
                        yield return null;
                        waited += Time.deltaTime;
                    }
                    if (lift == null || lift.Phase != LiftPhase.Riding) setupError = "the ride did not start within 90 s";
                    break;
                }
                case SeqKind.RideFrom:
                {
                    if (rig != null) rig.ClearCapturePose();
                    startClock = AbyssConst.T_RIDE_START + def.RideT0;
                    SetClock(startClock, false);
                    // board with the cinematic ride gaze of that second and, while the walker's deck plan has it at the -Z rail
                    // (ride 75-109.5, begun no later than RAIL_PLAN_LATEST_BEGIN), at the planner's own deck stand, so the
                    // sequence sees what the player sees (no 94 degree swing from a centre boarding at yaw 270)
                    var pcR = PlayerController.Instance;
                    float boardYaw = 270f, boardPitch = 0f;
                    if (pcR != null)
                    {
                        var gctx = new GazeCues.Context { Eye = pcR.EyePosition, HeadingYaw = 270f, SceneTime = startClock };
                        GazeCues.Evaluate(GazeCues.Beat5Ride, def.RideT0, in gctx, out boardYaw, out boardPitch);
                        boardPitch = Mathf.Clamp(boardPitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT);
                    }
                    // boarded at the top dock already standing where the deck plan has the walker (a teleport on the moving deck
                    // mid-ride left the walker unboarded in qa_r35: it walked off toward the gantry)
                    Vector2 stand = Vector2.zero;
                    if (def.RideT0 <= WalkPlanner.RAIL_PLAN_LATEST_BEGIN) WalkPlanner.DeckStand(def.RideT0, out stand, out _);
                    yield return BoardAt(def.RideT0, boardYaw, boardPitch, stand);
                    if (lift != null) lift.Frozen = false;
                    PrepareBeats(startClock);
                    Snap();
                    if (walker != null) walker.Begin(false); else setupError = "no AutoWalker";
                    yield return null;
                    break;
                }
                case SeqKind.LiftPose:
                {
                    startClock = AbyssConst.T_RIDE_START + def.RideT0;
                    SetClock(startClock, true);
                    if (rig != null) rig.SetCapturePose(FarPoint, key.Rotation, key.Fov);
                    yield return BoardAt(def.RideT0, key.Yaw, 0f);
                    if (lift != null) lift.Frozen = false;
                    PrepareBeats(startClock);
                    if (rig != null && lift != null) rig.SetCapturePose(lift.transform.position + liftOffset, key.Rotation, key.Fov);
                    Snap();
                    for (int i = 0; i < _settle; i++) yield return null;
                    SetClock(SceneClock.Time, def.ClockFrozen);
                    break;
                }
                case SeqKind.PlayerStand:
                {
                    if (rig != null) rig.ClearCapturePose();
                    LiftFor(-1f, def.StandD > AbyssConst.D_LIFT, true);
                    _h.InstallHold(def.StandYaw, def.StandPitch);
                    if (pc != null) pc.Teleport(PathGuide.WalkPoint(def.StandD, def.StandD > AbyssConst.D_LIFT), def.StandYaw, def.StandPitch);
                    startClock = double.IsNaN(def.Clock) ? AbyssConst.NominalTimeAtD(def.StandD, false) : def.Clock;
                    SetClock(startClock, true);
                    PrepareBeats(startClock);
                    Snap();
                    for (int i = 0; i < _settle + 2; i++) yield return null;
                    SetClock(startClock, false);
                    break;
                }
                default:   // StaticPose, Steps, MovingPose: a still preparation at the key pose, then the clock as asked
                {
                    var list = new List<ShotSpec>(1);
                    KeyShots.AddKeyVariants(list, def.Key, true, w, h);
                    ShotSpec s = list[0];
                    double clock = s.Clock - (moving ? 0.5 * seconds : 0.0);
                    s.Clock = clock;
                    if (moving) s.Eye = key.Eye - def.Velocity * (0.5f * seconds);
                    yield return Prepare(s);
                    startClock = clock;
                    bool frozen = def.ClockFrozen || def.Kind == SeqKind.Steps;
                    SetClock(clock, frozen);
                    break;
                }
            }

            // ---------------------------------------------------------------- record
            _csv.Length = 0;
            _csv.Append("frame,file,scene_time,real_time,d,e,beat,lift_phase,ride_s,ride_u,cam_x,cam_y,cam_z,yaw,pitch,roll,fov,head_dx_mm,head_dy_mm,head_dz_mm,lift_offset_mm,mean_luma,region_x0,region_y0,region_x1,region_y1,goal_s,cue_s,warm_px,warm_pct\n");
            int written = 0, failed = 0;
            float minD = float.MaxValue, maxD = float.MinValue;
            double minLuma = double.MaxValue, maxLuma = double.MinValue;
            float maxOffsetMm = 0f;
            bool canRecord = setupError == null;
            var goalT = new List<double>(frames);
            var cueT = new List<double>(frames);
            var warmPct = new List<double>(frames);
            var warmPx = new List<long>(frames);
            double cueRef = double.NaN;
            float prevD = float.NaN;
            if (def.Window && (Screen.width != w || Screen.height != h))
                Debug.LogWarning("[ABYSS] QA sequence " + def.Name + ": window capture at " + Screen.width + "x" + Screen.height + " (not " + w + "x" + h + ")");
            for (int k = 0; k < frames && canRecord; k++)
            {
                // pose for frame k (coroutine phase: after this frame's Update, before CameraRig's LateUpdate)
                if (rig != null && keyPose)
                {
                    if (def.Kind == SeqKind.MovingPose && moving)
                        rig.SetCapturePose(key.Eye + def.Velocity * ((float)k / fps - 0.5f * seconds), key.Rotation, key.Fov);
                    else if (def.Kind == SeqKind.Steps)
                        rig.SetCapturePose(key.Eye + right * (def.StepM * k), key.Rotation, key.Fov);
                    else if (def.Kind == SeqKind.LiftPose && lift != null)
                        rig.SetCapturePose(lift.transform.position + liftOffset, key.Rotation, key.Fov);
                }
                UpdateCueRef(def.Cue, ref cueRef, ref prevD);
                int every = Mathf.Max(1, def.RecordEvery);
                if (k % every != 0) { yield return null; continue; }   // simulated, not recorded
                int fk = k / every;
                string file = "frame_" + fk.ToString("00000", SInv) + ".png";
                if (def.Window)
                {
                    // composed window (post, final dither, overlay UI): the frame after this one's Update, at its end
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
                    if (t == null) { failed++; if (failed > 10) { setupError = "window capture failed"; break; } continue; }
                    try
                    {
                        File.WriteAllBytes(Path.Combine(dir, file), t.EncodeToPNG());
                        written++;
                        SnapWindow(t, def.WarmFromRow);
                    }
                    catch (Exception ex) { failed++; setupError = "png write failed: " + ex.Message; }
                    finally { UnityEngine.Object.Destroy(t); }
                    if (setupError != null) break;
                }
                else
                {
                    Request(Req.SeqFrame, w, h);
                    yield return null;
                    if (!_reqDone || !_reqOk)
                    {
                        failed++;
                        if (failed > 10) { setupError = "render failures: " + _reqError; break; }
                        continue;
                    }
                    try { _r.WritePng(Path.Combine(dir, file)); written++; }
                    catch (Exception ex) { failed++; setupError = "png write failed: " + ex.Message; break; }
                    if (def.WarmFromRow >= 0f) _snap.WarmPx = WarmCount(_r.Rgb, w, h, def.WarmFromRow, out _snap.WarmPct);
                    else { _snap.WarmPx = -1; _snap.WarmPct = double.NaN; }
                }
                double tg = GameState.GoalReached && GameState.GoalRealTime >= 0.0 ? SceneClock.RealTime - GameState.GoalRealTime : double.NaN;
                goalT.Add(tg);
                double tc = double.NaN;
                if (!double.IsNaN(cueRef)) tc = (def.Cue == CueRef.Goal ? _snap.RealT : _snap.SceneT) - cueRef;
                cueT.Add(tc);
                warmPct.Add(_snap.WarmPct);
                warmPx.Add(_snap.WarmPx);
                AppendRow(fk, file, def.ApertureRegion, w, h, tg, tc);
                if (_snap.D < minD) minD = _snap.D;
                if (_snap.D > maxD) maxD = _snap.D;
                if (_snap.Luma < minLuma) minLuma = _snap.Luma;
                if (_snap.Luma > maxLuma) maxLuma = _snap.Luma;
                if (_snap.LiftOffsetMm > maxOffsetMm) maxOffsetMm = _snap.LiftOffsetMm;
            }
            if (walker != null && walker.IsRunning) walker.Stop();
            if (_pathDriver.Running) _pathDriver.Stop();
            File.WriteAllText(Path.Combine(dir, "frames.csv"), _csv.ToString(), new UTF8Encoding(false));

            int expected = (frames + Mathf.Max(1, def.RecordEvery) - 1) / Mathf.Max(1, def.RecordEvery);
            bool ok = setupError == null && written == expected;
            if (!ok)
            {
                _failures++;
                Debug.LogWarning("[ABYSS] QA sequence " + def.Name + " incomplete: " + written + "/" + expected + " frames" + (setupError != null ? " (" + setupError + ")" : ""));
            }
            var j = new QaJson();
            j.BeginObject();
            j.Prop("name", def.Name);
            j.Prop("group", def.Group);
            j.Prop("kind", def.Kind.ToString());
            j.Prop("note", def.Note);
            j.Prop("fps", fps);
            j.Prop("seconds", seconds);
            j.Prop("frames", expected);
            j.Prop("written", written);
            if (def.RecordEvery > 1) { j.Prop("simulated_frames", frames); j.Prop("record_every", def.RecordEvery); j.Prop("written_fps", (float)fps / def.RecordEvery); }
            j.Prop("width", w);
            j.Prop("height", h);
            j.Prop("start_clock", startClock);
            j.Prop("clock_frozen", def.ClockFrozen || def.Kind == SeqKind.Steps);
            if (keyPose) j.Prop("key_shot", key.Tag);
            if (def.Kind == SeqKind.Steps) j.Prop("step_m", def.StepM);
            if (moving) j.Prop("velocity", def.Velocity);
            if (def.RideT0 >= 0f) j.Prop("ride_start", def.RideT0);
            j.Prop("d_min", minD < float.MaxValue ? minD : float.NaN);
            j.Prop("d_max", maxD > float.MinValue ? maxD : float.NaN);
            j.Prop("mean_luma_min", minLuma < double.MaxValue ? minLuma : double.NaN);
            j.Prop("mean_luma_max", maxLuma > double.MinValue ? maxLuma : double.NaN);
            if (def.Kind == SeqKind.Ride || def.Kind == SeqKind.RideFrom)
            {
                j.Prop("lift_max_offset_mm", maxOffsetMm);
                j.Prop("lift_offset_limit_mm", QaAutoTest.MaxLiftOffsetMm);
                j.Prop("lift_offset_pass", maxOffsetMm <= QaAutoTest.MaxLiftOffsetMm);
            }
            if (def.Kind == SeqKind.WalkPinned)
            {
                j.Prop("pinned_eye", def.PinEye);
                j.Prop("pinned_yaw", def.PinYaw);
                j.Prop("pinned_pitch", def.PinPitch);
                j.Prop("pinned_fov", def.PinFov);
            }
            if (def.Window) j.Prop("capture", "window (ScreenCapture after WaitForEndOfFrame: post, final dither and overlay UI)");
            if (def.ApertureRegion) j.Prop("luma_region", "frames.csv region_*: the clear aperture of the gallery exit doorway, the whole frame once the eye is past it");
            WriteGoalFrames(j, goalT);
            if (def.Cue != CueRef.None) WriteCue(j, def, cueRef, cueT, warmPx, warmPct);
            j.Prop("suggested", (def.Judge != null && def.Judge.StartsWith("encode", StringComparison.Ordinal))
                ? "python -I Tools/qa/" + def.Judge.Replace("encode_test.py", "encode_test.py \"" + dir + "\"")
                : "python -I Tools/qa/sequence_metrics.py \"" + dir + "\" --fps " + (fps / Mathf.Max(1, def.RecordEvery)) + " --plot " + (def.Judge ?? ""));
            if (setupError != null) j.Prop("error", setupError);
            j.Prop("ok", ok);
            j.EndObject();
            j.Save(Path.Combine(dir, "sequence.json"));
            index.Raw(j.ToString().Replace("\n", "\n    "));
        }

        /// <summary>Fill the frame snapshot from the game state for a window-captured frame (no RenderTexture render).</summary>
        void SnapWindow(Texture2D t, float warmFromRow = -1f)
        {
            _snap.SceneT = SceneClock.Time;
            _snap.RealT = SceneClock.RealTime;
            _snap.D = GameState.D;
            _snap.E = GameState.E;
            _snap.Beat = GameState.Beat;
            _snap.Lift = GameState.Lift;
            _snap.RideS = GameState.RideSeconds;
            _snap.RideU = GameState.RideU;
            _snap.CamPos = GameState.CameraPos;
            _snap.CamRot = GameState.CameraRot;
            var rig = CameraRig.Instance;
            Camera cam = GameState.MainCamera;
            _snap.Fov = rig != null ? rig.CurrentFov : (cam != null ? cam.fieldOfView : 0f);
            _snap.HeadOffset = rig != null ? rig.LastHeadOffset : Vector3.zero;
            var lift = LiftController.Instance;
            _snap.LiftOffsetMm = lift != null ? lift.MaxOffsetError * 1000f : -1f;
            Color32[] px = t.GetPixels32();
            double sum = 0.0;
            for (int i = 0; i < px.Length; i++) sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
            _snap.Luma = px.Length > 0 ? sum / (255.0 * px.Length) : double.NaN;
            _snap.WarmPx = -1;
            _snap.WarmPct = double.NaN;
            if (warmFromRow >= 0f && px.Length == t.width * t.height)
            {
                // texture order (row 0 = bottom): the rows below warmFromRow (from the top) are rows 0 .. (1 - warmFromRow) h
                int rows = Mathf.Clamp(Mathf.RoundToInt((1f - warmFromRow) * t.height), 0, t.height);
                long n = 0;
                for (int i = 0; i < rows * t.width; i++) if (IsWarm(px[i].r, px[i].g, px[i].b)) n++;
                _snap.WarmPx = n;
                _snap.WarmPct = 100.0 * n / Math.Max(1, px.Length);
            }
        }

        /// <summary>
        /// The warm-pixel rule of the ending and ignition cues (critique R2-05, R2-20): (R - B) &gt; 0.08 and R &gt; 0.12 (sRGB
        /// fractions, i.e. R - B &gt; 20 and R &gt; 30 levels) with R &gt;= G &gt;= B.
        /// </summary>
        public static bool IsWarm(byte r, byte g, byte b) => r - b > 20 && r > 30 && r >= g && g >= b;

        /// <summary>Warm pixels (see <see cref="IsWarm"/>) of an RGB24 capture (texture order) below a row (fraction from the top), and their share of the frame in %.</summary>
        public static long WarmCount(byte[] rgb, int w, int h, float fromRow, out double pct)
        {
            pct = double.NaN;
            if (rgb == null || rgb.Length < w * h * 3) return -1;
            int rows = Mathf.Clamp(Mathf.RoundToInt((1f - fromRow) * h), 0, h);
            long n = 0;
            for (int i = 0; i < rows * w; i++)
            {
                int k = 3 * i;
                if (IsWarm(rgb[k], rgb[k + 1], rgb[k + 2])) n++;
            }
            pct = 100.0 * n / Math.Max(1, w * h);
            return n;
        }

        /// <summary>Track the reference event of a cue sequence (called every simulated frame).</summary>
        static void UpdateCueRef(CueRef cue, ref double t, ref float prevD)
        {
            if (cue == CueRef.None || !double.IsNaN(t)) return;
            switch (cue)
            {
                case CueRef.Bolt:
                {
                    float d = GameState.D, rd = QaSequenceCatalog.BoltReleaseD;
                    if (!float.IsNaN(prevD) && prevD < rd && d >= rd && d - prevD < 2f) t = SceneClock.Time;
                    prevD = d;
                    break;
                }
                case CueRef.Ignition:
                {
                    // _AbLampIgnite.x turns from -1 to a start time when beat 7 fires the ignition. Its value is not the
                    // ignition moment (since R2-20 it is shifted by the lattice's fitted offset, which lives in the Beats
                    // assembly), so the reference is the scene time of the first simulated frame that sees it set: at most
                    // one frame (1/20 s here) after the trigger
                    Vector4 v = Shader.GetGlobalVector(ShaderIds.AbLampIgnite);
                    if (v.x >= 0f) t = SceneClock.Time;
                    break;
                }
                case CueRef.Goal:
                    if (GameState.GoalReached && GameState.GoalRealTime >= 0.0) t = GameState.GoalRealTime;
                    break;
            }
        }

        /// <summary>Index of the written frame whose cue time is nearest a mark (within half a second), or -1.</summary>
        static int NearestFrame(List<double> cueT, double mark)
        {
            int best = -1;
            double bd = 0.5;
            for (int i = 0; i < cueT.Count; i++)
            {
                if (double.IsNaN(cueT[i])) continue;
                double dd = Math.Abs(cueT[i] - mark);
                if (dd <= bd) { bd = dd; best = i; }
            }
            return best;
        }

        /// <summary>
        /// sequence.json "cue": the reference event, the frames nearest each mark (with their warm-pixel share) and the cue's
        /// verdict where one is defined: the ending's amber sliver (warm pixels of the lower half at goal +3 s, R2-05) and the
        /// on-screen ignition sweep (warm pixels of the lower 66 % from 2 % to 95 % of their final count, R2-20). Verdicts go to
        /// report.json tests_summary when the sequences run inside a still capture (the "cues" selector).
        /// </summary>
        void WriteCue(QaJson j, SeqDef def, double cueRef, List<double> cueT, List<long> warmPx, List<double> warmPct)
        {
            j.Obj("cue");
            j.Prop("reference", def.Cue.ToString());
            j.Prop("reference_seen", !double.IsNaN(cueRef));
            if (!double.IsNaN(cueRef)) j.Prop(def.Cue == CueRef.Goal ? "reference_real_time" : "reference_scene_time", cueRef);
            if (def.Cue == CueRef.Bolt) j.Prop("release_d", QaSequenceCatalog.BoltReleaseD);
            if (def.WarmFromRow >= 0f) j.Prop("warm_rule", "(R - B) > 0.08 and R > 0.12, R >= G >= B, rows below " + def.WarmFromRow.ToString("0.00", SInv) + " of the frame");
            j.Obj("cue_frames");
            double[] marks = def.CueMarks ?? new double[0];
            for (int m = 0; m < marks.Length; m++)
            {
                int i = NearestFrame(cueT, marks[m]);
                j.Key("plus_" + marks[m].ToString("0.0", SInv) + "s");
                if (i < 0) { j.Null(); continue; }
                string v = "frame_" + i.ToString("00000", SInv) + ".png (" + cueT[i].ToString("0.00", SInv) + " s";
                if (i < warmPct.Count && !double.IsNaN(warmPct[i])) v += ", warm " + warmPct[i].ToString("0.000", SInv) + " %, " + warmPx[i] + " px";
                j.Value(v + ")");
            }
            j.EndObject();

            string name = null;
            bool? pass = null;
            var t = new QaJson();
            t.BeginObject();
            if (def.Cue == CueRef.Goal)
            {
                name = def.Name + ":amber_sliver";
                int i = NearestFrame(cueT, QaSequenceCatalog.EndingWarmMark);
                t.Prop("test", "R2-05 ending: warm pixels of the lower half at goal +3 s (the amber sliver on the pier)");
                t.Prop("limit_pct", QaSequenceCatalog.EndingWarmMinPct);
                if (double.IsNaN(cueRef)) { t.Prop("error", "goal not reached"); pass = false; }
                else if (i < 0 || double.IsNaN(warmPct[i])) { t.Prop("error", "no frame near goal +3 s"); pass = false; }
                else
                {
                    t.Prop("frame", "frame_" + i.ToString("00000", SInv) + ".png");
                    t.Prop("goal_s", cueT[i]);
                    t.Prop("warm_pct", warmPct[i]);
                    pass = warmPct[i] >= QaSequenceCatalog.EndingWarmMinPct;
                }
            }
            else if (def.Cue == CueRef.Ignition)
            {
                name = def.Name + ":sweep";
                t.Prop("test", "R2-20 ignition: on-screen sweep (warm pixels of the lower 66 % from 2 % to 95 % of their final count)");
                t.Prop("limit_s", QaSequenceCatalog.IgnitionMinSweepSeconds);
                long before = 0;
                for (int i = 0; i < cueT.Count; i++) if ((double.IsNaN(cueT[i]) || cueT[i] < 0.0) && warmPx[i] > before) before = warmPx[i];
                long final = 0;
                for (int i = 0; i < cueT.Count; i++) if (!double.IsNaN(cueT[i]) && cueT[i] >= 0.0 && cueT[i] <= 12.0 && warmPx[i] - before > final) final = warmPx[i] - before;
                double t0 = double.NaN, t1 = double.NaN;
                for (int i = 0; i < cueT.Count; i++)
                {
                    if (double.IsNaN(cueT[i]) || cueT[i] < 0.0 || cueT[i] > 12.0) continue;
                    long g = warmPx[i] - before;
                    if (double.IsNaN(t0) && g >= Math.Max(5L, (long)(0.02 * final))) t0 = cueT[i];
                    if (double.IsNaN(t1) && g >= (long)(0.95 * final)) t1 = cueT[i];
                }
                // growth over the largest pre-ignition warm count (door frame, haze), judged up to ignition + 12 s (the door
                // lamp eases on after that and is not part of the sweep)
                t.Prop("warm_px_before", before);
                t.Prop("final_warm_px_growth", final);
                if (double.IsNaN(cueRef)) { t.Prop("error", "ignition not seen"); pass = false; }
                else if (final < 20 || double.IsNaN(t0) || double.IsNaN(t1)) { t.Prop("error", "no warm growth after the ignition"); pass = false; }
                else
                {
                    t.Prop("start_s", t0);
                    t.Prop("end_s", t1);
                    t.Prop("sweep_s", t1 - t0);
                    pass = t1 - t0 >= QaSequenceCatalog.IgnitionMinSweepSeconds;
                }
            }
            if (name != null)
            {
                t.Prop("pass", pass == true);
                t.EndObject();
                string tj = t.ToString();
                j.Key("test").Raw(tj.Replace("\n", "\n    "));
                _cueTests.Add(new KeyValuePair<string, bool?>(name, pass));
                _cueTestJson.Add(tj);
            }
            j.EndObject();
        }

        /// <summary>sequence.json "goal_frames": the frames nearest 0.5, 3 and 7 s after the goal (14.11 ending, R1-40).</summary>
        static void WriteGoalFrames(QaJson j, List<double> goalT)
        {
            double[] marks = { 0.5, 3.0, 7.0 };
            bool any = false;
            for (int i = 0; i < goalT.Count; i++) if (!double.IsNaN(goalT[i])) { any = true; break; }
            if (!any) return;
            j.Obj("goal_frames");
            for (int m = 0; m < marks.Length; m++)
            {
                int best = -1;
                double bd = double.MaxValue;
                for (int i = 0; i < goalT.Count; i++)
                {
                    if (double.IsNaN(goalT[i])) continue;
                    double dd = Math.Abs(goalT[i] - marks[m]);
                    if (dd < bd) { bd = dd; best = i; }
                }
                j.Key("goal_plus_" + marks[m].ToString("0.0", SInv) + "s");
                if (best < 0) j.Null();
                else j.Value("frame_" + best.ToString("00000", SInv) + ".png (" + goalT[best].ToString("0.00", SInv) + " s)");
            }
            j.EndObject();
        }

        void AppendRow(int k, string file, bool aperture = false, int w = 0, int h = 0, double goalS = double.NaN, double cueS = double.NaN)
        {
            ref Snapshot s = ref _snap;
            Vector3 f = s.CamRot * Vector3.forward;
            Vector3 r = s.CamRot * Vector3.right;
            float yaw = Mathf.Repeat(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 360f);
            float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            float roll = Mathf.Asin(Mathf.Clamp(r.y, -1f, 1f)) * Mathf.Rad2Deg;
            StringBuilder b = _csv;
            b.Append(k).Append(',').Append(file).Append(',')
             .Append(s.SceneT.ToString("F5", SInv)).Append(',').Append(s.RealT.ToString("F5", SInv)).Append(',')
             .Append(s.D.ToString("F4", SInv)).Append(',').Append(s.E.ToString("F4", SInv)).Append(',').Append(s.Beat).Append(',')
             .Append((int)s.Lift).Append(',').Append(s.RideS.ToString("F4", SInv)).Append(',').Append(s.RideU.ToString("F6", SInv)).Append(',')
             .Append(s.CamPos.x.ToString("F5", SInv)).Append(',').Append(s.CamPos.y.ToString("F5", SInv)).Append(',').Append(s.CamPos.z.ToString("F5", SInv)).Append(',')
             .Append(yaw.ToString("F4", SInv)).Append(',').Append(pitch.ToString("F4", SInv)).Append(',').Append(roll.ToString("F5", SInv)).Append(',')
             .Append(s.Fov.ToString("F3", SInv)).Append(',')
             .Append((s.HeadOffset.x * 1000f).ToString("F4", SInv)).Append(',').Append((s.HeadOffset.y * 1000f).ToString("F4", SInv)).Append(',')
             .Append((s.HeadOffset.z * 1000f).ToString("F4", SInv)).Append(',').Append(s.LiftOffsetMm.ToString("F5", SInv)).Append(',')
             .Append(s.Luma.ToString("F6", SInv));
            if (aperture && w > 0 && h > 0 && s.D < QaSequenceCatalog.T12_EV_REGION_STOP_D)
            {
                // the doorway's clear aperture in this frame (10 % inside the jambs, R2-01), or the whole frame when it is not
                // in view; from d 59.3 on no region is written (the aperture would grow to the full frame over d 59.3-59.8):
                // sequence_metrics.py judges those frames on the whole frame as a separate segment
                float x0 = 0f, y0 = 0f, x1 = 1f, y1 = 1f;
                if (KeyShots.DoorwayAperture(s.CamPos, s.CamRot, s.Fov, (float)w / h, AbyssConst.CAMERA_NEAR, KeyShots.SequenceApertureInset,
                        out float a0, out float a1, out float a2, out float a3)
                    && (a2 - a0) * w >= 8f && (a3 - a1) * h >= 8f)
                {
                    x0 = a0; y0 = a1; x1 = a2; y1 = a3;
                }
                b.Append(',').Append(x0.ToString("F5", SInv)).Append(',').Append(y0.ToString("F5", SInv))
                 .Append(',').Append(x1.ToString("F5", SInv)).Append(',').Append(y1.ToString("F5", SInv));
            }
            else b.Append(",,,,");
            b.Append(',');
            if (!double.IsNaN(goalS)) b.Append(goalS.ToString("F3", SInv));
            b.Append(',');
            if (!double.IsNaN(cueS)) b.Append(cueS.ToString("F3", SInv));
            b.Append(',');
            if (s.WarmPx >= 0) b.Append(s.WarmPx);
            b.Append(',');
            if (s.WarmPx >= 0 && !double.IsNaN(s.WarmPct)) b.Append(s.WarmPct.ToString("F4", SInv));
            b.Append('\n');
        }
    }
}
