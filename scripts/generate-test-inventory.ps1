<#
.SYNOPSIS
    Gera o inventário unificado de testes unitários para o Dashboard do Solidify.
    Suporta .NET (TRX), Flutter (Dart Test JSON), e formatos genéricos (JUnit XML).
#>
param(
    [string]$TrxPath = "backend/TestResults/backend_tests.trx",
    [string]$FlutterJsonPath = "frontend/test_results.json",
    [string]$OutputPath = "web/public/tests-inventory.json"
)

function Get-HumanFriendlyDescription {
    param(
        [string]$TestName,
        [string]$Suite,
        [string]$Tech
    )

    # 1. Mapeamento heurístico baseado em padrões comuns (When... Should..., When... Returns...)
    $desc = $TestName

    # Limpeza de prefixos comuns
    $clean = $TestName -replace ".*\.", "" -replace "^test_", ""

    # Padrão: Method_WhenCondition_ExpectedOutcome
    if ($clean -match "^([A-Za-z0-9]+)_When([A-Za-z0-9]+)_(Should|Returns|Does|Throws|Emits)([A-Za-z0-9]+)$") {
        $method = $matches[1]
        $condition = ($matches[2] -creplace '([A-Z])', ' $1').Trim()
        $action = $matches[3]
        $outcome = ($matches[4] -creplace '([A-Z])', ' $1').Trim()

        $actionPt = switch ($action) {
            "Returns" { "retorna" }
            "Should" { "deve" }
            "Does" { "executa" }
            "Throws" { "lança exceção" }
            "Emits" { "emite estado" }
            default { $action }
        }

        return "Cenário: Quando $condition, $actionPt $outcome no método $method."
    }

    # Padrão: Class_Feature_WhenCondition_ShouldResult
    if ($clean -match "^([A-Za-z0-9]+)_([A-Za-z0-9]+)_When([A-Za-z0-9]+)_Should([A-Za-z0-9]+)$") {
        $entity = $matches[1]
        $feature = ($matches[2] -creplace '([A-Z])', ' $1').Trim()
        $condition = ($matches[3] -creplace '([A-Z])', ' $1').Trim()
        $result = ($matches[4] -creplace '([A-Z])', ' $1').Trim()
        return "$entity ($feature): Quando $condition, deve $result."
    }

    # Padrões específicos do Flutter / Dart (BDD style)
    if ($clean -match "injects Bearer token into headers") {
        return "Injeta automaticamente o cabeçalho Authorization: Bearer quando access token existe no secure storage."
    }
    if ($clean -match "does not inject Authorization header") {
        return "Não injeta cabeçalho de autenticação quando access token não está disponível."
    }
    if ($clean -match "atomic silent refresh on 401") {
        return "Interceptação atômica em resposta 401: renova o token silenciosamente e reenvia a requisição original."
    }
    if ($clean -match "passes through non-401 errors") {
        return "Encaminha erros HTTP normais (não-401) sem disparar renovação de token."
    }
    if ($clean -match "passes through 401 errors on auth routes") {
        return "Evita loop recursivo ao não tentar refresh em falhas de rotas de autenticação (como login incorreto)."
    }
    if ($clean -match "clears tokens and notifies session expired") {
        return "Limpa tokens armazenados e dispara callback de sessão expirada quando refresh token não está disponível."
    }
    if ($clean -match "Hardware PlatformException" -or $clean -match "Defensive Keystore") {
        return "Tratamento defensivo de falha de Keystore/Keychain de hardware: efetua limpeza segura sem travar o aplicativo."
    }
    if ($clean -match "saveAccessToken writes to storage" -or $clean -match "writes to storage") {
        return "Grava credencial de autenticação no cofre seguro com a chave correta no dispositivo."
    }
    if ($clean -match "getAccessToken reads from storage" -or $clean -match "reads from storage") {
        return "Lê credencial de autenticação do cofre seguro do dispositivo."
    }
    if ($clean -match "clearTokens deletes access and refresh tokens") {
        return "Revogação local: exclui access e refresh tokens preservando identificador do dispositivo."
    }
    if ($clean -match "emits \[Authenticated\]" -or $clean -match "emits \[Authenticating, Authenticated\]") {
        return "Gestão de estado Cubit: transição reativa para estado Authenticated após operação bem-sucedida."
    }
    if ($clean -match "emits \[Unauthenticated\]") {
        return "Gestão de estado Cubit: transição para Unauthenticated quando sessão é encerrada ou token inválido."
    }
    if ($clean -match "emits \[Authenticating, AuthError\]") {
        return "Gestão de estado Cubit: transição para AuthError com mensagem de erro descritiva em caso de falha."
    }
    if ($clean -match "Counter increments smoke test") {
        return "Smoke test de renderização e estado do widget de contagem padrão do Flutter."
    }

    # Fallback: formata nome CamelCase ou com underlines
    $readable = $clean -replace "_", " " -creplace '([A-Z])', ' $1'
    return "Valida: $($readable.Trim())"
}

