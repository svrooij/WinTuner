﻿namespace WingetIntune.Models;

internal static class WingetInstallerExtensions
{
    public static Winget.CommunityRepository.Models.WingetInstaller? SingleOrDefault(this IList<Winget.CommunityRepository.Models.WingetInstaller>? installers, InstallerType installerType, Architecture architecture, InstallerContext installerContext)
    {
        if (installers is null || !installers.Any()) { return null; }
        return installers.singleOrDefault(installerType, architecture, installerContext, "en-US")
            ?? installers.singleOrDefault(installerType, architecture, installerContext);
    }

    private static Winget.CommunityRepository.Models.WingetInstaller? singleOrDefault(this IList<Winget.CommunityRepository.Models.WingetInstaller> installers, InstallerType installerType, Architecture architecture, InstallerContext installerContext, string? locale = null)
    {
        return installers.FirstOrDefault(i =>
            (i.ParseInstallerType() == installerType || installerType == InstallerType.Unknown)
            && (i.InstallerArchitecture() == architecture || architecture == Architecture.Unknown)
            && (i.ParseInstallerContext() == installerContext || installerContext == InstallerContext.Unknown)
            && (string.IsNullOrWhiteSpace(locale) || i.InstallerLocale == locale));
    }



    public static InstallerContext ParseInstallerContext(this Winget.CommunityRepository.Models.WingetInstaller installer)
    {
        return EnumParsers.ParseInstallerContext(installer.Scope);
    }

    public static Architecture InstallerArchitecture(this Winget.CommunityRepository.Models.WingetInstaller installer)
    {
        return EnumParsers.ParseArchitecture(installer.Architecture);
    }

    public static InstallerType ParseInstallerType(this Winget.CommunityRepository.Models.WingetInstaller installer)
    {
        return EnumParsers.ParseInstallerType(installer.InstallerType);
    }
}
