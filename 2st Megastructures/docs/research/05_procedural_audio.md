# 05 Procedural audio (Unity 6000.6.0f1, OnAudioFilterRead)

Scope: wind, drones, bells, footsteps and master bus, all synthesised in C# (no clips). Written for `AudioDirector` (spec 14.3) and the audio QA of spec 11.9.
Tags: **VERIFIED-SOURCE** (Unity's own files for this exact version, path:line), **VERIFIED-DOCS** (URL), **DERIVED** (algebra or arithmetic shown here; check it with a unit test), **UNVERIFIED** (from memory or an inference; prove it with the probe or test given).

Paths used below:
- `XML` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml` (API doc comments that ship with 6000.6.0f1)
- `DOC` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Documentation\en\` (offline docs for this version, the footer says "Unity 6.6 (6000.6)", built 2026-08-25)
- `AM` = `ProjectSettings\AudioManager.asset`, `PS` = `ProjectSettings\ProjectSettings.asset`

---

## 0. Decisions in short

1. **One generator chain plus a master bus.** GameObject `AudioDirector`, components in this order: `AudioSource` (no clip, 2D) -> `AudioDirector` (OnAudioFilterRead generator, must be the first filter) -> `AudioReverbFilter` (User preset). Main Camera: `AudioListener` -> `MasterBus` (OnAudioFilterRead: look-ahead limiter, DSP pause duck, optional capture to WAV). The limiter has to sit **after** the reverb, because the reverb adds level. Component order is processing order (section 1, F5).
2. **The DSP lives in plain C# classes** (`AudioEngine`, `Svf`, `Pink`, `LookaheadLimiter` and so on), with no `UnityEngine` references. `OnAudioFilterRead` only forwards `(float[] data, int channels)` to `engine.Render`. EditMode tests call the same `Render` in 1024-frame blocks and write WAV files, so no audio device is needed.
3. **Sample rate:** set `m_SampleRate: 48000` in `AM` (line 11, currently 0 = device rate). At runtime always read `AudioSettings.outputSampleRate` and compute every coefficient from it, because 48 kHz is a request, not a guarantee.
4. **Hand-off:** main thread to audio thread through one seqlock-protected struct (progress, speed, pause, surface, step counter). The audio thread smooths everything per sample. Footsteps go through an SPSC ring buffer. Nothing allocates, locks or logs on the audio thread.
5. **Offline render vs player:** the offline render has no `AudioReverbFilter` (it is native FMOD). So (a) per-beat RMS trims must be checked on the player dump, and (b) peak safety relies on the post-reverb `MasterBus` limiter. Fallback rung if the reverb causes trouble: a C# FDN reverb inside `AudioEngine` (section 6.12).

---

## 1. OnAudioFilterRead facts

| # | Fact | Tag |
|---|---|---|
| F1 | Signature: `void OnAudioFilterRead(float[] data, int channels)` on a MonoBehaviour. `data` is interleaved, `data.Length / channels` = frames per call. The nominal range is [-1, 1]. | VERIFIED-SOURCE `DOC\ScriptReference\MonoBehaviour.OnAudioFilterRead.html` |
| F2 | It runs **on the audio thread**, not the main thread. "Calling into many Unity functions from this function is not allowed (if you try, a warning shows up at run time)." The doc example reads `AudioSettings.dspTime` inside the callback. The example also calls `Debug.Log` there, which you should not copy. | VERIFIED-SOURCE same file |
| F3 | Call rate: "every ~20ms depending on the sample rate and platform". 1024 frames at 48 kHz is 21.3 ms. Never hard-code the length; read `data.Length` on every call. | VERIFIED-SOURCE same file |
| F4 | Generator mode: "If this is the first filter in the chain and a clip isn't attached to the audio source, this filter will be played as the audio source." | VERIFIED-SOURCE same file |
| F5 | Ordering: "The filter is inserted in the same order as the MonoBehaviour script is shown in the Inspector." The manual adds: "The ordering of the components is important, since it reflects the order in which the effects will be applied", and effects work on an Audio Source **and on an Audio Listener**. Built-in filters (AudioReverbFilter) and script filters interleave by component order. `AddComponent` appends at the end. | VERIFIED-SOURCE `DOC\Manual\class-AudioEffect.html` ("Use audio filters"), `DOC\Manual\class-AudioListener.html` |
| F6 | The Inspector shows a VU meter and the callback's ms cost, red when the mixer is starved. | VERIFIED-SOURCE OnAudioFilterRead page |
| F7 | It is not supported on the Web platform. Windows Standalone is supported (no restriction listed). | VERIFIED-SOURCE same page |
| F8 | The source must be **played**. Unity decides "generator vs filter" inside `AudioSource.Play` / `playOnAwake`. Add all components first, then call `src.Play()` once. | VERIFIED-DOCS (Unity Issue Tracker text quoted in search results: "This entry point in the AudioSource is where the logic to identify audio generators is executed.") |
| F9 | UUM-117120 (still open, "Under Consideration for 6000.6.X"): a generator does **not** stop on `AudioSource.Stop()`. Never use Stop/enable toggles for silence. Use DSP gain ramps. | VERIFIED-DOCS issue tracker (via search result summary) |
| F10 | UUM-116871 "OnAudioFilterRead does not affect signal on AudioListener after enable" is fixed in 6000.6.0 (also 6000.0.77f1, 6000.3.17f1, 6000.4.10f1, 6000.5.0b11, 6000.6.0a5). 6000.6.0f1 is fixed, but still keep `MasterBus` enabled from the start and never toggle it. | VERIFIED-DOCS issue tracker (via search result summary) |
| F11 | UUM-61733: a component that implements OnAudioFilterRead "may only be used by either one AudioSource or AudioListener at a time". **Never put an AudioSource and the AudioListener on the same GameObject as an OAFR script.** | VERIFIED-DOCS issue tracker (via search result summary) |
| F12 | UUM-122520 (Won't Fix): OAFR "is attached to the scripting thread" and is "susceptible to GC interrupts". A forced `GC.Collect` reliably causes stutter. Many small allocations hurt more than one large one (issue 870041). Unity points to the replacement scriptable audio pipeline (from 6.3). Allocate nothing per frame during the walk (main thread too), and keep `gcIncremental: 1` (`PS` line 853, already 1). | VERIFIED-DOCS issue tracker (via search result summary); VERIFIED-SOURCE `PS:853` |
| F13 | Adding an OAFR component with AddComponent at runtime works (bug 711884 fixed in 5.3.0). | VERIFIED-DOCS issue tracker (search summary) |
| F14 | Assigning a new array to `data` does nothing. Write into the existing array. | VERIFIED-DOCS Unity Discussions "OnAudioFilterRead Not Playing?" |
| F15 | Data handed to the first filter of a clip-less source is silence. Overwrite it (`data[i] = v`). `MasterBus` on the listener must read-modify-write. | UNVERIFIED (common practice; harmless either way) |
| F16 | When the window is unfocused and `runInBackground` is 0, the player pauses and `AudioSettings.dspTime` freezes. Currently `PS:90 runInBackground: 0`. The spec wants "run in background", so set it to 1. | VERIFIED-SOURCE `PS:90`; dspTime freezing on pause: VERIFIED-SOURCE `DOC\ScriptReference\AudioSettings-dspTime.html` ("While the game is paused or suspended, this time will not be updated") |
| F17 | `Disable Unity Audio` must stay off: `AM:17 m_DisableAudio: 0` (OK). If it is on, standalone has no audio system at all. | VERIFIED-SOURCE `AM:17`, `DOC\Manual\class-AudioManager.html` |
| F18 | "Enable Output Suspension" (Editor only) suspends output after a long stretch of silence. This only affects Editor listening tests, not the player. | VERIFIED-SOURCE `DOC\Manual\class-AudioManager.html` |
| F19 | 6000.6 also ships the new **scriptable audio pipeline**: `IAudioGenerator` (`isFinite`, `isRealtime`, `length`, `CreateInstance(ControlContext, AudioFormat?, ProcessorInstance.CreationParameters)`), `GeneratorInstance`, `ControlContext`, `RealtimeContext`, Burst-compatible HPC#, assigned in the AudioSource "Generator" field. This is a possible fallback rung if GC dropouts appear. Stay on OAFR as the spec requires. | VERIFIED-SOURCE `XML:298,516,851,1211`; `DOC\Manual\audio-scriptable-processors-generators.html` |

### Batchmode / -nographics / Windows player
- **UNVERIFIED** whether FMOD output (and therefore OAFR) runs under `-batchmode` or `-nographics`. No Unity page says so, and the command-line docs (`DOC\Manual\PlayerCommandLineArguments.html`, `EditorCommandLineArguments.html`) list no audio flag (VERIFIED-SOURCE: grep for "audio|sound" finds nothing). Inference: if there is no output device, the mixer never ticks and `dspTime` stays 0. **Do not depend on it.** EditMode tests drive `AudioEngine.Render` directly.
- **Windows player:** OAFR runs (F7). Prove it once per build with the probe below. Run the built player **windowed, without -batchmode** (`-autotest -audiodump`).
- Probe (main thread, 2 s after start): `callbacks > 0`, `AudioSettings.dspTime > 1.5`, then log `AudioSettings.outputSampleRate`, `AudioSettings.GetConfiguration()` (sampleRate, dspBufferSize, speakerMode) and the OAFR `channels` / `data.Length` seen by the audio thread.
- Alternative capture path: `AudioRenderer.Start()` / `GetSampleCountForCaptureFrame()` / `Render(NativeArray<float>)` / `Stop()`. Start "outputs silence until Stop", and the sample count "is dependent on the frame capture rate". Signatures are VERIFIED-SOURCE `XML:1948-1985`. Whether it works headless is UNVERIFIED. The in-chain capture in `MasterBus` is simpler and is the recommended path.

---

## 2. Sample rate, DSP buffer, configuration

| Fact | Tag |
|---|---|
| `AudioSettings.outputSampleRate` (static int, read-only): "As of version 5.0 setting the sample rate from scripts is no longer supported. This has to be set in the Audio section of the project settings instead." | VERIFIED-SOURCE `DOC\ScriptReference\AudioSettings-outputSampleRate.html`, `XML:2341` |
| Project setting "System Sample Rate": "If set to 0, Unity uses the sample rate of the system. Note: This only serves as a reference only, since certain platforms allow you to change the sample rate, such as iOS or Android." Serialized as `m_SampleRate` (`AM:11`, currently **0**). | VERIFIED-SOURCE `DOC\Manual\class-AudioManager.html`, `AM:11` |
| `AudioConfiguration` fields: `int sampleRate`, `int dspBufferSize`, `AudioSpeakerMode speakerMode`, `int numRealVoices`, `int numVirtualVoices`. | VERIFIED-SOURCE `XML:1762-1791` |
| `AudioSettings.GetConfiguration()` returns the struct. `static bool Reset(AudioConfiguration config)` "invokes the AudioSettings.OnAudioConfigurationChanged delegate with the argument deviceWasChanged=false. There's no guarantee that the exact settings specified are used, but Unity automatically uses the closest match". It "can cause main thread stalls" during async loading. It returns true if all settings were applied. | VERIFIED-SOURCE `XML:2357-2438` |
| `AudioSettings.OnAudioConfigurationChanged` (event `AudioConfigurationChangeHandler(bool deviceWasChanged)`) also fires when the OS default device or its rate changes (HDMI monitor, USB headset). | VERIFIED-SOURCE `XML:2351-2356, 2424-2429` |
| The Reset example restarts playback (`GetComponent<AudioSource>().Play()`) inside the change handler. **Assume a Reset or device change stops sources and rebuilds the DSP graph.** Re-Play the source and re-init the engine at the new `outputSampleRate` in the handler. | VERIFIED-SOURCE example `DOC\ScriptReference\AudioSettings.Reset.html`; "stops sources" = UNVERIFIED inference |
| Valid values listed in the Reset example: sample rates 11025, 22050, 44100, 48000, 88200, 96000; DSP sizes 32, 64, 128, 256, 340, 480, 512, 1024, 2048, 4096, 8192. | VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset |
| `AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers)`: ring buffer of `numBuffers` blocks of `bufferLength`. "Anything above 20 ms will be noticeable and sound parameter changes will be obvious instead of smooth." Prefer `GetConfiguration().dspBufferSize`. | VERIFIED-SOURCE `DOC\ScriptReference\AudioSettings.GetDSPBufferSize.html`, `DOC\ScriptReference\AudioConfiguration-dspBufferSize.html` |
| Project "DSP Buffer Size" options: Default / Best Latency / Good Latency / Best Performance. The manual gives no sample counts. `AM:12 m_DSPBufferSize: 1024`, `AM:19 m_RequestedDSPBufferSize: 0`. The mapping to the UI enum is UNVERIFIED. Leave it as is (1024 frames gives about 21 ms of slack against GC hiccups) and log the real value. | VERIFIED-SOURCE `AM:12,19`, manual; mapping UNVERIFIED |
| Speaker mode: `AM:10 Default Speaker Mode: 2` (stereo), so `channels` will normally be 2. Code must still handle 1 and more than 2 (write L/R, zero the rest). | VERIFIED-SOURCE `AM:10`; handling advice UNVERIFIED-safe |
| Max real voices 32 (`AM:14`). One source is enough. | VERIFIED-SOURCE |

**Forcing 48 kHz (recommended order):**
1. Edit `AM:11` to `m_SampleRate: 48000` (owner of ProjectSettings does it via the project-settings batch tool or a text edit while the Editor is closed).
2. Runtime guard in `AudioDirector.Awake`, before `Play()`:
```csharp
var cfg = AudioSettings.GetConfiguration();
if (cfg.sampleRate != 48000) { cfg.sampleRate = 48000; AudioSettings.Reset(cfg); }   // may be refused by the device; fine
fs = AudioSettings.outputSampleRate;   // ALWAYS use this value, log it
```
3. Never assume 48000 in DSP code. Every coefficient is computed from `fs`. The offline render uses 48000.

---

## 3. Scene setup skeleton

```csharp
using UnityEngine;
using System.Threading;

