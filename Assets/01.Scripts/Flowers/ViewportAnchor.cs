using CONFUSEDGAMEDEV.PollenGarden.Platform;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Pins this transform to a fixed viewport position, resolution-independent: bottom-right
    /// while the overlay is ambient, left of the screen while the expanded menu is open.
    /// Re-applied every frame so display or window size changes just work; the cost is one
    /// ViewportToWorldPoint.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Viewport Anchor")]
    public sealed class ViewportAnchor : MonoBehaviour
    {
        [Tooltip("Viewport position while in overlay mode: (0,0) bottom-left … (1,1) top-right.")]
        [SerializeField]
        private Vector2 viewportPosition = new Vector2(0.85f, 0.2f);

        [Tooltip("Viewport position while the expanded menu is open (flower moves aside).")]
        [SerializeField]
        private Vector2 expandedViewportPosition = new Vector2(0.27f, 0.5f);

        [Tooltip("Which mode is active. Unset (or in Edit mode) the overlay position is used.")]
        [SerializeField]
        private WindowModeManager windowMode;

        [Tooltip("Anchoring camera. Falls back to Camera.main.")]
        [SerializeField]
        private Camera anchorCamera;

        public Vector2 ViewportPosition
        {
            get => viewportPosition;
            set => viewportPosition = value;
        }

        public Vector2 ExpandedViewportPosition
        {
            get => expandedViewportPosition;
            set => expandedViewportPosition = value;
        }

        public WindowModeManager WindowMode
        {
            get => windowMode;
            set => windowMode = value;
        }

        private void LateUpdate()
        {
            Camera cam = anchorCamera != null ? anchorCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            bool expanded = Application.isPlaying && windowMode != null && !windowMode.IsOverlayActive;
            Vector2 target = expanded ? expandedViewportPosition : viewportPosition;

            // Keep the object on its current camera-space depth plane; only x/y follow the anchor.
            float depth = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
            transform.position = cam.ViewportToWorldPoint(new Vector3(target.x, target.y, depth));
        }
    }
}
