namespace QuickMCP.CLI.Commands.Build;

/// <summary>
/// Metadata for an environment variable used in Claude extension configuration
/// </summary>
public class EnvironmentVariableMetadata
{
    /// <summary>
    /// User-friendly title for the variable
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description of what this variable is used for
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this variable is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Format hint or example (e.g., "sk_...", "https://...")
    /// </summary>
    public string? FormatHint { get; set; }

    /// <summary>
    /// Whether this variable contains sensitive information
    /// </summary>
    public bool Sensitive { get; set; }
}
