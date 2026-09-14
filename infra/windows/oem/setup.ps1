# ==============================================================================
# Heimdall Windows 10 LTSC Test Node Provisioning Script
# Industrial OT Environment: Beckhoff TwinCAT ADS Runtime, OPC UA, & Heimdall Agent
# ==============================================================================

$ErrorActionPreference = "Continue"

Write-Output "[Heimdall] Starting Windows 10 LTSC Industrial Provisioning..."

# 1. Configure WinRM (Windows Remote Management) & Industrial OT Firewall Rules
try {
    Write-Output "[Heimdall] Configuring WinRM and Industrial OT Firewall..."
    Set-Service WinRM -StartupType Automatic
    Start-Service WinRM
    Enable-PSRemoting -Force -SkipNetworkProfileCheck

    winrm set winrm/config/service/auth '@{Basic="true"}'
    winrm set winrm/config/service '@{AllowUnencrypted="true"}'
    winrm set winrm/config/winrs '@{MaxMemoryPerShellMB="1024"}'
    
    # Configure Windows Firewall
    New-NetFirewallRule -Name "Heimdall_WinRM" -DisplayName "Heimdall WinRM HTTP" -Protocol TCP -LocalPort 5985 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_Agent_Config" -DisplayName "Heimdall Agent Configurator" -Protocol TCP -LocalPort 5998 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_SSH" -DisplayName "Heimdall OpenSSH" -Protocol TCP -LocalPort 22 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_TwinCAT_ADS" -DisplayName "Beckhoff TwinCAT ADS Protocol" -Protocol TCP -LocalPort 48898 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    New-NetFirewallRule -Name "Heimdall_OPC_UA" -DisplayName "OPC Unified Architecture (OPC UA)" -Protocol TCP -LocalPort 4840 -Action Allow -Profile Any -ErrorAction SilentlyContinue
    Write-Output "[Heimdall] WinRM and Firewall rules configured (5985, 5998, 22, 48898, 4840)."
} catch {
    Write-Warning "[Heimdall] WinRM / Firewall configuration notice: $_"
}

# 2. Setup Heimdall Data Directories
Write-Output "[Heimdall] Initializing Heimdall directories..."
$DataDir = "C:\ProgramData\Heimdall"
$AppDir = "C:\Heimdall\Agent"
$TrayDir = "C:\Heimdall"
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
New-Item -ItemType Directory -Force -Path $AppDir | Out-Null
New-Item -ItemType Directory -Force -Path $TrayDir | Out-Null

# 3. Seed Realistic Industrial OT Metadata (Beckhoff TwinCAT 3 & EtherCAT)
Write-Output "[Heimdall] Seeding industrial OT registry hierarchy..."
try {
    # Beckhoff Real-Time Driver Service
    $TcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\TcRTime"
    if (-not (Test-Path $TcKey)) { New-Item -Path $TcKey -Force | Out-Null }
    Set-ItemProperty -Path $TcKey -Name "DisplayName" -Value "TwinCAT Real-Time Driver" -Force
    Set-ItemProperty -Path $TcKey -Name "Description" -Value "Beckhoff TwinCAT 3 Real-Time Kernel Module (Ring-0 Scheduler)" -Force
    Set-ItemProperty -Path $TcKey -Name "Start" -Value 2 -Force
    Set-ItemProperty -Path $TcKey -Name "LatencyLimitUs" -Value 15 -Force
    Set-ItemProperty -Path $TcKey -Name "DedicatedCores" -Value 1 -Force

    # Beckhoff TwinCAT Version & Router Configuration
    $TcVersionKey = "HKLM:\SOFTWARE\Beckhoff\TwinCAT3"
    if (-not (Test-Path $TcVersionKey)) { New-Item -Path $TcVersionKey -Force | Out-Null }
    Set-ItemProperty -Path $TcVersionKey -Name "CurrentVersion" -Value "3.1.4026.11" -Force
    Set-ItemProperty -Path $TcVersionKey -Name "AmsNetId" -Value "5.80.201.44.1.1" -Force
    Set-ItemProperty -Path $TcVersionKey -Name "TargetRuntime" -Value "TwinCAT PLC (Port 851)" -Force

    # Beckhoff EtherCAT Master & I/O Coupler Devices
    $TcIoKey = "HKLM:\SOFTWARE\Beckhoff\TwinCAT3\System\IoDevices"
    if (-not (Test-Path $TcIoKey)) { New-Item -Path $TcIoKey -Force | Out-Null }
    Set-ItemProperty -Path $TcIoKey -Name "Master1_Type" -Value "EtherCAT Master (Device 1)" -Force
    Set-ItemProperty -Path $TcIoKey -Name "Master1_BusCoupler" -Value "EK1100 EtherCAT Coupler" -Force
    Set-ItemProperty -Path $TcIoKey -Name "Master1_Terminals" -Value "EL1008 (DI 8ch), EL2008 (DO 8ch), EL3104 (AI 4ch +-10V)" -Force
    Set-ItemProperty -Path $TcIoKey -Name "Master1_CycleTimeUs" -Value 1000 -Force

    # OPC UA Server Mock Configuration
    $OpcKey = "HKLM:\SOFTWARE\OPC\UnifiedArchitecture"
    if (-not (Test-Path $OpcKey)) { New-Item -Path $OpcKey -Force | Out-Null }
    Set-ItemProperty -Path $OpcKey -Name "DefaultEndpoint" -Value "opc.tcp://127.0.0.1:4840" -Force
    Set-ItemProperty -Path $OpcKey -Name "SecurityPolicies" -Value "None,Basic256Sha256" -Force

    # Ensure MachineGuid exists in Cryptography
    $CryptoKey = "HKLM:\SOFTWARE\Microsoft\Cryptography"
    if (Test-Path $CryptoKey) {
        $guid = (Get-ItemProperty -Path $CryptoKey -Name "MachineGuid" -ErrorAction SilentlyContinue).MachineGuid
        if (-not $guid) {
            Set-ItemProperty -Path $CryptoKey -Name "MachineGuid" -Value ([guid]::NewGuid().ToString()) -Force
        }
    }
    Write-Output "[Heimdall] Industrial OT registry seeded (TwinCAT 3.1.4026.11, EtherCAT EK1100, OPC UA 4840)."
} catch {
    Write-Warning "[Heimdall] Notice while seeding registry keys: $_"
}