[RequireComponent(typeof(AudioSource))]               // AudioSource gets added ABOVE this component
public sealed class AudioDirector : MonoBehaviour
{
    AudioEngine engine;                // Unity-free DSP (section 6)
    AudioReverbFilter reverb;
    public readonly ParamChannel Params = new ParamChannel();     // section 7
    public readonly SpscQueue<StepEvent> Steps = new SpscQueue<StepEvent>(64);
    int callbacks;                     // probe; read on the main thread with Volatile.Read
    int fs;

    void Awake()
    {
        var cfg = AudioSettings.GetConfiguration();
        if (cfg.sampleRate != 48000) { cfg.sampleRate = 48000; AudioSettings.Reset(cfg); }
        fs = AudioSettings.outputSampleRate;
        engine = new AudioEngine(fs, seed: 1234u, Params, Steps);

        var src = GetComponent<AudioSource>();
        src.clip = null; src.playOnAwake = false; src.loop = true;
        src.spatialBlend = 0f; src.volume = 1f; src.panStereo = 0f;
        src.dopplerLevel = 0f; src.bypassReverbZones = true; src.priority = 0;

        reverb = gameObject.AddComponent<AudioReverbFilter>();  // appended AFTER AudioDirector -> processes its output
        reverb.reverbPreset = AudioReverbPreset.User;           // set User FIRST, then the fields
        ReverbBlend.Apply(reverb, ReverbBlend.Capsule);

        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigChanged;
        src.Play();                                             // generator detection happens here (F8)
    }
    void OnDestroy() { AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigChanged; }

    void OnAudioConfigChanged(bool deviceWasChanged)
    {
        fs = AudioSettings.outputSampleRate;
        engine.SetSampleRate(fs);                               // recompute coefficients, keep musical state
        GetComponent<AudioSource>().Play();
    }

