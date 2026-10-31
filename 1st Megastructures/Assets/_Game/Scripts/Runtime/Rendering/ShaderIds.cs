using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Shader property names shared by C# and HLSL. Globals are written with Shader.SetGlobal* in LINEAR colour
    /// (use SetGlobalVector with AbyssPalette.LinV, never SetGlobalColor) by their owner, noted per entry.
    /// HLSL declarations live in Assets/_Game/Shaders/Include/AbyssFog.hlsl and AbyssCommon.hlsl.
    /// </summary>
    public static class ShaderIds
    {
        // ---------------------------------------------------------------- globals: fog / sky (owner ProgressDirector)
        public static readonly int AbFogSigma = Shader.PropertyToID("_AbFogSigma");         // float, 0.8326 / D50 (domain 0)
        public static readonly int AbFogDownMul = Shader.PropertyToID("_AbFogDownMul");     // float, m_down (domain 0)
        public static readonly int AbFogDown = Shader.PropertyToID("_AbFogDown");           // float4 linear rgb (domain 0)
        public static readonly int AbFogHoriz = Shader.PropertyToID("_AbFogHoriz");         // float4 linear rgb (domain 0)
        public static readonly int AbFogUp = Shader.PropertyToID("_AbFogUp");               // float4 linear rgb (domain 0)
        public static readonly int AbScatterAmt = Shader.PropertyToID("_AbScatterAmt");     // float (domain 0)
        public static readonly int AbScatterColor = Shader.PropertyToID("_AbScatterColor"); // float4 linear rgb (domain 0 SCATTER)
        public static readonly int AbSunDir = Shader.PropertyToID("_AbSunDir");             // float4 xyz = unit vector TOWARD the key light (domain 0)
        public static readonly int AbExtSunDir = Shader.PropertyToID("_AbExtSunDir");       // float4 xyz = unit vector toward the beat-7 sun (domain 2)
        public static readonly int AbSkyBlend = Shader.PropertyToID("_AbSkyBlend");         // float 0 = Shaft sky, 1 = Exterior sky (skybox only)

        // ---------------------------------------------------------------- globals: ambient (owner ProgressDirector)
        public static readonly int AbAmbUp = Shader.PropertyToID("_AbAmbUp");               // float4 linear, trilight sky term (already x ambient scale)
        public static readonly int AbAmbMid = Shader.PropertyToID("_AbAmbMid");             // float4 linear, equator term
        public static readonly int AbAmbDown = Shader.PropertyToID("_AbAmbDown");           // float4 linear, ground term

        // ---------------------------------------------------------------- globals: key light (owner ProgressDirector)
        public static readonly int AbKeyDepth = Shader.PropertyToID("_AbKeyDepth");         // float4 (world y of full main light, world y of zero, enabled 0/1, 0): shaft-depth falloff of the Key, domain 0 (AbyssLighting.hlsl)
        public static readonly int AbHazeSky = Shader.PropertyToID("_AbHazeSky");           // float4 linear rgb: haze skylight of the shaft sun (key colour x KeyRow.HazeSky x key fraction), domain 0 (AbyssLighting.hlsl AbHazeSkylight); w unused
        public static readonly int AbKeyChamfer = Shader.PropertyToID("_AbKeyChamfer");     // float4 (amplitude, y0, length, y zero; amplitude 0 = off): slow second depth falloff of the Key on the shaft ring top chamfers, domain 0 (AbyssLighting.hlsl AbRingChamferFactor, R2-06)
        public static readonly int AbRingBand = Shader.PropertyToID("_AbRingBand");         // float4 (shaft axis x, z, inner hex radius, outer hex radius): the ring band AbRingChamferFactor selects (constant, from AbyssConst)

        // ---------------------------------------------------------------- globals: camera / time (owner CameraRig, ClockDriver, CaptureHarness)
        public static readonly int AbPixelAngle = Shader.PropertyToID("_AbPixelAngle");     // float, 2 tan(vfov/2) / targetHeightPx (world size of a pixel = depth * this)
        public static readonly int AbTime = Shader.PropertyToID("_AbTime");                 // float4 (SceneClock mod 3600, RealTime mod 3600, 0, 0)
        public static readonly int AbDitherParams = Shader.PropertyToID("_AbDitherParams"); // float4 (amplitude LSB, temporal 0/1, frame index mod 64, enabled 0/1)

        // ---------------------------------------------------------------- globals: beat-specific (owner noted)
        public static readonly int AbLampIgnite = Shader.PropertyToID("_AbLampIgnite");     // float4 (start SceneClock time or -1, origin x, origin z, metres per second) owner Beat07
        public static readonly int AbBeamFade = Shader.PropertyToID("_AbBeamFade");         // float 0..1 beam visibility owner Beat06 / ProgressDirector
        public static readonly int AbFanTheta = Shader.PropertyToID("_AbFanTheta");         // float radians, fan rotation (beams and wall patches) owner Beat06

        // ---------------------------------------------------------------- material properties (all Megastructure shaders)
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");           // Color (sRGB in the inspector)
        public static readonly int AbDomain = Shader.PropertyToID("_AbDomain");             // float 0/1/2 fog domain
        public static readonly int Recipe = Shader.PropertyToID("_Recipe");                 // float recipe id for Megastructure/Surface
        public static readonly int Roughness = Shader.PropertyToID("_Roughness");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int ThinLine = Shader.PropertyToID("_ThinLine");             // float minimum on-screen width in px (0 = off)
        public static readonly int AbFade = Shader.PropertyToID("_AbFade");                 // float dithered tier fade: 1 = fully visible, 0 = hidden; negative = complementary pattern
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");   // Color
        public static readonly int EmissionStrength = Shader.PropertyToID("_EmissionStrength"); // float multiplier
        public static readonly int CardTop = Shader.PropertyToID("_CardTop");               // GradientCard top colour
        public static readonly int CardBottom = Shader.PropertyToID("_CardBottom");         // GradientCard bottom colour
        public static readonly int MinPixels = Shader.PropertyToID("_MinPixels");           // Glow minimum sprite size in px
    }
}
