using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Megastructure
{
    /// <summary>
    /// What FadeController draws: a full-screen black overlay, the title, the controls hint, the ending line and the pause
    /// menu. Implementations: <see cref="ToolkitUiView"/> (UI Toolkit with the PanelSettings asset, primary) and, when no
    /// PanelSettings is assigned, UguiUiView (UGUI Canvas + legacy Text; compiled only with the ABYSS_UGUI define, which the
    /// Megastructure.Runtime asmdef sets through a versionDefine on com.unity.ugui together with the "UnityEngine.UI"
    /// reference) or else a ToolkitUiView on a PanelSettings created at runtime. Setters ignore unchanged values, so calling
    /// them every frame costs nothing and allocates nothing.
    /// </summary>
    public interface IUiView
    {
        void SetOverlay(float alpha);
        void SetTitle(float alpha);
        void SetHint(float alpha);
        void SetEndLine(float alpha);
        void SetPause(bool visible, int selected);

        /// <summary>Menu item under a screen position (pixels, origin bottom-left as in the Input System), or -1.</summary>
        int HitTestMenu(Vector2 screenPos);

        void Dispose();
    }

    /// <summary>Shared helpers for both views.</summary>
    static class UiShared
    {
        public static Font LoadFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>
        /// Last-resort PanelSettings created at runtime (no asset assigned and no UGUI): scale with the screen height at a
        /// 1080 reference, like the asset. Without a theme the default control styles are missing, which is harmless here:
        /// every element is styled inline.
        /// </summary>
        public static PanelSettings CreateRuntimePanelSettings()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "AbyssRuntimePanelSettings";
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f;
            ps.sortingOrder = 100;
            ps.clearColor = false;
            return ps;
        }

        /// <summary>True (and the cache updated) when v differs from the cached value; tiny steps are skipped except onto 0 or 1.</summary>
        public static bool Changed(ref float cache, float v)
        {
            if (cache == v) return false;
            if (Mathf.Abs(cache - v) < 1e-4f && v != 0f && v != 1f) return false;
            cache = v;
            return true;
        }
    }

    /// <summary>UI Toolkit view on a runtime UIDocument that uses the PanelSettings asset (theme UnityDefaultRuntimeTheme.tss).</summary>
    public sealed class ToolkitUiView : IUiView
    {
        readonly GameObject _go;
        readonly UIDocument _doc;
        readonly VisualElement _root, _overlay, _dim, _menu;
        readonly Label _title, _hint, _end;
        readonly Label[] _items;
        readonly StyleFontDefinition _font;
        float _panelH = -1f;

        float _aOverlay = -1f, _aTitle = -1f, _aHint = -1f, _aEnd = -1f;
        bool _pauseVisible = true;
        int _selected = -2;

        public ToolkitUiView(Transform parent, PanelSettings panelSettings)
        {
            _go = new GameObject("UIDocument");
            _go.SetActive(false);
            _go.transform.SetParent(parent, false);
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _go.SetActive(true);

            _font = new StyleFontDefinition(FontDefinition.FromFont(UiShared.LoadFont()));
            _root = _doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;

            _overlay = FullScreen("Overlay", Color.black);
            _title = CenteredLabel("Title", TitleCard.TITLE, TitleCard.TITLE_CENTRE_Y);
            _hint = CenteredLabel("Hint", TitleCard.HINT, TitleCard.HINT_CENTRE_Y);
            _end = CenteredLabel("EndLine", TitleCard.ENDING_LINE, TitleCard.END_CENTRE_Y);

            _dim = FullScreen("PauseDim", Color.black);
            _menu = new VisualElement { name = "PauseMenu", pickingMode = PickingMode.Ignore };
            _menu.style.position = Position.Absolute;
            _menu.style.left = 0; _menu.style.top = 0; _menu.style.right = 0; _menu.style.bottom = 0;
            _menu.style.flexDirection = FlexDirection.Column;
            _menu.style.justifyContent = Justify.Center;
            _menu.style.alignItems = Align.Center;
            _root.Add(_menu);
            _items = new Label[TitleCard.MENU_ITEMS.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                _items[i] = MakeLabel("Menu" + i, TitleCard.MENU_ITEMS[i]);
                _menu.Add(_items[i]);
            }

            // Default reference: PanelSettings scale-with-height at 1080 units. Refined once the panel is laid out.
            float h = panelSettings != null && panelSettings.scaleMode == PanelScaleMode.ScaleWithScreenSize
                ? Mathf.Max(1, panelSettings.referenceResolution.y) : Mathf.Max(1, Screen.height);
            ApplySizes(h);
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            SetOverlay(1f); SetTitle(0f); SetHint(0f); SetEndLine(0f); SetPause(false, 0);
        }

        VisualElement FullScreen(string name, Color c)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
            e.style.backgroundColor = c;
            _root.Add(e);
            return e;
        }

        Label MakeLabel(string name, string text)
        {
            var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            l.style.unityFontDefinition = _font;
            l.style.unityFontStyleAndWeight = FontStyle.Normal;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.color = TitleCard.TEXT_COLOUR;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.paddingRight = 0;
            l.style.backgroundColor = Color.clear;
            return l;
        }

        /// <summary>A label whose centre sits at centreY (fraction of the height from the top), horizontally centred.</summary>
        Label CenteredLabel(string name, string text, float centreY)
        {
            var wrap = new VisualElement { name = name + "Wrap", pickingMode = PickingMode.Ignore };
            wrap.style.position = Position.Absolute;
            wrap.style.left = 0; wrap.style.right = 0;
            wrap.style.top = Length.Percent(Mathf.Max(0f, 2f * centreY - 1f) * 100f);
            wrap.style.bottom = Length.Percent(Mathf.Max(0f, 1f - 2f * centreY) * 100f);
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.justifyContent = Justify.Center;
            wrap.style.alignItems = Align.Center;
            var l = MakeLabel(name, text);
            wrap.Add(l);
            _root.Add(wrap);
            return l;
        }

        void OnGeometryChanged(GeometryChangedEvent e)
        {
            float h = _root.layout.height;
            if (h > 1f && Mathf.Abs(h - _panelH) > 0.5f) ApplySizes(h);
        }

        void ApplySizes(float h)
        {
            _panelH = h;
            SetTypography(_title, TitleCard.TITLE_SIZE * h, TitleCard.LETTER_SPACING_EM);
            SetTypography(_hint, TitleCard.HINT_SIZE * h, TitleCard.HINT_SPACING_EM);
            SetTypography(_end, TitleCard.END_SIZE * h, TitleCard.LETTER_SPACING_EM);
            for (int i = 0; i < _items.Length; i++)
            {
                SetTypography(_items[i], TitleCard.MENU_SIZE * h, TitleCard.LETTER_SPACING_EM);
                _items[i].style.marginBottom = i < _items.Length - 1 ? TitleCard.MENU_GAP * h : 0f;
            }
        }

        static void SetTypography(Label l, float sizePx, float spacingEm)
        {
            float spacing = spacingEm * sizePx;    // LengthUnit has no em: px = em x font size
            l.style.fontSize = sizePx;
            l.style.letterSpacing = spacing;
            l.style.paddingLeft = spacing;         // the tracking also follows the last glyph: balance it so the word stays centred
        }

        static void SetAlpha(VisualElement e, float a)
        {
            if (a <= 0.0005f)
            {
                e.style.display = DisplayStyle.None;
            }
            else
            {
                e.style.display = DisplayStyle.Flex;
                e.style.opacity = a;
            }
        }

        public void SetOverlay(float alpha) { if (UiShared.Changed(ref _aOverlay, alpha)) SetAlpha(_overlay, alpha); }
        public void SetTitle(float alpha) { if (UiShared.Changed(ref _aTitle, alpha)) SetAlpha(_title, alpha * TitleCard.TITLE_OPACITY); }
        public void SetHint(float alpha) { if (UiShared.Changed(ref _aHint, alpha)) SetAlpha(_hint, alpha * TitleCard.HINT_OPACITY); }
        public void SetEndLine(float alpha) { if (UiShared.Changed(ref _aEnd, alpha)) SetAlpha(_end, alpha * TitleCard.END_OPACITY); }

        public void SetPause(bool visible, int selected)
        {
            if (visible == _pauseVisible && selected == _selected) return;
            _pauseVisible = visible; _selected = selected;
            SetAlpha(_dim, visible ? TitleCard.PAUSE_DIM : 0f);
            _menu.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _items.Length; i++)
                _items[i].style.opacity = i == selected ? TitleCard.MENU_SELECTED_OPACITY : TitleCard.MENU_IDLE_OPACITY;
        }

        public int HitTestMenu(Vector2 screenPos)
        {
            if (!_pauseVisible || _root.panel == null) return -1;
            // The root covers the whole panel: map screen pixels (Input System, origin bottom-left) to panel units (origin
            // top-left) from its laid-out size, independent of the panel's scale mode.
            Rect lay = _root.layout;
            if (!(lay.width > 0f) || !(lay.height > 0f) || Screen.width <= 0 || Screen.height <= 0) return -1;
            var p = new Vector2(screenPos.x * lay.width / Screen.width, (Screen.height - screenPos.y) * lay.height / Screen.height);
            float padY = TitleCard.MENU_GAP * _panelH * 0.5f;
            float padX = TitleCard.MENU_SIZE * _panelH;
            for (int i = 0; i < _items.Length; i++)
            {
                Rect r = _items[i].worldBound;
                if (p.x >= r.xMin - padX && p.x <= r.xMax + padX && p.y >= r.yMin - padY && p.y <= r.yMax + padY) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_go != null) Object.Destroy(_go);
        }
    }