    void OnAudioFilterRead(float[] data, int channels)          // AUDIO THREAD
    {
        Interlocked.Increment(ref callbacks);
        engine.Render(data, channels);                          // no Unity API, no alloc, no lock, no log
    }
    public int CallbackCount => Volatile.Read(ref callbacks);
}
```
`MasterBus` goes on the Main Camera, below `AudioListener` (code in 6.9). Add an EditMode/PlayMode assertion on `GetComponents<Component>()` order (AudioSource < AudioDirector < AudioReverbFilter; AudioListener < MasterBus), because the order is the signal flow (F5). Whether a RequireComponent-added AudioSource always lands above the script when it is added at runtime is UNVERIFIED, hence the assertion. The 2D stereo source's pan law at `panStereo = 0` is assumed to be unity gain (UNVERIFIED). The player-dump vs offline-render level comparison measures it, and any offset can go into the trims.

---

## 4. AudioReverbFilter (FMOD SFX/I3DL2 reverb)

All are `public float` properties except `reverbPreset` (`public AudioReverbPreset`). Ranges and defaults come from the Manual for 6000.6. VERIFIED-SOURCE `DOC\Manual\class-AudioReverbFilter.html`, `XML:1986-2065` (property names), `XML:2206` (`AudioReverbPreset.User`).

| Property | Unit | Range | Default | Meaning |
|---|---|---|---|---|
| `reverbPreset` | enum | Off, Generic, PaddedCell, Room, Bathroom, Livingroom, Stoneroom, Auditorium, Concerthall, Cave, Arena, Hangar, CarpetedHallway, Hallway, StoneCorridor, Alley, Forest, City, Mountains, Quarry, Plain, ParkingLot, SewerPipe, Underwater, Drugged, Dizzy, Psychotic, **User** | - | "These values can only be modified if your Reverb Preset is set to User" |
| `dryLevel` | mB | -10000 .. 0 | 0 | dry mix |
| `room` | mB | -10000 .. 0 | 0 | room effect level at low frequencies (overall wet) |
| `roomHF` | mB | -10000 .. 0 | 0 | room HF level |
| `roomLF` | mB | -10000 .. 0 | 0 | room LF level |
| `decayTime` | s | 0.1 .. 20 | 1.0 | decay time at LF |
| `decayHFRatio` | ratio | 0.1 .. 2.0 | 0.5 | HF/LF decay ratio |
| `reflectionsLevel` | mB | -10000 .. 1000 | **-10000** | early reflections, **off by default** |
| `reflectionsDelay` | s | **0 .. 0.3** | 0.0 | early reflection delay |
| `reverbLevel` | mB | -10000 .. 2000 | 0 | late reverb relative to room |
| `reverbDelay` | s | 0 .. 0.1 | 0.04 | late delay relative to first reflection |
| `hfReference` | Hz | 1000 .. 20000 | 5000 | |
| `lfReference` | Hz | 20 .. 1000 | 250 | |
| `diffusion` | % | 0 .. 100 | 100 | echo density |
| `density` | % | 0 .. 100 | 100 | modal density |

Pitfalls:
- The **XML / ScriptReference text for `reflectionsDelay` is a copy-paste error** ("Late reverberation level ... -10000.0 to 2000.0", `XML:2026-2030`, `DOC\ScriptReference\AudioReverbFilter-reflectionsDelay.html`). The Manual's 0 .. 0.3 s is the real range. VERIFIED-SOURCE (both texts).
- Spec "wall 2.4 s, reflection delay 0.08 s" needs `reflectionsLevel` raised (for example -1500 .. -500 mB). At the default -10000 the delay is inaudible. DERIVED from the default above.
- Set `reverbPreset = AudioReverbPreset.User` once, before writing fields. Whether writing a field auto-switches the preset is UNVERIFIED, so do it explicitly.
- **Are parameter changes click-free? UNVERIFIED.** No Unity or FMOD statement was found. Values are applied from the main thread, at best once per frame, so the DSP sees per-buffer steps (about 21 ms). Level parameters (mB) and `decayTime` moved in small per-frame steps are normally smooth. Delay parameters (`reflectionsDelay`, `reverbDelay`) change delay-line lengths and are the most likely to glitch. Policy:
  1. Blend every region change over at least 2 s with smootherstep, writing all 14 fields every frame from `Update` (only when changed by more than 1e-4).
  2. Interpolate mB fields linearly (they are already logarithmic), and `decayTime`, `hfReference`, `lfReference` in the log domain.
  3. Keep `diffusion`, `density`, `reverbDelay` constant across the game.
  4. Change `reflectionsDelay` only while `reflectionsLevel` is at or below -6000 mB: fade ER down 1 s, move the delay, fade up 1 s.
  5. Prove it on the player dump with the click detector of section 9, run over each blend window.

Blend helper:
```csharp
[System.Serializable] public struct ReverbSettings {
    public float dryLevel, room, roomHF, roomLF, decayTime, decayHFRatio, reflectionsLevel,
                 reflectionsDelay, reverbLevel, reverbDelay, hfReference, lfReference, diffusion, density; }

public static class ReverbBlend {
    static float LogLerp(float a, float b, float t) => Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
    public static float Smootherstep(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6f - 15f) + 10f); }
    public static ReverbSettings Lerp(in ReverbSettings a, in ReverbSettings b, float t) {
        float u = Smootherstep(t); ReverbSettings r;
        r.dryLevel = Mathf.Lerp(a.dryLevel, b.dryLevel, u);       r.room = Mathf.Lerp(a.room, b.room, u);
        r.roomHF = Mathf.Lerp(a.roomHF, b.roomHF, u);             r.roomLF = Mathf.Lerp(a.roomLF, b.roomLF, u);
        r.decayTime = LogLerp(a.decayTime, b.decayTime, u);       r.decayHFRatio = Mathf.Lerp(a.decayHFRatio, b.decayHFRatio, u);
        r.reflectionsLevel = Mathf.Lerp(a.reflectionsLevel, b.reflectionsLevel, u);
        r.reflectionsDelay = Mathf.Lerp(a.reflectionsDelay, b.reflectionsDelay, u);
        r.reverbLevel = Mathf.Lerp(a.reverbLevel, b.reverbLevel, u); r.reverbDelay = Mathf.Lerp(a.reverbDelay, b.reverbDelay, u);
        r.hfReference = LogLerp(a.hfReference, b.hfReference, u); r.lfReference = LogLerp(a.lfReference, b.lfReference, u);
        r.diffusion = Mathf.Lerp(a.diffusion, b.diffusion, u);    r.density = Mathf.Lerp(a.density, b.density, u);
        return r; }
    public static void Apply(AudioReverbFilter f, in ReverbSettings p) {
        f.dryLevel = Mathf.Clamp(p.dryLevel, -10000f, 0f);   f.room = Mathf.Clamp(p.room, -10000f, 0f);
        f.roomHF = Mathf.Clamp(p.roomHF, -10000f, 0f);       f.roomLF = Mathf.Clamp(p.roomLF, -10000f, 0f);
        f.decayTime = Mathf.Clamp(p.decayTime, 0.1f, 20f);   f.decayHFRatio = Mathf.Clamp(p.decayHFRatio, 0.1f, 2f);
        f.reflectionsLevel = Mathf.Clamp(p.reflectionsLevel, -10000f, 1000f);
        f.reflectionsDelay = Mathf.Clamp(p.reflectionsDelay, 0f, 0.3f);
        f.reverbLevel = Mathf.Clamp(p.reverbLevel, -10000f, 2000f); f.reverbDelay = Mathf.Clamp(p.reverbDelay, 0f, 0.1f);
        f.hfReference = Mathf.Clamp(p.hfReference, 1000f, 20000f); f.lfReference = Mathf.Clamp(p.lfReference, 20f, 1000f);
        f.diffusion = Mathf.Clamp(p.diffusion, 0f, 100f);    f.density = Mathf.Clamp(p.density, 0f, 100f); }
    // Spec 14.3 rows; the other fields are starting values (UNVERIFIED, tune by ear and by the player dump):
    public static readonly ReverbSettings Capsule = new ReverbSettings { dryLevel = 0, room = -1200, roomHF = -400, roomLF = 0,
        decayTime = 0.6f, decayHFRatio = 0.5f, reflectionsLevel = -1200, reflectionsDelay = 0.005f, reverbLevel = 0,
        reverbDelay = 0.04f, hfReference = 5000, lfReference = 250, diffusion = 100, density = 100 };
    // valley 1.2 s / -2500; ark 4.0 s / -2000, decayHFRatio 0.4; dusk 3.0 s; underpass 1.1 s;
    // wall 2.4 s, reflectionsDelay 0.08 (+ reflectionsLevel ~ -1000); tunnel 3.6 s; platform 0.8 s.
}
```

---

## 5. Units and conventions (use these consistently)

- dBFS is `20*log10(|x|)` with full scale = 1.0. **RMS dBFS = `10*log10(mean(x^2))` with no +3.01 dB AES17 offset**, so a full-scale sine reads -3.01 dB RMS. Mean is over both channels. DERIVED; state this in `QA/audio-trims.json`.
- mB = 100 * dB (millibels), so -2500 mB = -25 dB. VERIFIED-SOURCE unit names in `XML:2011-2065`.
- A "decay N s" in the spec means **T60** (time to -60 dB). Per-sample multiplier `m = 10^(-3/(T60*fs)) = exp(-ln(1000)/(T60*fs))`. Example: T60 5 s at 48 kHz gives m = 0.99997122. DERIVED.
- One-pole smoother with time constant tau: `a = 1 - exp(-1/(tau*fs))`, `y += a*(target - y)`. It reaches 63 % in tau and 95 % in 3*tau. tau 20 ms at 48 kHz gives a = 1.0411e-3. DERIVED.
- Phases are `double` in cycles: `ph += f/fs; if (ph >= 1) ph -= 1; s = Math.Sin(2*Math.PI*ph)`. Filter states and envelopes are `double`. Output is cast to `float` at the end.

---

## 6. DSP building blocks (formulas + C#)

### 6.1 White noise (deterministic, allocation-free)
```csharp
public struct Rng {                                  // xorshift32; seed must be != 0; L/R use different seeds
    uint s; public Rng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; }
    public double NextBipolar() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (int)s * (1.0 / 2147483648.0); } // [-1,1)
    public double Next01() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s * (1.0 / 4294967296.0); }
}
```
UNVERIFIED-safe (standard xorshift32, Marsaglia 2003). Uniform white in [-1,1) has RMS 1/sqrt(3) = 0.577.

### 6.2 Paul Kellet refined pink noise ("pk3")
VERIFIED-DOCS https://www.firstpr.com.au/dsp/pink-noise/ : "accurate to within +/-0.05dB above 9.2Hz (44100Hz sampling rate)", "Unity gain is at Nyquist".
```csharp
public struct Pink {
    double b0, b1, b2, b3, b4, b5, b6;
    public double Next(double white) {
        b0 = 0.99886 * b0 + white * 0.0555179;
        b1 = 0.99332 * b1 + white * 0.0750759;
        b2 = 0.96900 * b2 + white * 0.1538520;
        b3 = 0.86650 * b3 + white * 0.3104856;
        b4 = 0.55000 * b4 + white * 0.5329522;
        b5 = -0.7616 * b5 - white * 0.0168980;
        double pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362;
        b6 = white * 0.115926;
        return pink * 0.11;          // "(roughly) compensate for gain" (noisehack); calibrate exactly by an RMS unit test
    }
}
```
- The economy version (+/-0.5 dB): `b0=0.99765*b0+w*0.0990460; b1=0.96300*b1+w*0.2965164; b2=0.57000*b2+w*1.0526913; out=b0+b1+b2+w*0.1848`. VERIFIED-DOCS same page.
- At 48 kHz the poles move about 9 % in frequency. The slope error stays small enough for ambience (UNVERIFIED, under 0.3 dB by estimate). Since every layer is trimmed to an RMS target anyway, normalise with a measured constant (`PinkRms` from a 60 s unit test).

### 6.3 Brown noise (leaky integrator, spec "0.02")
VERIFIED-DOCS https://noisehack.com/generate-noise-web-audio-api/ : `out = (last + 0.02*white)/1.02; last = out; out *= 3.5;`
```csharp
public struct Brown { double last; public double Next(double white) { last = (last + 0.02 * white) / 1.02; return last * 3.5; } }
```
DERIVED: the pole is 1/1.02 = 0.980392, so the corner is about (1 - 0.980392)*48000/(2*pi) = **150 Hz** at 48 kHz. The noise is brown (-6 dB/oct) above about 150 Hz and flat below. Fine for the LP 120-220 Hz rumbles. For a deeper rumble use a smaller leak (for example `(last + 0.005*w)/1.005`).

### 6.4 TPT / zero-delay-feedback state-variable filter (Cytomic / Simper, trapezoidal)
Source: Andrew Simper, "Linear Trapezoidal Integrated SVF" (SvfLinearTrapOptimised2.pdf, Cytomic). The extracted text of the PDF contains the nodal equations with `gc = 1`: `v1 = (ic1eq + g*v0 - g*v2)/(1 + g*k)`, `v2 = ic2eq + g*v1`, the capacitor-state update `iceq(n+1) = 2*vc(n+1) - iceq(n)`, and `k = 1/Q`. VERIFIED-DOCS https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf. Substituting v2 into v1 gives the optimised form (DERIVED, matches the PDF's `a1 = 1/(1 + g*(g + k))` pattern):
```
g  = tan(pi * fc / fs)          // prewarp; clamp fc <= 0.45*fs
k  = 1 / Q
a1 = 1 / (1 + g*(g + k));  a2 = g*a1;  a3 = g*a2
per sample (v0 = input):
  v3 = v0 - ic2eq
  v1 = a1*ic1eq + a2*v3          // band
  v2 = ic2eq + a2*ic1eq + a3*v3  // low
  ic1eq = 2*v1 - ic1eq
  ic2eq = 2*v2 - ic2eq
  low = v2;  band = v1;  high = v0 - k*v1 - v2
  notch = v0 - k*v1;  peak = v0 - k*v1 - 2*v2;  allpass = v0 - 2*k*v1
  bandNorm (0 dB at fc) = k*v1   // raw band peaks at Q (= 1/k)
