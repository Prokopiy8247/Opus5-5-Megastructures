using System.Collections.Generic;
using Megastructure.Beats;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Materials owned by beats 5-6 (owner BJ): M06 RingWall (wall, plinth), the apron (M01 with 4 m panels), M08
    /// RingTunnel (shell, and the door recess in wall ceramic), the tunnel strips / markers (M12 amber, black body), the
    /// climber point and the additive pools / mouth spill. Colours are linear palette values (14.2.1).
    /// </summary>
    public sealed class Beat56MaterialDefs : IRingMaterialDefs
    {
        const string WallShader = "Megastructure/RingWall";
        const string TunnelShader = "Megastructure/RingTunnel";
        const string ClimberShader = "Megastructure/RingWallClimber";
        const string GlowShader = "Megastructure/RingTunnelGlow";
        const string CeramicShader = "Megastructure/RingCeramic";
        const string EmissiveShader = "Megastructure/RingEmissive";

        // 14.2.6 M08 "amber strips 0.5 nit": AmberLamp x 0.5 (the EmissiveAmber level; local until RingConstants carries it).
        const float AmberHdr = 0.5f;

        // Door recess albedo on M08 relative to WallTone: RingWall gives base x foot shade x lerp(0.6, 1, ao), RingTunnel
        // gives base x lerp(0.75, 1, ao); over the recess (AO 0.54-1, mostly walls and crown) 0.92 matches the area-weighted
        // response (0.85 would match only the deepest AO corners).
        const float RecessAlbedo = 0.92f;

        // Render queues: additive after every opaque surface (RenderQueue.Transparent = 3000); the climber like M13 (+10).
        const int QueuePool = 3000;
        const int QueueClimber = 3010;

        // 14.6 5->6: NearPlain is switched off at s 810 in darkness; the pools seen through the door fade out first.
        const float PoolFadeS0 = 780f, PoolFadeS1 = 806f;

        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        public IEnumerable<RingMaterialDef> Defs()
        {
            Color white = new Color(1f, 1f, 1f, 1f);

            // M06 RingWall: WallTone albedo at all times (the night look comes from the lights, never from an albedo swap).
            // Joint arbitration (B56): 0.25 m joints with a 0.5 m lower-lip shadow (the spec's 6 mm groove is 0.06 px at
            // 84 m), filtered and faded below 4 px spacing; 4 m hairline courses (3.5 cm) only near the foot (envelope
            // e-folding 120 m); panel tone +-2.5 %; foot glow x1.2 at y 0 (e-folding 20 m); height haze toward the fog
            // colour up to 85 % (e-folding 160 m): measured (b56-01, WallTone under the beat-5 lights) the bare wall is
            // ~110 luma, so the gradient, not the glow, carries the foot-to-top fall (KS07 mean <= 100 with the wall at
            // 73 % of the frame). Soft highlight clamp (linear, pre-exposure): luminance <= 0.20 and channels <= 0.60
            // keep every wall pixel under luma ~190 at the beat-5 exposure, knee at 0.12.
            Vector4 seam = new Vector4(RingConstants.WallSeam, 0.25f, 0.72f, 4f);
            Vector4 seamShadow = new Vector4(0.5f, 0.22f, 0.4f, 0f);
            Vector4 footGlow = new Vector4(0.20f, 20f, 0f, 0f);
            Vector4 heightHaze = new Vector4(0.85f, 160f, 0f, 0f);
            Vector4 highlight = new Vector4(0.12f, 0.20f, 0.60f, 0f);
            yield return new RingMaterialDef(RingMat.Wall, WallShader)
                .Col("_BaseColor", RingPalette.Lin("WallTone"))
                .Col("_Tint", white)
                .F("_Smoothness", 0.35f)
                .Vec("_Seam", seam)
                .Vec("_SeamShadow", seamShadow)
                .Vec("_Course", new Vector4(4f, 0.035f, 0.10f, 120f))
                .Vec("_PanelTone", new Vector4(0.025f, 6f, 0f, 0f))
                .Vec("_FootGlow", footGlow)
                .Vec("_HeightHaze", heightHaze)
                .F("_FootAO", 0.10f)
                .F("_TopShade", 1f)
                .F("_AO", 1f)
                .Vec("_Highlight", highlight);

            // Plinth: the same ceramic one step darker (reads as a base course), the vertical seams run through it, the
            // same foot glow and haze as the wall face above; no courses, no panel tone; its top and chamfer are shaded
            // (x 0.75 on up-facing faces) so the 2 cm chamfer and the 0.35 m top never read as a bright 1 px line.
            yield return new RingMaterialDef(Beat56Ids.WallPlinth, WallShader)
                .Col("_BaseColor", RingPalette.Lin("WallTone"))
                .Col("_Tint", Rgb(white, 0.80f))
                .F("_Smoothness", 0.30f)
                .Vec("_Seam", seam)
                .Vec("_SeamShadow", seamShadow)
                .Vec("_Course", new Vector4(4f, 0.035f, 0f, 120f))
                .Vec("_PanelTone", new Vector4(0f, 6f, 0f, 0f))
                .Vec("_FootGlow", footGlow)
                .Vec("_HeightHaze", heightHaze)
                .F("_FootAO", 0f)
                .F("_TopShade", 0.75f)
                .F("_AO", 1f)
                .Vec("_Highlight", highlight);

            // Apron: 4 x 4 m stone slabs with 8 mm joints (14.5.5), the Stone recipe with 4 m panels.
            yield return new RingMaterialDef(Beat56Ids.Apron, CeramicShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicShade"), 1.08f))
                .Col("_JointColor", Rgb(RingPalette.Lin("CeramicShade"), 0.72f))
                .F("_Smoothness", 0.3f)
                .Vec("_PanelSize", new Vector4(4f, 4f, 4f, 0f))
                .F("_PanelLine", 0.008f)
                .F("_AO", 1f)
                .F("_EdgeWear", 0.6f);

            // M08 RingTunnel: TunnelGrey, smoothness 0.2, vertex AO, amber strip light (AmberLamp x 0.5 strips), bounce fill.
            // Calibrated for EV +2: wall base next to a strip ~40 levels, mid wall ~16, crown ~10, darkest AO corners >= 6.
            Color fill = new Color(1f, 0.80f, 0.62f, 1f);
            Vector4 strip = new Vector4(0.97f, 0.006f, 0.06f, 0.03f);
            Vector4 bleed = new Vector4(0.065f, 0.12f, 0.08f, 0f);
            Vector4 axis = new Vector4(660f, 114.85f, 233.95f, 0f);
            Vector4 marker = new Vector4(RingConstants.TunnelRibX0, RingConstants.TunnelRibPitch, RingConstants.TunnelRibs - 1, 1.038f);
            Vector4 markerShape = new Vector4(0.5f, 0.18f, 0f, 0f);
            Vector4 openings = new Vector4(RingConstants.WallX, RingConstants.WallOuterX, 1.3f, 0f);
            Vector4 profile = new Vector4(0.5f * RingConstants.DoorW, RingConstants.DoorStraight, 0f, 0f);
            // variation along the tube (B56): section ribs (vertex B) in a lighter warm grey (~2.2x TunnelGrey), floor
            // rungs at the section ribs (k = 4 mod 5) and at every rib from k 15, with a floor bleed like the strips'
            Color trim = Rgb(RingPalette.Lin("CeramicShade"), 0.12f);
            Vector4 rung = new Vector4(1.0f, 0.10f, 0.93f, 0f);
            Vector4 rungSel = new Vector4(5f, 4f, 15f, 0f);
            yield return new RingMaterialDef(RingMat.Tunnel, TunnelShader)
                .Col("_BaseColor", RingPalette.Lin("TunnelGrey"))
                .F("_Smoothness", 0.2f)
                .F("_AO", 1f)
                .Col("_StripColor", RingPalette.Lin("AmberLamp"))
                .F("_StripLight", 16f)
                .Vec("_Strip", strip)
                .F("_StripFalloff", 1.0f)
                .Vec("_Bleed", bleed)
                .Vec("_Axis", axis)
                .F("_MarkerLight", 1.3f)
                .Vec("_Marker", marker)
                .Vec("_MarkerShape", markerShape)
                .Col("_FillColor", fill)
                .F("_FillLight", 0.032f)
                .Col("_TrimColor", trim)
                .Vec("_Rung", rung)
                .Vec("_RungSel", rungSel)
                .Vec("_Openings", openings)
                .Vec("_Profile", profile)
                .F("_ShadowCull", 0f);

            // Door recess (x 113.99..114.8, always-on Tunnel set): the wall ceramic (WallTone, like M06) on M08, so
            // RingTunnelVisibility gates every directional light. The beat-5 wash and key (L.x < 0) still enter through the door plane; the beat-7 key L7 (L.x > 0, unshadowed beyond the 90 m shadow distance) and the
            // arch fill (L.x > 0, physically blocked by the wall above) would have to cross the tube and give 0.
            // No strip / marker / fill light: the recess keeps its beat-5 response (no emission, like M06). Its door plane is
            // the frame front (x 113.98), so the whole recess (x >= 113.99) is gated, including the 1 cm at the frame chamfer.
            Vector4 recessOpenings = new Vector4(RingConstants.WallX - Beat05WallBuilder.FrameProud, RingConstants.WallOuterX, 1.3f, 0f);
            yield return new RingMaterialDef(Beat56Ids.DoorRecess, TunnelShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("WallTone"), RecessAlbedo))
                .F("_Smoothness", 0.35f)
                .F("_AO", 1f)
                .Col("_StripColor", RingPalette.Lin("AmberLamp"))
                .F("_StripLight", 0f)
                .Vec("_Strip", strip)
                .F("_StripFalloff", 1.0f)
                .Vec("_Bleed", bleed)
                .Vec("_Axis", axis)
                .F("_MarkerLight", 0f)
                .Vec("_Marker", marker)
                .Vec("_MarkerShape", markerShape)
                .Col("_FillColor", fill)
                .F("_FillLight", 0f)
                .Col("_TrimColor", Rgb(RingPalette.Lin("WallTone"), RecessAlbedo))
                .Vec("_Rung", Vector4.zero)
                .Vec("_RungSel", rungSel)
                .Vec("_Openings", recessOpenings)
                .Vec("_Profile", profile)
                .F("_ShadowCull", 0f);

            // Tunnel strips and markers: EmissiveAmber's emission (AmberLamp x 0.5, always on) with a black body. RingEmissive
            // is lit by the plain shared model, so a lens albedo would let the beat-7 key L7 brighten the strips along the
            // whole tube beyond the 90 m shadow distance (~20 % on their top faces); the emission dominates everywhere else.
            yield return new RingMaterialDef(Beat56Ids.TunnelAmber, EmissiveShader)
                .Col("_BaseColor", new Color(0f, 0f, 0f, 1f))
                .F("_Smoothness", 0.35f)
                .Col("_EmissionColor", RingPalette.Lin("AmberLamp"))
                .F("_Intensity", AmberHdr)
                .F("_UseLampLevel", 0f).F("_UseCityLevel", 0f).F("_UseBeaconPulse", 0f).F("_UseSpill", 0f)
                .F("_MinLevel", 0f).F("_AO", 1f).F("_Cull", 2f);

            // Climber (14.5.5): WarmLamp, constant 1.4 mrad footprint (FWHM; 2.3 px at 4K, vFOV 66), drawn >= 2 px
            // with energy kept, faint 0.6 degree tail (5 % of the peak, fading as (1 - t)^2); elevation 8 -> 80 degrees
            // over s 594-774 seen from the eye, on the column z 700 at x 113.98. The sprite is pulled 30 % of its distance
            // toward the camera so grazing views along the wall never clip it (nothing else lies in that range).
            yield return new RingMaterialDef(Beat56Ids.Climber, ClimberShader).Queue(QueueClimber)
                .Col("_Color", RingPalette.Lin("WarmLamp"))
                .F("_Intensity", Beat05WallBuilder.ClimberHdr)   // 1.2, see Beat05WallBuilder.ClimberHdr
                .F("_AngularSize", Beat05WallBuilder.ClimberFootprint)
                .F("_MinPx", 2f)
                .F("_TailRad", Beat05WallBuilder.ClimberTailDeg * Mathf.Deg2Rad)
                .F("_TailGain", 0.05f)
                .Vec("_Climb", new Vector4(RingConstants.BeatS[4], RingConstants.BeatS[5], Beat05WallBuilder.ClimberEps0, Beat05WallBuilder.ClimberEps1))
                .Vec("_Anchor", new Vector4(Beat05WallBuilder.ClimberX, RingConstants.ClimberZ, RingConstants.Eye, RingConstants.WallX))
                .F("_DepthPull", 0.3f);

            // Mouth spill (14.5.6): the outside light reaching into the last 12 m, x _Lamps.w (0 at s 854 -> 1 at s 888),
            // capped at 1.0 (pre-exposure) so the clipped fraction stays well under 12 %. Critic S3 (run04): at 0.5 in
            // StarOutside the last 12 m read as a flat grey corridor (KS11 walls ~62, floor ~100): peak 0.3 and tinted
            // halfway to WarmLamp, so the tube stays dark and warm up to the threshold (walls ~40).
            Color spill = Color.Lerp(RingPalette.Lin("StarOutside"), RingPalette.Lin("WarmLamp"), 0.5f);
            yield return new RingMaterialDef(Beat56Ids.TunnelSpill, GlowShader).Queue(QueuePool)
                .Col("_Color", spill)
                .F("_Intensity", 0.3f)
                .F("_Mode", 1f)
                .F("_LampW", 0f)
                .F("_SpillW", 1f)
                .F("_Cap", 1.0f)
                .F("_SpillFalloff", 3.2f)
                .Vec("_FadeS", Vector4.zero);

            // Door and plain bollard light pools: tint and peak per vertex (WarmLamp x 0.12-0.18), x _Lamps.x / 3, faded
            // out over s 780-806 inside the tunnel (the door pool is visible through the door until NearPlain goes off
            // at s 810; 14.6 5->6 "at s 810 the plain is dark").
            yield return new RingMaterialDef(Beat56Ids.PlainPool, GlowShader).Queue(QueuePool)
                .Col("_Color", white)
                .F("_Intensity", 1f)
                .F("_Mode", 0f)
                .F("_LampW", 1f)
                .F("_SpillW", 0f)
                .F("_Cap", 1.0f)
                .F("_SpillFalloff", 3.2f)
                .Vec("_FadeS", new Vector4(PoolFadeS0, PoolFadeS1, 1f, 0f));
        }
    }
}
