using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor
{
    // Game input through the Input System's project-wide actions (Assets/InputSystem_Actions.inputactions):
    //   Player/Move   - gamepad left stick, WASD, arrow keys, generic joystick
    //   UI/Navigate   - choosing an upgrade card with stick / d-pad / arrows
    //   UI/Submit     - confirm (gamepad South button, Enter)
    // Rebind or add devices by editing that asset; no code change needed.
    // The on-screen joystick (touch / mouse drag) is merged in on top.
    public sealed class InputService
    {
        readonly InputAction move;
        readonly InputAction navigate;
        readonly InputAction submit;

        public InputService()
        {
            var actions = InputSystem.actions;
            if (actions == null) return; // no project-wide actions: keyboard fallback below
            move = actions.FindAction("Player/Move");
            navigate = actions.FindAction("UI/Navigate");
            submit = actions.FindAction("UI/Submit");
            actions.Enable();
        }

        // Set by the on-screen VirtualJoystick.
        public Vector2 Joystick { get; set; }

        public Vector2 Move
        {
            get
            {
                var v = move != null 
                    ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) 
                    : KeyboardMove();
                var joystickDirection = Joystick.normalized;
                var inputDirection = v.normalized;

                return Joystick.sqrMagnitude > 0.01f
                    ? joystickDirection
                    : inputDirection;
            }
        }

        // -1 / +1 when the player pushes left / right this frame (stick, d-pad, arrow keys), otherwise 0.
        public int NavigateHorizontal()
        {
            if (navigate == null || !navigate.WasPressedThisFrame()) return 0;
            var x = navigate.ReadValue<Vector2>().x;
            return x > 0.5f ? 1 : x < -0.5f ? -1 : 0;
        }

        // +1 / -1 when the player pushes up / down this frame, otherwise 0.
        public int NavigateVertical()
        {
            if (navigate == null || !navigate.WasPressedThisFrame()) return 0;
            var y = navigate.ReadValue<Vector2>().y;
            return y > 0.5f ? 1 : y < -0.5f ? -1 : 0;
        }

        // Opens / closes the pause menu: Esc on keyboard, Start on gamepad.
        public bool PausePressed =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        // Opens / closes the debug panel: F3 on keyboard, Back / View / Select on gamepad.
        public bool DebugTogglePressed =>
            (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);

        public bool SubmitPressed => submit != null && submit.WasPressedThisFrame();

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

        // Debug / developer keys stay on the keyboard.
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
