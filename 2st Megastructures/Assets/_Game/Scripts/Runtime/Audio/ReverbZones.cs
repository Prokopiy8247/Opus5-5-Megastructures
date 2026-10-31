using System;

namespace Megastructure
{
    /// <summary>AudioReverbFilter user settings (field names and ranges: docs/research/05 section 4). Levels in mB.</summary>
    public struct ReverbSettings
    {
        public double DryLevel, Room, RoomHF, RoomLF, DecayTime, DecayHFRatio, ReflectionsLevel, ReflectionsDelay,
                      ReverbLevel, ReverbDelay, HfReference, LfReference, Diffusion, Density;

        public static ReverbSettings Make(double room, double roomHF, double decay, double hfRatio, double refl, double reflDelay, double reverb)
        {
            return new ReverbSettings
            {
                DryLevel = 0, Room = room, RoomHF = roomHF, RoomLF = 0, DecayTime = decay, DecayHFRatio = hfRatio,
                ReflectionsLevel = refl, ReflectionsDelay = reflDelay, ReverbLevel = reverb, ReverbDelay = 0.02,
                HfReference = 5000, LfReference = 250, Diffusion = 100, Density = 100
            };
        }

        /// <summary>Blend: mB fields linear (already logarithmic), decay / HF / LF references in the log domain,
        /// the reflection delay switches at the midpoint (delay changes are hidden by the ER gate of the AudioDirector).</summary>
        public static ReverbSettings Lerp(in ReverbSettings a, in ReverbSettings b, double t)
        {
            t = DspMath.Clamp01(t);
            ReverbSettings r;
            r.DryLevel = DspMath.Lerp(a.DryLevel, b.DryLevel, t);
            r.Room = DspMath.Lerp(a.Room, b.Room, t);
            r.RoomHF = DspMath.Lerp(a.RoomHF, b.RoomHF, t);
            r.RoomLF = DspMath.Lerp(a.RoomLF, b.RoomLF, t);
            r.DecayTime = LogLerp(a.DecayTime, b.DecayTime, t);
            r.DecayHFRatio = DspMath.Lerp(a.DecayHFRatio, b.DecayHFRatio, t);
            r.ReflectionsLevel = DspMath.Lerp(a.ReflectionsLevel, b.ReflectionsLevel, t);
            r.ReflectionsDelay = t < 0.5 ? a.ReflectionsDelay : b.ReflectionsDelay;
            r.ReverbLevel = DspMath.Lerp(a.ReverbLevel, b.ReverbLevel, t);
            r.ReverbDelay = DspMath.Lerp(a.ReverbDelay, b.ReverbDelay, t);
            r.HfReference = LogLerp(a.HfReference, b.HfReference, t);
            r.LfReference = LogLerp(a.LfReference, b.LfReference, t);
            r.Diffusion = DspMath.Lerp(a.Diffusion, b.Diffusion, t);
            r.Density = DspMath.Lerp(a.Density, b.Density, t);
            return r;
        }

        static double LogLerp(double a, double b, double t) => Math.Exp(DspMath.Lerp(Math.Log(Math.Max(1e-6, a)), Math.Log(Math.Max(1e-6, b)), t));
    }

