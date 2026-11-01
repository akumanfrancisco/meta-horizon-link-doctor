# Causa raiz: por que o Meta Horizon Link não instala no Windows 11

Este documento registra a investigação que originou o **Meta Horizon Link Doctor**.
Todas as conclusões abaixo foram observadas em uma máquina real com o problema,
cruzando três fontes: o binário do instalador, os logs do próprio instalador e o
log de instalação de dispositivos do Windows.

## Resumo

| | |
|---|---|
| **Sintoma** | O instalador baixa tudo, chega ao fim e desfaz a instalação, sem mensagem útil |
| **Ponto de falha** | `Dawn.Setup.InstallDriversStep` → `oculus-driver.exe` retorna `-1` |
| **Causa** | Uma política do Windows proíbe instalar drivers da classe **Display** |
| **Erro do Windows** | `0xE0000248` (`SPAPI_E_INSTALL_DEVICE_BLOCKED`) |
| **Correção** | Remover o valor da política e reinstalar |

## 1. O instalador

`Setup.exe` (Meta Horizon Link) é um bootstrapper "Dawn" (framework interno
`Daybreak`), versão `1.115.0.0`, 32 bits. As strings do binário mostram o fluxo:

```
Dawn.Preflight.*            verificações iniciais
Dawn.Setup.Downloader       baixa o "gestalt" e os pacotes
Dawn.Setup.InstallDriversStep   <- onde tudo quebrava
Dawn.Setup.InstallRuntimeStep
Dawn.Setup.InstallChunkedPackagesStep
```

Ele baixa os pacotes do CDN (`securecdn.oculus.com`), instala os drivers com
`Support\oculus-drivers\oculus-driver.exe` e só então instala o runtime e o
cliente. **Se um driver falhar, todo o restante é revertido.**

## 2. Log do instalador

`%LOCALAPPDATA%\Oculus\OculusSetup.log`:

```
[Error] Process C:\Program Files\Meta Horizon\Support\oculus-drivers\oculus-driver.exe exited with code -1 (failure).
[Debug] Install 'Dawn.Setup.InstallDriversStep' failed.
[Debug] Uninstall 'Dawn.Setup.InstallDriversStep' failed.
[Debug] Uninstall 'Dawn.Setup.InstallShortcutsStep' succeeded.
... (rollback de todos os passos)
```

Ou seja: o instalador **não** falhou no download, na rede ou no espaço em disco.
Falhou ao instalar um driver.

## 3. Log do instalador de drivers

`%LOCALAPPDATA%\Oculus\OculusDriverInstaller_<data>.log`:

```
FAILURE! Failed to install driver: Meta Virtual Screen Driver
Sub Install Step: devcon: Meta Virtual Screen Driver
StdErr: devcon.exe failed.
Error Code: 2
Install Step: InstallDriver
Caller: DevCon
```

Todos os outros drivers passaram (`Monitor Driver`, `DK2Sensor`, `Tracker`,
`Audio`, `Rift S`, `Reality Labs XRSP`, `VR Air Bridge`, `Oculus Virtual Audio`).
Apenas o **Meta Virtual Screen Driver** falhou.

## 4. Log do Windows (a prova definitiva)

`C:\Windows\INF\setupapi.dev.log`:

```
dvi:      Searching for hardware ID(s): root\metavirtualscreendriver
dvi:      Class GUID of device changed to: {4d36e968-e325-11ce-bfc1-08002be10318}
ump:      {Plug and Play Service: Device Install for ROOT\DISPLAY\0002}
pol:           {Device installation policy check [ROOT\DISPLAY\0002]}
pol:                The device is explicitly restricted by the following policy settings:
pol:                [-] Restricted setup class of selected driver: {4d36e968-e325-11ce-bfc1-08002be10318}
pol:           {Device installation policy check [ROOT\DISPLAY\0002] exit(0xe0000248)}
dvi:           Installation of device is blocked by policy. Error = 0xE0000248
ndv:      Failed to install device instance 'ROOT\DISPLAY\0002'. Error = 0xe0000248
```

