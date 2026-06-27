using System.Diagnostics;
using Microsoft.Win32;

namespace OverlayApp.Helpers;

/// <summary>
/// Opens searches in the default browser's incognito/private mode.
/// Supports Chrome, Edge, Firefox, Brave, and Opera.
/// </summary>
public static class BrowserSearchHelper
{
    private static readonly Dictionary<string, string> IncognitoFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        { "chrome", "--incognito" },
        { "msedge", "--inPrivate" },
        { "firefox", "--private-window" },
        { "brave", "--incognito" },
        { "opera", "--private" },
        { "vivaldi", "--incognito" },
    };

    /// <summary>
    /// Opens a search query in the default browser's incognito mode.
    /// </summary>
    /// <param name="query">Search query text.</param>
    public static void SearchIncognito(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        string? browserPath = GetDefaultBrowserPath();
        string? incognitoFlag = GetIncognitoFlag(browserPath);

        try
        {
            if (browserPath != null && incognitoFlag != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = $"{incognitoFlag} \"{searchUrl}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                // Fallback: open URL with default handler (won't be incognito)
                Process.Start(new ProcessStartInfo
                {
                    FileName = searchUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a URL directly in the default browser's incognito mode.
    /// </summary>
    public static void OpenIncognito(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        string? browserPath = GetDefaultBrowserPath();
        string? incognitoFlag = GetIncognitoFlag(browserPath);

        try
        {
            if (browserPath != null && incognitoFlag != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = $"{incognitoFlag} \"{url}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects the default browser's executable path from the Windows Registry.
    /// </summary>
    private static string? GetDefaultBrowserPath()
    {
        try
        {
            // Try to get the default browser ProgId
            const string userChoicePath = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
            using var userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoicePath);
            string? progId = userChoiceKey?.GetValue("ProgId")?.ToString();

            if (string.IsNullOrEmpty(progId)) return null;

            // Get the executable path from the ProgId
            string commandPath = $@"{progId}\shell\open\command";
            using var commandKey = Registry.ClassesRoot.OpenSubKey(commandPath);
            string? command = commandKey?.GetValue(null)?.ToString();

            if (string.IsNullOrEmpty(command)) return null;

            // Parse the executable path from the command string
            // It may be quoted: "C:\path\browser.exe" --args
            if (command.StartsWith('"'))
            {
                int endQuote = command.IndexOf('"', 1);
                if (endQuote > 0)
                    return command.Substring(1, endQuote - 1);
            }
            else
            {
                int space = command.IndexOf(' ');
                return space > 0 ? command[..space] : command;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to detect browser: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the incognito/private flag for the detected browser.
    /// </summary>
    private static string? GetIncognitoFlag(string? browserPath)
    {
        if (string.IsNullOrEmpty(browserPath)) return null;

        string fileName = System.IO.Path.GetFileNameWithoutExtension(browserPath).ToLowerInvariant();

        foreach (var kvp in IncognitoFlags)
        {
            if (fileName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        // Default to Chrome-style flag for Chromium-based browsers
        return "--incognito";
    }
}