    /// <summary>
    /// Reverb zones of 14.3 "Reverb" (decay / room): capsule 0.6 s / -1 200 mB; valley 1.2 s / -2 500 mB; ark 4.0 s /
    /// -2 000 mB, HF ratio 0.4; dusk 3.0 s; underpass 1.1 s; wall 2.4 s with the 0.08 s reflection (28 m round trip,
    /// reflectionsLevel raised so it is audible); tunnel 3.6 s; platform 0.8 s. The other fields are chosen by ear.
    /// Blend windows (14.6): capsule -> open over 2 s with the hatch; valley -> ark s 260-290; under the footbridge arch
    /// (s 396-404) 60 % of the way to the underpass reverb, 3 m (2 s) windows each side; ark -> dusk s 411-431; dusk ->
    /// underpass s 552-566; underpass -> wall s 594-600; wall -> tunnel at the door (3 m = 2 s); tunnel -> platform at
    /// the mouth (2 s). The AudioDirector adds a time follower (tau <see cref="FollowTau"/>, 95 % in 2.1 s) so every
    /// change takes at least 2 s at any walking speed (14.3 "blended over >= 2 s").
    /// </summary>
    public static class ReverbZones
    {
        public const int Capsule = 0, Valley = 1, Ark = 2, Dusk = 3, Underpass = 4, Wall = 5, Tunnel = 6, Platform = 7, Count = 8;
        public static readonly string[] Names = { "capsule", "valley", "ark", "dusk", "underpass", "wall", "tunnel", "platform" };

        public static readonly ReverbSettings[] Zone =
        {
            //                   room   roomHF  decay  hfRatio  refl   reflDelay  reverb
            ReverbSettings.Make(-1200,  -600,   0.6,   0.50,   -900,  0.004,      0),      // capsule
            ReverbSettings.Make(-2500, -1200,   1.2,   0.45,  -2600,  0.020,   -400),      // valley
            ReverbSettings.Make(-2000, -1800,   4.0,   0.40,  -3000,  0.020,   -200),      // ark
            ReverbSettings.Make(-2300, -1500,   3.0,   0.45,  -3000,  0.020,   -300),      // dusk
            ReverbSettings.Make(-1100,  -700,   1.1,   0.60,   -900,  0.006,      0),      // underpass
            ReverbSettings.Make(-1800, -1000,   2.4,   0.50,   -500,  0.080,   -400),      // wall (echo 0.08 s)
            ReverbSettings.Make( -900,  -800,   3.6,   0.55,   -800,  0.012,    200),      // tunnel
            ReverbSettings.Make(-2800, -1600,   0.8,   0.50,  -3500,  0.020,   -900),      // platform
        };

        /// <summary>Zone pair and blend weight at progress s; hatchElapsed = seconds since the hatch trigger (-inf if not).</summary>
        public static void At(double s, double hatchElapsed, out int a, out int b, out double w)
        {
            if (s < 200)
            {
                a = Capsule; b = Valley;
                double tw = double.IsNegativeInfinity(hatchElapsed) ? 0.0 : DspMath.Smootherstep01((hatchElapsed - 0.3) / 2.0);
                w = Math.Max(tw, DspMath.SS(9.5, 12.5, s));
            }
            else if (s < 340) { a = Valley; b = Ark; w = DspMath.SS(260, 290, s); }
            else if (s < 408) { a = Ark; b = Underpass; w = FootbridgeWeight * DspMath.SS(393.5, 396.5, s) * (1.0 - DspMath.SS(403.5, 406.5, s)); }
            else if (s < 500) { a = Ark; b = Dusk; w = DspMath.SS(411, 431, s); }
            else if (s < 580) { a = Dusk; b = Underpass; w = DspMath.SS(552, 566, s); }
            else if (s < 700) { a = Underpass; b = Wall; w = DspMath.SS(594, 600, s); }
            else if (s < 850) { a = Wall; b = Tunnel; w = DspMath.SS(773, 776, s); }
            else { a = Tunnel; b = Platform; w = DspMath.SS(893, 896, s); }
        }

        public static ReverbSettings Target(double s, double hatchElapsed)
        {
            At(s, hatchElapsed, out int a, out int b, out double w);
            return ReverbSettings.Lerp(Zone[a], Zone[b], w);
        }

        /// <summary>Share of the underpass reverb under the 8 m footbridge arch (a short open vault, not a sealed room).</summary>
        public const double FootbridgeWeight = 0.6;
        /// <summary>Time constant of the AudioDirector's reverb follower: a one-pole reaches 95 % in 3 tau = 2.1 s.</summary>
        public const double FollowTau = 0.7;
    }
}
