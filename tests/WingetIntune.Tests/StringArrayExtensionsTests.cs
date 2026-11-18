using WingetIntune.Extensions;

namespace WingetIntune.Tests;

public class StringArrayExtensionsTests
{
    [Fact]
    public void ContainsIgnoreCase_ReturnsTrueWhenPresent()
    {
        var source = new[] { "Alpha", "Beta" };
        Assert.True(source.ContainsIgnoreCase("beta"));
    }

    [Fact]
    public void ContainsIgnoreCase_ReturnsFalseWhenAbsent()
    {
        var source = new[] { "Alpha", "Beta" };
        Assert.False(source.ContainsIgnoreCase("Gamma"));
    }

    [Fact]
    public void GetValue_ReturnsValueAfterColon()
    {
        var source = new[] { "Name: WinTuner", "Version: 1.0" };
        Assert.Equal("WinTuner", source.GetValue("Name"));
    }

    [Fact]
    public void GetValue_ReturnsNullWhenKeyMissing()
    {
        var source = new[] { "Name: WinTuner" };
        Assert.Null(source.GetValue("Version"));
    }

    [Fact]
    public void GetUri_ReturnsUriWhenValid()
    {
        var source = new[] { "Url: https://example.com" };
        Assert.Equal(new Uri("https://example.com"), source.GetUri("Url"));
    }

    [Fact]
    public void GetUri_ReturnsNullWhenMissing()
    {
        var source = new[] { "Name: WinTuner" };
        Assert.Null(source.GetUri("Url"));
    }

    [Fact]
    public void GetMultiLineValue_ReturnsJoinedLines()
    {
        var source = new[] { "Description: First", "  second line", "  third line", "Other: value" };
        var result = source.GetMultiLineValue("Description");
        Assert.Contains("First", result);
        Assert.Contains("second line", result);
        Assert.Contains("third line", result);
    }

    [Fact]
    public void GetMultiLineValue_ReturnsNullWhenKeyMissing()
    {
        var source = new[] { "Other: value" };
        Assert.Null(source.GetMultiLineValue("Description"));
    }
}
