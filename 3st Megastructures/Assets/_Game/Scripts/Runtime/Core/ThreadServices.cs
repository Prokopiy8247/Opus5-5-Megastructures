// ThreadServices: the small interfaces through which separately-owned systems talk to each other
// (player, camera, audio, fade/UI, capture). Each system registers itself in Awake; consumers must
// tolerate a null service (e.g. EditMode tests, capture mode without UI).
using UnityEngine;

namespace Megastructure
{
    public enum FootSurface { Salt = 0, Stone = 1, Terrace = 2, Metal = 3, Pier = 4 }

    public interface IPlayerState
    {
        /// <summary>Progress = plan distance along the path (m).</summary>
        double S { get; }
        /// <summary>Lateral offset to the right of the path centre line (m).</summary>
        double Lateral { get; }
        /// <summary>Current plan speed (m/s).</summary>
        float Speed { get; }
        bool Brisk { get; }
        /// <summary>True once the beacon trigger (s >= 858) fired; input is replaced by the stop ramp.</summary>
        bool Finished { get; }
        /// <summary>World position of the eye (camera) this frame.</summary>
        Vector3 EyePosition { get; }
        /// <summary>Teleport to a corridor coordinate (tests, captures, -progress).</summary>
        void Teleport(double s, double lateral);
        /// <summary>External drive (AutoWalker): desired plan speed in m/s and lateral target; null input = player.</summary>
        void SetDrive(bool active, float speed, float lateralTarget);
        /// <summary>Freeze all movement (pause, captures).</summary>
        bool Frozen { get; set; }
    }

    public interface ICameraControl
    {
        Camera Camera { get; }
        /// <summary>World yaw (deg, from +Z toward +X) and pitch (deg, up positive) of the view.</summary>
        float Yaw { get; }
        float Pitch { get; }
        /// <summary>Look drive for the AutoWalker / captures (world yaw and pitch, deg).</summary>
        void SetLook(float worldYawDeg, float pitchDeg);
        /// <summary>Full pose override for captures; fovDeg &lt;= 0 keeps FOV(s).</summary>
        void SetPoseOverride(Vector3 position, float worldYawDeg, float pitchDeg, float fovDeg);
        void ClearPoseOverride();
        bool PoseOverridden { get; }
        /// <summary>True while mouse/gamepad look input arrived this frame (cancels the autowalk).</summary>
        bool LookInputThisFrame { get; }
    }

    public interface IAudioHooks
    {
        void Footstep(FootSurface surface, float pan, float gain);
        /// <summary>0..1 master duck (pause = 1).</summary>
        void SetPauseDuck(bool paused);
        /// <summary>Called once at the goal trigger: master fades out over 6 s.</summary>
        void BeginEnding();
        /// <summary>Starts the -audio-dump capture (seconds) and writes a WAV when done.</summary>
        void StartDump(string path, float seconds);
    }

    public interface IFadeUI
    {
        /// <summary>0 = picture visible, 1 = black.</summary>
        float Black { get; }
        void BeginIntro(bool showHint);
        void BeginEnding();
        void HideHint();
        bool Paused { get; }
        void SetBlackImmediate(float black);
    }

    public static class ThreadServices
    {
        public static IPlayerState Player;
        public static ICameraControl CameraControl;
        public static IAudioHooks Audio;
        public static IFadeUI Fade;

        public static void Clear() { Player = null; CameraControl = null; Audio = null; Fade = null; }
    }
}
