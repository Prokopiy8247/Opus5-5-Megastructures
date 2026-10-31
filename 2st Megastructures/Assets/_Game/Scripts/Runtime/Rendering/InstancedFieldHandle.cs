using UnityEngine;

namespace Megastructure
{
    /// <summary>Lifetime owner of one <see cref="InstancedField"/> created through InstancedField.Create: lives under
    /// the field's set root and releases the field's GPU buffers when it is destroyed (scene unload, world rebuild).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class InstancedFieldHandle : MonoBehaviour
    {
        [System.NonSerialized] public InstancedField Field;

        void OnDestroy()
        {
            if (Field != null) Field.Release();
            Field = null;
        }
    }
}
