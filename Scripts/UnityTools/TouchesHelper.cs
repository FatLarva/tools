#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.UI;

namespace UnityTools
{
    public static class TouchesHelper
    {
        public static bool TryGetPointerId(ExtendedPointerEventData exEventData, int mousePointerId, out int pointerId)
        {
            /*if (exEventData.pointerType == UIPointerType.MouseOrPen)
            {
                pointerId = mousePointerId;

                return true;
            }*/

            var finger = GetFingerByTouchId(exEventData.touchId);
            if (finger != null)
            {
                pointerId = finger.index;
				
                return true;
            }

            pointerId = default;

            return false;
        }

        public static bool TryGetTouchByFingerId(int fingerId, out Touch outputTouch)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.finger == null)
                {
                    continue;
                }
                
                if (touch.finger.index == fingerId)
                {
                    outputTouch = touch;

                    return true;
                }
            }

            outputTouch = default;

            return false;
        }

        public static Finger GetFingerByFingerId(int fingerId)
        {
            foreach (var finger in Touch.activeFingers)
            {
                if (finger == null)
                {
                    continue;
                }
                
                if (finger.index == fingerId)
                {
                    return finger;
                }
            }

            return default;
        }

        public static bool IsThereAnyTouches(bool treatMouseAsTouch)
        {
            if (Touch.activeTouches.Count > 0)
            {
                return true;
            }

            /*if (!treatMouseAsTouch)
            {
                return false;
            }

            if (Mouse.current is { leftButton: { isPressed: true } })
            {
                return true;
            }*/

            return false;
        }

        private static Finger GetFingerByTouchId(int touchId)
        {
            var touch = GetTouchByTouchId(touchId);
            if (touch.valid)
            {
                return touch.finger;
            }

            return null;
        }

        private static Touch GetTouchByTouchId(int touchId)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.touchId == touchId)
                {
                    return touch;
                }
            }

            return default;
        }
    }
}
#endif
