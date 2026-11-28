using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Survivor
{
    // Sideways swipe on a UI area (the Home chapter card): Swiped(+1) = swiped left (next), -1 = swiped right.
    public sealed class SwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Pixels (screen) the finger must move sideways to count as a swipe.")]
        [SerializeField] float minDistance = 80f;

        public Action<int> Swiped;
        Vector2 start;

        public void OnBeginDrag(PointerEventData e) => start = e.position;

        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            var d = e.position - start;
            if (Mathf.Abs(d.x) < minDistance || Mathf.Abs(d.x) < Mathf.Abs(d.y)) return;
            Swiped?.Invoke(d.x < 0f ? 1 : -1);
        }
    }
}
