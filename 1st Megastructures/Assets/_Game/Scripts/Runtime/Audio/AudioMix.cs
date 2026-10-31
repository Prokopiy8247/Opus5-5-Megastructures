namespace Megastructure.Audio
{
    /// <summary>One reverb setting of the 14.3 table: RT60 (s), pre-delay (s), damping corner (Hz), wet return (dB).</summary>
    public readonly struct ReverbPreset
    {
        public readonly float Rt60, PreDelay, Damping, WetDb, BlendSeconds;

        public ReverbPreset(float rt60, float preDelay, float damping, float wetDb, float blendSeconds)
        {
            Rt60 = rt60; PreDelay = preDelay; Damping = damping; WetDb = wetDb; BlendSeconds = blendSeconds;
        }
    }

    /// <summary>
    /// The mix of MASTER_PROMPT 14.3 as data. Zones are indexed 0..7 = beat 1, 2, 3, 4, 5, 6, 7 tunnel, 7 outside
    /// (see <see cref="AbyssScore.Zone"/>). Layer values are RMS dBFS per channel (beds) and dry peak dBFS (events);
    /// <see cref="Off"/> means silent. docs/AUDIO_MIX.md documents every number and the measured results.
    /// </summary>
    public static class AudioMix
    {
        public const int ZoneCount = 8;
        public const float Off = DspMath.SilentDb;

        public static readonly string[] ZoneNames = { "1 gallery", "2 bridge", "3 ledge", "4 hall", "5 lift", "6 fan", "7 tunnel", "7 outside" };

        // ------------------------------------------------------------------ 14.3 layer table (RMS dBFS per beat)
        public static readonly float[] SubDb = { -44f, -40f, -36f, -31f, -32f, -29f, -34f, -40f };
        public static readonly float[] AirDb = { -48f, -40f, -36f, -40f, -36f, -34f, -34f, -40f };
        public static readonly float[] HumDb = { -34f, Off, -52f, -29f, -34f, -34f, Off, Off };

        // ------------------------------------------------------------------ 14.3 master RMS targets (0.4 s windows, +/-3 dB)
        public static readonly float[] TargetRmsDb = { -34f, -38f, -33f, -27f, -29f, -26f, -31f, -35f };
        public const float RmsToleranceDb = 3f;
        public const float MaxTrimDb = 6f;
        public const float RmsWindowSeconds = 0.4f;

        /// <summary>
        /// Per-beat master trim T_b = target - measured layer sum (dB), measured by rendering each zone's 20 s window
        /// with all trims at 0 (median of the 0.4 s window RMS, mean over seeds 1-8, spread 0.09-0.39 dB; re-measured after the
        /// per-line Jot FDN damping). Re-measure with AbyssScore.MeasureTrims after any change to the synthesis and update
        /// docs/AUDIO_MIX.md.
        /// </summary>
        public static readonly float[] MasterTrimDb = { -0.02f, -1.64f, -0.16f, -0.95f, -0.27f, -0.33f, 0.19f, -0.62f };

        // ------------------------------------------------------------------ 14.3 reverb table
        public static readonly ReverbPreset[] Reverb =
        {
            new ReverbPreset(0.7f, 0.005f, 4000f, -18f, 4f),   // 1 gallery
            new ReverbPreset(7.5f, 0.120f, 2500f, -14f, 4f),   // 2 bridge
            new ReverbPreset(5.0f, 0.080f, 3000f, -15f, 4f),   // 3 ledge
            new ReverbPreset(3.2f, 0.040f, 3000f, -13f, 4f),   // 4 hall
            new ReverbPreset(9.0f, 0.200f, 2200f, -14f, 5f),   // 5 lift (4 -> 5 blends over 5 s, 14.6)
            new ReverbPreset(6.5f, 0.150f, 2500f, -14f, 4f),   // 6 fan
            new ReverbPreset(2.5f, 0.020f, 4000f, -16f, 4f),   // 7 tunnel
            new ReverbPreset(11.0f, 0.250f, 2000f, -20f, 4f),  // 7 outside
        };

        /// <summary>Maximum rate of change of the reverb wet level (14.6, AbyssConst.REVERB_RATE).</summary>
        public const float WetRateDbPerSecond = AbyssConst.REVERB_RATE;

        // ------------------------------------------------------------------ events and special layers (14.3)
        public const float PulsePeakDb = -24f;          // MACHINE PULSE at full (beat 4)
        public const float PulseStartDb = -50f;         // from d = 270 (beat 3)
        public const float PulseRiseStartD = 270f;
        public const float GroanPeakDb = -34f;
        public const float GroanFarDb = -42f;           // beat 7 outside, "far GROAN" (also drier)
        public const float GroanFarDryDb = -9f;
        public const float BellPeakDb = -42f;
        public const float BellFarDb = -48f;            // beat 7, "far BELL"
        public const float LiftRumbleDb = -34f;
        public const float LiftWhineDb = -46f;
        public const float LiftTickDb = -40f;
        public const float LiftTickSpacing = 12f;       // metres of travel per rail tick
        public const float FanFullDb = -34f;
        /// <summary>Phase offset (cycles) of the FAN blade-pass swell against the scene clock, to align the swell with the moment a
        /// blade shadow crosses the gantry (0 = sin(2 pi t / 3.33 s)).</summary>
        public const double FanSwellPhaseCycles = 0.0;
        public const float FanRideStartDb = -50f;       // at ride second 60
        public const float AirflowFarDb = -40f, AirflowNearDb = -30f;
        public const float AirflowFarDist = 270f, AirflowNearDist = 170f; // metres from the fan centre (gantry start / mouth)
        public const float WindDb = -37f;
        public const float WindHoldBoostDb = 4f;        // cinematic hold H7
        public const float WhistleDb = -52f;
        public const float FootstepPeakDb = -28f;
        public const float StairStepPeakDb = -31f;
        public const float FootstepLevelJitterDb = 1.5f;
        public const float FootstepPitchJitter = 0.06f;
        public const float ThudPeakDb = -24f;
        public const float EndToneDb = -34f;            // RMS of the 31 Hz ending tone
        public const float PauseDuckDb = -15f;

        // ------------------------------------------------------------------ title and ending timeline (14.11, 14.3 d)
        public const float TitleBlack = 0.8f;           // full black; the door thud fires here
        public const float TitleFade = 2.5f;            // picture and audio fade in
        public const float EndingFadeDelay = 1.0f;      // from GoalReached
        public const float EndingFade = 4.0f;
        public const float EndingSilence = 3.0f;
        public const float EndToneIn = 3.0f, EndToneOut = 5.0f;

        /// <summary>Seconds after GoalReached at which the ending tone starts (1 + 4 + 3 = 8 s).</summary>
        public const float EndToneStart = EndingFadeDelay + EndingFade + EndingSilence;

        /// <summary>Analytic power sum of the bed layers of zone z (dBFS) - the "layer sum" of the 14.3 mix rule.</summary>
        public static float LayerSumDb(int zone)
        {
            double p = 0.0;
            p += Pow(SubDb[zone]);
            p += Pow(AirDb[zone]);
            p += Pow(HumDb[zone]);
            if (zone == 4) { p -= Pow(HumDb[4]); p += Pow(LiftRumbleDb); } // the ride: silence (b) replaces HUM by the rumble
            if (zone == 5) { p += Pow(FanFullDb); p += Pow(0.5f * (AirflowFarDb + AirflowNearDb)); }
            if (zone == 7) p += Pow(WindDb);
            return (float)(10.0 * System.Math.Log10(System.Math.Max(p, 1e-20)));
        }

        static double Pow(float db) => db <= Off ? 0.0 : System.Math.Pow(10.0, db / 10.0);
    }
}
