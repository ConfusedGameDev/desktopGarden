using UnityEngine;
using UnityEngine.InputSystem;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// The per-frame half of the click-through strategy (Plan.md §3): cursor inside any published
    /// interactive rect → the window takes input; outside → every click falls through to the
    /// desktop. A few pixels of hysteresis stop the toggle from flickering on rect edges.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WindowModeManager))]
    [AddComponentMenu("Pollen Garden/Click Through Manager")]
    [DefaultExecutionOrder(1000)] // consume rects after every publisher's Update has run
    public sealed class ClickThroughManager : MonoBehaviour
    {
        [Tooltip("Extra pixels the cursor may drift outside a rect before click-through re-engages.")]
        [SerializeField, Min(0f)]
        private float hysteresisPixels = 6f;

        private WindowModeManager windowModeManager;
        private bool cursorOverInteractive;

        private void Awake()
        {
            windowModeManager = GetComponent<WindowModeManager>();
        }

        private void LateUpdate()
        {
            if (!windowModeManager.IsOverlayActive)
            {
                InteractiveScreenRects.Clear();
                return;
            }

            if (!TryGetCursor(out Vector2 cursor))
            {
                // No cursor data this frame — keep the last decision rather than guessing.
                InteractiveScreenRects.Clear();
                return;
            }

            bool inside = InteractiveScreenRects.Contains(
                cursor, cursorOverInteractive ? hysteresisPixels : 0f);
            InteractiveScreenRects.Clear();

            if (inside != cursorOverInteractive)
            {
                cursorOverInteractive = inside;
                windowModeManager.Platform.SetClickThrough(!inside);
            }
        }

        private bool TryGetCursor(out Vector2 screenPosition)
        {
            // The platform query is authoritative: once the window ignores mouse events the OS
            // stops delivering them, so Unity's own pointer state freezes at the last in-window
            // position and could never report the cursor re-entering a petal.
            if (windowModeManager.Platform.TryGetCursorScreenPosition(out screenPosition))
            {
                return true;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            return false;
        }
    }
}
