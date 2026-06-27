using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OverlayApp.Helpers;

/// <summary>
/// Manages global hotkey registration using Win32 API.
/// Supports configurable modifier + key combinations.
/// </summary>
public class GlobalHotkeyHelper : IDisposable
{
    // Win32 API imports
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    // Modifier flags for Win32
    private const uint MOD_NONE = 0x0000;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _isRegistered;

    /// <summary>
    /// Fired when the registered hotkey is pressed.
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Current modifier keys.
    /// </summary>
    public ModifierKeys CurrentModifier { get; private set; }

    /// <summary>
    /// Current key.
    /// </summary>
    public Key CurrentKey { get; private set; }

    /// <summary>
    /// Registers a global hotkey for the given window.
    /// </summary>
    public bool Register(Window window, ModifierKeys modifier, Key key)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;

        if (_windowHandle == IntPtr.Zero)
        {
            // Window not yet initialized; defer
            window.SourceInitialized += (s, e) => Register(window, modifier, key);
            return false;
        }

        // Unregister previous if exists
        Unregister();

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        uint mod = ConvertModifier(modifier);
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);

        if (_isRegistered)
        {
            CurrentModifier = modifier;
            CurrentKey = key;
        }

        return _isRegistered;
    }

    /// <summary>
    /// Unregisters the current hotkey.
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }

        _source?.RemoveHook(HwndHook);
    }

    /// <summary>
    /// Re-registers with a new key combination.
    /// </summary>
    public bool ChangeHotkey(Window window, ModifierKeys modifier, Key key)
    {
        Unregister();
        return Register(window, modifier, key);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ConvertModifier(ModifierKeys modifier)
    {
        uint result = MOD_NONE;
        if (modifier.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (modifier.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (modifier.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (modifier.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }
}
