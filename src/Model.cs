using System;
using System.Collections.Generic;
using System.Text;

namespace MHDoctor
{
    public enum Severity
    {
        Ok = 0,
        Info = 1,
        Warn = 2,
        Blocker = 3
    }

    public class CheckResult
    {
        public string Id;
        public Severity Severity;
        public string Title;
        public string Details;

        /// <summary>Se true, o botao "Corrigir" pode resolver este item.</summary>
        public bool Fixable;
    }

    public class FixReport
    {
        public bool AnyApplied;
        public bool AnyFailed;
        public readonly List<string> Lines = new List<string>();

        public void Ok(string text)
        {
            AnyApplied = true;
            Lines.Add("[OK]   " + text);
        }

        public void Fail(string text)
        {
            AnyFailed = true;
            Lines.Add("[FALHA] " + text);
        }

        public void Info(string text)
        {
            Lines.Add("[info] " + text);
        }
    }

    internal static class Report
    {
        public static string Build(IList<CheckResult> results, string version)
        {
            var sb = new StringBuilder();
            Version os = Win32.RealOsVersion();

            sb.AppendLine(Program.AppName + " v" + version);
            sb.AppendLine("Data / Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Windows: " + os + (Win32.IsWindows11(os) ? " (Windows 11)" : ""));
            sb.AppendLine("Admin: " + (Win32.IsAdmin() ? "yes" : "no"));
            sb.AppendLine(new string('-', 78));

            foreach (CheckResult r in results)
            {
                sb.AppendLine("[" + Label(r.Severity) + "] " + r.Title);
                if (!string.IsNullOrEmpty(r.Details))
                {
                    foreach (string line in r.Details.Split('\n'))
                        sb.AppendLine("         " + line.TrimEnd('\r'));
                }
            }

            sb.AppendLine(new string('-', 78));
            sb.AppendLine(L.S("Cole este relatorio em uma issue do GitHub.",
                               "Paste this report into a GitHub issue."));
            return sb.ToString();
        }

        public static string Label(Severity s)
        {
            switch (s)
            {
                case Severity.Ok: return L.S(" OK ", " OK ");
                case Severity.Info: return L.S("INFO", "INFO");
                case Severity.Warn: return L.S("AVIS", "WARN");
                default: return L.S("ERRO", "FAIL");
            }
        }
    }
}
