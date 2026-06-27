using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OverlayApp.Helpers;

/// <summary>
/// Embeds external application windows into a WPF host panel using Win32 API.
/// </summary>
public class WindowEmbedder : IDisposable
{
    // Win32 API imports
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);



    // Window style constants
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_CHILD = 0x40000000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;

    private IntPtr _embeddedHandle = IntPtr.Zero;
    private IntPtr _hostHandle = IntPtr.Zero;
    private int _originalStyle;
    private Process? _embeddedProcess;

    /// <summary>
    /// Whether a window is currently embedded.
    /// </summary>
    public bool IsEmbedded => _embeddedHandle != IntPtr.Zero && IsWindow(_embeddedHandle);

    /// <summary>
    /// The currently embedded process.
    /// </summary>
    public Process? EmbeddedProcess => _embeddedProcess;

    /// <summary>
    /// Launches a process and embeds its main window into the host panel.
    /// </summary>
    /// <param name="exePath">Path to the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="hostHandle">Handle of the WPF host panel (HwndHost or WindowsFormsHost).</param>
    /// <param name="width">Target width.</param>
    /// <param name="height">Target height.</param>
    /// <returns>True if embedding succeeded.</returns>
    public async Task<bool> LaunchAndEmbed(string exePath, string arguments, IntPtr hostHandle, int width, int height)
    {
        // Release any currently embedded window first
        ReleaseEmbedded();

        _hostHandle = hostHandle;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true
            };

            _embeddedProcess = Process.Start(startInfo);
            if (_embeddedProcess == null) return false;

            // Wait for the process to create its main window
            IntPtr targetHandle = IntPtr.Zero;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                await Task.Delay(200);

                _embeddedProcess.Refresh();
                if (_embeddedProcess.HasExited) return false;

                targetHandle = _embeddedProcess.MainWindowHandle;
                if (targetHandle != IntPtr.Zero) break;
            }

            if (targetHandle == IntPtr.Zero) return false;

            return EmbedWindow(targetHandle, width, height);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Embeds an existing window handle into the host.
    /// </summary>
    public bool EmbedWindow(IntPtr windowHandle, int width, int height)
    {
        if (windowHandle == IntPtr.Zero || _hostHandle == IntPtr.Zero) return false;

        _embeddedHandle = windowHandle;

        // Save original style for restoration
        _originalStyle = GetWindowLong(_embeddedHandle, GWL_STYLE);

        // Remove window chrome and make it a child window
        int newStyle = _originalStyle;
        newStyle &= ~WS_CAPTION;
        newStyle &= ~WS_THICKFRAME;
        newStyle &= ~WS_BORDER;
        newStyle &= ~WS_DLGFRAME;
        newStyle &= ~WS_SYSMENU;
        newStyle &= ~WS_MINIMIZEBOX;
        newStyle &= ~WS_MAXIMIZEBOX;
        newStyle |= WS_CHILD;

        SetWindowLong(_embeddedHandle, GWL_STYLE, newStyle);

        // Re-parent the window
        SetParent(_embeddedHandle, _hostHandle);

        // Resize and show
        MoveWindow(_embeddedHandle, 0, 0, width, height, true);
        ShowWindow(_embeddedHandle, SW_SHOW);

        return true;
    }

    /// <summary>
    /// Resizes the embedded window to match the host panel.
    /// </summary>
    public void ResizeEmbedded(int width, int height)
    {
        if (IsEmbedded)
        {
            MoveWindow(_embeddedHandle, 0, 0, width, height, true);
        }
    }

    /// <summary>
    /// Releases the embedded window back to the desktop.
    /// </summary>
    public void ReleaseEmbedded()
    {
        if (_embeddedHandle != IntPtr.Zero && IsWindow(_embeddedHandle))
        {
            // Restore original style
            SetWindowLong(_embeddedHandle, GWL_STYLE, _originalStyle);

            // Re-parent to desktop
            SetParent(_embeddedHandle, IntPtr.Zero);

            // Show the window normally
            ShowWindow(_embeddedHandle, SW_RESTORE);
        }

        _embeddedHandle = IntPtr.Zero;
        _embeddedProcess = null;
    }

    /// <summary>
    /// Terminates the embedded process and cleans up.
    /// </summary>
    public void CloseEmbedded()
    {
        if (_embeddedProcess != null && !_embeddedProcess.HasExited)
        {
            try
            {
                _embeddedProcess.CloseMainWindow();
                if (!_embeddedProcess.WaitForExit(3000))
                {
                    _embeddedProcess.Kill();
                }
            }
            catch { /* Process may have already exited */ }
        }

        _embeddedHandle = IntPtr.Zero;
        _embeddedProcess = null;
    }

    public void Dispose()
    {
        ReleaseEmbedded();
        GC.SuppressFinalize(this);
    }
}
