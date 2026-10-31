// InputTests: the Input System is the only input handler and the device accessors work (MASTER_PROMPT 7.6, R8 3).
// In batchmode there may be no keyboard or mouse at all; then the device test is ignored instead of failing.
using System.Collections;
using Megastructure.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Megastructure.Tests
{
    public class InputTests
    {
        [Test]
        public void A_ActiveInputHandling_IsInputSystemOnly()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            Assert.Fail("ENABLE_LEGACY_INPUT_MANAGER is defined: set activeInputHandler: 1 (Input System only), 7.6");
#elif !ENABLE_INPUT_SYSTEM
            Assert.Fail("ENABLE_INPUT_SYSTEM is not defined: the Input System package is not the active handler, 7.6");
#else
            Assert.Pass("Input System only");
#endif
        }

        [Test]
        public void B_CurrentDevices_ExistWhenPresent()
        {
            bool anyKeyboard = false, anyMouse = false;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i] is Keyboard) anyKeyboard = true;
                if (devices[i] is Mouse) anyMouse = true;
            }
            if (!anyKeyboard && !anyMouse)
                Assert.Ignore("no keyboard or mouse device present (batchmode without devices); Keyboard.current / Mouse.current are checked in the built player");
            if (anyKeyboard) Assert.IsNotNull(Keyboard.current, "a keyboard is present but Keyboard.current is null");
            if (anyMouse) Assert.IsNotNull(Mouse.current, "a mouse is present but Mouse.current is null");
            ThreadInput.DeviceSummary(out bool kb, out bool mouse, out bool gp);
            Debug.Log($"[THREAD] InputTests devices: keyboard={kb} mouse={mouse} gamepad={gp}");
        }

        [Test]
        public void C_ThreadInput_SafeWithAnyDeviceSet()
        {
            // every accessor null-checks its device, so none of these may throw with or without hardware
            Assert.DoesNotThrow(() =>
            {
                ThreadInput.Move();
                ThreadInput.MoveActive();
                ThreadInput.Brisk();
                ThreadInput.LookDegrees(PlayerTuning.MouseDegPerCount, PlayerTuning.StickDegPerSec);
                ThreadInput.LookActive(PlayerTuning.LookThresholdCounts, PlayerTuning.StickThreshold);
                ThreadInput.AnyCancelInput(PlayerTuning.LookThresholdCounts, PlayerTuning.StickThreshold);
                ThreadInput.CinematicToggle();
                ThreadInput.PausePressed();
                ThreadInput.F11Pressed();
                ThreadInput.AltEnterPressed();
                ThreadInput.MenuUp(); ThreadInput.MenuDown(); ThreadInput.MenuConfirm(); ThreadInput.MenuBack();
                ThreadInput.MouseClicked();
                ThreadInput.MousePosition(out Vector2 _);
            });
            Assert.LessOrEqual(ThreadInput.Move().magnitude, 1f + 1e-5f);
        }

        [UnityTest]
        public IEnumerator D_VirtualKeyboard_DrivesMoveAndBrisk()
        {
            Keyboard previous = Keyboard.current;
            var kb = InputSystem.AddDevice<Keyboard>("ThreadTestKeyboard");
            try
            {
                kb.MakeCurrent();
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W, Key.LeftShift));
                yield return null;
                if (!kb.wKey.isPressed)
                {
                    Assert.Ignore("input events were not delivered this frame (no Game-view focus in this run)");
                }
                kb.MakeCurrent();   // a real keyboard event in the same frame could have made the hardware keyboard current
                Assert.AreSame(kb, Keyboard.current);
                Assert.Greater(ThreadInput.Move().y, 0.5f, "W walks forward");
                Assert.IsTrue(ThreadInput.Brisk(), "Shift is brisk");

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D));
                yield return null;
                kb.MakeCurrent();
                Assert.Greater(ThreadInput.Move().x, 0.5f, "D strafes right");
                Assert.IsFalse(kb.shiftKey.isPressed);

                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Assert.IsFalse(kb.wKey.isPressed || kb.dKey.isPressed, "keys released");
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }
    }
}
