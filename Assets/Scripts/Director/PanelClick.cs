using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Why.Director
{
    /// <summary>Forwards pointer clicks on a UI element (the narration panel completes its typewriter).</summary>
    public sealed class PanelClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
