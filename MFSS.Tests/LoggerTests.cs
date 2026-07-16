using MFSS.Services;

namespace MFSS.Tests;

public class LoggerTests
{
    [Fact]
    public void Constructor_CreatesLogFile()
    {
        var name = "test-create-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        Assert.True(File.Exists(logger.LogPath));
    }

    [Fact]
    public void LogPath_ContainsMigrationName()
    {
        var name = "my-test-migration";
        using var logger = new Logger(name);
        Assert.Contains(name, Path.GetFileName(logger.LogPath));
    }

    [Fact]
    public void LogPath_IsInLogsDirectory()
    {
        using var logger = new Logger("test-path");
        Assert.Contains("logs", logger.LogPath);t
    }

    [Fact]
    public void Info_WritesToFile()
    {
        var name = "test-info-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Info("Test info message");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Test info message", content);
    }

    [Fact]
    public void Error_WritesToFile()
    {
        var name = "test-error-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Error("Test error message");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Test error message", content);
    }

    [Fact]
    public void Warning_WritesToFile()
    {
        var name = "test-warning-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Warning("Test warning message");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Test warning message", content);
    }

    [Fact]
    public void Success_WritesToFile()
    {
        var name = "test-success-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Success("Test success message");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Test success message", content);
    }

    [Fact]
    public void Header_WritesToFile()
    {
        var name = "test-header-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Header("Test header message");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Test header message", content);
    }

    [Fact]
    public void MultipleWrites_AllAppearInFile()
    {
        var name = "test-multi-" + Guid.NewGuid().ToString("N")[..8];
        using var logger = new Logger(name);
        logger.Info("Line 1");
        logger.Warning("Line 2");
        logger.Error("Line 3");
        logger.Dispose();
        var content = File.ReadAllText(logger.LogPath);
        Assert.Contains("Line 1", content);
        Assert.Contains("Line 2", content);
        Assert.Contains("Line 3", content);
    }
}
