# 06 - Runtime UI Toolkit text, Input System from code, editor build automation (Unity 6000.6.0f1)

Researched 2026-10-29 for "The Ring". Read-only research; nothing else in the project was touched.
Tags: **VERIFIED-SOURCE** = read from package source, Unity's managed DLLs (reflection / IL dump with a .NET 10 script in the scratchpad), Unity's bundled XML docs or the project's own files. **VERIFIED-DOCS** = official docs page (URL). **UNVERIFIED** = engineering judgement or community knowledge, so test it in the built player.

Abbreviations: `UIE.xml` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.UIElementsModule.xml`. "Reflection" = member list dumped from `...\Data\Managed\UnityEngine\<Module>.dll`.

---

## 0. Facts about THIS project (read before acting)

| Fact | Value | Tag |
|---|---|---|
| `activeInputHandler` | `1` (Input System only), so **nothing to change and no restart needed** | VERIFIED-SOURCE ProjectSettings/ProjectSettings.asset:935 |
| Project-wide actions | `com.unity.input.settings.actions` -> `Assets/InputSystem_Actions.inputactions` (auto-enabled at startup; harmless next to code-made actions) | VERIFIED-SOURCE ProjectSettings/EditorBuildSettings.asset:12 |
| Colour space | `m_ActiveColorSpace: 1` = Linear | VERIFIED-SOURCE ProjectSettings.asset:49 |
| Windows graphics APIs | `m_Automatic: 0`, `m_APIs: 0200000012000000i` = [Direct3D11 (2), Direct3D12 (18)] | VERIFIED-SOURCE ProjectSettings.asset:542-544 + GraphicsDeviceType enum by reflection |
| `allowFullscreenSwitch: 1`, `fullscreenMode: 1` (FullScreenWindow), `resizableWindow: 0`, `runInBackground: 0`, `m_ShowUnitySplashScreen: 1`, `defaultScreenWidth/Height: 1024/768` | current defaults | VERIFIED-SOURCE ProjectSettings.asset:20,44,90,104,115,116 |
| Installed Windows player variations | **Mono only** (`win64_player_(non)development_mono`; no IL2CPP folder). **Use `ScriptingImplementation.Mono2x`** | VERIFIED-SOURCE `...\Data\PlaybackEngines\windowsstandalonesupport\Variations` |
| Build scene list | only `Assets/Scenes/SampleScene.unity` | VERIFIED-SOURCE EditorBuildSettings.asset:7-10 |
| Always Included Shaders | 7 built-in entries, `m_PreloadedShaders: []` | VERIFIED-SOURCE ProjectSettings/GraphicsSettings.asset:29-37 |
| No Build Profile assets, no PanelSettings / .tss in Assets | so `BuildPlayer(BuildPlayerOptions)` is the right call | VERIFIED-SOURCE (file search) |
| Packages | inputsystem 1.20.0, test-framework 1.8.0, URP 17.6.0, ugui 2.6.0 | VERIFIED-SOURCE Packages/manifest.json |
| `Temp/UnityLockfile` | absent at research time (2026-10-29) | VERIFIED-SOURCE |

---

## 1. UI Toolkit at runtime without UXML

### 1.1 Types and signatures (reflection on UnityEngine.UIElementsModule.dll) - VERIFIED-SOURCE
```
class PanelSettings : ScriptableObject
  ThemeStyleSheet themeStyleSheet {get;set;}
  PanelScaleMode scaleMode {get;set;}            // enum: ConstantPixelSize=0, ConstantPhysicalSize=1, ScaleWithScreenSize=2
  Vector2Int referenceResolution {get;set;}      // NOT Vector2
  PanelScreenMatchMode screenMatchMode {get;set;}// enum: MatchWidthOrHeight=0, Shrink=1, Expand=2
  float match {get;set;}
  float sortingOrder {get;set;}                  // FLOAT in 6000.6 (the docs page lists no type; some web summaries say int)
  float scale, referenceDpi, fallbackDpi, referenceSpritePixelsPerUnit
  int targetDisplay; RenderTexture targetTexture; PanelRenderMode renderMode (ScreenSpaceOverlay | WorldSpace)
  bool clearColor, clearDepthStencil; Color colorClearValue; uint vertexBudget
  TextureSlotCount textureSlotCount; DynamicAtlasSettings dynamicAtlasSettings; BindingLogLevel bindingLogLevel
  field PanelTextSettings textSettings;  field bool forceGammaRendering;
class UIDocument : MonoBehaviour   [AddComponentMenu("UI Toolkit/Legacy/UI Document (UI Toolkit)")] [ExecuteAlways] [DisallowMultipleComponent] [DefaultExecutionOrder(-100)]  (NO [Obsolete])
  PanelSettings panelSettings {get;set;}  VisualTreeAsset visualTreeAsset {get;set;}
  VisualElement rootVisualElement {get;}  float sortingOrder {get;set;}  IRuntimePanel runtimePanel {get;}
class PanelRenderer : Renderer     [AddComponentMenu("UI Toolkit/Panel Renderer (UI Toolkit)")]   // NEW successor of UIDocument
  PanelSettings panelSettings; VisualTreeAsset visualTreeAsset; (rootVisualElement is INTERNAL)
  void RegisterUIReloadCallback(VersionedUIReloadCallback cb)    // delegate void (PanelRenderer r, VisualElement rootElement, int version)
  void UnregisterUIReloadCallback(VersionedUIReloadCallback cb)  // the UIReloadCallback (no version) overloads are [Obsolete]
