# Changelog

All notable changes to this project are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-11-01

### Added
- First release of **Meta Horizon Link Doctor**.
- 12 local checks (OS, permissions, driver-class policy, `OVRService`, install
  registry, leftover folders, crash dumps, reboot, disk space, PnP device,
  installed version and the final verdict).
- Fix for the `DenyDeviceClasses` Display-class policy, with an automatic `.reg`
  backup in `%ProgramData%\MetaHorizonLinkDoctor`.
- Cleanup of orphan/invalid `OVRService` registrations.
- Renaming (not deleting) of leftover install folders.
- Cleanup of accumulated `OVRServerBreakpad` crash dumps.
- CLI modes `--scan`, `--fix`, `--out` with exit codes for support.
- Copyable report for issues.
- Portuguese and English UI (auto-detected from the Windows language;
  `MHDOCTOR_LANG` overrides it).

### Documentation
- `docs/ROOT-CAUSE.md` (EN) and `docs/CAUSA-RAIZ.md` (PT-BR) with the full
  investigation and log evidence.
- Original investigation scripts in `docs/investigation/`.
- `scripts/diagnose.ps1` for a read-only diagnosis.
