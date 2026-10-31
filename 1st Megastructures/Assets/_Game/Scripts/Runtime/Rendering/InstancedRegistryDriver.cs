using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Issues every registered <see cref="InstancedBatch"/> once per frame (CONTRACTS section 3: LateUpdate, order +200, after
    /// CameraRig +100 and before CaptureHarness +300). Graphics.RenderMeshInstanced submits for the current frame only, for all
    /// cameras (RenderParams.camera = null). Lives on the Bootstrap GameObject.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class InstancedRegistryDriver : MonoBehaviour
    {
        public static InstancedRegistryDriver Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void LateUpdate()
        {
            InstancedRegistry.DrawAll();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InstancedRegistry.Clear();
        }
    }
}