struct FontDefinition { Font font; FontAsset fontAsset; static FromFont(Font); static FromSDFFont(TextCore.Text.FontAsset); }
class ThemeStyleSheet : StyleSheet  (public ctor; ScriptableObject.CreateInstance<ThemeStyleSheet>() works)
```
PanelSettings defaults from IL (`PanelSettings..ctor`): scaleMode = ConstantPhysicalSize, referenceResolution = (1200, 800), referenceDpi = fallbackDpi = 96, scale = 1, match = 0, sortingOrder = 0, clearDepthStencil = true, textureSlotCount = 8. VERIFIED-SOURCE (IL).

**Surprise for 6000.6:** the manual says UIDocument is "obsolete and superseded by the Panel Renderer component" and can no longer be added from the Editor menu. Existing components keep working, and `AddComponent<UIDocument>()` from code still compiles with no CS0618, because the type has no `[Obsolete]`. VERIFIED-DOCS https://docs.unity.cn/6000.6/Documentation/Manual/UIE-create-ui-document-component.html + VERIFIED-SOURCE (attributes by reflection).
- UIDocument **clears and rebuilds its tree on every OnEnable/OnDisable**, so code-built children are lost when it is toggled. Never disable it. Hide with `root.style.display = DisplayStyle.None` instead. VERIFIED-DOCS (same page).
- PanelRenderer keeps its content while disabled, and you build UI inside the reload callback. `RegisterUIReloadCallback` invokes the callback immediately if the root is already attached to a panel. VERIFIED-SOURCE (IL of `PanelRenderer.RegisterUIReloadCallback`).
- Recommendation: use **UIDocument**, kept enabled, built once in code. It is simplest and has public `rootVisualElement`. PanelRenderer is the fallback / forward path (skeleton in 1.6). UNVERIFIED (judgement).

### 1.2 Theme style sheet: is one required? Where does it come from?
- No theme gives a warning in play mode / player only: `"No Theme Style Sheet set to PanelSettings " + name + ", UI will not render properly"`. VERIFIED-SOURCE (IL of `PanelSettings.ApplyThemeStyleSheet`). It is gated by the internal `m_DisableNoThemeWarning`, which has no public setter.
- The theme supplies default control styles and **the default font**. The runtime fallback `PanelTextSettings.defaultPanelTextSettings` is just `ScriptableObject.CreateInstance<PanelTextSettings>()`, which has no default font asset. VERIFIED-SOURCE (IL of `PanelTextSettings.InitializeDefaultPanelTextSettingsIfNull`). Consequence: **with no theme and no explicit font, text has no font, so it is blank or warns**. UNVERIFIED as observed behaviour, but implied by the source.
- The editor's "Create > UI Toolkit > Panel Settings" path (internal `UnityEditor.UIElements.PanelSettingsCreator.GetFirstThemeOrCreateDefaultTheme/CreateDefaultTheme`) writes **`Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`** with the content
  ```
  @import url("unity-theme://default");
  VisualElement {}
  ```
  VERIFIED-SOURCE (string constants `ThemeUtility.defaultRuntimeThemeContent` and the path in `UnityEditor.UIElementsModule.dll`). The package does **not** create the file on its own. It appears only through that menu path, or through `PanelSettings.Reset()` -> `GetOrCreateDefaultTheme` (an editor-only static hook).
- Default theme font in 6000.6: the editor resources contain `UIPackageResources/Fonts/Inter/Inter-Regular SDF.asset`, so the default runtime theme is very likely Inter. VERIFIED-SOURCE (string in `Data\Resources\unity editor resources`). That this exact asset is the theme font is UNVERIFIED.
- **Script-only way that imports nothing:** in the editor build script, write that same two-line `.tss` yourself (it is code-generated text referencing Unity's built-in theme), import it, and load it as `ThemeStyleSheet` (skeleton 1.5). `ScriptableObject.CreateInstance<ThemeStyleSheet>()` (empty) only silences the warning. It gives no default font or styles, so then **every** text element needs an explicit font. VERIFIED-SOURCE (public ctor).

### 1.3 Player pitfall: PanelSettings shaders and Shader.Find
`PanelSettings.OnEnable()` -> `InitializeShaders()` fills serialized `[SerializeField, HideInInspector]` shader fields **only if null**, via `Shader.Find(...)`. VERIFIED-SOURCE (IL + attributes):
| field | shader name |
|---|---|
| m_DefaultShader | `Hidden/Internal-UIRDefault` |
| m_AtlasBlitShader | `Hidden/Internal-UIRAtlasBlitCopy` |
| m_RuntimeGaussianBlurShader | `Hidden/UIR/GaussianBlur` |
| m_RuntimeColorEffectShader | `Hidden/UIR/ColorEffect` |
| m_RuntimeDropShadowComposite | `Hidden/UIR/DropShadowComposite` |
| m_SDFShader | `Hidden/TextCore/Distance Field SSD` (TextShaderUtilities.k_SDFText) |
| m_BitmapShader | `Hidden/Internal-GUITextureClipText` (k_BitmapText) |
| m_SpriteShader | `Hidden/TextCore/Sprite` (k_SpriteText) |
| (other) | `Hidden/Internal-UIE-ColorConversionBlit` (Shaders.k_ColorConversionBlit) |

These shaders live in the editor's `Data\Resources\unity_builtin_extra`, **not** in the player's `unity default resources`. VERIFIED-SOURCE (string scan of both files). `Shader.Find` in a player only finds shaders that are in the build. So a PanelSettings made with `CreateInstance` **at runtime in the player** can end up with null shaders, and the UI then renders nothing. UNVERIFIED as observed behaviour, but it follows from the IL.
**Fix (do both):**
1. Create the PanelSettings **as an asset in the editor** (`CreateInstance` -> OnEnable runs InitializeShaders in the editor -> `AssetDatabase.CreateAsset` serializes the shader references). Reference it from the scene's UIDocument, or put it under `Resources/`. Then verify that the `.asset` YAML has non-zero `m_DefaultShader: {fileID: ..., guid: 0000000000000000f000000000000000, type: 0}`.
2. Belt and braces: add the shaders above to Always Included Shaders (skeleton 4.4).
- Runtime TextCore font asset creation (`FontAsset.CreateFontAssetInstance`) builds its material from `Shader.Find("Hidden/TextCore/Distance Field SSD")`, so the same inclusion rule applies. VERIFIED-SOURCE (IL `TextShaderUtilities.get_ShaderRef_MobileSDF`).
- ICU: `"ICU Data not available ... It will not be present on PanelSettings created at runtime, so make sure the build contains at least one PanelSettings asset"`. This matters only for the **advanced** text generator. Keep the standard generator (the default). VERIFIED-SOURCE (string in UIElementsModule.dll).

### 1.4 Style API facts - VERIFIED-SOURCE (IStyle reflection, UIE.xml)
- `style.letterSpacing` / `wordSpacing` / `fontSize` / `unityParagraphSpacing` are `StyleLength`. `LengthUnit` has **only `Pixel` and `Percent`, no em**. A float converts implicitly to pixels (`new StyleLength(float)` ctor). For "0.3em", compute `0.3f * fontPx`. UIE.xml:13460-13581.
- `style.opacity` is `StyleFloat` (0..1, applies to children). `style.color` is `StyleColor`.
- `-unity-text-align` maps to `style.unityTextAlign = TextAnchor.MiddleCenter` (`StyleEnum<TextAnchor>`).
- `style.unityFontDefinition` is `StyleFontDefinition` (ctors: `FontDefinition`, `FontAsset`, `Font`, `StyleKeyword`) and "takes precedence over -unity-font" (UIE.xml:12452). `style.unityFont` is `StyleFont` (legacy Font).
- `style.unityFontStyleAndWeight = FontStyle.Bold`; `style.textShadow` is `StyleTextShadow`; `style.unityTextOutlineWidth` is StyleFloat; `style.unityTextOutlineColor`; `style.whiteSpace = WhiteSpace.Normal/NoWrap`; `style.unityTextGenerator` (Standard/Advanced, keep the default).
- `Label(string)`, `TextElement.text`, `enableRichText`, `SetText(...)` overloads; `MeasureTextSize(...)`.
- `TextSettings` (base of PanelTextSettings) props: `defaultFontAsset`, `fallbackFontAssets (List<FontAsset>)`, `missingCharacterUnicode`, `enableEmojiSupport`, `displayWarnings`. PanelSettings.`textSettings` is a public **field**.

### 1.5 Fonts at runtime - choices
- `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` is **valid in 6000.6**. The Win64 player's `Data\Resources\unity default resources` contains a Font object `LegacyRuntime` (family Arial), and `UnityPlayer.dll` contains the literal `LegacyRuntime.ttf`. VERIFIED-SOURCE (binary string scan). The old `"Arial.ttf"` name was replaced in 2022.2. UNVERIFIED for 6000.6 itself.
- `FontDefinition.FromFont(font)` makes UI Toolkit build a dynamic SDF FontAsset internally via `FontAsset.CreateFontAsset(Font,...)` -> `FontEngine.LoadFontFace(font)`. On failure it logs `Unable to load font face for [..]. Make sure "Include Font Data" is enabled`. VERIFIED-SOURCE (IL strings). Whether LegacyRuntime works through this path is UNVERIFIED, so test it in the player.
- OS font without an asset: `FontAsset.CreateFontAsset(string familyName, string styleName, int pointSize = 90)`, e.g. `("Segoe UI", "Light")`. It logs "Unable to find a font file with the specified Family Name" on failure. VERIFIED-SOURCE. Or use the file path overload `CreateFontAsset(string fontFilePath, int faceIndex, int samplingPointSize, int atlasPadding, GlyphRenderMode renderMode, int atlasWidth, int atlasHeight)` with `C:\Windows\Fonts\segoeuil.ttf`. Then `style.unityFontDefinition = FontDefinition.FromSDFFont(fa)`. Also `Font.CreateDynamicFontFromOSFont(string, int)` and `Font.GetOSInstalledFontNames()` exist. VERIFIED-SOURCE (reflection).
- Safest order: theme default font (from the generated .tss) -> explicit OS FontAsset (Segoe UI) -> LegacyRuntime. Always check `FontAsset != null` and log. Prove it with a **player capture** that has text on screen.

### 1.6 Skeletons
Editor (build script) - generate the theme + PanelSettings asset (UNVERIFIED as a whole; built from verified APIs):
```csharp
using System.IO; using UnityEditor; using UnityEngine; using UnityEngine.UIElements;
static PanelSettings EnsurePanelSettings() {
    const string dir = "Assets/Ring/UI"; Directory.CreateDirectory(dir);
    const string tssPath = dir + "/RingRuntimeTheme.tss";
    if (!File.Exists(tssPath)) File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\nVisualElement {}\n");
    AssetDatabase.ImportAsset(tssPath, ImportAssetOptions.ForceSynchronousImport);
    var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
    const string psPath = dir + "/RingPanelSettings.asset";   // or Assets/Ring/Resources/... for Resources.Load
    var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath);
    if (ps == null) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, psPath); }
    ps.themeStyleSheet = theme;
    ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
    ps.referenceResolution = new Vector2Int(1920, 1080);
    ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
    ps.match = 0.5f; ps.sortingOrder = 100f; ps.clearColor = false;
    EditorUtility.SetDirty(ps); AssetDatabase.SaveAssets();
    return ps;   // assign to UIDocument.panelSettings on a GameObject in the saved scene
}
```
Runtime HUD (UIDocument kept enabled):
```csharp
[RequireComponent(typeof(UIDocument))]
public sealed class RingHud : MonoBehaviour {
    Label _title; VisualElement _root;
    void OnEnable() {                       // UIDocument (order -100) enabled first, so rootVisualElement exists here
        _root = GetComponent<UIDocument>().rootVisualElement;
        _root.Clear();
        _root.style.flexGrow = 1; _root.pickingMode = PickingMode.Ignore;
        _title = new Label("THE RING") { pickingMode = PickingMode.Ignore };
        _title.style.unityTextAlign = TextAnchor.MiddleCenter;
        _title.style.fontSize = 64; _title.style.letterSpacing = 0.25f * 64f;   // px, no em
        _title.style.color = new Color(1, 1, 1, 1); _title.style.opacity = 0f;
        // optional explicit font: _title.style.unityFontDefinition = FontDefinition.FromSDFFont(fa);
        _root.Add(_title);
    }
    void Update() { _title.style.opacity = Mathf.MoveTowards(_title.resolvedStyle.opacity, 1f, Time.unscaledDeltaTime * 0.5f); }
}
```
If you add it at runtime: `var go = new GameObject("HUD"); go.SetActive(false); var d = go.AddComponent<UIDocument>(); d.panelSettings = ps; go.AddComponent<RingHud>(); go.SetActive(true);`. The `panelSettings` setter attaches the document (`PanelSettings.AttachAndInsertPanelComponentToVisualTree`). VERIFIED-SOURCE (IL of `UIDocument.set_panelSettings`).
PanelRenderer variant:
```csharp
var pr = go.AddComponent<PanelRenderer>(); pr.panelSettings = ps;
pr.RegisterUIReloadCallback((PanelRenderer r, VisualElement root, int version) => { root.Clear(); root.Add(new Label("THE RING")); });
```
- URP: overlay UI is drawn by `DrawScreenSpaceUIPass` at `RenderPassEvent.AfterRendering + k_AfterFinalBlitPassQueueOffset` ("after m_FinalBlitPass"), so **post-processing (bloom, tonemapping, grain) does not touch the HUD**. VERIFIED-SOURCE URP Runtime/UniversalRenderer.cs:421-422. A camera rendering to `targetTexture` will not contain the overlay UI. UNVERIFIED for capture tooling. Check whether `ScreenCapture` captures include the UI in the player.

---

## 2. Input System 1.20 from code

### 2.1 Signatures - VERIFIED-SOURCE (com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime/...)
- `new InputAction(string name = null, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string expectedControlType = null)` - Actions/InputAction.cs:800
- `Enable()` :946, `Disable()` :978, `ReadValue<TValue>() where TValue : struct` :1083, `WasPressedThisFrame()` :1307, `WasReleasedThisFrame()` :1402, `WasPerformedThisFrame()` :1509, `triggered => WasPerformedThisFrame()` :622
- `AddBinding(this InputAction, string path, string interactions = null, string processors = null, string groups = null)` -> `BindingSyntax` - Actions/InputActionSetupExtensions.cs:305; `.WithProcessor(string)` :1354, `.WithInteraction` :1288, `.WithGroup` :1243
- `AddCompositeBinding(this InputAction, string composite, string interactions = null, string processors = null)` -> `CompositeSyntax` :540; `.With(string name, string binding, string groups = null, string processors = null)` :1775
- Vector2Composite parts are `Up/Down/Left/Right`, `mode` = `DigitalNormalized(0, default) | Digital(1) | Analog(2)` - Actions/Composites/Vector2Composite.cs:27,161-182
- Processor names: `scaleVector2`, `invertVector2`, `normalizeVector2`, `stickDeadzone` (Controls/Processors/*.cs)
- `Keyboard.current` (Devices/Keyboard.cs:2389), indexer `KeyControl this[Key key]` :2368, `wKey` :1546, `escapeKey` :1741, `f11Key` :2107, `leftAltKey` :1655, `altKey` :2347, `anyKey` :1296, `spaceKey` :1302, `enterKey` :1311
- `Mouse.current` (Devices/Mouse.cs:258), `leftButton` :217, `rightButton` :227, `scroll` (DeltaControl) :212; `Pointer.delta` (DeltaControl) Devices/Pointer.cs:129
- `Gamepad.current` (Devices/Gamepad.cs:627), `leftStick`/`rightStick` (StickControl) :514/:520, `buttonSouth` :446, `startButton` :475
- `ButtonControl.isPressed` :198, `wasPressedThisFrame` :282, `wasReleasedThisFrame` :327 (Controls/ButtonControl.cs)
- Delta semantics: pointer deltas **auto-reset to (0,0) between frames and accumulate** multiple events within a frame (Pointer.cs:103-123). So `Mouse.current.delta.ReadValue()` is pixels **this frame**. Do **not** multiply mouse look by `Time.deltaTime`; do multiply stick look by it.
- `InputSystem.settings` has a public get/set (throws on null) - InputSystem.cs:2857. `InputSystem.actions` (project-wide) :3042.
- `InputSettings.backgroundBehavior`: `ResetAndDisableNonBackgroundDevices = 0` (default) | `ResetAndDisableAllDevices = 1` | `IgnoreFocus = 2` - InputSettings.cs:916-952. **Only used when `Application.runInBackground` is true.** Development players implicitly turn runInBackground on at build time (InputSettings.cs:470-481). `updateMode` default `ProcessEventsInDynamicUpdate` (:774,846). `editorInputBehaviorInPlayMode` :518.

### 2.2 activeInputHandler - VERIFIED-SOURCE InputSystem/Editor/Settings/EditorPlayerSettingHelpers.cs:71-78,117-150
- Values: `0 = OldInputManager`, `1 = NewInputSystem`, `2 = InputBoth`.
- **No public PlayerSettings API** (only internal `PlayerSettings.GetDisableOldInputManagerSupport()`; reflection on UnityEditor.CoreModule.dll). The package itself does `new SerializedObject(playerSettings).FindProperty("activeInputHandler")`. In 6000.x it gets the PlayerSettings object via the private static `BuildProfile.s_GlobalPlayerSettings` (or the active profile's `m_PlayerSettings`) to avoid a reimport loop.
- Simple form for a build script (UNVERIFIED that it avoids that loop; it is not needed here since the value is already 1):
  `var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]); so.FindProperty("activeInputHandler").intValue = 1; so.ApplyModifiedProperties();`
- Changing it needs an **Editor restart** (native backends and `ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER` defines). UNVERIFIED (standard behaviour).
- With value 1, any `UnityEngine.Input.*` call throws `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package in Player Settings.` VERIFIED-SOURCE (string in Win64 UnityPlayer.dll). Search the code for `Input.Get` and `Input.mousePosition` before building.

### 2.3 Skeleton
```csharp
using UnityEngine; using UnityEngine.InputSystem;
InputAction _move, _look, _toggleFs;
void Awake() {
    _move = new InputAction("Move", InputActionType.Value);
    _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
    _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
    _move.AddBinding("<Gamepad>/leftStick");
    _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");     // pixels/frame
    _look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=150,y=150)"); // deg/s, multiply by dt
    _toggleFs = new InputAction("ToggleFS", InputActionType.Button, "<Keyboard>/f11");
}
void OnEnable()  { _move.Enable(); _look.Enable(); _toggleFs.Enable(); }
void OnDisable() { _move.Disable(); _look.Disable(); _toggleFs.Disable(); }
void OnDestroy() { _move.Dispose(); _look.Dispose(); _toggleFs.Dispose(); }
// Update: var m = _move.ReadValue<Vector2>(); var kb = Keyboard.current; if (kb != null && kb.escapeKey.wasPressedThisFrame) {...}
```
- Separate mouse and stick look: `Mouse.current?.delta.ReadValue()` (no dt) vs `Gamepad.current?.rightStick.ReadValue() * dt`. Null-check `*.current` (no device gives null).
- Cursor (VERIFIED-SOURCE reflection): `Cursor.lockState = CursorLockMode.Locked` (None = 0, Locked = 1, Confined = 2); `Cursor.visible = false`. Re-apply in `OnApplicationFocus(true)` (UNVERIFIED but cheap). In the Editor, Esc releases the lock.
- Tests: `Unity.InputSystem.TestFramework` (InputTestFixture: `Press/Release/PressAndRelease(ButtonControl ...)`, `Set<T>(InputControl<T>, T)`, `Move(...)`) is compiled because it has `defineConstraints: UNITY_TESTS_FRAMEWORK` from `com.unity.test-framework`, but it is `autoReferenced: false`. Add `"Unity.InputSystem.TestFramework"` to the test asmdef references. VERIFIED-SOURCE Tests/TestFixture/Unity.InputSystem.TestFramework.asmdef, InputTestFixture.cs:79,482-616.

---

## 3. Fullscreen at runtime (Windows player)
- `FullScreenMode`: `ExclusiveFullScreen = 0`, `FullScreenWindow = 1`, `MaximizedWindow = 2`, `Windowed = 3`. VERIFIED-SOURCE (reflection). ExclusiveFullScreen is Windows-only; FullScreenWindow uses "the full-screen native display resolution". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/fullscreenmode
- `Screen.fullScreenMode {get;set;}`, `Screen.fullScreen {get;set;}`, `Screen.SetResolution(int,int,FullScreenMode)`, `(int,int,FullScreenMode,RefreshRate)`, `(int,int,bool)`, `Screen.currentResolution`, `Screen.mainWindowDisplayInfo`. VERIFIED-SOURCE. "A resolution change is applied at the end of the current frame and not immediately." VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/setresolution
- Toggle skeleton (UNVERIFIED):
```csharp
void ToggleFullscreen() {
    if (Screen.fullScreenMode == FullScreenMode.Windowed) {
        var r = Screen.currentResolution;                 // desktop res
        Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
    } else Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
}
```
- Alt+Enter: `PlayerSettings.allowFullscreenSwitch` = "allow users to switch between full-screen and windowed mode using OS-specific keyboard shortcuts". On Windows that shortcut is Alt+Enter, handled natively by the player. VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/playersettings/allowfullscreenswitch (the docs do not name Alt+Enter; that part is UNVERIFIED). It is already 1 in this project. **Don't also bind Alt+Enter in game code** (double toggle). Use F11 for a scripted toggle.

---

## 4. Editor build automation (6000.6)

### 4.1 BuildPipeline - VERIFIED-SOURCE (reflection, UnityEditor.CoreModule.dll) + VERIFIED-DOCS
- Overloads: `BuildReport BuildPlayer(BuildPlayerOptions)`, `BuildPlayer(BuildPlayerWithProfileOptions)`, plus legacy `(string[]/EditorBuildSettingsScene[], string, BuildTarget, BuildOptions)`.
- `BuildPlayerOptions` props: `scenes (string[])`, `locationPathName`, `target`, `targetGroup`, `subtarget (int)`, `options (BuildOptions)`, `extraScriptingDefines`, `assetBundleManifestPath`, `previousBuildReportDirectories`.
- Docs: "When working with BuildProfile, use the overload ... BuildPlayerWithProfileOptions". `BuildPlayer(BuildPlayerOptions)` is still the documented non-profile path, and the docs example uses it. Calling it "will invalidate any variables in the editor script that reference GameObjects". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/buildpipeline/buildplayer
- `BuildPlayerWithProfileOptions { BuildProfile buildProfile; string locationPathName; string assetBundleManifestPath; BuildOptions options; }`. `UnityEditor.Build.Profile.BuildProfile`: `GetActiveBuildProfile()`, `SetActiveBuildProfile(BuildProfile)`, `scenes`, `overrideGlobalScenes`, `scriptingDefines`, `GetScenesForBuild()`. This project has no profiles, so do not create one.
- `BuildOptions` flags of interest: `None = 0`, `Development = 1`, `AutoRunPlayer = 4`, `ShowBuiltPlayer = 8`, `CleanBuildCache = 128`, `AllowDebugging = 512`, `StrictMode = 2097152`, `DetailedBuildReport = 536870912`, `CompressWithLz4 = 262144`.
- `EditorBuildSettings.scenes {get;set;}` (`EditorBuildSettingsScene(string path, bool enabled)`), `EditorBuildSettings.AddConfigObject/TryGetConfigObject`.
- `ScriptingImplementation`: `Mono2x = 0`, `IL2CPP = 1`, `WinRTDotNET = 2`, **`CoreCLR = 3`** (new enum value; do not use it, it is not installed). `PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x)`. The `BuildTargetGroup` overloads are **[Obsolete]**.

### 4.2 PlayerSettings (static props, all get/set unless noted) - VERIFIED-SOURCE (reflection)
`companyName`, `productName`, `colorSpace (ColorSpace)`, `defaultScreenWidth`, `defaultScreenHeight`, `defaultIsNativeResolution`, `fullScreenMode (FullScreenMode)`, `resizableWindow`, `runInBackground`, `visibleInBackground`, `allowFullscreenSwitch`, `forceSingleInstance`, `useFlipModelSwapchain`, `captureSingleScreen`, `usePlayerLog`, `gpuSkinning`, `graphicsJobs`, `stripEngineCode`, `bundleVersion`, `enableFrameTimingStats`, `PlayerSettings.SplashScreen.show / showUnityLogo / unityLogoStyle`, `GetUseDefaultGraphicsAPIs(BuildTarget)`, `SetUseDefaultGraphicsAPIs(BuildTarget, bool)`, `SetGraphicsAPIs(BuildTarget, GraphicsDeviceType[])`, `SetManagedStrippingLevel(NamedBuildTarget, ManagedStrippingLevel)`, `SetApiCompatibilityLevel(NamedBuildTarget, ApiCompatibilityLevel)`, `SetStackTraceLogType(LogType, StackTraceLogType)`. `showUnitySplashScreen` still exists but is the obsolete form; use `SplashScreen.show`.
- Splash: Unity's support article says Personal-plan users **on Unity 6** can remove the splash screen; the restriction applies to versions before Unity 6. VERIFIED-DOCS https://support.unity.com/hc/en-us/articles/212165803 (as summarized by a search result; the page itself was not opened). If the license still forces it, the build simply shows the splash. Log it and do not fail.
- GraphicsDeviceType values: Direct3D11 = 2, Direct3D12 = 18, Vulkan = 21. VERIFIED-SOURCE.

### 4.3 Scenes in batchmode - VERIFIED-SOURCE (reflection)
`EditorSceneManager.NewScene(NewSceneSetup setup, NewSceneMode mode)` (`EmptyScene = 0`, `DefaultGameObjects = 1`; `Single = 0`, `Additive = 1`), `SaveScene(Scene, string dstScenePath)` / `SaveScene(Scene, string, bool saveAsCopy)`, `OpenScene(string, OpenSceneMode)`, `MarkSceneDirty(Scene)`, `SaveOpenScenes()`. Create the folder first (`Directory.CreateDirectory` + `AssetDatabase.Refresh()`). Batchmode suppresses Save Scene dialogs. VERIFIED-DOCS (command-line page).

### 4.4 Always Included Shaders / preloaded SVC via SerializedObject
- `GraphicsSettings.GetGraphicsSettings()` returns `UnityEngine.Object`. VERIFIED-SOURCE. Array property names are `m_AlwaysIncludedShaders` and `m_PreloadedShaders`. VERIFIED-SOURCE ProjectSettings/GraphicsSettings.asset:29,37.
```csharp
static void AlwaysInclude(params string[] names) {
    var so = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
    var arr = so.FindProperty("m_AlwaysIncludedShaders");
    foreach (var n in names) {
        var sh = Shader.Find(n); if (sh == null) { Debug.LogError("Missing shader " + n); continue; }
        bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
        if (!has) { arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh; }
    }
    so.ApplyModifiedProperties(); AssetDatabase.SaveAssets();
}
// AlwaysInclude("Hidden/Internal-UIRDefault","Hidden/Internal-UIRAtlasBlitCopy","Hidden/TextCore/Distance Field SSD",
//   "Hidden/TextCore/Sprite","Hidden/Internal-GUITextureClipText","Hidden/Internal-UIE-ColorConversionBlit",
//   "Ring/<every custom shader used only through Shader.Find>");
```
- **Project pitfall:** any custom HLSL shader that is used only via `new Material(Shader.Find("..."))` at runtime is **absent in the player** unless it is Always Included, referenced by a material asset or object in a build scene, or placed in `Resources/`. Keywords toggled at runtime need `multi_compile` (or `multi_compile_local`). `shader_feature` variants unused by any built material are stripped. URP also strips variants based on URP asset features. UNVERIFIED (standard Unity behaviour), so test in the player.
- `ShaderVariantCollection`: `new ShaderVariantCollection()`, `Add(new ShaderVariantCollection.ShaderVariant(Shader, PassType, params string[] keywords))`, `WarmUp()`, `WarmUpProgressively(int)`, `isWarmedUp`, `variantCount`. Save with `AssetDatabase.CreateAsset(svc, "Assets/Ring/Generated/Ring.shadervariants")`. VERIFIED-SOURCE. `PassType.ScriptableRenderPipeline = 13` (URP LightMode passes such as UniversalForward), `ScriptableRenderPipelineDefaultUnlit = 14` (no LightMode tag), `ShadowCaster = 8`. VERIFIED-SOURCE enum. The pass mapping is UNVERIFIED. The ShaderVariant ctor throws if the variant does not exist, so wrap it in try/catch.
- WarmUp: "fully supported on DX11 and OpenGL. On DX12, Vulkan, and Metal, the graphics driver might still need to perform work if the vertex layout and/or the render target setup is different". VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/shadervariantcollection/warmup. With D3D11 first in the list (project default), SVC.WarmUp is meaningful. For D3D12 use `UnityEngine.Rendering.GraphicsStateCollection` (`BeginTrace/EndTrace/SaveToFile/LoadFromFile/WarmUp(JobHandle)`), which exists in 6000.6. VERIFIED-SOURCE (reflection). The cheapest real warm-up is still rendering every material once behind a black frame. UNVERIFIED.

### 4.5 Build method skeleton (UNVERIFIED as a whole; APIs verified above)
```csharp
public static class RingBuild {
    public static void BuildWindows() {
        try {
            PlayerSettings.companyName = "Megastructures"; PlayerSettings.productName = "The Ring";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true; PlayerSettings.visibleInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            var scenes = new[] { "Assets/Ring/Scenes/Ring.unity" };
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenes[0], true) };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = "Builds/Win64/TheRing.exe",
                target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None });
            var s = report.summary;
            Debug.Log($"[RingBuild] {s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} time={s.totalTime}");
            EditorApplication.Exit(s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 2);
        } catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
