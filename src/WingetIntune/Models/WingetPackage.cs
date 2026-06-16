using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WingetIntune.Models;

public class WingetPackage
{
    public WingetPackage() { }
    internal WingetPackage(PackageInfo packageInfo, string packageFolder, string packageFilename)
    {
        PackageId = packageInfo.PackageIdentifier!;
        Version = packageInfo.Version!;
        PackageFolder = packageFolder;
        PackageFile = packageFilename;
    }
    /// <summary>
    /// The package id
    /// </summary>
    public string PackageId { get; set; }

    /// <summary>
    /// The version
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// The folder where the package is stored in
    /// </summary>
    public string PackageFolder { get; set; }

    /// <summary>
    /// The filename of the intunewin file
    /// </summary>
    public string PackageFile { get; set; }

    /// <summary>
    /// Installer filename
    /// </summary>
    public string? InstallerFile { get; set; }

    /// <summary>
    /// Installer arguments
    /// </summary>
    public string? InstallerArguments { get; set; }

    /// <summary>
    /// Extracted command, for Sandbox testing
    /// </summary>
    public string? InstallCommand { get; set; }
}