```
Equivalent **Zavalishin TPT SVF** ("The Art of VA Filter Design", 2-pole chapter), with `R = 1/(2Q)` (so `k = 2R`) and states s1, s2 (= ic1eq, ic2eq):
```
yHP = (x - (2R + g)*s1 - s2) / (1 + 2R*g + g*g)
v = g*yHP;  yBP = v + s1;  s1 = yBP + v
v = g*yBP;  yLP = v + s2;  s2 = yLP + v
```
DERIVED: from the TPT integrator `y = g*x + s; s = y + g*x` and `yHP = x - 2R*yBP - yLP`, solving gives the yHP line above. It is algebraically identical to the Cytomic form. Formula from memory, book PDF not reachable (404); check it with a unit test: |H(fc)| of bandNorm = 1 +/- 1e-6, LP DC gain 1, and HP + k*BP + LP = x.
```csharp
public struct Svf {
    double ic1, ic2, a1, a2, a3, k;
    public void Set(double fc, double q, double fs) {
        double g = System.Math.Tan(System.Math.PI * System.Math.Min(fc, 0.45 * fs) / fs);
        k = 1.0 / q; a1 = 1.0 / (1.0 + g * (g + k)); a2 = g * a1; a3 = g * a2; }
    public double Low(double v0)  { Tick(v0, out var lp, out _, out _); return lp; }
    public double BandNorm(double v0) { Tick(v0, out _, out var bp, out _); return k * bp; }
    public double High(double v0) { Tick(v0, out _, out _, out var hp); return hp; }
    public void Tick(double v0, out double lp, out double bp, out double hp) {
        double v3 = v0 - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
        ic1 = 2 * v1 - ic1; ic2 = 2 * v2 - ic2; lp = v2; bp = v1; hp = v0 - k * v1 - v2; }
}
```
- Modulation: the trapezoidal SVF stays stable under per-sample coefficient changes (the reason to use it over a biquad). Spec "modulation smoothed 20 ms": smooth `log2(fc)` with the one-pole of section 5 (tau 20 ms), then `Set` per sample, or every 8-16 samples to save `tan`. UNVERIFIED-safe.
- Q below 0.5 is overdamped, still fine. Spec Q 0.6-2.0 is OK.

### 6.5 DC blocker (20 Hz)
VERIFIED-DOCS https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html : `y(n) = x(n) - x(n-1) + R*y(n-1)`, "R=0.995 is good" at 44.1 kHz.
DERIVED for fc = 20 Hz: `R = exp(-2*pi*fc/fs)` gives **0.997385 at 48 kHz** (about 1 - 2*pi*20/48000 = 0.997382).
```csharp
public struct DcBlock { double x1, y1, R; public void Set(double fc, double fs) { R = System.Math.Exp(-2 * System.Math.PI * fc / fs); }
    public double Tick(double x) { double y = x - x1 + R * y1; x1 = x; y1 = y; return y; } }
```
Place it on the master (before the limiter) per channel. Brown noise and asymmetric envelopes are the usual DC sources.

### 6.6 Smooth random (interpolated value noise, 3 octaves)
A pure function of time (no state), so the main thread can compute the same `_Gust` value without a thread hop. Quintic interpolation is C2, with no kinks in gain. DERIVED / UNVERIFIED-safe.
```csharp
public static class SmoothRandom {
    static double Hash01(uint seed, long i) {                      // splitmix64 finaliser
        ulong z = (ulong)i * 0x9E3779B97F4A7C15UL + seed * 0xD1B54A32D192ED03UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0); }          // [0,1)
    static double Value(uint seed, double t) {
        double fl = System.Math.Floor(t); long i = (long)fl; double f = t - fl;
        double u = f * f * f * (f * (f * 6 - 15) + 10);             // quintic
        double a = Hash01(seed, i) * 2 - 1, b = Hash01(seed, i + 1) * 2 - 1; return a + (b - a) * u; }
    /// <summary>[-1,1], about rateHz "events" per second, 3 octaves (1, 1/2, 1/4).</summary>
    public static double Get(uint seed, double timeSec, double rateHz) {
        double t = timeSec * rateHz;
        return (Value(seed, t) + 0.5 * Value(seed + 0x51u, 2.0 * t) + 0.25 * Value(seed + 0xA3u, 4.0 * t)) / 1.75; }
    public static double Get01(uint seed, double timeSec, double rateHz) => 0.5 + 0.5 * Get(seed, timeSec, rateHz);
}
// W1 gust (spec): gust = 0.35 + 0.65 * SmoothRandom.Get01(seed, t, 0.15);  _Gust = SmoothRandom.Get01(seed, t, 0.15)
```
Main thread: compute `_Gust` from the **audio clock** (`AudioSettings.dspTime - startDsp`) so the grass sway stays locked to what is heard, then `Shader.SetGlobalFloat("_Gust", v)`. The output latency is about 1-4 buffers (UNVERIFIED), negligible for sway.

### 6.7 Oscillators, envelopes, ramps
```csharp
public struct Osc { double ph; public double Sine(double f, double fs) { ph += f / fs; ph -= System.Math.Floor(ph); return System.Math.Sin(2 * System.Math.PI * ph); }
    public double Tri(double f, double fs) { ph += f / fs; ph -= System.Math.Floor(ph); return 1.0 - 4.0 * System.Math.Abs(ph - 0.5); } }