```

### 4.6 Command lines - VERIFIED-DOCS https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor
- Build: `"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "<proj>" -buildTarget win64 -executeMethod RingBuild.BuildWindows -logFile "<proj>\Logs\build.log"`
  - An exception in executeMethod makes Unity exit with code **1**. Otherwise call `EditorApplication.Exit(n)`. The script must sit in an Editor folder or Editor asmdef, and the method must be static.
  - "You can't open a project in batch mode while the Editor has the same project open."
  - `-buildTarget` documented values: `win64`, `win`, ... (lowercase). It avoids a platform switch when the project has not been imported yet.
  - `-nographics`: no GPU device. Fine for a pure build, but **not** for anything that renders or bakes in the editor (captures, GI). Logs only go to the file given by `-logFile`.
  - `-quit` with async code "can cause the application to hang". `-quitTimeout` defaults to 300 s.
- Tests (test-framework 1.8.0): `Unity.exe -batchmode -projectPath "<proj>" -runTests -testPlatform EditMode -testResults "<proj>\Logs\editmode.xml" -logFile "<proj>\Logs\editmode.log"`, then the same with `-testPlatform PlayMode`, or a BuildTarget name such as `StandaloneWindows64` for player tests.
  - **Do NOT pass `-quit` with `-runTests`.** The runner exits by itself through `EditorApplication.Exit`. With `-quit` it logs `Running tests from command line arguments will not work when "quit" is specified.` VERIFIED-SOURCE com.unity.test-framework@7a3849e09bd0/UnityEditor.TestRunner/CommandLineTest/SettingsBuilder.cs:42,115-120; TestStarter.cs:89. Docs: "-quit causes the Editor to quit immediately, before in-progress tests have chance to complete". VERIFIED-DOCS (command-line page).
  - Other options: `-testFilter`, `-testCategory`, `-assemblyNames`, `-testSettingsFile`, `-runSynchronously` (EditMode only), `-playerHeartbeatTimeout`, `-buildPlayerPath`, `-retry`, `-repeat`, `-randomOrderSeed`, `-orderedTestListFile`; results via `-testResults` (alias `-editorTestsResultFile`). Default platform is EditMode. SettingsBuilder.cs:41-57,101-105.
  - Exit codes: `0` ok (also when no tests ran), `2` tests failed, `3` run error (also script compile errors), `4` platform not found. VERIFIED-SOURCE CommandLineTest/Executer.cs:91-97,159-165.
  - PlayMode tests that render need graphics, so no `-nographics`. UNVERIFIED.

