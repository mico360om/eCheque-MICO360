<#
    Removes the eCheque MICO360 Sync Server Windows Service.
    Run in an elevated PowerShell:  .\Uninstall-Server.ps1
    (The database in C:\eCheque\Data is left in place — delete it manually if you want a clean wipe.)
#>
#Requires -RunAsAdministrator
$ErrorActionPreference = 'SilentlyContinue'

$Svc  = 'eChequeSync'
$Port = 5210

if (Get-Service $Svc) {
    Stop-Service $Svc -Force
    sc.exe delete $Svc | Out-Null
    Write-Host "Service '$Svc' removed."
} else {
    Write-Host "Service '$Svc' was not installed."
}
Remove-NetFirewallRule -DisplayName "eCheque Sync $Port"
Write-Host "Done. Program files remain in C:\eCheque\Server; data in C:\eCheque\Data."
