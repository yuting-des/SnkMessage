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
} else {
    $publishArguments += @('--source', 'https://api.nuget.org/v3/index.json')
}

& $dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Path (Split-Path $dist -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publish 'SnkMessage.exe') -Destination $dist -Force
$serviceSource = Join-Path $repository 'algorithm-server'
$serviceDestination = Join-Path (Split-Path $dist -Parent) 'algorithm-server'
New-Item -ItemType Directory -Path $serviceDestination -Force | Out-Null
Copy-Item -Path (Join-Path $serviceSource '*.mjs') -Destination $serviceDestination -Force
Copy-Item -Path (Join-Path $serviceSource '.env.example') -Destination $serviceDestination -Force
Copy-Item -Path (Join-Path $serviceSource 'providers') -Destination $serviceDestination -Recurse -Force
$bundledNode = Join-Path $repository '.tools\node\node.exe'
if (Test-Path -LiteralPath $bundledNode) {
    $runtimeDestination = Join-Path (Split-Path $dist -Parent) 'runtime'
    New-Item -ItemType Directory -Path $runtimeDestination -Force | Out-Null
    Copy-Item -LiteralPath $bundledNode -Destination (Join-Path $runtimeDestination 'node.exe') -Force
} else {
    Write-Warning 'No bundled Node runtime found at .tools\node\node.exe; the target computer must provide Node.js on PATH.'
}
Write-Host "Built: $dist"
