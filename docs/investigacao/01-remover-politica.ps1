$ErrorActionPreference = 'Continue'
$out = 'C:\Users\Admin\Documents\Proojeto VR Dead Space\fix-meta-horizon\1-out.txt'
$key = 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("BEFORE:")
$lines.Add((reg query $key 2>&1 | Out-String))
$lines.Add((reg delete $key /v mutenx /f 2>&1 | Out-String))
$lines.Add("AFTER:")
$lines.Add((reg query $key 2>&1 | Out-String))
$lines.Add("DONE")
$lines | Set-Content -Path $out -Encoding UTF8