public struct Ramp {               // linear ramp to a target over a duration (>= 50 ms per spec), sample-accurate
    public double v; double inc; int left;
    public void To(double target, double seconds, double fs) { left = System.Math.Max(1, (int)(seconds * fs)); inc = (target - v) / left; }
    public double Next() { if (left > 0) { v += inc; left--; } return v; } }
```
- Cents: `f * 2^(c/1200)`. +/-3 cents gives 1 +/- 0.001735 (DERIVED).
- Vibrato 4.6 Hz +/-3 cents: `f(t) = 196 * 2^((3/1200)*sin(2*pi*4.6*t))`.
- Detune 0.09 Hz (S1 36.7 Hz + 55.0 Hz): add a second sine at f + 0.09 Hz at the same level, which gives an 11 s beat.
- `Math.Sin` per sample per oscillator: about 25 oscillators x 48k = 1.2 M calls/s, roughly 2-5 % of one core in Mono (UNVERIFIED estimate). Measure with the Inspector ms readout (F6) or the Profiler Audio tab.

### 6.8 Exponential-decay voices (bells, beacon, grains)
```csharp
public sealed class SineVoice {                    // pool these (preallocate 8), never new on the audio thread
    double ph1, ph2, f1, f2, w2, env, mul, amp; int atk, atkLen; public bool Active;
    public void Strike(double fs, double gain, double fA, double fB, double weightB, double t60, double attackSec = 0.004) {
        f1 = fA; f2 = fB; w2 = weightB; amp = gain; env = 1.0; mul = System.Math.Pow(10.0, -3.0 / (t60 * fs));
        atkLen = System.Math.Max(1, (int)(attackSec * fs)); atk = 0; ph1 = ph2 = 0; Active = true; }
    public double Next(double fs) {
        if (!Active) return 0;
        double a = atk < atkLen ? 0.5 - 0.5 * System.Math.Cos(System.Math.PI * atk++ / atkLen) : 1.0;  // raised-cosine attack, no click
        ph1 += f1 / fs; ph1 -= System.Math.Floor(ph1); ph2 += f2 / fs; ph2 -= System.Math.Floor(ph2);
        double s = amp * env * a * (System.Math.Sin(2 * System.Math.PI * ph1) + w2 * System.Math.Sin(2 * System.Math.PI * ph2));
        env *= mul; if (env < 1e-5) Active = false;   // -100 dB: stop (also avoids denormals)
        return s; } }
```
- **N1 bells:** `Strike(fs, g, 1174.7, 880.0, 0.7, t60: 5.0)` every `7 + 6*rng.Next01()` s. Interval and pan come from a seeded RNG, so the offline and player runs match.
- Optional realism, still spec-compatible (UNVERIFIED artistic): give the 880 Hz partial a shorter T60 (about 3.5 s), and add a +0.6 Hz partner at -14 dB for a slow shimmer beat.
- **B1 beacon:** `Strike(fs, g, 659.3, 0, 0, t60: 3.0, attackSec: 0.020)`. Schedule it by sample index from the pulse phase (see 7.3).
- **X1 grains:** 60 ms sine 2-6 kHz with a Hann window (not an exponential), 40/s, random pan with an equal-power law `L = cos(pi/4*(1+p))`, `R = sin(pi/4*(1+p))`. The window keeps the sample step tiny.

### 6.9 Look-ahead peak limiter (threshold -3, ceiling -1 dBFS, 5 ms look-ahead, 80 ms release)
Design per Signalsmith (VERIFIED-DOCS https://signalsmith-audio.co.uk/writing/2022/limiter/): (1) required gain per sample, (2) **moving minimum** over the look-ahead length ("the next N output samples are guaranteed to be <= that input sample"), (3) release that rises exponentially and never above the moving minimum, (4) **box-filter smoothing no longer than the minimum window** ("if the moving-average is finite, the output will be strictly below the input"), (5) delay the audio to align. Sample peaks only. Inter-sample peaks need oversampling.
DERIVED guarantee: with min window L, box length L and an audio delay of L-1, the output sample `x[k]*g[k+L-1]` satisfies `g[k+L-1] <= req[k]`, so `|y| <= ceiling` exactly for sample values.
Soft curve between threshold and ceiling (DERIVED): `outDb = T + (C - T)*tanh((pDb - T)/(C - T))`, so the slope is 1 at T and the output approaches C asymptotically.
```csharp
public sealed class LookaheadLimiter {           // stereo-linked; one instance on the master
    readonly int L; readonly double thrDb, ceilDb, thrLin, relCoef;
    readonly float[] dL, dR; int wp;            // ring of size L -> delay L-1
    readonly double[] qv; readonly long[] qi; int qh, qn;   // monotonic deque (ring, capacity L)
    readonly double[] box; double boxSum; int bp; double gRel = 1.0; long n;
    public int LatencySamples => L - 1;
    public LookaheadLimiter(int fs, double thresholdDb = -3.0, double ceilingDb = -1.2, double lookaheadMs = 5.0, double releaseMs = 80.0) {
        L = System.Math.Max(2, (int)System.Math.Round(lookaheadMs * 0.001 * fs));       // 240 @ 48 kHz
        thrDb = thresholdDb; ceilDb = ceilingDb; thrLin = System.Math.Pow(10, thrDb / 20);
        relCoef = 1.0 - System.Math.Exp(-1.0 / (releaseMs * 0.001 * fs));
        dL = new float[L]; dR = new float[L]; qv = new double[L]; qi = new long[L];
        box = new double[L]; for (int i = 0; i < L; i++) box[i] = 1.0; boxSum = L; }
    double Required(double p) {
        if (p <= thrLin) return 1.0;
        double pDb = 20.0 * System.Math.Log10(p), range = ceilDb - thrDb;
        double outDb = thrDb + range * System.Math.Tanh((pDb - thrDb) / range);
        return System.Math.Pow(10.0, (outDb - pDb) / 20.0); }
    public void Process(ref float l, ref float r) {
        double req = Required(System.Math.Max(System.Math.Abs(l), System.Math.Abs(r)));
        while (qn > 0 && qi[qh] <= n - L) { qh = (qh + 1) % L; qn--; }                 // expire old
        while (qn > 0 && qv[(qh + qn - 1) % L] >= req) qn--;                           // keep increasing
        int t = (qh + qn) % L; qv[t] = req; qi[t] = n; qn++;
        double m = qv[qh];                                                              // min of last L
        gRel = m < gRel ? m : gRel + (m - gRel) * relCoef;                              // instant down, 80 ms up
        boxSum += gRel - box[bp]; box[bp] = gRel; bp = (bp + 1) % L;
        if ((n & 0xFFFFF) == 0) { double s = 0; for (int i = 0; i < L; i++) s += box[i]; boxSum = s; } // drift guard
        double g = boxSum / L;
        dL[wp] = l; dR[wp] = r; wp = (wp + 1) % L;                                      // wp now = oldest
        l = (float)(dL[wp] * g); r = (float)(dR[wp] * g); n++; }
}
```
- **Use an internal ceiling of -1.2 dBFS** (sample peak) so the **true peak** stays at or below -1 dBTP. Inter-sample overshoot of low-frequency ambient material is small, typically under 0.2 dB (UNVERIFIED). Prove it with the 4x true-peak meter (section 9) on the offline renders and on the player dump. If it fails, feed `Required()` with a 4x-oversampled peak estimate.
- The latency of 239 samples (5 ms) is irrelevant here.
- `MasterBus` (on the AudioListener GameObject, last component):
```csharp
public sealed class MasterBus : MonoBehaviour {
    LookaheadLimiter lim; DcBlock dcL, dcR; double duck = 1, duckTarget = 1, duckA;
    float[] cap; int capPos; volatile bool capturing; volatile bool pausedFlag;
    void Awake() { int fs = AudioSettings.outputSampleRate; lim = new LookaheadLimiter(fs);
        dcL.Set(20, fs); dcR.Set(20, fs); duckA = 1 - System.Math.Exp(-1.0 / (0.4 / 3.0 * fs)); // ~0.4 s to 95 %
        cap = new float[fs * 2 * 10]; }                                     // 10 s stereo, preallocated
    public void SetPaused(bool p) => pausedFlag = p;                       // main thread
    public void BeginCapture() { capPos = 0; capturing = true; }
    public bool CaptureDone => !capturing && capPos > 0;
    void OnAudioFilterRead(float[] data, int ch) {
        double target = pausedFlag ? 0.2511886 : 1.0;                      // -12 dB pause duck
        for (int i = 0; i < data.Length; i += ch) {
            duck += duckA * (target - duck);
            float l = (float)(dcL.Tick(data[i]) * duck), r = ch > 1 ? (float)(dcR.Tick(data[i + 1]) * duck) : l;
            lim.Process(ref l, ref r); data[i] = l; if (ch > 1) data[i + 1] = r;
            for (int c = 2; c < ch; c++) data[i + c] = 0f; }
        if (capturing) { int p = capPos, k = System.Math.Min(data.Length, cap.Length - p);
            System.Array.Copy(data, 0, cap, p, k); capPos = p + k; if (capPos >= cap.Length) capturing = false; } }
}
```
(If `ch != 2`, the capture interleaving follows `ch`. Log `ch` and write the WAV with that channel count.)

### 6.10 Wind (W1)
Technique: band-passed pink noise whose centre frequency wanders under slow LFOs plus a smooth random gust envelope. Separate L/R noise seeds give width. Realism comes from 1/f-like intensity variation, several band-passes, a small lag between intensity and filter movement, and some reverb. VERIFIED-DOCS (practitioner thread, anecdotal) https://cycling74.com/forums/synthetic-wind . Spec values from 14.3:
```csharp
public sealed class Wind {      // one per channel (seed differs per channel)
    Rng rng; Pink pink; Svf bp1, bp2; double lfc1, lfc2, sA; readonly double fs; readonly double ph1, ph2, ph3;
    public double Fc = 420, Q = 0.8;                                   // beat 5: 220 Hz, Q 0.6 (ramped)
    public Wind(uint seed, double fs) { this.fs = fs; rng = new Rng(seed); var r = new Rng(seed * 7919u);
        ph1 = r.Next01() * 6.283; ph2 = r.Next01() * 6.283; ph3 = r.Next01() * 6.283;
        sA = 1 - System.Math.Exp(-1.0 / (0.020 * fs)); lfc1 = System.Math.Log(Fc); lfc2 = System.Math.Log(1150); }
    public double Next(double t, double gust) {                        // gust = 0.35 + 0.65*SmoothRandom.Get01(seed, t, 0.15)
        double w = pink.Next(rng.NextBipolar());
        double fc1 = Fc * (1 + 0.35 * System.Math.Sin(2 * System.Math.PI * 0.07 * t + ph1))
                        * (1 + 0.12 * System.Math.Sin(2 * System.Math.PI * 0.31 * t + ph2));
        double fc2 = 1150 * (1 + 0.15 * System.Math.Sin(2 * System.Math.PI * 0.11 * t + ph3));   // depth 15 % = UNVERIFIED choice
        lfc1 += sA * (System.Math.Log(fc1) - lfc1); lfc2 += sA * (System.Math.Log(fc2) - lfc2);    // 20 ms smoothing in log-f
        bp1.Set(System.Math.Exp(lfc1), Q, fs); bp2.Set(System.Math.Exp(lfc2), 1.4, fs);
        return (bp1.BandNorm(w) + 0.35481 * bp2.BandNorm(w)) * gust;   // second voice -9 dB = 0.35481
    }
}
```
Optional (artistic, UNVERIFIED): couple pitch to gusts (`Fc * (0.9 + 0.2*gust)`), lagging the gust by about 0.3 s, so stronger gusts sound brighter. S2 totality: wind -12 dB through a Ramp of at least 1 s.

### 6.11 Footsteps (noise burst into resonators)
Two-pole resonator with impulse response `r^n * sin((n+1)*w)` (peak about 1). DERIVED:
`y[n] = sin(w)*x[n] + 2r*cos(w)*y[n-1] - r^2*y[n-2]`, `w = 2*pi*f/fs`, `r = 10^(-3/(T60*fs))`.
```csharp
public struct Reso { double y1, y2, b0, c1, c2;
    public void Set(double f, double t60, double fs) { double w = 2 * System.Math.PI * f / fs, r = System.Math.Pow(10, -3.0 / (t60 * fs));
        b0 = System.Math.Sin(w); c1 = 2 * r * System.Math.Cos(w); c2 = -r * r; y1 = y2 = 0; }
    public double Tick(double x) { double y = b0 * x + c1 * y1 + c2 * y2; y2 = y1; y1 = y; return y; } }
