using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Runtime registry that decouples the systems (no system references another system's class directly).
    /// Writers: PlayerController (Player, Controller), CameraRig (Camera), scene objects (lights, volume via the
    /// AtmosphereDriver), WorldBuilder (Context). Directors register their synchronous tick in <see cref="Directors"/>.
    /// </summary>
    public static class RingRig
    {
        public static Transform Player;              // feet transform of the CharacterController object
        public static CharacterController Controller;
        public static Camera Camera;                 // the gameplay camera
        public static BuildContext Context;          // world content after load
        public static bool WorldReady;               // load complete (world built, pre-warmed)

        /// <summary>Capture/test override: when set (not NaN) the ProgressDirector uses this s instead of the player's.</summary>
        public static float ForcedS = float.NaN;
        /// <summary>Capture/test override of the elapsed time branch; NaN = derive from progress in capture mode.</summary>
        public static double ForcedT = double.NaN;

        /// <summary>Player velocity on the ground (m/s, xz), written by the PlayerController.</summary>
        public static Vector3 PlayerVelocity;
        /// <summary>Camera yaw / pitch in degrees (0 = +Z, pitch up positive), written by the CameraRig.</summary>
        public static float Yaw, Pitch;

        /// <summary>Synchronous update chain in order (ProgressDirector, BeatDirector, AtmosphereDriver, ...).
        /// Each entry receives dt. Used every frame by the directors themselves and by captures to settle state.</summary>
        public static readonly SortedList<int, Action<float>> Directors = new SortedList<int, Action<float>>();

        public static void RegisterDirector(int order, Action<float> tick) { Directors[order] = tick; }

        /// <summary>Runs the director chain once synchronously (captures call this after moving the camera).</summary>
        public static void TickDirectors(float dt)
        {
            var v = Directors.Values;                    // indexed: no boxed enumerator
            for (int i = 0; i < v.Count; i++) v[i](dt);
        }

        public static void Reset()
        {
            Player = null; Controller = null; Camera = null; Context = null; WorldReady = false;
            ForcedS = float.NaN; ForcedT = double.NaN; PlayerVelocity = Vector3.zero; Yaw = 0f; Pitch = 0f;
            Directors.Clear();
        }
    }

    /// <summary>Footstep surfaces of the 14.4 table.</summary>
    public enum RingSurface { Metal = 0, MetalRamp = 1, Ceramic = 2, Grass = 3, Bridge = 4, Underpass = 5, Stone = 6, Tunnel = 7, Deck = 8 }

    /// <summary>Game events (raised once, in order). Listeners: AudioDirector, UiDirector, QA.</summary>
    public static class RingEvents
    {
        public static event Action<int> BeatEntered;          // beat index 0..6, fired once each, in order
        public static event Action HatchTriggered;            // s >= 6.5
        public static event Action GoalReached;               // s >= 959
        public static event Action EndingComplete;            // ending text faded out (game stays on black)
        public static event Action<bool> PauseChanged;
        public static event Action<RingSurface, int, float> Footstep;   // surface, foot (0/1), speed m/s
        public static event Action LoadComplete;              // world built and pre-warmed; the fade-in may start
        public static event Action<bool> CinematicChanged;
        public static event Action PlayerMoved;               // first movement input (hides the controls hint)

        public static void RaiseBeatEntered(int b) => BeatEntered?.Invoke(b);
        public static void RaiseHatch() => HatchTriggered?.Invoke();
        public static void RaiseGoal() => GoalReached?.Invoke();
        public static void RaiseEndingComplete() => EndingComplete?.Invoke();
        public static void RaisePause(bool p) => PauseChanged?.Invoke(p);
        public static void RaiseFootstep(RingSurface s, int foot, float speed) => Footstep?.Invoke(s, foot, speed);
        public static void RaiseLoadComplete() => LoadComplete?.Invoke();
        public static void RaiseCinematic(bool on) => CinematicChanged?.Invoke(on);
        public static void RaisePlayerMoved() => PlayerMoved?.Invoke();

        public static void Reset()
        {
            BeatEntered = null; HatchTriggered = null; GoalReached = null; EndingComplete = null; PauseChanged = null;
            Footstep = null; LoadComplete = null; CinematicChanged = null; PlayerMoved = null;
        }

        /// <summary>Footstep surface for progress s (14.4 table).</summary>
        public static RingSurface SurfaceAt(float s, bool onGrass)
        {
            if (s < 10f) return RingSurface.Metal;
            if (s < 24f) return RingSurface.MetalRamp;
            if (s >= 120f && s < 180f) return RingSurface.Bridge;
            if (s < 558f) return onGrass ? RingSurface.Grass : RingSurface.Ceramic;
            if (s < 594f) return RingSurface.Underpass;
            if (s < 774f) return RingSurface.Stone;
            if (s < 894f) return RingSurface.Tunnel;
            return RingSurface.Deck;
        }
    }
}
