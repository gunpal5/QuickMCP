using QuickMCP.CLI.Commands;
using QuickMCP.CLI.Commands.Add;
using QuickMCP.CLI.Commands.Build;
using QuickMCP.CLI.Commands.Delete;
using QuickMCP.CLI.Commands.List;
using QuickMCP.CLI.Commands.Serve;
using Spectre.Console.Cli;

var commandApp = new CommandApp();

commandApp.Configure(app =>
{
    app.SetApplicationName("quickmcp");
    app.AddCommand<ServerCommand>("serve").WithAlias("server").WithExample("serve","-c mcp_server_config.json");

    app.AddBranch<BuildCommandSettings>("build", build =>
    {
        build.SetDescription("Build a MCP server configuration file and more");
        build.AddCommand<BuildConfigCommand>("config").WithAlias("config-file").WithAlias("config-file");
        build.AddCommand<BuildSpecCommand>("spec").WithExample("build spec", "-d url_to_documentation");
        build.AddCommand<BuildClaudeExtensionCommand>("ce")
            .WithAlias("claude-extension")
            .WithAlias("claude")
            .WithDescription("Build a Claude Desktop extension (.mcpb)")
            .WithExample("build ce", "-s https://api.example.com/swagger.json -n MyAPI");
    });
    
    app.AddBranch<ListCommandSettings>("list", list =>
    {
        list.SetDescription("List available configuration options such as authenticators");
        list.AddCommand<ListAuthenticatorsCommand>("auth").WithAlias("authenticator").WithAlias("authenticators");
        list.AddCommand<ListServerCommand>("server");
    });

    app.AddBranch<AddCommandSettings>("add", add =>
    {
        add.SetDescription("Add a new configuration option e.g. server");
        add.AddCommand<AddServerCommand>("server").WithExample("add server", "/path/to/config.json -n MyServer");
    });
    
    app.AddBranch<DeleteCommandSettings>("delete", add =>
    {
        add.SetDescription("Add a new configuration option e.g. server");
        add.AddCommand<DeleteServerCommand>("server").WithExample("add server", "/path/to/config.json -n MyServer");
    });
});
return await commandApp.RunAsync(args).ConfigureAwait(false);
