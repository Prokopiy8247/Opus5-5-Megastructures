using System.Collections.Generic;
using Megastructure.Beats;
using UnityEngine;

namespace Megastructure.EditorTools
{
    /// <summary>
    /// Beat 1-2 materials (owner BH). Colours are linear palette values (14.2.1). Everything else in beats 1-2 uses the
    /// shared lead / rendering ids (Ceramic, CeramicShade, PaleGold, Metal, PathStrip, Stone, Deck, Emissive, Glow,
    /// LightPool, Mote).
    /// The capsule interior materials are two-sided (_Cull 0): the inner skin then casts shadows from both faces, so
    /// together with the outer skin the shell has two independent shadow-caster layers and no normal-bias crack can
    /// let the key in anywhere but the porthole (14.5.1 "watertight ... no light leaks").
    /// Capsule look (14.5.1, critique run04 C18): a CeramicWhite shell with CeramicShade joints, white ribs whose rhythm
    /// comes from their form and the vertex-AO contact shadows (CapsuleGeo.CabinAo), satin tiles a step darker than the
    /// walls, a white porthole frame around a glossy PaleGold ring.
    /// </summary>
    public sealed class Beat12MaterialDefs : IRingMaterialDefs
    {
        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        public IEnumerable<RingMaterialDef> Defs()
        {
            Color white = RingPalette.Lin("CeramicWhite");
            Color shade = RingPalette.Lin("CeramicShade");
            Color gold = RingPalette.Lin("PaleGold");

            // Interior walls / ceiling. Panel grooves: walls (normal x) use (z, y) = (1.0, 0.625): the z grooves fall
            // under the ribs (1.0 m pitch at integer z), y grooves every 0.625 m; ceiling (normal y) uses (x, z) = (0.65, 1.0).
            yield return new RingMaterialDef(Beat12Mat.CapsuleInterior, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.96f)).Col("_JointColor", shade)
                .F("_Smoothness", 0.5f).Vec("_PanelSize", new Vector4(0.65f, 0.625f, 1.0f, 0f)).F("_PanelLine", 0.006f)
                .F("_AO", 1f).F("_EdgeWear", 0.6f).F("_Cull", 0f);

            // Floor tiles 0.6 m (14.5.1 scale cue "tile 0.6 m"), filtered grooves (RingFilteredGrid1 = footprint filter).
            // Warm grey satin tiles (a step below the walls) with dark joints; satin rather than glossy, so the floor shows
            // the warm strips instead of a grazing reflection.
            yield return new RingMaterialDef(Beat12Mat.CapsuleFloor, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(RingPalette.Lin("HullPale"), 0.72f)).Col("_JointColor", Rgb(shade, 0.35f))
                .F("_Smoothness", 0.35f).Vec("_PanelSize", new Vector4(0.6f, 0.6f, 0.6f, 0f)).F("_PanelLine", 0.006f)
                .F("_AO", 1f).F("_EdgeWear", 0.8f).F("_Cull", 0f);

            // Ribs, hatch reveal and slot, seat pads: CeramicWhite a step below the walls, no grooves (panel 100 m); the
            // rhythm reads from the lit inner faces, the chamfers and the AO contact shadows at the roots.
            yield return new RingMaterialDef(Beat12Mat.CapsuleRib, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.9f)).Col("_JointColor", Rgb(shade, 0.8f))
                .F("_Smoothness", 0.5f).Vec("_PanelSize", new Vector4(100f, 100f, 100f, 0f)).F("_PanelLine", 0.004f)
                .F("_AO", 1f).F("_EdgeWear", 0.7f).F("_Cull", 0f);

            // Porthole bay walls (cabin side): as the ribs but one-sided (the two-sided collar is two coincident one-sided
            // surfaces, cabin side here and Ceramic outside; a Cull Off material would z-fight with the Ceramic copy).
            yield return new RingMaterialDef(Beat12Mat.CapsuleBay, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.86f)).Col("_JointColor", Rgb(shade, 0.8f))
                .F("_Smoothness", 0.45f).Vec("_PanelSize", new Vector4(100f, 100f, 100f, 0f)).F("_PanelLine", 0.004f)
                .F("_AO", 1f).F("_EdgeWear", 0.7f).F("_Cull", 2f);

            // Porthole panel, frame and bore: white glaze (14.5.1 "lathe frame 0.80 m"), one-sided.
            yield return new RingMaterialDef(Beat12Mat.CapsuleFrame, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.9f)).Col("_JointColor", shade)
                .F("_Smoothness", 0.55f).Vec("_PanelSize", new Vector4(100f, 100f, 100f, 0f)).F("_PanelLine", 0.004f)
                .F("_AO", 1f).F("_EdgeWear", 0.6f).F("_Cull", 2f);

            // Porthole ring: PaleGold at the top of the glaze range (smoothness 0.65), edge wear on its chamfers.
            yield return new RingMaterialDef(Beat12Mat.CapsuleGold, "Megastructure/RingCeramic")
                .Col("_BaseColor", gold).Col("_JointColor", Rgb(gold, 0.7f))
                .F("_Smoothness", 0.65f).Vec("_PanelSize", new Vector4(100f, 100f, 100f, 0f)).F("_PanelLine", 0.004f)
                .F("_AO", 1f).F("_EdgeWear", 0.9f).F("_Cull", 2f);

            // Capsule strips: Practical2700, always on (no lamp channel), HDR 2.5 (practical, below the bloom threshold
            // after the beat-1 EV -0.4 except at the brightest texels).
            yield return new RingMaterialDef(Beat12Mat.CapsuleStrip, "Megastructure/RingEmissive")
                .Col("_BaseColor", Rgb(RingPalette.Lin("Practical2700"), 0.5f))
                .F("_Smoothness", 0.4f)
                .Col("_EmissionColor", RingPalette.Lin("Practical2700"))
                .F("_Intensity", 2.5f)
                .F("_UseLampLevel", 0f).F("_UseCityLevel", 0f).F("_UseBeaconPulse", 0f).F("_UseSpill", 0f)
                .F("_MinLevel", 0f).F("_AO", 1f).F("_Cull", 2f);

            // Beat-2 trim ceramic (bridge posts and rails, lamp posts, bollard shafts, benches): CeramicWhite glaze, no
            // grooves (panel 100 m), chamfer wear; PaleGold stays on the caps (shared PaleGold).
            yield return new RingMaterialDef(Beat12Mat.Trim, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.95f)).Col("_JointColor", shade)
                .F("_Smoothness", 0.5f).Vec("_PanelSize", new Vector4(100f, 100f, 100f, 0f)).F("_PanelLine", 0.004f)
                .F("_AO", 1f).F("_EdgeWear", 0.6f).F("_Cull", 2f);

            // Aqueduct (1.15 km): pale ceramic ashlar 3.0 x 1.5 m (the filtered joints fade out below 4 px spacing).
            yield return new RingMaterialDef(Beat12Mat.Aqueduct, "Megastructure/RingCeramic")
                .Col("_BaseColor", Rgb(white, 0.88f)).Col("_JointColor", Rgb(shade, 0.85f))
                .F("_Smoothness", 0.3f).Vec("_PanelSize", new Vector4(3.0f, 1.5f, 3.0f, 0f)).F("_PanelLine", 0.03f)
                .F("_AO", 1f).F("_EdgeWear", 0.5f).F("_Cull", 2f);
        }
    }
}
