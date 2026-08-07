#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// Windows implementation — stub. The real version is pure P/Invoke into user32/dwmapi:
    /// <c>DwmExtendFrameIntoClientArea</c> for the alpha-composited client area,
    /// <c>WS_EX_LAYERED</c> plus dynamic <c>WS_EX_TRANSPARENT</c> for click-through, and
    /// <c>HWND_TOPMOST</c> via <c>SetWindowPos</c>. No compiled plugin needed.
    /// </summary>
    internal sealed class WindowsWindowPlatform : IWindowPlatform
    {
        public bool SupportsOverlay => false; // flips true when the P/Invoke work lands

        public void SetTransparent(bool enabled) { }

        public void SetClickThrough(bool enabled) { }

        public void SetAlwaysOnTop(bool enabled) { }

        public void SetWindowRect(Rect rect) { }

        public void SetExpanded(bool expanded) { }

        public bool TryGetCursorScreenPosition(out Vector2 screenPosition)
        {
            // Real version: GetCursorPos + ScreenToClient, y flipped to bottom-left origin.
            screenPosition = default;
            return false;
        }

        public bool TryGetScreenFrame(out Rect frame)
        {
            // Real version: GetSystemMetrics / MonitorFromWindow, y flipped to bottom-left origin.
            frame = default;
            return false;
        }
    }
}
#endif
