using System.ComponentModel;
using QuickMCP.Types;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuickMCP.CLI.Commands.Build;

public class BuildClaudeExtensionCommandSettings : BuildConfigCommandSettings
{
    [Description("Path to an existing MCP server configuration file.")]
    [CommandOption("-c|--config-path <CONFIG_PATH>")]
    public string? ConfigPath { get; set; }

    [Description("Display name for the Claude extension.")]
    [CommandOption("--display-name <DISPLAY_NAME>")]
    public string? DisplayName { get; set; }

    [Description("Version of the extension (e.g., 1.0.0).")]
    [CommandOption("-v|--version <VERSION>")]
    public string Version { get; set; } = "1.0.0";

    [Description("Description of the extension.")]
    [CommandOption("-d|--description <DESCRIPTION>")]
    public string? Description { get; set; }

    [Description("Author name.")]
    [CommandOption("--author-name <AUTHOR_NAME>")]
    public string? AuthorName { get; set; }

    [Description("Author URL.")]
    [CommandOption("--author-url <AUTHOR_URL>")]
    public string? AuthorUrl { get; set; }

    [Description("Homepage URL.")]
    [CommandOption("--homepage <HOMEPAGE>")]
    public string? Homepage { get; set; }

    [Description("Documentation URL.")]
    [CommandOption("--documentation <DOCUMENTATION>")]
    public string? Documentation { get; set; }

    [Description("License (e.g., MIT, Apache-2.0).")]
    [CommandOption("--license <LICENSE>")]
    public string License { get; set; } = "MIT";

    [Description("Comma-separated keywords for the extension.")]
    [CommandOption("--keywords <KEYWORDS>")]
    public string? Keywords { get; set; }

    [Description("SVG icon data URI for the extension.")]
    [CommandOption("--icon <ICON>")]
    public string? Icon { get; set; }

    [Description("Skip README.md generation.")]
    [CommandOption("--skip-readme")]
    public bool SkipReadme { get; set; }

    public override ValidationResult Validate()
    {
        // If config path is provided, skip spec validation
        if (!string.IsNullOrEmpty(ConfigPath))
        {
            if (!File.Exists(ConfigPath))
            {
                return ValidationResult.Error($"Config file not found: {ConfigPath}");
            }

            // Only validate AI metadata if enabled
            if (AiMetadata == true)
            {
                if (string.IsNullOrEmpty(AiApiKey))
                    this.AiApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
                if (string.IsNullOrEmpty(AiApiKey))
                    return ValidationResult.Error(
                        "You must specify a Google Gemini API Key (-k) or GOOGLE_API_KEY environment variable for AI metadata generation.");
            }

            // Author name is required if AI is not enabled (AI can generate it)
            if (AiMetadata != true && string.IsNullOrEmpty(AuthorName))
            {
                return ValidationResult.Error(
                    "Author name is required. Use --author-name or enable AI metadata generation with -m to auto-generate.");
            }

            return ValidationResult.Success();
        }

        // Otherwise, use base validation (requires spec)
        var baseValidation = base.Validate();
        if (!baseValidation.Successful)
            return baseValidation;

        // Author name is required if AI is not enabled
        if (AiMetadata != true && string.IsNullOrEmpty(AuthorName))
        {
            return ValidationResult.Error(
                "Author name is required. Use --author-name or enable AI metadata generation with -m to auto-generate.");
        }

        return ValidationResult.Success();
    }
}