$allTests = @()
$techStats = @{}

# ==============================================================================
# 1. PARSE DE .NET (TRX FORMAT)
# ==============================================================================
if (Test-Path $TrxPath) {
    Write-Host "Processando arquivo TRX do .NET: $TrxPath" -ForegroundColor Cyan
    [xml]$trx = Get-Content $TrxPath

    $techKey = ".NET 9"
    if (-not $techStats.ContainsKey($techKey)) {
        $techStats[$techKey] = @{
            name = ".NET 9"
            framework = "xUnit"
            category = "Backend API"
            total = 0
            passed = 0
            failed = 0
            skipped = 0
            color = "#8b5cf6" # Purple
            icon = "dotnet"
        }
    }

    foreach ($result in $trx.TestRun.Results.UnitTestResult) {
        $fullName = $result.testName
        $parts = $fullName.Split('.')
        $methodName = $parts[-1]
        $suiteName = if ($parts.Length -gt 1) { $parts[-2] } else { "UnitTests" }
        $category = if ($parts.Length -gt 2) { $parts[-3] } else { "General" }

        # Duração
        $durationMs = 0
        if ($result.duration) {
            if ($result.duration -match "(\d+):(\d+):(\d+\.?\d*)") {
                $hours = [double]$matches[1]
                $mins = [double]$matches[2]
                $secs = [double]$matches[3]
                $durationMs = [math]::Round(($hours * 3600 + $mins * 60 + $secs) * 1000, 1)
            }
        }

        $status = switch ($result.outcome) {
            "Passed" { "PASSED" }
            "Failed" { "FAILED" }
            default { "SKIPPED" }
        }

        $techStats[$techKey].total++
        if ($status -eq "PASSED") { $techStats[$techKey].passed++ }
        elseif ($status -eq "FAILED") { $techStats[$techKey].failed++ }
        else { $techStats[$techKey].skipped++ }

        $desc = Get-HumanFriendlyDescription -TestName $methodName -Suite $suiteName -Tech ".NET 9"

        $allTests += [PSCustomObject]@{
            id = $result.testId
            technology = ".NET 9"
            framework = "xUnit"
            category = "Backend ($category)"
            suite = $suiteName
            test_name = $methodName
            full_name = $fullName
            description = $desc
            status = $status
            duration_ms = $durationMs
            error_message = $result.Output.ErrorInfo.Message
        }
    }
} else {
    Write-Warning "Arquivo TRX não encontrado em $TrxPath"
}

