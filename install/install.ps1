#Requires -Version 5
<#
.SYNOPSIS
    Installs or updates Jot, a fast single-file editor for Windows.
.DESCRIPTION
    Runs entirely per-user, so it needs no administrator rights. Re-running it updates an existing
    installation in place. By default it downloads the latest release; pass -Source to install from
    a local build folder instead.
.PARAMETER Source
    A folder containing a built Jot (jot.exe and its files). When omitted, the latest release is
    downloaded from GitHub.
.PARAMETER Quiet
    Suppresses progress messages.
#>
param(
    [string]$Source = "",
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$AppName     = "Jot"
$Repo        = "KingsMMA/jot"
$InstallDir  = Join-Path $env:LOCALAPPDATA $AppName
$AppDir      = Join-Path $InstallDir "app"
$Exe         = Join-Path $AppDir "jot.exe"

function Write-Step($message) {
    if (-not $Quiet) { Write-Host "==> $message" -ForegroundColor Cyan }
}

function Test-DotNet8 {
    $shared = Join-Path $env:ProgramFiles "dotnet\shared\Microsoft.NETCore.App"
    if (-not (Test-Path $shared)) { return $false }
    return (Get-ChildItem $shared -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "8.*" -or $_.Name -like "9.*" -or $_.Name -like "1*.*" }).Count -gt 0
}

function Install-DotNet8 {
    Write-Step "Installing the .NET 8 runtime (required to run Jot)"
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        winget install --id Microsoft.DotNet.Runtime.8 -e --silent `
            --accept-package-agreements --accept-source-agreements
    } else {
        throw "The .NET 8 runtime is required but winget is not available. " +
              "Install it from https://dotnet.microsoft.com/download/dotnet/8.0 (the 'Run desktop apps' runtime), then re-run this installer."
    }
}

function Stop-Jot {
    Get-Process jot -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

function Copy-Application {
    New-Item -ItemType Directory -Force -Path $AppDir | Out-Null

    if ($Source) {
        Write-Step "Installing Jot from $Source"
        Get-ChildItem -Path $AppDir -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item -Path (Join-Path $Source "*") -Destination $AppDir -Recurse -Force
    } else {
        Write-Step "Downloading the latest release of Jot"
        $headers = @{ "User-Agent" = "JotInstaller" }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $headers
        $asset = $release.assets | Where-Object { $_.name -like "jot-win-x64*.zip" } | Select-Object -First 1
        if (-not $asset) { throw "Could not find a download in the latest release." }

        $zip = Join-Path $env:TEMP "jot-download.zip"
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -Headers $headers
        Get-ChildItem -Path $AppDir -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -Path $zip -DestinationPath $AppDir -Force
        Remove-Item $zip -Force
    }

    if (-not (Test-Path $Exe)) { throw "Installation failed: jot.exe was not found in $AppDir." }
}

function Register-Integration {
    Write-Step "Registering the background agent, hotkey, and 'Edit with Jot' menu"

    # Start the warm agent at login so opens are instant.
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
        -Name $AppName -Value "`"$Exe`" --agent"

    # "Edit with Jot" on the right-click menu for every file. The key name is a literal "*",
    # which the registry provider treats as a wildcard, so use the .NET API directly.
    $shellKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\Classes\*\shell\$AppName")
    $shellKey.SetValue("", "Edit with Jot")
    $shellKey.SetValue("Icon", $Exe)
    $commandKey = $shellKey.CreateSubKey("command")
    $commandKey.SetValue("", "`"$Exe`" `"%1`"")
    $commandKey.Close()
    $shellKey.Close()

    # Start-menu shortcut.
    $programs = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $programs "Jot.lnk"))
    $shortcut.TargetPath = $Exe
    $shortcut.IconLocation = $Exe
    $shortcut.Description = "Jot, a fast single-file editor"
    $shortcut.Save()
}

function Start-Agent {
    Write-Step "Starting Jot"
    Start-Process -FilePath $Exe -ArgumentList "--agent"
}

if (-not (Test-DotNet8)) { Install-DotNet8 }
Stop-Jot
Copy-Application
Register-Integration
Start-Agent

if (-not $Quiet) {
    Write-Host ""
    Write-Host "Jot is installed." -ForegroundColor Green
    Write-Host "  - Select a file in File Explorer and press Ctrl+Space to open it." -ForegroundColor Gray
    Write-Host "  - Or right-click a file and choose 'Edit with Jot'." -ForegroundColor Gray
    Write-Host "  - Run this installer again at any time to update to the latest version." -ForegroundColor Gray
}
