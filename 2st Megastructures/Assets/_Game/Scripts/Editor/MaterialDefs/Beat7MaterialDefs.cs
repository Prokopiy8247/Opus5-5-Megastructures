using System.Collections.Generic;
using Megastructure.Beats;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Materials owned by beat 7 (owner BK, MASTER_PROMPT 14.5.7, 14.2.6): M09 RingHull (hull cap and gantries), the
    /// outer face of the rim wall (RingOuterFace, with the PaleGold mouth frame), M10 RingSwarmPanel (indirect) and
    /// the beacon lens (Beats/Beat07_Outside/Shaders/RingBeaconLens: BeaconAmber HDR x 8 x the global _BeaconPulse
    /// written by BeaconPulse; RingEmissive reads _Lamps.z instead, which is the AtmosphereDriver's, so the lens has its
    /// own shader). Colours are linear palette values (14.2.1); every geometric number comes from OutsideLayout, the
    /// C# twin of RingHullCore.hlsl, so mesh, shader and analytic shadows agree.
    /// </summary>
    public sealed class Beat7MaterialDefs : IRingMaterialDefs
    {
        const string HullShader = "Megastructure/RingHull";
        const string FaceShader = "Megastructure/RingOuterFace";
        const string SwarmShader = "Megastructure/RingSwarmPanel";
        const string LensShader = "Megastructure/RingBeaconLens";

        // Outer face: lit head-on (key 3.4 x 0.45, NdotL 0.99, EV -0.4 at s 900) a HullPale glaze reads as an overexposed
        // house wall (run04 KS15 mean luma 166). HullPale x 0.25 (albedo 0.17 before joints and ledges) and a matte
        // smoothness 0.22 put the face at luma ~110 (b7-01: x 0.36 gave 136, KS15 mean 130 > the beat-7 limit 105), so
        // the plate courses, seams and ledges read and the PaleGold doorway carries the frame.
        const float FaceTint = 0.21f, FaceSmoothness = 0.22f;   // KS15 mean 112.8 at 0.25 (beat-7 limit 105)
        // Hull: HullPale at the raking NdotL 0.12 (L7); gantries one step darker so they read against the plates.
        const float GantryTint = 0.78f;
        // Beacon lens body: dark amber glass under the emission (reads as glass when the pulse is low).
        const float LensGlass = 0.06f;

        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        public IEnumerable<RingMaterialDef> Defs()
        {
            Color hullPale = RingPalette.Lin("HullPale");
            Vector2 hc = RingConstants.HullCentre;

            // M09 RingHull
            yield return new RingMaterialDef(RingMat.Hull, HullShader)
                .Col("_BaseColor", hullPale)
                .Col("_StreakColor", RingPalette.Lin("HullStreak"))
                .Col("_GantryColor", Rgb(hullPale, GantryTint))
                .Vec("_HullCentre", new Vector4(hc.x, OutsideLayout.HullTopY, hc.y, RingConstants.HullR))
                .Vec("_Ridge", new Vector4(OutsideLayout.RidgePeriod, OutsideLayout.RidgeHeight, OutsideLayout.RidgeHalfWidth, OutsideLayout.RidgeCrestX))
                .Vec("_RidgeGeom", new Vector4(OutsideLayout.RidgeGeomR0, OutsideLayout.RidgeGeomR1, OutsideLayout.HullFaceAoWidth, OutsideLayout.FaceX))
                .Vec("_Rib", new Vector4(OutsideLayout.RibPeriod, OutsideLayout.RibHeight, OutsideLayout.RibHalfWidth, OutsideLayout.RibCrestX))
                .Vec("_Seam", new Vector4(OutsideLayout.HullSeamPeriod, OutsideLayout.HullSeamWidth, OutsideLayout.HullSeamOffsetX, OutsideLayout.HullSeamOffsetZ))
                .Vec("_Tone", new Vector4(OutsideLayout.HullPlateTone, OutsideLayout.HullSubPanel, OutsideLayout.HullSubPanelWidth, OutsideLayout.HullSubPanelDarken))
                .Vec("_DistDark", new Vector4(OutsideLayout.HullDistDarkFloor, OutsideLayout.HullDistDarkRange, 0f, 0f))
                .Vec("_Gantry", new Vector4(OutsideLayout.GantryX0, OutsideLayout.GantryPitchX, OutsideLayout.GantryJitterX, OutsideLayout.GantryRowNear))
                .Vec("_GantryB", new Vector4(OutsideLayout.GantryJitterZ, OutsideLayout.GantryBeamY0, OutsideLayout.GantryBeamY1, OutsideLayout.GantryHalfLen))
                .Vec("_GantryC", new Vector4(OutsideLayout.GantryLegX, OutsideLayout.GantryLegZ, OutsideLayout.GantryLegHalf, OutsideLayout.GantryHalfW))
                .Vec("_GantryD", new Vector4(OutsideLayout.GantryRowFar, 0f, 0f, 0f))
                .F("_Smoothness", 0.32f)
                .F("_StreakAmount", 1f)
                .F("_SeamDarken", 0.5f)
                .F("_Cull", 2f);

            // Outer face of the rim wall (+ PaleGold mouth frame, vertex colour B = 1)
            yield return new RingMaterialDef(RingMat.OuterFace, FaceShader)
                .Col("_BaseColor", Rgb(hullPale, FaceTint))
                .Col("_TrimColor", RingPalette.Lin("PaleGold"))
                .F("_Smoothness", FaceSmoothness)
                .F("_TrimSmoothness", 0.55f)
                .Vec("_Seam", new Vector4(RingConstants.WallSeam, OutsideLayout.FaceSeamWidth, 10f, 50f))
                .Vec("_SubPanel", new Vector4(OutsideLayout.FaceSubPanel, 0.08f, 0.80f, 0f))
                .Vec("_Plate", new Vector4(OutsideLayout.FacePlateW, OutsideLayout.FacePlateH, 0.04f, 0.62f))
                .Vec("_PlateOrigin", new Vector4(660f - 0.5f * OutsideLayout.FacePlateW, OutsideLayout.DeckTopY, 0.05f, 0.10f))
                .Vec("_Ledge", new Vector4(OutsideLayout.FaceLedgePeriod, OutsideLayout.FaceLedgeTop0, OutsideLayout.FaceLedgeHeight, OutsideLayout.FaceLedgeDepth))
                .Vec("_LedgeShade", new Vector4(1.12f, 0.90f, 0.70f, OutsideLayout.FaceX))
                .Vec("_PlateInset", new Vector4(0.3f, 0.02f, 0.78f, 0.15f))
                .Vec("_Portal", new Vector4(2.6f, 4.4f, -0.5f, 0.025f))
                .F("_PortalCentre", 660f)
                .F("_SeamDarken", 0.45f)
                .Vec("_Dark", new Vector4(OutsideLayout.FaceDark0, OutsideLayout.FaceDark1, 0f, 0f))
                .F("_Cull", 2f);

            // M10 RingSwarmPanel (three RenderMeshIndirect draws; no instancing keywords)
            yield return new RingMaterialDef(RingMat.SwarmPanel, SwarmShader)
                .Col("_BaseColor", RingPalette.Lin("SwarmBase"))
                .F("_GlintAmp", RingConstants.SwarmGlintAmp)
                .F("_GlintWidth", RingConstants.SwarmGlint)
                .F("_SpecClamp", RingConstants.SwarmClamp)
                .F("_PlateSize", RingConstants.SwarmPlate)
                .F("_MinPx", RingConstants.SwarmMinPx)
                .Vec("_Remap", new Vector4(OutsideLayout.SwarmRemapD0, OutsideLayout.SwarmRemapD1, OutsideLayout.SwarmRemapR0, OutsideLayout.SwarmRemapR1))
                .F("_LibAmp", OutsideLayout.SwarmLibAmp)
                .F("_Ambient", 1f)
                .F("_Bounce", OutsideLayout.SwarmBounce)
                .F("_Rim", OutsideLayout.SwarmRim)
                .F("_EdgePx", OutsideLayout.SwarmEdgePx)
                .F("_StarRadius", RingConstants.StarRadiusDeg * Mathf.Deg2Rad);

            // Beacon lens: BeaconAmber HDR x 8 x _BeaconPulse (0.55..1.0)
            yield return new RingMaterialDef(RingMat.Beacon, LensShader)
                .Col("_BaseColor", Rgb(RingPalette.Lin("BeaconAmber"), LensGlass))
                .Col("_EmissionColor", RingPalette.Lin("BeaconAmber"))
                .F("_Intensity", 8f)
                .F("_Smoothness", 0.62f)
                .F("_Rim", 0.25f)
                .F("_Cull", 2f);
        }
    }
}
