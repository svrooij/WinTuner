using Svrooij.PowerShell.DI;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Search for packages in WinGet</para>
/// <para type="description">Search for WinGet packages, but faster. This uses the an [online index](https://wintuner.app/docs/related/winget-package-index).</para>
/// </summary>
/// <psOrder>9</psOrder>
/// <parameterSet>
/// <para type="name">SearchQuery</para>
/// <para type="description">Filter packages based on search query. Used as `contains` on name, id and tags.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">All</para>
/// <para type="description">Return all packages, **be carefull** 7000+ packages is a large collection!</para>
/// </parameterSet>
/// <example>
/// <para type="name">Search for `fire`</para>
/// <para type="description">Searching in the online index.</para>
/// <code>Search-WtWinGetPackage fire</code>
/// </example>
/// <example>
/// <para type="name">All packages updated after</para>
/// <para type="description">All packages updated after a specific datetime `2025-02-25` in this case</para>
/// <code>Search-WtWinGetPackage -All -UpdatedAfter "2025-02-25" | Format-Table -Property PackageId,Version,LastUpdate</code>
/// </example>
[Cmdlet(VerbsCommon.Search, "WtWinGetPackage", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Search-WtWingetPackage")]
[OutputType(typeof(Winget.CommunityRepository.Models.WingetEntryExtended))]
[GenerateBindings]
public partial class SearchWtWinGetPackage : DependencyCmdlet<Startup>
{
    /// <summary>
    /// Filter packages by query string, used as `contains` on package id, name and tags.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(SearchQuery),
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Search query, used in wildcard search")]
    [Alias("PackageId", "Name")]
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Show all packages, be care full 7000+ pacakges is a lot.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(All),
        HelpMessage = "Specify you want all packages returned")]
    public SwitchParameter All { get; set; } = false;

    /// <summary>
    /// Only return packages updated after this date
    /// </summary>
    [Parameter(
    Mandatory = false,
    Position = 1,
    ParameterSetName = nameof(All),
    HelpMessage = "Only return packages updated after this date")]
    [Parameter(
    Mandatory = false,
    Position = 1,
    ParameterSetName = nameof(SearchQuery),
    HelpMessage = "Only return packages updated after this date")]
    public DateTime? UpdatedAfter { get; set; }

    [ServiceDependency(Required = true)]
    private Winget.CommunityRepository.WingetRepository wingetRepository;

    /// <inheritdoc />
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (ParameterSetName == nameof(SearchQuery) && (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length <= 1))
        {
            throw new ArgumentException("Search query is required, and has to be at least 2 chars");
        }
        var packages = await wingetRepository.SearchPackage(SearchQuery, cancellationToken);

        if (packages.Any() && UpdatedAfter.HasValue)
        {
            packages = packages.Where(p => p.LastUpdate > UpdatedAfter).ToArray();
        }

        WriteObject(packages, true);
    }
}
