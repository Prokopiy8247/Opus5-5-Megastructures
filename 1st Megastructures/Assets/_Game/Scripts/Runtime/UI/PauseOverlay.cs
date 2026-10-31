using UnityEngine;
using UnityEngine.InputSystem;

namespace Megastructure
{
    public enum PauseAction { None = 0, Resume = 1, Restart = 2, Quit = 3 }

    /// <summary>
    /// The minimal pause menu of Part 9 (Resume, Restart, Quit) as input logic; FadeController owns the pause state and draws it
    /// through an <see cref="IUiView"/>. Keyboard: Up/Down or W/S to move, Enter or Space to choose, Esc to resume. Mouse: hover
    /// selects (only when the pointer actually moves, so it never fights the keyboard), left click chooses. Gamepad: d-pad or
    /// left stick, South to choose, East or Start to resume. Input System only; allocation-free.
    /// </summary>
    public sealed class PauseOverlay
    {
        public const int ITEM_RESUME = 0, ITEM_RESTART = 1, ITEM_QUIT = 2;

        public bool IsOpen { get; private set; }
        public int Selected { get; private set; }

        Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        bool _stickLatched;

        public void Open()
        {
            IsOpen = true;
            Selected = ITEM_RESUME;
            Mouse m = Mouse.current;
            _lastMouse = m != null ? m.position.ReadValue() : Vector2.zero;
            _stickLatched = true;   // ignore a stick that is still deflected from play
        }

        public void Close()
        {
            IsOpen = false;
        }

        /// <summary>Read this frame's input while the menu is open and return the chosen action.</summary>
        public PauseAction Poll(IUiView view)
        {
            if (!IsOpen) return PauseAction.None;
            int count = TitleCard.MENU_ITEMS.Length;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Selected = (Selected + count - 1) % count;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Selected = (Selected + 1) % count;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    return Choose(Selected);
            }

            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) Selected = (Selected + count - 1) % count;
                if (gp.dpad.down.wasPressedThisFrame) Selected = (Selected + 1) % count;
                float y = gp.leftStick.ReadValue().y;
                if (Mathf.Abs(y) < 0.3f) _stickLatched = false;
                else if (!_stickLatched)
                {
                    _stickLatched = true;
                    Selected = y > 0f ? (Selected + count - 1) % count : (Selected + 1) % count;
                }
                if (gp.buttonSouth.wasPressedThisFrame) return Choose(Selected);
                if (gp.buttonEast.wasPressedThisFrame) return PauseAction.Resume;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && view != null)
            {
                Vector2 p = mouse.position.ReadValue();
                bool moved = (p - _lastMouse).sqrMagnitude > 1f;
                _lastMouse = p;
                int hit = view.HitTestMenu(p);
                if (moved && hit >= 0) Selected = hit;
                if (mouse.leftButton.wasPressedThisFrame && hit >= 0)
                {
                    Selected = hit;
                    return Choose(hit);
                }
            }
            return PauseAction.None;
        }

        static PauseAction Choose(int item)
        {
            switch (item)
            {
                case ITEM_RESTART: return PauseAction.Restart;
                case ITEM_QUIT: return PauseAction.Quit;
                default: return PauseAction.Resume;
            }
        }

        /// <summary>True on the frame Esc (keyboard) or Start (gamepad) is pressed: opens or closes the menu.</summary>
        public static bool TogglePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.startButton.wasPressedThisFrame;
        }
    }
}
