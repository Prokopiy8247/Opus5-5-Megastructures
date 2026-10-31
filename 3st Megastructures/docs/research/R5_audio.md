# R5 — Procedural audio in Unity 6000.6 and DSP building blocks

Researcher: R5 (read-only). Accessed 2026-10-29. Targets Unity 6000.6.0f1, C# 9 (Unity's language level), spec Part 14.3 (MASTER_PROMPT.md lines 507-576) and 11.9 (line 331).
Everything under "Snippets" was compiled with `LangVersion 9` against `netstandard2.1` + the Unity 6000.6.0f1 `UnityEngine.CoreModule.dll` / `UnityEngine.AudioModule.dll`, and the DSP was executed and measured in a .NET 10 console harness (numbers in section 3). The Python script was run with `py -3.10` (numpy 2.2.6) and agrees with the C# meter to 0.01 LU.

## 0. Decisions in one screen

1. **Engine wiring**: keep the spec's pattern (`ThreadSynth` with `OnAudioFilterRead` on the AudioListener GameObject, adding its layers into `data` and then running the master chain over the whole buffer, so footsteps also get reverb and the limiter). Add a **heartbeat** (`Interlocked.Increment` per callback). If it reads 0 after 0.5 s of unscaled time, fall back to the documented generator pattern: a dedicated GameObject with an `AudioSource` (no clip, `playOnAwake`, `Play()`) plus `ThreadSynth`. Log which path ran.
2. **Filters**: use the **Simper/Cytomic trapezoidal SVF** everywhere. The spec's Chamberlin `f = 2 sin(pi fc/fs)` is **unstable at 48 kHz above about 8.3 kHz at Q 0.707**: the 14 kHz master low-pass of L5/beat 4 blows up to infinity (measured). If Chamberlin is kept, clamp `f < sqrt(q^2+4) - q`.
3. **Limiter ceiling**: `0.89*tanh(x/0.89)` has a -1.01 dBFS asymptote, not -1.5. Use **0.84** (-1.51 dBFS) to meet 14.3.3's "sample peak below -1.5 dBFS".
4. **Sample rate is not guaranteed to be 48 kHz**: `ProjectSettings/AudioManager.asset` has `m_SampleRate: 0`, which means the device default (44.1 kHz on many machines). Compute every coefficient from `AudioSettings.outputSampleRate` and rebuild on `OnAudioConfigurationChanged`. Offline renders use 48 kHz.
5. **Reverb**: 8-line FDN, Householder (`y_i = x_i - 0.25*sum`), base primes 1427 to 3539 samples (48 kHz) times a fixed **size factor 2.2** (re-primed at init; 0.894 s of total delay, enough modal density for RT60 up to about 6 s). Per-line `g_i = 10^(-3 M_i/(RT60 fs))` and a unity-DC one-pole damping filter. Pre-delay changes use a 1 s crossfade between two taps (never move a read pointer).
6. **Parameter smoothing**: 20 ms one-pole per sample for gains and cutoffs. Slow (8 s) reverb blends run at control rate (every 32 samples) in **double**, because a float one-pole with a 1.7 s tau stalls about 0.5 % short of its target.
7. **Analysis**: compute peak, DC, step, RMS per beat and BS.1770 LUFS **in C#** inside the EditMode test (fast, no dependencies). The numpy script is the cross-check. **scipy is not installed and `python` (3.12) has no numpy; use `py -3.10 -I`.**
8. **Player proof**: `-audio-dump` arms a preallocated linear capture written from `OnAudioFilterRead`, and the main thread writes a float32 WAV once it is full. Set `Application.runInBackground = true` in capture/autoplay mode (the project has `runInBackground: 0`, so the player pauses when it is unfocused).

## 1. Verified Unity facts

| Fact | Source |
|---|---|
| `OnAudioFilterRead(float[] data, int channels)`: inserted into the DSP chain in Inspector order; called about every 20 ms; data in [-1,1], **interleaved**; "If this is the first filter in the chain and a clip isn't attached to the audio source, this filter will be played as the audio source"; runs on the **audio thread**, so "calling into many Unity functions from this function is not allowed"; not supported on Web. | docs.unity.com 6000.3 OnAudioFilterRead (S1) |
| The docs example uses `[RequireComponent(typeof(AudioSource))]` and reads `AudioSettings.dspTime`/`outputSampleRate` in `Start` (main thread). Note that it calls `Debug.Log` from the callback; do not copy that. | S1 |
| Listener filters process the mixed output of every AudioSource unless `AudioSource.bypassListenerEffects` is set, and do **not** apply to a source routed into a mixer group. | `UnityEngine.AudioModule.xml:2455-2459` |
| Components implementing `OnAudioFilterRead` "may only be used by either one AudioSource or AudioListener at a time" (editor error). So never put AudioSources on the same GameObject as `ThreadSynth`+AudioListener. | UUM-61733 (S6) |
| UUM-116871 "OnAudioFilterRead does not affect signal on AudioListener after enable": fixed in 6000.6.0a5 (and 6000.0.77f1, 6000.3.17f1, 6000.4.10f1). 6000.6.0f1 contains the fix. Still: add the component once at startup and never toggle `enabled`; mute with a DSP gain instead. | S5 |
| UUM-117120: an OnAudioFilterRead generator on an AudioSource **does not stop** on `AudioSource.Stop()` (open). Do not rely on Stop(); use a DSP gain. | S6 |
| `AudioSettings.outputSampleRate`: "Get the mixer's current output rate." Main-thread API; cache it. | `AudioModule.xml:2341-2345` |
| `AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers)`. The project uses `m_DSPBufferSize: 1024`, `m_RequestedDSPBufferSize: 0`, so 1024 frames is about 21.3 ms at 48 kHz. **Still handle any `data.Length`.** | `AudioModule.xml:2365-2371`; `ProjectSettings/AudioManager.asset` |
| `AudioSettings.OnAudioConfigurationChanged(bool deviceWasChanged)` fires on `Reset` **or** on an OS sample-rate or device change (HDMI, USB headset). The docs example re-`Play()`s its source afterwards. Read the new rate from `AudioSettings.GetConfiguration().sampleRate`. | `AudioModule.xml:2351-2356, 2424, 2430-2432, 1782`; S2 |
| `AudioClip.Create(string name, int lengthSamples, int channels, int frequency, bool stream)`: lengthSamples counts **frames**. Managed checks throw `ArgumentException` for length/channels/frequency <= 0 and `NullReferenceException` for a null name. | `AudioModule.xml:1502-1597`; UnityCsReference Audio.bindings.cs (S3) |
| `AudioClip.SetData(float[] data, int offsetSamples)` and `SetData(ReadOnlySpan<float>, int)` both exist in 6000.6. The offset is in **frames**; returns false (and logs) if the clip has no data; throws if the offset is out of range or the data is empty. | `AudioModule.xml:1659-1678`; S3 |
| `PlayOneShot(clip, volumeScale)` = null check + `extern PlayOneShotHelper`: **no managed allocation**. But one-shots share the source's `pitch`/`panStereo` and cannot be stopped individually, so the spec's per-step pitch ±3 % and pan ±0.15 need a **pool of AudioSources with `clip`+`Play()`**. | S3; `AudioModule.xml:2728-2740` |
| `panStereo` "only applies to sounds that are Mono or Stereo"; `spatialBlend = 0` keeps sources 2D. | `AudioModule.xml:2535-2538` |
| `AudioListener.GetOutputData(float[] samples, int channel)` gives a block of the master output on the main thread (length a power of 2): a cheap "not silent" check with no capture. | `AudioModule.xml:1898-1902` |
| `AudioRenderer.Start/Render(NativeArray<float>)/Stop` records the main output in step with the capture frame rate (Recorder-style), but "Unity will output silence until AudioRenderer.Stop". Use it only if a video capture needs frame-locked audio. | `AudioModule.xml:1948-1983` |
| 6000.6 also has `AudioSource.generator` / `IAudioGenerator` / `GeneratorInstance` (new scriptable generators). Not needed and less documented, so stay with `OnAudioFilterRead`. | `AudioModule.xml:829-890, 2480-2489` |
| `-batchmode`: "doesn't display anything or accept user input"; `-nographics` only skips the graphics device. **Neither documents audio behaviour.** A headless Linux report shows audio init still being attempted (ALSA errors). EditMode tests never call `OnAudioFilterRead` anyway, so render offline through the pure `ThreadDsp`. Keep `m_DisableAudio: 0` (currently 0). | S4; S7 |
| `Application.runInBackground` is 0 in `ProjectSettings.asset`. `dspTime` is reported to freeze in the background without Run In Background (forum). Set it from code in autoplay/capture runs. | `ProjectSettings/ProjectSettings.asset`; S8 |

