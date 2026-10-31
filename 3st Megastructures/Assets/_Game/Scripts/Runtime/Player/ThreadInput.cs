// ThreadInput: direct device polling through the Input System package (MASTER_PROMPT 7.6, Part 9, research R8 3).
// No InputActionAsset, no EventSystem, never UnityEngine.Input (it throws with activeInputHandler = 1).
// Every accessor null-checks Keyboard.current / Mouse.current / Gamepad.current, so the game runs (and tests
// pass) in batchmode without devices. Zero allocations: indexed loops only, no LINQ, no closures.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Megastructure.Player
{
    public static class ThreadInput
    {
        /// <summary>Frames of mouse delta to drop after a cursor lock / focus regain (the warp spike, R8 3.3 item 4).</summary>
        public static int SkipMouseFrames;

        // ------------------------------------------------------------------ movement
        /// <summary>Move intent: x = strafe right, y = forward; magnitude clamped to 1 (WASD, arrows, left stick).</summary>
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            }
            var gp = Gamepad.current;
            if (gp != null) v += gp.leftStick.ReadValue();   // stickDeadzone processor already applied
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>True while a movement key or the left stick is engaged.</summary>
        public static bool MoveActive() => Move().sqrMagnitude > 0.01f;

        /// <summary>Brisk walk: Shift or either gamepad shoulder button (Part 9).</summary>
        public static bool Brisk()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.shiftKey.isPressed) || (gp != null && (gp.leftShoulder.isPressed || gp.rightShoulder.isPressed));
        }

        // ------------------------------------------------------------------ look
        /// <summary>
        /// Degrees to add this frame (x = yaw right, y = pitch up). Mouse: raw counts accumulated since the last frame
        /// times degPerCount, never multiplied by deltaTime, no smoothing, no acceleration, Y not inverted, only while
        /// the cursor is locked. Gamepad: rate x |stick| (quadratic response) x unscaledDeltaTime.
        /// </summary>
        public static Vector2 LookDegrees(float degPerCount, float stickDegPerSec)
        {
            Vector2 d = Vector2.zero;
            var m = Mouse.current;
            if (m != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 md = m.delta.ReadValue();
                if (SkipMouseFrames > 0) SkipMouseFrames--;
                else d += md * degPerCount;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 s = gp.rightStick.ReadValue();
                d += s * (s.magnitude * stickDegPerSec * Time.unscaledDeltaTime);
            }
            return d;
        }

        /// <summary>Look input above the noise thresholds this frame (mouse counts, right-stick deflection).</summary>
        public static bool LookActive(float mouseThresholdCounts, float stickThreshold)
        {
            var m = Mouse.current;
            if (m != null && m.delta.ReadValue().sqrMagnitude > mouseThresholdCounts * mouseThresholdCounts) return true;
            var gp = Gamepad.current;
            return gp != null && gp.rightStick.ReadValue().sqrMagnitude > stickThreshold * stickThreshold;
        }

        // ------------------------------------------------------------------ buttons
        public static bool CinematicToggle() { var kb = Keyboard.current; return kb != null && kb.cKey.wasPressedThisFrame; }

        public static bool PausePressed()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
        }

        public static bool F11Pressed() { var kb = Keyboard.current; return kb != null && kb.f11Key.wasPressedThisFrame; }

        public static bool AltEnterPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            return alt && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }

        public static bool MenuUp()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (gp != null && gp.dpad.up.wasPressedThisFrame);
        }

        public static bool MenuDown()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (gp != null && gp.dpad.down.wasPressedThisFrame);
        }

        public static bool MenuConfirm()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        }

        public static bool MenuBack() { var gp = Gamepad.current; return gp != null && gp.buttonEast.wasPressedThisFrame; }

        public static bool MouseClicked() { var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame; }

        public static bool MousePosition(out Vector2 pos)
        {
            var m = Mouse.current;
            if (m == null) { pos = Vector2.zero; return false; }
            pos = m.position.ReadValue();   // screen pixels, origin bottom-left
            return true;
        }

        public static bool MouseMoved(float thresholdCounts)
        {
            var m = Mouse.current;
            return m != null && m.delta.ReadValue().sqrMagnitude > thresholdCounts * thresholdCounts;
        }

        // ------------------------------------------------------------------ "any input"
        /// <summary>
        /// Any deliberate player input this frame (cancels the autowalk, hides the hint): a newly pressed key other than the
        /// toggles and OS keys (C, F11, Esc, Alt, Enter, PrintScreen, Windows), mouse motion above the threshold, a mouse
        /// button, a stick above the threshold or a gamepad button.
        /// </summary>
        public static bool AnyCancelInput(float mouseThresholdCounts, float stickThreshold)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed)
            {
                var keys = kb.allKeys;
                for (int i = 0; i < keys.Count; i++)
                {
                    KeyControl k = keys[i];
                    if (k == null || !k.wasPressedThisFrame) continue;
                    switch (k.keyCode)
                    {
                        case Key.C: case Key.F11: case Key.Escape: case Key.LeftAlt: case Key.RightAlt:
                        case Key.Enter: case Key.NumpadEnter: case Key.PrintScreen: case Key.LeftMeta: case Key.RightMeta:
                            continue;
                        default:
                            return true;
                    }
                }
            }
            var m = Mouse.current;
            if (m != null)
            {
                if (m.delta.ReadValue().sqrMagnitude > mouseThresholdCounts * mouseThresholdCounts) return true;
                if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) return true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                float t2 = stickThreshold * stickThreshold;
                if (gp.leftStick.ReadValue().sqrMagnitude > t2 || gp.rightStick.ReadValue().sqrMagnitude > t2) return true;
                if (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
                    gp.buttonNorth.wasPressedThisFrame || gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
                    gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ cursor
        /// <summary>Cursor locked and hidden during play, visible in the pause overlay (Part 9). Sets both explicitly.</summary>
        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) SkipMouseFrames = 2;
        }

        public static void DeviceSummary(out bool keyboard, out bool mouse, out bool gamepad)
        {
            keyboard = Keyboard.current != null;
            mouse = Mouse.current != null;
            gamepad = Gamepad.current != null;
        }
    }
}
