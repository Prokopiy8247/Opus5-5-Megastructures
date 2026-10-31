// Beat00_Sky: installs the analytic sky (MASTER_PROMPT 14.2.3 M09/M10, 14.7.4, 14.7.6, 14.7.7).
// Sets the ThreadSky material (MaterialLibrary.Sky) as the scene skybox and turns Unity's own fog off, so all fog
// comes from ThreadCommon (ThreadFogApply / ThreadFogApplySky). Registers no updater: every sky uniform is computed
// in double by ProgressDirector.ApplyGlobals each frame. Discovered by GameBootstrap by reflection; the name sorts
// first so the sky exists before any beat geometry is built.
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class Beat00_Sky : IBeatBuilder
    {
        const string SkyShaderName = "Megastructure/ThreadSky";

        public string Name => "Beat00_Sky";

        public void Build(WorldContext ctx)
        {
            RenderSettings.fog = false;

            Material sky = ctx != null && ctx.Materials != null ? ctx.Materials.Sky : null;
            if (sky == null)
            {
                Debug.LogError("[THREAD-FAIL] Beat00_Sky: MaterialLibrary.Sky is not assigned; the sky cannot be drawn");
                return;
            }
            if (sky.shader == null || sky.shader.name != SkyShaderName)
                Debug.LogWarning("[THREAD] Beat00_Sky: the Sky material does not use " + SkyShaderName);

            RenderSettings.skybox = sky;

            // The skybox is drawn only for a Base camera whose background type is Skybox (URP 17.6, R2 1.5).
            if (ctx.Camera != null && ctx.Camera.clearFlags != CameraClearFlags.Skybox)
                Debug.LogWarning("[THREAD] Beat00_Sky: the main camera does not clear to the skybox; the analytic sky will not be visible");

            ctx.Log("sky: ThreadSky installed as the skybox, Unity fog off");
        }
    }
}
