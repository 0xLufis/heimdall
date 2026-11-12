<#
.SYNOPSIS
    Opens the Heimdall station terminal on a machine IPC as a chromeless browser window on top of the HMI.

.DESCRIPTION
    Starts Microsoft Edge (preferred) or Google Chrome in app mode (--app=<url>: no tabs, no address bar,
    no browser menus) with its own browser profile, then makes that window always-on-top with the Win32
    SetWindowPos(HWND_TOPMOST) call. The page opens in its compact overlay layout (?overlay=1); the
    operator expands it to the full terminal view from the page itself.

    -Kiosk opens the full terminal view full-screen instead (--kiosk), e.g. for an IPC without an HMI.

    The workstation token, when the deployment uses one (HEIMDALL_TERMINAL_TOKENS on the server), is
    passed as the ?token= query parameter; the terminal page forwards it to /api/terminal/*, which
    accepts it there. Anyone who can read the browser history of this dedicated profile can read the
    token, so keep the profile directory local to the IPC.

    Note: this is a normal (chromeless) browser window, not a headless browser. A headless browser
    has no window and therefore cannot be shown over the HMI.

.EXAMPLE
    .\Start-HeimdallTerminal.ps1 -MachineId OP10-LOAD -BaseUrl https://heimdall.plant.local -Token $env:HEIMDALL_TERMINAL_TOKEN

.EXAMPLE
    # Narrow strip on the left edge, kept on top even if the HMI raises itself
    .\Start-HeimdallTerminal.ps1 -MachineId OP10-LOAD -Dock Left -Width 360 -KeepOnTop

.EXAMPLE
    # Full-screen kiosk with the full terminal view
    .\Start-HeimdallTerminal.ps1 -MachineId OP10-LOAD -Kiosk
#>
[CmdletBinding()]
param(
    # Machine id or custom identifier of the station (the page is /terminal/machine/<MachineId>).
    [string]$MachineId = $env:HEIMDALL_STATION_ID,

    # Heimdall web front end, e.g. https://heimdall.plant.local (default: HEIMDALL_WEB_URL or http://localhost:3000).
    [string]$BaseUrl = $(if ($env:HEIMDALL_WEB_URL) { $env:HEIMDALL_WEB_URL } else { 'http://localhost:3000' }),

    # Workstation token (default: HEIMDALL_TERMINAL_TOKEN). Leave empty when the IPC authenticates otherwise.
    [string]$Token = $env:HEIMDALL_TERMINAL_TOKEN,

    # Overlay window size in screen units (DIPs).
    [int]$Width = 420,
    [int]$Height = 700,

    # Screen edge to place the overlay on when -X/-Y are not given.
    [ValidateSet('Right', 'Left')]
    [string]$Dock = 'Right',

    # Explicit window position (overrides -Dock).
    [int]$X,
    [int]$Y,

    # Full terminal view in full-screen kiosk mode instead of the overlay.
    [switch]$Kiosk,

    # Full terminal view in a normal app window (not on top, not full screen).
    [switch]$Full,

    # Keep re-applying always-on-top every 3 s while the window exists (for HMIs that raise themselves).
    [switch]$KeepOnTop,

    # Browser executable; default: Edge, then Chrome.
    [string]$BrowserPath,

    # Separate browser profile so the terminal never shares cookies, tabs or extensions with a normal browser.
    [string]$UserDataDir = (Join-Path $env:LOCALAPPDATA 'Heimdall\TerminalBrowser')
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Document title prefix set by the terminal page; used to find the window.
$WindowTitlePrefix = 'Heimdall Terminal'

if ([string]::IsNullOrWhiteSpace($MachineId)) {
    throw 'No station given. Pass -MachineId <machine id> or set HEIMDALL_STATION_ID.'
}

function Find-Browser {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe')
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }
    throw 'Neither Microsoft Edge nor Google Chrome was found. Pass -BrowserPath <path to msedge.exe or chrome.exe>.'
}

if (-not $BrowserPath) { $BrowserPath = Find-Browser }
$isEdge = [IO.Path]::GetFileNameWithoutExtension($BrowserPath) -ieq 'msedge'
$processName = [IO.Path]::GetFileNameWithoutExtension($BrowserPath)

# --- Terminal URL --------------------------------------------------------------------------------
$overlay = -not ($Kiosk -or $Full)
$query = @($(if ($overlay) { 'overlay=1' } else { 'overlay=0' }))
if (-not [string]::IsNullOrWhiteSpace($Token)) {
    $query += 'token=' + [uri]::EscapeDataString($Token.Trim())
}
$url = '{0}/terminal/machine/{1}?{2}' -f $BaseUrl.TrimEnd('/'), [uri]::EscapeDataString($MachineId.Trim()), ($query -join '&')

# --- Window geometry -----------------------------------------------------------------------------
Add-Type -AssemblyName System.Windows.Forms
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$Width = [Math]::Min($Width, $area.Width)
$Height = [Math]::Min($Height, $area.Height)
if (-not $PSBoundParameters.ContainsKey('X')) {
    $X = if ($Dock -eq 'Left') { $area.Left } else { $area.Right - $Width }
}
if (-not $PSBoundParameters.ContainsKey('Y')) {
    $Y = $area.Top + [Math]::Max(0, [int](($area.Height - $Height) / 2))
}

# --- Browser arguments ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $UserDataDir | Out-Null
$browserArgs = @(
    "--user-data-dir=`"$UserDataDir`"",
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-pinch',
    '--overscroll-history-navigation=0',
    '--disable-features=Translate',
    '--hide-crash-restore-bubble'
)
if ($Kiosk) {
    $browserArgs += '--kiosk'
    if ($isEdge) { $browserArgs += '--edge-kiosk-type=fullscreen' }
    $browserArgs += "`"$url`""
} else {
    $browserArgs += "--app=`"$url`""
    if ($overlay) {
        $browserArgs += "--window-size=$Width,$Height"
        $browserArgs += "--window-position=$X,$Y"
    }
}

Write-Verbose "Starting $BrowserPath $($browserArgs -join ' ')"
Start-Process -FilePath $BrowserPath -ArgumentList $browserArgs | Out-Null

if (-not $overlay) { return }

# --- Always on top -------------------------------------------------------------------------------
if (-not ('HeimdallTerminal.Win32' -as [type])) {
    Add-Type -Namespace HeimdallTerminal -Name Win32 -MemberDefinition @'
[DllImport("user32.dll", SetLastError = true)]
public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
[DllImport("user32.dll")]
public static extern bool IsWindow(IntPtr hWnd);
'@
}
$HWND_TOPMOST = [IntPtr]::new(-1)
$SWP_NOSIZE = 0x0001
$SWP_NOMOVE = 0x0002
$SWP_NOACTIVATE = 0x0010
$SWP_SHOWWINDOW = 0x0040

function Find-TerminalWindow {
    # The app window's title is the page title ("Heimdall Terminal - <station>"), set once the page has loaded.
    $proc = Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero -and $_.MainWindowTitle -like "$WindowTitlePrefix*" } |
        Select-Object -First 1
    if ($proc) { return $proc.MainWindowHandle }
    return [IntPtr]::Zero
}

$hwnd = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(30)
while ($hwnd -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    $hwnd = Find-TerminalWindow
}
if ($hwnd -eq [IntPtr]::Zero) {
    throw "The terminal window did not appear within 30 s (no '$processName' window titled '$WindowTitlePrefix*'). Is $BaseUrl reachable?"
}

# Only the Z-order changes; size and position stay as the browser applied them (no DPI conversion needed).
[void][HeimdallTerminal.Win32]::SetWindowPos($hwnd, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_SHOWWINDOW)
Write-Verbose "Terminal window $hwnd is now always on top."

if ($KeepOnTop) {
    while ([HeimdallTerminal.Win32]::IsWindow($hwnd)) {
        Start-Sleep -Seconds 3
        [void][HeimdallTerminal.Win32]::SetWindowPos($hwnd, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE -bor $SWP_NOACTIVATE)
    }
}
