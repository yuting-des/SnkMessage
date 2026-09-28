$ErrorActionPreference = 'Stop'

$repository = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $repository '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $repository '.tools\dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $PSScriptRoot 'SnkMessage.csproj'
$publish = Join-Path $PSScriptRoot 'bin\publish'
$dist = Join-Path $repository 'dist\SnkMessage-native-v0.3.exe'
$localPackages = Join-Path $repository '.tools\nuget'

$publishArguments = @(
    'publish', $project,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--no-self-contained',
    '--output', $publish,
    '--ignore-failed-sources'
)
if (Test-Path -LiteralPath $localPackages) {
    $publishArguments += @('--source', $localPackages)
}
$publishArguments += @('--source', 'https://api.nuget.org/v3/index.json')

& $dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Path (Split-Path $dist -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publish 'SnkMessage.exe') -Destination $dist -Force
Write-Host "Built: $dist"
