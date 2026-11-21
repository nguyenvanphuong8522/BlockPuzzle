using UnityEngine;
using UnityEngine.EventSystems;

namespace Survivor
{
    // Floating joystick: appears wherever the finger (or mouse) goes down.
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Background;
        public RectTransform Knob;
        public InputService Input;

        RectTransform area;

        void Awake() => area = (RectTransform)transform;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position,
                    eventData.pressEventCamera, out var local))
                Background.anchoredPosition = local;
            Background.gameObject.SetActive(true);
            Knob.anchoredPosition = Vector2.zero;
            Input.Joystick = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Background, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            var radius = Background.rect.width * 0.5f;
            var value = Vector2.ClampMagnitude(local / radius, 1f);
            Knob.anchoredPosition = value * radius;
            Input.Joystick = value;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void Release()
        {
            Background.gameObject.SetActive(false);
            if (Input != null) Input.Joystick = Vector2.zero;
        }

        void OnDisable() => Release();
    }
}
