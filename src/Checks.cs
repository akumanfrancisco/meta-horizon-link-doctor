using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace MHDoctor
{
    internal static class Consts
    {
        /// <summary>GUID da classe "Display adapters" do Windows.</summary>
        public const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";

        public const string DenyClassesKey =
            @"SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions\DenyDeviceClasses";

        public const string OculusBaseKey =
            @"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus";

        public const string OvrService = "OVRService";
        public const string DownloadUrl = "https://www.meta.com/quest/setup/";

        // IDs dos checks (usados pelo "Corrigir")
        public const string IdOs = "os";
        public const string IdAdmin = "admin";
        public const string IdPolicy = "policy.display";
        public const string IdService = "service.ovr";
        public const string IdBase = "base";
        public const string IdFolders = "folders";
        public const string IdDumps = "dumps";
        public const string IdReboot = "reboot";
        public const string IdDisk = "disk";
        public const string IdDevice = "device";
        public const string IdInstalled = "installed";
        public const string IdVerdict = "verdict";
    }

    internal static class Reg
    {
        public static RegistryKey Open(string path, bool writable)
        {
            try
            {
                RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey key = baseKey.OpenSubKey(path, writable);
                if (key == null) baseKey.Close();
                return key;
            }
            catch
            {
                return null;
            }
        }

        public static string GetString(string path, string name)
        {
            using (RegistryKey k = Open(path, false))
            {
                if (k == null) return null;
                object v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return v as string;
            }
        }

        public static string GetServiceImagePath(string serviceName)
        {
            return GetString(@"SYSTEM\CurrentControlSet\Services\" + serviceName, "ImagePath");
        }

        public static bool KeyExists(string path)
        {
            using (RegistryKey k = Open(path, false)) return k != null;
        }
    }

    internal class Doctor
    {
        public readonly List<string> StaleFolders = new List<string>();
        public string DumpDirectory;
        public long DumpBytes;
        public int DumpCount;

        // ------------------------------------------------------------------ SCAN

        public List<CheckResult> Scan()
        {
            StaleFolders.Clear();
            DumpDirectory = null;
            DumpBytes = 0;
            DumpCount = 0;

            var list = new List<CheckResult>();
            list.Add(CheckOs());
            list.Add(CheckAdmin());
            list.Add(CheckDisplayPolicy());
            list.Add(CheckOvrService());
            list.Add(CheckBase());
            list.Add(CheckStaleFolders());
            list.Add(CheckDumps());
            list.Add(CheckReboot());
            list.Add(CheckDisk());
            list.Add(CheckDevice());
            list.Add(CheckInstalled());
            list.Add(CheckVerdict(list));
            return list;
        }

        private CheckResult CheckOs()
        {
            Version v = Win32.RealOsVersion();
            var r = new CheckResult { Id = Consts.IdOs };

            if (Win32.IsWindows11(v))
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Windows 11 (build " + v.Build + ")",
                              "Windows 11 (build " + v.Build + ")");
                r.Details = L.S("Versao suportada pelo Meta Horizon Link.",
                                "Supported by Meta Horizon Link.");
            }
            else if (v.Major >= 10 && v.Build >= 19041)
            {
                r.Severity = Severity.Warn;
                r.Title = L.S("Windows 10 build " + v.Build, "Windows 10 build " + v.Build);
                r.Details = L.S("Funciona na maioria dos casos, mas o Link e otimizado para Windows 11.",
                                "Usually works, but Link is optimised for Windows 11.");
            }
            else
            {
                r.Severity = Severity.Blocker;
                r.Title = L.S("Windows muito antigo (" + v + ")", "Windows too old (" + v + ")");
                r.Details = L.S("O Meta Horizon Link exige Windows 10 2004 ou superior.",
                                "Meta Horizon Link requires Windows 10 2004 or newer.");
            }
            return r;
        }

        private CheckResult CheckAdmin()
        {
            bool admin = Win32.IsAdmin();
            return new CheckResult
            {
                Id = Consts.IdAdmin,
                Severity = admin ? Severity.Ok : Severity.Blocker,
                Title = L.S("Permissao de administrador", "Administrator rights"),
                Details = admin
                    ? L.S("Executando elevado.", "Running elevated.")
                    : L.S("Feche e execute o programa como Administrador.",
                          "Close it and run the program as Administrator.")
            };
        }

        private CheckResult CheckDisplayPolicy()
        {
            var r = new CheckResult { Id = Consts.IdPolicy };
            var blocked = new List<string>();
            var others = new List<string>();

            using (RegistryKey k = Reg.Open(Consts.DenyClassesKey, false))
            {
                if (k != null)
                {
                    foreach (string name in k.GetValueNames())
                    {
                        string data = k.GetValue(name) as string;
                        if (string.IsNullOrEmpty(data)) continue;

                        if (NormalizeGuid(data) == Consts.DisplayClassGuid)
                            blocked.Add(name + " = " + data);
                        else
                            others.Add(name + " = " + data);
                    }
                }
            }

            if (blocked.Count > 0)
            {
                r.Severity = Severity.Blocker;
                r.Fixable = true;
                r.Title = L.S("Politica bloqueia drivers da classe \"Adaptadores de video\"",
                              "Policy blocks \"Display adapters\" class drivers");
                r.Details =
                    L.S("Uma politica do Windows proibe instalar qualquer driver de video.\n",
                        "A Windows policy forbids installing any display driver.\n") +
                    L.S("O instalador da Meta precisa criar o dispositivo \"Meta Virtual Monitor\"\n",
                        "The Meta installer must create the \"Meta Virtual Monitor\" device\n") +
                    L.S("(ROOT\\DISPLAY\\...) e o Windows recusa com erro 0xE0000248.\n\n",
                        "(ROOT\\DISPLAY\\...) and Windows refuses with error 0xE0000248.\n\n") +
                    Consts.DenyClassesKey + "\n  " + Util.Join(blocked, "\n  ") + "\n\n" +
                    L.S("A correcao remove apenas esse valor e salva um backup .reg.",
                        "The fix removes only this value and saves a .reg backup.");
            }
            else
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Nenhuma politica bloqueando drivers de video",
                              "No policy blocking display drivers");
                r.Details = others.Count > 0
                    ? L.S("Outras classes bloqueadas (nao afetam o Link):\n  ",
                          "Other blocked classes (not related to Link):\n  ") + Util.Join(others, "\n  ")
                    : L.S("Nada encontrado em DenyDeviceClasses.",
                          "Nothing found in DenyDeviceClasses.");
            }
            return r;
        }

        private CheckResult CheckOvrService()
        {
            var r = new CheckResult { Id = Consts.IdService };
            string image = Reg.GetServiceImagePath(Consts.OvrService);

            if (image == null)
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Servico OVRService nao registrado",
                              "OVRService is not registered");
                r.Details = L.S("Normal quando o Link nunca foi instalado.",
                                "Normal when Link has never been installed.");
                return r;
            }

            string exe = Win32.ExtractExePath(image);
            bool exists = exe != null && File.Exists(exe);
            bool userPath = IsSuspiciousPath(exe);

            if (!exists)
            {
                r.Severity = Severity.Blocker;
                r.Fixable = true;
                r.Title = L.S("OVRService aponta para um arquivo inexistente",
                              "OVRService points to a missing file");
                r.Details = L.S("Instalacao quebrada detectada:\n  ", "Broken install detected:\n  ") + exe + "\n\n" +
                            L.S("A correcao remove apenas o registro do servico (o instalador cria de novo).",
                                "The fix removes only the service registration (the installer recreates it).");
            }
            else if (userPath)
            {
                r.Severity = Severity.Blocker;
                r.Fixable = true;
                r.Title = L.S("OVRService rodando de uma pasta de usuario",
                              "OVRService is running from a user folder");
                r.Details = L.S("O runtime esta registrado em:\n  ", "The runtime is registered at:\n  ") + exe + "\n\n" +
                            L.S("Instalacoes movidas/extraidas para Downloads, Desktop ou Temp deixam o\n",
                                "Installs moved/extracted to Downloads, Desktop or Temp leave the\n") +
                            L.S("runtime em estado \"potentially corrupt\" e o cliente nao abre.\n",
                                "runtime in a \"potentially corrupt\" state and the client will not open.\n") +
                            L.S("A correcao remove apenas o registro do servico.",
                                "The fix removes only the service registration.");
            }
            else
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("OVRService OK", "OVRService OK");
                r.Details = exe;
            }
            return r;
        }

        private CheckResult CheckBase()
        {
            var r = new CheckResult { Id = Consts.IdBase };
            string basePath = Reg.GetString(Consts.OculusBaseKey, "Base");
            bool servicePresent = Reg.GetServiceImagePath(Consts.OvrService) != null;

            if (string.IsNullOrEmpty(basePath))
            {
                if (servicePresent)
                {
                    r.Severity = Severity.Warn;
                    r.Title = L.S("Registro de instalacao ausente (estado inconsistente)",
                                  "Install registry is missing (inconsistent state)");
                    r.Details = L.S("O servico existe, mas HKLM\\...\\Oculus\\Base nao.\n",
                                    "The service exists but HKLM\\...\\Oculus\\Base does not.\n") +
                                L.S("Isso gera \"install potentially corrupt\" nos logs. Corrigir o\n",
                                    "This produces \"install potentially corrupt\" in the logs. Fixing the\n") +
                                L.S("servico acima ja resolve este item.",
                                    "service above already resolves this item.");
                }
                else
                {
                    r.Severity = Severity.Ok;
                    r.Title = L.S("Nenhuma instalacao anterior", "No previous installation");
                    r.Details = L.S("Ambiente limpo para instalar.", "Clean environment for installing.");
                }
                return r;
            }

            bool dirOk = Directory.Exists(basePath);
            r.Severity = dirOk ? Severity.Ok : Severity.Blocker;
            r.Title = dirOk
                ? L.S("Instalacao registrada em " + basePath, "Install registered at " + basePath)
                : L.S("Pasta de instalacao registrada nao existe", "Registered install folder is missing");
            r.Details = basePath;

            if (dirOk)
            {
                string versionFile = Path.Combine(basePath, @"Support\oculus-client\version");
                try
                {
                    if (File.Exists(versionFile))
                        r.Details += L.S("\nCliente instalado: ", "\nInstalled client: ") +
                                     File.ReadAllText(versionFile).Trim();
                }
                catch { }
            }
            else
            {
                r.Details += "\n\n" + L.S("Reinstale ou desinstale o Link antes de continuar.",
                                          "Reinstall or uninstall Link before continuing.");
            }
            return r;
        }

        private CheckResult CheckStaleFolders()
        {
            var r = new CheckResult { Id = Consts.IdFolders };
            string basePath = Reg.GetString(Consts.OculusBaseKey, "Base");

            foreach (string baseDir in CandidateDirectories())
            {
                foreach (string dir in SafeSubDirectories(baseDir))
                {
                    try
                    {
                        string name = Path.GetFileName(dir.TrimEnd('\\'));
                        if (!name.Equals("Oculus", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("Meta Horizon", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!LooksLikeInstall(dir)) continue;
                        if (!string.IsNullOrEmpty(basePath) && Util.SamePath(dir, basePath)) continue;
                        if (Util.SamePath(dir, AppDomain.CurrentDomain.BaseDirectory)) continue;

                        if (!StaleFolders.Any(f => Util.SamePath(f, dir)))
                            StaleFolders.Add(dir);
                    }
                    catch { }
                }
            }

            if (StaleFolders.Count == 0)
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Nenhuma pasta de instalacao antiga",
                              "No leftover install folders");
                r.Details = L.S("Nada para limpar.", "Nothing to clean up.");
            }
            else
            {
                r.Severity = Severity.Warn;
                r.Fixable = true;
                r.Title = L.S("Pastas de instalacao antigas encontradas",
                              "Leftover install folders found");
                r.Details = L.S("Pastas que parecem instalacoes antigas do Link:\n  ",
                                "Folders that look like old Link installs:\n  ") +
                            Util.Join(StaleFolders, "\n  ") + "\n\n" +
                            L.S("A correcao renomeia para *.old-<data> (nada e apagado).",
                                "The fix renames them to *.old-<date> (nothing is deleted).");
            }
            return r;
        }

        private CheckResult CheckDumps()
        {
            var r = new CheckResult { Id = Consts.IdDumps };
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Oculus\OVRServerBreakpad");

            if (!Directory.Exists(dir))
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Sem acumulo de crash dumps", "No crash dump pileup");
                r.Details = L.S("Pasta de dumps nao existe.", "Dump folder does not exist.");
                return r;
            }

            long total = 0;
            int count = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.dmp"))
                {
                    count++;
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }

            DumpDirectory = dir;
            DumpBytes = total;
            DumpCount = count;

            if (count == 0)
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Sem acumulo de crash dumps", "No crash dump pileup");
                r.Details = dir;
            }
            else if (count > 500 || total > 512L * 1024 * 1024)
            {
                r.Severity = Severity.Warn;
                r.Fixable = true;
                r.Title = L.S("Acumulo de crash dumps (" + Util.FormatBytes(total) + ")",
                              "Crash dump pileup (" + Util.FormatBytes(total) + ")");
                r.Details = L.S(count + " arquivos em " + dir + "\n",
                                count + " files in " + dir + "\n") +
                            L.S("O OVRService tenta enviar todos eles na inicializacao e o cliente\n",
                                "OVRService tries to upload all of them on startup and the client\n") +
                            L.S("pode ficar preso na tela de carregamento (splash).\n",
                                "can stay stuck on the splash screen.\n") +
                            L.S("A correcao apaga somente os arquivos .dmp.",
                                "The fix deletes only the .dmp files.");
            }
            else
            {
                r.Severity = Severity.Info;
                r.Title = L.S("Crash dumps: " + count + " (" + Util.FormatBytes(total) + ")",
                              "Crash dumps: " + count + " (" + Util.FormatBytes(total) + ")");
                r.Details = dir;
            }
            return r;
        }

        private CheckResult CheckReboot()
        {
            var r = new CheckResult { Id = Consts.IdReboot };
            bool pending = Reg.KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

            string value = Reg.GetString(
                @"SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations");
            if (!string.IsNullOrEmpty(value)) pending = true;

            r.Severity = pending ? Severity.Warn : Severity.Ok;
            r.Title = pending
                ? L.S("Reinicializacao pendente", "Pending reboot")
                : L.S("Nenhuma reinicializacao pendente", "No pending reboot");
            r.Details = pending
                ? L.S("Reinicie o PC antes de instalar (o Windows pode bloquear drivers).",
                      "Reboot before installing (Windows may block drivers).")
                : "";
            return r;
        }

        private CheckResult CheckDisk()
        {
            var r = new CheckResult { Id = Consts.IdDisk };
            string root = Path.GetPathRoot(Environment.SystemDirectory);
            try
            {
                var di = new DriveInfo(root);
                double freeGb = di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                bool ntfs = di.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);

                r.Severity = freeGb < 20 ? Severity.Warn : Severity.Ok;
                r.Title = L.S("Disco do sistema: " + freeGb.ToString("0.0") + " GB livres",
                              "System drive: " + freeGb.ToString("0.0") + " GB free");
                r.Details = root + (ntfs ? " NTFS" : " (" + di.DriveFormat + ")") + "\n" +
                            L.S("O Link precisa de ~10 GB + cache de download. Instale em C:.",
                                "Link needs ~10 GB + download cache. Install to C:.");
            }
            catch (Exception ex)
            {
                r.Severity = Severity.Info;
                r.Title = L.S("Nao foi possivel ler o disco", "Could not read the drive");
                r.Details = ex.Message;
            }
            return r;
        }

        private CheckResult CheckDevice()
        {
            var r = new CheckResult { Id = Consts.IdDevice };
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name, Status, ConfigManagerErrorCode FROM Win32_PnPEntity " +
                    "WHERE DeviceID LIKE 'ROOT\\\\DISPLAY%'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = Convert.ToString(mo["Name"]);
                        if (name == null || name.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string status = Convert.ToString(mo["Status"]);
                        bool ok = string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase);

                        r.Severity = ok ? Severity.Ok : Severity.Warn;
                        r.Title = name + " - " + status;
                        r.Details = Convert.ToString(mo["DeviceID"]);
                        return r;
                    }
                }
            }
            catch (Exception ex)
            {
                r.Severity = Severity.Info;
                r.Title = L.S("Nao foi possivel consultar dispositivos", "Could not query devices");
                r.Details = ex.Message;
                return r;
            }

            r.Severity = Severity.Info;
            r.Title = L.S("Meta Virtual Monitor nao instalado", "Meta Virtual Monitor not installed");
            r.Details = L.S("Normal antes da instalacao. Depois dela este item deve ficar OK.",
                            "Normal before installing. After installing this item should be OK.");
            return r;
        }

        private CheckResult CheckInstalled()
        {
            var r = new CheckResult { Id = Consts.IdInstalled };
            var found = new List<string>();

            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string root in roots)
            {
                using (RegistryKey k = Reg.Open(root, false))
                {
                    if (k == null) continue;

                    foreach (string sub in k.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey app = k.OpenSubKey(sub))
                            {
                                if (app == null) continue;
                                string name = app.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name)) continue;
                                if (name.IndexOf("Meta Horizon", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    name.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) < 0)
                                    continue;
                                if (name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                                string version = Convert.ToString(app.GetValue("DisplayVersion"));
                                string location = Convert.ToString(app.GetValue("InstallLocation"));
                                found.Add(name + " " + version +
                                          (string.IsNullOrEmpty(location) ? "" : " -> " + location));
                            }
                        }
                        catch { }
                    }
                }
            }

            if (found.Count == 0)
            {
                r.Severity = Severity.Info;
                r.Title = L.S("Meta Horizon Link nao instalado", "Meta Horizon Link is not installed");
                r.Details = L.S("Nada a fazer alem de instalar.", "Nothing to do besides installing.");
            }
            else
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Meta Horizon Link ja instalado", "Meta Horizon Link is already installed");
                r.Details = Util.Join(found, "\n");
                foreach (string line in found)
                {
                    if (line.IndexOf("Meta Horizon", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.Severity = Severity.Ok;
                        r.Title = L.S("Meta Horizon Link instalado", "Meta Horizon Link installed");
                        break;
                    }
                }
            }
            return r;
        }

        private CheckResult CheckVerdict(List<CheckResult> all)
        {
            int blockers = all.Count(c => c.Severity == Severity.Blocker);
            var r = new CheckResult { Id = Consts.IdVerdict };

            if (blockers == 0)
            {
                r.Severity = Severity.Ok;
                r.Title = L.S("Pronto: nenhum bloqueio conhecido",
                              "Ready: no known blockers");
                r.Details = L.S("Pode executar o instalador do Meta Horizon Link.",
                                "You can run the Meta Horizon Link installer now.");
            }
            else
            {
                r.Severity = Severity.Blocker;
                r.Title = L.S(blockers + " bloqueio(s) encontrado(s)",
                              blockers + " blocker(s) found");
                r.Details = L.S("Enquanto houver bloqueios, o instalador da Meta vai desfazer tudo\n",
                                "While blockers remain, the Meta installer will roll everything back\n") +
                            L.S("no final sem explicar o motivo.", "at the end without explaining why.");
            }
            return r;
        }

        // ------------------------------------------------------------------ FIX

        public FixReport ApplyFixes(IList<CheckResult> results, Action<string> log)
        {
            var report = new FixReport();
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            foreach (CheckResult r in results)
            {
                if (!r.Fixable) continue;
                if (r.Severity != Severity.Blocker && r.Severity != Severity.Warn) continue;

                try
                {
                    switch (r.Id)
                    {
                        case Consts.IdPolicy:
                            FixDisplayPolicy(report, log);
                            break;

                        case Consts.IdService:
                            FixOvrService(report, log);
                            break;

                        case Consts.IdFolders:
                            FixStaleFolders(report, log, stamp);
                            break;

                        case Consts.IdDumps:
                            FixDumps(report, log);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    report.Fail(r.Id + ": " + ex.Message);
                    log("[FALHA] " + r.Id + ": " + ex.Message);
                }
            }

            return report;
        }

        private void FixDisplayPolicy(FixReport report, Action<string> log)
        {
            using (RegistryKey k = Reg.Open(Consts.DenyClassesKey, true))
            {
                if (k == null)
                {
                    report.Fail(L.S("Chave de politica nao encontrada", "Policy key not found"));
                    return;
                }

                var names = new List<string>();
                foreach (string name in k.GetValueNames())
                {
                    string data = k.GetValue(name) as string;
                    if (!string.IsNullOrEmpty(data) && NormalizeGuid(data) == Consts.DisplayClassGuid)
                        names.Add(name);
                }

                if (names.Count == 0)
                {
                    report.Info(L.S("Nada para remover na politica", "Nothing to remove in the policy"));
                    return;
                }

                string backup = BackupDenyClasses(k, names);
                report.Info(L.S("Backup: " + backup, "Backup: " + backup));
                log(L.S("Backup da politica: ", "Policy backup: ") + backup);

                foreach (string name in names)
                {
                    k.DeleteValue(name, false);
                    report.Ok(L.S("Politica removida: " + name, "Policy value removed: " + name));
                    log(L.S("Removido valor de politica: ", "Removed policy value: ") + name);
                }
            }
        }

        private void FixOvrService(FixReport report, Action<string> log)
        {
            string sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
            string output = RunAndCapture(sc, "delete " + Consts.OvrService);

            if (Reg.GetServiceImagePath(Consts.OvrService) == null)
            {
                report.Ok(L.S("Registro do OVRService removido", "OVRService registration removed"));
                report.Info(L.S("sc: ", "sc: ") + output.Trim());
                log(L.S("sc output: ", "sc output: ") + output.Trim());
                LogHelper.Add("OVRService removido. Saida do sc: " + output.Trim());
            }
            else
            {
                report.Fail(L.S("Nao foi possivel remover o OVRService",
                                "Could not remove OVRService"));
                log(output);
            }
        }

        private void FixStaleFolders(FixReport report, Action<string> log, string stamp)
        {
            foreach (string dir in StaleFolders.ToArray())
            {
                string dst = dir.TrimEnd('\\') + ".old-" + stamp;
                try
                {
                    Directory.Move(dir, dst);
                    report.Ok(L.S("Renomeada: " + dir + " -> " + Path.GetFileName(dst),
                                  "Renamed: " + dir + " -> " + Path.GetFileName(dst)));
                    log(L.S("Renomeada pasta antiga: ", "Renamed old folder: ") + dir);
                }
                catch (Exception ex)
                {
                    report.Fail(L.S("Nao foi possivel renomear " + dir + ": " + ex.Message,
                                    "Could not rename " + dir + ": " + ex.Message));
                }
            }
        }

        private void FixDumps(FixReport report, Action<string> log)
        {
            if (DumpDirectory == null || DumpCount == 0) return;

            bool wasRunning = ServiceHelper.IsRunning(Consts.OvrService);
            if (wasRunning)
            {
                log(L.S("Parando OVRService para limpar os dumps...",
                        "Stopping OVRService to clean dumps..."));
                ServiceHelper.Stop(Consts.OvrService, TimeSpan.FromSeconds(30));
            }

            int deleted = 0;
            foreach (string f in Directory.GetFiles(DumpDirectory, "*.dmp"))
            {
                try { File.Delete(f); deleted++; } catch { }
            }

            report.Ok(L.S("Crash dumps removidos: " + deleted, "Crash dumps deleted: " + deleted));
            log(L.S("Dumps removidos: ", "Dumps deleted: ") + deleted);

            if (wasRunning)
            {
                ServiceHelper.Start(Consts.OvrService, TimeSpan.FromSeconds(30));
                report.Info(L.S("OVRService iniciado novamente.", "OVRService started again."));
                log(L.S("OVRService reiniciado.", "OVRService started again."));
            }
        }

        private string BackupDenyClasses(RegistryKey key, List<string> names)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "MetaHorizonLinkDoctor");
            Directory.CreateDirectory(dir);

            string file = Path.Combine(dir,
                "DenyDeviceClasses-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".reg");

            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            sb.AppendLine(@"[HKEY_LOCAL_MACHINE\" + Consts.DenyClassesKey + "]");

            foreach (string name in names)
            {
                string data = key.GetValue(name) as string;
                sb.AppendLine("\"" + name + "\"=\"" + (data ?? "") + "\"");
            }

            File.WriteAllText(file, sb.ToString(), Encoding.Unicode);
            return file;
        }

        // ------------------------------------------------------------------ helpers

        private static string NormalizeGuid(string guid)
        {
            if (guid == null) return "";
            string g = guid.Trim().ToLowerInvariant();
            if (!g.StartsWith("{")) g = "{" + g;
            if (!g.EndsWith("}")) g = g + "}";
            return g;
        }

        private static bool IsSuspiciousPath(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(user) &&
                exe.StartsWith(user, StringComparison.OrdinalIgnoreCase))
                return true;

            return exe.IndexOf(@"\Temp\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrEmpty(user))
            {
                yield return Path.Combine(user, "Downloads");
                yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                yield return user;
            }

            yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string temp = Path.GetTempPath();
            if (!string.IsNullOrEmpty(temp)) yield return temp;

            DriveInfo[] drives = null;
            try { drives = DriveInfo.GetDrives(); } catch { }

            if (drives != null)
            {
                foreach (DriveInfo d in drives)
                {
                    if (d.DriveType != DriveType.Fixed) continue;
                    if (!d.IsReady) continue;
                    yield return d.RootDirectory.FullName;
                }
            }
        }

        /// <summary>Subpastas imediatas; nunca lanca excecao (raiz de disco, pastas protegidas...).</summary>
        private static IEnumerable<string> SafeSubDirectories(string baseDir)
        {
            string[] dirs = null;
            try { dirs = Directory.GetDirectories(baseDir); }
            catch { }

            if (dirs == null) yield break;

            foreach (string d in dirs)
                yield return d;
        }

        private static bool LooksLikeInstall(string dir)
        {
            string[] probes =
            {
                @"Support\oculus-runtime\OVRServer_x64.exe",
                @"Oculus\Support\oculus-runtime\OVRServer_x64.exe",
                @"Oculus\OculusSetup.exe",
                "Setup.exe",
                "OculusSetup.exe"
            };

            foreach (string p in probes)
            {
                try { if (File.Exists(Path.Combine(dir, p))) return true; } catch { }
            }

            try { if (Directory.Exists(Path.Combine(dir, "Manifests"))) return true; } catch { }

            return false;
        }

        private static string RunAndCapture(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    return (stdout + stderr).Trim();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    internal static class ServiceHelper
    {
        public static bool IsRunning(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    return sc.Status == ServiceControllerStatus.Running;
            }
            catch { return false; }
        }

        public static void Stop(string name, TimeSpan timeout)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status != ServiceControllerStatus.Running) return;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                }
            }
            catch { }
        }

        public static void Start(string name, TimeSpan timeout)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running) return;
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                }
            }
            catch { }
        }
    }

    /// <summary>Log simples em arquivo, para suporte.</summary>
    internal static class LogHelper
    {
        private static readonly List<string> Buffer = new List<string>();
        private static string _path;

        public static void SetPath(string path)
        {
            _path = path;
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch { }
        }

        public static void Add(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            Buffer.Add(line);
            try { if (_path != null) File.AppendAllText(_path, line + Environment.NewLine); } catch { }
        }
    }
}
