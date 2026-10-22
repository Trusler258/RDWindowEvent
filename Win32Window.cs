using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RDWindowEvent
{
    internal static class Win32Window
    {
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;
        public const uint SwpAsyncWindowPos = 0x4000;

        private const uint MonitorDefaultToNearest = 0x00000002;
        private const uint GwOwner = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint command);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height,
            uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        public static IntPtr FindGameWindow()
        {
            uint processId = (uint)Process.GetCurrentProcess().Id;
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;

            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid != processId || !IsWindowVisible(hWnd))
                {
                    return true;
                }
                if (GetWindow(hWnd, GwOwner) != IntPtr.Zero)
                {
                    return true;
                }
                if (!GetWindowRect(hWnd, out var rect))
                {
                    return true;
                }
                long area = (long)rect.Width * rect.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hWnd;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        public static bool IsValid(IntPtr hWnd)
        {
            return hWnd != IntPtr.Zero && IsWindow(hWnd);
        }

        public static bool TryGetRect(IntPtr hWnd, out Rect rect)
        {
            rect = default;
            return hWnd != IntPtr.Zero && GetWindowRect(hWnd, out rect);
        }

        public static bool TryGetMonitorRect(IntPtr hWnd, out Rect rect)
        {
            rect = default;
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }
            IntPtr monitor = MonitorFromWindow(hWnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }
            rect = info.Monitor;
            return true;
        }

        public static bool IsMaximized(IntPtr hWnd)
        {
            return hWnd != IntPtr.Zero && IsZoomed(hWnd);
        }

        public static void Move(IntPtr hWnd, int left, int top)
        {
            SetWindowPos(hWnd, IntPtr.Zero, left, top, 0, 0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);
        }

        public static void MoveAndResize(IntPtr hWnd, Rect rect)
        {
            SetWindowPos(hWnd, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height,
                SwpNoZOrder | SwpNoActivate);
        }
    }
}
