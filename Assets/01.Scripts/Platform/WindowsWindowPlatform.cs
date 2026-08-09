#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// Windows implementation — pure P/Invoke into user32/dwmapi, no compiled plugin.
    /// Player-only: the editor must never restyle its own window.
    /// </summary>
    internal sealed class WindowsWindowPlatform : IWindowPlatform
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_SYSMENU = 0x00080000;

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint SPI_GETWORKAREA = 0x0030;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        // Unity's Windows player has exactly one top-level window, already active by the time
        // scripts run, so GetActiveWindow() at first use is enough — no window-message plumbing.
        private IntPtr windowHandle;

        // Last transparency state requested by C#, kept for the same reason the Mac bundle keeps
        // its own: the styling has to be re-asserted after anything that rebuilds the window.
        private bool transparent;

        private IntPtr Handle => windowHandle != IntPtr.Zero ? windowHandle : (windowHandle = GetActiveWindow());

        public bool SupportsOverlay => true;

        public void SetTransparent(bool enabled)
        {
            transparent = enabled;
            ApplyStyling();
        }

        private void ApplyStyling()
        {
            IntPtr hWnd = Handle;
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            int style = GetWindowLong(hWnd, GWL_STYLE);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if (transparent)
            {
                style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
                style |= WS_POPUP;
                exStyle |= WS_EX_LAYERED;
            }
            else
            {
                style &= ~WS_POPUP;
                style |= WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
                exStyle &= ~WS_EX_LAYERED;
            }

            SetWindowLong(hWnd, GWL_STYLE, style);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);

            if (transparent)
            {
                // "Sheet of glass": all-(-1) margins extend the DWM frame over the entire client
                // area, so the compositor honours the backbuffer's own per-pixel alpha.
                //
                // Deliberately NOT paired with SetLayeredWindowAttributes: LWA_ALPHA installs a
                // uniform window alpha that overrides D3D's per-pixel alpha, which is precisely
                // the channel the overlay depends on. This also requires
                // "Use DXGI Flip Model Swapchain" to be OFF in Player Settings — DWM cannot
                // composite a flip-model swapchain's alpha, and the overlay renders over black.
                MARGINS margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(hWnd, ref margins);
            }

            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public void SetClickThrough(bool enabled)
        {
            IntPtr hWnd = Handle;
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            exStyle = enabled ? (exStyle | WS_EX_TRANSPARENT) : (exStyle & ~WS_EX_TRANSPARENT);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            IntPtr hWnd = Handle;
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(hWnd, enabled ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void SetWindowRect(Rect rect)
        {
            IntPtr hWnd = Handle;
            if (hWnd == IntPtr.Zero || !TryGetScreenFrame(out Rect screenFrame))
            {
                return;
            }

            // rect arrives bottom-left origin (macOS/Unity screen convention); SetWindowPos wants
            // top-left, measured from the primary monitor's actual top edge, not the work area's.
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            int top = screenHeight - Mathf.RoundToInt(rect.y) - height;

            // Style before sizing, so the frame this rect has to fit is already the final one.
            // Deliberately not Screen.SetResolution: that makes Unity rebuild its own window and
            // restore the standard frame, silently undoing the borderless styling. Unity's
            // backbuffer follows the OS resize via WM_SIZE on its own.
            ApplyStyling();

            SetWindowPos(hWnd, IntPtr.Zero, Mathf.RoundToInt(rect.x), top, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public void SetExpanded(bool expanded)
        {
            // M1: expanded mode restores the titled window at a fixed size. Until then, no-op.
        }

        public bool TryGetCursorScreenPosition(out Vector2 screenPosition)
        {
            IntPtr hWnd = Handle;
            if (hWnd == IntPtr.Zero || !GetCursorPos(out POINT cursor))
            {
                screenPosition = default;
                return false;
            }

            POINT client = cursor;
            if (!ScreenToClient(hWnd, ref client) || !GetClientRect(hWnd, out RECT clientRect))
            {
                screenPosition = default;
                return false;
            }

            // Client rect is top-left origin; flip to bottom-left to match Unity's convention.
            int clientHeight = clientRect.Bottom - clientRect.Top;
            screenPosition = new Vector2(client.X, clientHeight - client.Y);
            return true;
        }

        public bool TryGetScreenFrame(out Rect frame)
        {
            RECT workArea = default;
            if (!SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
            {
                frame = default;
                return false;
            }

            // Work area excludes the taskbar, same reasoning as the Mac side's visibleFrame: a
            // window sized exactly to the display trips fullscreen-optimized scanout, which
            // bypasses DWM composition and would render the overlay over black.
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            float width = workArea.Right - workArea.Left;
            float height = workArea.Bottom - workArea.Top;
            float y = screenHeight - workArea.Bottom;

            frame = new Rect(workArea.Left, y, width, height);
            return true;
        }
    }
}
#endif
