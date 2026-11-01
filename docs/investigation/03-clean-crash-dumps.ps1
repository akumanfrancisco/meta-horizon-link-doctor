$out = 'C:\Users\Admin\Documents\Proojeto VR Dead Space\fix-meta-horizon\12-clean-dumps-out.txt'
$lines = New-Object System.Collections.Generic.List[string]
$dir = "$env:LOCALAPPDATA\Oculus\OVRServerBreakpad"
if (Test-Path $dir) {
    $f = Get-ChildItem $dir -File -ErrorAction SilentlyContinue
    $size = ($f | Measure-Object Length -Sum).Sum
    $lines.Add("dumps antes: $($f.Count) arquivos, $([math]::Round($size/1MB,1)) MB")
} else { $lines.Add("pasta nao existe") }

Stop-Service OVRService -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Get-Process OVRServer_x64,OVRServiceLauncher,OVRRedir -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (Test-Path $dir) {
    Get-ChildItem $dir -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    $f2 = Get-ChildItem $dir -File -ErrorAction SilentlyContinue
    $lines.Add("dumps depois: $($f2.Count) arquivos")
}

# encerra todas as instancias do cliente para subir limpo
Get-Process Client -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Start-Service OVRService -ErrorAction SilentlyContinue
Start-Sleep -Seconds 5
$lines.Add("OVRService: $((Get-Service OVRService -ErrorAction SilentlyContinue).Status)")
$lines | Set-Content $out -Encoding UTF8
