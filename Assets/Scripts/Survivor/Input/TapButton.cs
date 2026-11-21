using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Survivor
{
    public sealed class TapButton : MonoBehaviour, IPointerClickHandler
    {
        public Action OnPressed;

        public void OnPointerClick(PointerEventData eventData) => OnPressed?.Invoke();
    }
}
