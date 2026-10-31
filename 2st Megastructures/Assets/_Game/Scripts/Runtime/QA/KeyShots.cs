using System;
using System.Globalization;
using UnityEngine;

namespace Megastructure
{
    /// <summary>One key shot of the 14.10 table.</summary>
    public struct KeyShot
    {
        public int Index;          // 0..15
        public string Id;          // "KS02"
        public string Tag;         // "valley_crest" (file-name suffix)
        public float S;            // progress
        public Vector3 Eye;
        public float Yaw, Pitch;   // degrees (yaw 0 = +Z, +90 = +X; pitch up positive)
        public float Vfov;         // vertical FOV at 16:9 (the only value a capture may use)
        public double T;           // forced time (NaN = derived from progress in capture mode)
        public bool ExtraAspects;  // also 1680x1050 (16:10) and 2560x1080 (21:9)

        /// <summary>Beat number 1..7.</summary>
        public int Beat => RingConstants.BeatAt(S) + 1;
    }

    /// <summary>
    /// The key-shot table of MASTER_PROMPT 14.10, the ONLY source of capture poses and vFOV (KS00..KS15), plus the
    /// per-shot composition subjects taken from the KEY FRAME text of 14.5 (Part 10 #35). Resolutions: 1920x1080 and
    /// 3840x2160 for every key shot; 1680x1050 and 2560x1080 also for KS02, KS06 and KS12 (14.10, Part 11.2).
    /// </summary>
    public static class KeyShots
    {
        public static readonly KeyShot[] All =
        {
            K(0,  "title",          0f,   0f,    2.6f,  0f,    0f,   -2f, 60f, 3.0),
            K(1,  "capsule_window", 5.1f, -0.88f, 2.60f, 5.07f, -45f, 25f, 60f),
            K(2,  "valley_crest",   150f, 0f,    4.7f,  150f,  0f,   14f, 64f, double.NaN, true),
            K(3,  "arch_up",        150f, 0f,    4.7f,  150f,  6f,   38f, 64f),
            K(4,  "ark_shadow",     364f, 0f,    1.7f,  364f,  -12f, 14f, 66f),
            K(5,  "sunset_squares", 442f, 0f,    1.7f,  442f,  -58f, 20f, 64f),
            K(6,  "night_arc",      505f, 0f,    1.7f,  505f,  0f,   28f, 64f, double.NaN, true),
            K(7,  "wall_reveal",    606f, 30f,   1.7f,  576f,  80f,  16f, 66f),
            K(8,  "wall_foot",      700f, 100f,  1.7f,  600f,  20f,  30f, 66f),
            K(9,  "wall_door",      752f, 100f,  1.7f,  652f,  60f,  2f,  66f),
            K(10, "tunnel_axis",    800f, 140f,  1.7f,  660f,  90f,  0f,  60f),
            K(11, "tunnel_exit",    888f, 228f,  1.7f,  660f,  90f,  0f,  60f),
            K(12, "swarm",          912f, 252f,  1.7f,  660f,  92f,  9f,  66f, double.NaN, true),
            K(13, "hull_down",      912f, 252f,  1.7f,  660f,  90f,  -28f, 66f),
            K(14, "beacon",         956f, 296f,  1.7f,  660f,  90f,  3f,  66f),
            K(15, "doorway_back",   900f, 240f,  1.7f,  660f,  270f, 20f, 61.3f),
        };

        static KeyShot K(int i, string tag, float s, float x, float y, float z, float yaw, float pitch, float vfov,
                         double t = double.NaN, bool extra = false)
        {
            return new KeyShot
            {
                Index = i, Id = "KS" + i.ToString("00", CultureInfo.InvariantCulture), Tag = tag, S = s,
                Eye = new Vector3(x, y, z), Yaw = yaw, Pitch = pitch, Vfov = vfov, T = t, ExtraAspects = extra
            };
        }

        public static readonly Vector2Int Res1080 = new Vector2Int(1920, 1080);
        public static readonly Vector2Int Res4K = new Vector2Int(3840, 2160);
        public static readonly Vector2Int Res1610 = new Vector2Int(1680, 1050);
        public static readonly Vector2Int Res219 = new Vector2Int(2560, 1080);

