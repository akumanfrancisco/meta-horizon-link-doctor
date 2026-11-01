using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MHDoctor
{
    internal class MainForm : Form
    {
        private readonly Doctor _doctor = new Doctor();
        private readonly double _scale;
        private List<CheckResult> _results = new List<CheckResult>();

        private ListView _list;
        private TextBox _log;
        private Button _btnScan;
        private Button _btnFix;
        private Button _btnDownload;
        private Button _btnSave;
        private Button _btnCopy;
        private ToolStripStatusLabel _status;
        private Label _verdict;
        private bool _busy;

        public MainForm()
        {
            _scale = Win32.SystemDpi() / 96.0;

            Text = Program.AppName + " v" + Program.AppVersion;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;   // escalamos manualmente (ver S)
            ClientSize = new Size(S(960), S(640));
            MinimumSize = new Size(S(880), S(560));
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(250, 250, 252);
            Icon = AppIcon.Load();

            BuildUi();
            Shown += (s, e) => RunScan();
        }

        /// <summary>Converte uma medida de 96 DPI para o DPI atual da tela.</summary>
        private int S(int logical)
        {
            return (int)Math.Round(logical * _scale);
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = S(88),
                BackColor = Color.FromArgb(23, 33, 46),
                Padding = new Padding(0)
            };

            var title = new Label
            {
                Text = Program.AppName,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15f),
                AutoSize = true,
                Location = new Point(S(18), S(10)),
                BackColor = Color.Transparent
            };

            var subtitle = new Label
            {
                Text = L.S("Diagnostico e correcao da falha de instalacao do Meta Horizon Link no Windows 10/11.",
                           "Diagnoses and fixes the Meta Horizon Link install failure on Windows 10/11."),
                ForeColor = Color.FromArgb(178, 193, 210),
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(S(20), S(52)),
                BackColor = Color.Transparent
            };

            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            _verdict = new Label
            {
                Dock = DockStyle.Top,
                Height = S(46),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(S(16), 0, S(16), 0),
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = Color.FromArgb(240, 243, 247),
                Text = L.S("Verificando...", "Checking...")
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add(L.S("Status", "Status"), S(80));
            _list.Columns.Add(L.S("Item", "Item"), S(320));
            _list.Columns.Add(L.S("Detalhes", "Details"), S(520));

            _log = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = S(120),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(32, 36, 44),
                ForeColor = Color.FromArgb(210, 218, 228),
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.None,
                WordWrap = false
            };

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = S(62),
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(S(12), S(13), S(12), S(10))
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent
            };

            _btnScan = MakeButton(L.S("Verificar de novo", "Scan again"), S(150));
            _btnScan.Click += (s, e) => RunScan();

            _btnFix = MakeButton(L.S("Corrigir problemas", "Fix problems"), S(165));
            _btnFix.Click += (s, e) => RunFix();

            _btnDownload = MakeButton(L.S("Baixar instalador", "Get installer"), S(150));
            _btnDownload.Click += (s, e) => OpenUrl(Consts.DownloadUrl);

            _btnSave = MakeButton(L.S("Salvar relatorio", "Save report"), S(140));
            _btnSave.Click += (s, e) => SaveReport();

            _btnCopy = MakeButton(L.S("Copiar relatorio", "Copy report"), S(140));
            _btnCopy.Click += (s, e) => CopyReport();

            flow.Controls.AddRange(new Control[] { _btnScan, _btnFix, _btnDownload, _btnSave, _btnCopy });
            bottom.Controls.Add(flow);

            var status = new StatusStrip { SizingGrip = false };
            _status = new ToolStripStatusLabel(L.S("Pronto.", "Ready."))
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            status.Items.Add(_status);

            Controls.Add(_list);
            Controls.Add(_verdict);
            Controls.Add(_log);
            Controls.Add(bottom);
            Controls.Add(header);
            Controls.Add(status);

            _list.Resize += (s, e) => LayoutColumns();

            LogLine(L.S("Iniciando diagnostico...", "Starting diagnosis..."));
            LogLine(L.S("Nenhum dado sai do seu computador. Tudo roda localmente.",
                        "No data leaves your computer. Everything runs locally."));
        }

        private void LayoutColumns()
        {
            int w = _list.ClientSize.Width;
            if (w < S(300)) return;

            int c0 = Math.Max(S(70), w / 10);
            int c1 = Math.Max(S(190), (int)(w * 0.36));
            int c2 = Math.Max(S(150), w - c0 - c1 - S(26));

            _list.Columns[0].Width = c0;
            _list.Columns[1].Width = c1;
            _list.Columns[2].Width = c2;
        }

        private Button MakeButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = S(34),
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, S(8), 0),
                UseVisualStyleBackColor = true
            };
        }

        // ------------------------------------------------------------------ actions

        private void RunScan()
        {
            if (_busy) return;
            SetBusy(true, L.S("Verificando o sistema...", "Checking the system..."));

            Task.Run(() => _doctor.Scan()).ContinueWith(t =>
            {
                if (IsDisposed) return;

                if (t.IsFaulted)
                {
                    SetBusy(false, L.S("Falha ao verificar.", "Scan failed."));
                    MessageBox.Show(this, t.Exception != null ? t.Exception.GetBaseException().Message : "?",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _results = t.Result;
                FillList(_results);
                SetBusy(false, L.S("Verificacao concluida.", "Scan finished."));
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void RunFix()
        {
            if (_busy) return;

            List<CheckResult> fixable = _results
                .Where(r => r.Fixable && (r.Severity == Severity.Blocker || r.Severity == Severity.Warn))
                .ToList();

            if (fixable.Count == 0)
            {
                MessageBox.Show(this,
                    L.S("Nenhum item corrigivel encontrado.", "No fixable items found."),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var text = new StringBuilder();
            text.AppendLine(L.S("As seguintes alteracoes serao feitas:", "The following changes will be made:"));
            text.AppendLine();
            foreach (CheckResult r in fixable)
                text.AppendLine("  - " + r.Title);
            text.AppendLine();
            text.AppendLine(L.S("Tudo e reversivel (backup .reg e pastas renomeadas).",
                                "Everything is reversible (.reg backup and renamed folders)."));
            text.AppendLine();
            text.AppendLine(L.S("Continuar?", "Continue?"));

            if (MessageBox.Show(this, text.ToString(), Program.AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            SetBusy(true, L.S("Corrigindo...", "Fixing..."));

            Task.Run(() =>
            {
                Action<string> log = line => BeginInvoke(new MethodInvoker(() => LogLine("  " + line)));
                FixReport report = _doctor.ApplyFixes(fixable, log);
                List<CheckResult> after = _doctor.Scan();
                return new Tuple<FixReport, List<CheckResult>>(report, after);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;

                if (t.IsFaulted)
                {
                    SetBusy(false, L.S("Falha ao corrigir.", "Fix failed."));
                    MessageBox.Show(this, t.Exception != null ? t.Exception.GetBaseException().Message : "?",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                foreach (string line in t.Result.Item1.Lines) LogLine(line);

                _results = t.Result.Item2;
                FillList(_results);
                SetBusy(false, L.S("Correcao concluida.", "Fix finished."));

                int remaining = _results.Count(r => r.Severity == Severity.Blocker);
                MessageBox.Show(this,
                    remaining == 0
                        ? L.S("Tudo certo. Agora rode o instalador do Meta Horizon Link.",
                              "All set. Now run the Meta Horizon Link installer.")
                        : L.S("Ainda restam " + remaining + " bloqueio(s). Veja a lista.",
                              remaining + " blocker(s) still remain. Check the list."),
                    Program.AppName, MessageBoxButtons.OK,
                    remaining == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SaveReport()
        {
            if (_results.Count == 0) return;

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Text (*.txt)|*.txt";
                dlg.FileName = "meta-horizon-link-doctor-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    File.WriteAllText(dlg.FileName, Report.Build(_results, Program.AppVersion));
                    _status.Text = L.S("Relatorio salvo.", "Report saved.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CopyReport()
        {
            if (_results.Count == 0) return;
            try
            {
                Clipboard.SetText(Report.Build(_results, Program.AppVersion));
                _status.Text = L.S("Relatorio copiado.", "Report copied.");
            }
            catch { }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        // ------------------------------------------------------------------ rendering

        private void FillList(List<CheckResult> results)
        {
            _list.BeginUpdate();
            _list.Items.Clear();

            foreach (CheckResult r in results)
            {
                var item = new ListViewItem(Report.Label(r.Severity).Trim());
                item.SubItems.Add(r.Title ?? "");
                item.SubItems.Add(FirstLine(r.Details));
                item.Tag = r;
                item.ForeColor = ColorFor(r.Severity);
                _list.Items.Add(item);
            }

            _list.EndUpdate();

            int blockers = results.Count(r => r.Severity == Severity.Blocker);
            int warns = results.Count(r => r.Severity == Severity.Warn);

            if (blockers > 0)
            {
                _verdict.Text = L.S("  " + blockers + " bloqueio(s) encontrado(s) - a instalacao vai falhar",
                                    "  " + blockers + " blocker(s) found - the installation will fail");
                _verdict.ForeColor = Color.FromArgb(170, 28, 28);
                _verdict.BackColor = Color.FromArgb(255, 235, 235);
            }
            else if (warns > 0)
            {
                _verdict.Text = L.S("  " + warns + " aviso(s) - provavelmente funciona, mas revise",
                                    "  " + warns + " warning(s) - probably fine, but review");
                _verdict.ForeColor = Color.FromArgb(140, 84, 0);
                _verdict.BackColor = Color.FromArgb(255, 246, 228);
            }
            else
            {
                _verdict.Text = L.S("  Nenhum bloqueio conhecido. Pode instalar.",
                                    "  No known blockers. You can install.");
                _verdict.ForeColor = Color.FromArgb(20, 105, 50);
                _verdict.BackColor = Color.FromArgb(232, 248, 238);
            }

            LogLine(Report.Label(Severity.Info).Trim() + " " +
                    L.S(results.Count + " verificacoes concluidas.", results.Count + " checks finished."));

            foreach (CheckResult r in results)
            {
                if (r.Severity == Severity.Blocker || r.Severity == Severity.Warn)
                    LogLine(Report.Label(r.Severity).Trim() + " " + r.Title);
            }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
        }

        private static Color ColorFor(Severity s)
        {
            switch (s)
            {
                case Severity.Blocker: return Color.FromArgb(165, 25, 25);
                case Severity.Warn: return Color.FromArgb(135, 80, 0);
                case Severity.Ok: return Color.FromArgb(20, 105, 50);
                default: return Color.FromArgb(70, 80, 95);
            }
        }

        private void LogLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _log.AppendText(text + Environment.NewLine);
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _btnScan.Enabled = !busy;
            _btnFix.Enabled = !busy;
            _status.Text = status;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }
    }

    internal static class AppIcon
    {
        public static Icon Load()
        {
            try
            {
                // usa o icone embutido no proprio .exe (/win32icon)
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            return null;
        }
    }
}
