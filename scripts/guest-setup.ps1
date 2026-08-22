$ErrorActionPreference = "Continue"
$DataRoot = "C:\ClosedEnv\data"
$LogPath = Join-Path $DataRoot "guest.log"
$SessionPath = Join-Path $DataRoot "session.json"

function Write-Log {
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [string]$Detail = ""
    )
    $line = "{0:o}`t{1}`t{2}" -f (Get-Date), $Action, $Detail
    try { Add-Content -Path $LogPath -Value $line -Encoding UTF8 } catch {}
}

function Convert-ToStringArray {
    param($Value)
    if ($null -eq $Value) { return @() }
    if ($Value -is [string]) { return @($Value) }
    $list = @()
    foreach ($item in @($Value)) {
        if ($null -ne $item -and "$item".Trim() -ne "") {
            $list += "$item"
        }
    }
    return $list
}

function Get-HostAddressesSafe {
    param([string]$Name)
    $ips = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($entry in [System.Net.Dns]::GetHostEntry($Name).AddressList) {
            if ($null -ne $entry) { [void]$ips.Add($entry.ToString()) }
        }
    } catch {
        try {
            foreach ($entry in [System.Net.Dns]::GetHostAddresses($Name)) {
                if ($null -ne $entry) { [void]$ips.Add($entry.ToString()) }
            }
        } catch {
            Write-Log -Action dns -Detail "fail $Name $($_.Exception.Message)"
        }
    }
    return $ips
}

function Enable-GuestFirewall {
    param([string[]]$Domains)
    Write-Log -Action firewall -Detail "apply allowlist"
    netsh advfirewall firewall add rule name="ClosedEnv DNS UDP" dir=out action=allow protocol=UDP remoteport=53 | Out-Null
    netsh advfirewall firewall add rule name="ClosedEnv DNS TCP" dir=out action=allow protocol=TCP remoteport=53 | Out-Null
    netsh advfirewall firewall add rule name="ClosedEnv DHCP" dir=out action=allow protocol=UDP remoteport=67 | Out-Null
    netsh advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound | Out-Null
    foreach ($domain in $Domains) {
        $clean = "$domain".Trim()
        if ($clean.StartsWith("*.")) { $clean = $clean.Substring(2) }
        if ([string]::IsNullOrWhiteSpace($clean)) { continue }
        foreach ($ip in (Get-HostAddressesSafe -Name $clean)) {
            netsh advfirewall firewall add rule name="ClosedEnv $clean $ip" dir=out action=allow remoteip=$ip | Out-Null
            Write-Log -Action allow-ip -Detail "$clean $ip"
        }
    }
}

function Ensure-Junction {
    param([string]$Link, [string]$Target)
    if ([string]::IsNullOrWhiteSpace($Link) -or [string]::IsNullOrWhiteSpace($Target)) { return }
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    if (Test-Path $Link) {
        $item = Get-Item $Link -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { return }
        return
    }
    $parent = Split-Path $Link -Parent
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    cmd /c "mklink /J `"$Link`" `"$Target`"" | Out-Null
    Write-Log -Action junction -Detail "$Link -> $Target"
}