---

## 5. Top pitfalls checklist (6000.6-specific)
1. UIDocument is "legacy/obsolete" in the docs but not `[Obsolete]` in code. Keep it **enabled**, because enable/disable wipes code-built UI. PanelRenderer is the alternative (callback-based; its `rootVisualElement` is internal).
2. PanelSettings created at runtime in the player risks null UI shaders (`Shader.Find` misses editor-only built-in shaders) and logs the no-theme warning. Create it as an **editor asset** with the generated `.tss` theme and also Always-Include the UIR/TextCore shaders.
3. The runtime default `PanelTextSettings` has no font. Without a theme font or an explicit `unityFontDefinition`, text renders blank.
4. `letterSpacing` takes px only (no em). `sortingOrder` is float. `referenceResolution` is Vector2Int.
5. HUD overlay is drawn after URP post-processing.
6. Only the Mono player is installed, so IL2CPP/CoreCLR builds fail.
7. Legacy `UnityEngine.Input` throws in this project (activeInputHandler = 1).
8. Mouse delta is per-frame pixels (no deltaTime). Gamepad stick needs deltaTime.
9. `allowFullscreenSwitch = 1` already gives Alt+Enter, so don't double-bind it.
10. `-runTests` + `-quit` is a mistake. Use exit codes 0/2/3/4.
11. Runtime `Shader.Find` materials and `shader_feature` keywords are a classic "pink/invisible only in the player" trap.

