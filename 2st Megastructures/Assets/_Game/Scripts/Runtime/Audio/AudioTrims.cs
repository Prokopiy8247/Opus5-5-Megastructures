namespace Megastructure
{
    /// <summary>
    /// Per-beat trims of 14.3 "Master" ("this table wins over the layer levels; each beat applies a per-beat trim to
    /// its layers to hit it"). Four knots per beat across its steady region (the beat minus the 24 m Rule-P windows at
    /// its boundaries), seven in beat 2 (the bridge s 116-184 with its G1 dip and bridge footsteps, and the crest hold
    /// at s 150), six in beat 4 (two extra around the levee mouth and the underpass, s 552-568, where five layers
    /// hand over to U1 within 14 m); linear between knots of one beat, smootherstep across a boundary window. Found
    /// with the offline render (AudioOfflineRender.FitTrims, cinematic walk at 1.5 m/s, 48 kHz, with the W1 3 s gust
    /// loudness compensation of RingAudioEngine.GustComp) and written to QA/audio-trims.json by AudioTests. Footsteps
    /// and the hatch keep their absolute peak levels (-30 dBFS) and are not trimmed.
    /// </summary>
    public static class AudioTrims
    {
        public static readonly double[] KnotS =
        {
            0, 4, 8, 12,                        // B1 capsule
            36, 76, 116, 150, 184, 224, 264,    // B2 valley (bridge s 116-184, crest hold s 150)
            288, 325, 362, 399,                 // B3 ark
            423, 476, 529, 552, 568, 582,       // B4 sunset, night, underpass
            606, 658, 710, 762,                 // B5 rim wall
            786, 818, 850, 882,                 // B6 tunnel
            906, 924, 942, 960                  // B7 platform
        };

        public static readonly int[] KnotBeat =
        {
            0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6
        };

        /// <summary>Trim in dB at each knot.</summary>
        public static readonly double[] KnotDb =
        {
            4.91, 4.91, 4.91, 4.91,
            -0.46, -0.17, -0.22, -0.98, -0.58, -0.53, -0.54,
            2.45, 0.84, -0.19, -1.64,
            -5.63, -1.57, -1.50, -2.79, 3.77, 3.06,
            -2.09, -2.38, -2.40, -2.44,
            -2.26, 0.18, 2.30, 2.89,
            -0.18, 0.24, 0.09, 0.15
        };
    }
}
