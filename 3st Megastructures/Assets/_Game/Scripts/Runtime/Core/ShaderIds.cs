// ShaderIds: property ids of the global uniforms declared in Shaders/Include/ThreadCommon.hlsl
// (the C# side of the shader contract) plus the per-material property names used by MaterialLibrary.
using UnityEngine;

namespace Megastructure
{
    public static class ShaderIds
    {
        // globals (ThreadCommon.hlsl)
        public static readonly int SunDir = Shader.PropertyToID("_ThSunDir");
        public static readonly int SunColor = Shader.PropertyToID("_ThSunColor");
        public static readonly int AmbUp = Shader.PropertyToID("_ThAmbUp");
        public static readonly int AmbSide = Shader.PropertyToID("_ThAmbSide");
        public static readonly int AmbDown = Shader.PropertyToID("_ThAmbDown");
        public static readonly int Sky = Shader.PropertyToID("_ThSky");
        public static readonly int Fog = Shader.PropertyToID("_ThFog");
        public static readonly int Bank = Shader.PropertyToID("_ThBank");
        public static readonly int Deck = Shader.PropertyToID("_ThDeck");
        public static readonly int Cable = Shader.PropertyToID("_ThCable");
        public static readonly int Thread = Shader.PropertyToID("_ThThread");
        public static readonly int Thread2 = Shader.PropertyToID("_ThThread2");
        public static readonly int Planet = Shader.PropertyToID("_ThPlanet");
        public static readonly int PlanetDir = Shader.PropertyToID("_ThPlanetDir");
        public static readonly int Earth = Shader.PropertyToID("_ThEarth");
        public static readonly int Sea = Shader.PropertyToID("_ThSea");
        public static readonly int Time = Shader.PropertyToID("_ThTime");
        public static readonly int Beacon = Shader.PropertyToID("_ThBeacon");
        public static readonly int Screen = Shader.PropertyToID("_ThScreen");

        // per-material (ThreadFlat / ThreadLine / ThreadEmissive)
        public const string BaseColor = "_BaseColor";
        public const string ShadeColor = "_ShadeColor";
        public const string UseShadeColor = "_UseShadeColor";
        public const string SecondColor = "_SecondColor";
        public const string RimCoral = "_RimTint";
        public const string RimScale = "_RimScale";
        public const string CableShadowOn = "_CableShadowOn";
        public const string ShadowMode = "_ShadowMode";
        public const string Sway = "_Sway";
        public const string ReliefFade = "_ReliefFade";
        public const string Emission = "_Emission";
        public const string EmissionRamp = "_EmissionRamp";
        public const string SrcBlend = "_SrcBlend";
        public const string DstBlend = "_DstBlend";
        public const string ZWrite = "_ZWrite";
        public const string Cull = "_Cull";
        public const string OffsetFactor = "_OffsetFactor";
        public const string OffsetUnits = "_OffsetUnits";
        public const string MinWidthPx = "_MinWidthPx";
        public const string LineWidth = "_LineWidth";
        public const string FadeNear = "_FadeNear";
        public const string FadeFar = "_FadeFar";
    }
}
