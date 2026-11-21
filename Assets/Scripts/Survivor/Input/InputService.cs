using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor
{
    // Movement from keyboard (WASD / arrows) or the on-screen joystick, whichever is stronger.
    public sealed class InputService
    {
        public Vector2 Joystick { get; set; }

        public Vector2 Move
        {
            get
            {
                var keys = KeyboardMove();
                return Joystick.sqrMagnitude > keys.sqrMagnitude ? Joystick : keys;
            }
        }

        // 0, 1, 2 when the player presses 1/2/3 this frame, otherwise -1.
        public int UpgradeKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
            return -1;
        }

        public bool WasPressed(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }

        static Vector2 KeyboardMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var v = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            return v.normalized;
        }
    }
}
