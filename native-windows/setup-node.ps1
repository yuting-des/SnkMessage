$ErrorActionPreference = 'Stop'

$version = '22.20.0'
$repository = Split-Path $PSScriptRoot -Parent
$tools = Join-Path $repository '.tools'
$archive = Join-Path $tools "node-v$version-win-x64.zip"
$expanded = Join-Path $tools "node-v$version-win-x64"
$destination = Join-Path $tools 'node'

if (Test-Path -LiteralPath (Join-Path $destination 'node.exe')) {
    Write-Host "Node runtime already available: $destination"
    exit 0
}

New-Item -ItemType Directory -Path $tools -Force | Out-Null
Invoke-WebRequest -Uri "https://nodejs.org/dist/v$version/node-v$version-win-x64.zip" -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $tools -Force
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
Move-Item -LiteralPath $expanded -Destination $destination
Remove-Item -LiteralPath $archive -Force
Write-Host "Installed Node runtime: $destination"