## Sources (all accessed 2026-10-29)
- `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.UIElementsModule.dll` + `.xml`: PanelSettings/UIDocument/PanelRenderer/FontDefinition/IStyle/Length members, IL of InitializeShaders/Reset/OnEnable/ApplyThemeStyleSheet/ctor, UIR.Shaders constants, warning strings.
- `...\Managed\UnityEngine\UnityEditor.UIElementsModule.dll`: PanelSettingsCreator / ThemeUtility (default .tss path and content).
- `...\Managed\UnityEngine\UnityEngine.TextCoreTextEngineModule.dll`: TextSettings, FontAsset.CreateFontAsset overloads + IL, TextShaderUtilities shader names.
- `...\Managed\UnityEngine\UnityEngine.TextRenderingModule.dll`: Font API.
- `...\Managed\UnityEngine\UnityEngine.CoreModule.dll`: Screen, FullScreenMode, Cursor, GraphicsDeviceType, GraphicsSettings, ShaderVariantCollection, PassType, GraphicsStateCollection.
- `...\Managed\UnityEngine\UnityEditor.CoreModule.dll`: BuildPipeline, BuildPlayerOptions, BuildOptions, BuildPlayerWithProfileOptions, BuildProfile, EditorBuildSettings, PlayerSettings (+SplashScreen), NamedBuildTarget, ScriptingImplementation, EditorSceneManager.
- `...\Editor\Data\Resources\unity_builtin_extra`, `...\Resources\unity editor resources`, `...\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_nondevelopment_mono\Data\Resources\unity default resources`, `...\UnityPlayer.dll`: where UIR/TextCore shaders, the Inter font and LegacyRuntime live; legacy Input exception text.
- `Library\PackageCache\com.unity.inputsystem@7a4e1a2a8194\InputSystem\Runtime\...` (InputAction.cs, InputActionSetupExtensions.cs, Composites/Vector2Composite.cs, Devices/*.cs, Controls/ButtonControl.cs, InputSettings.cs, InputSystem.cs), `...\InputSystem\Editor\Settings\EditorPlayerSettingHelpers.cs`, `...\Tests\TestFixture\*`: Input System API, activeInputHandler values, test fixture.
- `Library\PackageCache\com.unity.test-framework@7a3849e09bd0\UnityEditor.TestRunner\CommandLineTest\{SettingsBuilder,Executer,TestStarter}.cs`: CLI options, -quit warning, exit codes.
- `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184\Runtime\UniversalRenderer.cs:421-422`, `Runtime\Passes\DrawScreenSpaceUIPass.cs`: overlay UI after the final blit.
- Project files: `ProjectSettings\ProjectSettings.asset`, `GraphicsSettings.asset`, `EditorBuildSettings.asset`, `Packages\manifest.json`: current settings.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/uielements/panelsettings: member list cross-check.
- https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-get-started-with-runtime-ui: UIDocument OnEnable lifecycle.
- https://docs.unity.cn/6000.6/Documentation/Manual/UIE-create-ui-document-component.html: UIDocument obsolete -> PanelRenderer, clear-on-enable behaviour.
- https://discussions.unity.com/t/does-uidocument-clear-contents-when-disabled/837983: "by design" clearing (community/staff).
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/playersettings/allowfullscreenswitch: Alt+Enter setting.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/fullscreenmode, .../screen/setresolution, .../unityengine/fullscreenmode: fullscreen semantics, end-of-frame apply.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/buildpipeline/buildplayer: BuildPlayerOptions vs build profiles.
- https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/shadervariantcollection/warmup: DX12/Vulkan warm-up caveat.
- https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor: batchmode/quit/executeMethod/buildTarget/nographics/exit codes, project-lock note.
- https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html: -runTests/-testPlatform/-testResults.
- https://support.unity.com/hc/en-us/articles/212165803: Unity 6 Personal can disable the splash (via search summary).
- https://forum.needle.tools/t/unity-default-text-in-worldspace-canvas-doesnt-export-anymore-with-2022-2-unity-versions/323: LegacyRuntime font context (community).
