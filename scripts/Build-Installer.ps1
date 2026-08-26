[CmdletBinding()]
param(
    [string]$Version = '2.0.0-alpha.1',
    [string]$Configuration = 'Release',
    [string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\installer'))
$publishRoot = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish'))
$installerScript = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'installer\ProjectScaffolder.iss'))

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $compilerCandidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    $InnoCompiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Pass its path with -InnoCompiler.'
}

if (-not $publishRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to prepare an installer publish directory outside artifacts\installer.'
}

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

& dotnet publish (Join-Path $repositoryRoot 'src\ProjectScaffolder.Editor\ProjectScaffolder.Editor.csproj') `
    -c $Configuration -r win-x64 --self-contained true -p:Version=$Version `
    -p:SatelliteResourceLanguages=en -p:DebugType=None -p:DebugSymbols=false -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Editor publish failed.' }

& dotnet publish (Join-Path $repositoryRoot 'src\ProjectScaffolder.Runner\ProjectScaffolder.Runner.csproj') `
    -c $Configuration -r win-x64 --self-contained true -p:Version=$Version `
    -p:SatelliteResourceLanguages=en -p:DebugType=None -p:DebugSymbols=false -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'Runner publish failed.' }

& $InnoCompiler "/DAppVersion=$Version" "/DSourceDir=$publishRoot" "/DOutputDir=$artifactsRoot" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$installerPath = Join-Path $artifactsRoot "ProjectScaffolder-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Inno Setup completed but the expected installer was not found: $installerPath"
}

Write-Host "Installer created: $installerPath"
