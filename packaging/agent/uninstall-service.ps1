<#
.SYNOPSIS
    Uninstalls the Heimdall Industrial Edge Agent Windows Service.
#>
[CmdletBinding()]
param (
    [string]$ServiceName = "HeimdallAgent"
)

$ErrorActionPreference = "Stop"

$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be executed in an elevated PowerShell session (Run as Administrator)."
    exit 1
}

Write-Host "=== Uninstalling Heimdall Industrial Edge Agent (Windows Service) ===" -ForegroundColor Cyan

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service '$ServiceName'..."
        Stop-Service -Name $ServiceName -Force
    }

    Write-Host "Removing service '$ServiceName' from Service Control Manager..."
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "=== Heimdall Agent Service Uninstalled Successfully ===" -ForegroundColor Green
} else {
    Write-Host "Service '$ServiceName' was not found on this system." -ForegroundColor Yellow
}
