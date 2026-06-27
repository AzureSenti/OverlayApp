using System.IO;
using OverlayApp.Models;

namespace OverlayApp.Helpers;

/// <summary>
/// Injects profile isolation arguments for known applications (Chromium, Firefox)
/// to bypass single-instance limitations and keep data sandboxed.
/// </summary>
public static class ProfileInjectorHelper
{
    private static readonly string ProfilesBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OverlayApp", "Profiles");

    /// <summary>
    /// Gets the final arguments for launching an app, injecting isolation flags if requested and applicable.
    /// </summary>
    public static string GetLaunchArguments(AppEntry app)
    {
        if (!app.UseIsolatedProfile || string.IsNullOrWhiteSpace(app.ExePath))
            return app.Arguments;

        string fileName = Path.GetFileNameWithoutExtension(app.ExePath).ToLowerInvariant();
        string profileDir = Path.Combine(ProfilesBaseDir, app.Id);

        // Ensure profile directory exists
        if (!Directory.Exists(profileDir))
        {
            Directory.CreateDirectory(profileDir);
        }

        string extraArgs = "";

        if (IsChromiumBased(fileName))
        {
            extraArgs = $"--user-data-dir=\"{profileDir}\" --no-sandbox";
        }
        else if (IsFirefoxBased(fileName))
        {
            extraArgs = $"-profile \"{profileDir}\" -no-remote";
        }

        // If we have extra args, prepend them to any existing arguments the user configured
        if (!string.IsNullOrEmpty(extraArgs))
        {
            return string.IsNullOrWhiteSpace(app.Arguments) 
                ? extraArgs 
                : $"{extraArgs} {app.Arguments}";
        }

        return app.Arguments;
    }

    private static bool IsChromiumBased(string fileName)
    {
        string[] chromiumBrowsers = { "chrome", "msedge", "brave", "opera", "vivaldi", "coccoc", "code", "obsidian", "discord" };
        foreach (var b in chromiumBrowsers)
        {
            if (fileName.Contains(b)) return true;
        }
        return false;
    }

    private static bool IsFirefoxBased(string fileName)
    {
        return fileName.Contains("firefox") || fileName.Contains("waterfox") || fileName.Contains("librewolf");
    }
}
