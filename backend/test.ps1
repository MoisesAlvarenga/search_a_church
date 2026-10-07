$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = "$env:LOCALAPPDATA\Temp"
& "C:\Program Files\dotnet\dotnet.exe" test backend/SearchAChurch.slnx --collect:"XPlat Code Coverage" --results-directory backend/TestResults
$testExitCode = $LASTEXITCODE

$coverageFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($coverageFile) {
    Copy-Item $coverageFile.FullName -Destination "backend/TestResults/coverage.cobertura.xml" -Force
}

exit $testExitCode
