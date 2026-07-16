using MFSS.Services;

namespace MFSS.Tests;

public class SecretMaskerTests
{
    [Fact]
    public void MaskConnectionString_MasksPassword()
    {
        var input = "Server=localhost;Database=test;User=root;password=supersecret;";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("password=****", result);
        Assert.DoesNotContain("supersecret", result);
    }

    [Fact]
    public void MaskConnectionString_MasksPwd()
    {
        var input = "Server=localhost;pwd=mysecret;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("pwd=****", result);
        Assert.DoesNotContain("mysecret", result);
    }

    [Fact]
    public void MaskConnectionString_CaseInsensitive()
    {
        var input = "Server=host;PASSWORD=Secret123;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("PASSWORD=****", result);
        Assert.DoesNotContain("Secret123", result);
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

    [Fact]
    public void MaskConnectionString_MultiplePasswords_MasksAll()
    {
        var input = "Server=localhost;password=secret1;pwd=secret2;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("password=****", result);
        Assert.Contains("pwd=****", result);
        Assert.DoesNotContain("secret1", result);
        Assert.DoesNotContain("secret2", result);
    }

    [Fact]
    public void MaskConnectionString_PasswordAtEnd_MasksCorrectly()
    {
        var input = "Server=localhost;Database=test;password=endsecret";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("password=****", result);
        Assert.DoesNotContain("endsecret", result);
    }

    [Fact]
    public void MaskConnectionString_PasswordWithSpaces_MasksCorrectly()
    {
        var input = "Server=localhost;password = spacedsecret;Database=db";
        var result = SecretMasker.MaskConnectionString(input);
        Assert.Contains("password=****", result);
        Assert.DoesNotContain("spacedsecret", result);
    }
}
