# QuickMCP

Effortlessly Build and Serve Model Context Protocol (MCP) Servers with OpenAPI, Swagger, or Google Discovery Specifications using .NET.

## Introduction

QuickMCP is a powerful .NET toolkit designed to streamline the creation and deployment of Model Context Protocol (MCP) servers. It allows developers to quickly generate servers from OpenAPI, Swagger, or Google Discovery specifications, reducing boilerplate code and accelerating development.

## Features

- Generate .NET MCP servers from OpenAPI/Swagger/Google Discovery specifications
- **Build Claude Desktop Extensions (.mcpb)** with AI-powered metadata generation
- Highly configurable .NET library with CLI utility for rapid deployment
- Multiple configuration approaches (Configuration options or method chaining)
- Comprehensive authentication support:
  - API Key, Basic, Bearer Token, OAuth 2.0, Custom Header
  - Custom authentication implementation interface
- Generate Tools with Path filtering, HTTP customization, error handling, and logging
- Seamless integration with MCP clients like Claude Desktop
- Configuration file support and full async/await capabilities
- AI-powered metadata generation using Google Gemini for extension descriptions

## Installation

### CLI Installation
```bash
dotnet tool install -g QuickMCP.CLI
```

### Library Installation
```bash
dotnet add package QuickMCP
```

## Quick Start

### Basic CLI Usage
```bash
# Serve directly from OpenAPI specification
quickmcp serve --spec-url https://petstore.swagger.io/v2/swagger.json

# Build a configuration file
quickmcp build config --spec-url https://petstore.swagger.io/v2/swagger.json --output-path ./config

# Build a Claude Desktop extension (.mcpb) with AI-generated metadata
quickmcp build ce --spec-url https://api.example.com/swagger.json -m --author-name "Your Company"

# Build extension from existing config file
quickmcp build ce -c path/to/config.json -m -k YOUR_GEMINI_API_KEY

# Serve using a configuration file
quickmcp serve --config-path ./config/mcp_server_config.json

# Add authentication to your configuration
quickmcp build config --spec-url https://api.example.com/swagger.json --auth bearer

# Add a server configuration for quick access
quickmcp add server /path/to/config.json -n MyServer

# List available servers
quickmcp list server

# Serve installed server
quickmcp serve -i myServer

# Remove a stored server configuration
quickmcp delete server MyServer
```

### Library Integration Example
```csharp
// Create and configure a server
var serverInfoBuilder = McpServerInfoBuilder.ForOpenApi()
    .FromUrl("https://petstore.swagger.io/v2/swagger.json")
    .WithBaseUrl("https://petstore.swagger.io")
    .AddDefaultHeader("User-Agent", "QuickMCP Client")
    .AddAuthentication(new ApiKeyAuthenticator("your-api-key", "X-API-Key", "header"));

// Build server info
var serverInfo = await serverInfoBuilder.BuildAsync();

//Integrate with official MCP C# SDK
var hostBuilder = Host.CreateApplicationBuilder();

var mcpBuilder = hostBuilder.Services
    .AddMcpServer()
    .WithQuickMCP(mcpServerInfo)
    .WithStdioServerTransport();

//Run Server
await hostBuilder.Build().RunAsync();  
```

### Integration with MCP Clients
```json
{
  "mcpServers": {
    "petStore": {
      "command": "quickmcp",
      "args": ["serve", "-c", "path/to/config.json"]
    }
  }
}
```
or Use installed server
```json
{
  "mcpServers": {
    "petStore": {
      "command": "quickmcp",
      "args": ["serve", "-i", "server_id"]
    }
  }
}
```

## Documentation

For detailed documentation on the following topics, refer to the wiki:

- [Library Implementation Guide](https://github.com/gunpal5/QuickMCP/wiki/Library-Implementation-Guide)
- [Authentication Options](https://github.com/gunpal5/QuickMCP/wiki/Authentication-Options)
- [Configuration Methods](https://github.com/gunpal5/QuickMCP/wiki/Configuration-Methods)
- [CLI Usage](https://github.com/gunpal5/QuickMCP/wiki/CLI-Usage)
- [Claude Desktop Extension Builder](https://github.com/gunpal5/QuickMCP/wiki/Claude-Desktop-Extension-Builder)

## Contributing

We welcome contributions! Submit PR or Issues to contribute to the project.

## License

QuickMCP is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
