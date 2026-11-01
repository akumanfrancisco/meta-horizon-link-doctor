# Changelog

Todas as mudanças importantes deste projeto são documentadas aqui.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [1.0.0] - 2026-11-01

### Adicionado
- Primeira versão do **Meta Horizon Link Doctor**.
- 12 verificações locais (SO, permissões, política de classe de driver, serviço
  `OVRService`, registro de instalação, pastas antigas, crash dumps, reboot,
  espaço em disco, dispositivo PnP, versão instalada e veredito final).
- Correção da política `DenyDeviceClasses` da classe Display com backup `.reg`
  automático em `%ProgramData%\MetaHorizonLinkDoctor`.
- Limpeza de `OVRService` órfão/inválido.
- Renomeação (sem apagar) de pastas de instalação antigas.
- Limpeza de crash dumps acumulados do `OVRServerBreakpad`.
- Modo CLI `--scan`, `--fix`, `--out` com códigos de saída para suporte.
- Relatório copiável para issues.
- Suporte a português e inglês (detectado pela cultura do Windows;
  `MHDOCTOR_LANG=en` força inglês).

### Documentação
- `docs/CAUSA-RAIZ.md` com a investigação completa e as evidências de log.
- Scripts originais usados na investigação em `docs/investigacao/`.
- `scripts/diagnose.ps1` para diagnóstico somente-leitura.
