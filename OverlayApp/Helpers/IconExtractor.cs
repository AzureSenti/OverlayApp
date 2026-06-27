using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OverlayApp.Helpers;

/// <summary>
/// Extracts icons from executable files and converts them for WPF use.
/// </summary>
public static class IconExtractor
{
    /// <summary>
    /// Extracts the icon from an executable and returns it as a WPF ImageSource.
    /// </summary>
    /// <param name="exePath">Path to the executable file.</param>
    /// <returns>WPF ImageSource or null if extraction fails.</returns>
    public static ImageSource? GetIconFromExe(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;

            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the display name from an executable path.
    /// </summary>
    /// <param name="exePath">Path to the executable file.</param>
    /// <returns>A friendly display name derived from the filename.</returns>
    public static string GetDisplayName(string exePath)
    {
        try
        {
            string fileName = Path.GetFileNameWithoutExtension(exePath);

            // Try to get product name from version info
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                return versionInfo.ProductName;

            if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                return versionInfo.FileDescription;

            return fileName;
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(exePath);
        }
    }
}
