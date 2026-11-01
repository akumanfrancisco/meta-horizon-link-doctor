# Scripts da investigação original

Estes são os scripts usados **antes** de o app existir, durante o diagnóstico em
uma máquina real que não conseguia instalar o Meta Horizon Link.

Eles ficam aqui como evidência e como referência para quem quiser entender (ou
reproduzir) cada passo. **Não** são a ferramenta recomendada para o público: use
`MetaHorizonLinkDoctor.exe`, que faz as mesmas coisas com interface, backup e
relatório.

| Arquivo | O que faz |
|---|---|
| `01-remover-politica.ps1` | Remove o valor `mutenx` de `DenyDeviceClasses` (a causa raiz) |
| `02-limpar-estado-quebrado.ps1` | Para/remove o `OVRService` órfão e renomeia pastas antigas para `*.old-<data>` |
| `03-limpar-crash-dumps.ps1` | Para o serviço, apaga os `.dmp` acumulados e sobe o serviço de novo |

Todos exigem PowerShell como **Administrador**.

## Como reproduzir o diagnóstico

1. Confirme a política bloqueando a classe Display:

```powershell
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses"
```

Saída que indica o problema:

```
mutenx    REG_SZ    {4d36e968-e325-11ce-bfc1-08002be10318}
```

2. Procure a confirmação no log do Windows (o instalador da Meta falha depois
   de instalar os drivers):

```powershell
Select-String -Path C:\Windows\INF\setupapi.dev*.log -Pattern 'MetaVirtualScreenDriver|0xE0000248' |
    Select-Object -Last 20
```

3. Procure no log do instalador:

```powershell
Select-String -Path "$env:LOCALAPPDATA\Oculus\OculusSetup.log" -Pattern 'InstallDriversStep|oculus-driver.exe exited'
```

## Aviso

O valor do registro pode ter **qualquer nome** (`mutenx`, `1`, `2`, ...). O que
importa é o dado: o GUID `{4d36e968-e325-11ce-bfc1-08002be10318}`.
O app trata isso corretamente; os scripts acima são específicos do caso original.
