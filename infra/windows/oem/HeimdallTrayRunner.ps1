# ==============================================================================
# Heimdall Industrial Edge Agent Taskbar System Tray Runner
# Runs as an interactive taskbar notification tray icon in Windows environments.
# Provides quick access to Web Dashboard, instant telemetry triggers, ADS status, and restart controls.
# ==============================================================================

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$agentUrl = "http://localhost:5998"

# 1. Create Notification Tray Icon
$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Text = "Heimdall Industrial Edge Agent`nADS: 48898 RUN | OPC: Connected"
$notifyIcon.Visible = $true

# Generate a clean Shield icon using System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 32, 32
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(99, 102, 241)) # Indigo
$graphics.FillEllipse($brush, 2, 2, 28, 28)
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font ("Arial", 14, [System.Drawing.FontStyle]::Bold)
$graphics.DrawString("H", $font, $textBrush, 7, 4)
$notifyIcon.Icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())

# 2. Context Menu
$contextMenu = New-Object System.Windows.Forms.ContextMenuStrip

# Item: Open Dashboard
$itemOpen = $contextMenu.Items.Add("🌐 Open Agent Web Dashboard (Port 5998)")
$itemOpen.Font = New-Object System.Drawing.Font($contextMenu.Font, [System.Drawing.FontStyle]::Bold)
$itemOpen.add_Click({
    Start-Process "$agentUrl/"
})

$contextMenu.Items.Add("-") | Out-Null

# Item: Trigger Telemetry
$itemTrigger = $contextMenu.Items.Add("🚀 Trigger Immediate Telemetry Report")
$itemTrigger.add_Click({
    try {
        $res = Invoke-RestMethod -Uri "$agentUrl/api/trigger-report" -Method Post -TimeoutSec 3
        $notifyIcon.ShowBalloonTip(3000, "Heimdall Telemetry", "Immediate report successfully dispatched to backend gRPC.", [System.Windows.Forms.ToolTipIcon]::Info)
    } catch {
        $notifyIcon.ShowBalloonTip(3000, "Heimdall Telemetry", "Notice: Backend unreachable or agent offline: $_", [System.Windows.Forms.ToolTipIcon]::Warning)
    }
})

# Item: Toggle ADS State
$itemAds = $contextMenu.Items.Add("⚡ Toggle TwinCAT ADS State (RUN / STOP)")
$itemAds.add_Click({
    try {
        $res = Invoke-RestMethod -Uri "$agentUrl/api/ads/toggle" -Method Post -TimeoutSec 3
        $stateName = if ($res.state -eq 5) { "RUN" } else { "STOP" }
        $notifyIcon.ShowBalloonTip(3000, "TwinCAT ADS Runtime", "Simulated ADS state changed to $stateName", [System.Windows.Forms.ToolTipIcon]::Info)
    } catch {
        $notifyIcon.ShowBalloonTip(3000, "TwinCAT ADS Runtime", "Failed to communicate with agent ADS server: $_", [System.Windows.Forms.ToolTipIcon]::Warning)
    }
})

# Item: Show Status
$itemStatus = $contextMenu.Items.Add("📊 View Agent Status Summary")
$itemStatus.add_Click({
    try {
        $status = Invoke-RestMethod -Uri "$agentUrl/api/status" -TimeoutSec 3
        $msg = "Host: $($status.hostname)`nADS: $($status.ads.state) ($($status.ads.amsNetId))`nOPC: $($status.opc.endpointUrl)`nTotal Reports: $($status.triggers.totalReports)"
        [System.Windows.Forms.MessageBox]::Show($msg, "Heimdall Agent Status", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
    } catch {
        [System.Windows.Forms.MessageBox]::Show("Agent daemon not responding on $agentUrl.", "Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    }
})

$contextMenu.Items.Add("-") | Out-Null

# Item: Exit Tray Icon
$itemExit = $contextMenu.Items.Add("❌ Exit Tray Icon")
$itemExit.add_Click({
    $notifyIcon.Visible = $false
    $notifyIcon.Dispose()
    [System.Windows.Forms.Application]::Exit()
})

$notifyIcon.ContextMenuStrip = $contextMenu

# Double click launches dashboard
$notifyIcon.add_DoubleClick({
    Start-Process "$agentUrl/"
})

# Display initial balloon
$notifyIcon.ShowBalloonTip(4000, "Heimdall Industrial Edge Agent", "Agent running with Beckhoff TwinCAT ADS simulation & OPC client active.", [System.Windows.Forms.ToolTipIcon]::Info)

# Run application message loop
[System.Windows.Forms.Application]::Run()
