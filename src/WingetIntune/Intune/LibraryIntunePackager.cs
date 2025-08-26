using Microsoft.Extensions.Logging;
using SvRooij.ContentPrep;
using WingetIntune.Interfaces;
using WingetIntune.Models;

namespace WingetIntune.Intune;

internal partial class LibraryIntunePackager : IIntunePackager
{
    private readonly Packager packager;
    private readonly ILogger logger;

    public LibraryIntunePackager(ILogger<Packager> logger)
    {
        this.packager = new Packager(logger);
        this.logger = logger;
    }

    public async Task<string> CreatePackage(string inputFolder, string outputFolder, string installerFilename, PackageInfo? packageInfo = null, bool partialPackage = false, CancellationToken cancellationToken = default)
    {
        LogCreatingPackage(inputFolder);
        var details = new SvRooij.ContentPrep.Models.ApplicationDetails
        {
            Name = packageInfo?.DisplayName ?? packageInfo?.PackageIdentifier,
        };
        if (packageInfo?.MsiProductCode != null && packageInfo.MsiVersion != null)
        {
            details.MsiInfo = new SvRooij.ContentPrep.Models.MsiInfo
            {
                MsiProductCode = packageInfo.MsiProductCode,
                MsiProductVersion = packageInfo.MsiVersion,
            };
        }

        if (partialPackage)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            var outputFilename = Path.GetFileNameWithoutExtension(installerFilename) + ".partial.intunewin";

            using var partialFile = new FileStream(Path.Combine(outputFolder, outputFilename), FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, true);

            var info = await packager.CreateUploadablePackage(inputFolder, partialFile, details, cancellationToken);
            await partialFile.FlushAsync(cancellationToken);
            await File.AppendAllTextAsync(Path.Combine(outputFolder, "metadata.xml"), info!.ToXml(), cancellationToken);

            return outputFilename;
        }


        _ = await packager.CreatePackage(inputFolder, Path.Combine(inputFolder, installerFilename), outputFolder, details, cancellationToken);
        return Path.GetFileNameWithoutExtension(installerFilename) + ".intunewin";
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Creating Intune package from {InputFolder}")]
    private partial void LogCreatingPackage(string InputFolder);
}
