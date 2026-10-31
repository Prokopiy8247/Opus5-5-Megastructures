## Procedural audio in Unity 6000.6 and DSP design

Tags: **[VS]** VERIFIED-SOURCE (local file and line), **[VD]** VERIFIED-DOCS (official or reference web page), **[U]** UNVERIFIED (inference or common practice, prove it with a runtime probe), **[C]** computed here (numbers checked with PowerShell on 2026-10-29).
`AM.xml` means `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml`. `AMgr` means `ProjectSettings\AudioManager.asset`.

### 1. `MonoBehaviour.OnAudioFilterRead(float[] data, int channels)`
- **[VD]** (6000.6 page) Unity inserts a custom filter into the DSP chain, in the component order shown in the Inspector. It is called "every ~20ms depending on the sample rate and platform". `data` holds interleaved floats in [-1, 1]. Frames = `data.Length / channels`. The callback runs on **the audio thread**: "calling into many Unity functions from this function is not allowed". It is not supported on Web.
- **[VD]** Without a clip: "If this is the first filter in the chain and a clip isn't attached to the audio source, this filter will be played as the audio source" (6000.6, identical to 6000.3). **[U]** The issue tracker says the AudioSource's `Play` entry point is "where the logic to identify audio generators is executed". A generator regression was fixed in 2023.2.8f1 and 2023.3.0b5. So call `Play()` explicitly **after** the filter component is added. `playOnAwake` fires before the filter exists if `AudioSource` is added first.
- **Dummy-clip carrier (as the spec asks, and the most robust option):** `AudioClip.Create(string name, int lengthSamples, int channels, int frequency, bool stream)` **[VS AM.xml:1502]**. Call it non-streamed with no callback (`AudioClip.Create("AbyssCarrier", fs, 2, fs, false)`). Then set `loop = true`, `spatialBlend = 0` (**[VS AM.xml:2570]**: 0 = full 2D), `dopplerLevel = 0`, `priority = 0`, `bypassReverbZones = true`, `volume = 1`, `pitch = 1`, and call `Play()`. The filter **overwrites** `data` (`data[i] = out`) and never adds to it.
- **`PCMReaderCallback` alternative:** **[VD]** a streamed clip calls the reader "continuously", and the docs warn that the array must be filled with zeros when there is no data. The docs do not say which thread runs it **[U]**. It adds buffering latency. Do not use it.
- **New in 6.x:** **[VS AM.xml:829, 2480, 685]** the Scriptable Audio Pipeline (`IAudioGenerator`, `AudioSource.generator`, `GeneratorInstance.IRealtime.Process(context, pipe, buffer, args)`, `ControlContext.CreateManualControlContext`). **[VD]** "You can't use `AudioClip.CreateInstance` with clips you generate at runtime via `AudioClip.Create`". This API is new and thinly documented. Do not adopt it.
- **Forbidden on the audio thread [VD/U]:** any `UnityEngine.Object` access (transform, components, `AudioSource.*`), `Time.*`, `UnityEngine.Random` (main thread only), allocation (GC), locks, string formatting and LINQ. Pure math (`System.Math`, `Mathf`) is fine. The docs example calls `Debug.Log` there, but avoid it: set a flag and log from the main thread. Wrap the body in `try/catch`. On an exception, zero `data` and set a flag.
- **Buffer size and rate:** **[VS AMgr:11-12]** `m_SampleRate: 0` (0 = system rate **[VD]**) and `m_DSPBufferSize: 1024`. `Default Speaker Mode: 2` = stereo **[VS AMgr:10, VD]**. **[U]** 1024 is probably the "Best Performance" setting, about 21.3 ms at 48 kHz. Never assume it: size every buffer from `data.Length` and support any `channels`.
- **Pause, timeScale, capture:** **[VD]** the dspTime docs say that "While the game is paused or suspended, this time will not be updated". This covers Editor pause, `AudioListener.pause` and app suspension. **[VS AM.xml:189-196]** `AudioMixerUpdateMode.Normal` uses *scaled* game time (only relevant if an AudioMixer is used, so do not use one). **[U]** `Time.timeScale = 0` does not stop the device-driven callback. **[VD]** `captureDeltaTime`/`captureFramerate` make `Time.time` advance "regardless of real time", and the page says nothing about audio. So **[U]** the audio thread keeps real time and drifts from captured frames. Use offline rendering for capture (Recommendation 9).
- **Batchmode:** not documented anywhere I found **[U]**. **[VD]** "Disable Unity Audio" turns off audio in standalone builds. It is `m_DisableAudio: 0` here **[VS AMgr:17]**. **[VD]** "Virtualize Effect" turns off effects on culled sources. It is `m_VirtualizeEffects: 1` **[VS AMgr:18]**, with `m_RealVoiceCount: 32`. Keep at most 1 or 2 sources at priority 0. **[VD]** "Enable Output Suspension" (Editor only) suspends output after long silence.

