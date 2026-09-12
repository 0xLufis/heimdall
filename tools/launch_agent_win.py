import winrm

s = winrm.Session('http://127.0.0.1:5985/wsman', auth=('Docker', 'Password123!'), transport='ntlm')

ps_script = r"""
$hostsPath = 'C:\Windows\System32\drivers\etc\hosts'
if (-not (Select-String -Path $hostsPath -Pattern 'backend' -SimpleMatch)) {
    Add-Content -Path $hostsPath -Value "`r`n172.18.0.1 backend"
}

Copy-Item -Path 'C:\Users\Docker\Desktop\Shared\agent\*' -Destination 'C:\Heimdall\Agent' -Recurse -Force

$config = @{
    ConfigSchemaVersion = '1.0.0'
    BackendUrl = 'http://backend:5001'
    AuthType = 'NoAuth'
    AllowRemoteExecution = $true
    AllowUnsignedCommands = $true
    EnforceHardwareBinding = $false
    SpoolEncryptionMode = 'Plaintext'
    HeartbeatIntervalSeconds = 5
} | ConvertTo-Json
Set-Content -Path 'C:\ProgramData\Heimdall\agent.json' -Value $config -Force

$env:AGENT_URLS = 'http://0.0.0.0:5998'
$env:Backend__Url = 'http://backend:5001'
$env:DOTNET_ENVIRONMENT = 'Development'

Start-Process -FilePath 'C:\Heimdall\Agent\App.Agent.Daemon.exe' -ArgumentList '--urls http://0.0.0.0:5998' -WorkingDirectory 'C:\Heimdall\Agent'
Start-Sleep -Seconds 3

Get-Process -Name 'App.Agent.Daemon' -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, WorkingSet64
"""

r = s.run_ps(ps_script)
print("Exit code:", r.status_code)
print("Stdout:", r.std_out.decode())
if r.std_err:
    print("Stderr:", r.std_err.decode())
