using System.ComponentModel;
using Firecrawl;
using GenerativeAI;
using GenerativeAI.Utility;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuickMCP.CLI.Commands.Build;

[Description("Builds an openapi spec from a documentation url")]
public class BuildSpecCommand:AsyncCommand<BuildSpecCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSpecCommandSettings settings)
    {
        AnsiConsole.MarkupLine($"[green]Loading[/] [yellow]{settings.DocumentationUrl}[/]...");
        
        using var firecrawlApi = new FirecrawlApp(apiKey:
            settings.FirecrawlApiKey ??
            throw new InvalidOperationException("Please set FIRECRAWL_API_KEY environment variable or pass it as a parameter."));
        var scrapeRequest = new Firecrawl.ScrapeAndExtractFromUrlRequest2(settings.DocumentationUrl);
        var firecrawlResponse = await firecrawlApi.Scraping.ScrapeAndExtractFromUrlAsync(
            scrapeRequest).ConfigureAwait(false);

        var markdown = firecrawlResponse.Data?.Markdown ?? throw new InvalidOperationException("[red]No markdown data found.[/]");

      
        
        AnsiConsole.MarkupLine($"[blue]Generating OpenAPI spec from markdown...[/]");
        var generativeModel = new GenerativeModel(settings.GoogleApiKey, GoogleAIModels.Gemini2Flash);
        
        var response = await generativeModel.GenerateContentAsync($"Please generate an OpenAPI 3.0 spec from the following markdown:\r\n   - add description for each method, parameter and body\r\n {markdown}");
        
        if(response == null)
            throw new InvalidOperationException("[red]No response from Google AI[/]");
        var specs = response.Text();
        var extension = response.Text().Contains("yaml")? "yaml": "json";
        var codeBlocks = MarkdownExtractor.ExtractCodeBlocks(markdown);
        if (codeBlocks.Count > 0)
        {
            var block = codeBlocks.FirstOrDefault();
            specs = response.Text().Replace("```json", "").Replace("```yaml","").Replace("```","");
            extension =specs.Trim().StartsWith("{")? "json": "yaml";
            if (!string.IsNullOrWhiteSpace(block.Language))
            {
                extension = block.Language;
            }
        }
       
        
        var outputFile = settings.OutputFile?? $"open_api_spec.{extension}";
        await File.WriteAllTextAsync(outputFile, specs);
        AnsiConsole.MarkupLine($"[green]Generated {extension} file at {outputFile}[/]");
        return 0;
    }
}