# WinTuner - Winget Detection script https://wintuner.app
# 
# Script parameters in `{parameter_name}`
# packageId - The package id of the application to be detected
# version - The version of the application to be detected

# --------------------------- Start parameters -------------------------------
Param(
    [string]$packageId = "{packageId}",
    [string]$version = "{version}"
)
# --------------------------- End parameters ---------------------------------

# ------------------------------------Start script, do not edit below -----------------------------------------
Start-Transcript -Path "$env:ProgramData\Microsoft\IntuneManagementExtension\Logs\$packageId-detection.log" -Force
Write-Host "Starting $packageId $version detection"

# Need to get the full path of winget, because detection script is run in a different context
Function Get-WingetCmd {

    $WingetCmd = $null
    #Get WinGet Path

    try {
        # Get Admin Context Winget Location
        $WingetInfo = (Get-Item "$env:ProgramFiles\WindowsApps\Microsoft.DesktopAppInstaller_*_8wekyb3d8bbwe\winget.exe").VersionInfo | Sort-Object -Property FileVersionRaw
        # if multiple versions, pick most recent one
        $WingetCmd = $WingetInfo[-1].FileName
    }
    catch {
        #Get User context Winget Location
        if (Test-Path "$env:LocalAppData\Microsoft\WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe")
        {
            $WingetCmd ="$env:LocalAppData\Microsoft\WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe"
        }
    }
    Write-Host "Winget location: $WingetCmd"
    return $WingetCmd
}

# Convert command output to array of values
Function Get-ColumnValuesFromWingetOutput {
    param (
        [array]$Output
    )
    if ($Output -is [string]) {
        return @($Output) # Convert single string to array
    }

    if ($Output -is [array] -and $Output.Length -gt 2) {
        # $Output is an array, first row is header, last row is data
        # this will break if multiple lines are returned, but that should not happen with --exact
        #$headerRow = $Output[$Output.Length - 3]
        $headerRow = $Output | Where-Object { $_ -match '^[A-Za-z]' } | Select-Object -First 1
        $lastRow = $Output[$Output.Length - 1]

        # Find the start index of each column by searching for non-space transitions
        $columnStarts = @()
        for ($i = 0; $i -lt $headerRow.Length; $i++) {
            if (($i -eq 0 -or $headerRow[$i - 1] -eq ' ') -and $headerRow[$i] -ne ' ') {
                $columnStarts += $i
            }
        }

        # Add the end of the line as the last column boundary
        $columnStarts += $lastRow.Length

        # Extract column values from the data row
        $columns = @()
        for ($i = 0; $i -lt $columnStarts.Count - 1; $i++) {
            $start = $columnStarts[$i]
            $length = $columnStarts[$i + 1] - $start
            $columns += $lastRow.Substring($start, $length).Trim()
        }

        return $columns
    }
    else {
        Write-Host "Unexpected output format from winget list command"
        return @()
    }
}

Function Compare-Versions {
    param (
        [string]$VersionExpected,
        [string]$VersionInstalled
    )
    try {
        $vi = [version]$VersionInstalled
        $ve = [version]$VersionExpected
        return $vi.CompareTo($ve)
    } catch {
        # Fallback to string comparison if version parsing fails
        return $VersionInstalled.CompareTo($VersionExpected)
    }
}

$wingetCmd = Get-WingetCmd
if ($null -eq $wingetCmd) {
    Write-Host "winget not detected, exiting with code 1"
    Exit 1
}

$wingetOutput = & $wingetCmd "list" "--id" $packageId "--exact" "--accept-source-agreements"

if($wingetOutput -is [array]) { # the output will be either an array of lines or a string when it is just one line.

    $columns = Get-ColumnValuesFromWingetOutput -Output $wingetOutput
    if ($columns.Length -lt 4) {
        Write-Host "Got invalid column count $($columns.Length) expected at least 4, exiting with code 10"
        Write-Host "Winget output:"
        Write-Host "$($wingetOutput)"
        Exit 10
    }

    if ($columns[1] -eq $packageId) {
        if ($null -eq $version -or $version -eq "") {
            Write-Host "$($packageId) version $($columns[2]) is installed, exiting with code 0"
            Exit 0
        }
        if ($columns[2] -eq $version) {
            Write-Host "$($packageId) version $($version) is installed, exiting with code 0"
            Exit 0
        }
        $versionValue = Compare-Versions -VersionExpected $version -VersionInstalled $columns[2]
        if ($versionValue -lt 0) {
            Write-Host "$($packageId) is installed but $($columns[2]) is lower than expected $($version), exit code 4"
            Exit 4
        } else {
            Write-Host "$($packageId) is installed $($columns[2]) is equal of higher than expected $($version), exit code 0"
            Exit 0
        }
    }
}

if ($packageId -like "*.*") { # If the packageId contains a period enter this logic, it more than likely will
	$idArray = $packageId.Split(".") # split the packag into an array at the period
	foreach ($id in $idArray) { # loop through the array, and then run the detection logic on each array element
		$wingetOutput = & $wingetCmd "list" "--id" $id "--accept-source-agreements"

		if($wingetOutput -is [array]) { # the output will be either an array of lines or a string when it is just one line.

		$columns = Get-ColumnValuesFromWingetOutput -Output $wingetOutput
		if ($columns.Length -lt 4) {
			Write-Host "Got invalid column count $($columns.Length) expected at least 4, exiting with code 10"
			Write-Host "Winget output:"
			Write-Host "$($wingetOutput)"
			Exit 10
		}

			if ($columns[1] -eq $id) {
				if ($null -eq $version -or $version -eq "") {
					Write-Host "$($id) version $($columns[2]) is installed, exiting with code 0"
					Exit 0
				}
				if ($columns[2] -eq $version) {
					Write-Host "$($id) version $($version) is installed, exiting with code 0"
					Exit 0
				}
				$versionValue = Compare-Versions -VersionExpected $version -VersionInstalled $columns[2]
				if ($versionValue -lt 0) {
					Write-Host "$($id) is installed but $($columns[2]) is lower than expected $($version), exit code 4"
					Exit 4
				} else {
					Write-Host "$($id) is installed $($columns[2]) is equal of higher than expected $($version), exit code 0"
					Exit 0
				}
			}
		}
	}
}

Write-Host "$($packageId) not detected using winget, exiting with code 10"
Write-Host "Winget output:"
Write-Host "$($wingetOutput)"
Exit 10
