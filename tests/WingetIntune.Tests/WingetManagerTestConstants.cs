namespace WingetIntune.Tests;

internal class WingetManagerTestConstants
{
    internal const string wingetListNoInstalledPackage = @"No installed package found matching input criteria.";

    internal const string wingetListInstalledPackage = @"Name                   Id                  Version Source
----------------------------------------------------------
Notepad++ (64-bit x64) Notepad++.Notepad++ 8.5.4   winget";

    internal const string ohMyPoshOutput = @"Found Oh My Posh [JanDeDobbeleer.OhMyPosh]
Version: 18.7.0
Publisher: Jan De Dobbeleer
Publisher Url: https://github.com/JanDeDobbeleer/oh-my-posh/
Publisher Support Url: https://github.com/JanDeDobbeleer/oh-my-posh/issues
Author: Jan De Dobbeleer
Moniker: oh-my-posh
Description: Prompt theme engine for any shell
Homepage: https://ohmyposh.dev/
License: MIT
License Url: https://github.com/JanDeDobbeleer/oh-my-posh/raw/main/COPYING
Release Notes Url: https://github.com/JanDeDobbeleer/oh-my-posh/releases/tag/v18.7.0
Tags:
  console
  command-line
  shell
  command-prompt
  powershell
  wsl
  developer-tools
  utilities
  cli
  cmd
  ps
  terminal
  oh-my-posh
Installer:
  Installer Type: inno
  Installer Url: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.7.0/install-amd64.exe
  Installer SHA256: 071ceebaafbfbce77352ab2752301aa51938f2601d112574bedbf58773dbda25
  Release Date: 2023-09-04";

    internal const string ohMyPoshOutputFrench = @"
Encontrado Oh My Posh [JanDeDobbeleer.OhMyPosh]
Versión: 18.7.0
Editor: Jan De Dobbeleer
Dirección URL del editor: https://github.com/JanDeDobbeleer/oh-my-posh/
Dirección URL de soporte del editor: https://github.com/JanDeDobbeleer/oh-my-posh/issues
Autor: Jan De Dobbeleer
Moniker: oh-my-posh
Descripción: Prompt theme engine for any shell
Página principal: https://ohmyposh.dev/
Licencia: MIT
Dirección URL de la licencia: https://github.com/JanDeDobbeleer/oh-my-posh/raw/main/COPYING
Dirección URL de notas de la versión: https://github.com/JanDeDobbeleer/oh-my-posh/releases/tag/v18.7.0
Etiquetas:
  console
  command-line
  shell
  command-prompt
  powershell
  wsl
  developer-tools
  utilities
  cli
  cmd
  ps
  terminal
  oh-my-posh
Instalador:
  Tipo de instalador: inno
  Dirección URL del instalador: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.7.0/install-amd64.exe
  Instalador SHA256: 071ceebaafbfbce77352ab2752301aa51938f2601d112574bedbf58773dbda25
  Fecha de lanzamiento: 2023-09-04";

    internal const string powershellOutput = @"Found PowerShell [9MZ1SNWT0N5D]
Version: Unknown
Publisher: Microsoft Corporation
Publisher Url: https://github.com/powershell/powershell
Publisher Support Url: https://github.com/PowerShell/PowerShell/issues
Description:
  PowerShell is a task-based command-line shell and scripting language built on .NET.  PowerShell helps system administrators and power-users rapidly automate task that manage operating systems (Linux, macOS, and Windows) and processes.

  PowerShell commands let you manage computers from the command line.  PowerShell providers let you access data stores, such as the registry and certificate store, as easily as you access the file system.  PowerShell includes a rich expression parser and a fully developed scripting language.

  PowerShell is Open Source.  See https://github.com/powershell/powershell
License: ms-windows-store://pdp/?ProductId=9MZ1SNWT0N5D
Privacy Url: https://github.com/PowerShell/PowerShell#telemetry
Copyright: Microsoft Corporation
Agreements:
Category: Developer tools
Pricing: Free
Free Trial: No
Terms of Transaction: https://aka.ms/microsoft-store-terms-of-transaction
Seizure Warning: https://aka.ms/microsoft-store-seizure-warning
Store License Terms: https://aka.ms/microsoft-store-license

Installer:
  Installer Type: msstore
  Store Product Id: 9MZ1SNWT0N5D";

    internal const string ohMyPoshYaml = @"# Created using wingetcreate 1.5.1.0
# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.4.0.schema.json

PackageIdentifier: JanDeDobbeleer.OhMyPosh
PackageVersion: 18.3.3
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.4.0";