function Find-AppExe {
    param([string]$Root, [string]$PreferredName)
    if (-not (Test-Path $Root)) { return $null }
    if ($PreferredName) {
        $direct = Join-Path $Root $PreferredName
        if (Test-Path $direct) { return $direct }
        $found = Get-ChildItem -Path $Root -Recurse -Filter $PreferredName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    $candidates = Get-ChildItem -Path $Root -Recurse -Filter "*.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch "uninstall|update|crash|helper|setup" }
    $named = $candidates | Where-Object { $_.BaseName -match "^(MAX|max)$" } | Select-Object -First 1
    if ($named) { return $named.FullName }
    if ($candidates) { return $candidates[0].FullName }
    return $null
}

New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
Write-Log -Action start -Detail "guest-setup"

if (-not (Test-Path $SessionPath)) {
    Write-Log -Action error -Detail "session.json missing"
    Start-Process explorer.exe $DataRoot
    exit 1
}

$session = Get-Content -Path $SessionPath -Raw -Encoding UTF8 | ConvertFrom-Json
$allowlist = Convert-ToStringArray $session.allowlist
$persistFolders = Convert-ToStringArray $session.persistFolders
$installRoot = "C:\ClosedEnv\data\app"
if ($session.installRoot) { $installRoot = [string]$session.installRoot }

if ($session.guestFirewall -eq $true -and $allowlist.Count -gt 0) {
    Enable-GuestFirewall -Domains $allowlist
}

foreach ($name in $persistFolders) {
    Ensure-Junction -Link (Join-Path $env:LOCALAPPDATA $name) -Target (Join-Path $DataRoot "userdata\Local\$name")
    Ensure-Junction -Link (Join-Path $env:APPDATA $name) -Target (Join-Path $DataRoot "userdata\Roaming\$name")
}

$mode = [string]$session.mode
if ([string]::IsNullOrWhiteSpace($mode)) { $mode = [string]$session.profileId }

if ($mode -eq "generic") {
    $payload = [string]$session.payloadPath
    if ([string]::IsNullOrWhiteSpace($payload) -or -not (Test-Path $payload)) {
        $payloadDir = "C:\ClosedEnv\data\payload"
        $payload = Get-ChildItem -Path $payloadDir -File -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    }
    if ($payload -and (Test-Path $payload)) {
        Write-Log -Action launch -Detail $payload
        Start-Process -FilePath $payload
        exit 0
    }
    Write-Log -Action error -Detail "payload missing"
    Start-Process explorer.exe $DataRoot
    exit 1
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
$exe = Find-AppExe -Root $installRoot -PreferredName ([string]$session.launchRelativePath)
if (-not $exe) {
    $pf = "C:\Program Files\MAX"
    if (Test-Path $pf) {
        Write-Log -Action copy -Detail "Program Files\MAX -> persist"
        Copy-Item -Path $pf -Destination $installRoot -Recurse -Force
        $exe = Find-AppExe -Root $installRoot -PreferredName ([string]$session.launchRelativePath)
    }
}

if (-not $exe) {
    $cacheDir = Join-Path $DataRoot "cache"
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $installerName = [string]$session.installerFileName
    if ([string]::IsNullOrWhiteSpace($installerName)) { $installerName = "MAX.msi" }
    $installer = Join-Path $cacheDir $installerName
    $url = [string]$session.downloadUrl
    if (-not (Test-Path $installer) -and -not [string]::IsNullOrWhiteSpace($url)) {
        Write-Log -Action download -Detail $url
        try {
            Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing
            Write-Log -Action download -Detail "complete $url"
        } catch {
            Write-Log -Action download -Detail "fail $url $($_.Exception.Message)"
        }
    }
    if (Test-Path $installer) {
        Write-Log -Action install -Detail $installer
        $args = "/i `"$installer`" /qn /norestart INSTALL_ROOT=`"$installRoot`""
        $p = Start-Process -FilePath "msiexec.exe" -ArgumentList $args -Wait -PassThru
        Write-Log -Action install -Detail "msiexec $($p.ExitCode)"
        if (Test-Path "C:\Program Files\MAX") {
            Copy-Item -Path "C:\Program Files\MAX" -Destination $installRoot -Recurse -Force
        }
        $exe = Find-AppExe -Root $installRoot -PreferredName ([string]$session.launchRelativePath)
        if (-not $exe) {
            $exe = Find-AppExe -Root "C:\Program Files\MAX" -PreferredName ([string]$session.launchRelativePath)
        }
    }
}

if ($exe -and (Test-Path $exe)) {
    Write-Log -Action launch -Detail $exe
    Start-Process -FilePath $exe
    exit 0
}

Write-Log -Action error -Detail "application not found"
Start-Process explorer.exe $DataRoot
exit 1
