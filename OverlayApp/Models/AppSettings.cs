using System.Windows.Input;

namespace OverlayApp.Models;

/// <summary>
/// Application settings that persist between sessions.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Window X position.
    /// </summary>
    public double WindowLeft { get; set; } = 100;

    /// <summary>
    /// Window Y position.
    /// </summary>
    public double WindowTop { get; set; } = 100;

    /// <summary>
    /// Window width.
    /// </summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>
    /// Window height.
    /// </summary>
    public double WindowHeight { get; set; } = 620;

    /// <summary>
    /// Hotkey modifier keys (Ctrl, Alt, Shift, Win).
    /// </summary>
    public ModifierKeys HotkeyModifier { get; set; } = ModifierKeys.Control;

    /// <summary>
    /// Hotkey virtual key.
    /// </summary>
    public Key HotkeyKey { get; set; } = Key.Space;

    /// <summary>
    /// List of registered applications.
    /// </summary>
    public List<AppEntry> Apps { get; set; } = new();

    /// <summary>
    /// Overlay opacity (0.0 - 1.0).
    /// </summary>
    public double Opacity { get; set; } = 0.95;
}
