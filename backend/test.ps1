$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = "$env:LOCALAPPDATA\Temp"
if (Test-Path "backend/TestResults") {
    Get-ChildItem -Path "backend/TestResults" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

& "C:\Program Files\dotnet\dotnet.exe" test backend/SearchAChurch.slnx --logger "trx;LogFileName=backend_tests.trx" --collect:"XPlat Code Coverage;Format=opencover,cobertura" --results-directory backend/TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*]*.Migrations.*"
$testExitCode = $LASTEXITCODE

$destCobertura = [System.IO.Path]::GetFullPath("backend/TestResults/coverage.cobertura.xml")
$coberturaFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destCobertura } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($coberturaFile) {
    Copy-Item $coberturaFile.FullName -Destination $destCobertura -Force
}

$destOpencover = [System.IO.Path]::GetFullPath("backend/TestResults/coverage.opencover.xml")
$opencoverFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destOpencover } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($opencoverFile) {
    Copy-Item $opencoverFile.FullName -Destination $destOpencover -Force
}

# Auto-update unified tests inventory for Solidify Dashboard
if (Test-Path "scripts/generate-test-inventory.ps1") {
    & powershell -ExecutionPolicy Bypass -File "scripts/generate-test-inventory.ps1" -ErrorAction SilentlyContinue
}

exit $testExitCode
