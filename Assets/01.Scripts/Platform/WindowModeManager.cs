using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// Owns the overlay ⇄ expanded presentation switch and delegates window styling to
    /// <see cref="IWindowPlatform"/>. Entering overlay also configures the camera for alpha
    /// output: solid clear at alpha 0 is what lets the desktop show through — the skybox would
    /// otherwise fill every pixel with alpha 1.
    /// </summary>
    /// <remarks>
    /// Alpha survives to the framebuffer only because of three settings that live outside this
    /// class: HDR is off on the RP asset (URP's 32-bit HDR format B10G11R11 has no alpha channel),
    /// post-process alpha output is enabled there as a safety net, and the plugin marks the
    /// window's CAMetalLayer non-opaque. Breaking any of those breaks the overlay silently.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Window Mode Manager")]
    public sealed class WindowModeManager : MonoBehaviour
    {
        [Tooltip("Camera to reconfigure for overlay rendering. Falls back to Camera.main.")]
        [SerializeField]
        private Camera overlayCamera;

        [SerializeField]
        private bool enterOverlayOnStart = true;

        [Tooltip("Overlay frame-rate budget. Plan target: 30 active, dropping to 10 when idle (M2).")]
        [SerializeField, Min(1)]
        private int overlayTargetFrameRate = 30;

        [Tooltip("Seconds after entering overlay during which the window rect is re-asserted each " +
                 "frame, to win the race against the OS/engine finishing their own window setup.")]
        [SerializeField, Min(0f)]
        private float overlaySettleSeconds = 2f;

        private float settleUntilTime;

        private IWindowPlatform platform;
        private CameraClearFlags previousClearFlags;
        private Color previousBackgroundColor;

        public IWindowPlatform Platform => platform ??= WindowPlatformFactory.Create();

        public bool IsOverlayActive { get; private set; }

        private void Start()
        {
            if (enterOverlayOnStart)
            {
                EnterOverlay();
            }
        }

        public void EnterOverlay()
        {
            if (IsOverlayActive)
            {
                return;
            }

            Camera cam = ResolveCamera();
            if (cam != null)
            {
                previousClearFlags = cam.clearFlags;
                previousBackgroundColor = cam.backgroundColor;

                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

                UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
                if (cameraData != null)
                {
                    // Post-FX is the canonical alpha destroyer; the overlay look needs none of it.
                    cameraData.renderPostProcessing = false;
                }
            }

            Application.targetFrameRate = overlayTargetFrameRate;

            // Resize first, restyle second: an external resize makes Unity rebuild its Metal
            // surface, which resets the layer to opaque — transparency applied before the resize
            // would be silently undone (the plugin also re-asserts it after resizes, as the
            // rebuild lands asynchronously). The rect is the screen's visible frame: desktop-wide
            // so the flower can anchor bottom-right and helpers can fly in from the edges, but
            // never exactly display-sized — that trips macOS Game Mode and direct-to-display
            // scanout, which bypasses the compositing that transparency depends on.
            if (Platform.TryGetScreenFrame(out Rect screenFrame))
            {
                Platform.SetWindowRect(screenFrame);
            }

            Platform.SetTransparent(true);
            Platform.SetAlwaysOnTop(true);

            // Start fully click-through: ambient-first means the failure mode of a missing
            // ClickThroughManager is an inert overlay, never a desktop that swallows clicks.
            // The manager flips this per frame from the published interactive rects.
            Platform.SetClickThrough(true);

            IsOverlayActive = true;
            settleUntilTime = Time.unscaledTime + overlaySettleSeconds;

            Debug.Log($"[WindowModeManager] Overlay entered (platform support: {Platform.SupportsOverlay}).");
        }

        /// <summary>
        /// Re-asserts the overlay rect for a short window after entering it. Unity restores its
        /// own window geometry asynchronously during startup, so a single SetWindowRect issued
        /// from Start is sometimes applied and sometimes overwritten a few frames later — which
        /// showed up as the flower landing in a different place on each launch.
        /// </summary>
        private void Update()
        {
            if (!IsOverlayActive || Time.unscaledTime > settleUntilTime)
            {
                return;
            }

            if (Platform.TryGetScreenFrame(out Rect screenFrame))
            {
                Platform.SetWindowRect(screenFrame);
            }
        }

        public void ExitOverlay()
        {
            if (!IsOverlayActive)
            {
                return;
            }

            Camera cam = ResolveCamera();
            if (cam != null)
            {
                cam.clearFlags = previousClearFlags;
                cam.backgroundColor = previousBackgroundColor;
            }

            Application.targetFrameRate = -1;

            Platform.SetTransparent(false);
            Platform.SetAlwaysOnTop(false);
            Platform.SetClickThrough(false);

            IsOverlayActive = false;
        }

        private Camera ResolveCamera()
        {
            return overlayCamera != null ? overlayCamera : Camera.main;
        }
    }
}