// burst: e[n] = white * hann(n / N), N = burstSec * fs (3 ms = 144 samples at 48 kHz)
```
Spec recipes (14.3): metal = 3 ms burst into resonators 210/540/1300 Hz with T60 120/70/40 ms. Ceramic = 6 ms burst through BP 2.4 kHz + damped sine 380 Hz (T60 60 ms); the bridge adds 85 Hz (180 ms). Grass = noise BP 1.8 kHz Q 0.6 under a 110 ms envelope (10 ms attack). Stone = a 3.1 kHz tick + 220 Hz (80 ms). Deck = ceramic + 220 Hz (90 ms). Per step: alternate feet, gain `10^(0.8*u/20)` and pitch `1 + 0.04*u` (u uniform in [-1,1]) multiply all frequencies.
- A damped sine `A*sin(2*pi*f*t)*exp(-6.9078*t/T60)` starts at 0, so it needs no attack ramp.
- **Peak calibration:** in an EditMode test, render each surface's step 64 times (all random variants), measure the max sample peak, and store `norm[surface]` so that the peak is -30 dBFS +/- 0.3 dB (spec: "peak -30 dBFS").
- Footsteps bypass the master duck in S3/S4 ("only footsteps and tails"), so keep them on a separate bus gain inside `AudioEngine`.

### 6.12 Fallback rung: C# reverb (only if AudioReverbFilter fails the click test)
8-line FDN (Jot), delays mutually prime, about 1031-3001 samples at 48 kHz scaled by room size, Householder feedback `A = I - (2/N)*1*1^T`. Per-line gain `g_i = 10^(-3*d_i/(T60*fs))` and per-line one-pole HF damping for `decayHFRatio`. Parameter changes are then sample-smoothed by design, and the offline render becomes complete. UNVERIFIED design (standard FDN literature); ship it only if needed.

---

## 7. Thread-safe hand-off (main thread to audio thread)

Facts:
- `volatile` applies to reference types, `sbyte, byte, short, ushort, int, uint, char, float, bool`, small enums, IntPtr/UIntPtr. **Not `double` or `long`** ("reads and writes ... can't be guaranteed to be atomic"). VERIFIED-DOCS https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile
- `Volatile.Read/Write` overloads exist for `double`, `long`, `float`, `int` and `T : class`. "Volatile reads and writes on such 64-bit memory are atomic even on 32-bit processors". A volatile write keeps earlier memory operations before it (release). A volatile read keeps later operations after it (acquire). It does not guarantee freshness ("a following volatile read ... may not see the updated value"). VERIFIED-DOCS https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile
- Never use `lock`, `Monitor`, allocations, LINQ, `foreach` over interfaces, string ops or `Debug.Log` in OAFR (F2, F12). UNVERIFIED-safe best practice.

### 7.1 Seqlock snapshot (wait-free reader, no allocation)
```csharp
public struct AudioParams { public double progress; public double progressTime; public float speed; public int surface; public byte paused; }
public sealed class ParamChannel {
    int seq; AudioParams data;
    public void Publish(in AudioParams p) {                       // MAIN thread only, once per frame
        Volatile.Write(ref seq, seq + 1);                          // odd = writing
        Interlocked.MemoryBarrier();                               // keep data writes after the odd mark
        data = p;
        Volatile.Write(ref seq, seq + 1); }                        // even = stable (release)
    public bool TryRead(ref AudioParams dst) {                     // AUDIO thread, once per callback
        int s1 = Volatile.Read(ref seq); if ((s1 & 1) != 0) return false;
        AudioParams tmp = data;
        Interlocked.MemoryBarrier();                               // keep data reads before the second check
        if (Volatile.Read(ref seq) != s1) return false;
        dst = tmp; return true; }                                  // on false keep the previous snapshot, never spin
}
```
DERIVED (standard seqlock). Unit test: a writer thread hammers `Publish` with `progress == speed * k` invariants while a reader checks the invariant 1e7 times.

### 7.2 Per-sample smoothing (zipper-free)
- Gains: `Ramp` (linear, at least 50 ms per spec) or a one-pole with tau 20-50 ms. Apply them **per sample on the audio thread**, never per frame.
- Filter frequency: smooth in log-f with tau 20 ms (6.4 / 6.10).
- Progress `s` (metres): the audio thread extrapolates between snapshots, `sAudio = snap.progress + snap.speed * (tAudio - snap.progressTime)`, then a one-pole with tau 100 ms. `progressTime` is the main thread's `AudioSettings.dspTime` at publish. `tAudio` is the engine's own sample clock mapped to dspTime once at start. That keeps beat crossfades (24 m windows) smooth at a 60 Hz publish rate. DERIVED.
- Zipper rule of thumb: if a gain jumps per buffer (21 ms) by more than about 0.1 dB, it is audible on pure tones (UNVERIFIED). Per-sample smoothing removes the issue.

### 7.3 Events (footsteps, beacon pings): SPSC ring
```csharp
public struct StepEvent { public int surface; public byte foot; public double dspTime; }
public sealed class SpscQueue<T> where T : struct {       // single producer (main), single consumer (audio)
    readonly T[] buf; readonly int mask; int head, tail;
    public SpscQueue(int pow2) { buf = new T[pow2]; mask = pow2 - 1; }
    public bool TryEnqueue(in T v) { int h = head; if (h - Volatile.Read(ref tail) == buf.Length) return false;
        buf[h & mask] = v; Volatile.Write(ref head, h + 1); return true; }
    public bool TryDequeue(out T v) { int t = tail; if (t == Volatile.Read(ref head)) { v = default; return false; }
        v = buf[t & mask]; Volatile.Write(ref tail, t + 1); return true; } }
