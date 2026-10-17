using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Survivors
{
    // Floating virtual joystick for touch/mouse, with WASD/arrows as a desktop fallback.
    // Works with either the legacy Input Manager or the Input System package.
    public static class InputService
    {
        public const float Radius = 90f; // screen pixels

        public static bool Dragging { get; private set; }
        public static Vector2 Origin { get; private set; }
        public static Vector2 Current { get; private set; }
        public static Vector2 Move { get; private set; }

        static bool _wasDown;

        public static void Tick()
        {
            Vector2 key = ReadKeys();
            Vector2 pos;
            bool down = ReadPointer(out pos);

            if (down)
            {
                if (!_wasDown) Origin = pos;
                Current = pos;
                Vector2 d = Current - Origin;
                if (d.magnitude > Radius)
                {
                    Origin = Current - d.normalized * Radius;
                    d = Current - Origin;
                }
                Move = d / Radius;
                Dragging = true;
            }
            else
            {
                Move = Vector2.zero;
                Dragging = false;
            }
            _wasDown = down;

            if (key.sqrMagnitude > 0.01f) Move = key.normalized;
        }

        static Vector2 ReadKeys()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
#endif
            return v;
        }

        static bool ReadPointer(out Vector2 pos)
        {
            pos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                pos = ts.primaryTouch.position.ReadValue();
                return true;
            }
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed)
            {
                pos = m.position.ReadValue();
                return true;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                pos = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButton(0))
            {
                pos = Input.mousePosition;
                return true;
            }
            return false;
#else
            return false;
#endif
        }
    }
}
