using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MHDoctor
{
    internal static class Program
    {
        public const string AppName = "Meta Horizon Link Doctor";
        public const string AppVersion = "1.0.0";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LogHelper.SetPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"MetaHorizonLinkDoctor\doctor.log"));

            bool headless = args.Any(a => Eq(a, "--scan") || Eq(a, "--fix"));
            if (headless) return Cli.Run(args);

            Application.Run(new MainForm());
            return 0;
        }

        internal static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Modo sem interface (para suporte / CI):
    ///   MetaHorizonLinkDoctor.exe --scan [--out relatorio.txt]
    ///   MetaHorizonLinkDoctor.exe --fix  [--out relatorio.txt]
    /// Codigo de saida: 0 = sem bloqueios, 2 = bloqueios encontrados.
    /// </summary>
    internal static class Cli
    {
        public static int Run(string[] args)
        {
            bool fix = args.Any(a => Program.Eq(a, "--fix"));
            string outPath = ValueOf(args, "--out");

            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mh-doctor-report.txt");

            var doctor = new Doctor();
            List<CheckResult> results = doctor.Scan();

            var sb = new StringBuilder();
            sb.Append(Report.Build(results, Program.AppVersion));

            if (fix)
            {
                sb.AppendLine();
                sb.AppendLine("================ LET'S FIX / CORRECAO ================");

                FixReport report = doctor.ApplyFixes(results, line => { });
                foreach (string line in report.Lines) sb.AppendLine(line);

                sb.AppendLine();
                sb.AppendLine("================ AFTER / DEPOIS ================");
                results = doctor.Scan();
                sb.Append(Report.Build(results, Program.AppVersion));
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, sb.ToString());
                LogHelper.Add("Relatorio gravado em " + outPath);
            }
            catch { }

            return results.Any(r => r.Severity == Severity.Blocker) ? 2 : 0;
        }

        private static string ValueOf(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (Program.Eq(args[i], name)) return args[i + 1];
            return null;
        }
    }
}
