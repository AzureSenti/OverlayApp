using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OverlayApp.Helpers;

/// <summary>
/// Provides utility methods to capture an external window by screen coordinates.
/// </summary>
public static class WindowCaptureHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    private const uint GA_ROOT = 2;

    /// <summary>
    /// Gets the root window handle at the specified screen coordinates.
    /// </summary>
    public static IntPtr GetRootWindowFromPoint(int x, int y)
    {
        var pt = new POINT { X = x, Y = y };
        IntPtr hWnd = WindowFromPoint(pt);

        if (hWnd != IntPtr.Zero)
        {
            // Get the top-level window (root), ignoring owner windows (like tooltips or popups)
            IntPtr hRoot = GetAncestor(hWnd, GA_ROOT);
            return hRoot != IntPtr.Zero ? hRoot : hWnd;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Gets the process ID associated with the window handle.
    /// </summary>
    public static int GetProcessIdFromWindow(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        return (int)pid;
    }

    /// <summary>
    /// Gets the text (title) of the window handle.
    /// </summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