        /// <summary>Capture resolutions of a key shot (14.10).</summary>
        public static Vector2Int[] ResolutionsFor(KeyShot k)
        {
            return k.ExtraAspects ? new[] { Res1080, Res4K, Res1610, Res219 } : new[] { Res1080, Res4K };
        }

        /// <summary>ksNN_&lt;tag&gt;_&lt;W&gt;x&lt;H&gt;.png</summary>
        public static string FileName(KeyShot k, int w, int h, string suffix = null)
        {
            return "ks" + k.Index.ToString("00", CultureInfo.InvariantCulture) + "_" + k.Tag + (suffix ?? "") + "_" +
                   w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture) + ".png";
        }

        /// <summary>Finds a key shot by id ("KS02", "ks2", "2") or tag ("valley_crest").</summary>
        public static bool TryFind(string token, out KeyShot k)
        {
            k = default;
            if (string.IsNullOrEmpty(token)) return false;
            string t = token.Trim();
            foreach (var x in All)
                if (string.Equals(x.Id, t, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Tag, t, StringComparison.OrdinalIgnoreCase)) { k = x; return true; }
            string digits = t.StartsWith("ks", StringComparison.OrdinalIgnoreCase) ? t.Substring(2) : t;
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 && n < All.Length) { k = All[n]; return true; }
            return false;
        }

        /// <summary>
        /// Composition subjects per key shot from the 14.5 KEY FRAME text (Part 10 #35). A leading '~' means "must be
        /// visible" (any part in frame) instead of "fully inside with the 3 % margin". "A|B" lists alternatives: the
        /// first registry key found wins (as is, "Subject." + name, "Subject." + name without dots), then the first
        /// analytic definition of <see cref="CompositionCheck"/>. Registry keys "Subject.KSnn.*" are added to that shot
        /// automatically; other "Subject.*" keys are recorded wherever they are in frame (informational).
        /// KS00 the hatch ahead (the title is screen-space UI); KS01 the 0.55 m porthole opening; KS02 the footbridge arch
        /// 250 m ahead and the terrain edge at 14.67 deg; KS03 the terrain edge at the bottom of the arch view; KS04 the
        /// ship; KS05 the star (10 deg left, 6 deg up) and pavilion P2; KS06 the lit levee mouth; KS07 / KS09 the door;
        /// KS08 the wall face; KS10 / KS11 the tunnel mouth; KS12 the beacon, the outside star and every band ellipse
        /// (largest radius 16.7 deg); KS13 the hull horizon 11.4 deg below the eye line; KS14 the beacon; KS15 the doorway.
        /// </summary>
        public static string[] SubjectsFor(KeyShot k)
        {
            switch (k.Index)
            {
                case 0: return new[] { "Capsule.Hatch" };
                case 1: return new[] { "Porthole.Opening" };
                case 2: return new[] { "Footbridge.Opening", "Terrain.EdgeAhead" };
                case 3: return new[] { "Terrain.EdgeAhead" };
                case 4: return new[] { "Ark.Hull|Subject.Ark" };
                case 5: return new[] { "Star", "Pavilion.P2" };
                case 6: return new[] { "Underpass.Mouth" };
                case 7: return new[] { "Door.Opening|Subject.Door" };
                case 8: return new[] { "~Subject.Wall|Wall" };
                case 9: return new[] { "Door.Opening|Subject.Door" };
                case 10: return new[] { "Tunnel.Mouth|Subject.TunnelMouth" };
                case 11: return new[] { "Tunnel.Mouth|Subject.TunnelMouth" };
                case 12: return new[] { "Beacon", "StarOut", "Swarm.Bands" };
                case 13: return new[] { "Hull.Horizon" };
                case 14: return new[] { "Beacon" };
                case 15: return new[] { "~Tunnel.Mouth|Subject.TunnelMouth" };
                default: return new string[0];
            }
        }
    }
}
