// ThreadPalette: the only place a colour is typed (MASTER_PROMPT 14.2.1, 14.7.8).
// The editor build script writes Assets/_Game/Settings/ThreadPalette.asset and the generated
// Assets/_Game/Shaders/ThreadPalette.hlsl from the table below; materials read their colours
// through MaterialLibrary, shaders through the generated PAL_* macros.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    public enum Pal
    {
        SALT_WHITE, SAND, SAND_SHADE, CORAL, CORAL_DEEP, TEAL, TEAL_DEEP, INK, CREAM,
        SKY_H_DAY, SKY_M_DAY, SKY_Z_DAY, SKY_H_DUSK, SKY_M_DUSK, SKY_Z_DUSK, SKY_H_NIGHT, SKY_M_NIGHT, SKY_Z_NIGHT,
        CLOUD_TOP, CLOUD_SHADE, STATION_MINT, STATION_PEACH, STATION_LILAC,
        EARTH_OCEAN, EARTH_LAND, ATMO_RIM, PLANET_A, PLANET_B, PLANET_C, BEACON,
        SUN_5600, SUN_6200, SUN_6500,
    }

    [Serializable]
    public struct PaletteToken
    {
        public string name;
        public string hex;
        public Color linear;   // linear-light RGB (what shaders receive)
    }

    [CreateAssetMenu(menuName = "Megastructure/Thread Palette")]
    public class ThreadPalette : ScriptableObject
    {
        // name, sRGB hex. PLANET_A = CORAL and PLANET_C = TEAL by specification.
        public static readonly (Pal id, string hex)[] Table =
        {
            (Pal.SALT_WHITE, "#F2EDE2"), (Pal.SAND, "#E3C79A"), (Pal.SAND_SHADE, "#C9A777"),
            (Pal.CORAL, "#E8745E"), (Pal.CORAL_DEEP, "#B9483F"), (Pal.TEAL, "#2E8C8A"), (Pal.TEAL_DEEP, "#1B5560"),
            (Pal.INK, "#1D2233"), (Pal.CREAM, "#FBF4E4"),
            (Pal.SKY_H_DAY, "#F3E4D0"), (Pal.SKY_M_DAY, "#BFD9E3"), (Pal.SKY_Z_DAY, "#6FAFD3"),
            (Pal.SKY_H_DUSK, "#F2C9A6"), (Pal.SKY_M_DUSK, "#7F86B8"), (Pal.SKY_Z_DUSK, "#2F3A7A"),
            (Pal.SKY_H_NIGHT, "#1E2A4F"), (Pal.SKY_M_NIGHT, "#0A0F26"), (Pal.SKY_Z_NIGHT, "#04050D"),
            (Pal.CLOUD_TOP, "#FFF6EA"), (Pal.CLOUD_SHADE, "#C9B8D6"),
            (Pal.STATION_MINT, "#A8DCCB"), (Pal.STATION_PEACH, "#F7C1A4"), (Pal.STATION_LILAC, "#C7B6E2"),
            (Pal.EARTH_OCEAN, "#2C6E9E"), (Pal.EARTH_LAND, "#D9B982"), (Pal.ATMO_RIM, "#7FC4F0"),
            (Pal.PLANET_A, "#E8745E"), (Pal.PLANET_B, "#F0D9B0"), (Pal.PLANET_C, "#2E8C8A"),
            (Pal.BEACON, "#FFD9A0"),
            (Pal.SUN_5600, "#FFF2DE"), (Pal.SUN_6200, "#FFF7EA"), (Pal.SUN_6500, "#FFFBF4"),
        };

        public List<PaletteToken> tokens = new List<PaletteToken>();

        static Color[] s_cache;

        /// <summary>sRGB 8-bit hex to linear-light colour with the exact piecewise sRGB EOTF.</summary>
        public static Color HexToLinear(string hex)
        {
            string h = hex.TrimStart('#');
            int r = Convert.ToInt32(h.Substring(0, 2), 16), g = Convert.ToInt32(h.Substring(2, 2), 16), b = Convert.ToInt32(h.Substring(4, 2), 16);
            return new Color((float)SrgbToLinear(r / 255.0), (float)SrgbToLinear(g / 255.0), (float)SrgbToLinear(b / 255.0), 1f);
        }

        public static double SrgbToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        public static double LinearToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;

        /// <summary>Linear colour of a token from the code table (identical to the asset by construction; T-PAL checks it).</summary>
        public static Color Linear(Pal id)
        {
            if (s_cache == null)
            {
                var c = new Color[Table.Length];
                foreach (var t in Table) c[(int)t.id] = HexToLinear(t.hex);
                s_cache = c;
            }
            return s_cache[(int)id];
        }

        public static Vector3 LinearV(Pal id) { var c = Linear(id); return new Vector3(c.r, c.g, c.b); }

        public void FillFromTable()
        {
            tokens.Clear();
            foreach (var t in Table)
                tokens.Add(new PaletteToken { name = t.id.ToString(), hex = t.hex, linear = HexToLinear(t.hex) });
        }

        public Color Get(Pal id)
        {
            string n = id.ToString();
            foreach (var t in tokens) if (t.name == n) return t.linear;
            return Linear(id);
        }
    }
}
