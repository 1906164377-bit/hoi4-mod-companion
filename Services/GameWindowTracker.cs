using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hoi4ModOverlay.Services;

public sealed class GameWindowTracker
{
    public bool TryGetHoi4Window(out GameWindowInfo info)
    {
        info = default;
        Process? process = null;
        try
        {
            process = Process.GetProcessesByName("hoi4")
                .FirstOrDefault(candidate => candidate.MainWindowHandle != IntPtr.Zero);
        }
        catch
        {
            return false;
        }

        if (process is null)
        {
            return false;
        }

        var hwnd = process.MainWindowHandle;
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out var clientRect))
        {
            return false;
        }

        var origin = new Point { X = clientRect.Left, Y = clientRect.Top };
        if (!ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        info = new GameWindowInfo(
            hwnd,
            origin.X,
            origin.Y,
            Math.Max(0, clientRect.Right - clientRect.Left),
            Math.Max(0, clientRect.Bottom - clientRect.Top),
            GetForegroundWindow() == hwnd);
        return info.Width > 0 && info.Height > 0;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}

public readonly record struct GameWindowInfo(
    IntPtr Handle,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsForeground);
