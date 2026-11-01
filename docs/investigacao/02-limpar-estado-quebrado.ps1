$ErrorActionPreference = 'Continue'
$out = 'C:\Users\Admin\Documents\Proojeto VR Dead Space\fix-meta-horizon\3-cleanup-out.txt'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = New-Object System.Collections.Generic.List[string]
function L($m) { $log.Add($m) }

L "=== 1. Parando servico/processos Oculus ==="
Stop-Service -Name OVRService -Force -ErrorAction SilentlyContinue
Get-Process OVRServiceLauncher, OVRRedir, OVRServer, OVRServer_x64, OculusClient -ErrorAction SilentlyContinue | ForEach-Object {
    L "kill: $($_.Name) ($($_.Id))"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

L "=== 2. Removendo registro do servico OVRService ==="
$r = sc.exe delete OVRService 2>&1
L ($r | Out-String)
L "service exists after: $((Get-Service OVRService -ErrorAction SilentlyContinue) -ne $null)"

L "=== 3. Renomeando pastas antigas (nada e apagado) ==="
$targets = @(
    'C:\Users\Admin\Downloads\Oculus',
    'C:\Program Files\Oculus'
)
foreach ($t in $targets) {
    if (Test-Path $t) {
        $dst = "$t.old-$stamp"
        try {
            Rename-Item -LiteralPath $t -NewName (Split-Path $dst -Leaf) -ErrorAction Stop
            L "OK   $t  ->  $dst"
        } catch {
            L "FAIL $t : $($_.Exception.Message)"
        }
    } else { L "SKIP (nao existe) $t" }
}

L "=== 4. Cache vazio do instalador ==="
if (Test-Path 'C:\OculusSetup-DownloadCache') {
    $n = (Get-ChildItem 'C:\OculusSetup-DownloadCache' -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object).Count
    if ($n -eq 0) { Remove-Item 'C:\OculusSetup-DownloadCache' -Recurse -Force -ErrorAction SilentlyContinue; L "removido cache vazio" }
    else { L "cache tem $n itens, mantido" }
} else { L "sem cache" }

L "=== 5. Estado final ==="
L ("Service OVRService: " + [string]((Get-Service OVRService -ErrorAction SilentlyContinue).Status))
foreach ($t in $targets) { L "$t existe? $(Test-Path $t)" }
L ("DenyDeviceClasses: " + ((reg query 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses' 2>&1) -join ' '))
L "DONE"
$log | Set-Content -Path $out -Encoding UTF8
