# WinTuner - Winget Command script https://wintuner.app
# 
# Script parameters in `{parameter_name}`
# packageId - The package id of the application to be detected
# action - The action to be performed (install, uninstall, upgrade) used for Transcript name
# success - The string to look for in the output to determine success
# message - The message to be displayed on success
# The command is below the function, because it can contain calls to the function, and powershell does not like it otherwise
# command - The command to be executed, e.g. $(Get-WingetCmd) install --id {packageId} --exact

# --------------------------- Start parameters -------------------------------
$packageId = "{packageId}"
$action = "{action}"
$success = "{success}"
$message = "{message}"
# --------------------------- End parameters ---------------------------------

Start-Transcript -Path "$env:ProgramData\Microsoft\IntuneManagementExtension\Logs\$packageId-$action.log" -Force
Write-Host "Starting $packageId $action"

Function Get-WingetCmd {

    $WingetCmd = $null
    #Get WinGet Path

    try {
        # Get Admin Context Winget Location
        $WingetInfo = (Get-Item "$($env:ProgramFiles)\WindowsApps\Microsoft.DesktopAppInstaller_*_8wekyb3d8bbwe\winget.exe").VersionInfo | Sort-Object -Property FileVersionRaw
        # if multiple versions, pick most recent one
        $WingetCmd = $WingetInfo[-1].FileName
    }
    catch {
        #Get User context Winget Location
        if (Test-Path "$env:LocalAppData\Microsoft\WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe")
        {
            $WingetCmd ="$env:LocalAppData\Microsoft\WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe"
        } else {
            Write-Host "winget not detected"
            Exit 1
        }
    }
    Write-Host "Winget location: $WingetCmd"
    return $WingetCmd
}
# Command parameter can contain calls to the function, so it must be after the function, the ampersand makes powershell execute the code
$procOutput = & {command}
if($procOutput -is [array]) {
    $lastRow = $procOutput[$wingetOutput.Length -1]
    if ($lastRow.Contains($success)) {
        Write-Host $message
        Exit 0
    }
}
Write-Host "Command Unsuccessful"
Write-Host "$procOutput"
Exit 5