```
Sample-accurate timing: the main thread stamps `dspTime = AudioSettings.dspTime + 0.05` (a 50 ms margin covers 2 buffers). The audio thread converts it to a sample index and starts the voice inside the right buffer. Otherwise jitter is up to 21 ms, which is acceptable for steps but not for the beacon "locked to the pulse peak". Better still, compute the beacon schedule inside the engine from its own clock and publish the pulse phase to the main thread (the shader reads it), so the visual follows the audio.

---

## 8. Writing WAV files (EditMode renders, player dump)

Layout (VERIFIED-DOCS https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html): little-endian RIFF; `"RIFF" <size=4+n> "WAVE"`; `"fmt "` chunk with `wFormatTag, nChannels, nSamplesPerSec, nAvgBytesPerSec(=fs*block), nBlockAlign(=channels*bytes), wBitsPerSample`. PCM = tag 1, fmt size **16**. IEEE float = tag **3**, fmt size **18** with `cbSize = 0` and a **`fact` chunk** (size 4, `dwSampleLength` = frames per channel), both required for non-PCM. Pad odd-sized chunks. WAVE_FORMAT_EXTENSIBLE (0xFFFE) is needed for more than 2 channels or more than 16-bit PCM. 32-bit float stereo with tag 3 is fine.
```csharp
using System; using System.IO; using System.Text;
public static class Wav {
    public static void Write(string path, float[] interleaved, int frames, int channels, int fs, bool float32) {
        int bps = float32 ? 4 : 2, dataBytes = frames * channels * bps, fmtSize = float32 ? 18 : 16, factBytes = float32 ? 12 : 0;
        int riff = 4 + (8 + fmtSize) + factBytes + (8 + dataBytes) + (dataBytes & 1);
        using var w = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write), Encoding.ASCII);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(riff); w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(fmtSize);
        w.Write((short)(float32 ? 3 : 1)); w.Write((short)channels); w.Write(fs);
        w.Write(fs * channels * bps); w.Write((short)(channels * bps)); w.Write((short)(bps * 8));
        if (float32) { w.Write((short)0); w.Write(Encoding.ASCII.GetBytes("fact")); w.Write(4); w.Write(frames); }
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
        int n = frames * channels;
        if (float32) for (int i = 0; i < n; i++) w.Write(interleaved[i]);
        else for (int i = 0; i < n; i++) { double v = Math.Max(-1.0, Math.Min(1.0, interleaved[i])); w.Write((short)Math.Round(v * 32767.0)); }
        if ((dataBytes & 1) != 0) w.Write((byte)0); } }
