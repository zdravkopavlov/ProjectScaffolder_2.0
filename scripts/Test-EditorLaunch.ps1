[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editorExe = Join-Path $repositoryRoot 'src\ProjectScaffolder.Editor\bin\Release\net10.0-windows\ProjectScaffolder.Editor.exe'
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
    $process = Start-Process -FilePath $editorExe -ArgumentList @('--data-root', $testRoot) -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    $process.Refresh()
    if ($process.HasExited) {
        throw "The editor exited during startup with code $($process.ExitCode)."
    }

    $seededTemplate = Join-Path $testRoot 'Templates\01993a6c-f686-7e81-b530-35940e1b4072\template.json'
    $library = Join-Path $testRoot 'Settings\template-library.json'
    if (-not (Test-Path -LiteralPath $seededTemplate -PathType Leaf)) {
        throw 'The editor did not seed the bundled 3ds Max template.'
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
