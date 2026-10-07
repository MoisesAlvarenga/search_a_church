$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = "$env:LOCALAPPDATA\Temp"
& "C:\Program Files\dotnet\dotnet.exe" test backend/SearchAChurch.slnx --collect:"XPlat Code Coverage" --results-directory backend/tests/SearchAChurch.UnitTests/TestResults
exit $LASTEXITCODE
