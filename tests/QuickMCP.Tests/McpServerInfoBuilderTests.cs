using QuickMCP.Builders;
using Shouldly;

namespace QuickMCP.Tests;

public class McpServerInfoBuilderTests
{
    [Theory]
    [InlineData("ai21.json","https://example.com/")]
    [InlineData("anthropic.yaml","https://example.com/")]
    [InlineData("cohere.yaml","https://example.com/")]
    [InlineData("dedoose.json","https://example.com/")]
    [InlineData("github.yaml","https://example.com/")]
    [InlineData("huggingface.yaml","https://example.com/")]
    [InlineData("ipinfo.yaml","https://example.com/")]
    [InlineData("langsmith.json","https://example.com/")]
    [InlineData("leonardo.yaml","https://example.com/")]
    [InlineData("mystic.yaml","https://example.com/")]
    [InlineData("ollama.yaml","https://example.com/")]
    [InlineData("openai.yaml","https://example.com/")]
    [InlineData("petstore.yaml","https://example.com/")]
    [InlineData("replicate.json","https://example.com/")]
    [InlineData("special-cases.yaml","https://example.com/")]
    [InlineData("together.yaml","https://example.com/")]
    [InlineData("twitch.json","https://example.com/")]
    [InlineData("filtering.yaml","https://example.com/")]
    [InlineData("heygen.yaml","https://example.com/")]
    [InlineData("instill.yaml","https://example.com/")]
    [InlineData("ideogram.yaml","https://example.com/")]
    [InlineData("google-gemini.yaml","https://example.com/")]
    [InlineData("mistral.yaml","https://example.com/")]
    [InlineData("elevenlabs.json","https://example.com/")]
    [InlineData("jina.json","https://example.com/")]
    [InlineData("runway.yaml","https://example.com/")]
    [InlineData("recraft.yaml","https://example.com/")]
    [InlineData("luma.yaml","https://example.com/")]
    [InlineData("maven.json","https://example.com/")]
    [InlineData("xquik.json","https://xquik.com/")]
    public async Task OpenApiTest(string fileName,string baseUrl)
    {
        var path = $"specs/{fileName}";
        var serverInfo = await McpServerInfoBuilder.ForOpenApi().FromFile(path).WithBaseUrl(baseUrl).BuildAsync();
        // Assert the builder contains some tools
        serverInfo.Tools.Count.ShouldBeGreaterThan(0);

        foreach (var tool in serverInfo.Tools)
        {
            // Assert that the key exists and is not empty
            tool.Key.ShouldNotBeNullOrEmpty();

            // Assert simple properties are not null or empty
            tool.Value.Method.ShouldNotBeNullOrEmpty();
            tool.Value.Url.ShouldNotBeNullOrEmpty();
            tool.Value.MimeType.ShouldNotBeNullOrEmpty();

            // Assert metadata properties using ShouldSatisfyAllConditions
            tool.Value.Metadata.ShouldSatisfyAllConditions(
                () => tool.Value.Metadata.Name.ShouldNotBeNullOrEmpty(),
                () => tool.Value.Metadata.Description.ShouldNotBeNullOrEmpty(),
                // Depending on the type, you may need to assert differently (e.g., empty JSON string is valid)
                () => tool.Value.Metadata.InputSchema.ShouldNotBeNull(),
                () => tool.Value.Metadata.Parameters.ShouldNotBeNull(),
                () => tool.Value.Metadata.Tags.ShouldNotBeNull(),
                () => tool.Value.Metadata.ServerInfo.ShouldNotBeNull(),
                () => tool.Value.Metadata.ServerInfo?.Name.ShouldNotBeNullOrEmpty(),
                () => tool.Value.Metadata.ResponseSchema.ShouldNotBeNull()
            );

            // If you need to further assert collections like Parameters or Tags, you can do so:
            //tool.Value.Metadata.Parameters.Count().ShouldBeGreaterThan(0);
            tool.Value.Metadata.Tags.Count().ShouldBeGreaterThan(0);
        }

        foreach (var tool in serverInfo.Tools)
        {
            Console.WriteLine(tool.Key);
            Console.WriteLine($"Method: {tool.Value.Method}");
            Console.WriteLine($"Url: {tool.Value.Url}");
            Console.WriteLine($"ContentType: {tool.Value.MimeType}");
            Console.WriteLine($"Metadata.Name: {tool.Value.Metadata.Name}");
            Console.WriteLine($"Metadata.Description: {tool.Value.Metadata.Description}");
            Console.WriteLine($"Metadata.InputSchema: {tool.Value.Metadata.InputSchema}");
            Console.WriteLine(
                $"Metadata.Parameters: {string.Join(", ", tool.Value.Metadata.Parameters.Select(p => p.ToString()))}");
            Console.WriteLine($"Metadata.Tags: {string.Join(", ", tool.Value.Metadata.Tags)}");
            Console.WriteLine($"Metadata.ServerInfo: {tool.Value.Metadata.ServerInfo}");
            Console.WriteLine($"Metadata.ResponseSchema: {tool.Value.Metadata.ResponseSchema}");
        }
    }
    
