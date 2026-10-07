using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace TFY.CatAdventure.Gameplay
{
    public enum GameInput { Tap, SwipeUp, SwipeDown, SwipeLeft, SwipeRight }

    /// <summary>
    /// Child-friendly controls: TAP = jump, a second tap in the air = double jump, SWIPE UP = long jump,
    /// SWIPE LEFT/RIGHT = change lane, SWIPE DOWN = slide. A swipe fires as soon as the finger has moved far enough
    /// (no need to lift), with a generous threshold; taps on UI buttons are ignored. Keyboard and mouse work in the editor.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public event Action<GameInput> OnInput;
        public float swipeThresholdDp = 26f, tapMaxSeconds = 0.28f;

        private Vector2 start, mouseStart; private double startTime; private float mouseStartTime;
        private bool tracking, consumed, mouseTracking, mouseConsumed;
        private float ThresholdPx => swipeThresholdDp * (Screen.dpi > 0 ? Screen.dpi / 160f : 1f);

        private void OnEnable() { EnhancedTouchSupport.Enable(); }
        private void OnDisable() { EnhancedTouchSupport.Disable(); }

        private void Update()
        {
            foreach (var t in Touch.activeTouches)
            {
                if (t.finger.index != 0) continue;
                switch (t.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        start = t.screenPosition; startTime = t.startTime; consumed = false; tracking = !OverUI(start); break;
                    case UnityEngine.InputSystem.TouchPhase.Moved:
                        if (tracking && !consumed && (t.screenPosition - start).magnitude > ThresholdPx) { consumed = true; Fire(Direction(t.screenPosition - start)); }
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                        if (tracking && !consumed && t.time - startTime < tapMaxSeconds * 1.6f) Fire(GameInput.Tap);
                        tracking = false; break;
                    case UnityEngine.InputSystem.TouchPhase.Canceled: tracking = false; break;
                }
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Fire(GameInput.Tap);
                if (kb.eKey.wasPressedThisFrame) Fire(GameInput.SwipeUp);
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Fire(GameInput.SwipeDown);
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Fire(GameInput.SwipeLeft);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Fire(GameInput.SwipeRight);
            }

            var mouse = Mouse.current; // editor / desktop testing: click = tap, drag = swipe
            if (mouse != null && Touch.activeTouches.Count == 0)
            {
                var pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) { mouseStart = pos; mouseStartTime = Time.unscaledTime; mouseConsumed = false; mouseTracking = !OverUI(pos); }
                if (mouseTracking && !mouseConsumed && mouse.leftButton.isPressed && (pos - mouseStart).magnitude > ThresholdPx) { mouseConsumed = true; Fire(Direction(pos - mouseStart)); }
                if (mouse.leftButton.wasReleasedThisFrame) { if (mouseTracking && !mouseConsumed && Time.unscaledTime - mouseStartTime < tapMaxSeconds * 1.6f) Fire(GameInput.Tap); mouseTracking = false; }
            }
        }

        private static GameInput Direction(Vector2 d) => Mathf.Abs(d.x) > Mathf.Abs(d.y)
            ? (d.x > 0 ? GameInput.SwipeRight : GameInput.SwipeLeft)
            : (d.y > 0 ? GameInput.SwipeUp : GameInput.SwipeDown);

        private static bool OverUI(Vector2 screen)
        {
            var es = EventSystem.current; if (!es) return false;
            var data = new PointerEventData(es) { position = screen };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()) return true;
            return false;
        }

        /// <summary>Also used by the optional on-screen buttons.</summary>
        public void Fire(GameInput i) => OnInput?.Invoke(i);
    }
}
