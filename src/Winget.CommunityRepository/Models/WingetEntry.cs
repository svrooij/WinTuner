namespace Winget.CommunityRepository.Models;

public class WingetEntry
{
    public string? Name { get; set; }
    public string? PackageId { get; set; }
    public string? Version { get; set; }
}

public class WingetEntryExtended : WingetEntry
{
    public string[]? Tags { get; set; }
    public DateTimeOffset? LastUpdate { get; set; }
}
