@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'SvRooij.WinTuner.CmdLets.dll'

    # Version number of this module.
    ModuleVersion = '0.1.0'

    # ID used to uniquely identify this module.
    GUID = 'd813f5e2-ef9a-44a3-8f14-d282c01b5ead'

    # Author of this module.
    Author = 'Stephan van Rooij'

    # Company or vendor that produced this module.
    CompanyName = 'Stephan van Rooij'

    Copyright = 'Stephan van Rooij 2024, licensed under GNU GPLv3'

    # Description of this module.
    Description = 'WinTuner by SvRooij, package and publish any apps from WinGet to Intune.'

    # Minimum version of the Windows PowerShell engine required by this module.
    PowerShellVersion = '5.1'

    # Minimum version of the .NET Framework required by this module.
    # DotNetFrameworkVersion = '4.7.2'

    # Processor architecture (None, X86, Amd64) supported by this module.
    # ProcessorArchitecture = 'None'

    # Modules that must be imported into the global environment prior to importing this module.
    # RequiredModules = @()

    # Assemblies that must be loaded prior to importing this module.
    # RequiredAssemblies = @(
    #     "Microsoft.Extensions.Logging.Abstractions.dll",
    #     "SvR.ContentPrep.dll",
    #     "System.Buffers.dll",
    #     "System.Memory.dll",
    #     "System.Numerics.Vectors.dll",
    #     "System.Runtime.CompilerServices.Unsafe.dll"
    # )

    # Script files (.ps1) that are run in the caller's environment prior to importing this module.
    # ScriptsToProcess = @()

    # Type files (.ps1xml) that are loaded into the session prior to importing this module.
    # TypesToProcess = @()

    # Format files (.ps1xml) that are loaded into the session prior to importing this module.
    # FormatsToProcess = @()

    # Modules to import as nested modules of the module specified in RootModule/ModuleToProcess.
    # NestedModules = @()

    # Functions to export from this module.
    # FunctionsToExport = @()

    # Cmdlets to export from this module.
    CmdletsToExport = @(
        "Deploy-WtWin32App",
        "New-IntuneWinPackage",
        "New-WtWingetPackage",
        "Unprotect-IntuneWinPackage"
    )

    # Variables to export from this module.
    # VariablesToExport = @()

    # Aliases to export from this module.
    # AliasesToExport = @()

    # List of all files included in this module.
    FileList = @(
        "SvRooij.WinTuner.CmdLets.dll",
        "WinTuner.psd1",
        "WinTuner.psm1",
        "SvRooij.WinTuner.CmdLets.dll-Help.xml",
        "WingetIntune.dll",
        "System.Management.Automation.dll"
    )

    # Private data to pass to the module specified in RootModule/ModuleToProcess.
    PrivateData = @{
        PSData = @{
            Tags = @('Intune', 'Win32', 'WinGet')

            LisenceUri = 'https://github.com/svrooij/WingetIntune/blob/main/LICENSE.txt'
            ProjectUri = 'https://wintuner.app/'
            ReleaseNotes = 'This module is still a work-in-progress. Changes might be made without notice.'
        }
    }

    # HelpInfo URI of this module.
    # HelpInfoURI = 'https://wintuner.app/'
}
