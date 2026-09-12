# ==============================================================================
# Heimdall Windows 10 LTSC Test Node Provisioning Script
# ==============================================================================

$ErrorActionPreference = "Continue"

Write-Output "[Heimdall] Starting Windows 10 LTSC Provisioning..."

# 1. Configure WinRM (Windows Remote Management) for Headless Test Automation
try {
    Write-Output "[Heimdall] Configuring WinRM..."
    Set-Service WinRM -StartupType Automatic
    Start-Service WinRM
    Enable-PSRemoting -Force -SkipNetworkProfileCheck

    winrm set winrm/config/service/auth '@{Basic="true"}'
    winrm set winrm/config/service '@{AllowUnencrypted="true"}'
    winrm set winrm/config/winrs '@{MaxMemoryPerShellMB="1024"}'
    
    # Configure Windows Firewall for WinRM, Configurator, and SSH
    New-NetFirewallRule -Name "Heimdall_WinRM" -DisplayName "Heimdall WinRM HTTP" -Protocol TCP -LocalPort 5985 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_Agent_Config" -DisplayName "Heimdall Agent Configurator" -Protocol TCP -LocalPort 5998 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_SSH" -DisplayName "Heimdall OpenSSH" -Protocol TCP -LocalPort 22 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    Write-Output "[Heimdall] WinRM and Firewall configured."
} catch {
    Write-Warning "[Heimdall] WinRM configuration encountered: $_"
}

# 2. Setup Heimdall Data Directories
Write-Output "[Heimdall] Initializing Heimdall directories..."
$DataDir = "C:\ProgramData\Heimdall"
$AppDir = "C:\Heimdall\Agent"
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
New-Item -ItemType Directory -Force -Path $AppDir | Out-Null

# 3. Seed Mock Industrial OT Metadata (Beckhoff TwinCAT)
Write-Output "[Heimdall] Seeding mock industrial registry keys..."
try {
    # Beckhoff Real-Time Driver Service
    $TcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\TcRTime"
    if (-not (Test-Path $TcKey)) {
        New-Item -Path $TcKey -Force | Out-Null
    }
    Set-ItemProperty -Path $TcKey -Name "DisplayName" -Value "TwinCAT Real-Time Driver" -Force
    Set-ItemProperty -Path $TcKey -Name "Description" -Value "Beckhoff TwinCAT 3 Real-Time Kernel Module" -Force
    Set-ItemProperty -Path $TcKey -Name "Start" -Value 2 -Force

    # Beckhoff TwinCAT Version
    $TcVersionKey = "HKLM:\SOFTWARE\Beckhoff\TwinCAT3"
    if (-not (Test-Path $TcVersionKey)) {
        New-Item -Path $TcVersionKey -Force | Out-Null
    }
    Set-ItemProperty -Path $TcVersionKey -Name "CurrentVersion" -Value "3.1.4024.55" -Force

    # Ensure MachineGuid exists in Cryptography
    $CryptoKey = "HKLM:\SOFTWARE\Microsoft\Cryptography"
    if (Test-Path $CryptoKey) {
        $guid = (Get-ItemProperty -Path $CryptoKey -Name "MachineGuid" -ErrorAction SilentlyContinue).MachineGuid
        if (-not $guid) {
            Set-ItemProperty -Path $CryptoKey -Name "MachineGuid" -Value ([guid]::NewGuid().ToString()) -Force
        }
    }
    Write-Output "[Heimdall] Industrial registry keys seeded."
} catch {
    Write-Warning "[Heimdall] Failed to seed registry keys: $_"
}

# 4. Generate Default Agent Configuration in C:\ProgramData\Heimdall\agent.json
$DefaultConfig = @{
    ConfigSchemaVersion = "1.0.0"
    BackendUrl = "http://backend:5001"
    AuthType = "NoAuth"
    AllowRemoteExecution = $true
    AllowUnsignedCommands = $true
    EnforceHardwareBinding = $false
    SpoolEncryptionMode = "Plaintext"
    HeartbeatIntervalSeconds = 5
} | ConvertTo-Json -Depth 5

Set-Content -Path "$DataDir\agent.json" -Value $DefaultConfig -Force
Write-Output "[Heimdall] Default agent.json written to $DataDir\agent.json"

# 5. Create Background Daemon Auto-Start Watcher
# Looks for published agent binaries in Z:\agent\ (shared volume) or C:\Heimdall\Agent\
$LauncherScript = @"
`$ErrorActionPreference = 'Continue'
`$SourcePath = 'Z:\agent'
`$TargetPath = 'C:\Heimdall\Agent'

Write-Host '[Watcher] Waiting for agent binary in ' + `$SourcePath
while (`$true) {
    if (Test-Path "`$SourcePath\App.Agent.Daemon.exe") {
        Write-Host '[Watcher] Binary detected. Syncing and launching...'
        Copy-Item -Path "`$SourcePath\*" -Destination `$TargetPath -Recurse -Force -ErrorAction SilentlyContinue
        
        `$env:AGENT_URLS = 'http://0.0.0.0:5998'
        `$env:Backend__Url = 'http://backend:5001'
        `$env:DOTNET_ENVIRONMENT = 'Development'
        
        Set-Location `$TargetPath
        Start-Process -FilePath "`$TargetPath\App.Agent.Daemon.exe" -ArgumentList '--urls http://0.0.0.0:5998' -Wait
    }
    Start-Sleep -Seconds 3
}
"@

Set-Content -Path "C:\Heimdall\watcher.ps1" -Value $LauncherScript -Force

# Create Scheduled Task to run watcher at startup with highest privileges
try {
    $Action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File C:\Heimdall\watcher.ps1"
    $Trigger = New-ScheduledTaskTrigger -AtStartup
    $Principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName "HeimdallAgentWatcher" -Action $Action -Trigger $Trigger -Principal $Principal -Force
    Start-ScheduledTask -TaskName "HeimdallAgentWatcher" -ErrorAction SilentlyContinue
    Write-Output "[Heimdall] Agent launcher task registered."
} catch {
    Write-Warning "[Heimdall] Scheduled task registration error: $_"
}

# 6. Write Ready Sentinel File
Set-Content -Path "C:\heimdall_ready.txt" -Value "READY - $(Get-Date -Format 'o')" -Force
Write-Output "[Heimdall] Provisioning complete. Ready sentinel written to C:\heimdall_ready.txt"
