using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GenerativeAI;
using GenerativeAI.Types;
using QuickMCP.Helpers;
using QuickMCP.Types;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuickMCP.CLI.Commands.Build;

[Description("Build a Claude Desktop extension (.mcpb) with MCP Server configuration and manifest.")]
public class BuildClaudeExtensionCommand : AsyncCommand<BuildClaudeExtensionCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildClaudeExtensionCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[bold]Building Claude Desktop Extension[/]");

        string configFile;
        string outputPath;
        string prefix;
        BuilderConfig config;

        // Check if user provided existing config file
        if (!string.IsNullOrEmpty(settings.ConfigPath))
        {
            // Use existing config file
            configFile = settings.ConfigPath;
            if (!File.Exists(configFile))
            {
                AnsiConsole.MarkupLine($"[bold red]Error: Config file not found: {configFile}[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[bold yellow]Using existing config file: {configFile}[/]");

            // Load the config
            var configJson = await File.ReadAllTextAsync(configFile);
            config = JsonSerializer.Deserialize<BuilderConfig>(configJson, QuickMcpJsonSerializerContext.Default.BuilderConfig)!;

            // Determine output path - same directory as config file
            outputPath = Path.GetDirectoryName(configFile) ?? Directory.GetCurrentDirectory();
            prefix = Path.GetFileNameWithoutExtension(configFile).Replace("_config", "");
        }
        else
        {
            // Build new config using the base BuildConfigCommand logic
            AnsiConsole.MarkupLine("[bold yellow]No config file provided, creating new configuration...[/]");

            var buildConfigCommand = new BuildConfigCommand();
            var result = await buildConfigCommand.ExecuteAsync(context, settings);

            if (result != 0)
            {
                return result;
            }

            // BuildConfigCommand already created the config file somewhere
            // We need to find it by searching in the likely output directory
            var searchPath = settings.OutputPath ?? Directory.GetCurrentDirectory();

            AnsiConsole.MarkupLine($"[bold yellow]Searching for generated config in: {searchPath}[/]");

            // Find all *_config.json files in subdirectories
            var possibleConfigs = Directory.GetFiles(searchPath, "*_config.json", SearchOption.AllDirectories);

            if (possibleConfigs.Length == 0)
            {
                AnsiConsole.MarkupLine($"[bold red]Error: No config file found after build[/]");
                return 1;
            }

            // Use the most recently created config file
            configFile = possibleConfigs.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            AnsiConsole.MarkupLine($"[bold green]Found config file: {configFile}[/]");

            // Now load the config to get the actual values used
            var configJson = await File.ReadAllTextAsync(configFile);
            config = JsonSerializer.Deserialize<BuilderConfig>(configJson, QuickMcpJsonSerializerContext.Default.BuilderConfig)!;

            // Determine output path and prefix from the actual config file location
            outputPath = Path.GetDirectoryName(configFile) ?? Directory.GetCurrentDirectory();
            prefix = Path.GetFileNameWithoutExtension(configFile).Replace("_config", "");
        }

        // Read the config file to parse environment variables
        var configJsonText = await File.ReadAllTextAsync(configFile);

        // Parse environment variables from config
        var envVars = ParseEnvironmentVariables(configJsonText);

        AnsiConsole.MarkupLine("[bold yellow]Analyzing configuration and generating metadata...[/]");

        // Use AI to generate comprehensive metadata
        var extensionMetadata = await GenerateComprehensiveMetadata(settings, config, envVars, configJsonText);

        // Merge AI-generated metadata with user-provided values
        MergeMetadata(settings, extensionMetadata);

        AnsiConsole.MarkupLine("[bold yellow]Generating Claude Desktop extension files...[/]");

        // Generate manifest.json
        var manifestPath = await GenerateManifest(settings, config, outputPath, prefix, envVars, extensionMetadata.EnvVarMetadata);

        // Generate README.md if not skipped
        string? readmePath = null;
        if (!settings.SkipReadme)
        {
            readmePath = await GenerateReadme(settings, config, outputPath, prefix, envVars, extensionMetadata.EnvVarMetadata);
        }

        // Create .mcpb file (zip archive)
        await CreateMcpbFile(config, outputPath, prefix, manifestPath, readmePath, configFile);

        AnsiConsole.MarkupLine($"[bold green]Claude Desktop extension successfully built![/]");
        return 0;
    }

    private List<string> ParseEnvironmentVariables(string configJson)
    {
        var envVars = new HashSet<string>();

        // Match both ${VAR} and {{VAR}} patterns
        var dollarPattern = new Regex(@"\$\{([^}]+)\}");
        var curlyPattern = new Regex(@"\{\{([^}]+)\}\}");

        foreach (Match match in dollarPattern.Matches(configJson))
        {
            envVars.Add(match.Groups[1].Value);
        }

        foreach (Match match in curlyPattern.Matches(configJson))
        {
            envVars.Add(match.Groups[1].Value);
        }

        return envVars.ToList();
    }

    private async Task<ComprehensiveMetadata> GenerateComprehensiveMetadata(
        BuildClaudeExtensionCommandSettings settings,
        BuilderConfig config,
        List<string> envVars,
        string configJson)
    {
        // Check if AI metadata generation is enabled
        if (settings.AiMetadata != true)
        {
            AnsiConsole.MarkupLine("[bold yellow]Skipping AI metadata generation (use -m flag to enable)[/]");
            return new ComprehensiveMetadata();
        }

        if (string.IsNullOrEmpty(settings.AiApiKey))
        {
            settings.AiApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        }

        if (string.IsNullOrEmpty(settings.AiApiKey))
        {
            AnsiConsole.MarkupLine("[bold yellow]No AI API key provided, skipping AI metadata generation[/]");
            return new ComprehensiveMetadata();
        }

        AnsiConsole.MarkupLine("[bold yellow]Generating comprehensive AI metadata...[/]");

        var client = new GenerativeModel(settings.AiApiKey, "gemini-2.5-flash");

        var prompt = $@"Analyze the following MCP server configuration and generate comprehensive metadata for a Claude Desktop extension.

Configuration:
{configJson}

Environment Variables Found:
{string.Join("\n", envVars.Select(v => $"- {v}"))}

Generate metadata in the following JSON format:
{{
  ""extension"": {{
    ""display_name"": ""User-friendly extension name"",
    ""description"": ""Clear 1-2 sentence description of what this extension does"",
    ""keywords"": [""keyword1"", ""keyword2"", ""keyword3""],
    ""author_name"": ""Company or individual name if identifiable from API"",
    ""homepage"": ""Homepage URL if identifiable from API base URL""
  }},
  ""environment_variables"": [
    {{
      ""variable_name"": ""API_Key"",
      ""title"": ""API Key"",
      ""description"": ""Enter your API key from the dashboard. This authenticates your requests."",
      ""required"": true,
      ""format_hint"": ""sk_..."",
      ""sensitive"": true
    }}
  ]
}}

For environment variables:
- Analyze the authentication type and variable names
- Provide clear, helpful descriptions
- Include format hints if the pattern is obvious (e.g., bearer tokens, API keys with prefixes)
- Mark variables as sensitive if they contain credentials

For extension metadata:
- Generate a professional display name
- Write a clear description of API capabilities
- Suggest relevant keywords
- Extract author/homepage from API info if available

Reply ONLY with the JSON object.";

        var response = await client.GenerateContentAsync(prompt);
        var jsonText = response.Text();

        // Extract JSON block if wrapped in markdown
        var jsonMatch = Regex.Match(jsonText, @"```json\s*(\{[\s\S]*?\})\s*```");
        if (jsonMatch.Success)
        {
            jsonText = jsonMatch.Groups[1].Value;
        }
        else
        {
            // Try to find JSON object directly
            jsonMatch = Regex.Match(jsonText, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                jsonText = jsonMatch.Value;
            }
        }

        var result = new ComprehensiveMetadata();

        try
        {
            var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            // Parse extension metadata
            if (root.TryGetProperty("extension", out var ext))
            {
                result.DisplayName = ext.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                result.Description = ext.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                result.AuthorName = ext.TryGetProperty("author_name", out var an) ? an.GetString() : null;
                result.Homepage = ext.TryGetProperty("homepage", out var hp) ? hp.GetString() : null;

                if (ext.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
                {
                    var keywords = new List<string>();
                    foreach (var keyword in kw.EnumerateArray())
                    {
                        var k = keyword.GetString();
                        if (k != null) keywords.Add(k);
                    }
                    result.Keywords = string.Join(", ", keywords);
                }
            }

            // Parse environment variable metadata
            if (root.TryGetProperty("environment_variables", out var envVarsArray) && envVarsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in envVarsArray.EnumerateArray())
                {
                    var varName = item.TryGetProperty("variable_name", out var vn) ? vn.GetString() : null;
                    if (varName != null)
                    {
                        result.EnvVarMetadata[varName] = new EnvironmentVariableMetadata
                        {
                            Title = item.TryGetProperty("title", out var title) ? title.GetString() : varName,
                            Description = item.TryGetProperty("description", out var d) ? d.GetString() : "",
                            Required = item.TryGetProperty("required", out var req) && req.GetBoolean(),
                            FormatHint = item.TryGetProperty("format_hint", out var hint) ? hint.GetString() : null,
                            Sensitive = item.TryGetProperty("sensitive", out var sens) && sens.GetBoolean()
                        };
                    }
                }
            }

            AnsiConsole.MarkupLine($"[bold green]Generated comprehensive metadata successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold yellow]Warning: Failed to parse AI response: {ex.Message}[/]");
        }

        return result;
    }

    private void MergeMetadata(BuildClaudeExtensionCommandSettings settings, ComprehensiveMetadata aiMetadata)
    {
        // Only use AI-generated values if user didn't provide them
        if (string.IsNullOrEmpty(settings.DisplayName) && !string.IsNullOrEmpty(aiMetadata.DisplayName))
        {
            settings.DisplayName = aiMetadata.DisplayName;
            AnsiConsole.MarkupLine($"[dim]Using AI-generated display name: {aiMetadata.DisplayName}[/]");
        }

        if (string.IsNullOrEmpty(settings.Description) && !string.IsNullOrEmpty(aiMetadata.Description))
        {
            settings.Description = aiMetadata.Description;
            AnsiConsole.MarkupLine($"[dim]Using AI-generated description: {aiMetadata.Description}[/]");
        }

        if (string.IsNullOrEmpty(settings.AuthorName) && !string.IsNullOrEmpty(aiMetadata.AuthorName))
        {
            settings.AuthorName = aiMetadata.AuthorName;
            AnsiConsole.MarkupLine($"[dim]Using AI-generated author: {aiMetadata.AuthorName}[/]");
        }

        if (string.IsNullOrEmpty(settings.Homepage) && !string.IsNullOrEmpty(aiMetadata.Homepage))
        {
            settings.Homepage = aiMetadata.Homepage;
            AnsiConsole.MarkupLine($"[dim]Using AI-generated homepage: {aiMetadata.Homepage}[/]");
        }

        if (string.IsNullOrEmpty(settings.Keywords) && !string.IsNullOrEmpty(aiMetadata.Keywords))
        {
            settings.Keywords = aiMetadata.Keywords;
            AnsiConsole.MarkupLine($"[dim]Using AI-generated keywords: {aiMetadata.Keywords}[/]");
        }
    }


    private async Task<string> GenerateManifest(BuildClaudeExtensionCommandSettings settings, BuilderConfig config,
        string outputPath, string prefix, List<string> envVars, Dictionary<string, EnvironmentVariableMetadata>? envMetadata)
    {
        AnsiConsole.MarkupLine("[bold yellow]Generating manifest.json...[/]");

        var serverName = StringHelpers.SanitizeServerName(config.ServerName) ?? prefix;
        var displayName = settings.DisplayName ?? config.ServerName ?? serverName;
        var description = settings.Description ?? config.ServerDescription ?? $"MCP server for {displayName}";

        // Build user_config from environment variables
        var userConfig = new Dictionary<string, object>();
        foreach (var envVar in envVars)
        {
            var configKey = envVar.ToLower().Replace("_", "");

            if (envMetadata != null && envMetadata.TryGetValue(envVar, out var metadata))
            {
                // Use AI-generated metadata
                userConfig[configKey] = new
                {
                    type = "string",
                    title = metadata.Title ?? envVar.Replace("_", " "),
                    description = metadata.Description ?? $"Enter your {envVar.Replace("_", " ")}",
                    required = metadata.Required
                };
            }
            else
            {
                // Fallback to basic metadata
                userConfig[configKey] = new
                {
                    type = "string",
                    title = envVar.Replace("_", " "),
                    description = $"Enter your {envVar.Replace("_", " ")}",
                    required = true
                };
            }
        }

        var manifest = new
        {
            manifest_version = "0.3",
            name = serverName,
            display_name = displayName,
            version = settings.Version,
            description = description,
            author = settings.AuthorName != null
                ? new { name = settings.AuthorName, url = settings.AuthorUrl }
                : null,
            homepage = settings.Homepage,
            documentation = settings.Documentation ?? config.ApiSpecUrl,
            icon = settings.Icon ?? "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%234F46E5'%3E%3Cpath d='M12 2L2 7v10c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V7l-10-5z'/%3E%3C/svg%3E",
            license = settings.License,
            keywords = settings.Keywords?.Split(',').Select(k => k.Trim()).ToArray(),
            server = new
            {
                type = "binary",
                entry_point = "quickmcp",
                mcp_config = new
                {
                    command = "quickmcp",
                    args = new[]
                    {
                        "serve",
                        "--config-path",
                        $"${{__dirname}}/{prefix}_config.json"
                    },
                    env = envVars.Count > 0
                        ? envVars.ToDictionary(
                            k => k,
                            v => $"${{user_config.{v.ToLower().Replace("_", "")}}}"
                        )
                        : null
                }
            },
            user_config = userConfig.Count > 0 ? userConfig : null,
            compatibility = new
            {
                platforms = new[] { "darwin", "win32", "linux" }
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);
        var manifestPath = Path.Combine(outputPath, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, manifestJson);

        AnsiConsole.MarkupLine($"[bold green]Manifest saved to {manifestPath}[/]");
        return manifestPath;
    }

    private async Task<string> GenerateReadme(BuildClaudeExtensionCommandSettings settings, BuilderConfig config,
        string outputPath, string prefix, List<string> envVars, Dictionary<string, EnvironmentVariableMetadata>? envMetadata)
    {
        AnsiConsole.MarkupLine("[bold yellow]Generating README.md...[/]");

        var serverName = StringHelpers.SanitizeServerName(config.ServerName) ?? prefix;
        var displayName = settings.DisplayName ?? config.ServerName ?? serverName;
        var description = settings.Description ?? config.ServerDescription ?? $"MCP server for {displayName}";

        var readme = new StringBuilder();
        readme.AppendLine($"# {displayName} MCP Extension");
        readme.AppendLine();
        readme.AppendLine(description);
        readme.AppendLine();
        readme.AppendLine("## Installation Guide");
        readme.AppendLine();
        readme.AppendLine("### Step 1: Install QuickMCP CLI Tool");
        readme.AppendLine();
        readme.AppendLine("QuickMCP is required to run the MCP server.");
        readme.AppendLine();
        readme.AppendLine("#### Option A: Using Setup Scripts (Recommended)");
        readme.AppendLine();
        readme.AppendLine("##### **Windows**");
        readme.AppendLine("```powershell");
        readme.AppendLine(".\\setup.bat");
        readme.AppendLine("```");
        readme.AppendLine();
        readme.AppendLine("##### **macOS**");
        readme.AppendLine("```bash");
        readme.AppendLine("chmod +x setup-mac-os.sh");
        readme.AppendLine("./setup-mac-os.sh");
        readme.AppendLine("```");
        readme.AppendLine();
        readme.AppendLine("##### **Linux**");
        readme.AppendLine("```bash");
        readme.AppendLine("chmod +x setup-linux.sh");
        readme.AppendLine("./setup-linux.sh");
        readme.AppendLine("```");
        readme.AppendLine();
        readme.AppendLine("#### Option B: Manual Installation");
        readme.AppendLine();
        readme.AppendLine("```bash");
        readme.AppendLine("dotnet tool install -g quickmcp.cli");
        readme.AppendLine("```");
        readme.AppendLine();
        readme.AppendLine("#### Verify Installation");
        readme.AppendLine();
        readme.AppendLine("```bash");
        readme.AppendLine("quickmcp --version");
        readme.AppendLine("```");
        readme.AppendLine();
        readme.AppendLine("### Step 2: Install Claude Desktop");
        readme.AppendLine();
        readme.AppendLine("Download Claude Desktop from https://claude.ai/download");
        readme.AppendLine();
        readme.AppendLine("### Step 3: Install the Extension");
        readme.AppendLine();
        readme.AppendLine("1. Open Claude Desktop");
        readme.AppendLine("2. Go to Settings > Extensions");
        readme.AppendLine($"3. Click \"Install Extension...\" and select `{serverName}.mcpb`");
        readme.AppendLine("4. Configure the required settings");
        readme.AppendLine("5. Restart Claude Desktop");
        readme.AppendLine();
        readme.AppendLine("## Configuration");
        readme.AppendLine();

        if (envVars.Count > 0)
        {
            readme.AppendLine("The extension requires the following configuration:");
            readme.AppendLine();
            foreach (var envVar in envVars)
            {
                if (envMetadata != null && envMetadata.TryGetValue(envVar, out var metadata))
                {
                    readme.AppendLine($"- **{metadata.Title ?? envVar}**: {metadata.Description}");
                    if (!string.IsNullOrEmpty(metadata.FormatHint))
                    {
                        readme.AppendLine($"  - Format: `{metadata.FormatHint}`");
                    }
                }
                else
                {
                    readme.AppendLine($"- **{envVar}**: Required configuration value");
                }
            }
            readme.AppendLine();
        }

        if (settings.Homepage != null)
        {
            readme.AppendLine($"Homepage: {settings.Homepage}");
            readme.AppendLine();
        }

        if (settings.Documentation != null || config.ApiSpecUrl != null)
        {
            readme.AppendLine($"Documentation: {settings.Documentation ?? config.ApiSpecUrl}");
            readme.AppendLine();
        }

        readme.AppendLine("## Support");
        readme.AppendLine();
        readme.AppendLine("For issues or questions:");
        readme.AppendLine();
        if (settings.Documentation != null || config.ApiSpecUrl != null)
        {
            readme.AppendLine($"- API Documentation: {settings.Documentation ?? config.ApiSpecUrl}");
        }
        readme.AppendLine("- QuickMCP Documentation: https://github.com/gunpal5/QuickMCP");
        readme.AppendLine();
        readme.AppendLine("## License");
        readme.AppendLine();
        readme.AppendLine($"{settings.License} License");
        readme.AppendLine();

        var readmePath = Path.Combine(outputPath, "README.md");
        await File.WriteAllTextAsync(readmePath, readme.ToString());

        AnsiConsole.MarkupLine($"[bold green]README saved to {readmePath}[/]");
        return readmePath;
    }

    private async Task CreateMcpbFile(BuilderConfig config,
        string outputPath, string prefix, string manifestPath, string? readmePath, string configFile)
    {
        AnsiConsole.MarkupLine("[bold yellow]Creating .mcpb package...[/]");

        var mcpbFileName = $"{StringHelpers.SanitizeServerName(config.ServerName) ?? prefix}.mcpb";
        var mcpbPath = Path.Combine(outputPath, mcpbFileName);

        // Delete existing file if it exists
        if (File.Exists(mcpbPath))
        {
            File.Delete(mcpbPath);
        }

        using (var archive = ZipFile.Open(mcpbPath, ZipArchiveMode.Create))
        {
            // Add manifest.json
            archive.CreateEntryFromFile(manifestPath, "manifest.json");

            // Add config file
            if (File.Exists(configFile))
            {
                archive.CreateEntryFromFile(configFile, Path.GetFileName(configFile));
            }

            // Add API spec file
            if (config.ApiSpecPath != null)
            {
                var specFile = Path.Combine(outputPath, config.ApiSpecPath);
                if (File.Exists(specFile))
                {
                    archive.CreateEntryFromFile(specFile, config.ApiSpecPath);
                }
            }

            // Add README if generated
            if (readmePath != null && File.Exists(readmePath))
            {
                archive.CreateEntryFromFile(readmePath, "README.md");
            }
        }

        AnsiConsole.MarkupLine($"[bold green]Package created: {mcpbPath}[/]");
    }
}
