using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The look-bible palette (14.2). Colours are authored as sRGB hex; <see cref="Lin"/> converts to linear for shaders and lights.
    /// Shader-side constants in AbyssFog.hlsl / AbyssCommon.hlsl mirror the domain 1 / domain 2 values defined here.
    /// </summary>
    public static class AbyssPalette
    {
        public static readonly Color CONC_LIGHT = Hex(0x8C9094);
        public static readonly Color CONC_MID = Hex(0x5D6267);
        public static readonly Color CONC_DARK = Hex(0x33373C);
        public static readonly Color STEEL_DARK = Hex(0x262B32);
        public static readonly Color RUST = Hex(0x5B3A27);
        public static readonly Color BLUE_BLACK = Hex(0x0B0E14);
        public static readonly Color COLD_LAMP = Hex(0xBFD6EA);
        public static readonly Color WIN_COLD = Hex(0x9FC4E8);
        public static readonly Color AMBER = Hex(0xFFB45A);
        public static readonly Color AMBER_LIGHT = Hex(0xFFA64A);
        public static readonly Color GLASS = Hex(0x0B1117);
        public static readonly Color OUT_HORIZ = Hex(0x8496A5);
        public static readonly Color OUT_ZENITH = Hex(0x3E5062);
        public static readonly Color ROD_POLISHED = Hex(0x8A9096);
        public static readonly Color CABLE_RUBBER = Hex(0x15181C);
        public static readonly Color DOOR_LEAF = Hex(0x3B4148);
        public static readonly Color FIXTURE_LIGHT = Hex(0xD5E4F2);
        public static readonly Color SPOT_CAN = Hex(0xCFE0F0);

        public const float AMBER_EMISSIVE = 8.0f;
        public const float MOUTH_EMISSIVE = 6.0f;
        public const float WINDOW_EMISSIVE_MAX = 5.0f;
        public const float MIN_LINEAR = 0.002f;

        // Fog domain 1 (Tunnel) and 2 (Exterior) constants (14.2)
        public static readonly Color TUNNEL_FOG = Hex(0x0A0D10);
        public const float TUNNEL_D50 = 90f;
        public static readonly Color EXT_FOG_DOWN = Hex(0x0A1015);
        public const float EXT_D50 = 7000f, EXT_M_DOWN = 0.4f;
        public static readonly Color DARK_CARD_TOP = Hex(0x0A1015);
        public static readonly Color DARK_CARD_BOTTOM = Hex(0x070B10);

        /// <summary>sRGB hex (gamma-space Color). Use .linear or <see cref="Lin"/> for shader constants.</summary>
        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>Linear-space colour of an sRGB palette token.</summary>
        public static Color Lin(Color srgb) => srgb.linear;

        /// <summary>Linear colour as Vector4 (for Shader.SetGlobalVector).</summary>
        public static Vector4 LinV(Color srgb) { Color c = srgb.linear; return new Vector4(c.r, c.g, c.b, 1f); }

        public static float D50ToSigma(float d50) => 0.8326f / Mathf.Max(d50, 1e-3f);
    }
}
