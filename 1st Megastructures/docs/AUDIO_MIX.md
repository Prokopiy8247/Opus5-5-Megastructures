# Abyss: procedural audio mix (MASTER_PROMPT 14.3, 14.6, 14.11, 11.9)

Owner: audio builder. Code: `Assets/_Game/Scripts/Runtime/Audio/` (namespace `Megastructure.Audio`, Unity-free except
`AudioDirector`). Tests: `Assets/_Game/Scripts/Tests/EditMode/AudioTests.cs`. Analyser: `Tools/audio_analyze.py`.
The per-beat master trims T_b live here and in `AudioMix.MasterTrimDb` (the spec names `DEVELOPMENT.md`; the lead may copy
the table below there).

## 1. Signal flow

```
GameState / SceneClock --(main thread, AudioDirector order 20)--> AbyssScore.Evaluate --> AudioParams
   --> AudioParamsExchange (3-slot lock-free swap, Interlocked) --> ProceduralAudioEngine (audio thread, OnAudioFilterRead)

per 32-sample control block:
  clocks extrapolated from the snapshot (scene time, crank angle, fan phase, lift travel, real time)
  events: crank dead centres (MACHINE PULSE), rail ticks every 12 m (monotonic indices: extrapolation jitter never
          re-fires), seeded GROAN / BELL, door thud (once per run, walk real time 0.8 s), queued footsteps
  50 ms one-pole smoothing of every gain (pause duck 0.25 s, trim 1 s), LFOs, swept filter coefficients
  faded bus  : SUB HUM AIR FAN AIRFLOW LIFT(rumble, whine, ticks) WIND(+whistle) PULSE GROAN BELL footsteps
  unfaded bus: door thud (walk real time 0.8 s), ending 31 Hz tone
  reverb in  = faded send x MasterFade + unfaded send   -> pre-delay (<= 250 ms) -> 8-line FDN
  mix        = ((faded dry x MasterFade + reverb x wet x tail) x trim + unfaded dry) x duck
  out        = SoftLimiter(DcBlocker 8 Hz(mix)),  limiter 0.708 tanh(1.2 x) / tanh(1.2)
```

- 48 kHz requested with `AudioSettings.Reset`; any other device rate is used as is (all coefficients, FDN delays and
  calibrations follow `AudioSettings.outputSampleRate`; a device change rebuilds the engine and replays the source).
- `OnAudioFilterRead` runs on the audio thread about every 20 ms with interleaved floats; it calls no Unity API (only the
  engine, preallocated arrays and plain fields). The AudioSource plays a silent looping stereo dummy clip, is 2D,
  `ignoreListenerPause = true` (pause never cuts the sound; the engine ducks by -15 dB over about 0.25 s).
- Stereo: noise beds use independent mid and side sources, L = m + w s, R = m - w s (w 0.2-0.5). The mono sum is 2 m, so
  nothing cancels: measured mono minus stereo level -0.04 to -0.69 dB (gallery to outside). Tonal beds are centred; the FDN
  taps even lines for L and odd lines for R.
- Master fade and pause duck (14.3 "fade-in with the picture"): AudioDirector copies `GameState.PictureFade` into
  `MasterFade` every frame; the engine smooths it with the 50 ms one-pole and ramps it per sample (title fade-in 0.8-3.3 s,
  ending, restart fade). `GameState.Paused` sets the duck target to -15 dB; a 0.25 s one-pole, ramped per sample, applied
  to the whole mix after the reverb (about 0.002 of linear gain per 32-sample block). Both verified by
  `Master_FollowsThePictureFadeAndDucksSmoothlyOnPause`.
- Zero allocation in the render path (EditMode test with `GC.GetAllocatedBytesForCurrentThread`: 0 bytes over 512 blocks
  of 1024 frames, footsteps included). Cost: 77-118x real time on .NET 10 (about 1 % of one core at 48 kHz).
- Denormals: IIR states are double; the FDN loop carries a 1e-20 offset; gains that reach 1e-6 toward a zero target snap
  to exactly 0, and layers at gain 0 are skipped.

## 2. Bed layers (14.3 table, RMS dBFS per channel)