    [Theory]
    [InlineData("https://aiplatform.googleapis.com/$discovery/rest?version=v1beta1")]
    public async Task GoogleDiscoveryTests(string url)
    {
        
        var serverInfo = await McpServerInfoBuilder.ForGoogleDiscovery().FromUrl(url).BuildAsync();
        // Assert the builder contains some tools
        serverInfo.Tools.Count.ShouldBeGreaterThan(0);

        foreach (var tool in serverInfo.Tools)
        {
            // Assert that the key exists and is not empty
            tool.Key.ShouldNotBeNullOrEmpty();

            // Assert simple properties are not null or empty
            tool.Value.Method.ShouldNotBeNullOrEmpty();
            tool.Value.Url.ShouldNotBeNullOrEmpty();
            tool.Value.MimeType.ShouldNotBeNullOrEmpty();

            // Assert metadata properties using ShouldSatisfyAllConditions
            tool.Value.Metadata.ShouldSatisfyAllConditions(
                () => tool.Value.Metadata.Name.ShouldNotBeNullOrEmpty(),
                () => tool.Value.Metadata.Description.ShouldNotBeNullOrEmpty(),
                // Depending on the type, you may need to assert differently (e.g., empty JSON string is valid)
                () => tool.Value.Metadata.InputSchema.ShouldNotBeNull(),
                () => tool.Value.Metadata.Parameters.ShouldNotBeNull(),
                () => tool.Value.Metadata.Tags.ShouldNotBeNull(),
                () => tool.Value.Metadata.ServerInfo.ShouldNotBeNull(),
                () => tool.Value.Metadata.ServerInfo?.Name.ShouldNotBeNullOrEmpty(),
                () => tool.Value.Metadata.ResponseSchema.ShouldNotBeNull()
            );

            // If you need to further assert collections like Parameters or Tags, you can do so:
            //tool.Value.Metadata.Parameters.Count().ShouldBeGreaterThan(0);
            tool.Value.Metadata.Tags.Count().ShouldBeGreaterThan(0);
        }

        foreach (var tool in serverInfo.Tools)
        {
            Console.WriteLine(tool.Key);
            Console.WriteLine($"Method: {tool.Value.Method}");
            Console.WriteLine($"Url: {tool.Value.Url}");
            Console.WriteLine($"ContentType: {tool.Value.MimeType}");
            Console.WriteLine($"Metadata.Name: {tool.Value.Metadata.Name}");
            Console.WriteLine($"Metadata.Description: {tool.Value.Metadata.Description}");
            Console.WriteLine($"Metadata.InputSchema: {tool.Value.Metadata.InputSchema}");
            Console.WriteLine(
                $"Metadata.Parameters: {string.Join(", ", tool.Value.Metadata.Parameters.Select(p => p.ToString()))}");
            Console.WriteLine($"Metadata.Tags: {string.Join(", ", tool.Value.Metadata.Tags)}");
            Console.WriteLine($"Metadata.ServerInfo: {tool.Value.Metadata.ServerInfo}");
            Console.WriteLine($"Metadata.ResponseSchema: {tool.Value.Metadata.ResponseSchema}");
        }
    }
}
