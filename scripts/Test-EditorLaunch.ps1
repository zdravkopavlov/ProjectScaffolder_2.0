[CmdletBinding()]
param(
    [string]$EditorPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editorExe = if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    Join-Path $repositoryRoot 'src\ProjectScaffolder.Editor\bin\Release\net10.0-windows\ProjectScaffolder.Editor.exe'
} else {
    [IO.Path]::GetFullPath($EditorPath)
}
if (-not (Test-Path -LiteralPath $editorExe -PathType Leaf)) {
    throw 'Build the Release solution before running this launch test.'
}

$testPrefix = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'ProjectScaffolder.EditorSmoke-'))
$testRoot = [IO.Path]::GetFullPath("$testPrefix$([Guid]::NewGuid().ToString('N'))")
if (-not $testRoot.StartsWith($testPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to use an editor smoke-test directory outside the expected temporary path.'
}

$process = $null
try {
    $process = Start-Process -FilePath $editorExe -ArgumentList @('--data-root', $testRoot, '--smoke-select-first') -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(10000)) {
        throw 'The editor did not complete its selection smoke test within ten seconds.'
    }

    if ($process.ExitCode -ne 0) {
        throw "The editor selection smoke test exited with code $($process.ExitCode)."
    }

    $seededTemplate = Join-Path $testRoot 'Templates\019c97d4-2000-7a10-8a01-000000000001\template.json'
    $library = Join-Path $testRoot 'Settings\template-library.json'
    if (-not (Test-Path -LiteralPath $seededTemplate -PathType Leaf)) {
        throw 'The editor did not seed the bundled Demo Project template.'
    }

    if (-not (Test-Path -LiteralPath $library -PathType Leaf)) {
        throw 'The editor did not initialize its template library.'
    }

    Write-Host 'Editor launch smoke test passed.'
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }

    if (Test-Path -LiteralPath $testRoot) {
        [IO.Directory]::Delete($testRoot, $true)
    }
}