### 2. AudioSettings
- **[VS AM.xml:1762-1790]** `struct AudioConfiguration { speakerMode, dspBufferSize, sampleRate, numRealVoices, numVirtualVoices }`. **[VS AM.xml:2357, 2430]** `AudioConfiguration AudioSettings.GetConfiguration()` and `bool AudioSettings.Reset(AudioConfiguration)`. Reset gives "no guarantee that the exact settings specified are used … closest match", raises `OnAudioConfigurationChanged(bool deviceWasChanged)`, and can stall the main thread during async loads.
- **[VD]** Valid values from the docs example: buffers 32, 64, 128, 256, 340, 480, 512, 1024, 2048, 4096, 8192; rates 11025, 22050, 44100, 48000, 88200, 96000. The example calls `Play()` again after Reset. **[U]** Reset stops playing sources.
- **[VS AM.xml:2365]** `GetDSPBufferSize(out int bufferLength, out int numBuffers)`. **[VS AM.xml:2336, 2341]** `double AudioSettings.dspTime` (based on samples processed **[VD]**) and `int AudioSettings.outputSampleRate`.

### 3. Thread-safe parameters and events
- C# `volatile` does not apply to `double`/`long`. Use `System.Threading.Volatile.Read/Write(ref double)` or `Interlocked`.
- The spec says "double-buffered". Implement it as a wait-free triple buffer, which has no race when the main thread publishes twice inside one callback:
```
Main:  slots[back] = p;  back = Interlocked.Exchange(ref middle, back | FRESH) & 3;
Audio: if ((Volatile.Read(ref middle) & FRESH) != 0) front = Interlocked.Exchange(ref middle, front) & 3;
       var p = slots[front];   // once per callback, then smooth per sample
```
- One-shot events (footsteps, rail ticks): use a preallocated SPSC ring of structs (head and tail written with `Volatile.Write`). Keep the audio state a pure function of (progress d, rideU, time, seed), so the offline render uses the same code.

### 4. DSP blocks (fs = 48000)
- **One-pole smoother [VD JOS / C]:** `a = exp(-1/(tau*fs))`, `y = x + a*(y - x)`. For tau = 50 ms, a = 0.99958342. Note that tau is the 63 % point and 99 % is reached at about 4.6·tau = 230 ms. Write down which meaning "50 ms" has.
- **DC blocker [VD JOS]:** `y = g*(x - x1) + R*y1`, `H = (1 - z^-1)/(1 - R z^-1)`. Normalised with `g = (1 + R)/2`, because the peak gain is 2/(1 + R). For an 8 Hz corner, `R = exp(-2π·8/fs) = 0.99895335` **[C]**. That gives -3.01 dB at 8 Hz, -0.96 dB at 16 Hz and -0.29 dB at 30 Hz, so SUB at 31 Hz is barely touched **[C]**.
- **xorshift32 [VD Wikipedia, Marsaglia 2003, JSS 8(14)]:** `x ^= x<<13; x ^= x>>17; x ^= x<<5;`. The period is 2^32-1 and the state must never be 0. White noise in [-1, 1): `(int)x * 4.656613e-10f`. Give each layer and each L/R stream its own state, hashed from (seed, id) and forced nonzero.
- **Brown noise (leaky integrator) [C]:** `y = a*y + b*w` with `a = exp(-2π f_leak/fs)` and `b = sqrt(1 - a²)` (unit variance for unit-variance input; uniform w has variance 1/3). For f_leak = 10 Hz, a = 0.99869186 and b = 0.05113287. The slope is -6 dB/oct above f_leak.
- **Pink noise (Paul Kellet, "refined", ±0.05 dB above 9.2 Hz at 44.1 kHz) [VD musicdsp, JUCE transcription]:**
```
b0=0.99886*b0+w*0.0555179; b1=0.99332*b1+w*0.0750759; b2=0.96900*b2+w*0.1538520;
b3=0.86650*b3+w*0.3104856; b4=0.55000*b4+w*0.5329522; b5=-0.7616*b5-w*0.0168980;
pink=b0+b1+b2+b3+b4+b5+b6+w*0.5362; b6=w*0.115926;
```
  The economy version (±0.5 dB) is `0.99765/0.0990460, 0.96300/0.2965164, 0.57000/1.0526913, +w*0.1848`. The coefficients are tuned for 44.1 kHz. **[U]** At 48 kHz the poles move up about 0.12 octave, which is harmless above 20 Hz. Calibrate the output gain with an RMS measurement in a test.
