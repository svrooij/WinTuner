$packageId = "Microsoft.AzureCLI"

if ($packageId - like ".") {
	$idArray = $packageId -split "."
	
	foreach ($in in $idArray) {
		$wingetOutput = & "winget" "list" "--id" $in "--exact" "--disable-interactivity" "--accept-source-agreements"

		if($wingetOutput -is [array]) {
			$lastRow = $wingetOutput[$wingetOutput.Length -1]
			if ($lastRow.Contains($in)) {
				Write-Host "$($in) is installed"
				Write-Host "Winget output: $($lastRow)"
				Exit 0
			}
		}
	}
}
else {
	$wingetOutput = & "winget" "list" "--id" $packageId "--exact" "--disable-interactivity" "--accept-source-agreements"

	if($wingetOutput -is [array]) {
		$lastRow = $wingetOutput[$wingetOutput.Length -1]
		if ($lastRow.Contains($packageId)) {
			Write-Host "$($packageId) is installed"
			Write-Host "Winget output: $($lastRow)"
			Exit 0
		}
	}
}

Write-Host "$($packageId) not detected using winget"
Exit 10