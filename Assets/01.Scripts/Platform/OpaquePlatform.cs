using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// No-op implementation for every context without a restylable window: the editor, mobile,
    /// and MR. The game runs as a normal opaque app and overlay features simply do nothing.
    /// </summary>
    internal sealed class OpaquePlatform : IWindowPlatform
    {
        public bool SupportsOverlay => false;

        public void SetTransparent(bool enabled) { }

        public void SetClickThrough(bool enabled) { }

        public void SetAlwaysOnTop(bool enabled) { }

        public void SetWindowRect(Rect rect) { }

        public void SetExpanded(bool expanded) { }

        public bool TryGetCursorScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = default;
            return false;
        }

        public bool TryGetScreenFrame(out Rect frame)
        {
            frame = default;
            return false;
        }
    }
}
