using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using QuickMCP.Types;

namespace QuickMCP.Server;

[McpServerResourceType]
internal class ResourceRegistry
{
    public static IReadOnlyDictionary<string, ResourceInfo>? ApiResources { get; set; }

    public static IReadOnlyDictionary<string, Resource>? ExternalResources { get; set; }

    public static string? Root { get; internal set; }

    [McpServerResource(UriTemplate = "resources/api/{uri}", Name = "Get API Resource Contents")]
    [Description("Gets API Resource Contents by Uri")]
    public static ResourceContents GetApiResourceContents(string uri)
    {
        var apiResource = ApiResources?.GetValueOrDefault(uri);
        if (apiResource == null)
        {
            return new TextResourceContents
            {
                Uri = uri,
                MimeType = "text/plain",
                Text = $"API Resource '{uri}' not found."
            };
        }

        var blobData = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            { "schema", apiResource.Schema },
            { "metadata", apiResource.Metadata },
        });

        var base64Content = Convert.ToBase64String(blobData);
        return new BlobResourceContents
        {
            Uri = $"resources/api/{uri}",
            MimeType = "application/json+base64",
            Blob = base64Content
        };
    }

    [McpServerResource(UriTemplate = "resources/external/{uri}", Name = "Get External Resource Contents")]
    [Description("Gets External Resource Contents by Uri")]
    public static ResourceContents GetExternalResourceContents(string uri)
    {
        var externalResource = ExternalResources
            ?.FirstOrDefault(r => r.Value.Uri.Equals(uri, StringComparison.OrdinalIgnoreCase)).Value;
        if (externalResource == null)
        {
            return new TextResourceContents
            {
                Uri = uri,
                MimeType = "text/plain",
                Text = $"External Resource '{uri}' not found."
            };
        }

        return new BlobResourceContents
        {
            Uri = externalResource.Uri,
            MimeType = externalResource.MimeType ?? "application/octet-stream",
            Blob = ReadBase64Content($"resources/{externalResource.Uri}")
        };
    }

    public static ResourceContents GetResourceContents(string uri)
    {
        if (ApiResources?.ContainsKey(uri) == true)
        {
            return GetApiResourceContents(uri);
        }

        return GetExternalResourceContents(uri);
    }

    private static string ReadBase64Content(string fileName)
    {
        var fullPath = Root != null ? Path.Combine(Root, fileName) : fileName;
        if (!File.Exists(fullPath))
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"File '{fullPath}' not found."));
        }

        var fileBytes = File.ReadAllBytes(fullPath);
        return Convert.ToBase64String(fileBytes);
    }

    internal static async ValueTask<ListResourcesResult> GetList(RequestContext<ListResourcesRequestParams> context,
        CancellationToken token)
    {
        var allResources_ = BuildResources();
        return new ListResourcesResult
        {
            Resources = allResources_ ?? new List<Resource>()
        };
    }

    private static List<Resource> BuildResources()
    {
        var allResources_ = new List<Resource>();
        if (ApiResources != null)
        {
            foreach (var kvp in ApiResources)
            {
                allResources_.Add(new Resource
                {
                    Name = kvp.Key,
                    Uri = $"resources/api/{kvp.Key}",
                    Description = kvp.Value.Metadata.GetValueOrDefault("description")?.ToString() ??
                                  "No description available."
                });
            }
        }

        if (ExternalResources != null)
        {
            foreach (var kvp in ExternalResources)
            {
                var resource = kvp.Value;
                allResources_.Add(new Resource
                {
                    Name = resource.Name,
                    Uri = $"resources/external/{resource.Uri}",
                    Description = resource.Description ?? "No description available."
                });
            }
        }

        return allResources_;
    }
}

[McpServerPromptType]
internal class PromptsRegistry
{
    public static IReadOnlyDictionary<string, QuickMCP.Types.Prompt>? ApiPrompts { get; set; }

    public static IReadOnlyDictionary<string, ExtendedPrompt>? ExtendedPrompts { get; set; }


