<#
    diagnose.ps1 - diagnostico SOMENTE LEITURA do Meta Horizon Link no Windows.

    Nao altera nada. Serve para colar o resultado em uma issue mas tambem
    funciona sozinho, caso a pessoa prefira nao executar o .exe.

    Uso:
        powershell -ExecutionPolicy Bypass -File diagnose.ps1
        powershell -ExecutionPolicy Bypass -File diagnose.ps1 | Out-File report.txt
#>

$ErrorActionPreference = "SilentlyContinue"
$DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}"
$DenyKey = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses"

function Line($status, $text) { "{0} {1}" -f $status.PadRight(6), $text }
function Head($text) { ""; "== $text" }

"Meta Horizon Link Doctor - diagnostico (somente leitura)"
"Data: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
"Computador: $env:COMPUTERNAME   Usuario: $env:USERNAME"
""

Head "Sistema"
$os = Get-CimInstance Win32_OperatingSystem
Line "INFO" "$($os.Caption) build $($os.BuildNumber)"
Line "INFO" "Arquitetura: $($os.OSArchitecture)"

Head "Politica de instalacao de drivers (classe Display)"
$blocked = @()
if (Test-Path $DenyKey) {
    $item = Get-Item $DenyKey
    foreach ($n in $item.GetValueNames()) {
        $v = [string]$item.GetValue($n)
        if ($v.Trim().TrimStart('{').TrimEnd('}').ToLower() -eq $DisplayClassGuid.Trim('{','}').ToLower()) {
            $blocked += "$n = $v"
        } elseif ($v) {
            Line "INFO" "outra classe bloqueada: $n = $v"
        }
    }
}
if ($blocked.Count -gt 0) {
    Line "ERRO" "POLITICA BLOQUEANDO DRIVERS DE VIDEO (causa mais comum da falha):"
    $blocked | ForEach-Object { "         $_" }
    "         -> HKLM\$($DenyKey -replace '^HKLM:\\','')"
} else {
    Line "OK"   "nenhuma politica bloqueando a classe Display"
}

Head "Servico OVRService"
$svcKey = "HKLM:\SYSTEM\CurrentControlSet\Services\OVRService"
if (Test-Path $svcKey) {
    $image = (Get-ItemProperty $svcKey).ImagePath
    $exe = $image -replace '^"([^"]+)".*$', '$1'
    if (Test-Path $exe) {
        Line "OK" "OVRService -> $exe"
    } else {
        Line "ERRO" "OVRService aponta para arquivo inexistente: $exe"
    }
    $st = (Get-Service OVRService).Status
    Line "INFO" "estado do servico: $st"
} else {
    Line "OK" "OVRService nao registrado (ambiente limpo)"
}

Head "Registro de instalacao"
$base = (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus").Base
if ($base) {
    if (Test-Path $base) {
        Line "OK" "Base = $base"
        $ver = Get-Content (Join-Path $base "Support\oculus-client\version")
        if ($ver) { Line "INFO" "cliente: $ver" }
    } else {
        Line "ERRO" "Base aponta para pasta inexistente: $base"
    }
} else {
    Line "INFO" "chave Base nao existe"
}

Head "Pastas de instalacao antigas"
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

    # so reporta se realmente parecer uma instalacao (evita cache/temp)
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
        Line "AVIS" "pasta de instalacao antiga: $c"
        $found = $true
    }
}
if (-not $found) { Line "OK" "nenhuma pasta antiga conhecida" }

Head "Crash dumps do OVRServer"
$dumpDir = Join-Path $env:LOCALAPPDATA "Oculus\OVRServerBreakpad"
if (Test-Path $dumpDir) {
    $dumps = Get-ChildItem $dumpDir -Filter *.dmp
    $mb = [math]::Round(($dumps | Measure-Object Length -Sum).Sum / 1MB, 1)
    if ($dumps.Count -gt 500) {
        Line "AVIS" "$($dumps.Count) dumps ($mb MB) - pode travar o cliente no splash"
    } else {
        Line "OK"   "$($dumps.Count) dumps ($mb MB)"
    }
} else {
    Line "OK" "sem pasta de dumps"
}

Head "Dispositivo Meta Virtual Monitor"
$dev = Get-PnpDevice -Class Display | Where-Object { $_.FriendlyName -match 'Meta' }
if ($dev) {
    foreach ($d in $dev) { Line "INFO" "$($d.FriendlyName) - $($d.Status) ($($d.InstanceId))" }
} else {
    Line "INFO" "nao instalado (normal antes de instalar o Link)"
}

Head "Disco e reinicializacao"
$sys = Get-PSDrive C
Line "INFO" ("C: {0} GB livres" -f [math]::Round($sys.Free / 1GB, 1))
$pending = Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
if ($pending) { Line "AVIS" "reinicializacao pendente" } else { Line "OK" "nenhuma reinicializacao pendente" }

""
"Fim do diagnostico. Nada foi alterado."
