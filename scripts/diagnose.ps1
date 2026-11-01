<#
    diagnose.ps1 - READ-ONLY diagnosis of Meta Horizon Link on Windows.

    Changes nothing. Use it to paste the result into an issue, or when the
    person prefers not to run the .exe.

    Usage:
        powershell -ExecutionPolicy Bypass -File diagnose.ps1
        powershell -ExecutionPolicy Bypass -File diagnose.ps1 | Out-File report.txt
#>

$ErrorActionPreference = "SilentlyContinue"
$DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}"
$DenyKey = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses"

function Line($status, $text) { "{0} {1}" -f $status.PadRight(6), $text }
function Head($text) { ""; "== $text" }

"Meta Horizon Link Doctor - read-only diagnosis"
"Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
"Computer: $env:COMPUTERNAME   User: $env:USERNAME"
""

Head "System"
$os = Get-CimInstance Win32_OperatingSystem
Line "INFO" "$($os.Caption) build $($os.BuildNumber)"
Line "INFO" "Architecture: $($os.OSArchitecture)"

Head "Driver install policy (Display class)"
$blocked = @()
if (Test-Path $DenyKey) {
    $item = Get-Item $DenyKey
    foreach ($n in $item.GetValueNames()) {
        $v = [string]$item.GetValue($n)
        if ($v.Trim().TrimStart('{').TrimEnd('}').ToLower() -eq $DisplayClassGuid.Trim('{','}').ToLower()) {
            $blocked += "$n = $v"
        } elseif ($v) {
            Line "INFO" "another blocked class: $n = $v"
        }
    }
}
if ($blocked.Count -gt 0) {
    Line "FAIL" "POLICY BLOCKING DISPLAY DRIVERS (most common cause of the failure):"
    $blocked | ForEach-Object { "         $_" }
    "         -> HKLM\$($DenyKey -replace '^HKLM:\\','')"
} else {
    Line "OK"   "no policy blocking the Display class"
}

Head "OVRService"
$svcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\OVRService"
if (Test-Path $svcKey) {
    $image = (Get-ItemProperty $svcKey).ImagePath
    $exe = $image -replace '^"([^"]+)".*$', '$1'
    if (Test-Path $exe) {
        Line "OK" "OVRService -> $exe"
    } else {
        Line "FAIL" "OVRService points to a missing file: $exe"
    }
    $st = (Get-Service OVRService).Status
    Line "INFO" "service state: $st"
} else {
    Line "OK" "OVRService not registered (clean environment)"
}

Head "Install registry"
$base = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus").Base
if ($base) {
    if (Test-Path $base) {
        Line "OK" "Base = $base"
        $ver = Get-Content (Join-Path $base "Support\oculus-client\version")
        if ($ver) { Line "INFO" "client: $ver" }
    } else {
        Line "FAIL" "Base points to a missing folder: $base"
    }
} else {
    Line "INFO" "Base key does not exist"
}

Head "Leftover install folders"
$candidates = @(
    (Join-Path $env:USERPROFILE "Downloads\Oculus"),
    (Join-Path $env:USERPROFILE "Desktop\Oculus"),
    "$env:ProgramFiles\Oculus",
    "${env:ProgramFiles(x86)}\Oculus",
    "$env:ProgramFiles\Meta Horizon",
    (Join-Path $env:TEMP "Oculus")
)
$found = $false
foreach ($c in $candidates) {
    if (-not $c) { continue }
    if (-not (Test-Path $c)) { continue }
    if ($base -and $c.TrimEnd('\') -eq $base.TrimEnd('\')) { continue }

    # only report if it really looks like an install (avoids cache/temp noise)
    $markers = @(
        "Support\oculus-runtime\OVRServer_x64.exe",
        "Oculus\Support\oculus-runtime\OVRServer_x64.exe",
        "Oculus\OculusSetup.exe",
        "Setup.exe",
        "OculusSetup.exe",
        "Manifests"
    )
    $isInstall = $false
    foreach ($m in $markers) { if (Test-Path (Join-Path $c $m)) { $isInstall = $true; break } }

    if ($isInstall) {
        Line "WARN" "leftover install folder: $c"
        $found = $true
    }
}
if (-not $found) { Line "OK" "no known leftover folder" }

Head "OVRServer crash dumps"
$dumpDir = Join-Path $env:LOCALAPPDATA "Oculus\OVRServerBreakpad"
if (Test-Path $dumpDir) {
    $dumps = Get-ChildItem $dumpDir -Filter *.dmp
    $mb = [math]::Round(($dumps | Measure-Object Length -Sum).Sum / 1MB, 1)
    if ($dumps.Count -gt 500) {
        Line "WARN" "$($dumps.Count) dumps ($mb MB) - may keep the client stuck on the splash screen"
    } else {
        Line "OK"   "$($dumps.Count) dumps ($mb MB)"
    }
} else {
    Line "OK" "no dump folder"
}

Head "Meta Virtual Monitor device"
$dev = Get-PnpDevice -Class Display | Where-Object { $_.FriendlyName -match 'Meta' }
if ($dev) {
    foreach ($d in $dev) { Line "INFO" "$($d.FriendlyName) - $($d.Status) ($($d.InstanceId))" }
} else {
    Line "INFO" "not installed (normal before installing Link)"
}

Head "Disk and reboot"
$sys = Get-PSDrive C
Line "INFO" ("C: {0} GB free" -f [math]::Round($sys.Free / 1GB, 1))
$pending = Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
if ($pending) { Line "WARN" "pending reboot" } else { Line "OK" "no pending reboot" }

""
"End of diagnosis. Nothing was changed."
