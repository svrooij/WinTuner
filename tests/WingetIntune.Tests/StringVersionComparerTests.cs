using WingetIntune.Models;

namespace WingetIntune.Tests;

public class StringVersionComparerTests
{
    [Fact]
    public void Compare_ValidVersions_ComparesCorrectly()
    {
        var comparer = new StringVersionComparer();
        Assert.True(comparer.Compare("1.2.3", "1.2.4") < 0);
        Assert.True(comparer.Compare("2.0.0", "1.9.9") > 0);
        Assert.Equal(0, comparer.Compare("1.0.0", "1.0.0"));
    }

    [Fact]
    public void Compare_Nulls_Handled()
    {
        var comparer = new StringVersionComparer();
        Assert.Equal(0, comparer.Compare(null, null));
        Assert.True(comparer.Compare(null, "1.0") < 0);
        Assert.True(comparer.Compare("1.0", null) > 0);
    }

    [Fact]
    public void Compare_InvalidVersions_FallsBackToString()
    {
        var comparer = new StringVersionComparer();
        Assert.True(comparer.Compare("abc", "def") < 0);
    }
}