Zones: 1 gallery, 2 bridge, 3 ledge, 4 hall, 5 lift (window = cruise), 6 fan gantry, 7 tunnel, 8 outside (beat 7).

| Layer | Synthesis (as built) | 1 | 2 | 3 | 4 | 5 | 6 | 7t | 7o |
|---|---|---|---|---|---|---|---|---|---|
| SUB | sines 31.0 / 41.2 Hz, each + a 0.3 partner detuned 0.2 +/- 0.15 Hz (0.05 Hz LFO); AM 0.03 Hz depth 30 %; asymmetric tanh saturation of the 31 Hz tone, 62 / 93 Hz at -16 / -17.5 dB | -44 | -40 | -36 | -31 | -32 | -29 | -34 | -40 |
| AIR | brown noise, TPT SVF band-pass at 268 Hz (120-600 Hz) Q 0.8, centre swept +/-35 % at 0.07 Hz, level compensated by sqrt(fc / fc0) | -48 | -40 | -36 | -40 | -36 | -34 | -34 | -40 |
| HUM | 50 Hz + 100 (-6 dB), 150 (-10), 250 (-18), 5 % amplitude wobble at 0.2 Hz | -34 | off (a) | -52 | -29 | -34 then off (b) | -34 | off | off |
| LIFT rumble | white noise LP 120 Hz Q 0.6, HP 25 Hz; level x (0.25 + 0.75 v / 4.8) | | | | | -34 at cruise | | | |
| LIFT whine | sine 140 + 60 v / 4.8 Hz + 2nd (-10 dB) and 3rd (-16 dB) harmonics, -46 dB, dwell only, fades over ride seconds 0-4 (b) | | | | | | | | |
| FAN | white LP 70 Hz, HP 20 Hz, blade-pass swell +/-4 dB at 12 / 40 s from the scene clock; -50 -> -34 dB over ride seconds 60-100 | | | | | | -34 | | |
| AIRFLOW | pink noise band-pass 424 Hz (200-900 Hz) Q 0.6, +/-1.5 dB swell; -40 -> -30 dB with fan distance 270 -> 170 m (nominal eye from d / ride s), riding on the FAN envelope | | | | | | -40..-30 | | |
| WIND | pink noise band-pass 600 Hz (300-1200 Hz) Q 0.6, gusts 0.04 / 0.11 Hz (amplitude and +/-12 % centre); whistle: white noise BP 2.8 kHz Q 30 at -52 dB riding gust^2; +4 dB in hold H7 | | | | | | | | -37 |

Normalisation: bed levels are exact by construction (tones analytically; noise from the impulse-response energy of the
colour filter + SVF chain, variance of the uniform xorshift noise 1/3). LFO mean squares are divided out.

## 3. Events (dry peak dBFS)

| Event | Synthesis | Level | Timing |
|---|---|---|---|
| MACHINE PULSE | 38 Hz sine (attack 20 ms, T60 1.4 s) + noise BP 150 Hz Q 0.94 (90-250 Hz, T60 0.6 s); panned by bank v / 96 | -50 at d 270 -> -24 at the portal (d 329.54, smootherstep in dB), -24 in the hall, -30 on the dock, gone over ride s 0-4 | every crank dead centre: floor(CrankThetaIL(t) / 45) increments, i.e. every 15 s (t = 4.69 mod 15); monotonic index (see below) |
| GROAN | two triangle partials (f, 2.01 f) gliding 74 -> 61 Hz over 6 s through a resonant BP Q 6 swept 160 -> 300 -> 160 Hz, + LP body; 1.2 s swell, 2.5 s release; drifts across the stereo field | -34 (beats 2-6), off in the tunnel, -42 and 9 dB drier outside ("far") | seeded schedule on the scene clock: first at 45-60 s, then every 25-55 s |
| BELL | modes 83 / 137 / 211 / 283 Hz, T60 5 / 4 / 3 / 2 s, 4 ms half-sine mallet; dry x0.5, send x4 ("mostly reverb") | -42, -48 from the tunnel on ("far") | seeded: first at 70-90 s, then every 30-70 s |
| Rail tick | white noise BP 800 Hz Q 3, T60 25 ms, pan +0.25 | -40 | every 12 m of lift travel (40 ticks, 2.5 s apart at cruise); monotonic index |
| Door thud | 30 Hz sine (attack 5 ms, T60 0.5 s) + noise LP 400 Hz (T60 0.25 s), fixed seed, unfaded bus, through the gallery reverb | -24 (measured -24.6) | walk real time 0.8 s, exactly once per run (see below) |
| Footsteps | concrete: noise LP 2.2 kHz 70 ms + 90 Hz thump 120 ms; plate: + modes 420 / 1130 / 2370 Hz (180 / 120 / 80 ms); grating: BP 3-6 kHz tick 40 ms + 1.1 kHz ring 90 ms (+ faint thump); lift deck: modes 140 + 600 Hz 150 ms | -28 (stairs -31, pitch alternating 0 / -1 semitone); pitch +/-6 %, level +/-1.5 dB seeded | from `AudioEvents.Footstep` (player); nominal render: 0.75 m, 16 treads at 0.28 m |
| Ending tone | 31 Hz sine, unfaded bus, dry | -34 RMS (measured -34.18) | goal + 8 s: 3 s in, 5 s out |

