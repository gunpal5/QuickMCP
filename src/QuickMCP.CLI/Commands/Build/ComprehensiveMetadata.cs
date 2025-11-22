namespace QuickMCP.CLI.Commands.Build;

/// <summary>
/// Comprehensive metadata for both the extension and its environment variables
/// </summary>
public class ComprehensiveMetadata
{
    /// <summary>
    /// Extension display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Extension description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Author name
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Homepage URL
    /// </summary>
    public string? Homepage { get; set; }

    /// <summary>
    /// Keywords (comma-separated)
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// Environment variable metadata
    /// </summary>
    public Dictionary<string, EnvironmentVariableMetadata> EnvVarMetadata { get; set; } = new();
}
