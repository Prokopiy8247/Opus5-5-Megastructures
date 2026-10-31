using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// One frame of player input, produced by the human input reader of <see cref="PlayerController"/> or by an
    /// <see cref="IPlayerDriver"/> (the AutoWalker, a QA bot). Plain struct, no allocation.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>Move intent, magnitude 0..1. Body frame (x = strafe right, y = forward) unless <see cref="MoveIsWorld"/>.</summary>
        public Vector2 Move;

        /// <summary>Brisk walk (2.5 m/s instead of 1.5 m/s). Ignored when <see cref="SpeedOverride"/> is positive.</summary>
        public bool Brisk;

        /// <summary>Relative look in degrees this frame (yaw right positive, pitch up positive). Ignored with <see cref="AbsoluteLook"/>.</summary>
        public float YawDelta, PitchDelta;

        /// <summary>When true the controller takes <see cref="Yaw"/> / <see cref="Pitch"/> as the new orientation.</summary>
        public bool AbsoluteLook;
        public float Yaw, Pitch;

        /// <summary>Target ground speed in m/s at |Move| = 1 when positive (AutoWalker, bots); 0 = walk / brisk speeds.</summary>
        public float SpeedOverride;

        /// <summary>When true <see cref="Move"/> is a world-space direction (x = +X, y = +Z) instead of the body frame.</summary>
        public bool MoveIsWorld;

        public void Clear()
        {
            Move = Vector2.zero; Brisk = false; YawDelta = 0f; PitchDelta = 0f;
            AbsoluteLook = false; Yaw = 0f; Pitch = 0f; SpeedOverride = 0f; MoveIsWorld = false;
        }
    }

    /// <summary>
    /// Something that drives the player instead of the human (AutoWalker, wall-hugging bots). The PlayerController calls
    /// <see cref="Drive"/> once per frame in its Update (order -100) while <see cref="WantsControl"/> is true; the frame passed
    /// in already holds the human input, so a driver can read it (for cancel detection) before overwriting it.
    /// </summary>
    public interface IPlayerDriver
    {
        bool WantsControl { get; }
        void Drive(ref PlayerInputFrame input, float dt);
    }
}