- `{4d36e968-e325-11ce-bfc1-08002be10318}` é o GUID da classe **Display adapters**
  ("Adaptadores de vídeo").
- `0xE0000248` = `SPAPI_E_INSTALL_DEVICE_BLOCKED`.

O "Meta Virtual Screen Driver" cria o dispositivo **Meta Virtual Monitor**
(`ROOT\DISPLAY\0002`), que é da classe Display. Com a classe bloqueada, o Windows
recusa a instalação do dispositivo.

## 5. A política culpada

```
HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses
    mutenx = {4d36e968-e325-11ce-bfc1-08002be10318}
```

Essa chave implementa a política *"Prevent installation of devices in these device
setup classes"*. O nome do valor **não importa** (o Windows enumera os valores);
o que importa é o GUID no dado.

### De onde ela veio?

Não é possível afirmar com certeza. O que se observou:

- não estava no `registry.pol` da Diretiva de Grupo local, então não veio de GPO;
- o nome `mutenx` não segue o padrão numérico (`1`, `2`, ...) usado pelo Editor de
  Diretiva de Grupo, o que indica gravação direta no registro por alguma
  ferramenta.

Candidatos típicos: "tweaks" para impedir o Windows Update de instalar driver de
vídeo, otimizadores de sistema, ou ferramentas de bloqueio de driver.

### Prova de que era ela

Depois de remover **somente esse valor** e rodar o instalador novamente:

```
[Debug] [01/11/2026 14:32:50] Install 'Dawn.Setup.InstallDriversStep' succeeded.
[Debug] [01/11/2026 14:33:22] Installation succeeded.
```

E o dispositivo passou a existir:

```
ROOT\DISPLAY\0002   Meta Virtual Monitor   Status: OK
```

## 6. O mesmo bloqueio quebra outros drivers virtuais

No mesmo log, o driver do **Virtual Desktop** (`Root\vdvmd`,
`ROOT\DISPLAY\0001`) falhava repetidamente com o erro idêntico:

```
!!! pol: [-] Restricted setup class of selected driver: {4d36e968-...}
!!! dvi: Installation of device is blocked by policy. Error = 0xE0000248
!!! ndv: Failed to install device instance 'ROOT\DISPLAY\0001'. Error = 0xe0000248
```

Isso é útil para diagnosticar: se o usuário também tem problemas com Virtual
Desktop, Parsec ou similares, a chance de ser a mesma política é alta.

## 7. Problemas secundários encontrados no caminho

Esses não impedem a instalação, mas quebram o app depois de instalado:

1. **`OVRService` apontando para uma cópia movida** (ex.: `C:\Users\<user>\Downloads\Oculus\...`).
   O runtime loga `Failed to open Oculus root registry key, install potentially corrupt`
   e `Unable to obtain root installation path`, e o cliente não abre.

2. **Acúmulo de crash dumps.** A máquina tinha **33.304 arquivos `.dmp` (10,2 GB)**
   em `%LOCALAPPDATA%\Oculus\OVRServerBreakpad`. O serviço tentava enviá-los na
   inicialização e o cliente ficava preso na tela de carregamento (splash).
   Após limpar, o app abriu normalmente.

3. **Escolha de disco.** O instalador recusa discos sem espaço/NTFS:
   `Unable to find an install location with enough free space`.

## 8. Como o Doctor cobre isso

| Achado | Verificação no app | Correção automática |
|---|---|---|
| Política de classe Display | `policy.display` | remove o valor + backup `.reg` |
| `OVRService` inválido/movido | `service.ovr` | `sc delete OVRService` |
| Pastas antigas em Downloads/Desktop/outros discos | `folders` | renomeia para `*.old-<data>` |
| Crash dumps acumulados | `dumps` | apaga só os `.dmp` |
| Registro `Base` inconsistente | `base` | (informativo; resolvido ao remover o serviço) |

As demais verificações (SO, admin, reboot, disco, dispositivo PnP, versão
instalada) existem para dar contexto no relatório de suporte.