Each voice type is calibrated to a unit dry peak at engine construction (fixed calibration seed); footsteps per component
and per surface.

**Monotonic event indices.** The engine extrapolates the crank angle and the lift travel between snapshots; when the audio
thread renders a burst before the next frame arrives, the next snapshot can land behind the extrapolation, and the clock
steps back across a boundary it has just crossed. The pulse and tick indices therefore only advance forward by one (and
fire); a step of -1 that lands less than half an interval behind the boundary is jitter (hold, no fire); anything else
(two or more intervals, or a step back by more than half an interval: `-clock`, capture, `ClockDriver.Begin`) is a seek
and resynchronises silently. Tests: `MachinePulse_` / `RailTick_FiresOnce...DespiteExtrapolationJitter` (the clock is made
to cross a boundary, step 4 ms back and cross again; the former code fired twice).

**Door thud gating.** `SceneClock.RealTime` also runs from 0 during the black load, and `ClockDriver.Begin` resets it to 0
when the walk starts, so a thud keyed on RealTime alone fired at load time 0.8 s (inaudible behind the black load or heard
during it) and never at the title. AudioDirector publishes `WalkStarted = ClockDriver.Running` (false while GameBootstrap
holds the clock); the engine detects the crossing of 0.8 s on a high-water mark of the extrapolated walk real time (armed
at 0 while loading, re-armed by a backward jump of more than 0.5 s = a new Begin; jitter cannot re-cross). An engine
created mid-walk (device change) starts at the current real time and does not replay it; a crossing found later than the
2.5 s picture fade-in (a stalled first frame) is dropped. Test: `DoorThud_WaitsForTheWalkAndFiresOncePerRunAtPointEightSeconds`
(6 s of load with RealTime passing 0.8 s: no thud and silence; walk with late snapshots: one thud at 0.800 s; a second
Begin: one more at 0.800 s).

## 4. Transitions (extended progress E: d before the ride, D_LIFT + ride s during it, d + 121 after the gate)

