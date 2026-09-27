using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Why.UI
{
    /// <summary>Reports pointer enter / exit of a UI element (added at runtime, e.g. to preset buttons).</summary>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Invoked with true on enter, false on exit or when the element is hidden.</summary>
        public Action<bool> Changed;

        public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);

        void OnDisable() => Changed?.Invoke(false);
    }
}
