$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SnkMessage.csproj'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
& $msbuild $project /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built: $(Join-Path $PSScriptRoot 'bin\SnkMessage.exe')"
