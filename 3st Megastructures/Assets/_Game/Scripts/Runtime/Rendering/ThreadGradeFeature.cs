// ThreadGradeFeature: Thread's own bloom + grade + final dither as a URP 17.6 Render Graph renderer feature
// (MASTER_PROMPT 8.2, 14.2.5, 14.7.1 #2; research R1 option B). Add it to the PC renderer with
// `material` = Grade.mat (shader Megastructure/ThreadGrade; the material reference also guarantees that the
// shader ships in the player build).
//
// Camera contract (set by the editor setup, not here): URP post-processing OFF on the camera
// (UniversalAdditionalCameraData.renderPostProcessing = false), URP camera dithering off, HDR on with the
// 64-bit colour buffer (R16G16B16A16_SFloat), MSAA 4x, no FXAA/SMAA/TAA, render scale 1, a single base camera.
// Parameters come from GradeState (written by ProgressDirector each frame) and ThreadClock (dither frame).
// GradeState.Enabled = false or a missing material: the feature does nothing.
// Cameras: Game cameras only (this includes the capture harness' SubmitRenderRequest renders); the Scene
// view only when applyInSceneView is set (then without the title/ending fade); Preview, Reflection and other
// camera types are always skipped. The pass runs on the last camera of a stack (resolveFinalTarget).
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Megastructure.Rendering
{
    [DisallowMultipleRendererFeature("Thread Grade")]
    public sealed class ThreadGradeFeature : ScriptableRendererFeature
    {
        /// <summary>Grade.mat (shader Megastructure/ThreadGrade).</summary>
        public Material material;

        /// <summary>Editor convenience: also grade the Scene view (no fade; only when its post-processing toggle is off).</summary>
        public bool applyInSceneView = false;

        ThreadGradePass m_Pass;

        public override void Create()
        {
            if (m_Pass == null) m_Pass = new ThreadGradePass();
            m_Pass.Bind(material);
            // Generate the blue-noise tile up front in play mode (behind the load fade), not on the first frame.
            if (Application.isPlaying) BlueNoise.Get();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GradeState.Enabled || material == null) return;
            CameraType type = renderingData.cameraData.cameraType;
            bool allowed = type == CameraType.Game || (applyInSceneView && type == CameraType.SceneView);
            if (!allowed) return;
            if (m_Pass == null) m_Pass = new ThreadGradePass();
            if (!m_Pass.Bind(material)) return;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            // Nothing owned: the material is an asset, the blue-noise texture is a process-wide cache.
            m_Pass = null;
        }
    }
}