| Where | Audio |
|---|---|
| d 52-72 | SUB -44 -> -40, AIR -48 -> -40, trim 1 -> 2; GROAN / BELL buses on from d 56-72 |
| d 56 | reverb gallery -> bridge, 4 s time blend |
| d 60-64.5 | silence (a): HUM -34 -> -60 (3 s at 1.5 m/s), then out by d 90 |
| d 205-225 | SUB -> -36, AIR -> -36, HUM in at -52; reverb -> ledge at d 216 (4 s) |
| d 270-329.54 | MACHINE PULSE -50 -> -24 (beat 3 reaches -40 at d 297.5) |
| d 285-310 / -329.54 | SUB -> -31, AIR -> -40; HUM -52 -> -29 by the portal; reverb -> hall at d 326 (4 s, ends at the portal) |
| d 427.54-439.54 | last 12 m of the hall: SUB -32, AIR -36, HUM -34, pulse -30 at the deck; reverb -> lift at d 439.54 (5 s blend) |
| dwell (2 s) | lift motor whine and idle rumble fade in |
| ride s 0-4 | silence (b): HUM, PULSE, whine fade out; rumble rises with speed, rail ticks |
| ride s 50-100 | FAN in, -50 dB at 60 s -> -34 dB at 100 s (airflow follows) |
| ride s 100-120 | HUM returns at -34; s 110-120: SUB -29, AIR -34, trim 5 -> 6 |
| ride s 121 (gate) | reverb -> fan (4 s); LIFT fades out over 3 s |
| d 668.54-671.54 | silence (c): HUM, FAN, airflow, GROAN cut (2 s at 1.5 m/s); d 664.5-672.5 SUB -34, trim 6 -> 7; reverb -> tunnel (4 s); BELL -6 dB |
| d 716-728.54-734.54 | WIND audible inside the last 12.5 m (-46 at the exit) -> -37; SUB / AIR -40; GROAN far; reverb -> outside (4 s) |
| goal (z -72.8) | master follows min(PictureFade, own fade) over goal + 1..5 s, reverb tail with it; absolute silence 5-8 s; tone 8-16 s |

Curves are smootherstep segments, in dB between audible levels and in linear amplitude when an end is "off".

## 5. Reverb (custom FDN, 14.3)

Delays 1049, 1217, 1423, 1601, 1801, 2039, 2269, 2503 samples at 48 kHz (scaled to the device rate), orthonormal Hadamard
(fast Walsh-Hadamard), G0_i = 10^(-3 L_i / (fs RT60)), per-line in-loop damping (below), stereo pre-delay up to 250 ms
(fractional, slewed). A preset change blends RT60 (log), damping (log), pre-delay and wet over the preset's blend time
with smootherstep (the damping poles are recomputed with the loop gains every control block of the blend); the wet level is
additionally rate limited to 6 dB/s (measured maximum 0.94 dB/s). Wet = level of the early tail re dry (first pass at
0 dB energy), so steady-state reverberant energy grows with RT60 like a real space.

**Damping: per-line Jot design** (Jot and Chaigne 1991; docs/research/audio.md "HF damping"). Interpretation of the
spec's damping corner f_d: the RT60 at f_d is RT60 / 2 (`FdnReverb.DampingRt60Ratio`). Per line i:
G_d = 10^(-3 L_i / (fs RT60 / 2)), r = G_d / G0_i, c = cos(2 pi f_d / fs), A = 1 - r^2, B = 1 - r^2 c,
p_i = A / (B + sqrt(B^2 - A^2)), loop filter z_i = G0_i (1 - p_i) x + p_i z_i (implemented as the unit-DC state
lp_i = (1 - p_i) x + p_i lp_i, z_i = G0_i lp_i, so the output taps keep the "first pass at 0 dB" calibration). DC gain
G0_i (low-frequency RT60 exact), gain at f_d exactly G_d, and every line loses the same dB per second, so the RT60 falls
smoothly from RT60 to RT60 / 2 at f_d. Check value: L 2503, 7.5 s, 2.5 kHz gives p = 0.3732. A corner at or above
0.45 fs disables the damping (p = 0).

The former design used one fixed one-pole per pass (c = exp(-2 pi f_d / fs), the same for every line), which damps the
short lines more per second and collapsed the mid band: bridge preset (7.5 s, 2.5 kHz) measured 5.06 s at 500 Hz and
3.55 s at 1 kHz (single band-pass, the 1 kHz value inflated by low-frequency leakage; by design 3.8-5.3 s and 1.6-3.0 s).

Bridge preset, band-limited RT60 measured on the impulse response (two cascaded TPT band-passes Q 4.32, Schroeder
integration, T30 fit; `Fdn_BridgeBandRt60FollowsTheJotDesign`, 48 and 44.1 kHz):

| Band | 250 Hz | 500 Hz | 1 kHz | 2.5 kHz (f_d) | 5 kHz |
|---|---|---|---|---|---|
| Jot design (per line) | 7.42 | 7.20 | 6.43 | 3.75 | 1.64 |
| measured | 7.41 | 7.20 | 6.43 | 3.87 | 1.80 |
| test bound | | 7.5 +/- 20 % | 7.5 +/- 20 % | 3.75 +/- 20 % | |

