namespace Megastructure.Beats
{
    /// <summary>
    /// Material ids owned by the beat 1-2 builder (defined in Scripts/Editor/MaterialDefs/Beat12MaterialDefs.cs).
    /// Everything else uses the shared ids of <see cref="RingMat"/>.
    /// </summary>
    public static class Beat12Mat
    {
        /// <summary>Capsule interior walls and ceiling: M01 ceramic whose panel grooves fall under the 1.0 m ribs (z) and
        /// run every 0.625 m up the walls. Two-sided, so the inner skin is a second shadow-caster layer (light-tight).</summary>
        public const string CapsuleInterior = "B1_Interior";
        /// <summary>Capsule floor and gangway deck: dark metal-glaze ceramic with 0.6 m tile grooves (14.5.1 "0.6 m floor tiles (shader)").</summary>
        public const string CapsuleFloor = "B1_Floor";
        /// <summary>Ribs, hatch reveal and slot, seat pads: CeramicShade without panel grooves, two-sided.</summary>
        public const string CapsuleRib = "B1_Rib";
        /// <summary>Porthole bay walls and collars seen from the cabin: as B1_Rib but one-sided, because where the bay
        /// meets the outside air the collar is two coincident one-sided surfaces (cabin side and Ceramic outside).</summary>
        public const string CapsuleBay = "B1_Bay";
        /// <summary>The two interior emissive strips, always on, Practical2700 (14.2.1 "capsule strips").</summary>
        public const string CapsuleStrip = "B1_Strip";
        /// <summary>Porthole panel, 0.80 m lathe frame and bore: CeramicWhite glaze without grooves (the frame reads as
        /// part of the white shell around the PaleGold ring, 14.5.1).</summary>
        public const string CapsuleFrame = "B1_Frame";
        /// <summary>Porthole ring: PaleGold, glossier than the shared PaleGold so it catches the strips and the bore.</summary>
        public const string CapsuleGold = "B1_Gold";
        /// <summary>Beat-2 human-scale ceramic: bridge posts and rails, lamp posts, bollard shafts, benches. CeramicWhite
        /// glaze without panel grooves (world-space grooves would cut thin rods and posts at random heights).</summary>
        public const string Trim = "B2_Trim";
        /// <summary>Aqueduct (1.15 km): pale ceramic ashlar, so it reads as part of the white-ceramic world in the haze.</summary>
        public const string Aqueduct = "B2_Aqueduct";
        // Bridge lamp pools use the shared RingMat.LightPool: the lamp height is per vertex (uv1.x = R / h, B12Additive.Pool).
    }
}
