using CONFUSEDGAMEDEV.PollenGarden.Platform;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Turns pointer presses into petal damage via a physics raycast against the petal colliders,
    /// and gives the flower's centre button two gestures: a quick click opens the menu, while
    /// holding (or pulling past a few pixels) drags the whole flower to a new spot on screen.
    /// The same path serves mouse today and touch/XR rays later, which is why this polls
    /// <see cref="Pointer"/> rather than mouse-specific controls.
    /// </summary>
    /// <remarks>
    /// In overlay mode a press only ever reaches Unity when the cursor is inside an interactive
    /// rect (see ClickThroughManager) — by construction that means it is over a petal's bounds,
    /// so the raycast here is the precise hit test, not a redundant one. While a drag is live the
    /// whole screen is published as interactive: a fast mouse would otherwise outrun the flower's
    /// own rects, flip the window to click-through mid-drag, and the mouse events would vanish.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Petal Click Input")]
    public sealed class PetalClickInput : MonoBehaviour
    {
        /// <summary>Keeps a dragged flower from being parked (and lost) off the screen edge.</summary>
        private const float DragClampMargin = 0.05f;

        [Tooltip("Camera the pointer position is interpreted through. Falls back to Camera.main.")]
        [SerializeField]
        private Camera raycastCamera;

        [Tooltip("Hit points removed per click. Powerups and helpers scale damage elsewhere.")]
        [SerializeField, Min(1)]
        private int damagePerClick = 1;

        [SerializeField, Min(1f)]
        private float maxRayDistance = 100f;

        [Header("Centre button gestures")]
        [Tooltip("Holding the centre longer than this starts a drag instead of a click.")]
        [SerializeField, Min(0.05f)]
        private float holdToDragSeconds = 0.25f;

        [Tooltip("Pulling the cursor this many pixels while pressed also starts a drag.")]
        [SerializeField, Min(1f)]
        private float dragStartPixels = 8f;

        private FlowerCenterButton pressedCenter;
        private Vector2 pressPosition;
        private float pressStartTime;
        private bool dragging;
        private ViewportAnchor draggedAnchor;
        private Vector2 dragGrabOffsetViewport;

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 cursor = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                HandlePress(cursor);
            }
            else if (pointer.press.isPressed)
            {
                HandleHold(cursor);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                HandleRelease();
            }
        }

        private void HandlePress(Vector2 cursor)
        {
            // uGUI gets first claim: a click on the expanded menu must not also harvest whatever
            // petal happens to sit behind the panel.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(cursor);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
            {
                return;
            }

            if (hit.collider.TryGetComponent(out PetalController petal))
            {
                petal.ApplyDamage(damagePerClick);
            }
            else if (hit.collider.TryGetComponent(out FlowerCenterButton centerButton))
            {
                // Decided on release: a short, still press is a click; held or pulled is a drag.
                pressedCenter = centerButton;
                pressPosition = cursor;
                pressStartTime = Time.unscaledTime;
            }
        }

        private void HandleHold(Vector2 cursor)
        {
            if (pressedCenter == null)
            {
                return;
            }

            if (!dragging
                && (Time.unscaledTime - pressStartTime >= holdToDragSeconds
                    || (cursor - pressPosition).magnitude >= dragStartPixels))
            {
                StartDrag(cursor);
            }

            if (dragging)
            {
                UpdateDrag(cursor);
            }
        }

        private void HandleRelease()
        {
            if (pressedCenter != null && !dragging)
            {
                pressedCenter.Click();
            }

            pressedCenter = null;
            dragging = false;
            draggedAnchor = null;
        }

        private void StartDrag(Vector2 cursor)
        {
            draggedAnchor = pressedCenter.Flower != null
                ? pressedCenter.Flower.GetComponent<ViewportAnchor>()
                : null;
            if (draggedAnchor == null)
            {
                // No anchor to move — fall back to plain click semantics on release.
                return;
            }

            // Grab offset keeps the flower under the same point of the hand that picked it up,
            // instead of snapping its centre to the cursor.
            dragGrabOffsetViewport = ActiveAnchorPosition() - ToViewport(cursor);
            dragging = true;
        }

        private void UpdateDrag(Vector2 cursor)
        {
            Vector2 target = ToViewport(cursor) + dragGrabOffsetViewport;
            target.x = Mathf.Clamp(target.x, DragClampMargin, 1f - DragClampMargin);
            target.y = Mathf.Clamp(target.y, DragClampMargin, 1f - DragClampMargin);

            if (IsOverlayActive())
            {
                draggedAnchor.ViewportPosition = target;
            }
            else
            {
                draggedAnchor.ExpandedViewportPosition = target;
            }

            // Keep the window interactive for the whole gesture, wherever the cursor flies.
            InteractiveScreenRects.Publish(new Rect(0f, 0f, Screen.width, Screen.height));
        }

        private Vector2 ActiveAnchorPosition()
        {
            return IsOverlayActive()
                ? draggedAnchor.ViewportPosition
                : draggedAnchor.ExpandedViewportPosition;
        }

        private bool IsOverlayActive()
        {
            return draggedAnchor.WindowMode == null || draggedAnchor.WindowMode.IsOverlayActive;
        }

        private static Vector2 ToViewport(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x / Screen.width, screenPosition.y / Screen.height);
        }
    }
}
