using MFSS.Models;
using MFSS.Services;

namespace MFSS.Tests;

public class EnvConfigResolverTests
{
    [Fact]
    public void Resolve_ReplacesEnvVariables()
    {
        Environment.SetEnvironmentVariable("TEST_MFSS_HOST", "myhost.com");
        Environment.SetEnvironmentVariable("TEST_MFSS_USER", "admin");

        try
        {
            var sourceDb = new SourceDbConfig
            {
                ConnectionString = "Server=${TEST_MFSS_HOST};User=${TEST_MFSS_USER};"
            };
            var destDb = new DestinationDbConfig { ConnectionString = "" };
            var thirdDb = new ThirdDbConfig { ConnectionString = "" };
            var srcFs = new FileSystemConfig();
            var destFs = new FileSystemConfig();

            EnvConfigResolver.ResolveAll(sourceDb, destDb, thirdDb, srcFs, destFs);

            Assert.Equal("Server=myhost.com;User=admin;", sourceDb.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_MFSS_HOST", null);
            Environment.SetEnvironmentVariable("TEST_MFSS_USER", null);
        }
    }

    [Fact]
    public void Resolve_KeepsPlaceholderIfEnvNotSet()
    {
        var sourceDb = new SourceDbConfig
        {
            ConnectionString = "Server=${NONEXISTENT_VAR_12345};"
        };
        var destDb = new DestinationDbConfig { ConnectionString = "" };
        var thirdDb = new ThirdDbConfig { ConnectionString = "" };
        var srcFs = new FileSystemConfig();
        var destFs = new FileSystemConfig();

        EnvConfigResolver.ResolveAll(sourceDb, destDb, thirdDb, srcFs, destFs);

        Assert.Equal("Server=${NONEXISTENT_VAR_12345};", sourceDb.ConnectionString);
    }

    [Fact]
    public void Resolve_HandlesEmptyStrings()
    {
        var sourceDb = new SourceDbConfig { ConnectionString = "" };
        var destDb = new DestinationDbConfig { ConnectionString = "" };
        var thirdDb = new ThirdDbConfig { ConnectionString = "" };
        var srcFs = new FileSystemConfig();
        var destFs = new FileSystemConfig();

        EnvConfigResolver.ResolveAll(sourceDb, destDb, thirdDb, srcFs, destFs);

        Assert.Equal("", sourceDb.ConnectionString);
    }

    [Fact]
    public void Resolve_ResolvesAccessKeys()
    {
        Environment.SetEnvironmentVariable("TEST_AWS_KEY", "AKIA12345");

        try
        {
            var sourceDb = new SourceDbConfig { ConnectionString = "" };
            var destDb = new DestinationDbConfig { ConnectionString = "" };
            var thirdDb = new ThirdDbConfig { ConnectionString = "" };
            var srcFs = new FileSystemConfig();
            var destFs = new FileSystemConfig { AccessKey = "${TEST_AWS_KEY}", SecretKey = "plain-value" };

            EnvConfigResolver.ResolveAll(sourceDb, destDb, thirdDb, srcFs, destFs);

            Assert.Equal("AKIA12345", destFs.AccessKey);
            Assert.Equal("plain-value", destFs.SecretKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_AWS_KEY", null);
        }
    }
}