```
- Use **32-bit float for analysis** (no quantisation, values above 1 survive) and 16-bit for listening copies.
- A 16 min stereo render at 48 kHz is about 92 M floats (368 MB). Render and analyse per beat, or stream blocks to the file (write the header with placeholder sizes, then seek back and patch the RIFF and data sizes).

---

## 9. Analysis metrics (pure C#, EditMode and post-run)

Spec master targets (14.3): true peak <= -1 dBFS, |DC| < 0.001, max sample step <= 0.25 (hatch sweep excepted), sliding 3 s RMS per beat within +/-1.5 dB.
```csharp
public static class AudioMetrics {
    // 4x polyphase windowed-sinc interpolator (65 taps, Blackman-Harris); phase 0 reproduces the samples.
    public static double[][] TruePeakPhases(int L = 4, int tapsPerPhase = 16) {
        int N = L * tapsPerPhase + 1; double c = (N - 1) / 2.0; var h = new double[N];
        for (int k = 0; k < N; k++) { double x = (k - c) / L, s = x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);
            double a = 2 * Math.PI * k / (N - 1);
            h[k] = s * (0.35875 - 0.48829 * Math.Cos(a) + 0.14128 * Math.Cos(2 * a) - 0.01168 * Math.Cos(3 * a)); }
        var ph = new double[L][];
        for (int p = 0; p < L; p++) { int cnt = (N - p + L - 1) / L; ph[p] = new double[cnt]; double sum = 0;
            for (int j = 0; j < cnt; j++) { ph[p][j] = h[p + j * L]; sum += ph[p][j]; }
            for (int j = 0; j < cnt; j++) ph[p][j] /= sum; }      // unity DC gain per phase
        return ph; }
    public static double TruePeakDb(float[] x, int frames, int channels, double[][] ph) {
        double max = 0;
        for (int ch = 0; ch < channels; ch++)
            for (int n = 0; n < frames; n++)
                for (int p = 0; p < ph.Length; p++) { double acc = 0; var f = ph[p];
                    for (int j = 0; j < f.Length; j++) { int m = n - j; if (m < 0) break; acc += f[j] * x[m * channels + ch]; }
                    double a = Math.Abs(acc); if (a > max) max = a; }
        return 20 * Math.Log10(Math.Max(max, 1e-12)); }
    public static double SamplePeakDb(float[] x, int n) { double m = 0; for (int i = 0; i < n; i++) m = Math.Max(m, Math.Abs(x[i])); return 20 * Math.Log10(Math.Max(m, 1e-12)); }
    public static double[] DcPerChannel(float[] x, int frames, int ch) { var s = new double[ch];
        for (int n = 0; n < frames; n++) for (int c = 0; c < ch; c++) s[c] += x[n * ch + c];
        for (int c = 0; c < ch; c++) s[c] /= frames; return s; }
    public static (double maxStep, int atFrame) MaxStep(float[] x, int frames, int ch) { double m = 0; int at = 0;
        for (int n = 1; n < frames; n++) for (int c = 0; c < ch; c++) { double d = Math.Abs(x[n * ch + c] - x[(n - 1) * ch + c]); if (d > m) { m = d; at = n; } }
        return (m, at); }
    // Sliding RMS: hop sums (memory-light), window = 3 s, hop = 0.1 s; returns dB per hop (window end time = (i+1)*hop)
    public static double[] SlidingRmsDb(float[] x, int frames, int ch, int fs, double windowSec = 3.0, double hopSec = 0.1) {
        int hop = (int)(hopSec * fs), nh = frames / hop, wh = (int)Math.Round(windowSec / hopSec); var hs = new double[nh];
        for (int i = 0; i < nh; i++) { double s = 0; int b = i * hop * ch, e = b + hop * ch; for (int k = b; k < e; k++) s += (double)x[k] * x[k]; hs[i] = s; }
        var outDb = new double[Math.Max(0, nh - wh + 1)]; double acc = 0;
        for (int i = 0; i < nh; i++) { acc += hs[i]; if (i >= wh) acc -= hs[i - wh];
            if (i >= wh - 1) outDb[i - wh + 1] = 10 * Math.Log10(acc / ((double)wh * hop * ch) + 1e-30); }
        return outDb; }
}
```
- The **true-peak self-test** (DERIVED): a sine at fs/4 with phase pi/4 and amplitude 1 has sample peaks of 0.7071 (-3.01 dBFS) but a true peak of 0 dBTP. The meter must read 0 +/- 0.1 dB. BS.1770-4 Annex 2 uses 4x oversampling at 48 kHz with a 48-tap (12 per phase) FIR, whose ripple can shift estimates by about +/-0.1 dB. VERIFIED-DOCS via the MATLAB loudnessMeter page, Essentia's TruePeakDetector reference and Slate Digital (search summaries). The exact ITU coefficient table was not retrieved, so this windowed-sinc design is an equivalent substitute (UNVERIFIED equivalence; the self-test proves it is adequate).
- The cost is O(frames x 65) per channel. For 16 min stereo that is about 6e9 MACs, a few seconds in C# (UNVERIFIED). Analyse per beat, or only where the sample peak is above -4 dBFS.
- **Click detector** (heuristic, UNVERIFIED): the second difference `d2 = x[n] - 2x[n-1] + x[n-2]`. Flag frames where `|d2| > 8 * RMS(d2 over the surrounding 10 ms)` and `|d2| > 0.005`. Run it on every blend window and on layer starts and stops. The step rule (0.25) only catches gross jumps.
- Step budget sanity (DERIVED): the max step of a sine is `2*pi*f*A/fs`. A 6 kHz grain at -38 dBFS (A = 0.0126) gives 0.0099, and wind BP 420 Hz at -24 dBFS RMS has steps far below 0.25. Only full-band white noise near 0 dBFS could break 0.25. The hatch LP sweep to 6 kHz is the declared exception.
- Also check that the first and last 10 ms of every render are at or below -60 dBFS RMS (fade-in and fade-out with the picture, spec S5).

---

## 10. Pitfalls specific to this version / setup (checklist)

1. `m_SampleRate: 0` in `AM:11`: the device rate is used (it may be 44.1 kHz). Set 48000 and still compute from `outputSampleRate`.
2. `runInBackground: 0` in `PS:90`: on focus loss the player and `dspTime` stop. Set it to 1 (spec).
3. Generator detection only happens at `AudioSource.Play()` (F8). Add components, then Play. Never Stop or toggle (F9, F10).
4. Never share one GameObject between an AudioSource and an AudioListener with an OAFR script (F11).
5. GC pauses cause dropouts (F12). Zero allocations per frame during the walk. Keep incremental GC.
6. `AudioReverbFilter.reflectionsLevel` defaults to -10000 (ER off). The scripting doc for `reflectionsDelay` shows the wrong range text (section 4).
7. The reverb is not in the offline render. Trims are verified on the player dump, and the limiter sits post-reverb on the listener.
8. The sample-peak limiter does not bound the true peak. Use a -1.2 dBFS internal ceiling and verify dBTP.
9. Batchmode audio is UNVERIFIED. Do not base tests on OAFR in batchmode. Use the pure-C# render plus one windowed player run with the probe and dump.
10. Device change or `AudioSettings.Reset`: re-Play the source and re-init coefficients in `OnAudioConfigurationChanged`.
11. Denormals: decaying voices are stopped at -100 dB; filters fed by noise never reach the denormal range. If an input can be exactly 0 for a long time, flush states below 1e-20 (UNVERIFIED need, cheap insurance).
12. `AudioListener.pause` freezes all sources (and the reverb tail) abruptly. Implement the pause duck (-12 dB over 0.4 s) in `MasterBus` instead (spec).
13. `Debug.Log` inside OAFR (as in the official example) is off-limits. Expose counters through `Interlocked` fields and log them from `Update`.

---

## Sources (all accessed 2026-10-29)

| Source | Used for |
|---|---|
| `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.AudioModule.xml` (lines 298, 516, 851, 1211, 1762-1791, 1898-1985, 1986-2065, 2206, 2336-2438) | Exact API names: AudioConfiguration, AudioSettings (GetConfiguration/Reset/OnAudioConfigurationChanged/GetDSPBufferSize/outputSampleRate/dspTime), AudioReverbFilter properties, AudioRenderer, scriptable pipeline types |
| `...\Editor\Data\Documentation\en\ScriptReference\MonoBehaviour.OnAudioFilterRead.html` | Thread, call rate, generator mode, ordering, interleaving, Web restriction, example |
| `...\Documentation\en\ScriptReference\AudioSettings-outputSampleRate.html`, `AudioSettings.GetDSPBufferSize.html`, `AudioSettings-dspTime.html`, `AudioConfiguration-sampleRate.html`, `AudioConfiguration-dspBufferSize.html`, `AudioSettings.Reset.html`, `AudioReverbFilter-reflectionsDelay.html`, `AudioReverbFilter-reverbPreset.html` | Sample rate rule, buffer semantics, Reset example, doc error |
| `...\Documentation\en\Manual\class-AudioReverbFilter.html`, `class-AudioManager.html`, `class-AudioEffect.html`, `audio-filters.html`, `class-AudioListener.html`, `audio-scriptable-processors.html`, `audio-scriptable-processors-generators.html`, `PlayerCommandLineArguments.html`, `EditorCommandLineArguments.html` | Reverb ranges/defaults, project audio settings, effect ordering, listener effects, IAudioGenerator, no audio CLI flag |
| `W:\...\ProjectSettings\AudioManager.asset` (lines 10-19), `ProjectSettings\ProjectSettings.asset` (lines 90, 853) | Current sample rate, DSP buffer, speaker mode, audio disable flag, runInBackground, incremental GC |
| `W:\...\MASTER_PROMPT.md` 14.3 (lines 530-555), 11.9 (line 331) | Spec targets the notes are aligned to |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/onaudiofilterread | Cross-check of the OAFR page (online) |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosettings/reset | Reset example and valid value lists |
| https://docs.unity.com/en-us/engine/6000.3/manual/audio/reference/filters/class-audio-reverb-filter | Reverb table cross-check |
| https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager | Audio settings cross-check |
| https://issuetracker.unity3d.com/issues/onaudiofilterread-does-not-affect-signal-on-audiolistener-after-enable (UUM-116871; content via search summaries, page is JS-rendered) | Listener filter fix versions; related UUM-117120, UUM-61733 |
| https://issuetracker.unity.com/issues/8659/audio-stuttering-occurs-when-heavy-processing-is-performed-while-onaudiofilterread-is-in-use (UUM-122520) and https://issuetracker-mig.prd.it.unity3d.com/issues/audio-dropouts-are-caused-by-garbage-collector-when-large-amount-of-memory-are-being-allocated (870041) | GC coupling of OAFR, Won't Fix |
| https://issuetracker.unity3d.com/issues/onaudiofilterread-is-never-called-if-added-programmatically-via-addcomponent | AddComponent works since 5.3 |
| https://issuetracker.unity3d.com/issues/empty-data-buffer-gets-passed-to-onaudiofilterread-when-using-video-player (quoted in search results) | Generator detection at AudioSource.Play |
| https://discussions.unity.com/t/onaudiofilterread-not-playing/503615 | Write into `data`, do not reassign |
| https://discussions.unity.com/t/audiosettings-dsptime-and-native-unityaudioeffectstate-currdsptick-sometimes-out-of-sync/671905 | dspTime freezes in background without Run In Background |
| https://www.firstpr.com.au/dsp/pink-noise/ | Kellet refined and economy pink filters, accuracy |
| https://noisehack.com/generate-noise-web-audio-api/ | Pink gain 0.11, brown leaky integrator 0.02/1.02 and gain 3.5 |
| https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf | Trapezoidal SVF nodal equations, k = 1/Q, state update |
| https://www.native-instruments.com/fileadmin/ni_media/downloads/pdf/VAFilterDesign_2.1.0.pdf (404 on fetch) | Zavalishin TPT SVF reference (equations derived, not fetched) |
| https://ccrma.stanford.edu/~jos/filters/DC_Blocker.html | DC blocker equation, R guidance |
| https://signalsmith-audio.co.uk/writing/2022/limiter/ | Look-ahead limiter structure and no-overshoot argument |
| https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html | WAV PCM / IEEE float / fact / extensible rules |
| https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/volatile | Types allowed for `volatile`, limits |
| https://learn.microsoft.com/en-us/dotnet/api/system.threading.volatile | Volatile.Read/Write semantics, 64-bit atomicity |
| https://cycling74.com/forums/synthetic-wind | Wind realism heuristics (anecdotal) |
| https://essentia.upf.edu/reference/std_TruePeakDetector.html, https://cacophony.js.org/classes/TruePeakDetector.html, MATLAB loudnessMeter page and Slate Digital bus-clipper article (via search summaries) | True-peak 4x oversampling structure, ITU filter ripple |
