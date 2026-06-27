namespace OverlayApp.Models;

/// <summary>
/// Represents an application entry in the launcher list.
/// </summary>
public class AppEntry
{
    /// <summary>
    /// Display name of the application.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the executable file.
    /// </summary>
    public string ExePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional command-line arguments.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}
