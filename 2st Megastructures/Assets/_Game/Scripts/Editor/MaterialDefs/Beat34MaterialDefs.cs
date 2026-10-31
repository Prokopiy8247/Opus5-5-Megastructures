using System.Collections.Generic;
using Megastructure.Beats;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Materials owned by beats 3-4 (owner BI): M07 RingArk (hull, window band, engines: one shader, _Mode 0 / 1 / 2),
    /// the footbridge ashlar (RingCeramic without joints), the coursed stone of the levee and the footbridge and the turf
    /// (RingUnderpass masonry modes), the underpass lining and ribs (RingUnderpass, Beats/Beat04_Sunset) and the beat-4
    /// lamp lenses (RingEmissive). Colours are linear palette values (14.2.1, RingPalette). Other materials used by these
    /// beats come from their owners: Stone, Ceramic, CeramicShade, PaleGold, PathStrip (lead), Emissive, EmissiveAmber,
    /// LightPool, Glow (rendering foundations).
    /// </summary>
    public sealed class Beat34MaterialDefs : IRingMaterialDefs
    {
        const string ArkShader = "Megastructure/RingArk";
        const string CeramicShader = "Megastructure/RingCeramic";
        const string UnderpassShader = "Megastructure/RingUnderpass";

        // RingArk _Mode values
        const float ModeHull = 0f, ModeWindows = 1f, ModeEngine = 2f;

        // 14.5.3 M07: panel lines 15 m (0.35 m wide, 35 % darker, fading below 6 px spacing); windows 12 x 5 m at 18 m
        // pitch on 14 m decks, grid above 8 px, pure band below 4 px; engines EngineBlue HDR 6.
        const float PanelPitch = 15f, PanelLine = 0.35f, PanelDark = 0.35f, PanelFadePx = 6f;
        const float WindowHdr = 2.8f;   // window radiance at level 1 (x WindowWarm); level 0.4 + 0.6 N from _ArkParams.y
        const float EngineHdr = 6f;
        // Underside from object n.y 0.1 (lower flank) to -0.45 (full), belly specular occlusion 0.7 (the belly sees the
        // meadow, not the horizon), structure (keel, spars) at 0.85 x ArkHull so it reads against the belly.
        static readonly Vector4 ArkShade = new Vector4(0.1f, -0.45f, 0.7f, 0.85f);
        // Star rim gain / power (backlit sheen of the silhouette, key colour), weak arch bounce (x trilight), self floor
        // (x albedo): the mass stays dark, the star-side edge carries a highlight, no face is ever black (14.5.3).
        static readonly Vector4 ArkRim = new Vector4(0.16f, 3f, 0.12f, 0.02f);

        // Masonry (RingUnderpass _Masonry.x 1): 0.6 x 0.3 m courses in running bond, 2 cm joints, +-7 % per-block tone,
        // 25 % recess darkening. The levee is a pale grey limestone and the footbridge a warmer sandstone, both clearly
        // apart from the taupe aqueduct stone (RingMat.Stone, CeramicShade x 1.08).
        static readonly Vector4 Courses = new Vector4(0.6f, 0.3f, 0.6f, 0f);
        const float CourseJoint = 0.02f;
        static readonly Vector4 MasonryCourses = new Vector4(1f, 0.07f, 0.25f, 0f);
        static readonly Vector4 MasonryTurf = new Vector4(2f, 0.16f, 0f, 0f);
        static readonly Vector4 MasonryTiles = new Vector4(0f, 0f, 0f, 0f);
        static readonly Color LeveeStone = new Color(0.60f, 0.585f, 0.545f, 1f);    // linear: pale grey limestone
        static readonly Color BridgeStone = new Color(0.58f, 0.50f, 0.39f, 1f);     // linear: warm sandstone

        // Beat-4 lamps: Beat34Mat.WarmLampDisplay x Beat34Mat.LampLevel (shared with the pool tints, see ArkSpec.cs).

        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>RingUnderpass in a masonry mode with full exposure (no strip light): courses or turf.</summary>
        static RingMaterialDef Masonry(string id, Color baseCol, Color joint, Vector4 mode, float smoothness, float panel, float line)
        {
            Vector4 size = panel > 0f ? new Vector4(panel, panel, panel, 0f) : Courses;
            return new RingMaterialDef(id, UnderpassShader)
                .Col("_BaseColor", baseCol)
                .Col("_JointColor", joint)
                .F("_Smoothness", smoothness)
                .Vec("_PanelSize", size)
                .F("_PanelLine", line)
                .F("_AO", 1f)
                .F("_EdgeWear", 0.6f)
                .Col("_StripColor", RingPalette.Lin("AmberLamp"))
                .F("_StripLight", 0f)
                .Col("_FillColor", RingPalette.Lin("Practical2700"))
                .F("_FillLight", 0f)
                .Vec("_Masonry", mode)
                .F("_Cull", 2f);
        }

        /// <summary>Turns on the in-shader wash of the levee's terrace sconces (LeveeMeshes.SconceLight, 8 vectors):
        /// window 4.5 m, r0^2 0.3 m^2, downlight bias 0.7, WarmLampDisplay, gain LeveeMeshes.SconceGain.</summary>
        static RingMaterialDef Sconces(RingMaterialDef d)
        {
            d.Col("_SconceColor", Beat34Mat.WarmLampDisplay).Vec("_SconceParams", new Vector4(4.5f, 0.3f, 0.7f, 1f));
            for (int i = 0; i < 8; i++)
            {
                Vector4 v = new Vector4(0f, -1000f, 0f, 0f);
                if (i < LeveeMeshes.SconceCount)
                {
                    Vector3 p = LeveeMeshes.SconceLight(i);
                    v = new Vector4(p.x, p.y, p.z, LeveeMeshes.SconceGain);
                }
                d.Vec("_Sconce" + i, v);
            }
            return d;
        }

        static RingMaterialDef Ark(string id, float mode)
        {
            return new RingMaterialDef(id, ArkShader)
                .F("_Mode", mode)
                .Col("_HullColor", RingPalette.Lin("ArkHull"))
                .Col("_UnderColor", RingPalette.Lin("ArkUnderside"))
                .F("_Smoothness", 0.25f)
                .Vec("_Panel", new Vector4(PanelPitch, PanelLine, PanelDark, PanelFadePx))
                .Vec("_Shade", ArkShade)
                .Vec("_Rim", ArkRim)
                .Col("_WindowColor", RingPalette.Lin("WindowWarm"))
                .F("_WinIntensity", WindowHdr)
                .Vec("_WinCell", new Vector4(ArkSpec.WinPitch, ArkSpec.RowPitch, ArkSpec.WinW, ArkSpec.WinH))
                .Vec("_WinFade", new Vector4(4f, 8f, ArkSpec.WindowMeanLevel(), 0.5f))
                .Col("_EngineColor", RingPalette.Lin("EngineBlue"))
                .F("_EngineIntensity", EngineHdr)
                .F("_Cull", 2f);
        }

        public IEnumerable<RingMaterialDef> Defs()
        {
            // M07 RingArk
            yield return Ark(RingMat.ArkHull, ModeHull);
            yield return Ark(RingMat.ArkWindow, ModeWindows);
            yield return Ark(Beat34Mat.ArkEngine, ModeEngine);

            // Turf (levee berms and tops, footbridge mound): LeveeGreen with the two-octave mottle (RingUnderpass masonry
            // mode 2), matte, full exposure (vertex A = 1).
            yield return Sconces(Masonry(Beat34Mat.Turf, RingPalette.Lin("LeveeGreen"), Rgb(RingPalette.Lin("LeveeGreen"), 0.8f), MasonryTurf, 0.12f, 1000f, 0f));

            // Ashlar: dressed blocks with real joints (voussoirs, jambs, footbridge copings and wing copings); the 1.2 cm
            // chamfers catch the light (edge wear). Paler than the coursed walls so the arch ring frames the dark portal.
            yield return new RingMaterialDef(Beat34Mat.Ashlar, CeramicShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicWhite"), 0.82f))
                .Col("_JointColor", Rgb(RingPalette.Lin("CeramicShade"), 0.72f))
                .F("_Smoothness", 0.3f)
                .Vec("_PanelSize", new Vector4(1000f, 1000f, 1000f, 0f))
                .F("_PanelLine", 0f)
                .F("_AO", 1f)
                .F("_EdgeWear", 0.8f);

            // Coursed stone (14.5.3 footbridge "stone courses 0.6 x 0.3 m (triplanar, fwidth-filtered)"; 14.5.4 levee faces).
            yield return Sconces(Masonry(Beat34Mat.LeveeStone, LeveeStone, Rgb(LeveeStone, 0.55f), MasonryCourses, 0.3f, 0f, CourseJoint));
            // Levee copings and stair cheek walls: pale ceramic without joints, lit by the sconces like the stone.
            yield return Sconces(Masonry(Beat34Mat.LeveeTrim, Rgb(RingPalette.Lin("CeramicWhite"), 0.9f), RingPalette.Lin("CeramicShade"), MasonryTiles, 0.45f, 1000f, 0f));
            yield return Masonry(Beat34Mat.BridgeStone, BridgeStone, Rgb(BridgeStone, 0.55f), MasonryCourses, 0.3f, 0f, CourseJoint);

            // Beat-4 lamp lenses (bollard heads, mouth lamp, terrace sconces): RingEmissive on the lamp level _Lamps.x / 3.
            yield return new RingMaterialDef(Beat34Mat.Lamp, "Megastructure/RingEmissive")
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicWhite"), 0.92f))
                .F("_Smoothness", 0.45f)
                .Col("_EmissionColor", Beat34Mat.WarmLampDisplay)
                .F("_Intensity", Beat34Mat.LampLevel)
                .F("_UseLampLevel", 1f).F("_UseCityLevel", 0f).F("_UseBeaconPulse", 0f).F("_UseSpill", 0f)
                .F("_MinLevel", 0f).F("_AO", 1f).F("_Cull", 2f);

            // Underpass lining: warm grey ceramic, 1.5 x 0.75 m tiles, lit by the baked amber strips (AmberLamp) and
            // a faint warm bounce; directional sky light only near the portals (vertex exposure).
            yield return new RingMaterialDef(Beat34Mat.UnderpassLining, UnderpassShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicShade"), 0.9f))
                .Col("_JointColor", Rgb(RingPalette.Lin("CeramicShade"), 0.62f))
                .F("_Smoothness", 0.4f)
                .Vec("_PanelSize", new Vector4(1.5f, 0.75f, 1.5f, 0f))
                .F("_PanelLine", 0.008f)
                .F("_AO", 1f)
                .F("_EdgeWear", 0.6f)
                .Col("_StripColor", RingPalette.Lin("AmberLamp"))
                .F("_StripLight", 0.12f)
                .Col("_FillColor", RingPalette.Lin("Practical2700"))
                .F("_FillLight", 0.004f)
                .Vec("_Masonry", MasonryTiles)
                .F("_Cull", 2f);

            // Underpass ribs: a lighter ceramic band every 3 m, no joints, chamfer wear.
            yield return new RingMaterialDef(Beat34Mat.UnderpassRib, UnderpassShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicWhite"), 0.85f))
                .Col("_JointColor", Rgb(RingPalette.Lin("CeramicShade"), 0.7f))
                .F("_Smoothness", 0.45f)
                .Vec("_PanelSize", new Vector4(1000f, 1000f, 1000f, 0f))
                .F("_PanelLine", 0f)
                .F("_AO", 1f)
                .F("_EdgeWear", 0.8f)
                .Col("_StripColor", RingPalette.Lin("AmberLamp"))
                .F("_StripLight", 0.12f)
                .Col("_FillColor", RingPalette.Lin("Practical2700"))
                .F("_FillLight", 0.004f)
                .Vec("_Masonry", MasonryTiles)
                .F("_Cull", 2f);
        }
    }
}
