using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Tests.Configuration;

public class UserConfigInitializerTests : IAsyncLifetime
{
    private string _seedDirectory = null!;
    private string _userConfigDirectory = null!;

    public Task InitializeAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dataquerymcp-seed-tests-{Guid.NewGuid():N}");
        _seedDirectory = Path.Combine(root, "seed");
        _userConfigDirectory = Path.Combine(root, "user");
        Directory.CreateDirectory(_seedDirectory);

        File.WriteAllText(Path.Combine(_seedDirectory, "sample.db"), "not a real sqlite file, just seed test content");
        File.WriteAllText(Path.Combine(_seedDirectory, "appsettings.json"), """
            {
              "Database": {
                "Mode": "ReadOnly",
                "Connections": {
                  "SampleSqlite": {
                    "Provider": "Sqlite",
                    "ConnectionString": "Data Source=sample.db",
                    "Description": "seed template"
                  }
                }
              }
            }
            """);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var root = Path.GetDirectoryName(_seedDirectory)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void EnsureSeeded_UserConfigMissing_CopiesSeedFilesAndRewritesSampleDbPath()
    {
        var configPath = UserConfigInitializer.EnsureSeeded(_userConfigDirectory, _seedDirectory);

        configPath.Should().Be(Path.Combine(_userConfigDirectory, "appsettings.json"));
        File.Exists(configPath).Should().BeTrue();

        var expectedSampleDbPath = Path.Combine(_userConfigDirectory, "sample.db");
        File.Exists(expectedSampleDbPath).Should().BeTrue();

        var written = File.ReadAllText(configPath);
        written.Should().Contain($"Data Source={expectedSampleDbPath}".Replace(@"\", @"\\"));
    }

    [Fact]
    public void EnsureSeeded_UserConfigAndSampleDbAlreadyExist_LeavesThemUntouched()
    {
        Directory.CreateDirectory(_userConfigDirectory);
        var existingConfigPath = Path.Combine(_userConfigDirectory, "appsettings.json");
        const string existingConfigContent = """{ "Database": { "Connections": {} } }""";
        File.WriteAllText(existingConfigPath, existingConfigContent);

        var existingSampleDbPath = Path.Combine(_userConfigDirectory, "sample.db");
        const string existingSampleDbContent = "user's own sample.db, must not be replaced";
        File.WriteAllText(existingSampleDbPath, existingSampleDbContent);

        UserConfigInitializer.EnsureSeeded(_userConfigDirectory, _seedDirectory);

        File.ReadAllText(existingConfigPath).Should().Be(existingConfigContent);
        File.ReadAllText(existingSampleDbPath).Should().Be(existingSampleDbContent);
    }

    [Fact]
    public void EnsureSeeded_UserConfigDirectoryMissing_CreatesIt()
    {
        Directory.Exists(_userConfigDirectory).Should().BeFalse();

        UserConfigInitializer.EnsureSeeded(_userConfigDirectory, _seedDirectory);

        Directory.Exists(_userConfigDirectory).Should().BeTrue();
    }
}
