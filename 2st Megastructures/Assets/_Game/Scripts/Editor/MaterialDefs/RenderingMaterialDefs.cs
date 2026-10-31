using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Rendering-foundation materials (owner BF): M12 emissive family, the additive family (M13 motes, light pools,
    /// glows) and the final static dither of 14.7 H5. Colours are linear palette values (14.2.1, RingPalette).
    /// </summary>
    public sealed class RenderingMaterialDefs : IRingMaterialDefs
    {
        const string EmissiveShader = "Megastructure/RingEmissive";
        const string AdditiveShader = "Megastructure/RingAdditive";
        const string DitherShader = "Megastructure/RingFinalDither";

        // Render queues (RenderQueue.Transparent = 3000). 14.2.6 M13: motes at Transparent+10. Additive blending is
        // order-independent; the queues only keep them after every opaque surface and the water.
        const int QueuePool = 3000;
        const int QueueGlow = 3005;
        const int QueueMote = 3010;

        // RingAdditive _Mode values
        const float ModeMote = 0f, ModeGlow = 1f, ModeLightPool = 2f;

        // 14.2.1: lamps WarmLamp x3.0; 14.2.6 M08 / task: amber strips and markers AmberLamp x1 at 0.5
        // (palette multipliers; local until RingConstants carries them, contract request to the lead)
        const float LampHdr = 3.0f;
        const float AmberHdr = 0.5f;
        // 14.12 "Dither: one final static pass, 1.5 LSB"; 14.7 H5 (1.5 / 255 in the display-encoded domain).
        // A 14.12 number: local until RingConstants.DitherLsb exists (contract request to the lead), then reference it.
        const float DitherLsb = 1.5f;
        // Mote and glow sizes, drift and pool numbers come from LightPoolKit (their single C# source, 14.2.3, 14.2.6,
        // 14.5.1), so the mesh bounds and the material never drift apart; the shader Property defaults are fallbacks.

        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        public IEnumerable<RingMaterialDef> Defs()
        {
            Color white = new Color(1f, 1f, 1f, 1f);

            // M12 lamps (bridge lamp posts, bollard heads, door lamp, platform lamps): on with the lamp level _Lamps.x / 3,
            // so 3.0 x WarmLamp at night and dark glass in daylight (14.5.4: ramp over u 0.30-0.60).
            yield return new RingMaterialDef(RingMat.Emissive, EmissiveShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("CeramicWhite"), 0.92f))
                .F("_Smoothness", 0.45f)
                .Col("_EmissionColor", RingPalette.Lin("WarmLamp"))
                .F("_Intensity", LampHdr)
                .F("_UseLampLevel", 1f).F("_UseCityLevel", 0f).F("_UseBeaconPulse", 0f).F("_UseSpill", 0f)
                .F("_MinLevel", 0f).F("_AO", 1f).F("_Cull", 2f);

            // M12 amber: tunnel and underpass strips, markers; always on (emissive only in beat 6).
            yield return new RingMaterialDef(RingMat.EmissiveAmber, EmissiveShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("AmberLamp"), 0.3f))
                .F("_Smoothness", 0.35f)
                .Col("_EmissionColor", RingPalette.Lin("AmberLamp"))
                .F("_Intensity", AmberHdr)
                .F("_UseLampLevel", 0f).F("_UseCityLevel", 0f).F("_UseBeaconPulse", 0f).F("_UseSpill", 0f)
                .F("_MinLevel", 0f).F("_AO", 1f).F("_Cull", 2f);

            // M13 motes (14.5.1: <= 60 motes, drift 0.02 m/s, 5 mm turbulence at 0.3 Hz, <= 0.05 m/s, min 2 px), lit by the
            // key light and brightest inside the porthole sunbeam: origin = porthole centre, radius = the 0.55 m opening
            // seen along the key vector.
            Vector3 port = RingConstants.PortholeCentre;
            float cosPort = Mathf.Abs(Vector3.Dot(RingConstants.DirFromAzEl(RingConstants.PortholeYaw, RingConstants.PortholePitch),
                RingConstants.StarL));
            float beamR = 0.5f * RingConstants.PortholeOpening * Mathf.Clamp(cosPort, 0.5f, 1f);
            yield return new RingMaterialDef(RingMat.Mote, AdditiveShader).Queue(QueueMote)
                .F("_Mode", ModeMote)
                .Col("_Color", white)
                .F("_Intensity", 0.12f)
                .F("_MinPx", LightPoolKit.DefaultMinPx)
                .Vec("_NearFade", new Vector4(0.08f, 0.3f, 0f, 0f))
                .F("_SoftDepth", 0f)
                .F("_UseKeyLight", 1f)
                .F("_ForwardScatter", 2f)
                // the drift and turbulence amplitudes also size the mote bounds (LightPoolKit.MoteDriftMargin)
                .Vec("_Drift", new Vector4(LightPoolKit.MoteDriftX, LightPoolKit.MoteDriftY, LightPoolKit.MoteDriftZ, 0f))
                .Vec("_Turbulence", new Vector4(LightPoolKit.MoteTurbulence, LightPoolKit.MoteTurbulenceHz, 0f, 0f))
                .Vec("_BeamOrigin", new Vector4(port.x, port.y, port.z, beamR))
                .F("_BeamOutside", 0.06f)
                .F("_OffsetFactor", 0f).F("_OffsetUnits", 0f);

            // Light pools (14.2.3 radius 3 m; 14.5.4 quads 0.02 m above the path strip with polygon offset). Tint,
            // intensity, lamp channel (bollards: _Lamps.x / 3) and lamp height (uv1.x = radius / height) are per vertex
            // (LightPoolKit), so one material serves the 0.9 m bollards and the 3.2 m lamp posts.
            yield return new RingMaterialDef(RingMat.LightPool, AdditiveShader).Queue(QueuePool)
                .F("_Mode", ModeLightPool)
                .Col("_Color", white)
                .F("_Intensity", 1f)
                .F("_SoftDepth", 0f)
                .F("_OffsetFactor", -1f).F("_OffsetUnits", -1f);

            // Glows: lamp halos, ark engine glow quads (EngineBlue HDR 6 set per vertex), the beat-5 climber point.
            yield return new RingMaterialDef(RingMat.Glow, AdditiveShader).Queue(QueueGlow)
                .F("_Mode", ModeGlow)
                .Col("_Color", white)
                .F("_Intensity", 1f)
                .F("_MinPx", LightPoolKit.DefaultMinPx)
                .Vec("_NearFade", new Vector4(0.05f, 0.3f, 0f, 0f))
                .F("_Core", 0.35f)
                .F("_Pull", 1f)
                .F("_SoftDepth", 0f)
                .F("_OffsetFactor", 0f).F("_OffsetUnits", 0f);

            // 14.7 H5 / 14.12: one final static dither, 1.5 LSB (FullScreenPassRendererFeature, AfterRenderingPostProcessing).
            // _Decorrelate 0 = the 14.7 H5 formula verbatim (Part 14 overrides the 8.2 "triangular" wording; see the
            // shader header for the measured IGN-pair correlation). Set 1 (exact TPDF) only if the lead's 11.12 encode
            // test (BandScore on the 8-bit capture and the x264 CRF 23 re-encode) finds it equal or better.
            // _BlackGuard 1 also guards clipped white per channel.
            yield return new RingMaterialDef(RingMat.FinalDither, DitherShader)
                .F("_Amplitude", DitherLsb)
                .F("_BlackGuard", 1f)
                .F("_Decorrelate", 0f);
        }
    }
}
