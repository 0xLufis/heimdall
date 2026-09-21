<#
.SYNOPSIS
    Installs the Heimdall Industrial Edge Agent as a native Windows Service.
.DESCRIPTION
    Registers HeimdallAgent.exe with the Windows Service Control Manager,
    configures automatic recovery actions, and starts the service.
#>
[CmdletBinding()]
param (
    [string]$BinaryPath = "$PSScriptRoot\HeimdallAgent.exe",
    [string]$InstallDir = "C:\Program Files\Heimdall\Agent",
    [string]$ServiceName = "HeimdallAgent",
    [string]$DisplayName = "Heimdall Industrial Edge Telemetry Agent"
)

$ErrorActionPreference = "Stop"

# Verify administrator rights
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be executed in an elevated PowerShell session (Run as Administrator)."
    exit 1
}

Write-Host "=== Installing Heimdall Industrial Edge Agent (Windows Service) ===" -ForegroundColor Cyan

if (-not (Test-Path $BinaryPath)) {
    Write-Error "Binary not found at '$BinaryPath'."
    exit 1
}

# Create installation directory
if (-not (Test-Path $InstallDir)) {
    Write-Host "Creating installation directory at $InstallDir..."
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
}

# Copy binary and configuration
$TargetExe = Join-Path $InstallDir "HeimdallAgent.exe"
Write-Host "Copying binary to $TargetExe..."
Copy-Item -Path $BinaryPath -Destination $TargetExe -Force

if (Test-Path "$PSScriptRoot\default-config.json") {
    $TargetConfig = Join-Path $InstallDir "agent.json"
    if (-not (Test-Path $TargetConfig)) {
        Write-Host "Copying default configuration to $TargetConfig..."
        Copy-Item -Path "$PSScriptRoot\default-config.json" -Destination $TargetConfig -Force
    }
}

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Write-Host "Removing existing service registration..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create Windows Service
Write-Host "Creating Windows Service '$ServiceName'..."
New-Service -Name $ServiceName `
            -BinaryPathName "`"$TargetExe`"" `
            -DisplayName $DisplayName `
            -Description "Monitors industrial automation hardware, SCADA files, OPC UA, and Beckhoff ADS telemetry." `
            -StartupType Automatic

# Configure recovery actions (restart on failure after 5s, 10s, 60s)
Write-Host "Configuring automatic service recovery policy..."
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null

# Start service
Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

Write-Host "=== Heimdall Agent Service Installed and Running Successfully ===" -ForegroundColor Green
Get-Service -Name $ServiceName
