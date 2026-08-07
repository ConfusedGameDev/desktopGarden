using UnityEngine;
using UnityEngine.InputSystem;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Turns pointer presses into petal damage via a physics raycast against the petal colliders.
    /// The same path serves mouse today and touch/XR rays later, which is why this polls
    /// <see cref="Pointer"/> rather than mouse-specific controls.
    /// </summary>
    /// <remarks>
    /// In overlay mode a press only ever reaches Unity when the cursor is inside an interactive
    /// rect (see ClickThroughManager) — by construction that means it is over a petal's bounds,
    /// so the raycast here is the precise hit test, not a redundant one.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Petal Click Input")]
    public sealed class PetalClickInput : MonoBehaviour
    {
        [Tooltip("Camera the pointer position is interpreted through. Falls back to Camera.main.")]
        [SerializeField]
        private Camera raycastCamera;

        [Tooltip("Hit points removed per click. Powerups and helpers scale damage elsewhere.")]
        [SerializeField, Min(1)]
        private int damagePerClick = 1;

        [SerializeField, Min(1f)]
        private float maxRayDistance = 100f;

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(pointer.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance)
                && hit.collider.TryGetComponent(out PetalController petal))
            {
                petal.ApplyDamage(damagePerClick);
            }
        }
    }
}
