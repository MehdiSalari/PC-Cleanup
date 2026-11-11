// MainForm.cs - main window layout and controls
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public partial class MainForm : Form
    {
        List<CleanupOp> ops = new List<CleanupOp>();
        List<string> groupOrder = new List<string>();
        Dictionary<string, string> groupTitles = new Dictionary<string, string>();
        Dictionary<string, GroupBox> gboxes = new Dictionary<string, GroupBox>();
        Dictionary<CleanupOp, CheckBox> checks = new Dictionary<CleanupOp, CheckBox>();
        Dictionary<CleanupOp, Label> sizeLabels = new Dictionary<CleanupOp, Label>();
        Panel groupsPanel;
        CheckBox dryCheck, forceCheck, resetCheck;
        NumericUpDown ageUpDown;
        Button startBtn, cancelBtn, adminBtn, rescanBtn, logBtn;
        Label headerFree, headerAdmin, totalLabel, currentLabel, freedLabel;
        FancyProgressBar bar;
        RichTextBox logBox;
        ToolTip tip = new ToolTip();
        BackgroundWorker scanWorker = new BackgroundWorker();
        BackgroundWorker runWorker = new BackgroundWorker();
        bool isAdmin;
        int scanAge = 1;
        int totalOps;
        double fraction;
        string logFile;
        string[] spins = new string[] { "|", "/", "-", "\\" };

        static Color C_BG = Color.FromArgb(11, 16, 29);
        static Color C_PANEL = Color.FromArgb(19, 27, 44);
        static Color C_FIELD = Color.FromArgb(29, 40, 61);
        static Color C_TEXT = Color.FromArgb(235, 241, 250);
        static Color C_DIM = Color.FromArgb(139, 153, 176);
        static Color C_ACCENT = Color.FromArgb(139, 125, 255);
        static Color C_GREEN = Color.FromArgb(52, 211, 153);
        static Color C_WARN = Color.FromArgb(251, 191, 36);
        static Color C_GREENBTN = Color.FromArgb(5, 150, 105);
        static Color C_REDBTN = Color.FromArgb(185, 55, 75);

        public MainForm()
        {
            scanWorker.WorkerReportsProgress = true;
            scanWorker.DoWork += ScanDoWork;
            scanWorker.ProgressChanged += ScanProgressChanged;
            scanWorker.RunWorkerCompleted += ScanCompleted;
            runWorker.WorkerReportsProgress = true;
            runWorker.WorkerSupportsCancellation = true;
            runWorker.DoWork += RunDoWork;
            runWorker.ProgressChanged += RunProgressChanged;
            runWorker.RunWorkerCompleted += RunCompleted;

            BuildUi();
            this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            CheckAdmin();
            RefreshFree();
            BuildOps();
            BuildGroups();
            AppendLog("PC Cleanup is ready. Scanning cleanup targets…", LogKind.Dim);
            if (!isAdmin) AppendLog("Administrator access is required for Windows-level cleanup. Use the Restart as Admin button.", LogKind.Warning);
            else AppendLog("Running with Administrator privileges.", LogKind.Dim);
            if (FileSystemService.FreeC() / 1073741824.0 < 20.0)
                AppendLog("Warning: Drive C: has less than 20 GB free. Windows-level cleanup will be protected.", LogKind.Warning);
            ScanStart();
        }

        void BuildUi()
        {
            Text = "PC Cleanup | System Optimizer";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 760);
            Size = new Size(1240, 900);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9.5F);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = C_BG;
            root.RightToLeft = RightToLeft.No;
            root.ColumnCount = 2;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = C_PANEL;
            header.Padding = new Padding(22, 10, 22, 8);
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);

            Label title = new Label();
            title.Text = "PC Cleanup";
            title.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
            title.ForeColor = C_TEXT;
            title.BackColor = C_PANEL;
            title.AutoSize = true;
            title.Location = new Point(22, 8);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Clean safely. Reclaim space. Keep your files and apps intact.";
            subtitle.Font = new Font("Segoe UI", 9F);
            subtitle.ForeColor = C_DIM;
            subtitle.BackColor = C_PANEL;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(24, 48);
            header.Controls.Add(subtitle);

            headerFree = new Label();
            headerFree.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            headerFree.Dock = DockStyle.Bottom;
            headerFree.TextAlign = ContentAlignment.MiddleRight;
            headerFree.ForeColor = C_ACCENT;
            headerFree.BackColor = C_PANEL;
            headerFree.Height = 26;
            header.Controls.Add(headerFree);

            headerAdmin = new Label();
            headerAdmin.Font = new Font("Segoe UI", 8.5F);
            headerAdmin.Dock = DockStyle.Bottom;
            headerAdmin.TextAlign = ContentAlignment.MiddleRight;
            headerAdmin.ForeColor = C_DIM;
            headerAdmin.BackColor = C_PANEL;
            headerAdmin.Height = 22;
            header.Controls.Add(headerAdmin);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = C_BG;
            root.Controls.Add(left, 0, 1);

            GroupBox opt = new GroupBox();
            opt.Text = "  CLEANUP SETTINGS  ";
            opt.ForeColor = C_TEXT;
            opt.BackColor = C_PANEL;
            opt.Dock = DockStyle.Top;
            opt.Location = new Point(10, 36);
            opt.AutoSize = true;
            opt.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            opt.Padding = new Padding(14, 10, 14, 12);
            left.Controls.Add(opt);

            TableLayoutPanel otl = new TableLayoutPanel();
            otl.Dock = DockStyle.Top;
            otl.AutoSize = true;
            otl.BackColor = C_PANEL;
            otl.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            otl.ColumnCount = 2;
            otl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            otl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            opt.Controls.Add(otl);

            Label ageLbl = new Label();
            ageLbl.Text = "Temporary files older than (days)";
            ageLbl.AutoSize = true;
            ageLbl.ForeColor = C_TEXT;
            ageLbl.BackColor = C_PANEL;
            ageLbl.Margin = new Padding(3, 7, 8, 5);
            ageUpDown = new NumericUpDown();
            ageUpDown.Minimum = 1;
            ageUpDown.Maximum = 30;
            ageUpDown.Value = 1;
            ageUpDown.Width = 50;
            ageUpDown.BackColor = C_FIELD;
            ageUpDown.ForeColor = C_TEXT;
            ageUpDown.BorderStyle = BorderStyle.FixedSingle;
            ageUpDown.Margin = new Padding(3, 4, 3, 5);
            otl.Controls.Add(ageLbl, 0, 0);
            otl.Controls.Add(ageUpDown, 1, 0);

            dryCheck = new CheckBox();
            dryCheck.Text = "Preview mode (nothing is deleted)";
            dryCheck.Checked = true;
            dryCheck.AutoSize = true;
            dryCheck.ForeColor = C_TEXT;
            dryCheck.BackColor = C_PANEL;
            dryCheck.Margin = new Padding(3, 8, 3, 2);
            tip.SetToolTip(dryCheck, "Run a safe preview to estimate reclaimed space. Turn this off to perform cleanup.");
            otl.Controls.Add(dryCheck, 0, 1);
            otl.SetColumnSpan(dryCheck, 2);

            forceCheck = new CheckBox();
            forceCheck.Text = "Override Windows Update protection";
            forceCheck.AutoSize = true;
            forceCheck.ForeColor = C_TEXT;
            forceCheck.BackColor = C_PANEL;
            forceCheck.Margin = new Padding(3, 6, 3, 2);
            tip.SetToolTip(forceCheck, "Allows Windows Update cache cleanup while an update may be in progress. Not recommended.");
            otl.Controls.Add(forceCheck, 0, 2);
            otl.SetColumnSpan(forceCheck, 2);

            resetCheck = new CheckBox();
            resetCheck.Text = "DISM /ResetBase (permanent)";
            resetCheck.AutoSize = true;
            resetCheck.ForeColor = C_WARN;
            resetCheck.BackColor = C_PANEL;
            resetCheck.Margin = new Padding(3, 6, 3, 2);
            tip.SetToolTip(resetCheck, "May free more space, but permanently removes the ability to uninstall installed updates.");
            otl.Controls.Add(resetCheck, 0, 3);
            otl.SetColumnSpan(resetCheck, 2);

            totalLabel = new Label();
            totalLabel.Text = "SELECTED SPACE   —";
            totalLabel.AutoSize = true;
            totalLabel.ForeColor = C_ACCENT;
            totalLabel.BackColor = C_PANEL;
            totalLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            totalLabel.Margin = new Padding(3, 10, 3, 2);
            otl.Controls.Add(totalLabel, 0, 4);
            otl.SetColumnSpan(totalLabel, 2);

            Panel btns = new Panel();
            btns.Dock = DockStyle.Bottom;
            btns.Height = 218;
            btns.BackColor = C_BG;
            btns.Padding = new Padding(12, 0, 12, 10);
            left.Controls.Add(btns);

            logBtn = MakeBtn("OPEN ACTIVITY LOG", 34, C_FIELD, Color.FromArgb(60, 60, 76), false);
            logBtn.Click += delegate { OpenLogFile(); };
            btns.Controls.Add(logBtn);

            adminBtn = MakeBtn("RESTART AS ADMIN", 36, Color.FromArgb(60, 90, 140), Color.FromArgb(75, 110, 165), true);
            adminBtn.Click += delegate { RestartAsAdmin(); };
            btns.Controls.Add(adminBtn);

            rescanBtn = MakeBtn("RESCAN CLEANUP TARGETS", 36, C_FIELD, Color.FromArgb(60, 60, 76), true);
            rescanBtn.Click += delegate { ScanStart(); };
            btns.Controls.Add(rescanBtn);

            cancelBtn = MakeBtn("STOP", 36, C_REDBTN, Color.FromArgb(200, 75, 75), false);
            cancelBtn.Click += delegate { runWorker.CancelAsync(); };
            btns.Controls.Add(cancelBtn);

            startBtn = MakeBtn("START CLEANUP", 48, C_GREENBTN, Color.FromArgb(38, 180, 125), true);
            startBtn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            startBtn.Click += delegate { StartRun(); };
            startBtn.Text = dryCheck.Checked ? "START PREVIEW" : "START CLEANUP";
            dryCheck.CheckedChanged += delegate
            {
                if (!runWorker.IsBusy)
                    startBtn.Text = dryCheck.Checked ? "START PREVIEW" : "START CLEANUP";
            };
            btns.Controls.Add(startBtn);

            groupsPanel = new Panel();
            groupsPanel.Dock = DockStyle.Fill;
            groupsPanel.AutoScroll = true;
            groupsPanel.BackColor = C_BG;
            groupsPanel.Padding = new Padding(16, 14, 16, 14);
            root.Controls.Add(groupsPanel, 1, 1);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = C_BG;
            bottom.Padding = new Padding(16, 10, 16, 14);
            root.Controls.Add(bottom, 0, 2);
            root.SetColumnSpan(bottom, 2);

            Label activityCaption = new Label();
            activityCaption.Text = "LIVE ACTIVITY";
            activityCaption.ForeColor = C_DIM;
            activityCaption.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            activityCaption.AutoSize = true;
            activityCaption.Location = new Point(18, 0);
            bottom.Controls.Add(activityCaption);

            TableLayoutPanel btl = new TableLayoutPanel();
            btl.Dock = DockStyle.Fill;
            btl.Padding = new Padding(0, 18, 0, 0);
            btl.BackColor = C_BG;
            btl.ColumnCount = 2;
            btl.RowCount = 3;
            btl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            btl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            btl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            btl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            btl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bottom.Controls.Add(btl);

            currentLabel = new Label();
            currentLabel.Text = "READY";
            currentLabel.ForeColor = C_TEXT;
            currentLabel.BackColor = C_BG;
            currentLabel.Dock = DockStyle.Fill;
            currentLabel.TextAlign = ContentAlignment.MiddleLeft;
            freedLabel = new Label();
            freedLabel.Text = "";
            freedLabel.ForeColor = C_GREEN;
            freedLabel.BackColor = C_BG;
            freedLabel.Dock = DockStyle.Fill;
            freedLabel.TextAlign = ContentAlignment.MiddleRight;
            freedLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btl.Controls.Add(currentLabel, 0, 0);
            btl.Controls.Add(freedLabel, 1, 0);

            bar = new FancyProgressBar();
            bar.Dock = DockStyle.Fill;
            btl.Controls.Add(bar, 0, 1);
            btl.SetColumnSpan(bar, 2);

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BackColor = Color.FromArgb(8, 12, 22);
            logBox.ForeColor = C_TEXT;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Cascadia Code", 9F);
            logBox.DetectUrls = false;
            logBox.RightToLeft = RightToLeft.No;
            btl.Controls.Add(logBox, 0, 2);
            btl.SetColumnSpan(logBox, 2);

            FormClosing += delegate(object s, FormClosingEventArgs e2)
            {
                if (runWorker.IsBusy)
                {
                    DialogResult dr = MessageBox.Show("Cleanup is still running. Closing now may leave the operation incomplete. Close anyway?", "PC Cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.No) e2.Cancel = true;
                    else runWorker.CancelAsync();
                }
            };
        }

        Button MakeBtn(string text, int h, Color back, Color hover, bool enabled)
        {
            Button b = new Button();
            b.Text = text;
            b.Height = h;
            b.Dock = DockStyle.Top;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(48, 62, 87);
            b.BackColor = back;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.ForeColor = Color.White;
            b.Enabled = enabled;
            b.RightToLeft = RightToLeft.No;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            return b;
        }

        void BuildGroups()
        {
            groupsPanel.Controls.Clear();
            checks.Clear();
            sizeLabels.Clear();
            gboxes.Clear();
            foreach (string g in groupOrder)
            {
                GroupBox gb = new GroupBox();
                gb.Text = "  " + groupTitles[g];
                gb.ForeColor = C_TEXT;
                gb.BackColor = C_PANEL;
                gb.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                gb.Dock = DockStyle.Top;
                gb.AutoSize = true;
                gb.Padding = new Padding(14, 8, 14, 12);

                TableLayoutPanel tl = new TableLayoutPanel();
                tl.Dock = DockStyle.Top;
                tl.AutoSize = true;
                tl.BackColor = C_PANEL;
                tl.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                tl.Padding = new Padding(2, 0, 2, 4);
                tl.ColumnCount = 2;
                tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                int row = 0;
                foreach (CleanupOp op in ops)
                {
                    if (op.Group != g) continue;
                    CheckBox cb = new CheckBox();
                    cb.Text = op.Name;
                    cb.ForeColor = C_TEXT;
                    cb.AutoSize = true;
                    cb.Checked = op.DefaultChecked;
                    cb.Enabled = !op.Admin || isAdmin;
                    cb.Padding = new Padding(2, 7, 2, 7);
                    if (op.Hint != null) tip.SetToolTip(cb, op.Hint);
                    cb.CheckedChanged += delegate { UpdateTotal(); };
                    Label lbl = new Label();
                    lbl.Text = "…";
                    lbl.ForeColor = C_DIM;
                    lbl.BackColor = C_PANEL;
                    lbl.TextAlign = ContentAlignment.MiddleRight;
                    lbl.Dock = DockStyle.Fill;
                    lbl.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                    tl.Controls.Add(cb, 0, row);
                    tl.Controls.Add(lbl, 1, row);
                    checks[op] = cb;
                    sizeLabels[op] = lbl;
                    row++;
                }
                gb.Controls.Add(tl);
                groupsPanel.Controls.Add(gb);
                gb.SendToBack();
                gboxes[g] = gb;
            }
            RefreshGroupTitles();
            UpdateTotal();
        }

        void CheckAdmin()
        {
            isAdmin = false;
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal p = new WindowsPrincipal(id);
                    isAdmin = p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { }
            headerAdmin.Text = isAdmin ? "●  ADMINISTRATOR ACCESS" : "●  STANDARD USER  |  ADMIN ACCESS REQUIRED FOR SYSTEM TASKS";
            headerAdmin.ForeColor = isAdmin ? C_GREEN : C_WARN;
            adminBtn.Visible = !isAdmin;
        }

        void RefreshFree()
        {
            long free = FileSystemService.FreeC();
            long tot = FileSystemService.TotalC();
            double pct = tot > 0 ? 100.0 * (1.0 - ((double)free / (double)tot)) : 0;
            headerFree.Text = string.Format("DRIVE C:  {0} FREE   ·   {1:0.0}% USED", FileSystemService.FmtBytes(free), pct);
        }

        void AppendLog(string text, LogKind kind)
        {
            Color c = C_TEXT;
            if (kind == LogKind.Success) c = C_GREEN;
            else if (kind == LogKind.Warning) c = C_WARN;
            else if (kind == LogKind.Error) c = Color.FromArgb(240, 120, 120);
            else if (kind == LogKind.Dim) c = C_DIM;
            else if (kind == LogKind.Accent) c = C_ACCENT;
            string stamp = DateTime.Now.ToString("HH:mm:ss") + "   ";
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = Color.FromArgb(120, 120, 135);
            logBox.AppendText(stamp);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionColor = c;
            logBox.AppendText(text + Environment.NewLine);
            logBox.ScrollToCaret();
        }

        void UpdateTotal()
        {
            long sum = 0;
            bool any = false;
            foreach (CleanupOp op in ops)
            {
                CheckBox cb;
                if (!checks.TryGetValue(op, out cb)) continue;
                if (cb.Checked && op.Measured >= 0) { sum += op.Measured; any = true; }
            }
            totalLabel.Text = any ? ("SELECTED SPACE   " + FileSystemService.FmtBytes(sum)) : "SELECTED SPACE   —";
        }

        void RefreshGroupTitles()
        {
            foreach (KeyValuePair<string, GroupBox> kv in gboxes)
            {
                long sum = 0;
                bool any = false;
                foreach (CleanupOp op in ops)
                {
                    if (op.Group != kv.Key) continue;
                    if (op.Measured >= 0) { sum += op.Measured; any = true; }
                }
                kv.Value.Text = "  " + groupTitles[kv.Key] + (any ? "   —   " + FileSystemService.FmtBytes(sum) : "");
            }
        }

        void SetOpsEnabled(bool enabled)
        {
            foreach (KeyValuePair<CleanupOp, CheckBox> kv in checks)
            {
                bool can = !kv.Key.Admin || isAdmin;
                kv.Value.Enabled = enabled && can;
            }
            dryCheck.Enabled = enabled;
            forceCheck.Enabled = enabled;
            resetCheck.Enabled = enabled;
            ageUpDown.Enabled = enabled;
        }

        void RestartAsAdmin()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Application.Exit();
            }
            catch { }
        }

        void OpenLogFile()
        {
            try
            {
                if (logFile != null && File.Exists(logFile))
                    Process.Start("notepad.exe", "\"" + logFile + "\"");
                else
                    Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(Application.ExecutablePath) + "\"");
            }
            catch { }
        }
    }
}


