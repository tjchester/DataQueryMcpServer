# DataQueryMcpServer

[![CI](https://github.com/tjchester/DataQueryMcpServer/actions/workflows/ci.yml/badge.svg)](https://github.com/tjchester/DataQueryMcpServer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

An [MCP](https://modelcontextprotocol.io/) server exposing read-only (by default) SQL query access to
SQL Server, Azure SQL, and SQLite. Point your AI assistant at a database, let it inspect the schema, and
have it run `SELECT` queries — without handing it write access unless you explicitly opt in.

## Tools

| Tool | Description |
| --- | --- |
| `list_connections` | Lists the configured connections (name, provider, description). Never exposes connection strings or credentials. |
| `get_schema` | Returns table and column metadata for a named connection. |
| `run_query` | Executes a SQL statement against a named connection and returns the results. |

## Safety by default

- **Read-only by default.** `run_query` rejects anything but `SELECT` statements unless the server is
  explicitly configured for read-write access (`Database:Mode`). This is enforced by a statement
  validator (a real T-SQL parser for SQL Server, a dedicated classifier for SQLite) — not a regex or an
  allow/deny keyword list — so multi-statement injection attempts (`SELECT 1; DROP TABLE ...`) and
  disguised writes (`EXEC xp_cmdshell ...`) are rejected too.
- **Row-capped and time-boxed.** `Database:MaxRows` and `Database:CommandTimeoutSeconds` bound every
  query's result size and runtime.
- **No credentials over the wire to the model.** `list_connections` returns names, providers, and
  descriptions only — never connection strings.

## Installing

Once published, the recommended way to use this server is via [`dnx`](https://aka.ms/nuget/mcp/guide),
which VS Code and Visual Studio both support for installing MCP servers straight from NuGet.org:

```json
{
  "servers": {
    "DataQueryMcpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": ["DataQueryMcpServer", "--version", "<package version>", "--yes"]
    }
  }
}
```

- **VS Code**: put this in `<WORKSPACE DIRECTORY>/.vscode/mcp.json`
- **Visual Studio**: put this in `<SOLUTION DIRECTORY>\.mcp.json`

To run it from source instead (for development, or before it's published), see
[`src/DataQueryMcpServer/README.md`](src/DataQueryMcpServer/README.md#developing-locally).

## Configuration

Connections, query mode, and other server settings live in a per-user, per-machine config file, not in
the install directory:

- **Windows**: `%LocalAppData%\DataQueryMcpServer\appsettings.json`
- **Linux/macOS**: `$XDG_DATA_HOME/DataQueryMcpServer/appsettings.json` (falls back to
  `~/.local/share/...` / `~/Library/Application Support/...` per the platform's
  `SpecialFolder.LocalApplicationData`)

On first run, if this file doesn't exist yet, the server creates it from a bundled template containing:

- `LocalSqlServer` — Windows-integrated auth to `localhost`
- `SampleSqlite` — a bundled demo database (`sample.db`, copied into the same folder), so there's
  something to query with zero setup

Edit the file to point at your own databases, or delete unwanted entries — the server never overwrites
this file once it exists. This keeps configuration independent of both the install location and
whatever working directory the MCP client happens to launch the process from.

Example connection entry:

```json
{
  "Database": {
    "Mode": "ReadOnly",
    "CommandTimeoutSeconds": 30,
    "MaxRows": 1000,
    "Connections": {
      "MyDatabase": {
        "Provider": "SqlServer",
        "ConnectionString": "Server=...;Database=...;...",
        "Description": "What this connection is for"
      }
    }
  }
}
```

`Provider` is `SqlServer` or `Sqlite`. Set `Mode` to `ReadWrite` to allow non-`SELECT` statements.

## Project layout

```
src/DataQueryMcpServer/    The MCP server (see its README for local dev and publishing details)
tests/DataQueryMcpServer.Tests/    Unit and integration tests
.github/workflows/ci.yml   CI: build + test on Windows, Linux, and macOS
```

## Running the tests

```
dotnet test
```

runs the full suite, including integration tests against real SQLite files and a real local SQL Server
instance. CI only runs the subset that needs no external services:

```
dotnet test --filter "Category!=RequiresLocalSqlServer"
```

To run the SQL Server integration tests locally, you need a SQL Server instance reachable at `localhost`
via Windows Integrated Security with permission to `CREATE DATABASE` / `DROP DATABASE` — see
[`src/DataQueryMcpServer/README.md`](src/DataQueryMcpServer/README.md#running-the-tests) for details.

## License

[MIT](LICENSE)
