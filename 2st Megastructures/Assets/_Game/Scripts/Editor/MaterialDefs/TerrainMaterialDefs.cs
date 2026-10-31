using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>Terrain-owned materials: M02 Terrain, M03 Water, M04 Grass and its flower variant (MASTER_PROMPT 14.2.6).
    /// The field grids and fades come from TerrainConfig so the C# instance counts and the GPU windows agree.</summary>
    public sealed class TerrainMaterialDefs : IRingMaterialDefs
    {
        public IEnumerable<RingMaterialDef> Defs()
        {
            yield return new RingMaterialDef(RingMat.Terrain, "Megastructure/RingTerrain")
                .F("_Smoothness", 0.15f)
                .F("_WetDarken", 0.62f)
                .F("_WetSmoothness", 0.32f)
                .F("_SkirtDepth", TerrainConfig.SkirtDepth)
                .F("_ShadowKeep", TerrainConfig.ShadowKeep)
                .Vec("_FarWater", new Vector4(TerrainConfig.FarWaterSpacing0, TerrainConfig.FarWaterSpacing1, 0f, 0f));

            // M03 (owner B12): analytic depth = metres from the waterline x 1/3 (the ADR-006 1:3 bank continued under the
            // water: WaterDeep core from 9.6 m out); key shadows dim the body by at most 30 %, never the sky reflection.
            Color foam = RingPalette.Lin("CeramicWhite");
            yield return new RingMaterialDef(RingMat.Water, "Megastructure/RingWater")
                .F("_Roughness", 0.08f)
                .F("_FoamStrength", 0.55f)
                .Col("_FoamColor", new Color(foam.r * 0.85f, foam.g * 0.85f, foam.b * 0.85f, 1f))
                .F("_BankSlope", 1f / 3f)
                .F("_ShadowBody", 0.3f);

            yield return new RingMaterialDef(RingMat.Grass, "Megastructure/RingGrass")
                .F("_Mode", 0f)
                .Vec("_Grid", new Vector4(TerrainConfig.GrassCell, TerrainConfig.GrassCells, TerrainConfig.GrassCandidates, 1f))
                .Vec("_Fade", new Vector4(TerrainConfig.GrassFadeStart, TerrainConfig.GrassFadeEnd, TerrainConfig.GrassRadius, 0f))
                .Vec("_Shape", new Vector4(TerrainConfig.GrassHeight, TerrainConfig.GrassHeightVar, TerrainConfig.GrassBaseWidth, TerrainConfig.GrassTipWidth))
                .Vec("_Sway", new Vector4(TerrainConfig.SwayTip, TerrainConfig.SwayGust, TerrainConfig.SwayOmega, TerrainConfig.SwayPhasePerX))
                .F("_Smoothness", 0.15f);

            yield return new RingMaterialDef(RingMat.Flower, "Megastructure/RingGrass")
                .F("_Mode", 1f)
                .Vec("_Grid", new Vector4(TerrainConfig.FlowerCell, TerrainConfig.FlowerCells, 1f, TerrainConfig.FlowerAccept))
                .Vec("_Fade", new Vector4(TerrainConfig.FlowerFadeStart, TerrainConfig.FlowerFadeEnd, TerrainConfig.FlowerRadius, 1f))
                .Vec("_Shape", new Vector4(TerrainConfig.FlowerStem, 0f, TerrainConfig.FlowerHead, TerrainConfig.FlowerStemWidth))
                .Vec("_Sway", new Vector4(TerrainConfig.FlowerSway, TerrainConfig.SwayGust, TerrainConfig.SwayOmega, TerrainConfig.SwayPhasePerX))
                // the terrain's smoothness: far stems and heads are lit with the terrain normal at grazing angles, where
                // 0.2 against the terrain's 0.15 added about 10 % of grazing sky sheen (pale needles at 25-40 m)
                .F("_Smoothness", 0.15f);
        }
    }
}
