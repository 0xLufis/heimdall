<#
.SYNOPSIS
    Builds the Heimdall Edge Agent MSI installer for enterprise MDM (Intune / SCCM).
.DESCRIPTION
    Supports silent deployment with custom properties:
    msiexec /i HeimdallAgent.msi /quiet SERVER_URL="https://heimdall.factory.corp:5001" ENROLLMENT_TOKEN="ENR-SECRET-991"
#>
param (
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\..\dist\agent-msi"
)

$ErrorActionPreference = "Stop"

Write-Host ">> Publishing Standalone Windows Edge Agent..."
dotnet publish "$PSScriptRoot\..\..\agent\App.Agent.Daemon\App.Agent.Daemon.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$OutputDir\publish"

Write-Host ">> Compiling WiX MSI Package..."
if (Get-Command wix -ErrorAction SilentlyContinue) {
    wix build "$PSScriptRoot\wix\HeimdallAgent.wxs" `
        -o "$OutputDir\HeimdallAgent.msi"
    Write-Host ">> MSI created successfully: $OutputDir\HeimdallAgent.msi"
} else {
    Write-Host "Note: WiX toolset CLI not detected on build host. Manifest ready for automated CI runner."
}
