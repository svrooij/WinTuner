namespace WingetIntune.Models;

internal static class WingetInstallerExtensions
{
    public static Winget.CommunityRepository.Models.WingetInstaller? SingleOrDefault(this IList<Winget.CommunityRepository.Models.WingetInstaller>? installers, InstallerType installerType, Architecture architecture, InstallerContext installerContext, string? locale = null)
    {
        if (installers is null || !installers.Any()) { return null; }
        return installers.singleOrDefault(installerType, architecture, installerContext, locale ?? "en-US")
            ?? installers.singleOrDefault(installerType, architecture, installerContext);
    }

    private static Winget.CommunityRepository.Models.WingetInstaller? singleOrDefault(this IList<Winget.CommunityRepository.Models.WingetInstaller> installers, InstallerType installerType, Architecture architecture, InstallerContext installerContext, string? locale = null)
    {
        return installers.FirstOrDefault(i =>
            InstallerTypeMatches(i, installerType)
            && (i.InstallerArchitecture() == architecture || (architecture == Architecture.Unknown && i.InstallerArchitecture() == Architecture.X64))
            && (i.ParseInstallerContext() == installerContext || installerContext == InstallerContext.Unknown)
            && (string.IsNullOrWhiteSpace(locale) || i.InstallerLocale == locale));
    }

    private static bool InstallerTypeMatches(Winget.CommunityRepository.Models.WingetInstaller installer, InstallerType requestedType)
    {
        if (requestedType == InstallerType.Unknown)
            return true;
        if (installer.ParseInstallerType() == requestedType)
            return true;
        // For zip installers, also match by NestedInstallerType (e.g., zip + wix matches a Wix preference)
        return installer.ParseInstallerType() == InstallerType.Zip
            && !string.IsNullOrEmpty(installer.NestedInstallerType)
            && EnumParsers.ParseInstallerType(installer.NestedInstallerType) == requestedType;
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