    internal const string ohMyPoshInstallYaml = @"# Created using wingetcreate 1.5.1.0
# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.4.0.schema.json

PackageIdentifier: JanDeDobbeleer.OhMyPosh
PackageVersion: 18.3.3
Platform:
- Windows.Desktop
MinimumOSVersion: 10.0.0.0
InstallerType: inno
InstallModes:
- interactive
- silent
- silentWithProgress
Installers:
- Architecture: x64
  Scope: machine
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-amd64.exe
  InstallerSha256: D2732860CFEDAD53F7B06962D27F6EFA9C4F529086EC02D16808C378974AE20A
  InstallerSwitches:
    Custom: /INSTALLER=winget /ALLUSERS
- Architecture: x64
  Scope: user
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-amd64.exe
  InstallerSha256: D2732860CFEDAD53F7B06962D27F6EFA9C4F529086EC02D16808C378974AE20A
  InstallerSwitches:
    Custom: /INSTALLER=winget /CURRENTUSER
- Architecture: x86
  Scope: machine
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-386.exe
  InstallerSha256: 974C596F7E97BEB7E3FFF3D37DE70CB24CCB3807303848678B04F0240620C8B8
  InstallerSwitches:
    Custom: /INSTALLER=winget /ALLUSERS
- Architecture: x86
  Scope: user
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-386.exe
  InstallerSha256: 974C596F7E97BEB7E3FFF3D37DE70CB24CCB3807303848678B04F0240620C8B8
  InstallerSwitches:
    Custom: /INSTALLER=winget /CURRENTUSER
- Architecture: arm64
  Scope: machine
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-arm64.exe
  InstallerSha256: D9F7A215233F7F2C6317EC5864BB70CE587CA4BAA39B78C124FCE6FCE5408F2F
  InstallerSwitches:
    Custom: /INSTALLER=winget /ALLUSERS
- Architecture: arm64
  Scope: user
  InstallerUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.3.3/install-arm64.exe
  InstallerSha256: D9F7A215233F7F2C6317EC5864BB70CE587CA4BAA39B78C124FCE6FCE5408F2F
  InstallerSwitches:
    Custom: /INSTALLER=winget /CURRENTUSER
ManifestType: installer
ManifestVersion: 1.4.0
ReleaseDate: 2023-08-09";

    internal const string ohMyPoshLocaleYaml = @"# Created using wingetcreate 1.5.1.0
# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.4.0.schema.json

PackageIdentifier: JanDeDobbeleer.OhMyPosh
PackageVersion: 18.3.3
PackageLocale: en-US
Publisher: Jan De Dobbeleer
PublisherUrl: https://github.com/JanDeDobbeleer/oh-my-posh/
PublisherSupportUrl: https://github.com/JanDeDobbeleer/oh-my-posh/issues
Author: Jan De Dobbeleer
PackageName: Oh My Posh
PackageUrl: https://ohmyposh.dev/
License: MIT
LicenseUrl: https://github.com/JanDeDobbeleer/oh-my-posh/raw/main/COPYING
ShortDescription: Prompt theme engine for any shell
Moniker: oh-my-posh
Tags:
- console
- command-line
- shell
- command-prompt
- powershell
- wsl
- developer-tools
- utilities
- cli
- cmd
- ps
- terminal
- oh-my-posh
ReleaseNotesUrl: https://github.com/JanDeDobbeleer/oh-my-posh/releases/tag/v18.3.3
ManifestType: defaultLocale
ManifestVersion: 1.4.0";

    // Adobe Acrobat Pro - a real-world package that uses InstallerType: zip with NestedInstallerType: exe
    internal const string adobeAcrobatYaml = @"PackageIdentifier: Adobe.Acrobat.Pro
PackageVersion: 26.001.21662
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.4.0";

    internal const string adobeAcrobatInstallYaml = @"PackageIdentifier: Adobe.Acrobat.Pro
PackageVersion: 26.001.21662
InstallerType: zip
NestedInstallerType: exe
NestedInstallerFiles:
- RelativeFilePath: Adobe Acrobat\setup.exe
Scope: machine
InstallModes:
- interactive
- silent
- silentWithProgress
InstallerSwitches:
  Silent: /sAll /rs /msi
  SilentWithProgress: /sPB /rs /msi
  Interactive: /msi
  Custom: EULA_ACCEPT=YES
ProductCode: '{AC76BA86-1033-FFFF-7760-BC15014EA700}'
AppsAndFeaturesEntries:
- DisplayName: Adobe Acrobat (64-bit)
  ProductCode: '{AC76BA86-1033-FFFF-7760-BC15014EA700}'
  InstallerType: msi
Installers:
- Architecture: x64
  InstallerUrl: https://trials.adobe.com/AdobeProducts/APRO/Acrobat_HelpX/win32/Acrobat_DC_Web_x64_WWMUI.zip
  InstallerSha256: C310F060D303CFE6A8EC8DFE60B403CF659C25B50C0C2AE65BC4A146142AEAB2
ManifestType: installer
ManifestVersion: 1.4.0";

    internal const string adobeAcrobatLocaleYaml = @"PackageIdentifier: Adobe.Acrobat.Pro
PackageVersion: 26.001.21662
PackageLocale: en-US
Publisher: Adobe
PublisherUrl: https://www.adobe.com/
PublisherSupportUrl: https://helpx.adobe.com/support.html
PackageName: Adobe Acrobat Pro
PackageUrl: https://www.adobe.com/acrobat/acrobat-pro.html
ShortDescription: Adobe Acrobat Pro PDF editor
ManifestType: defaultLocale
ManifestVersion: 1.4.0";
}
