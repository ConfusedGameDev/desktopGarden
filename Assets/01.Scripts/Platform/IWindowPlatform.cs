using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// The one seam between the game and the operating system's window layer. Everything
    /// OS-specific — transparency, click-through, always-on-top — hides behind this interface;
    /// gameplay code never touches a platform API directly.
    /// </summary>
    public interface IWindowPlatform
    {
        /// <summary>Whether this platform can actually restyle its window (mobile/editor cannot).</summary>
        bool SupportsOverlay { get; }

        /// <summary>Borderless window with an alpha-composited framebuffer; desktop shows through alpha 0.</summary>
        void SetTransparent(bool enabled);

        /// <summary>
        /// When enabled, all mouse input falls through to whatever is behind the window.
        /// Overlay mode toggles this per frame from the union of interactive screen rects.
        /// </summary>
        void SetClickThrough(bool enabled);

        /// <summary>Float above normal windows and follow the user across Spaces.</summary>
        void SetAlwaysOnTop(bool enabled);

        /// <summary>Window frame in screen points, origin bottom-left (macOS convention).</summary>
        void SetWindowRect(Rect rect);

        /// <summary>Overlay ⇄ expanded presentation. No-op until M1 builds the expanded mode.</summary>
        void SetExpanded(bool expanded);

        /// <summary>
        /// Cursor position in this window's pixel coordinates, origin bottom-left (Unity's screen
        /// convention). Asked of the OS, not the engine, because a click-through window stops
        /// receiving mouse events and Unity's own pointer state goes stale. False when the
        /// platform cannot answer; callers fall back to engine input.
        /// </summary>
        bool TryGetCursorScreenPosition(out Vector2 screenPosition);

        /// <summary>
        /// Primary screen frame in the same units <see cref="SetWindowRect"/> takes (macOS:
        /// points, origin bottom-left). Overlay mode stretches the window across this so
        /// viewport-anchored content can sit anywhere on the desktop.
        /// </summary>
        bool TryGetScreenFrame(out Rect frame);
    }
}
