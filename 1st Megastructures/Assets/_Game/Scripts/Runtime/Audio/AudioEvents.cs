namespace Megastructure
{
    /// <summary>Footstep surface kinds (14.3 Footsteps).</summary>
    public enum SurfaceKind { Concrete = 0, Stair = 1, SteelPlate = 2, Grating = 3, LiftDeck = 4 }

    /// <summary>
    /// Main-thread to audio-thread event bus. Gameplay code calls these; the ProceduralAudioEngine drains them on the
    /// audio thread. Implementations must be lock-free and allocation-free (owner: audio builder).
    /// </summary>
    public static partial class AudioEvents
    {
        /// <summary>One footstep on the given surface (PlayerController, one per 0.75 m; on stairs one per tread).</summary>
        public static void Footstep(SurfaceKind surface) { EnqueueFootstep(surface); }

        static partial void EnqueueFootstep(SurfaceKind surface);
    }
}