# 4. Generate Default Agent Configuration in C:\ProgramData\Heimdall\agent.json
$DefaultConfig = @{
    ConfigSchemaVersion = "1.1.0"
    BackendUrl = "http://backend:5001"
    AuthType = "NoAuth"
    AllowRemoteExecution = $true
    AllowUnsignedCommands = $true
    EnforceHardwareBinding = $false
    SpoolEncryptionMode = "Plaintext"
    HeartbeatIntervalSeconds = 5
    HardwarePollIntervalSeconds = 30
    AdsAmsNetId = "5.80.201.44.1.1"
    AdsPort = 48898
    OpcUaEndpoint = "opc.tcp://127.0.0.1:4840"
} | ConvertTo-Json -Depth 5

Set-Content -Path "$DataDir\agent.json" -Value $DefaultConfig -Force
Write-Output "[Heimdall] Default agent.json written to $DataDir\agent.json"

# 5. Copy Tray Icon Runner and Register for Interactive Login
if (Test-Path "C:\OEM\HeimdallTrayRunner.ps1") {
    Copy-Item -Path "C:\OEM\HeimdallTrayRunner.ps1" -Destination "C:\Heimdall\HeimdallTrayRunner.ps1" -Force
    # Register in Run registry key so the tray icon appears when interactive desktop session starts
    $RunKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $RunKey -Name "HeimdallTrayIcon" -Value "powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -File C:\Heimdall\HeimdallTrayRunner.ps1" -Force
    Write-Output "[Heimdall] Taskbar tray runner registered for interactive desktop sessions."
}

# 6. Create Background Daemon Auto-Start Watcher
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

# Create Scheduled Task to run daemon watcher at startup with highest privileges
try {
    $Action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File C:\Heimdall\watcher.ps1"
    $Trigger = New-ScheduledTaskTrigger -AtStartup
    $Principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName "HeimdallAgentWatcher" -Action $Action -Trigger $Trigger -Principal $Principal -Force
    Start-ScheduledTask -TaskName "HeimdallAgentWatcher" -ErrorAction SilentlyContinue
    Write-Output "[Heimdall] Agent launcher scheduled task registered."
} catch {
    Write-Warning "[Heimdall] Scheduled task registration notice: $_"
}

# 7. Write Ready Sentinel File
Set-Content -Path "C:\heimdall_ready.txt" -Value "READY - $(Get-Date -Format 'o') - ADS:48898 OPC:4840" -Force
Write-Output "[Heimdall] Industrial provisioning complete. Ready sentinel written to C:\heimdall_ready.txt"
