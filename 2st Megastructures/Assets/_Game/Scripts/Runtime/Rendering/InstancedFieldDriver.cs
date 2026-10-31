using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The single static driver of every <see cref="InstancedField"/>: submits all visible fields once per frame in
    /// LateUpdate (last in the frame, after every director), for all cameras, except in capture mode where the
    /// CaptureHarness submits per camera with <see cref="InstancedField.SubmitAllFor"/>. Created on demand by the
    /// first field in play mode (or placed in the bootstrap scene); its destruction releases all field buffers.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class InstancedFieldDriver : MonoBehaviour
    {
        static InstancedFieldDriver s_Instance;

        public static bool Exists => s_Instance != null;

        /// <summary>Creates the driver if it does not exist yet (play mode only; edit-mode tests submit by hand).</summary>
        public static void Ensure()
        {
            if (s_Instance != null || !Application.isPlaying) return;
            var go = new GameObject("[InstancedFieldDriver]");
            s_Instance = go.AddComponent<InstancedFieldDriver>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }
            s_Instance = this;
        }

        void LateUpdate()
        {
            if (!RingState.Capture) InstancedField.SubmitAll();
        }

        void OnDestroy()
        {
            if (s_Instance != this) return;
            s_Instance = null;
            InstancedField.ReleaseAll();
        }
    }
}
