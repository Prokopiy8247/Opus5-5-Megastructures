// GlyphText: tracked single-line text from the built-in font, one legacy UGUI Text per glyph (MASTER_PROMPT 14.11,
// research R8 2). The legacy Text has no letter-spacing, so each glyph is placed at the measured advance plus the
// tracking in em. Glyph bounds are measured with a TextGenerator (the same generator UGUI renders with), so a row is
// centred on its true visual extent, and every glyph sits on whole pixels (crisp in the built player).
// Only the built-in font ("LegacyRuntime.ttf" in Unity 6000.6, R8 2.1), no TextMeshPro, no imported assets.
// Rows are rebuilt only when the resolution changes; nothing here runs per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Megastructure.UI
{
    public static class GlyphText
    {
        public const string BuiltinFontName = "LegacyRuntime.ttf";

        static Font s_font;
        static TextGenerator s_gen;

        public static Font BuiltinFont
        {
            get
            {
                if (s_font == null) s_font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
                return s_font;
            }
        }

        /// <summary>CREAM as a UI vertex colour (UGUI colours are sRGB; the canvas converts them in linear colour space).</summary>
        public static Color Cream
        {
            get { Color c = ThreadPalette.Linear(Pal.CREAM).gamma; c.a = 1f; return c; }
        }

        /// <summary>Screen Space Overlay canvas in real pixels (ConstantPixelSize, scale 1), no raycaster, no EventSystem.</summary>
        public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;   // snaps every glyph quad to the pixel grid
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            return canvas;
        }

        /// <summary>A full-screen child rect with its own CanvasGroup (row fades never rebuild glyph meshes).</summary>
        public static RectTransform CreateLayer(Transform parent, string name, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            return rt;
        }

        /// <summary>An empty row anchored at a normalised screen point (0..1, origin bottom-left) of its parent.</summary>
        public static RectTransform CreateRow(Transform parent, string name, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        public static void Clear(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var c = row.GetChild(i).gameObject;
                c.SetActive(false);
                Object.Destroy(c);
            }
        }

        /// <summary>
        /// Builds one Text per glyph into row, optically centred on the row's anchor: advance + trackingEm * fontPx
        /// between pens, the row centred on the visual extent (first glyph's left edge to last glyph's right edge,
        /// lowest to highest glyph pixel). Returns the visual size in pixels (used for mouse hit tests).
        /// </summary>
        public static Vector2 BuildRow(RectTransform row, string text, int fontPx, float trackingEm, Color color, FontStyle style = FontStyle.Normal)
        {
            Clear(row);
            if (string.IsNullOrEmpty(text) || fontPx <= 0) { row.sizeDelta = Vector2.zero; return Vector2.zero; }
            Font font = BuiltinFont;
            if (font == null) { Debug.LogError("[THREAD-FAIL] built-in font " + BuiltinFontName + " not found"); return Vector2.zero; }
            font.RequestCharactersInTexture(text, fontPx, style);
            float tracking = trackingEm * fontPx;
            int n = text.Length;
            var pens = new float[n];
            var glyphs = new Text[n];
            float pen = 0f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            if (s_gen == null) s_gen = new TextGenerator();

            for (int i = 0; i < n; i++)
            {
                char ch = text[i];
                bool ok = font.GetCharacterInfo(ch, out CharacterInfo ci, fontPx, style);
                float adv = ok ? ci.advance : fontPx * 0.3f;
                pens[i] = pen;
                if (!char.IsWhiteSpace(ch))
                {
                    var go = new GameObject("glyph_" + i, typeof(RectTransform));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(row, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(Mathf.Ceil(adv) + 2f, Mathf.Ceil(fontPx * 1.6f));
                    var t = go.AddComponent<Text>();
                    t.font = font; t.fontSize = fontPx; t.fontStyle = style;
                    t.alignment = TextAnchor.MiddleLeft;            // pen origin = rect left edge
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    t.verticalOverflow = VerticalWrapMode.Overflow;
                    t.supportRichText = false; t.raycastTarget = false; t.resizeTextForBestFit = false;
                    t.lineSpacing = 1f;
                    t.color = color;
                    t.text = ch.ToString();
                    glyphs[i] = t;

                    // measured visual bounds of this glyph, relative to its pen (generator output is pivot-relative)
                    var settings = t.GetGenerationSettings(rt.rect.size);
                    settings.scaleFactor = 1f;
                    if (s_gen.Populate(t.text, settings))
                    {
                        var verts = s_gen.verts;
                        for (int v = 0; v < verts.Count; v++)
                        {
                            Vector3 p = verts[v].position;
                            if (p.x + pen < minX) minX = p.x + pen;
                            if (p.x + pen > maxX) maxX = p.x + pen;
                            if (p.y < minY) minY = p.y;
                            if (p.y > maxY) maxY = p.y;
                        }
                    }
                    else if (ok)
                    {
                        if (pen + ci.minX < minX) minX = pen + ci.minX;
                        if (pen + ci.maxX > maxX) maxX = pen + ci.maxX;
                    }
                }
                pen += adv + (i < n - 1 ? tracking : 0f);
            }
            if (minX > maxX) { minX = 0f; maxX = pen; }
            if (minY > maxY) { minY = -0.5f * fontPx; maxY = 0.5f * fontPx; }

            float offX = Mathf.Round(-0.5f * (minX + maxX));
            float offY = Mathf.Round(-0.5f * (minY + maxY));
            for (int i = 0; i < n; i++)
            {
                if (glyphs[i] == null) continue;
                ((RectTransform)glyphs[i].transform).anchoredPosition = new Vector2(Mathf.Round(pens[i] + offX), offY);
            }
            var size = new Vector2(maxX - minX, maxY - minY);
            row.sizeDelta = size;
            return size;
        }

        /// <summary>A plain single Text line (the controls hint: no tracking), centred on the row.</summary>
        public static Text BuildLine(RectTransform row, string text, int fontPx, Color color, int screenWidth)
        {
            Clear(row);
            var go = new GameObject("line", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // even width and height keep the centred line on whole pixels
            int w = Mathf.Max(2, screenWidth & ~1), h = Mathf.Max(2, Mathf.CeilToInt(fontPx * 2f) & ~1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = BuiltinFont; t.fontSize = fontPx; t.fontStyle = FontStyle.Normal;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false; t.raycastTarget = false;
            t.color = color;
            t.text = text;
            row.sizeDelta = new Vector2(w, h);
            return t;
        }

        /// <summary>Pixel size from a fraction of the screen height (14.11: 4.5 %, 3.5 %, 1.6 %).</summary>
        public static int PixelSize(double heightFrac, int screenHeight)
        {
            return Mathf.Max(1, Mathf.RoundToInt((float)(heightFrac * screenHeight)));
        }
    }
}
