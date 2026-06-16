namespace WingetIntune.Implementations;

internal class WingetManagerKeys
{
    internal const string WingetPrefixEn = "Found ";
    internal const string WingetPrefixFr = "Encontrado ";
    public string Prefix { get; set; }
    public string Version { get; set; }
    public string Publisher { get; set; }
    public string PublisherUrl { get; set; }
    public string InformationUrl { get; set; }
    public string SupportUrl { get; set; }
    public string Description { get; set; }
    public string InstallerType { get; set; }
    public string InstallerUrl { get; set; }
    public string InstallerSha256 { get; set; }

    public static WingetManagerKeys English() => new WingetManagerKeys
    {
        Prefix = WingetPrefixEn,
        Version = "Version",
        Publisher = "Publisher",
        PublisherUrl = "Publisher Url",
        InformationUrl = "Homepage",
        SupportUrl = "Publisher Support Url",
        Description = "Description",
        InstallerType = "Installer Type",
        InstallerUrl = "Installer Url",
        InstallerSha256 = "Installer SHA256",
    };

    public static WingetManagerKeys French() => new WingetManagerKeys
    {
        Prefix = WingetPrefixFr,
        Version = "Versión",
        Publisher = "Editor",
        PublisherUrl = "Dirección URL del editor",
        InformationUrl = "Página principal",
        SupportUrl = "Dirección URL de soporte del editor",
        Description = "Descripción",
        InstallerType = "Tipo de instalador",
        InstallerUrl = "Dirección URL del instalador",
        InstallerSha256 = "Instalador SHA256"
    };
}
