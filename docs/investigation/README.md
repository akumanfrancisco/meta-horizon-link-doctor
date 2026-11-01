# Original investigation scripts

These are the scripts used **before** the app existed, during the diagnosis on a
real machine that could not install Meta Horizon Link.

They are kept here as evidence and as a reference for anyone who wants to
understand (or reproduce) each step. They are **not** the recommended tool for end
users: use `MetaHorizonLinkDoctor.exe`, which does the same things with a UI,
backups and a report.

| File | What it does |
|---|---|
| `01-remove-policy.ps1` | Removes the `mutenx` value from `DenyDeviceClasses` (the root cause) |
| `02-clean-broken-install.ps1` | Stops/removes the orphan `OVRService` and renames old folders to `*.old-<date>` |
| `03-clean-crash-dumps.ps1` | Stops the service, deletes the accumulated `.dmp` files and starts the service again |

All of them require PowerShell **as Administrator**.

## Reproducing the diagnosis

1. Confirm the policy blocking the Display class:

```powershell
reg query "HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses"
```

Output that indicates the problem:

```
mutenx    REG_SZ    {4d36e968-e325-11ce-bfc1-08002be10318}
```

2. Look for confirmation in the Windows log (the Meta installer fails right after
   installing the drivers):

```powershell
Select-String -Path C:\Windows\INF\setupapi.dev*.log -Pattern 'MetaVirtualScreenDriver|0xE0000248' |
    Select-Object -Last 20
```

3. Look in the installer log:

```powershell
Select-String -Path "$env:LOCALAPPDATA\Oculus\OculusSetup.log" -Pattern 'InstallDriversStep|oculus-driver.exe exited'
```

## Warning

The registry value can have **any name** (`mutenx`, `1`, `2`, ...). What matters is
the data: the GUID `{4d36e968-e325-11ce-bfc1-08002be10318}`.
The app handles that correctly; the scripts above are specific to the original
case.
