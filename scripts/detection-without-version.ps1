$packageId = "Microsoft.AzureCLI"

$wingetOutput = & "winget" "list" "--id" $packageId "--exact" "--disable-interactivity" "--accept-source-agreements"

if($wingetOutput -is [array]) {
    $lastRow = $wingetOutput[$wingetOutput.Length -1]
    if ($lastRow.Contains($packageId)) {
        Write-Host "$($packageId) is installed"
        Write-Host "Winget output: $($lastRow)"
        Exit 0
    }
}

Write-Host "$($packageId) not detected using winget"
Exit 10