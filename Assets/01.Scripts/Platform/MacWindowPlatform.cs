#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// macOS implementation backed by PollenGardenWindow.bundle (source in
    /// Assets/00.Plugins/macOS/Source~). Player-only: the editor must never restyle its own window.
    /// </summary>
    internal sealed class MacWindowPlatform : IWindowPlatform
    {
        private const string Lib = "PollenGardenWindow";

        // C bool is 1 byte; default marshaling assumes 4-byte Win32 BOOL. I1 keeps the ABI honest.
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PG_IsAvailable();

        [DllImport(Lib)] private static extern void PG_SetTransparent([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport(Lib)] private static extern void PG_SetClickThrough([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport(Lib)] private static extern void PG_SetAlwaysOnTop([MarshalAs(UnmanagedType.I1)] bool enabled);
        [DllImport(Lib)] private static extern void PG_SetWindowRect(float x, float y, float width, float height);

        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PG_TryGetCursorPixels(out float x, out float y);

        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PG_TryGetScreenFrame(out float x, out float y, out float width, out float height);

        public bool SupportsOverlay => PG_IsAvailable();

        public void SetTransparent(bool enabled) => PG_SetTransparent(enabled);

        public void SetClickThrough(bool enabled) => PG_SetClickThrough(enabled);

        public void SetAlwaysOnTop(bool enabled) => PG_SetAlwaysOnTop(enabled);

        public void SetWindowRect(Rect rect) => PG_SetWindowRect(rect.x, rect.y, rect.width, rect.height);

        public void SetExpanded(bool expanded)
        {
            // M1: expanded mode restores the titled window at a fixed size. Until then, no-op.
        }

        public bool TryGetCursorScreenPosition(out Vector2 screenPosition)
        {
            if (PG_TryGetCursorPixels(out float x, out float y))
            {
                screenPosition = new Vector2(x, y);
                return true;
            }

            screenPosition = default;
            return false;
        }

        public bool TryGetScreenFrame(out Rect frame)
        {
            if (PG_TryGetScreenFrame(out float x, out float y, out float width, out float height))
            {
                frame = new Rect(x, y, width, height);
                return true;
            }

            frame = default;
            return false;
        }
    }
}
#endif