    [McpServerPrompt(Name = "complex_prompt"), Description("A prompt with arguments")]
    public static IEnumerable<ChatMessage> GetApiPrompt()
    {
        return
        [
            new ChatMessage(ChatRole.Assistant,
                "I understand. You've provided a complex prompt with temperature and style arguments. How would you like me to proceed?")
        ];
    }


    internal static async ValueTask<ListPromptsResult> GetList(RequestContext<ListPromptsRequestParams> context,
        CancellationToken token)
    {
        var allPrompts_ = BuildPrompts();
        return new ListPromptsResult
        {
            Prompts = allPrompts_ ?? new List<ModelContextProtocol.Protocol.Prompt>()
        };
    }

    private static List<ModelContextProtocol.Protocol.Prompt> BuildPrompts()
    {
        var allPrompts = new List<ModelContextProtocol.Protocol.Prompt>();
        if (ApiPrompts != null)
        {
            foreach (var kvp in ApiPrompts)
            {
                allPrompts.Add(new ModelContextProtocol.Protocol.Prompt
                {
                    Name = kvp.Key,
                    Description = kvp.Value.Description ?? "No description available."
                });
            }
        }

        if (ExtendedPrompts != null)
        {
            foreach (var kvp in ExtendedPrompts)
            {
                var prompt = kvp.Value;
                allPrompts.Add(new ModelContextProtocol.Protocol.Prompt
                {
                    Name = prompt.Name,
                    Description = prompt.Description ?? "No description available.",
                    Arguments = prompt.Arguments?.Select(arg => new PromptArgument
                    {
                        Name = arg.Name,
                        Description = arg.Description ?? "No description available.",
                        Required = arg.Required
                    }).ToList(),
                });
            }
        }

        return allPrompts;
    }

    internal static async ValueTask<GetPromptResult> GetPrompt(RequestContext<GetPromptRequestParams> request,
        CancellationToken token)
    {
        var name = request.Params.Name;

        if (ApiPrompts == null || !ApiPrompts.ContainsKey(name))
        {
            if (ExtendedPrompts == null || !ExtendedPrompts.ContainsKey(name))
            {
                return new GetPromptResult()
                {
                    Messages = new List<PromptMessage>() { }
                };
            }

            var arguments = request.Params.Arguments != null
                ? new Dictionary<string, JsonElement>(request.Params.Arguments)
                : new Dictionary<string, JsonElement>();
            return GetExtendedPrompt(name, arguments);
        }

        return GetApiPrompt(name);
    }

    private static GetPromptResult GetExtendedPrompt(string name, Dictionary<string, JsonElement> arguments)
    {
        if (ExtendedPrompts == null || !ExtendedPrompts.ContainsKey(name))
        {
            return new GetPromptResult
            {
                Messages = new List<PromptMessage>()
            };
        }

        var prompt = ExtendedPrompts[name];
        var messages = new List<PromptMessage>();

        if (prompt.Messages != null)
        {
            foreach (var msg in prompt.Messages)
            {
                var messageText = msg.Content is TextContentBlock textBlock ? textBlock.Text : string.Empty;
                if (prompt.Arguments != null && prompt.Arguments.Count > 0)
                {
                    foreach (var arg in prompt.Arguments)
                    {
                        if (arguments.ContainsKey(arg.Name))
                        {
                            var argValue = arguments[arg.Name].ToString();
                            messageText = messageText?.Replace($"{{{arg.Name}}}", argValue);
                        }
                    }
                }

                var newMsg = new PromptMessage
                {
                    Role = msg.Role,
                    Content = new TextContentBlock
                    {
                        Type = "text",
                        Text = messageText
                    }
                };


                messages.Add(newMsg);
            }
        }

        return new GetPromptResult
        {
            Messages = messages
        };
    }

    private static GetPromptResult GetApiPrompt(string name)
    {
        List<PromptMessage> messages = [];
        var prompt = ApiPrompts[name];

        if (prompt == null)
        {
            throw new KeyNotFoundException($"Prompt '{name}' not found.");
        }


        messages.Add(new PromptMessage()
        {
            Role = Role.User,
            Content = new TextContentBlock()
            {
                Type = "text",
                Text = prompt.Content
            }
        });

        return new GetPromptResult()
        {
            Messages = messages
        };
    }
}