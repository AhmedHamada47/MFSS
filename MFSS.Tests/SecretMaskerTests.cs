using MFSS.Services;

namespace MFSS.Tests;

public class SecretMaskerTests
{
    [Fact]
    public void MaskConnectionString_MasksPassword()
    {
        var input = "Server=localhost;Database=test;User=root;******;";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("******", result);
        Assert.DoesNotContain("supersecret", result);
    }

    [Fact]
    public void MaskConnectionString_MasksPwd()
    {
        var input = "Server=localhost;******;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("******", result);
        Assert.DoesNotContain("mysecret", result);
    }

    [Fact]
    public void MaskConnectionString_CaseInsensitive()
    {
        var input = "Server=host;******;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskConnectionString_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("(empty)", SecretMasker.MaskConnectionString(""));
    }

    [Fact]
    public void MaskConnectionString_Null_ReturnsEmpty()
    {
        Assert.Equal("(empty)", SecretMasker.MaskConnectionString(null!));
    }

    [Fact]
    public void MaskConnectionString_NoPassword_ReturnsOriginal()
    {
        var input = "Server=localhost;Database=test;User=root;";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Equal(input, result);
    }
}