- **TPT state-variable filter (Simper/Cytomic) [VD, transcription of the Cytomic paper; the PDF itself could not be text-extracted]:**
```
g=tan(π fc/fs); k=1/Q; a1=1/(1+g*(g+k)); a2=g*a1; a3=g*a2;
v3=v0-ic2eq; v1=a1*ic1eq+a2*v3; v2=ic2eq+a2*ic1eq+a3*v3; ic1eq=2*v1-ic1eq; ic2eq=2*v2-ic2eq;
low=v2; band=v1 (peak gain Q); bandUnity=k*v1; high=v0-k*v1-v2; notch=v0-k*v1;
```
  Recompute g every 32 samples from the smoothed fc and clamp fc to at most 0.45·fs. Band edges map to centre `fc = sqrt(f_lo*f_hi)`: AIR 120-600 gives 268 Hz, WIND 300-1200 gives 600 Hz, FAN 200-900 gives 424 Hz. The spec's Q values are kept as given **[interpretation]**.
- **Soft limiter (pitfall) [C]:** in the spec's `0.708*tanh(1.2x)/tanh(1.2)`, tanh(1.2) = 0.833655. So the output is 0.708 (-3 dBFS) only at |x| = 1. The **asymptotic ceiling is 0.8493 = -1.42 dBFS**, and the small-signal gain is 1.019. That still meets "peak ≤ -1 dBFS". For a true -3 dBFS ceiling, use `c*tanh(x/c)` with c = 0.708. Put the DC blocker **before** the limiter so the ceiling holds.
- **FDN, 8 lines [VD JOS/Jot; VS spec delays]:** the delays 1049, 1217, 1423, 1601, 1801, 2039, 2269, 2503 are all prime, with ΣL = 13902 samples = 0.29 s **[C]**. Hadamard: `H2N = (1/√2)[[HN, HN], [HN, -HN]]`, computed as an in-place FWHT (3 butterfly stages) then ×0.35355339. Line gain `G0_i = 10^(-3 L_i/(fs·T60))`. For example, at T60 0.7 s G0 is 0.806/0.598 (shortest/longest line), and at 11 s it is 0.986/0.968 **[C]**.
  **HF damping:** JOS gives the first-order design `H_i = g_i/(1 - p_i z^-1)`, `p_i = (R0^M - Rπ^M)/(R0^M + Rπ^M)`, `g_i = 2 R0^M Rπ^M/(R0^M + Rπ^M)` **[VD]**. The spec gives a *damping frequency* f_d, so use this exact one-pole design instead. Take `T60(f_d) = T60/2` (an interpretation to record), `r = G_d/G0`, `c = cos(2π f_d/fs)`, `A = 1 - r²`, `B = 1 - r²c`, `p = A/(B + sqrt(B² - A²))`, and in-loop gain `k_i = G0_i(1 - p_i)`. Check: L = 2503, T60 = 7.5 s, f_d = 2.5 kHz gives p = 0.3732, and the gain at f_d matches the target exactly **[C]**.
```
x = predelay.Read(preSamples)            // 16384-sample ring buffer, up to 250 ms
for i: z[i] = k[i]*line[i].Read(L[i]) + p[i]*z[i]
v = FWHT8(z) * 0.35355339
for i: line[i].Write(v[i] + sgn[i]*x*inGain)
yL = Σ oL[i]*z[i]; yR = Σ oR[i]*z[i]     // two orthogonal ±1 rows, /√8
```
  **Mode density [VD JOS]:** Schroeder asks for M ≥ 0.15·T60·fs, which is 79200 at 11 s against 13902 available. The risk is metallic ringing on bells and long tails. Mitigate with 2-4 input allpass diffusers (g about 0.6) and/or ±0.5-sample delay modulation. Optional reading: Schlecht/Habets on echo and modal density **[VD]**.
  **Wet level:** tail energy grows with T60 (∝ T60). Define "wet" against a calibrated reference so the 4 s blends do not jump in loudness.
  **Pre-delay changes:** crossfade two read taps. Do not move one tap, which clicks or shifts pitch.
