using MFSS.Abstractions;
using MFSS.Models;
using MFSS.Services;
using Moq;

namespace MFSS.Tests;

public class ThirdDbServiceTests
{
    private readonly Mock<ILogger> _logMock;
    private readonly ThirdDbConfig _config;

    public ThirdDbServiceTests()
    {
        _logMock = new Mock<ILogger>();
        _config = new ThirdDbConfig
        {
            Enabled = true,
            ConnectionString = "Server=localhost;Database=fake;Integrated Security=True;",
            UpdateQuery = "UPDATE table SET col = @url WHERE id = @id"
        };
    }

    [Fact]
    public async Task UpdateWithTransactionAsync_EmptyRecords_ReturnsZero()
    {
        var service = new ThirdDbService(_config, _logMock.Object);
        var result = await service.UpdateWithTransactionAsync(new List<(long Id, string Url)>());
        Assert.Equal((0, 0), result);
    }

    [Fact]
    public async Task UpdateWithTransactionAsync_CancelledToken_Throws()
    {
        var service = new ThirdDbService(_config, _logMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UpdateWithTransactionAsync(
                new List<(long Id, string Url)> { (1, "https://example.com") }, cts.Token));
    }
}
