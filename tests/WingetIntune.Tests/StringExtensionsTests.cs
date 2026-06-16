using WingetIntune.Models;
namespace WingetIntune.Tests;

public class StringExtensionsTests
{
    [Fact]
    public void ExtractPackageIdAndSourceFromNotes_WhenNotesIsNull_ReturnsNull()
    {
        // Arrange
        string? notes = null;
        // Act
        var result = notes.ExtractPackageIdAndSourceFromNotes();
        // Assert
        Assert.Null(result.Item1);
        Assert.Null(result.Item2);
    }

    [Fact]
    public void ExtractPackageIdAndSourceFromNotes_WhenNotesContainsOldNotesFormat_ReturnsPackageIdAndSource()
    {
        // Arrange
        string notes = "   dadfdafdf [WingetIntune|winget|Microsoft.AzureCLI]    $%#%$@";
        // Act
        var result = notes.ExtractPackageIdAndSourceFromNotes();
        // Assert
        Assert.Equal("Microsoft.AzureCLI", result.Item1);
        Assert.Equal("winget", result.Item2);
    }

    [Fact]
    public void ExtractPackageIdAndSourceFromNotes_WhenNotesContainsWinTunerNotesFormat_ReturnsPackageIdAndSource()
    {
        // Arrange
        string notes = "   dadfdafdf [WinTuner|winget|Microsoft.AzureCLI]    $%#%$@";
        // Act
        var result = notes.ExtractPackageIdAndSourceFromNotes();
        // Assert
        Assert.Equal("Microsoft.AzureCLI", result.Item1);
        Assert.Equal("winget", result.Item2);
    }

    [Fact]
    public void ExtractPackageIdAndSourceFromNotes_WhenNotesContainsInvalidFormat_ReturnsNull()
    {
        // Arrange
        string notes = "   dadfdafdf [WinTuner|winget]    $%#%$@";
        // Act
        var result = notes.ExtractPackageIdAndSourceFromNotes();
        // Assert
        Assert.Null(result.Item1);
        Assert.Null(result.Item2);
    }
}