- **Modal resonator [VD JOS]:** `y = 2r cosθ·y1 - r²·y2 + x`, with `θ = 2πf/fs` and `r = 10^(-3/(T60·fs))`. An impulse `x0 = A sinθ` gives `y[n] = A r^n sin((n+1)θ)`. Use `double` state at 31-83 Hz. For BELL: 83/137/211/283 Hz with T60 5/4/3/2 s.
- **Click-free envelopes [U, standard practice]:** raised-cosine attack `0.5 - 0.5cos(πt/Ta)`. Exponential decay `env *= 10^(-3/(T60·fs))` (record that "decay X s" means T60). Free a voice below 1e-5. A stolen voice fades over 5 ms. Layer on/off goes through the smoothers. Use double phase accumulators. Add an anti-denormal guard (flush |s| < 1e-15) in FDN and IIR states.

### 5. Offline WAV and metrics (EditMode)
- **WAV layout [VD McGill]:** little-endian (`BinaryWriter`). `RIFF <4+chunks> WAVE`, then `fmt `: tag (1 = PCM, 3 = float), channels, rate, byteRate = rate·blockAlign, blockAlign = ch·bits/8, bits. The fmt chunk is 16 bytes for PCM. Non-PCM float needs an 18-byte fmt (cbSize = 0) **and** a `fact` chunk (frames per channel). EXTENSIBLE is needed only for more than 2 channels or more than 16-bit PCM. Pad odd data with one byte. Write a placeholder header and patch the sizes afterwards. For 16-bit: `(short)clamp(round(x*32767))`.
- **Metrics [C/U]:**
  - RMS in 0.4 s windows (19200 frames, as BS.1770 momentary): `20log10(sqrt(mean x²))`. Under this convention a full-scale sine reads -3.01 dBFS (AES17 reads +3 dB higher), so write the convention down.
  - Sample peak: `20log10(max|x|)`.
  - True peak: 4× polyphase windowed-sinc interpolator (for example 4 phases × 16-24 taps, Kaiser β ≈ 7). BS.1770 Annex 2 uses a 48-tap, 4× filter with about ±0.1 dB ripple **[VD secondary]**.
  - Also measure max |x[n] - x[n-1]| ≤ 0.1 and |mean| < 1e-4.
  - The live callback cannot be sample-identical to the offline render (parameters arrive at different times). Compare RMS/peak statistics instead.

### 6. `AudioReverbFilter` (fallback)
- **[VS AM.xml:1986-2065, VD manual]** `[RequireComponent(typeof(AudioBehaviour))]`. Properties: `decayTime` 0.1-20 s, `decayHFRatio` 0.1-2.0, `hfReference` 1000-20000 Hz, `lfReference` 20-1000 Hz, `dryLevel`/`room`/`roomHF`/`roomLF` -10000..0 mB, `reflectionsLevel` -10000..1000 mB, `reflectionsDelay` **0-0.3 s** (the scripting-reference text at AM.xml:2026 wrongly copies the "late reverberation level" description), `reverbLevel` -10000..2000 mB, `reverbDelay` 0-0.1 s, `diffusion`/`density` 0-100 %, `reverbPreset`.
- **[VD]** Custom values need `reverbPreset = AudioReverbPreset.User`. Mapping: decayTime = RT60, reflectionsDelay = pre-delay, hfReference = f_d, wet -14 dB = -1400 mB. Ramp the values from the main thread at no more than 6 dB/s.

