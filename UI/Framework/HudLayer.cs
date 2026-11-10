using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class HudLayer
    {
        readonly List<VisualElement> elements = new();
        VisualElement layer;

        public void AttachLayer(VisualElement layerElement)
        {
            layer = layerElement;
            // a live reload throws the old layer away; carry the HUD over to the new one
            foreach (var element in elements) layer.Add(element);
        }

        // safe before the layer exists; the element is parented when the panel first loads
        public void Add(VisualElement element)
        {
            elements.Add(element);
            layer?.Add(element);
        }

        public void Remove(VisualElement element)
        {
            elements.Remove(element);
            element.RemoveFromHierarchy();
        }
    }
}
