using UnityEngine;

namespace Saga.Rendering
{
    // colours this object's row in the Hierarchy; draws nothing in the Scene view, so folder objects don't clutter the origin
    [DisallowMultipleComponent]
    [AddComponentMenu("Saga/Hierarchy Folder")]
    public class HierarchyFolder : MonoBehaviour
    {
        // opaque, since the row is painted over Unity's own label and that would show through
        public Color color = new Color(0.25f, 0.45f, 0.8f);
    }
}