| Preset | RT60 s | pre-delay ms | damping Hz | wet dB | blend s |
|---|---|---|---|---|---|
| 1 gallery | 0.7 | 5 | 4000 | -18 | 4 |
| 2 bridge | 7.5 | 120 | 2500 | -14 | 4 |
| 3 ledge | 5.0 | 80 | 3000 | -15 | 4 |
| 4 hall | 3.2 | 40 | 3000 | -13 | 4 |
| 5 lift | 9.0 | 200 | 2200 | -14 | 5 |
| 6 fan | 6.5 | 150 | 2500 | -14 | 4 |
| 7 tunnel | 2.5 | 20 | 4000 | -16 | 4 |
| 7 outside | 11.0 | 250 | 2000 | -20 | 4 |

Verified: line gains equal the formula exactly; every line's loop gain at f_d equals G_d to 1e-9; an undamped 2 s RT60
impulse decays 29.7 dB per second (30 expected); a blend is half way (geometric mean RT60) at 2 s and complete at 4 s.

## 6. Master trims T_b and measured levels

T_b = target - measured layer sum: each zone's 20 s window rendered with trims disabled, median of the 0.4 s window RMS,
mean over seeds 1-8 (spread 0.09-0.39 dB). Analytic sum = power sum of the 14.3 table (plus rumble / fan / airflow / wind).
Re-measured after the per-line Jot damping (the longer mid-band tails moved the bridge -0.11 dB, lift -0.08, fan -0.06,
outside -0.05, the others at most 0.01 dB).

| Zone | Window (nominal t) | Target | Analytic layer sum | Measured, T = 0 (seed 1) | **T_b** | Median with T_b | 0.4 s window range | Peak / true peak |
|---|---|---|---|---|---|---|---|---|
| 1 gallery | 6-26 s | -34 | -33.43 | -33.98 | **-0.02** | -34.00 | -34.60 .. -33.29 | -21.5 / -21.5 |
| 2 bridge | 60-80 s | -38 | -36.99 | -36.24 | **-1.64** | -37.88 | -39.19 .. -36.13 | -24.6 / -24.6 |
| 3 ledge | 152-172 s | -33 | -32.94 | -32.81 | **-0.16** | -32.97 | -34.28 .. -31.68 | -20.8 / -20.8 |
| 4 hall | 230.4-250.4 s | -27 | -26.67 | -26.10 | **-0.95** | -27.05 | -28.34 .. -25.77 | -16.2 / -16.2 |
| 5 lift | ride s 30-50 | -29 | -28.93 | -28.78 | **-0.27** | -29.05 | -30.89 .. -27.44 | -15.8 / -15.8 |
| 6 fan | 470-490 s | -26 | -25.58 | -25.72 | **-0.33** | -26.05 | -27.83 .. -24.22 | -13.9 / -13.9 |
| 7 tunnel | 578-598 s | -31 | -30.99 | -31.11 | **+0.19** | -30.92 | -32.55 .. -29.62 | -18.3 / -18.3 |
| 7 outside | 624.7-644.7 s | -35 | -33.98 | -34.30 | **-0.62** | -34.92 | -36.04 .. -32.73 | -20.6 / -20.6 |

Robustness: seeds 1-8, 99 and 31337, every zone, every 0.4 s window within +/-3 dB (worst deviation 2.39 dB, seed 6
fan). The bridge trim is the largest (-1.6 dB) because the 7.5 s reverb adds the most reverberant energy.

## 7. Whole walk (nominal 1.5 m/s, `AbyssScore.RenderWalkToWav`, seed 1, 674.2 s = goal + 17 s)

