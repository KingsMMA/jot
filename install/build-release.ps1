#Requires -Version 5
<#
.SYNOPSIS
    Builds Jot and produces the release zip (jot-win-x64.zip) used by the installer.
#>
param(
    [string]$Output = "dist"
)

$ErrorActionPreference = "Stop"

$root    = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\Jot\Jot.csproj"
$appDir  = Join-Path $root "$Output\app"
$zip     = Join-Path $root "$Output\jot-win-x64.zip"

Write-Host "==> Publishing Jot (framework-dependent, win-x64)" -ForegroundColor Cyan
Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $project -c Release -r win-x64 --self-contained false -o $appDir

Write-Host "==> Creating $zip" -ForegroundColor Cyan
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $appDir "*") -DestinationPath $zip

$size = "{0:N1} MB" -f ((Get-Item $zip).Length / 1MB)
Write-Host "==> Done: $zip ($size)" -ForegroundColor Green