## 2. Engine pattern (main thread / audio thread contract)

- **Audio thread (`OnAudioFilterRead`)**: no UnityEngine calls (no `Time`, `Debug.Log`, `Mathf` is fine but use `System.Math`), no `new`, no LINQ, no `foreach` over interfaces, no `string` work, no locks. Read targets, run `ThreadDsp.Process`, copy into the capture, increment the heartbeat. Wrap the body in `try/catch (Exception e) { lastError = e; }` (storing a reference does not allocate) and let the main thread log it once, so log scans stay clean and the callback never dies silently.
- **Footsteps from AudioSources are already in `data`**. The synth must **add** its layers (`data[i] += ...`) and then apply the DC-block, reverb and limiter to the sum. Overwriting `data` would delete the footsteps.
- **Parameters**: each smoothed parameter is an independent `volatile float` (C# allows `volatile float`), or the whole `ThreadAudioParams` struct goes through a double-buffer mailbox (snippet C). A torn read only means one block of mixed old and new targets, and the per-sample smoothing hides it.
- **NaN guard**: once per block, if the summed `|x|` is not finite, reset every DSP state, output silence for that block and raise a flag. One NaN in an FDN poisons it forever.
- **Denormals**: assume Mono and IL2CPP do not set FTZ/DAZ (not verified; flushing is cheap either way). Flush feedback-loop writes below 1e-15 (done in the FDN), and call `FlushDenormals()` on IIR states once per block. This matters after the final fade-out and during pause ducking.
- **Static init**: `SineTable` has a static constructor (16 KB allocation). Call `SineTable.Warm()` in `Awake` so it never runs on the audio thread.
- **Channels**: the project speaker mode is stereo (`Default Speaker Mode: 2`), but write L to channel 0, R to channel 1 and leave channels 2 and up unchanged, so other layouts do not crash.
- **Footstep clips**: render at `AudioSettings.outputSampleRate` (no resampling) as mono clips, normalise each variant to its exact peak target, use a 1 ms attack and a 40-60 ms cosine fade-out to an exact zero. Read every spec "decay" as **T60** (`env = exp(-6.9078 t / T60)`) so clips stay short. Pool rule: clip length <= poolSize / maxStepRate - 0.05 s, so 4 sources at 3.2 steps/s allow 1.2 s. Steal the **oldest** voice. `AudioClip`s created at runtime must be `Destroy`ed on teardown.
- **Editor CPU**: the measured cost of a whole stereo chain (pink + SVF + sine + DC + FDN + tanh) was 0.45 % of real time on .NET 10. Mono in Editor Debug code optimisation is slower (estimated 5-10x, not measured), which would still be fine. The Inspector VU meter shows ms per callback and turns red when starved.
- **Pause**: duck through a DSP gain (smoothed). Do not use `AudioListener.pause`; it pauses sources abruptly.

## 3. Measured DSP facts (harness output, 48 kHz)

| Item | Result |
|---|---|
| Chamberlin LP, fc 14 kHz, Q 0.707, `f = 2 sin(pi fc/fs)` unclamped | **diverges to infinity** within 1 s of noise |
| Chamberlin stability bound (Jury on the 2x2 state matrix): `f^2 + 2 f q < 4`, so `f < sqrt(q^2+4) - q` | fc_max at 48 kHz: Q 0.5: 6.5 kHz, Q 0.707: 8.3 kHz, Q 0.9: 9.6 kHz, Q 1: 10.2 kHz, Q 2: 13.7 kHz, Q 4: 16.5 kHz |
| Simper SVF LP Q 0.7071 gain at fc = 1.1 / 6 / 14 kHz | -3.01 / -3.01 / -3.01 dB (exact, stable) |
| Simper `k*band` (normalised BP) peak at fc, Q 2 | 0.00 dB (the raw band output peaks at +Q) |
| FDN size 2.2, no damping: target RT60 0.9 / 3.2 / 7.0 s, measured T30 | 0.86 / 3.19 / 7.01 s (size 1.8: 0.15 s gave 0.13 s) |
| FDN size 1.8 with 2.2 kHz damping, broadband T30 for target 0.9 / 3.2 / 7 | 0.72 / 2.74 / 6.03 s (the RT60 formula holds at low frequencies; HF dies sooner, as intended) |
| FDN tail energy with energy normalisation, wet 0 dB, no damping (size 2.2) | -0.6 / +1.7 / +1.9 dB for RT 0.9 / 3.2 / 7 s; **-16 dB at 0.15 s** (size 1.8; lines longer than the decay). Calibrate per beat in the test. |
| FDN jump RT 7 to 0.15 s plus pre-delay 200 to 10 ms on running noise | max wet sample step 3.0e-3 during the transition vs 3.2e-3 steady, so **no click** |
| `Footsteps.RenderSalt` (below) | 7680 frames (0.16 s), peak exactly -30.00 dBFS, first sample 0, last about 3e-14, no NaN |
| `tanh` limiter c = 0.84 | asymptote -1.51 dBFS; gain -0.04 dB at a -20 dBFS input, **-0.95 dB at -6 dBFS** (the beat-4 swell is slightly compressed) |
| Allocation over 10 s of the chain | 0 bytes (the 40 bytes measured were the Stopwatch) |
| Sine table 4096 + linear interpolation, max error | 3.5e-7 (-129 dB) |
| Kellet pink, uniform white in [-1,1), 48 kHz | RMS 1.755, so `UnitRms = 1/1.755` gives RMS 1.005, peak 4.86 (crest 13.7 dB). Slope deviation from -3 dB/oct is within ±0.05 dB from 20 Hz to 20 kHz even at 48 kHz |
| DC blocker 10 Hz | R = 0.99869186 (48 kHz), 0.99857626 (44.1 kHz) |
| One-pole 20 ms at 48 kHz | a = exp(-1/(0.02*48000)) = 0.99895888; coefficient 1-a = 0.00104112 |
| K-weighting magnitude | 27.5 Hz -9.27, 36.71 Hz -6.31, 55 Hz -3.36, 110 Hz -0.94, 440 Hz 0.01, 1 kHz +0.70, 4 kHz +3.97 dB |
| LUFS check: 997 Hz 0 dBFS sine on L only | -3.01 LUFS in both C# and Python (reference -3.01) |

**Consequence for 14.3.3**: LUFS sums channel powers (a centred signal reads +3 dB above its per-channel dBFS RMS) and K-weighting removes 3-6 dB from the 36.7/55 Hz drone. So "-30 LUFS integrated" and "RMS -38 to -26 dBFS" are different scales; test them separately and expect the drone-heavy beats to read lower in LUFS.

## 4. FDN details

- Base primes (48 kHz, 29.7-73.7 ms): `1427 1783 1973 2099 2579 2879 3221 3539`. They are multiplied by `size * fs / 48000` and re-primed (`NearestPrime`) **once at construction**.
  - Size 2.2 at 48 kHz: `3137 3923 4339 4621 5669 6337 7079 7789` (total 42 894 = 0.894 s).
  - Size 2.2 at 44.1 kHz: `2887 3607 3989 4243 5209 5821 6521 7151`.
- Mode-density rule (Schroeder/Logan via JOS): sum of M >= 0.15 * t60 * fs. Size 1.0 is enough only up to 2.7 s, 1.8 up to 4.9 s, 2.2 up to 6.0 s. Beat 3 (7 s) is marginal, but its 2.2 kHz damping hides the colouration. Above size 2.4 (lines over 170 ms), expect single echoes on footsteps when the RT is short (estimate, not measured).
- Gains: `g_i = 10^(-3 M_i / (RT60 fs))`, clamped to 0.9995. Recompute every 32 samples from the smoothed RT60.
- Damping, simple form (spec): in-loop `lp += (1 - e^(-2 pi fd/fs)) (y - lp)`. Unity DC gain, so the low-frequency RT60 is exact.
- Damping, exact Jot form (JOS "First-Order Delay Filter Design"), if a Nyquist RT is wanted: `H_i(z) = g_i/(1 - p_i z^-1)`, `R0 = 10^(-3 M_i T/t60(0))`, `Rpi = 10^(-3 M_i T/t60(pi))`, `g_i = 2 R0 Rpi/(R0 + Rpi)`, `p_i = (R0 - Rpi)/(R0 + Rpi)`.
- Householder N = 8: `A = I - (2/8) 1 1^T` is orthogonal (lossless), with O(N) cost. Outputs are taken with two Hadamard rows (`++--++--`, `+--++--+`) that are orthogonal to each other and to the all-ones vector, which gives decorrelated L/R.
- Changing only gains, damping and wet levels does not click. Delay lengths stay fixed. Pre-delay changes crossfade between two taps over 1 s (a linear crossfade of a delayed copy causes brief comb filtering on the wet signal only).
- "Wet dB rel. dry": with energy normalisation `wetNorm = wet * sqrt(2 N Mmean / tau)`, `tau = RT60 fs / ln 1000`, the tail energy of an impulse is about equal to `wet` for RT >= 0.9 s. Adding the wet tail raises the master RMS by about 0.2 dB at -14 dB wet, so the RMS-per-beat checks still pass.

## 5. Snippets (compile-verified)

### A. `ThreadDspBlocks.cs` (pure C#, no UnityEngine, zero allocation after construction)

Namespace note: do **not** name a namespace `Thread`, because it shadows `System.Threading.Thread` in every file that imports both. `ThreadGame.Audio` is used here.

```csharp
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThreadGame.Audio
{
    public struct XorShift32
    {
        uint s;
        public XorShift32(uint seed) { s = seed != 0u ? seed : 0x9E3779B9u; } // state must never be 0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt() { uint x = s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; s = x; return x; }
        public float NextBipolar() { return unchecked((int)NextUInt()) * (1.0f / 2147483648.0f); } // [-1, 1)
        public float Next01() { return (NextUInt() >> 8) * (1.0f / 16777216.0f); }                   // [0, 1)
    }

    public struct PinkKellet   // Paul Kellet refined ("instrumentation grade") pinking filter
    {
        float b0, b1, b2, b3, b4, b5, b6;
        public const float UnitRms = 1f / 1.755f;  // unit RMS for uniform white in [-1,1)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next(float white)
        {
            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
            b6 = white * 0.115926f;
            return pink * UnitRms;
        }
    }

    public struct Smoother     // one-pole; double state so second-long taus converge
    {
        double y, a;
        public float Value { get { return (float)y; } }
        public void Init(float value, float tauSeconds, float rate) { y = value; a = 1.0 - Math.Exp(-1.0 / (tauSeconds * rate)); }
        public void Reset(float value) { y = value; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next(float target) { y += a * (target - y); return (float)y; }
    }

    public struct SvfTpt       // Simper/Cytomic linear trapezoidal SVF
    {
        float ic1, ic2, a1, a2, a3, k;
        public void Set(float fc, float q, float fs)   // call per sub-block (e.g. every 16-32 samples)
        {
            double f = Math.Min(Math.Max(fc, 1.0), 0.49 * fs);
            double g = Math.Tan(Math.PI * f / fs);
            double kk = 1.0 / Math.Max(q, 0.05);
            double A1 = 1.0 / (1.0 + g * (g + kk));
            k = (float)kk; a1 = (float)A1; a2 = (float)(g * A1); a3 = (float)(g * g * A1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick(float x, out float low, out float band, out float high)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            low = v2; band = v1; high = x - k * v1 - v2;
        }
        public float Low(float x) { float l, b, h; Tick(x, out l, out b, out h); return l; }
        public float BandNorm(float x) { float l, b, h; Tick(x, out l, out b, out h); return k * b; } // 0 dB peak
        public void FlushDenormals() { if (Math.Abs(ic1) < 1e-15f) ic1 = 0f; if (Math.Abs(ic2) < 1e-15f) ic2 = 0f; }
    }

    // Chamberlin (only if the spec form is kept): low += f*band; hp = x - low - q*band; band += f*hp;
    // with q = 1/Q and f = Math.Min(2*Math.Sin(Math.PI*fc/fs), 0.9*(Math.Sqrt(q*q + 4) - q))  (stability clamp)

    public struct DcBlocker    // y = x - x1 + R y1, R = exp(-2 pi fc / fs)
    {
        float x1, y1, r;
        public void Init(float fc, float fs) { r = (float)Math.Exp(-2.0 * Math.PI * fc / fs); x1 = 0f; y1 = 0f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float x) { float y = x - x1 + r * y1; x1 = x; y1 = y; return y; }
    }

    public static class SineTable
    {
        public const int Size = 4096;
        static readonly float[] T = Build();
        static float[] Build()                                         // guard sample: T[i + 1] never wraps
        { var t = new float[Size + 1]; for (int i = 0; i <= Size; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / Size); return t; }
        public static void Warm() { Lookup(0.0); }                     // main thread, in Awake
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lookup(double phase01)                     // phase01 in [0, 1)
        { double p = phase01 * Size; int i = (int)p; float fr = (float)(p - i); i &= Size - 1; return T[i] + fr * (T[i + 1] - T[i]); }
    }

    public struct SineOsc
    {
        public double Phase;                                           // [0,1), double: no drift
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next(double freqHz, double invFs)
        { float s = SineTable.Lookup(Phase); Phase += freqHz * invFs; if (Phase >= 1.0) Phase -= 1.0; return s; }
    }

    public static class Limiter
    {
        public const float Ceiling = 0.84f;                            // -1.51 dBFS (0.89 = -1.01 dBFS)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SoftClip(float x, float ceiling) { return ceiling * (float)Math.Tanh(x / ceiling); }
        public static float DbToLin(float db) { return (float)Math.Pow(10.0, db / 20.0); }
    }

    public sealed class FdnReverb8
    {
        const int N = 8;
        static readonly int[] BasePrimes48k = { 1427, 1783, 1973, 2099, 2579, 2879, 3221, 3539 };
        readonly float[][] line = new float[N][];
        readonly int[] len = new int[N], pos = new int[N];
        readonly float[] lp = new float[N], gain = new float[N], v = new float[N];
        readonly float[] pre; readonly int preFrames;
        int preW, tapA, tapB, tPreSmp, ctl;
        float xf = 1f;
        readonly float xfStep, fs, meanLen;
        Smoother sRt60, sDamp, sWet;
        float tRt60 = 1f, tDamp = 7000f, tWet, dampC, wetNorm;

        public FdnReverb8(float sampleRate, float sizeFactor, float maxPreDelaySeconds)
        {
            fs = sampleRate; float sum = 0f;
            for (int i = 0; i < N; i++)
            {
                int l = NearestPrime((int)Math.Round(BasePrimes48k[i] * sizeFactor * fs / 48000.0));
                len[i] = l; line[i] = new float[l]; sum += l;
            }
            meanLen = sum / N;
            preFrames = (int)(maxPreDelaySeconds * fs) + 2;
            pre = new float[2 * preFrames];
            float ctlRate = fs / 32f;
            sRt60.Init(1f, 1.74f, ctlRate); sDamp.Init(7000f, 1.74f, ctlRate); sWet.Init(0f, 1.74f, ctlRate); // 99 % in 8 s
            xfStep = 1f / fs;                                          // 1 s pre-delay tap crossfade
            UpdateControl();
        }

        public void SetTargets(float rt60Seconds, float preDelayMs, float wetDbRelDry, float dampHz) // audio thread
        {
            tRt60 = Math.Max(0.05f, rt60Seconds); tDamp = dampHz; tWet = Limiter.DbToLin(wetDbRelDry);
            int p = Math.Min((int)(preDelayMs * 0.001f * fs), preFrames - 2);
            if (p != tPreSmp && xf >= 1f) { tapA = tapB; tapB = p; xf = 0f; tPreSmp = p; }
        }

        public void SnapToTargets()
        { sRt60.Reset(tRt60); sDamp.Reset(tDamp); sWet.Reset(tWet); tapA = tapB = tPreSmp; xf = 1f; UpdateControl(); }

        void UpdateControl()
        {
            float rt = sRt60.Next(tRt60), dHz = sDamp.Next(tDamp), w = sWet.Next(tWet);
            for (int i = 0; i < N; i++)
                gain[i] = Math.Min((float)Math.Pow(10.0, -3.0 * len[i] / (rt * fs)), 0.9995f); // 10^(-3 M/(RT60 fs))
            dampC = dHz >= 0.45f * fs ? 1f : 1f - (float)Math.Exp(-2.0 * Math.PI * dHz / fs);
            float tau = rt * fs / 6.9078f;
            wetNorm = w * (float)Math.Sqrt(2.0 * N * meanLen / tau);   // tail energy ~ wet^2 (RT >= 0.9 s)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(float inL, float inR, out float outL, out float outR)
        {
            if (++ctl >= 32) { ctl = 0; UpdateControl(); }
            pre[2 * preW] = inL; pre[2 * preW + 1] = inR;
            int ra = preW - tapA; if (ra < 0) ra += preFrames;
            int rb = preW - tapB; if (rb < 0) rb += preFrames;
            if (xf < 1f) { xf += xfStep; if (xf > 1f) xf = 1f; }
            float dL = pre[2 * ra] + xf * (pre[2 * rb] - pre[2 * ra]);
            float dR = pre[2 * ra + 1] + xf * (pre[2 * rb + 1] - pre[2 * ra + 1]);
            if (++preW >= preFrames) preW = 0;
            float sum = 0f;
            for (int i = 0; i < N; i++)
            {
                float y = line[i][pos[i]];
                lp[i] += dampC * (y - lp[i]);                          // unity-DC damping
                float g = gain[i] * lp[i];
                v[i] = g; sum += g;
            }
            float h = 0.25f * sum;                                     // Householder, N = 8
            for (int i = 0; i < N; i++)
            {
                float x = v[i] - h + 0.5f * ((i & 1) == 0 ? dL : dR);
                if (x < 1e-15f && x > -1e-15f) x = 0f;                 // no denormals in the loop
                line[i][pos[i]] = x;
                if (++pos[i] >= len[i]) pos[i] = 0;
            }
            const float c = 0.35355339f;
            outL = wetNorm * c * (v[0] + v[1] - v[2] - v[3] + v[4] + v[5] - v[6] - v[7]);
            outR = wetNorm * c * (v[0] - v[1] - v[2] + v[3] + v[4] - v[5] - v[6] + v[7]);
        }

        static int NearestPrime(int n)
        { for (int d = 0; ; d++) { if (IsPrime(n - d)) return n - d; if (IsPrime(n + d)) return n + d; } }
        static bool IsPrime(int n)
        { if (n < 2) return false; for (int k = 2; k * k <= n; k++) if (n % k == 0) return false; return true; }
    }

    public static class Footsteps   // example: salt crust (14.3.5); decays read as T60
    {
        public static int RenderSalt(float[] dst, int fs, ref XorShift32 rng, float peakDbfs)
        {
            int n = Math.Min(dst.Length, (int)(0.16f * fs));
            Array.Clear(dst, 0, dst.Length);
            var bp = new SvfTpt(); bp.Set(3200f, 0.8f, fs);
            var osc = new SineOsc(); double inv = 1.0 / fs;
            for (int i = 0; i < n; i++)                                // band noise (T60 70 ms) + 85 Hz thump (T60 90 ms, -6 dB)
            {
                float t = i / (float)fs, att = Math.Min(1f, t / 0.001f);
                dst[i] = att * (bp.BandNorm(rng.NextBipolar()) * (float)Math.Exp(-6.9078 * t / 0.070)
                              + 0.5f * osc.Next(85.0, inv) * (float)Math.Exp(-6.9078 * t / 0.090));
            }
            int clicks = 6 + (int)(rng.Next01() * 5f), w = Math.Max(1, fs / 1000);   // 6-10 micro-clicks, 1 ms
            for (int c = 0; c < clicks; c++)
            {
                int at = (int)((0.005f + 0.030f * rng.Next01()) * fs); double hz = 1000.0 + 5000.0 * rng.Next01(), ph = 0;
                for (int j = 0; j < w && at + j < n; j++, ph += hz * inv)
                    dst[at + j] += 0.4f * (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Sin(Math.PI * j / w);
            }
            int fade = (int)(0.04f * fs);
            for (int i = n - fade; i < n; i++) dst[i] *= 0.5f + 0.5f * (float)Math.Cos(Math.PI * (i - (n - fade)) / fade);
            float pk = 1e-9f; for (int i = 0; i < n; i++) pk = Math.Max(pk, Math.Abs(dst[i]));
            float k = Limiter.DbToLin(peakDbfs) / pk; for (int i = 0; i < n; i++) dst[i] *= k;
            return n;                                                  // frames to pass to AudioClip.Create
        }
    }

    public static class WavWriter   // main/test thread only
    {
        // float32: "RIFF" | 4+26+12+8+data | "WAVE" | "fmt " 18: tag 3, ch, rate, rate*ch*4, ch*4, 32, cbSize 0
        //          | "fact" 4: frames | "data" bytes | samples  (58-byte header; non-PCM needs cbSize + fact)
        public static void WriteFloat32(string path, float[] interleaved, int sampleCount, int channels, int sampleRate)
        {
            int dataBytes = sampleCount * 4;
            using (var w = new BinaryWriter(File.Create(path)))        // BinaryWriter is little-endian
            {
                w.Write(0x46464952); w.Write(4 + 26 + 12 + 8 + dataBytes); w.Write(0x45564157);
                w.Write(0x20746D66); w.Write(18);
                w.Write((short)3); w.Write((short)channels); w.Write(sampleRate);
                w.Write(sampleRate * channels * 4); w.Write((short)(channels * 4)); w.Write((short)32); w.Write((short)0);
                w.Write(0x74636166); w.Write(4); w.Write(sampleCount / channels);
                w.Write(0x61746164); w.Write(dataBytes);
                w.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<float>(interleaved, 0, sampleCount))); // x64 is LE
            }
        }
        // PCM16 (44-byte header): "RIFF" | 36+data | "WAVE" | "fmt " 16: tag 1, ch, rate, rate*ch*2, ch*2, 16
        // | "data" bytes | short samples = (short)Math.Round(clamp(x,-1,1)*32767). Prefer float32 for analysis:
        // no quantisation, and overs above 1.0 stay visible.
    }

    public sealed class AudioCapture  // SPSC: audio thread writes, main thread reads after IsFull
    {
        readonly float[] buf; int count;
        volatile int state;                                            // 0 idle, 1 armed, 2 recording, 3 full
        public int Channels { get; private set; }
        public AudioCapture(int seconds, int sampleRate, int maxChannels) { buf = new float[seconds * sampleRate * maxChannels]; }
        public void Arm() { count = 0; state = 1; }
        public bool IsFull { get { return state == 3; } }              // volatile read = acquire
        public int Count { get { return count; } }
        public float[] Buffer { get { return buf; } }
        public void Write(float[] data, int channels)                  // audio thread, no allocation
        {
            int st = state;
            if (st == 1) { Channels = channels; st = 2; }
            if (st != 2) return;
            int n = Math.Min(data.Length, buf.Length - count);
            n -= n % channels;
            Array.Copy(data, 0, buf, count, n);
            count += n;
            state = (buf.Length - count < channels || n == 0) ? 3 : 2; // volatile write = release
        }
    }
}
```

Master chain order per frame (14.3.1): `sum layers + data[i]` -> `DcBlocker(10 Hz)` -> master `SvfTpt` LP (L5 sweep) -> `FdnReverb8.Process` (dry + wet) -> `Limiter.SoftClip(x, 0.84f)` -> `data[i]`. Call `SvfTpt.Set` per sub-block of 16-32 frames from smoothed cutoffs (one `Math.Tan` each), and smooth gains per sample with `Smoother` (tau 20 ms at fs).

### B. Offline loudness in C# (EditMode test; allocates, never in the player callback)

```csharp
using System;
using System.Collections.Generic;

namespace ThreadGame.Audio
{
    public struct Biquad   // transposed direct form II, a0 = 1, y = b0 x + ... - a1 y1 - a2 y2
    {
        public double b0, b1, b2, a1, a2, z1, z2;
        public double Process(double x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
    }

    public static class Loudness
    {
        // libebur128 formulas; at 48 kHz they reproduce the BS.1770 table to 1e-14:
        // shelf b = 1.53512485958697, -2.69169618940638, 1.19839281085285; a = -1.69065929318241, 0.73248077421585
        // RLB   b = 1, -2, 1; a = -1.99004745483398, 0.99007225036621
        public static void KWeighting(double fs, out Biquad shelf, out Biquad hp)
        {
            double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
            double K = Math.Tan(Math.PI * f0 / fs), Vh = Math.Pow(10.0, G / 20.0), Vb = Math.Pow(Vh, 0.4996667741545416);
            double a0 = 1.0 + K / Q + K * K;
            shelf = new Biquad { b0 = (Vh + Vb * K / Q + K * K) / a0, b1 = 2.0 * (K * K - Vh) / a0, b2 = (Vh - Vb * K / Q + K * K) / a0,
                                 a1 = 2.0 * (K * K - 1.0) / a0, a2 = (1.0 - K / Q + K * K) / a0 };
            f0 = 38.13547087602444; Q = 0.5003270373238773; K = Math.Tan(Math.PI * f0 / fs);
            a0 = 1.0 + K / Q + K * K;
            hp = new Biquad { b0 = 1.0, b1 = -2.0, b2 = 1.0, a1 = 2.0 * (K * K - 1.0) / a0, a2 = (1.0 - K / Q + K * K) / a0 };
        }

        // Integrated LUFS (400 ms blocks, 75 % overlap, abs gate -70, rel gate -10 LU), L/R weight 1.
        public static double Integrated(float[] x, int channels, int fs, out double maxMomentary, out double maxShortTerm)
        {
            int frames = x.Length / channels, hop = fs / 10;
            var sub = new List<double>();
            var s = new Biquad[channels]; var h = new Biquad[channels];
            for (int c = 0; c < channels; c++) KWeighting(fs, out s[c], out h[c]);
            double acc = 0; int n = 0;
            for (int f = 0; f < frames; f++)
            {
                for (int c = 0; c < channels; c++) { double y = h[c].Process(s[c].Process(x[f * channels + c])); acc += y * y; }
                if (++n == hop) { sub.Add(acc / hop); acc = 0; n = 0; }
            }
            maxMomentary = WindowMax(sub, 4); maxShortTerm = WindowMax(sub, 30);
            var blocks = new List<double>();
            for (int i = 0; i + 4 <= sub.Count; i++) blocks.Add((sub[i] + sub[i + 1] + sub[i + 2] + sub[i + 3]) * 0.25);
            double absSum = 0; int absN = 0;
            foreach (var z in blocks) if (Lk(z) > -70.0) { absSum += z; absN++; }
            if (absN == 0) return double.NegativeInfinity;
            double rel = Lk(absSum / absN) - 10.0, sum = 0; int cnt = 0;
            foreach (var z in blocks) { double l = Lk(z); if (l > -70.0 && l > rel) { sum += z; cnt++; } }
            return cnt == 0 ? double.NegativeInfinity : Lk(sum / cnt);
        }
        static double Lk(double z) { return -0.691 + 10.0 * Math.Log10(Math.Max(z, 1e-30)); }
        static double WindowMax(List<double> sub, int w)
        {
            double best = double.NegativeInfinity;
            for (int i = 0; i + w <= sub.Count; i++) { double a = 0; for (int j = 0; j < w; j++) a += sub[i + j]; best = Math.Max(best, Lk(a / w)); }
            return best;
        }
    }
}
```

The same test also computes sample peak (`max|x|`), DC (`mean` per channel), max `|x[n]-x[n-1]|` (excluding footstep windows) and RMS per beat window. These are one-pass loops over the render.

### C. Unity glue (main thread + audio thread)

```csharp
using System;
using System.Threading;
using UnityEngine;

namespace ThreadGame.Audio
{
    public sealed class ParamMailbox<T> where T : struct   // main thread publishes, audio thread reads
    {
        T a, b; volatile int front;
        public void Publish(in T value) { if (front == 0) { b = value; front = 1; } else { a = value; front = 0; } }
        public void Read(out T value) { value = front == 0 ? a : b; }   // a torn read is harmless (smoothed)
    }

    [DisallowMultipleComponent]
    public sealed class ThreadSynthSketch : MonoBehaviour     // shape of ThreadSynth; DSP call elided
    {
        FdnReverb8 reverb; int callbacks; Exception lastError; float startTime;
        [NonSerialized] public volatile AudioCapture Capture; // non-null only with -audio-dump
        public int Callbacks { get { return Volatile.Read(ref callbacks); } }
        public int SampleRate { get; private set; }

        void Awake()
        {
            SineTable.Warm();
            SampleRate = AudioSettings.outputSampleRate;      // 0 in AudioManager = device default (44.1k possible)
            int bufLen, numBufs; AudioSettings.GetDSPBufferSize(out bufLen, out numBufs);
            Volatile.Write(ref reverb, new FdnReverb8(SampleRate, 2.2f, 0.25f));
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            startTime = Time.unscaledTime;
        }
        void OnDestroy() { AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged; }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            int fs = AudioSettings.GetConfiguration().sampleRate;
            if (fs != SampleRate) { SampleRate = fs; Volatile.Write(ref reverb, new FdnReverb8(fs, 2.2f, 0.25f)); } // rebuild whole ThreadDsp
        }

        void Update()
        {
            var e = Interlocked.Exchange(ref lastError, null);
            if (e != null) Debug.LogException(e);
            if (Callbacks == 0 && Time.unscaledTime - startTime > 0.5f) { /* log + switch to AudioSource generator fallback */ }
            var cap = Capture;
            if (cap != null && cap.IsFull) { Capture = null; /* WavWriter.WriteFloat32(path, cap.Buffer, cap.Count, cap.Channels, SampleRate) on a worker */ }
        }

        void OnAudioFilterRead(float[] data, int channels)   // audio thread
        {
            try
            {
                var r = Volatile.Read(ref reverb);
                if (r == null) return;
                // ThreadDsp.Process(data, channels, in snapshot) goes here (adds layers, master chain)
                var cap = Capture;
                if (cap != null) cap.Write(data, channels);
                Interlocked.Increment(ref callbacks);
            }
            catch (Exception ex) { lastError = ex; }
        }
    }

    public sealed class FootstepPool                          // host GameObject must NOT carry ThreadSynth
    {
        readonly AudioSource[] sources; readonly float[] startedAt;
        public FootstepPool(GameObject host, int count)
        {
            sources = new AudioSource[count]; startedAt = new float[count];
            for (int i = 0; i < count; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.dopplerLevel = 0f;
                s.bypassReverbZones = true; s.bypassListenerEffects = false; s.priority = 64;
                sources[i] = s;
            }
        }
        public static AudioClip MakeClip(string name, float[] mono, int frames, int sampleRate)
        {
            var clip = AudioClip.Create(name, frames, 1, sampleRate, false);   // lengthSamples = frames
            clip.SetData(new ReadOnlySpan<float>(mono, 0, frames), 0);         // offset in frames
            return clip;
        }
        public void Play(AudioClip clip, float volume, float pitch, float pan)
        {
            int best = 0;
            for (int i = 1; i < sources.Length; i++) if (startedAt[i] < startedAt[best]) best = i; // steal oldest
            var s = sources[best];
            s.clip = clip; s.volume = volume; s.pitch = pitch; s.panStereo = pan;
            s.Play();
            startedAt[best] = Time.unscaledTime;
        }
    }
}
```

Fallback generator (if the heartbeat stays at 0): `var go = new GameObject("ThreadSynthSource"); go.SetActive(false); var src = go.AddComponent<AudioSource>(); src.playOnAwake = true; src.loop = true; src.spatialBlend = 0f; src.bypassListenerEffects = true; go.AddComponent<ThreadSynth>(); go.SetActive(true); src.Play();`. The callback then receives zeros and the synth writes its layers. Footsteps then bypass the synth's limiter; that is still safe at -28 dBFS peaks.

`-audio-dump`: parse `Environment.GetCommandLineArgs()` on the main thread, set `Application.runInBackground = true`, create `new AudioCapture(10, SampleRate, 2)` (3.8 MB, allocated before arming), `Arm()`, then on `IsFull` write `Audio/dump_player.wav` from a `System.Threading.Tasks.Task.Run` worker (no frame hitch). Check: no NaN, RMS above -60 dBFS, peak below -1.5 dBFS, RMS within ±3 dB of the offline render of the same s range. Sample-exact identity is impossible because block timing differs.

### D. Python cross-check (numpy only; run with `py -3.10 -I analyze_audio.py render.wav --window 1`)

The stdlib `wave` reads IEEE float only from **Python 3.15** on (installed: 3.10-3.14), so this parses chunks itself. K-weighting is applied by FFT overlap-add with the biquads' impulse response truncated at 2^15 samples (the HP pole radius is 0.995, so the tail is below 1e-12 after about 5.5k samples). This is exact to numerical precision without scipy. Memory is about 4 float64 copies of the file, so analyse **per-beat WAVs** (Part 11.9), not one 10-minute render.

```python
import sys, math, struct, json
import numpy as np

def read_wav(path):
    raw = open(path, "rb").read()
    assert raw[0:4] == b"RIFF" and raw[8:12] == b"WAVE"
    pos, fmt, data = 12, None, None
    while pos + 8 <= len(raw):
        cid, size = raw[pos:pos + 4], struct.unpack_from("<I", raw, pos + 4)[0]
        body = raw[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            tag, ch, fs, _, _, bits = struct.unpack_from("<HHIIHH", body, 0)
            if tag == 0xFFFE and size >= 40: tag = struct.unpack_from("<H", body, 24)[0]  # EXTENSIBLE
            fmt = (tag, ch, fs, bits)
        elif cid == b"data":
            data = body
        pos += 8 + size + (size & 1)
    tag, ch, fs, bits = fmt
    if tag == 3 and bits == 32: x = np.frombuffer(data, dtype="<f4").astype(np.float64)
    elif tag == 1 and bits == 16: x = np.frombuffer(data, dtype="<i2").astype(np.float64) / 32768.0
    else: raise ValueError("unsupported format %d/%d" % (tag, bits))
    return x.reshape(-1, ch), fs

def biquad_impulse(b, a, n):
    h = np.zeros(n); x1 = x2 = y1 = y2 = 0.0
    for i in range(n):
        x = 1.0 if i == 0 else 0.0
        y = b[0] * x + b[1] * x1 + b[2] * x2 - a[1] * y1 - a[2] * y2
        x2, x1, y2, y1 = x1, x, y1, y
        h[i] = y
    return h

def k_weighting(fs):
    f0, G, Q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    K = math.tan(math.pi * f0 / fs); Vh = 10 ** (G / 20); Vb = Vh ** 0.4996667741545416; a0 = 1 + K / Q + K * K
    s = ([(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0],
         [1.0, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0])
    f0, Q = 38.13547087602444, 0.5003270373238773
    K = math.tan(math.pi * f0 / fs); a0 = 1 + K / Q + K * K
    return s, ([1.0, -2.0, 1.0], [1.0, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0])

def fft_filter(x, h, block=1 << 18):
    n, m = x.shape[0], len(h)
    nfft = 1 << int(math.ceil(math.log2(block + m - 1)))
    H = np.fft.rfft(h, nfft); y = np.zeros((n + m - 1, x.shape[1]))
    for s in range(0, n, block):
        Y = np.fft.irfft(np.fft.rfft(x[s:s + block], nfft, axis=0) * H[:, None], nfft, axis=0)
        e = min(s + nfft, y.shape[0]); y[s:e] += Y[:e - s]
    return y[:n]

def loudness(x, fs):
    (b1, a1), (b2, a2) = k_weighting(fs)
    h = np.convolve(biquad_impulse(b1, a1, 1 << 15), biquad_impulse(b2, a2, 1 << 15))[:1 << 15]
    y = fft_filter(x, h); hop = fs // 10; nsub = y.shape[0] // hop
    sub = (y[:nsub * hop] ** 2).reshape(nsub, hop, -1).mean(axis=1).sum(axis=1)
    lk = lambda z: -0.691 + 10 * np.log10(np.maximum(z, 1e-30))
    win = lambda w: np.convolve(sub, np.ones(w) / w, mode="valid")
    blocks = win(4); g = blocks[lk(blocks) > -70]
    if g.size == 0: return float("-inf"), float("-inf"), float("-inf")
    g2 = blocks[(lk(blocks) > -70) & (lk(blocks) > lk(g.mean()) - 10)]
    return float(lk(g2.mean())), float(lk(blocks).max()), (float(lk(win(30)).max()) if nsub >= 30 else float("nan"))

def db(v): return 20 * math.log10(max(float(v), 1e-12))

def analyze(path, window_s=1.0):
    x, fs = read_wav(path); step = np.abs(np.diff(x, axis=0))
    w = int(window_s * fs); nw = x.shape[0] // w
    rms_w = np.sqrt((x[:nw * w] ** 2).reshape(nw, w, -1).mean(axis=(1, 2))) if nw else np.array([])
    m400 = int(0.4 * fs); c = np.concatenate(([0.0], np.cumsum((x ** 2).mean(axis=1))))
    ms = (c[m400:] - c[:-m400]) / m400                          # O(n) 400 ms moving mean square
    i, mm, st = loudness(x, fs)
    return {"fs": fs, "channels": x.shape[1], "seconds": x.shape[0] / fs, "nan_or_inf": int((~np.isfinite(x)).sum()),
            "sample_peak_dbfs": db(np.abs(x).max()), "dc_per_channel": [float(v) for v in x.mean(axis=0)],
            "max_step": float(step.max()), "max_step_at_s": float(step.max(axis=1).argmax() / fs),
            "rms_dbfs": db(np.sqrt((x ** 2).mean())), "max_400ms_rms_dbfs": db(np.sqrt(ms.max())),
            "rms_per_window_dbfs": [round(db(v), 2) for v in rms_w],
            "lufs_integrated": i, "lufs_momentary_max": mm, "lufs_short_term_max": st}

if __name__ == "__main__":
    ws = float(sys.argv[sys.argv.index("--window") + 1]) if "--window" in sys.argv else 1.0
    print(json.dumps(analyze(sys.argv[1], ws), indent=1))
```

Validated: the 997 Hz sine on L only gives -3.01 LUFS. A 30 s pink+drone test with a 10 s gap gives I = -35.83 LUFS (C# -35.83), and the gap is correctly removed by the -70 LUFS absolute gate; `max_step_at_s` found the hard gate edge.

## 6. Pitfalls checklist

1. Filter and limiter: Chamberlin above its bound gives an infinite/NaN output and then a permanently dead reverb, so use Simper or clamp. A 0.89 limiter ceiling fails the -1.5 dBFS target, so use 0.84. The tanh limiter is not transparent above about -10 dBFS.
2. Never hardcode 48000. Rebuild the DSP on a device change (build the new instance on the main thread and swap it atomically). Smoothers with taus of seconds run in double or at control rate. Never move a delay read pointer; crossfade taps instead.
3. In the callback: no `Debug.Log`, `Time.*`, `GetComponent` or UnityEngine `Random` (use `XorShift32`). No `?.` or `??` on UnityEngine.Object fields.
4. Footsteps: `Play()` on a still-ringing source clicks, so size the pool and clip length and fade every clip to an exact zero. `PlayOneShot` shares pitch and pan across voices. Sources routed into an `AudioMixerGroup` skip listener filters, so do not use mixers.
5. `AudioSource.Stop()` does not stop an OnAudioFilterRead generator (UUM-117120). `runInBackground: 0` pauses an unfocused player. Gate with DSP gains, and set `Application.runInBackground = true` in autoplay/dump modes.
6. LUFS is not dBFS RMS (+3 dB stereo power summation; K-weighting is -6.3 dB at 36.7 Hz). Python: `python` = 3.12 without numpy, `py -3.10` has numpy 2.2.6, there is no scipy, and stdlib `wave` cannot read float WAV before 3.15.
7. Kellet output is not unit gain. Multiply by `1/1.755`; the crest factor is about 13.7 dB, so at -40 dBFS RMS the wind peaks near -26 dBFS. Naive pad triangles alias about -84 dB down, which is fine behind the 1.1 kHz LP.
8. `xorshift32` with seed 0 is stuck at 0 (mapped to a constant here). Use one generator instance per layer and per channel for decorrelated stereo noise.

## Sources (accessed 2026-10-29)

| # | URL | Used for |
|---|---|---|
| S1 | https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/onaudiofilterread.md | callback semantics, threading, generator rule, interleaving |
| S2 | https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/onaudioconfigurationchanged | device/sample-rate change handling |
| S3 | https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Modules/Audio/Public/ScriptBindings/Audio.bindings.cs | PlayOneShot (extern helper, no managed alloc), AudioClip.Create/SetData validation |
| S4 | https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player | -batchmode / -nographics wording (no audio statement) |
| S5 | https://issuetracker.unity3d.com/issues/onaudiofilterread-does-not-affect-signal-on-audiolistener-after-enable | UUM-116871 fix versions (via search summary) |
| S6 | https://issuetracker-mig.prd.it.unity3d.com/issues/onaudiofilterread-component-as-generator-does-not-stop-when-calling-audiosource-dot-stop ; https://issuetracker.unity3d.com/issues/crash-adding-audiolistener-component-with-onaudiofilterread-crashes-the-editor ; https://issuetracker.unity.com/issues/8769/onaudiofilterread-not-working-as-expected-for-generator-scripts | UUM-117120, UUM-61733 (one AudioSource or AudioListener per filter), UUM-58426 (fixed 2023.2/2023.3) |
| S7 | https://discussions.unity.com/t/unity-5-3-seems-to-break-batchmode-nographics-on-headless-linux/610316 ; https://docs.unity3d.com/Manual/desktop-headless-mode.html | headless audio init attempts; headless mode covers graphics only |
| S8 | https://forum.unity3d.com/threads/discrepancy-between-audio-dsptime-and-state-currdsptick-in-native-plugin.461617/ | dspTime freezes in background without Run In Background |
| S9 | https://discussions.unity.com/t/using-a-script-with-onaudiofilterread-as-an-audiosource/526148 | generator on a clip-less AudioSource; listener-on-same-object behaviour (anecdotal) |
| S10 | https://www.firstpr.com.au/dsp/pink-noise/ | Kellet refined and economy coefficients, ±0.05 dB claim |
| S11 | https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf ; https://forum.juce.com/t/svf-resonant/56889 ; https://ccrma.stanford.edu/~jatin/chowdsp/chowdsp_utils/classchowdsp_1_1StateVariableFilter.html | Simper trapezoidal SVF equations (g, k, a1-a3, ic1eq/ic2eq) |
| S12 | https://ccrma.stanford.edu/~jos/pasp/Achieving_Desired_Reverberation_Times.html ; https://ccrma.stanford.edu/~jos/pasp/First_Order_Delay_Filter_Design.html ; https://ccrma.stanford.edu/~jos/pasp/Mode_Density_Requirement.html | RT60 to per-line gain, Jot damping filter, 0.15 t60 fs mode density |
| S13 | https://itu.int/dms_pubrec/itu-r/rec/bs/R-REC-BS.1770-1-200709-S!!PDF-F.pdf ; https://forum.juce.com/t/iir-filter-coefficients-for-lufs-calculation/59111 ; https://cacophony.js.org/variables/K_WEIGHTING_STAGE1_48K.html | BS.1770 K-weighting 48 kHz table, gating constants |
| S14 | https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html | WAV float: fmt size 18, cbSize, fact chunk |
| S15 | https://docs.python.org/3/library/wave.html | stdlib wave float support only from 3.15 |
| S16 | https://mcvuk.com/development-news/procedural-audio-with-unity/ ; https://github.com/pixlpa/unity-synth-experiments | community generator patterns (AudioSource + OnAudioFilterRead) |
| Local | `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml` (lines cited above); `ProjectSettings/AudioManager.asset`; `ProjectSettings/ProjectSettings.asset` | ground-truth API docs for 6000.6.0f1 and project audio settings |
