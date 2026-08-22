#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"

Write-Host "Enabling Windows Sandbox (Containers-DisposableClientVM)..."
$feature = Get-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM"
if ($feature.State -eq "Enabled") {
    Write-Host "Windows Sandbox is already enabled."
    exit 0
}

Enable-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM" -All -NoRestart
Write-Host ""
Write-Host "Feature enable requested. Reboot the computer, then start ClosedEnv again."
Write-Host "If this fails, turn on virtualization in BIOS/UEFI and use Windows Pro/Enterprise/Education."
