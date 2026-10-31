using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Megastructure.Tests
{
    /// <summary>
    /// Part 7.6: the new Input System is the active handler (Keyboard.current and Mouse.current exist whenever such devices
    /// are present), plus smoke tests of the PlayerController: a driver moves it at the walk speed on a corridor floor, and
    /// without any floor collider the safety returns stop after NO_FLOOR_RETURNS falls (analytic floor backstop).
    /// The movement tests run on a fixed 60 Hz frame time (Time.captureDeltaTime) so a slow batchmode frame (the controller
    /// clamps dt to 0.1 s) cannot skew the measured speed; every test restores the frame time and destroys its objects.
    /// </summary>
    public sealed class InputTests
    {
        const float FrameDt = 1f / 60f;

        static bool HasDevice<T>() where T : InputDevice
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
                if (devices[i] is T) return true;
            return false;
        }

        [Test]
        public void KeyboardAndMouse_AreCurrent()
        {
            bool kb = HasDevice<Keyboard>(), mouse = HasDevice<Mouse>();
            if (!kb && !mouse)
                Assert.Ignore("No keyboard or mouse device (headless batchmode run); verified in the built player instead.");
            if (kb) Assert.IsNotNull(Keyboard.current, "Keyboard device exists but Keyboard.current is null");
            if (mouse) Assert.IsNotNull(Mouse.current, "Mouse device exists but Mouse.current is null");
        }

        [Test]
        public void InputSystem_IsEnabled()
        {
#if ENABLE_INPUT_SYSTEM
            Assert.IsNotNull(InputSystem.settings, "Input System settings missing");
#else
            Assert.Fail("ENABLE_INPUT_SYSTEM is not defined: Active Input Handling must be the Input System (new)");
#endif
        }

        /// <summary>Drives forward with a world-space move at the walk speed.</summary>
        sealed class ForwardDriver : IPlayerDriver
        {
            public bool WantsControl => true;
            public void Drive(ref PlayerInputFrame input, float dt)
            {
                input.Clear();
                input.Move = new Vector2(0f, 1f);
                input.MoveIsWorld = true;
                input.SpeedOverride = AbyssConst.V_WALK;
                input.AbsoluteLook = true;
                input.Yaw = 0f;
                input.Pitch = 0f;
            }
        }

        /// <summary>Stands still with a level look (no human input reaches the controller).</summary>
        sealed class StillDriver : IPlayerDriver
        {
            public bool WantsControl => true;
            public void Drive(ref PlayerInputFrame input, float dt)
            {
                input.Clear();
                input.AbsoluteLook = true;
            }
        }

        static PlayerController MakePlayer(GameObject root)
        {
            ClockDriver.Begin(0.0);   // the player stands still while the walk clock is held
            GameState.Paused = false;
            GameState.Ended = false;
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            var pc = player.AddComponent<PlayerController>();
            pc.ManageCursor = false;
            return pc;
        }

        [UnityTest]
        public IEnumerator Driver_MovesPlayerAtWalkSpeed()
        {
            var root = new GameObject("InputTestRoot");
            float oldCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = FrameDt;
                var floor = new GameObject("Floor");
                floor.transform.SetParent(root.transform, false);
                floor.layer = CorridorKit.LayerCorridor;
                floor.transform.position = new Vector3(0f, -0.5f, 50f);
                floor.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 120f);
                Physics.SyncTransforms();

                var pc = MakePlayer(root);
                yield return null;
                pc.Teleport(new Vector3(0f, 0f, 2f), 0f, 0f);
                pc.Driver = new ForwardDriver();

                // let the velocity filter settle (0.6 s), then measure over one second of frames
                for (int i = 0; i < 36; i++) yield return null;
                float z0 = pc.FeetPosition.z;
                const int frames = 60;
                for (int i = 0; i < frames; i++) yield return null;
                float speed = (pc.FeetPosition.z - z0) / (frames * FrameDt);

                Assert.That(speed, Is.EqualTo(AbyssConst.V_WALK).Within(0.05f), "walk speed");
                Assert.That(pc.FeetPosition.y, Is.EqualTo(0f).Within(0.05f), "stays on the floor");
                Assert.That(pc.SafetyReturns, Is.EqualTo(0));
                Assert.That(pc.FloorBackstop, Is.False);
                Assert.That(Mathf.Abs(pc.Pitch), Is.LessThan(1e-3f));
            }
            finally
            {
                Time.captureDeltaTime = oldCapture;
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoFloorCollider_BackstopEndsTheFallLoop()
        {
            var root = new GameObject("InputTestRoot");
            float oldCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = FrameDt;
                var pc = MakePlayer(root);
                yield return null;
                pc.Teleport(new Vector3(0f, 0f, 10f), 0f, 0f);   // gallery, floor y 0: no collider anywhere
                pc.Driver = new StillDriver();

                // each fall return takes about 1 s (4 m drop, 1 s cooldown): allow 8 s for the returns, then 2 s held
                int guard = 0;
                while (!pc.FloorBackstop && guard < 480) { guard++; yield return null; }
                Assert.That(pc.FloorBackstop, Is.True, "backstop after repeated falls");
                Assert.That(pc.SafetyReturns, Is.EqualTo(PlayerController.NO_FLOOR_RETURNS));

                for (int i = 0; i < 120; i++) yield return null;
                Assert.That(pc.SafetyReturns, Is.EqualTo(PlayerController.NO_FLOOR_RETURNS), "no further returns");
                Assert.That(pc.FeetPosition.y, Is.EqualTo(0f).Within(0.01f), "held on the analytic floor");
                Assert.That(pc.Grounded, Is.True);
            }
            finally
            {
                Time.captureDeltaTime = oldCapture;
                Object.Destroy(root);
            }
            yield return null;
        }
    }
}