#if ABYSS_UGUI
    /// <summary>
    /// UGUI fallback (no PanelSettings asset): ScreenSpaceOverlay Canvas scaled with the screen height (reference 1080), legacy
    /// Text with LegacyRuntime.ttf. UGUI Text has no letter spacing, so tracking is made of U+200A hair spaces between the
    /// glyphs (count measured from the font's hair-space advance so the gap is about the wanted em fraction).
    /// Compiled only with ABYSS_UGUI (needs the "UnityEngine.UI" reference in Megastructure.Runtime.asmdef).
    /// </summary>
    public sealed class UguiUiView : IUiView
    {
        const float RefHeight = 1080f;
        readonly GameObject _canvasGo;
        readonly UnityEngine.UI.Image _overlay, _dim;
        readonly UnityEngine.UI.Text _title, _hint, _end;
        readonly UnityEngine.UI.Text[] _items;
        readonly Font _font;

        float _aOverlay = -1f, _aTitle = -1f, _aHint = -1f, _aEnd = -1f;
        bool _pauseVisible = true;
        int _selected = -2;

        public UguiUiView(Transform parent)
        {
            _font = UiShared.LoadFont();
            _canvasGo = new GameObject("UGUI Canvas");
            _canvasGo.transform.SetParent(parent, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, RefHeight);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            _overlay = MakeImage("Overlay", Color.black);
            _title = MakeText("Title", TitleCard.TITLE, TitleCard.TITLE_SIZE, TitleCard.LETTER_SPACING_EM, TitleCard.TITLE_CENTRE_Y);
            _hint = MakeText("Hint", TitleCard.HINT, TitleCard.HINT_SIZE, TitleCard.HINT_SPACING_EM, TitleCard.HINT_CENTRE_Y);
            _end = MakeText("EndLine", TitleCard.ENDING_LINE, TitleCard.END_SIZE, TitleCard.LETTER_SPACING_EM, TitleCard.END_CENTRE_Y);
            _dim = MakeImage("PauseDim", Color.black);
            int n = TitleCard.MENU_ITEMS.Length;
            _items = new UnityEngine.UI.Text[n];
            float step = TitleCard.MENU_SIZE + TitleCard.MENU_GAP;
            for (int i = 0; i < n; i++)
            {
                float cy = 0.5f + (i - (n - 1) * 0.5f) * step;
                _items[i] = MakeText("Menu" + i, TitleCard.MENU_ITEMS[i], TitleCard.MENU_SIZE, TitleCard.LETTER_SPACING_EM, cy);
            }
            SetOverlay(1f); SetTitle(0f); SetHint(0f); SetEndLine(0f); SetPause(false, 0);
        }

        UnityEngine.UI.Image MakeImage(string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        UnityEngine.UI.Text MakeText(string name, string text, float sizeFrac, float spacingEm, float centreYFromTop)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            float y = 1f - centreYFromTop;                    // UGUI anchors run bottom-up
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            int size = Mathf.Max(1, Mathf.RoundToInt(sizeFrac * RefHeight));
            rt.sizeDelta = new Vector2(0f, size * 2f);
            rt.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<UnityEngine.UI.Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Normal;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.color = TitleCard.TEXT_COLOUR;
            t.text = Track(text, size, spacingEm);
            return t;
        }

        /// <summary>Insert hair spaces between glyphs so the extra advance is about spacingEm x size.</summary>
        string Track(string text, int size, float spacingEm)
        {
            const char hair = ' ';
            float advance = 0f;
            if (_font != null)
            {
                _font.RequestCharactersInTexture(hair.ToString(), size, FontStyle.Normal);
                if (_font.GetCharacterInfo(hair, out CharacterInfo ci, size, FontStyle.Normal)) advance = ci.advance;
            }
            if (advance <= 0f) advance = size / 12f;          // typical hair-space width (1/12 em) when the glyph is unknown
            int count = Mathf.Clamp(Mathf.RoundToInt(spacingEm * size / advance), 1, 16);
            var sb = new StringBuilder(text.Length * (count + 1));
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);
                if (i < text.Length - 1) sb.Append(hair, count);
            }
            return sb.ToString();
        }

        static void SetAlpha(UnityEngine.UI.Graphic g, float a)
        {
            bool on = a > 0.0005f;
            if (g.enabled != on) g.enabled = on;
            if (on) g.color = UiShared.WithAlpha(g.color, a);
        }

        public void SetOverlay(float alpha) { if (UiShared.Changed(ref _aOverlay, alpha)) SetAlpha(_overlay, alpha); }
        public void SetTitle(float alpha) { if (UiShared.Changed(ref _aTitle, alpha)) SetAlpha(_title, alpha * TitleCard.TITLE_OPACITY); }
        public void SetHint(float alpha) { if (UiShared.Changed(ref _aHint, alpha)) SetAlpha(_hint, alpha * TitleCard.HINT_OPACITY); }
        public void SetEndLine(float alpha) { if (UiShared.Changed(ref _aEnd, alpha)) SetAlpha(_end, alpha * TitleCard.END_OPACITY); }

        public void SetPause(bool visible, int selected)
        {
            if (visible == _pauseVisible && selected == _selected) return;
            _pauseVisible = visible; _selected = selected;
            SetAlpha(_dim, visible ? TitleCard.PAUSE_DIM : 0f);
            for (int i = 0; i < _items.Length; i++)
                SetAlpha(_items[i], visible ? (i == selected ? TitleCard.MENU_SELECTED_OPACITY : TitleCard.MENU_IDLE_OPACITY) : 0f);
        }

        public int HitTestMenu(Vector2 screenPos)
        {
            if (!_pauseVisible) return -1;
            for (int i = 0; i < _items.Length; i++)
            {
                var rt = (RectTransform)_items[i].transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null)) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_canvasGo != null) Object.Destroy(_canvasGo);
        }
    }
#endif
}
