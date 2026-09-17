# MCP Server

This README was created using the C# MCP server project template.
It demonstrates how you can easily create an MCP server using C# and publish it as a NuGet package.

The MCP server is built as a self-contained application and does not require the .NET runtime to be installed on the target machine.
However, since it is self-contained, it must be built for each target platform separately.
By default, the template is configured to build for:
* `win-x64`
* `win-arm64`
* `osx-arm64`
* `linux-x64`
* `linux-arm64`
* `linux-musl-x64`

If your users require more platforms to be supported, update the list of runtime identifiers in the project's `<RuntimeIdentifiers />` element.

See [aka.ms/nuget/mcp/guide](https://aka.ms/nuget/mcp/guide) for the full guide.

## Checklist before publishing to NuGet.org

- Test the MCP server locally using the steps below.
- Update the package metadata in the .csproj file, in particular the `<PackageId>`.
- Update `.mcp/server.json` to declare your MCP server's inputs.
  - See [configuring inputs](https://aka.ms/nuget/mcp/guide/configuring-inputs) for more details.
- Pack the project using `dotnet pack`.

The `bin/Release` directory will contain the package file (.nupkg), which can be [published to NuGet.org](https://learn.microsoft.com/nuget/nuget-org/publish-a-package).

## Configuration

Connections, query mode, and other server settings live in a per-user, per-machine config file, not in
the install directory:

* Windows: `%LocalAppData%\DataQueryMcpServer\appsettings.json`
* Linux/macOS: `$XDG_DATA_HOME/DataQueryMcpServer/appsettings.json` (falls back to `~/.local/share/...` /
  `~/Library/Application Support/...` per the platform's `SpecialFolder.LocalApplicationData`)

On first run, if this file doesn't exist yet, the server creates it from a bundled template that
includes a `LocalSqlServer` connection (Windows-integrated auth to `localhost`) and a `SampleSqlite`
connection pointing at a bundled demo database (`sample.db`, also copied into that same folder). Edit
the file to point at your own databases, or delete unwanted entries — the server never overwrites this
file once it exists.

This design keeps configuration independent of where the tool package is installed and of whatever
working directory the MCP client happens to launch the process from.

## Developing locally

To test this MCP server from source code (locally) without using a built MCP server package, you can configure your IDE to run the project directly using `dotnet run`.

```json
{
  "servers": {
    "DataQueryMcpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<PATH TO PROJECT DIRECTORY>"
      ]
    }
  }
}
```

Refer to the VS Code or Visual Studio documentation for more information on configuring and using MCP servers:

- [Use MCP servers in VS Code](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio](https://learn.microsoft.com/visualstudio/ide/mcp-servers)

## Testing the MCP Server

Once configured, ask your assistant something like `List the database connections available` or
`Show me the schema for the SampleSqlite connection`. It should call the `list_connections`,
`get_schema`, or `run_query` tool on the `DataQueryMcpServer` MCP server and show you the results. The
bundled `SampleSqlite` connection (see [Configuration](#configuration)) works out of the box with no
setup, so it's the fastest way to confirm the server is wired up correctly.

## Running the tests

```
dotnet test
```

runs the full suite, including integration tests that talk to real SQLite files and a real SQL Server
instance. CI only runs the subset that needs no external services:

```
dotnet test --filter "Category!=RequiresLocalSqlServer"
```

To run the SQL Server integration tests locally (`DataQueryToolsSqlServerIntegrationTests`), you need a
SQL Server instance reachable at `localhost` via Windows Integrated Security, with permission to
`CREATE DATABASE` / `DROP DATABASE` — matching the `LocalSqlServer` connection seeded into
`%LocalAppData%\DataQueryMcpServer\appsettings.json` (see [Configuration](#configuration)). Each test run
creates and drops its own throwaway database, so it won't touch existing data. These tests fail (rather
than skip) when no such instance is reachable, since they're meant for a developer machine that has one.

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`

## Using the MCP Server from NuGet.org

Once the MCP server package is published to NuGet.org, you can configure it in your preferred IDE. Both VS Code and Visual Studio use the `dnx` command to download and install the MCP server package from NuGet.org.

- **VS Code**: Create a `<WORKSPACE DIRECTORY>/.vscode/mcp.json` file
- **Visual Studio**: Create a `<SOLUTION DIRECTORY>\.mcp.json` file

For both VS Code and Visual Studio, the configuration file uses the following server definition:

```json
{
  "servers": {
    "DataQueryMcpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "<your package ID here>",
        "--version",
        "<your package version here>",
        "--yes"
      ]
    }
  }
}
```

## More information

.NET MCP servers use the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
