using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataQueryMcpServer.Configuration;

/// <summary>
/// Seeds a per-user, per-machine configuration file under local app data on first run, so the
/// server has working connections regardless of the working directory the MCP client launches it
/// from and regardless of where the tool package itself is installed.
/// </summary>
public static class UserConfigInitializer
{
    private const string AppFolderName = "DataQueryMcpServer";
    private const string ConfigFileName = "appsettings.json";
    private const string SampleDbFileName = "sample.db";

    public static string GetUserConfigDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    /// <summary>
    /// Ensures <paramref name="userConfigDirectory"/> exists and contains a config file and sample
    /// database, copying them from <paramref name="seedDirectory"/> the first time only. Returns the
    /// path to the user config file.
    /// </summary>
    public static string EnsureSeeded(string userConfigDirectory, string seedDirectory)
    {
        Directory.CreateDirectory(userConfigDirectory);

        var sampleDbPath = Path.Combine(userConfigDirectory, SampleDbFileName);
        var seedSampleDbPath = Path.Combine(seedDirectory, SampleDbFileName);
        if (!File.Exists(sampleDbPath) && File.Exists(seedSampleDbPath))
        {
            File.Copy(seedSampleDbPath, sampleDbPath);
        }

        var configPath = Path.Combine(userConfigDirectory, ConfigFileName);
        var seedConfigPath = Path.Combine(seedDirectory, ConfigFileName);
        if (!File.Exists(configPath) && File.Exists(seedConfigPath))
        {
            var template = JsonNode.Parse(File.ReadAllText(seedConfigPath))
                ?? throw new InvalidOperationException($"Seed config at '{seedConfigPath}' is not valid JSON.");

            // Point the bundled demo connection at the copy of sample.db that now lives next to this
            // config file, so it resolves to an absolute path independent of the process's working directory.
            var sampleSqlite = template["Database"]?["Connections"]?["SampleSqlite"]?.AsObject();
            if (sampleSqlite is not null)
            {
                sampleSqlite["ConnectionString"] = $"Data Source={sampleDbPath}";
            }

            File.WriteAllText(configPath, template.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        return configPath;
    }
}
