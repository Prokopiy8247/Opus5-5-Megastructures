// MaterialLibrary: every material of the game (MASTER_PROMPT 14.2.3), created as .mat assets by the
// editor build script (MaterialFactory) with colours taken from ThreadPalette only. Beat builders use
// these references; nobody creates a material at runtime.
using UnityEngine;

namespace Megastructure
{
    [CreateAssetMenu(menuName = "Megastructure/Material Library")]
    public class MaterialLibrary : ScriptableObject
    {
        [Header("Beat 1-2 (ThreadFlat)")]
        public Material Salt;            // M01: SALT_WHITE, _PATTERN_SALT
        public Material SaltShadow;      // M01 shadow mode: litTotal 0, vertex alpha, ZWrite off, queue Geometry+2, Offset -2,-2
        public Material RobeTeal;        // M02 left row
        public Material RobeCoral;       // M02 right row (rim tint coral, shade CORAL_DEEP)
        public Material RobeHem;         // SAND hem band
        public Material HoodInk;         // INK hood interior / doorways
        public Material FacePlate;       // SAND_SHADE blank face plate
        public Material Stone;           // SAND ledges
        public Material Bollard;         // INK bollards
        public Material DoorFrame;       // CREAM 0.1 m colossus door frame (critique A15), sand shade tint

        [Header("Beat 3-4 (ThreadFlat)")]
        public Material Steps;           // M03: SAND tread, SAND_SHADE riser, SALT_WHITE stripes every 8 m, _PATTERN_STEPS
        public Material Stringer;        // M04 without mandala (SAND)
        public Material Terrace;         // M04 with mandala, _PATTERN_MANDALA
        public Material Cable;           // M05: TEAL / TEAL_DEEP / CORAL bands, _PATTERN_CABLE, relief fade
        public Material Collar;          // SAND_SHADE collars

        [Header("Beat 5-7")]
        public Material Deck;            // M06: SAND_SHADE, CREAM edge stripes, _PATTERN_DECK
        public Material Arm;             // SAND_SHADE support arms
        public Material RailPost;        // INK posts (ThreadFlat, instanced, dithered far fade)
        public Material Handrail;        // M07: CORAL handrail (ThreadLine)
        public Material BeadMint;        // M08
        public Material BeadPeach;
        public Material BeadLilac;
        public Material BeadDoor;        // INK
        public Material Window;          // CREAM emissive 1.5 (ThreadEmissive, instanced)
        public Material BeaconPillar;    // SAND
        public Material BeaconCrystal;   // M11: BEACON x 6 (ThreadEmissive), ramped by _ThTime.w
        public Material BeaconHalo;      // M11 halo: additive soft billboard (ThreadEmissive halo mode)
        public Material ThreadRibbon;    // ThreadLine TEAL_DEEP (cable ribbons beyond 20 km, KF7c thread)

        [Header("Sky / post")]
        public Material Sky;             // M09/M10 ThreadSky (scene skybox)
        public Material Grade;           // ThreadGrade (bloom + grade + dither), used by ThreadGradeFeature
    }
}
