using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Data;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Tools;
using DataQueryMcpServer.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Pin the content root to the executable's own directory rather than the process's current working
// directory (the generic host default), since MCP clients launch this as a stdio subprocess with an
// arbitrary cwd. This is what lets the bundled appsettings.json be found reliably.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Seed (on first run only) and load the per-user, per-machine config file from local app data. This
// is where connections live, independent of both the install location and the launching cwd.
var userConfigDirectory = UserConfigInitializer.GetUserConfigDirectory();
var seedDirectory = Path.Combine(AppContext.BaseDirectory, "Seed");
var userConfigPath = UserConfigInitializer.EnsureSeeded(userConfigDirectory, seedDirectory);
builder.Configuration.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);

builder.Services.AddOptions<DatabaseServerOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseServerOptions.SectionName));

builder.Services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<ISchemaReader, SqlServerSchemaReader>();
builder.Services.AddSingleton<ISchemaReader, SqliteSchemaReader>();
builder.Services.AddSingleton<IStatementClassifier, TSqlStatementClassifier>();
builder.Services.AddSingleton<IStatementClassifier, SqliteStatementClassifier>();

builder.Services.AddSingleton(sp =>
    new ProviderRegistry<IDbConnectionFactory>(sp.GetServices<IDbConnectionFactory>(), f => f.Provider));
builder.Services.AddSingleton(sp =>
    new ProviderRegistry<ISchemaReader>(sp.GetServices<ISchemaReader>(), r => r.Provider));
builder.Services.AddSingleton(sp =>
    new ProviderRegistry<IStatementClassifier>(sp.GetServices<IStatementClassifier>(), c => c.Provider));

builder.Services.AddSingleton<ISqlStatementValidator, SqlStatementValidator>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<DataQueryTools>();

await builder.Build().RunAsync();
