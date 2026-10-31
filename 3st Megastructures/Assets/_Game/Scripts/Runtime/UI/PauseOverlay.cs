// PauseOverlay: the minimal pause menu (MASTER_PROMPT Part 9, 14.11): RESUME, RESTART, QUIT in the title's
// typography (built-in font, CREAM, 0.3 em tracking), keyboard (Up/Down or W/S, Enter/Space, Esc), gamepad (d-pad,
// south = select, east / start = back) and mouse (hover selects, left click activates; a plain rectangle test, no
// EventSystem, because StandaloneInputModule would call the legacy Input class). Owned and driven by FadeController.
using Megastructure.Player;
using UnityEngine;

namespace Megastructure.UI
{
    public sealed class PauseOverlay
    {
        public enum Action { None = -1, Resume = 0, Restart = 1, Quit = 2 }

        // Layout (fractions of the screen height). 14.11 sizes the title at 4.5 %; the menu is quieter.
        public const double ItemHeightFrac = 0.026;
        public const double ItemSpacingFrac = 0.065;
        public const double CentreFromTop = 0.5;
        public const float SelectedAlpha = 0.9f;     // CREAM at 90 percent, as the title
        public const float IdleAlpha = 0.38f;
        public const float AlphaRate = 8f;            // selection fade speed (1/s)
        public const float HitPaddingEm = 0.6f;

        static readonly string[] s_labels = { "RESUME", "RESTART", "QUIT" };

        RectTransform m_layer;
        CanvasGroup m_group;
        readonly RectTransform[] m_items = new RectTransform[3];
        readonly CanvasGroup[] m_itemGroups = new CanvasGroup[3];
        readonly float[] m_itemAlpha = new float[3];
        int m_selected;
        int m_fontPx;
        Vector2 m_lastMouse;
        bool m_haveMouse;

        public int Selected => m_selected;
        public bool Built => m_layer != null;

        public void Create(Transform canvas)
        {
            m_layer = GlyphText.CreateLayer(canvas, "Pause", out m_group);
            for (int i = 0; i < m_items.Length; i++)
            {
                var holder = GlyphText.CreateLayer(m_layer, "item_" + s_labels[i], out m_itemGroups[i]);
                float y = (float)(1.0 - CentreFromTop - (i - 1) * ItemSpacingFrac);
                m_items[i] = GlyphText.CreateRow(holder, "row", new Vector2(0.5f, y));
                m_itemAlpha[i] = i == 0 ? SelectedAlpha : IdleAlpha;
            }
            m_layer.gameObject.SetActive(false);
        }

        public void Rebuild(int screenHeight)
        {
            if (m_layer == null) return;
            m_fontPx = GlyphText.PixelSize(ItemHeightFrac, screenHeight);
            Color c = GlyphText.Cream;
            for (int i = 0; i < m_items.Length; i++)
            {
                Vector2 size = GlyphText.BuildRow(m_items[i], s_labels[i], m_fontPx, (float)ThreadConstants.TitleSpacingEm, c);
                float pad = HitPaddingEm * m_fontPx;
                m_items[i].sizeDelta = new Vector2(size.x + 2f * pad, Mathf.Max(size.y, m_fontPx) + pad);
            }
        }

        public void Open()
        {
            if (m_layer == null) return;
            m_selected = 0;
            for (int i = 0; i < m_items.Length; i++) m_itemAlpha[i] = i == 0 ? SelectedAlpha : IdleAlpha;
            m_haveMouse = ThreadInput.MousePosition(out m_lastMouse);
            m_layer.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (m_layer != null && m_layer.gameObject.activeSelf) m_layer.gameObject.SetActive(false);
        }

        /// <summary>Overall alpha of the overlay (eased in and out by FadeController).</summary>
        public void SetAlpha(float a)
        {
            if (m_group != null) m_group.alpha = a;
        }

        /// <summary>Per-frame input while open (unscaled time). Returns the chosen action or None.</summary>
        public Action Tick(float dt)
        {
            if (m_layer == null) return Action.None;
            if (ThreadInput.MenuUp()) m_selected = (m_selected + m_items.Length - 1) % m_items.Length;
            if (ThreadInput.MenuDown()) m_selected = (m_selected + 1) % m_items.Length;

            int hover = -1;
            if (ThreadInput.MousePosition(out Vector2 mp))
            {
                for (int i = 0; i < m_items.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(m_items[i], mp, null)) { hover = i; break; }
                bool moved = !m_haveMouse || (mp - m_lastMouse).sqrMagnitude > 1f;
                m_lastMouse = mp; m_haveMouse = true;
                if (hover >= 0 && moved) m_selected = hover;   // hover selects only when the mouse actually moves
            }

            float k = 1f - Mathf.Exp(-AlphaRate * dt);
            for (int i = 0; i < m_items.Length; i++)
            {
                float target = i == m_selected ? SelectedAlpha : IdleAlpha;
                m_itemAlpha[i] += (target - m_itemAlpha[i]) * k;
                m_itemGroups[i].alpha = m_itemAlpha[i];
            }

            if (ThreadInput.MenuConfirm()) return (Action)m_selected;
            if (hover >= 0 && ThreadInput.MouseClicked()) { m_selected = hover; return (Action)hover; }
            if (ThreadInput.MenuBack()) return Action.Resume;
            return Action.None;
        }
    }
}
