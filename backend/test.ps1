$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = "$env:LOCALAPPDATA\Temp"
& "C:\Program Files\dotnet\dotnet.exe" test backend/SearchAChurch.slnx --collect:"XPlat Code Coverage;Format=opencover,cobertura" --results-directory backend/TestResults
$testExitCode = $LASTEXITCODE

$destCobertura = [System.IO.Path]::GetFullPath("backend/TestResults/coverage.cobertura.xml")
$coberturaFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destCobertura } | Select-Object -First 1
if ($coberturaFile) {
    Copy-Item $coberturaFile.FullName -Destination $destCobertura -Force
}

$destOpencover = [System.IO.Path]::GetFullPath("backend/TestResults/coverage.opencover.xml")
$opencoverFile = Get-ChildItem -Path "backend/TestResults" -Recurse -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destOpencover } | Select-Object -First 1
if ($opencoverFile) {
    Copy-Item $opencoverFile.FullName -Destination $destOpencover -Force
}

exit $testExitCode
