$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SnkMessage.csproj'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
& $msbuild $project /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$source = Join-Path $PSScriptRoot 'bin\SnkMessage.exe'
$dist = Join-Path (Split-Path $PSScriptRoot -Parent) 'dist\SnkMessage-native-v0.2.exe'
New-Item -ItemType Directory -Path (Split-Path $dist -Parent) -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $dist -Force
Write-Host "Built: $dist"
