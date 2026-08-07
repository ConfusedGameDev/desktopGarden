using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Pins this transform to a fixed viewport position (e.g. bottom-right corner for the tended
    /// flower), resolution-independent. Re-applied every frame so display or window size changes
    /// just work; the cost is one ViewportToWorldPoint.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Viewport Anchor")]
    public sealed class ViewportAnchor : MonoBehaviour
    {
        [Tooltip("Viewport position: (0,0) bottom-left … (1,1) top-right.")]
        [SerializeField]
        private Vector2 viewportPosition = new Vector2(0.85f, 0.2f);

        [Tooltip("Anchoring camera. Falls back to Camera.main.")]
        [SerializeField]
        private Camera anchorCamera;

        public Vector2 ViewportPosition
        {
            get => viewportPosition;
            set => viewportPosition = value;
        }

        private void LateUpdate()
        {
            Camera cam = anchorCamera != null ? anchorCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            // Keep the object on its current camera-space depth plane; only x/y follow the anchor.
            float depth = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
            transform.position = cam.ViewportToWorldPoint(
                new Vector3(viewportPosition.x, viewportPosition.y, depth));
        }
    }
}
