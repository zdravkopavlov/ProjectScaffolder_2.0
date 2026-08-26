[CmdletBinding()]
param(
    [switch]$KeepOutput
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runnerDll = Join-Path $repositoryRoot 'src\ProjectScaffolder.Runner\bin\Release\net10.0-windows\ProjectScaffolder.Runner.dll'
$examplePath = Join-Path $repositoryRoot 'examples\3ds-max-template.json'

if (-not (Test-Path -LiteralPath $runnerDll -PathType Leaf)) {
    throw 'Build the Release solution before running this smoke test.'
}

$template = Get-Content -LiteralPath $examplePath -Raw | ConvertFrom-Json
$testPrefix = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'ProjectScaffolder.RunnerSmoke-'))
$testRoot = [IO.Path]::GetFullPath("$testPrefix$([Guid]::NewGuid().ToString('N'))")
if (-not $testRoot.StartsWith($testPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to use a smoke-test directory outside the expected temporary path.'
}

$templatesRoot = Join-Path $testRoot 'Templates'
$templateDirectory = Join-Path $templatesRoot $template.templateId
$targetDirectory = Join-Path $testRoot 'Hotel Lobby'
$logsRoot = Join-Path $testRoot 'Logs'

try {
    New-Item -ItemType Directory -Path $templateDirectory, $targetDirectory -Force | Out-Null
    Copy-Item -LiteralPath $examplePath -Destination (Join-Path $templateDirectory 'template.json')

    & dotnet $runnerDll `
        --template-id $template.templateId `
        --target $targetDirectory `
        --templates-root $templatesRoot `
        --logs-root $logsRoot `
        --no-ui

    if ($LASTEXITCODE -ne 0) {
        throw "Runner returned exit code $LASTEXITCODE. Inspect $logsRoot."
    }

    $requiredPaths = @(
        (Join-Path $targetDirectory 'SCENES'),
        (Join-Path $targetDirectory 'RENDERS'),
        (Join-Path $targetDirectory 'ASSETS'),
        (Join-Path $targetDirectory 'Hotel Lobby.mxp'),
        (Join-Path $targetDirectory '.projectID.json')
    )
    foreach ($path in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Runner did not create expected path: $path"
        }
    }

    $identityPath = Join-Path $targetDirectory '.projectID.json'
    if (-not ((Get-Item -LiteralPath $identityPath -Force).Attributes -band [IO.FileAttributes]::Hidden)) {
        throw 'The generated .projectID.json does not have the Windows Hidden attribute.'
    }

    $latestLog = Get-ChildItem -LiteralPath $logsRoot -Filter '*.json' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $log = Get-Content -LiteralPath $latestLog.FullName -Raw | ConvertFrom-Json
    if ($log.outcome -ne 'success') {
        throw 'The runner diagnostic log did not record a successful outcome.'
    }

    $missingTemplateId = '01993a6c-f686-7e81-b530-35940e1b4073'
    & dotnet $runnerDll `
        --template-id $missingTemplateId `
        --target $targetDirectory `
        --templates-root $templatesRoot `
        --logs-root $logsRoot `
        --no-ui

    if ($LASTEXITCODE -ne 3) {
        throw "Missing-template invocation returned exit code $LASTEXITCODE instead of 3."
    }

    $latestFailureLog = Get-ChildItem -LiteralPath $logsRoot -Filter '*.json' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $failureLog = Get-Content -LiteralPath $latestFailureLog.FullName -Raw | ConvertFrom-Json
    if ($failureLog.outcome -ne 'failure') {
        throw 'The runner diagnostic log did not record the expected failure.'
    }

    Write-Host "Runner smoke test passed: $targetDirectory"
}
finally {
    if ($KeepOutput) {
        Write-Host "Smoke-test output retained at: $testRoot"
    }
    elseif (Test-Path -LiteralPath $testRoot) {
        [IO.Directory]::Delete($testRoot, $true)
    }
}
