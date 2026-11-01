# Meta Horizon Link Doctor

[![build](https://github.com/akumanfrancisco/meta-horizon-link-doctor/actions/workflows/build.yml/badge.svg)](https://github.com/akumanfrancisco/meta-horizon-link-doctor/actions)

Utilitário para Windows 10/11 que **descobre e corrige a causa real** da falha de
instalação do **Meta Horizon Link** (antigo Oculus Link) — aquela em que o
instalador baixa tudo, chega ao fim e desfaz tudo sem explicar o motivo.

<p align="center">
  <img src="assets/screenshot.png" alt="Meta Horizon Link Doctor" width="760">
</p>

---

## O problema

O instalador da Meta (`Setup.exe` / "Dawn") baixa os pacotes, instala os drivers e
só então o runtime. Se **qualquer** driver falhar, ele **reverte a instalação
inteira** e mostra apenas *"ocorreu um erro… reinicie e tente de novo"*.

Na prática, a falha mais comum é esta, registrada no `setupapi.dev.log` do Windows:

```
dvi:  Searching for hardware ID(s): root\metavirtualscreendriver
dvi:  Class GUID of device changed to: {4d36e968-e325-11ce-bfc1-08002be10318}
pol:  The device is explicitly restricted by the following policy settings:
pol:  [-] Restricted setup class of selected driver: {4d36e968-...}
pol:  {Device installation policy check [ROOT\DISPLAY\0002] exit(0xe0000248)}
dvi:  Installation of device is blocked by policy. Error = 0xE0000248
```

O instalador precisa criar o dispositivo **"Meta Virtual Monitor"** (`ROOT\DISPLAY\...`),
que pertence à classe **"Adaptadores de vídeo"**. Se existir uma política do Windows
bloqueando essa classe de driver, o Windows recusa com `0xE0000248` e o instalador
aborta.

A política fica em:

```
HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses
```

Ela costuma ser criada por "tweaks" de otimização de driver, ferramentas de
bloqueio de driver de vídeo ou configurações de terceiros. Ela **também quebra o
Virtual Desktop** e outros drivers virtuais de tela.

> Investigação completa com todas as evidências: **[docs/CAUSA-RAIZ.md](docs/CAUSA-RAIZ.md)**

## O que o aplicativo faz

Ele roda localmente, sem enviar nada para lugar nenhum, e verifica 12 pontos:

| Verificação | Corrige? |
|---|---|
| Windows 10/11 e build mínimo | — |
| Permissão de administrador | — |
| **Política bloqueando drivers de vídeo (`DenyDeviceClasses`)** | ✅ remove só o valor culpado, com backup `.reg` |
| **`OVRService` apontando para arquivo inexistente / pasta de usuário** | ✅ remove só o registro do serviço |
| Registro de instalação (`HKLM\...\Oculus\Base`) inconsistente | — |
| **Pastas de instalação antigas** (Downloads, Desktop, outros discos) | ✅ renomeia para `*.old-<data>` (não apaga) |
| **Acúmulo de crash dumps** do `OVRServerBreakpad` | ✅ apaga apenas os `.dmp` |
| Reinicialização pendente | — |
| Espaço livre / NTFS no disco do sistema | — |
| Estado do dispositivo "Meta Virtual Monitor" (PnP) | — |
| Versão do Meta Horizon Link já instalada | — |
| Veredito final: pode instalar ou não | — |

Tudo o que é alterado é **reversível** e aparece no relatório.

## Como usar

1. Baixe `MetaHorizonLinkDoctor.exe` na página de **[Releases](../../releases)**.
2. Dê duplo clique e aceite o UAC (o app precisa de administrador para ler/escrever
   em `HKLM` e nos serviços).
3. Clique em **Corrigir problemas** (se houver algo em vermelho/laranja).
4. Clique em **Baixar instalador** e rode o instalador oficial da Meta.

### "O Windows protegeu o seu PC"

O `.exe` não tem assinatura digital (certificado de code signing custa dinheiro).
O Windows mostra esse aviso para qualquer executável novo baixado da internet.
O código-fonte é público e você pode compilar você mesmo com `build.ps1`.

Se quiser conferir a integridade, cada Release publica um `SHA256SUMS`:

```powershell
Get-FileHash .\MetaHorizonLinkDoctor.exe -Algorithm SHA256
```

### Modo linha de comando (para dar suporte)

```text
MetaHorizonLinkDoctor.exe --scan                 # só diagnostica, grava mh-doctor-report.txt
MetaHorizonLinkDoctor.exe --fix                  # corrige tudo e grava o relatório
MetaHorizonLinkDoctor.exe --scan --out C:\temp\r.txt
```

Código de saída: `0` = nenhum bloqueio, `2` = bloqueios encontrados.
No modo `--scan` **nada é alterado**.

Também existe um script somente-leitura para colar o resultado numa issue:
[`scripts/diagnose.ps1`](scripts/diagnose.ps1).

## O que NÃO é feito

- Não baixa nem instala o Meta Horizon Link por você (isso é feito pelo instalador oficial).
- Não mexe em drivers de vídeo já instalados, nem desinstala nada.
- Não envia telemetria, não acessa a internet (exceto o botão "Baixar instalador",
  que abre o site da Meta no seu navegador).

## Compilar

Requisitos: Windows + Visual Studio 2022 (ou Build Tools) com o workload
*.NET desktop development* — **não precisa de .NET SDK**.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
# opcional: -Version 1.2.3 -OutputDir .\dist
```

Saída: `dist\MetaHorizonLinkDoctor.exe` (~80 KB, roda em qualquer Windows 10/11
com o .NET Framework 4.8 que já vem no sistema).

O workflow em [`.github/workflows/build.yml`](.github/workflows/build.yml) compila
e publica automaticamente em cada tag `v*`.

## Estrutura

```
src/            código C# (WinForms, .NET Framework 4.8)
build.ps1       compilação via csc.exe do Visual Studio
scripts/        diagnose.ps1 (diagnóstico somente-leitura)
docs/           causa raiz, evidências e scripts originais da investigação
assets/         ícone e screenshot
```

## Contribuindo

Abra uma issue usando o template e **cole o relatório gerado pelo app**
(botão *Copiar relatório*). Ele contém exatamente o que é necessário para
diagnosticar, sem dados pessoais.

## Licença

MIT — veja [LICENSE](LICENSE).

---

## English

**Meta Horizon Link Doctor** finds and fixes the real cause of the Meta Horizon
Link install failure on Windows 10/11.

The Meta installer downloads everything, installs the drivers, and if **any**
driver fails it rolls back the whole installation with a generic error. The most
common blocker is a Windows policy that forbids installing **Display class**
drivers (`{4d36e968-e325-11ce-bfc1-08002be10318}`), which makes the
"Meta Virtual Monitor" (`ROOT\DISPLAY\...`) fail with `0xE0000248`.

The app checks 12 items locally (no telemetry), offers reversible one-click fixes
(registry value removal with `.reg` backup, stale `OVRService` cleanup, leftover
install folders renamed, crash dump cleanup) and produces a report you can paste
into an issue.

Run `MetaHorizonLinkDoctor.exe`, click **Fix problems**, then run the official
Meta installer. CLI: `--scan` / `--fix` / `--out <file>`.