# ==============================================================================
# 2. PARSE DE FLUTTER / DART (JSON TEST REPORTER)
# ==============================================================================
if (Test-Path $FlutterJsonPath) {
    Write-Host "Processando arquivo JSON do Flutter: $FlutterJsonPath" -ForegroundColor Cyan
    $lines = Get-Content $FlutterJsonPath
    $testsMap = @{}
    $groupsMap = @{}
    $suitesMap = @{}

    $techKey = "Flutter / Dart"
    if (-not $techStats.ContainsKey($techKey)) {
        $techStats[$techKey] = @{
            name = "Flutter / Dart"
            framework = "Flutter Test"
            category = "Mobile App"
            total = 0
            passed = 0
            failed = 0
            skipped = 0
            color = "#0284c7" # Sky Blue
            icon = "flutter"
        }
    }

    foreach ($line in $lines) {
        if (-not $line.Trim().StartsWith("{")) { continue }
        try {
            $event = $line | ConvertFrom-Json
        } catch {
            continue
        }

        if ($event.type -eq "suite") {
            $suitesMap[$event.suite.id] = $event.suite.path
        }
        elseif ($event.type -eq "group") {
            $groupsMap[$event.group.id] = $event.group.name
        }
        elseif ($event.type -eq "testStart") {
            $t = $event.test
            # Ignora testes internos do flutter runner de loading e teardown
            if ($t.name -match "^loading " -or $t.name -match "^\(setUpAll\)" -or $t.name -match "^\(tearDownAll\)") {
                continue
            }
            $testsMap[$t.id] = @{
                id = "flutter-$($t.id)"
                name = $t.name
                suiteID = $t.suiteID
                startTime = $event.time
            }
        }
        elseif ($event.type -eq "testDone") {
            $testId = $event.testID
            if ($testsMap.ContainsKey($testId)) {
                $testInfo = $testsMap[$testId]
                $duration = 0
                if ($event.time -and $testInfo.startTime) {
                    $duration = [math]::Max(0, [math]::Round([double]($event.time - $testInfo.startTime), 1))
                }

                $status = switch ($event.result) {
                    "success" { "PASSED" }
                    "failure" { "FAILED" }
                    "error" { "FAILED" }
                    default { if ($event.skipped) { "SKIPPED" } else { "PASSED" } }
                }

                $rawPath = $suitesMap[$testInfo.suiteID]
                $suiteName = if ($rawPath) { [System.IO.Path]::GetFileNameWithoutExtension($rawPath) } else { "FlutterTests" }
                $categoryName = if ($rawPath -match "core/storage") { "Storage & Keystore" }
                                elseif ($rawPath -match "core/network") { "Network & Interceptors" }
                                elseif ($rawPath -match "features/auth") { "Auth State & Cubits" }
                                else { "Widgets & UI" }

                $techStats[$techKey].total++
                if ($status -eq "PASSED") { $techStats[$techKey].passed++ }
                elseif ($status -eq "FAILED") { $techStats[$techKey].failed++ }
                else { $techStats[$techKey].skipped++ }

                $desc = Get-HumanFriendlyDescription -TestName $testInfo.name -Suite $suiteName -Tech "Flutter"

                $allTests += [PSCustomObject]@{
                    id = $testInfo.id
                    technology = "Flutter / Dart"
                    framework = "Flutter Test"
                    category = "Mobile ($categoryName)"
                    suite = $suiteName
                    test_name = $testInfo.name
                    full_name = "$suiteName > $($testInfo.name)"
                    description = $desc
                    status = $status
                    duration_ms = $duration
                    error_message = $null
                }
            }
        }
    }
} else {
    Write-Warning "Arquivo de testes do Flutter não encontrado em $FlutterJsonPath"
}

# ==============================================================================
# 3. CONSOLIDAÇÃO DO INVENTÁRIO
# ==============================================================================
$totalTests = $allTests.Count
$totalPassed = ($allTests | Where-Object { $_.status -eq "PASSED" }).Count
$totalFailed = ($allTests | Where-Object { $_.status -eq "FAILED" }).Count
$totalSkipped = ($allTests | Where-Object { $_.status -eq "SKIPPED" }).Count
$passRate = if ($totalTests -gt 0) { [math]::Round(($totalPassed / $totalTests) * 100, 1) } else { 100 }

$technologiesList = @()
foreach ($key in $techStats.Keys) {
    $technologiesList += $techStats[$key]
}

$inventory = [PSCustomObject]@{
    schema_version = "1.0.0"
    generated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    project_name = "search_a_church"
    summary = [PSCustomObject]@{
        total_tests = $totalTests
        passed = $totalPassed
        failed = $totalFailed
        skipped = $totalSkipped
        pass_rate_pct = $passRate
    }
    technologies = $technologiesList
    tests = $allTests
}

# Garante existência do diretório de saída
$outDir = [System.IO.Path]::GetDirectoryName($OutputPath)
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$jsonString = $inventory | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($OutputPath, $jsonString, [System.Text.UTF8Encoding]::new($false))
Write-Host "Inventário de testes gerado com sucesso em $OutputPath ($totalTests testes registrados)." -ForegroundColor Green
