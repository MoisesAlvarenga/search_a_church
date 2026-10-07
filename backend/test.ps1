$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = "$env:LOCALAPPDATA\Temp"
& "C:\Program Files\dotnet\dotnet.exe" test backend/SearchAChurch.slnx --collect:"XPlat Code Coverage" --results-directory backend/TestResults
$testExitCode = $LASTEXITCODE

$destPath = [System.IO.Path]::GetFullPath("backend/TestResults/coverage.cobertura.xml")
$coverageFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destPath } | Select-Object -First 1
if ($coverageFile) {
    Copy-Item $coverageFile.FullName -Destination $destPath -Force
}

exit $testExitCode
