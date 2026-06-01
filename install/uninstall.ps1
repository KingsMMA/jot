#Requires -Version 5
<#
.SYNOPSIS
    Removes Jot. Your configuration under %APPDATA%\Jot is kept unless you pass -RemoveConfig.
#>
param(
    [switch]$RemoveConfig,
    [switch]$Quiet
)

$ErrorActionPreference = "SilentlyContinue"

$AppName    = "Jot"
$InstallDir = Join-Path $env:LOCALAPPDATA $AppName

function Write-Step($message) {
    if (-not $Quiet) { Write-Host "==> $message" -ForegroundColor Cyan }
}

Write-Step "Stopping Jot"
Get-Process jot | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Step "Removing the background agent, hotkey, and menu entries"
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $AppName
# The "*" key name is a wildcard to the registry provider, so delete it via the .NET API.
[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree("Software\Classes\*\shell\$AppName", $false)
Remove-Item -Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Jot.lnk")

Write-Step "Removing the application"
Remove-Item -Path $InstallDir -Recurse -Force

if ($RemoveConfig) {
    Write-Step "Removing configuration"
    Remove-Item -Path (Join-Path $env:APPDATA $AppName) -Recurse -Force
}

if (-not $Quiet) {
    Write-Host ""
    Write-Host "Jot has been removed." -ForegroundColor Green
    if (-not $RemoveConfig) {
        Write-Host "Your configuration was kept. Pass -RemoveConfig to delete it too." -ForegroundColor Gray
    }
}