`python -I Tools/audio_analyze.py walk.wav` (after the Jot damping and the trims above): peak -11.24 dBFS, true peak
-11.24 dBFS, max sample step 0.042 (grating ticks), DC 6e-8; every 0.4 s window inside each beat's steady span within
+/-3 dB (span medians -33.96, -38.11, -33.25, -27.19, -29.39, -25.89, -31.39, -34.60); deliberate silences 6.2 dB
(a, bridge), 3.85 dB (b, departure, ride s 3-9), 6.19 dB (c, mouth) below the surrounding level; ending silence peak
2e-15 (absolute), tone -34.18 dBFS. Events: 9 pulses, 40 rail ticks, 14 groans, 14 bells, 1078 footsteps, 1 door thud.
Before 0.8 s the output is below -400 dBFS. The largest window-to-window change after the title is -3.3 dB (silence a,
a 3 s fade), so no level jumps anywhere. Rendered at 110x real time.

## 8. How to run

- EditMode: `AudioTests` (30 cases: 8 per-beat renders, trims, title, ending, determinism, zero allocation, limiter, FDN,
  bridge band RT60 at 500 Hz / 1 kHz / 2.5 kHz, Jot poles, reverb blend, pulses, rail ticks, pulse and tick jitter, door
  thud gating, master fade and pause duck, 44.1 kHz, exchange, footstep queue, E continuity). Writes
  `QA/audio/editmode/*.wav`.
- Player: `Abyss.exe -render-audio QA/audio/walk.wav` renders the whole walk (blocking, about 6-20 s) before play, logs
  `[ABYSS] audio render: ...`; `-audio-dump QA/audio/dump.wav 20` records 20 s of the real callback output and logs peak,
  RMS and callback size (`[ABYSS] audio dump: ...`). Compare the dump with the first 20 s of the render
  (`audio_analyze.py dump.wav --skip 4 --zone 1`).
- Re-measure trims after a synthesis change: `AbyssScore.MeasureTrims(seed, 20f, out measured)`.
- `python -I Tools/audio_analyze.py <wav> [--zone N] [--json out] [--csv curve.csv] [--top N]` (numpy; pure-Python
  fallback). Exit 0 = all checks pass.

## 9. Interpretations and open points

- The limiter formula has full scale -> -3 dBFS and an absolute bound of -1.42 dBFS (0.708 / tanh 1.2), so the true-peak
  limit holds for any input; the mix never drives it above -11.2 dBFS.
- FDN damping corner: "damping 2.5 kHz" is read as RT60(f_d) = RT60 / 2 with the exact per-line first-order Jot design
  (research interpretation, docs/research/audio.md); 500 Hz and 1 kHz then stay within -4 % and -14 % of the nominal RT60.
- The door thud is keyed on the walk's real time (from `ClockDriver.Begin`), not on the load: "at 0.8 s" in 14.3 / 14.11
  is title time, and the title timeline starts at Begin.
- Time-stated transitions ("over 3 s", "cut over 2 s") are laid out as the same distance at 1.5 m/s (14.6: blends on d);
  reverb blends are true 4 s (5 s at the hall exit) time blends.
- Choices where 14.3 / 14.6 are silent: the hall reverb starts at d 326 (blend ends at the portal), the pulse is -30 dB on
  the dock and deck, HUM returns for beat 6 over ride seconds 100-120, the wind is audible from 12.5 m inside the tunnel,
  GROAN is off in the tunnel and "far" outside, the bell is -6 dB from the mouth on.
- FAN swell phase is sin(2 pi t / 3.33 s) of the scene clock. It is phase-locked to the blade pass but not yet aligned to
  the moment a blade shadow crosses the gantry (needs the beat-6 blade angle convention). Adjust
  `AudioMix.FanSwellPhaseCycles` if the critics ask for it.
- Recommended: set Project Settings > Audio > System Sample Rate to 48000 so the start-up `AudioSettings.Reset` is a no-op.

Sources: Unity manual, MonoBehaviour.OnAudioFilterRead (audio thread, ~20 ms chunks, interleaved data, no Unity API calls)
and AudioSettings.Reset (fires OnAudioConfigurationChanged(false); replay sources), docs.unity.com 6000.0; A. Simper,
"Linear Trapezoidal Integrated SVF" (Cytomic, 2013); P. Kellet, refined pink noise filter (musicdsp.org); J.-M. Jot and
A. Chaigne, "Digital delay networks for designing artificial reverberators" (AES 1991); J. O. Smith, "Introduction to
Digital Filters" (two-pole resonator); ITU-R BS.1770 (4x oversampled true-peak estimate).
