# Root cause: why Meta Horizon Link does not install on Windows 11

**🇺🇸 English** · **🇧🇷 [Português](CAUSA-RAIZ.md)**

This document records the investigation that led to **Meta Horizon Link Doctor**.
Every conclusion below was observed on a real machine with the problem, by
cross-checking three sources: the installer binary, the installer's own logs, and
the Windows device-installation log.

## Summary

| | |
|---|---|
| **Symptom** | The installer downloads everything, reaches the end, rolls back, and shows no useful message |
| **Failure point** | `Dawn.Setup.InstallDriversStep` → `oculus-driver.exe` returns `-1` |
| **Cause** | A Windows policy forbids installing **Display class** drivers |
| **Windows error** | `0xE0000248` (`SPAPI_E_INSTALL_DEVICE_BLOCKED`) |
| **Fix** | Remove the policy value and run the installer again |

## 1. The installer

`Setup.exe` (Meta Horizon Link) is a "Dawn" bootstrapper (internal framework
`Daybreak`), version `1.115.0.0`, 32-bit. Strings in the binary reveal the flow:

```
Dawn.Preflight.*                initial checks
Dawn.Setup.Downloader           downloads the "gestalt" and the packages
Dawn.Setup.InstallDriversStep   <- where everything broke
Dawn.Setup.InstallRuntimeStep
Dawn.Setup.InstallChunkedPackagesStep
```

It downloads packages from the CDN (`securecdn.oculus.com`), installs drivers with
`Support\oculus-drivers\oculus-driver.exe`, and only then installs the runtime and
the client. **If one driver fails, everything else is rolled back.**

## 2. Installer log

`%LOCALAPPDATA%\Oculus\OculusSetup.log`:

```
[Error] Process C:\Program Files\Meta Horizon\Support\oculus-drivers\oculus-driver.exe exited with code -1 (failure).
[Debug] Install 'Dawn.Setup.InstallDriversStep' failed.
[Debug] Uninstall 'Dawn.Setup.InstallDriversStep' failed.
[Debug] Uninstall 'Dawn.Setup.InstallShortcutsStep' succeeded.
... (rollback of every step)
```

So the installer did **not** fail on the download, on the network, or on disk
space. It failed while installing a driver.

## 3. Driver installer log

`%LOCALAPPDATA%\Oculus\OculusDriverInstaller_<date>.log`:

```
FAILURE! Failed to install driver: Meta Virtual Screen Driver
Sub Install Step: devcon: Meta Virtual Screen Driver
StdErr: devcon.exe failed.
Error Code: 2
Install Step: InstallDriver
Caller: DevCon
```

Every other driver succeeded (`Monitor Driver`, `DK2Sensor`, `Tracker`, `Audio`,
`Rift S`, `Reality Labs XRSP`, `VR Air Bridge`, `Oculus Virtual Audio`). Only the
**Meta Virtual Screen Driver** failed.

## 4. Windows log (the definitive proof)

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

- `{4d36e968-e325-11ce-bfc1-08002be10318}` is the GUID of the **Display adapters**
  setup class.
- `0xE0000248` = `SPAPI_E_INSTALL_DEVICE_BLOCKED`.

The "Meta Virtual Screen Driver" creates the **Meta Virtual Monitor** device
(`ROOT\DISPLAY\0002`), which belongs to the Display class. With that class
blocked, Windows refuses to install the device.

## 5. The offending policy

```
HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses
    mutenx = {4d36e968-e325-11ce-bfc1-08002be10318}
```

That key implements the policy *"Prevent installation of devices in these device
setup classes"*. The **value name does not matter** (Windows enumerates the
values); only the GUID in the data matters.

### Where did it come from?

It cannot be stated with certainty. What was observed:

- it was not present in the local Group Policy `registry.pol`, so it did not come
  from a GPO;
- the name `mutenx` does not follow the numeric pattern (`1`, `2`, ...) used by
  the Group Policy Editor, which suggests it was written directly to the registry
  by some tool.

Typical candidates: "tweaks" that prevent Windows Update from installing video
drivers, system optimizers, or driver-blocking utilities.

### Proof that it was the cause

After removing **only that value** and running the installer again:

```
[Debug] [01/11/2026 14:32:50] Install 'Dawn.Setup.InstallDriversStep' succeeded.
[Debug] [01/11/2026 14:33:22] Installation succeeded.
```

And the device now exists:

```
ROOT\DISPLAY\0002   Meta Virtual Monitor   Status: OK
```

## 6. The same block breaks other virtual drivers

In the same log, the **Virtual Desktop** driver (`Root\vdvmd`,
`ROOT\DISPLAY\0001`) failed repeatedly with the identical error:

```
!!! pol: [-] Restricted setup class of selected driver: {4d36e968-...}
!!! dvi: Installation of device is blocked by policy. Error = 0xE0000248
!!! ndv: Failed to install device instance 'ROOT\DISPLAY\0001'. Error = 0xe0000248
```

This is useful for diagnosis: if the user also has problems with Virtual Desktop,
Parsec or similar, the odds of it being the same policy are high.

## 7. Secondary problems found along the way

These do not block the installation, but break the app after it is installed:

1. **`OVRService` pointing to a moved copy** (e.g. `C:\Users\<user>\Downloads\Oculus\...`).
   The runtime logs `Failed to open Oculus root registry key, install potentially corrupt`
   and `Unable to obtain root installation path`, and the client never opens.

2. **Crash dump pileup.** The machine had **33,304 `.dmp` files (10.2 GB)** in
   `%LOCALAPPDATA%\Oculus\OVRServerBreakpad`. The service tried to upload them at
   startup and the client stayed stuck on the splash screen. After cleaning, the
   app opened normally.

3. **Drive choice.** The installer refuses drives without space/NTFS:
   `Unable to find an install location with enough free space`.

## 8. How the Doctor covers this

| Finding | App check | Automatic fix |
|---|---|---|
| Display class policy | `policy.display` | removes the value + `.reg` backup |
| Invalid/moved `OVRService` | `service.ovr` | `sc delete OVRService` |
| Old folders in Downloads/Desktop/other drives | `folders` | renames to `*.old-<date>` |
| Crash dump pileup | `dumps` | deletes only the `.dmp` files |
| Inconsistent `Base` registry | `base` | (informational; resolved by removing the service) |

The remaining checks (OS, admin, reboot, disk, PnP device, installed version) exist
to give context in support reports.

## 9. Reproducing the diagnosis

The original scripts are in [`docs/investigation/`](investigation/).
