<#
    Installs the eCheque MICO360 Sync Server as a Windows Service.

    HOW TO USE (on the server, e.g. your VPS 84.247.142.2):
      1. Copy eCheque.MICO360.Server.exe and this script into the same folder.
      2. Open PowerShell *as Administrator* in that folder.
      3. Edit $OrgKey below to a long random secret, then run:  .\Install-Server.ps1

    The service auto-starts on boot and restarts on failure. Config is written to
    echeque.server.json next to the EXE (not env vars), so it is reliable for a service.
#>
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

# ============ EDIT THESE ============
$OrgKey = 'CHANGE-ME-to-a-long-random-secret'   # clients register with this key
$Port   = 5210                                  # TCP port the server listens on
# ====================================

$Svc        = 'eChequeSync'
$InstallDir = 'C:\eCheque\Server'
$DataDir    = 'C:\eCheque\Data'
$Exe        = Join-Path $InstallDir 'eCheque.MICO360.Server.exe'

Write-Host "Installing eCheque Sync Server..." -ForegroundColor Cyan

# 1. Folders + copy the EXE
New-Item -ItemType Directory -Force -Path $InstallDir, $DataDir | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'eCheque.MICO360.Server.exe') $Exe -Force

# 2. Config file next to the EXE (read at startup)
@{
    ECHEQUE_ORG_KEY   = $OrgKey
    ECHEQUE_SERVER_DB = (Join-Path $DataDir 'server.db')
    Urls              = "http://0.0.0.0:$Port"
} | ConvertTo-Json | Set-Content (Join-Path $InstallDir 'echeque.server.json') -Encoding UTF8

# 3. Firewall — allow inbound TCP on the port
New-NetFirewallRule -DisplayName "eCheque Sync $Port" -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort $Port -ErrorAction SilentlyContinue | Out-Null

# 4. (Re)create the service: auto-start + auto-restart on failure
if (Get-Service $Svc -ErrorAction SilentlyContinue) {
    Stop-Service $Svc -Force -ErrorAction SilentlyContinue
    sc.exe delete $Svc | Out-Null
    Start-Sleep 2
}
New-Service -Name $Svc -BinaryPathName "`"$Exe`"" -DisplayName 'eCheque MICO360 Sync Server' `
    -Description 'Central data-sync server for eCheque MICO360.' -StartupType Automatic | Out-Null
sc.exe failure $Svc reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
Start-Service $Svc

# 5. Verify
Start-Sleep 3
Get-Service $Svc | Format-Table Name, Status, StartType -Auto
try   { Write-Host ("Health: " + (Invoke-WebRequest "http://localhost:$Port/api/health" -UseBasicParsing).Content) -ForegroundColor Green }
catch { Write-Warning "Health check failed (give it a few seconds and retry): $_" }

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
Write-Host "  Clients set Server URL: http://<this-server-ip>:$Port   (use https:// once TLS is configured)"
Write-Host "  Clients set Organisation key: $OrgKey"