### Sources (all accessed 2026-10-29)
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/onaudiofilterread: threading, generator rule, data layout.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset: Reset semantics, valid buffer sizes and rates.
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/dsptime: dspTime and pause behaviour.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audioclip/create: Create overloads, stream semantics, Scriptable Audio Pipeline note.
- https://docs.unity.com/en-us/engine/current/script-reference/unityengine/time/captureframerate and .../time/capturedeltatime: capture timing.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager.md: sample rate 0, Disable Unity Audio, Virtualize Effect, Output Suspension.
- https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/filters/class-audio-reverb-filter.md and https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audioreverbfilter.md: reverb filter ranges, User preset, RequireComponent.
- https://docs.unity.com/en-us/engine/6000.6/manual/audio/scriptable-processors/generators: IAudioGenerator overview (not adopted).
- https://issuetracker.unity3d.com/issues/onaudiofilterread-not-working-as-expected-for-generator-scripts and https://issuetracker.unity3d.com/issues/empty-data-buffer-gets-passed-to-onaudiofilterread-when-using-video-player: generator detection in Play() (search snippet only; the pages are dynamic).
- https://discussions.unity.com/t/onaudiofilterread-not-playing/503615: clipless-source anecdotes.
- https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf (primary) and https://forum.juce.com/t/svf-resonant/56889/17 (text transcription): SVF.
- https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html and .../DC_Blocker_Frequency_Response.html: DC blocker and gain normalisation.
- https://ccrma.stanford.edu/~jos/filters/Two_Pole.html: resonator.
- https://ccrma.stanford.edu/~jos/pasp/ pages First_Order_Delay_Filter_Design, Delay_Line_Damping_Filter_Design, Achieving_Desired_Reverberation_Times, Hadamard_Matrix, Choice_Delay_Lengths, Mode_Density_Requirement: FDN design.
- https://www.musicdsp.org/en/latest/Filters/76-pink-noise-filter.html, its pink.txt, and https://forum.juce.com/t/pink-noise-generator/40013/2: Kellet coefficients.
- https://en.wikipedia.org/wiki/Xorshift: xorshift32 (Marsaglia, doi:10.18637/jss.v008.i14).
- https://mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html: WAV format.
- https://cacophony.js.org/classes/TruePeakDetector.html and https://github.com/audionuma/r128x/issues/10: BS.1770 true-peak structure.
- https://www.audiolabs-erlangen.com/resources/2018-IEEE-Modal, https://audiolabs-erlangen.de/resources/2019-WASPAA-DFM-FDN, https://ar5iv.arxiv.org/html/1912.08888: Schlecht FDN density research.

### Recommendations
1. Put the DSP in a Unity-free `AbyssSynth` class with `Render(Span<float> interleaved, int frames, int ch)`. Use a thin `ProceduralAudioEngine : MonoBehaviour` whose `OnAudioFilterRead` only calls it.
2. Set System Sample Rate to 48000 in AudioManager instead of calling Reset at runtime. Still cache `outputSampleRate` on the main thread and derive every coefficient from it. Call Reset only as a fallback, before the source plays.
3. Use a looping stereo carrier clip, add the filter component, then call `Play()`. Re-`Play()` and re-initialise state in `OnAudioConfigurationChanged`.
4. Use a triple buffer for parameters and an SPSC ring for events. Do not allocate, lock or call Unity APIs on the audio thread. Wrap the callback in try/catch.
5. Duck for the pause overlay inside the engine (smoothed gain). Never use `AudioListener.pause`, which freezes dspTime and can click. Set `Application.runInBackground = true`.
6. Signal order: mix → master trim → fade → DC blocker (R = 0.99895335, normalised) → limiter. Decide between -1.42 dBFS (spec formula) and a true -3 dBFS ceiling (`c*tanh(x/c)`), and record the choice.
7. FDN: use the exact per-line one-pole damping design above, energy-normalised wet, crossfaded pre-delay taps, optional input diffusers to counter the low mode density at 7.5-11 s, and an anti-denormal guard.
8. Built-player proof: a listener-side `OnAudioFilterRead` tap copies the final mix into a preallocated 10 s ring buffer, and the main thread writes a WAV. Compare its statistics (not samples) with the offline render. Log the callback count, `GetDSPBufferSize` and `outputSampleRate`.
9. For `captureFramerate` capture and `-render-audio`, render exactly fs/fps samples per frame on the main thread (800 at 60 fps, 1600 at 30) through the same synth, so picture and sound stay frame-locked.
10. EditMode tests: per-beat WAVs, 0.4 s RMS against target ±3 dB, sample peak and 4× true peak ≤ -1 dBFS, step ≤ 0.1, |DC| < 1e-4, presence of the silences.
