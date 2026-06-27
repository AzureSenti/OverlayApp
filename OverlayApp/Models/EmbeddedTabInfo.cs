using System.Diagnostics;
using OverlayApp.Helpers;

namespace OverlayApp.Models;

/// <summary>
/// Tracks information about an embedded tab (app window or browser).
/// </summary>
public class EmbeddedTabInfo : IDisposable
{
    /// <summary>
    /// Unique tab identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display title for the tab header.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Type of embedded content.
    /// </summary>
    public EmbedType Type { get; set; }

    /// <summary>
    /// The WindowEmbedder instance managing this tab's embedded window.
    /// Null for browser tabs.
    /// </summary>
    public WindowEmbedder? Embedder { get; set; }

    /// <summary>
    /// The WebView2 control for browser tabs.
    /// Null for app tabs.
    /// </summary>
    public Microsoft.Web.WebView2.Wpf.WebView2? WebView { get; set; }

    /// <summary>
    /// The WinForms panel hosting the embedded window.
    /// Null for browser tabs.
    /// </summary>
    public System.Windows.Forms.Panel? HostPanel { get; set; }

    /// <summary>
    /// The WindowsFormsHost wrapping the panel.
    /// Null for browser tabs.
    /// </summary>
    public System.Windows.Forms.Integration.WindowsFormsHost? FormsHost { get; set; }

    /// <summary>
    /// The associated process (if launched by us).
    /// </summary>
    public Process? Process { get; set; }

    /// <summary>
    /// The embedded window handle.
    /// </summary>
    public IntPtr EmbeddedHandle { get; set; } = IntPtr.Zero;

    /// <summary>
    /// Source AppEntry if this tab was opened from the app list.
    /// </summary>
    public AppEntry? SourceApp { get; set; }

    public void Dispose()
    {
        if (Embedder != null)
        {
            Embedder.ReleaseEmbedded();
            Embedder.Dispose();
        }

        WebView?.Dispose();
        HostPanel?.Dispose();

        GC.SuppressFinalize(this);
    }
}

public enum EmbedType
{
    Application,
    Browser
}
