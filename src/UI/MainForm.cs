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
        Dictionary<string, CategoryCard> gboxes = new Dictionary<string, CategoryCard>();
        Dictionary<CleanupOp, CheckBox> checks = new Dictionary<CleanupOp, CheckBox>();
        Dictionary<CleanupOp, Label> sizeLabels = new Dictionary<CleanupOp, Label>();
        Panel groupsPanel;
        Panel topBar;
        Panel content;
        Panel activityHost;
        CheckBox dryCheck, forceCheck, resetCheck;
        Stepper ageUpDown;
        Button startBtn, cancelBtn, adminBtn, rescanBtn, logBtn, recBtn, allBtn, noneBtn;
        StatusPill adminPill;
        Label summaryLabel, freedLabel;
        StatusLabel currentLabel;
        DriveGauge gauge;
        FancyProgressBar bar;
        RichTextBox logBox;
        ToolTip tip = new ToolTip();
        BackgroundWorker scanWorker = new BackgroundWorker();
        BackgroundWorker runWorker = new BackgroundWorker();
        WheelFilter wheelFilter;
        Timer fadeTimer;
        bool isAdmin;
        int scanAge = 1;
        int totalOps;
        double fraction;
        string logFile;

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
            try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            CheckAdmin();
            RefreshFree();
            BuildOps();
            BuildGroups();
            AppendLog("PC Cleanup is ready. Scanning cleanup targets…", LogKind.Dim);
            if (!isAdmin) AppendLog("Administrator access is required for Windows-level cleanup. Use the Restart as admin button.", LogKind.Warning);
            else AppendLog("Running with Administrator privileges.", LogKind.Dim);
            if (FileSystemService.FreeC() / 1073741824.0 < 20.0)
                AppendLog("Warning: Drive C: has less than 20 GB free. Windows-level cleanup will be protected.", LogKind.Warning);
            ScanStart();
        }

        // ------------------------------------------------------------------
        // window chrome
        // ------------------------------------------------------------------
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.StyleTitleBar(Handle, Theme.Bg, Theme.Text, Theme.Border);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            fadeTimer = new Timer();
            fadeTimer.Interval = 14;
            fadeTimer.Tick += delegate
            {
                double o = Opacity + 0.09;
                if (o >= 1.0)
                {
                    Opacity = 1.0;
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                    fadeTimer = null;
                }
                else Opacity = o;
            };
            fadeTimer.Start();
        }

        // ------------------------------------------------------------------
        // layout
        // ------------------------------------------------------------------
        void BuildUi()
        {
            Text = "PC Cleanup | System Optimizer";
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Theme.S(1280), wa.Width), Math.Min(Theme.S(860), wa.Height));
            MinimumSize = new Size(Math.Min(Theme.S(1100), wa.Width), Math.Min(Theme.S(740), wa.Height));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(9.5f);
            DoubleBuffered = true;
            Opacity = 0.0;   // faded in from OnShown

            SetupToolTip();

            content = new BufferedPanel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Theme.Bg;
            Panel sidebar = new BufferedPanel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Theme.S(332);
            sidebar.BackColor = Theme.Sidebar;
            sidebar.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(Theme.Border, 1f))
                    pe.Graphics.DrawLine(p, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };
            Controls.Add(content);
            Controls.Add(sidebar);

            BuildSidebar(sidebar);
            BuildContent();

            FormClosing += delegate(object s, FormClosingEventArgs e2)
            {
                if (runWorker.IsBusy)
                {
                    DialogResult dr = MessageBox.Show("Cleanup is still running. Closing now may leave the operation incomplete. Close anyway?", "PC Cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr == DialogResult.No) e2.Cancel = true;
                    else runWorker.CancelAsync();
                }
            };
            FormClosed += delegate
            {
                if (wheelFilter != null) Application.RemoveMessageFilter(wheelFilter);
            };
        }

        void SetupToolTip()
        {
            tip.OwnerDraw = true;
            tip.ShowAlways = true;
            tip.InitialDelay = 300;
            tip.AutoPopDelay = 12000;
            tip.Popup += delegate(object s, PopupEventArgs pe)
            {
                try
                {
                    string txt = tip.GetToolTip(pe.AssociatedControl);
                    using (Bitmap bmp = new Bitmap(1, 1))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        SizeF sz = g.MeasureString(txt, Theme.Ui(9f), Theme.S(320));
                        pe.ToolTipSize = new Size((int)Math.Ceiling(sz.Width) + Theme.S(24), (int)Math.Ceiling(sz.Height) + Theme.S(18));
                    }
                }
                catch { }
            };
            tip.Draw += delegate(object s, DrawToolTipEventArgs de)
            {
                Graphics g = de.Graphics;
                Theme.Quality(g);
                g.Clear(Theme.SurfaceHi);
                using (Pen p = new Pen(Theme.Border, 1f))
                    g.DrawRectangle(p, 0, 0, de.Bounds.Width - 1, de.Bounds.Height - 1);
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString(de.ToolTipText, Theme.Ui(9f), b, new RectangleF(Theme.S(12), Theme.S(9), de.Bounds.Width - Theme.S(24), de.Bounds.Height - Theme.S(18)));
            };
        }

        Label MakeLabel(string text, Font font, Color color, Color back)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = back;
            l.AutoSize = false;
            return l;
        }

        ModernButton MakeButton(string text, ButtonLook look, IconKind icon, int heightPx, Color back)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Look = look;
            b.Icon = icon;
            b.Height = Theme.S(heightPx);
            b.BackColor = back;
            return b;
        }

        Panel Spacer(int heightPx, Color back)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = Theme.S(heightPx);
            p.BackColor = back;
            return p;
        }

        void BuildSidebar(Panel sidebar)
        {
            // ---- brand ----
            BrandPanel brand = new BrandPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = Theme.S(86);
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            brand.Subtitle = "System Optimizer  ·  v" + v.Major + "." + v.Minor + "." + v.Build;

            // ---- drive gauge (flexible) ----
            Panel gaugeHost = new BufferedPanel();
            gaugeHost.Dock = DockStyle.Fill;
            gaugeHost.BackColor = Theme.Sidebar;
            gaugeHost.Padding = new Padding(Theme.S(16), Theme.S(2), Theme.S(16), Theme.S(12));
            SurfacePanel gaugeCard = new SurfacePanel();
            gaugeCard.Dock = DockStyle.Fill;
            gaugeCard.Padding = new Padding(Theme.S(18), Theme.S(14), Theme.S(18), Theme.S(10));
            gauge = new DriveGauge();
            gauge.Dock = DockStyle.Fill;
            gaugeCard.Controls.Add(gauge);
            gaugeHost.Controls.Add(gaugeCard);

            // ---- settings ----
            Panel settingsHost = new BufferedPanel();
            settingsHost.Dock = DockStyle.Bottom;
            settingsHost.Height = Theme.S(238);
            settingsHost.BackColor = Theme.Sidebar;
            settingsHost.Padding = new Padding(Theme.S(16), 0, Theme.S(16), Theme.S(12));
            SurfacePanel settingsCard = new SurfacePanel();
            settingsCard.Dock = DockStyle.Fill;
            settingsCard.Padding = new Padding(Theme.S(18), Theme.S(12), Theme.S(18), Theme.S(8));
            settingsHost.Controls.Add(settingsCard);

            resetCheck = MakeToggle("DISM /ResetBase", "Permanent — removes update uninstall", "May free more space, but permanently removes the ability to uninstall installed updates.");
            ((ToggleSwitch)resetCheck).TextColor = Theme.Amber;
            ((ToggleSwitch)resetCheck).SetOnColors(Theme.Orange, Theme.Amber);
            forceCheck = MakeToggle("Override update protection", "Not recommended", "Allows Windows Update cache cleanup while an update may be in progress. Not recommended.");
            dryCheck = MakeToggle("Preview mode", "Nothing is deleted", "Run a safe preview to estimate reclaimed space. Turn this off to perform cleanup.");
            dryCheck.Checked = true;

            Panel ageRow = new BufferedPanel();
            ageRow.Dock = DockStyle.Top;
            ageRow.Height = Theme.S(42);
            ageRow.BackColor = Theme.Surface;
            Label ageLbl = MakeLabel("Temp files older than", Theme.Semi(9.5f), Theme.Text, Theme.Surface);
            ageLbl.TextAlign = ContentAlignment.MiddleLeft;
            ageUpDown = new Stepper();
            ageUpDown.Minimum = 1;
            ageUpDown.Maximum = 30;
            ageUpDown.Value = 1;
            ageUpDown.SetUnits("day", "days");
            tip.SetToolTip(ageUpDown, "Temporary files older than this age are removed. Files locked by running apps are skipped.");
            tip.SetToolTip(ageLbl, "Temporary files older than this age are removed. Files locked by running apps are skipped.");
            ageRow.Controls.Add(ageLbl);
            ageRow.Controls.Add(ageUpDown);
            ageUpDown.BringToFront();
            ageRow.Resize += delegate
            {
                ageUpDown.Location = new Point(ageRow.ClientSize.Width - ageUpDown.Width, (ageRow.ClientSize.Height - ageUpDown.Height) / 2);
                ageLbl.SetBounds(0, 0, Math.Max(10, ageUpDown.Left - Theme.S(8)), ageRow.ClientSize.Height);
            };

            Label caption = MakeLabel("SETTINGS", Theme.Bold(8.25f), Theme.Faint, Theme.Surface);
            caption.Dock = DockStyle.Top;
            caption.Height = Theme.S(26);
            caption.TextAlign = ContentAlignment.MiddleLeft;

            // docked "Top" controls stack in reverse add order
            settingsCard.Controls.Add(resetCheck);
            settingsCard.Controls.Add(forceCheck);
            settingsCard.Controls.Add(dryCheck);
            settingsCard.Controls.Add(ageRow);
            settingsCard.Controls.Add(caption);

            // ---- action buttons ----
            Panel buttonsHost = new BufferedPanel();
            buttonsHost.Dock = DockStyle.Bottom;
            buttonsHost.Height = Theme.S(170);
            buttonsHost.BackColor = Theme.Sidebar;
            buttonsHost.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), Theme.S(16));

            ModernButton start = MakeButton("Start Preview", ButtonLook.Primary, IconKind.Play, 54, Theme.Sidebar);
            start.Font = Theme.Semi(11f);
            start.Radius = 14;
            start.Dock = DockStyle.Top;
            startBtn = start;
            startBtn.Click += delegate { StartRun(); };

            ModernButton stop = MakeButton("Stop", ButtonLook.Danger, IconKind.Stop, 40, Theme.Sidebar);
            stop.Enabled = false;
            cancelBtn = stop;
            cancelBtn.Click += delegate { runWorker.CancelAsync(); };

            ModernButton rescan = MakeButton("Rescan", ButtonLook.Secondary, IconKind.Refresh, 40, Theme.Sidebar);
            rescanBtn = rescan;
            rescanBtn.Click += delegate { ScanStart(); };

            Panel pair = new BufferedPanel();
            pair.Dock = DockStyle.Top;
            pair.Height = Theme.S(40);
            pair.BackColor = Theme.Sidebar;
            pair.Controls.Add(cancelBtn);
            pair.Controls.Add(rescanBtn);
            pair.Resize += delegate
            {
                int gap = Theme.S(10);
                int w = (pair.ClientSize.Width - gap) / 2;
                cancelBtn.SetBounds(0, 0, w, pair.ClientSize.Height);
                rescanBtn.SetBounds(w + gap, 0, pair.ClientSize.Width - w - gap, pair.ClientSize.Height);
            };

            ModernButton log = MakeButton("Open activity log", ButtonLook.Ghost, IconKind.Log, 36, Theme.Sidebar);
            log.Dock = DockStyle.Top;
            logBtn = log;
            logBtn.Click += delegate { OpenLogFile(); };

            // admin button lives in the Windows (Administrator) card header
            ModernButton admin = MakeButton("Restart as admin", ButtonLook.Secondary, IconKind.Shield, 32, Theme.Surface);
            admin.Width = Theme.S(158);
            admin.Radius = 9;
            admin.Font = Theme.Semi(9f);
            adminBtn = admin;
            adminBtn.Click += delegate { RestartAsAdmin(); };

            buttonsHost.Controls.Add(logBtn);
            buttonsHost.Controls.Add(Spacer(8, Theme.Sidebar));
            buttonsHost.Controls.Add(pair);
            buttonsHost.Controls.Add(Spacer(10, Theme.Sidebar));
            buttonsHost.Controls.Add(startBtn);

            UpdateStartButton();
            dryCheck.CheckedChanged += delegate { if (!runWorker.IsBusy) UpdateStartButton(); };

            // z-order: last added docks first (top / bottom-most), Fill must be docked last
            sidebar.Controls.Add(gaugeHost);
            sidebar.Controls.Add(settingsHost);
            sidebar.Controls.Add(buttonsHost);
            sidebar.Controls.Add(brand);
        }

        CheckBox MakeToggle(string text, string hint, string tooltip)
        {
            ToggleSwitch t = new ToggleSwitch();
            t.Text = text;
            t.Hint = hint;
            t.Dock = DockStyle.Top;
            tip.SetToolTip(t, tooltip);
            return t;
        }

        void BuildContent()
        {
            // ---- scrolling list of categories ----
            BufferedPanel list = new BufferedPanel();
            list.Dock = DockStyle.Fill;
            list.AutoScroll = true;
            list.BackColor = Theme.Bg;
            list.Padding = new Padding(Theme.S(24), Theme.S(2), Theme.S(24), Theme.S(4));
            groupsPanel = list;
            Native.DarkScrollbars(groupsPanel);
            wheelFilter = new WheelFilter(groupsPanel);
            Application.AddMessageFilter(wheelFilter);

            // ---- activity card ----
            activityHost = new BufferedPanel();
            activityHost.Dock = DockStyle.Bottom;
            activityHost.Height = Theme.S(260);
            activityHost.BackColor = Theme.Bg;
            activityHost.Padding = new Padding(Theme.S(24), Theme.S(6), Theme.S(24), Theme.S(22));

            SurfacePanel card = new SurfacePanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(Theme.S(20), Theme.S(14), Theme.S(20), Theme.S(16));
            activityHost.Controls.Add(card);

            currentLabel = new StatusLabel();
            currentLabel.Text = "READY";
            currentLabel.Dock = DockStyle.Fill;
            currentLabel.IsBusy = delegate { return scanWorker.IsBusy || runWorker.IsBusy; };

            freedLabel = MakeLabel("", Theme.Semi(10.5f), Theme.Green, Theme.Surface);
            freedLabel.Dock = DockStyle.Right;
            freedLabel.Width = Theme.S(340);
            freedLabel.TextAlign = ContentAlignment.MiddleRight;

            Panel headerRow = new BufferedPanel();
            headerRow.Dock = DockStyle.Top;
            headerRow.Height = Theme.S(30);
            headerRow.BackColor = Theme.Surface;
            headerRow.Controls.Add(currentLabel);
            headerRow.Controls.Add(freedLabel);

            bar = new FancyProgressBar();
            bar.Dock = DockStyle.Fill;
            Panel barHost = new BufferedPanel();
            barHost.Dock = DockStyle.Top;
            barHost.Height = Theme.S(36);
            barHost.BackColor = Theme.Surface;
            barHost.Padding = new Padding(0, Theme.S(6), 0, Theme.S(8));
            barHost.Controls.Add(bar);

            SurfacePanel logHost = new SurfacePanel();
            logHost.Dock = DockStyle.Fill;
            logHost.Radius = 10;
            logHost.Fill = Theme.LogBg;
            logHost.Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(8), Theme.S(10));

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BackColor = Theme.LogBg;
            logBox.ForeColor = Theme.Text;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = Theme.Mono(9f);
            logBox.DetectUrls = false;
            logBox.RightToLeft = RightToLeft.No;
            Native.DarkScrollbars(logBox);
            logHost.Controls.Add(logBox);

            card.Controls.Add(logHost);
            card.Controls.Add(barHost);
            card.Controls.Add(headerRow);

            // ---- top bar ----
            topBar = new BufferedPanel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = Theme.S(88);
            topBar.BackColor = Theme.Bg;

            Label title = MakeLabel("Cleanup targets", Theme.Semi(18f), Theme.Text, Theme.Bg);
            title.Location = new Point(Theme.S(24), Theme.S(18));
            title.Size = new Size(Theme.S(420), Theme.S(32));
            title.TextAlign = ContentAlignment.MiddleLeft;
            summaryLabel = MakeLabel("Scanning your system…", Theme.Ui(9.5f), Theme.Dim, Theme.Bg);
            summaryLabel.Location = new Point(Theme.S(26), Theme.S(52));
            summaryLabel.Size = new Size(Theme.S(520), Theme.S(22));
            summaryLabel.TextAlign = ContentAlignment.MiddleLeft;

            adminPill = new StatusPill();
            recBtn = MakeSmallButton("Recommended", 112);
            allBtn = MakeSmallButton("Select all", 92);
            noneBtn = MakeSmallButton("Clear", 70);
            tip.SetToolTip(recBtn, "Restore the default selection.");
            recBtn.Click += delegate { ApplySelection(0); };
            allBtn.Click += delegate { ApplySelection(1); };
            noneBtn.Click += delegate { ApplySelection(2); };

            topBar.Controls.Add(title);
            topBar.Controls.Add(summaryLabel);
            topBar.Controls.Add(adminPill);
            topBar.Controls.Add(recBtn);
            topBar.Controls.Add(allBtn);
            topBar.Controls.Add(noneBtn);
            topBar.Resize += delegate { LayoutTopBar(); };

            content.Resize += delegate
            {
                activityHost.Height = Math.Max(Theme.S(214), Math.Min(Theme.S(340), (int)(content.ClientSize.Height * 0.34)));
            };

            // Fill first, then edge-docked controls (last added docks first)
            content.Controls.Add(groupsPanel);
            content.Controls.Add(activityHost);
            content.Controls.Add(topBar);
        }

        ModernButton MakeSmallButton(string text, int widthPx)
        {
            ModernButton b = MakeButton(text, ButtonLook.Secondary, IconKind.None, 30, Theme.Bg);
            b.Width = Theme.S(widthPx);
            b.Radius = 9;
            b.Font = Theme.Semi(9f);
            return b;
        }

        void LayoutTopBar()
        {
            if (topBar == null) return;
            adminPill.Height = Theme.S(30);
            int x = topBar.ClientSize.Width - Theme.S(24);
            Control[] order = new Control[] { noneBtn, allBtn, recBtn, adminPill };
            foreach (Control c in order)
            {
                x -= c.Width;
                c.Location = new Point(x, (topBar.ClientSize.Height - c.Height) / 2);
                x -= Theme.S(8);
            }
        }

        void UpdateStartButton()
        {
            ModernButton mb = startBtn as ModernButton;
            if (mb == null) return;
            bool dry = dryCheck.Checked;
            mb.Text = dry ? "Start Preview" : "Start Cleanup";
            mb.Icon = dry ? IconKind.Play : IconKind.Sparkle;
            if (dry) mb.SetGradient(Theme.Accent, Theme.Sky);
            else mb.SetGradient(Color.FromArgb(16, 185, 129), Theme.Teal);
        }

        // 0 = recommended, 1 = all, 2 = none
        void ApplySelection(int mode)
        {
            foreach (CleanupOp op in ops)
            {
                CheckBox cb;
                if (!checks.TryGetValue(op, out cb) || !cb.Enabled) continue;
                if (mode == 0) cb.Checked = op.DefaultChecked;
                else cb.Checked = mode == 1;
            }
        }

        // ------------------------------------------------------------------
        // categories
        // ------------------------------------------------------------------
        static Color GroupAccent(string g)
        {
            switch (g)
            {
                case "temp": return Theme.Sky;
                case "tools": return Theme.Accent;
                case "apps": return Theme.Pink;
                case "junk": return Theme.Teal;
                case "risk": return Theme.Amber;
                case "win": return Theme.Blue;
            }
            return Theme.Accent;
        }

        static IconKind GroupIcon(string g)
        {
            switch (g)
            {
                case "temp": return IconKind.Clock;
                case "tools": return IconKind.Code;
                case "apps": return IconKind.Apps;
                case "junk": return IconKind.Chip;
                case "risk": return IconKind.Trash;
                case "win": return IconKind.Windows;
            }
            return IconKind.Sparkle;
        }

        long MaxMeasured()
        {
            long max = 0;
            foreach (CleanupOp op in ops) if (op.Measured > max) max = op.Measured;
            return max;
        }

        void BuildGroups()
        {
            groupsPanel.SuspendLayout();
            if (adminBtn.Parent != null) adminBtn.Parent = null;
            List<Control> old = new List<Control>();
            foreach (Control c in groupsPanel.Controls) old.Add(c);
            groupsPanel.Controls.Clear();
            foreach (Control c in old) c.Dispose();
            checks.Clear();
            sizeLabels.Clear();
            gboxes.Clear();

            foreach (string g in groupOrder)
            {
                CategoryCard card = new CategoryCard();
                card.Dock = DockStyle.Top;
                card.Title = groupTitles[g];
                card.Accent = GroupAccent(g);
                card.Icon = GroupIcon(g);

                foreach (CleanupOp op in ops)
                {
                    if (op.Group != g) continue;
                    CleanupOp theOp = op;

                    RowPanel row = new RowPanel();
                    row.Height = Theme.S(42);

                    ModernCheckBox cb = new ModernCheckBox();
                    cb.Dock = DockStyle.Fill;
                    cb.Text = op.Name;
                    cb.Checked = op.DefaultChecked;
                    cb.Enabled = !op.Admin || isAdmin;
                    cb.Locked = op.Admin && !isAdmin;
                    if (op.Hint != null) tip.SetToolTip(cb, op.Hint);
                    else if (cb.Locked) tip.SetToolTip(cb, "Requires administrator rights.");
                    cb.CheckedChanged += delegate { UpdateTotal(); };

                    SizeLabel lbl = new SizeLabel();
                    lbl.Dock = DockStyle.Right;
                    lbl.Width = Theme.S(210);
                    lbl.Text = "…";
                    lbl.Bytes = delegate { return theOp.Measured; };
                    lbl.MaxBytes = new Func<long>(MaxMeasured);
                    lbl.Click += delegate { if (cb.Enabled) cb.Checked = !cb.Checked; };

                    row.Controls.Add(cb);
                    row.Controls.Add(lbl);
                    row.Wire(cb);
                    row.Wire(lbl);
                    card.AddRow(row);

                    checks[op] = cb;
                    sizeLabels[op] = lbl;
                }

                if (g == "win" && !isAdmin) card.HeaderAction = adminBtn;

                groupsPanel.Controls.Add(card);
                card.BringToFront();   // keep the natural top-to-bottom order
                gboxes[g] = card;
            }
            groupsPanel.ResumeLayout(true);
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
            adminPill.Text = isAdmin ? "Administrator" : "Standard user";
            adminPill.Tone = isAdmin ? Theme.Green : Theme.Amber;
            tip.SetToolTip(adminPill, isAdmin
                ? "Running with administrator privileges. Windows-level targets are available."
                : "Windows-level targets are locked. Use Restart as admin in the Windows System card.");
            adminBtn.Visible = !isAdmin;
            LayoutTopBar();
        }

        void RefreshFree()
        {
            gauge.SetDrive(FileSystemService.FreeC(), FileSystemService.TotalC());
        }

        void AppendLog(string text, LogKind kind)
        {
            Color c = Theme.Text;
            if (kind == LogKind.Success) c = Theme.Green;
            else if (kind == LogKind.Warning) c = Theme.Amber;
            else if (kind == LogKind.Error) c = Theme.Red;
            else if (kind == LogKind.Dim) c = Theme.Dim;
            else if (kind == LogKind.Accent) c = Theme.Blend(Theme.Accent, Color.White, 0.35f);
            string stamp = DateTime.Now.ToString("HH:mm:ss") + "   ";
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = Theme.Faint;
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
            int selected = 0;
            foreach (CleanupOp op in ops)
            {
                CheckBox cb;
                if (!checks.TryGetValue(op, out cb)) continue;
                if (!cb.Checked) continue;
                selected++;
                if (op.Measured >= 0) { sum += op.Measured; any = true; }
            }
            gauge.SetRecoverable(any ? sum : 0);
            string text = gboxes.Count + " categories  ·  " + selected + " of " + ops.Count + " targets selected";
            if (any) text += "  ·  " + FileSystemService.FmtBytes(sum) + " selected";
            summaryLabel.Text = text;
            RefreshGroupTitles();
        }

        void RefreshGroupTitles()
        {
            foreach (KeyValuePair<string, CategoryCard> kv in gboxes)
            {
                long sum = 0;
                bool any = false;
                int count = 0;
                int selected = 0;
                foreach (CleanupOp op in ops)
                {
                    if (op.Group != kv.Key) continue;
                    count++;
                    CheckBox cb;
                    if (checks.TryGetValue(op, out cb) && cb.Checked) selected++;
                    if (op.Measured >= 0) { sum += op.Measured; any = true; }
                }
                kv.Value.SetInfo(count, selected, any ? sum : -1);
                kv.Value.InvalidateRows();
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
            recBtn.Enabled = enabled;
            allBtn.Enabled = enabled;
            noneBtn.Enabled = enabled;
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
                {
                    Process.Start("notepad.exe", "\"" + logFile + "\"");
                    return;
                }
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC Cleanup", "Logs");
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        /// <summary>Scrolls the category list with the mouse wheel without needing keyboard focus.</summary>
        sealed class WheelFilter : IMessageFilter
        {
            readonly Panel panel;

            public WheelFilter(Panel p) { panel = p; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A) return false;
                if (!panel.IsHandleCreated || !panel.Visible) return false;
                Form f = panel.FindForm();
                if (f == null || f != Form.ActiveForm) return false;
                if (!panel.RectangleToScreen(panel.ClientRectangle).Contains(Cursor.Position)) return false;
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                int step = Theme.S(72) * delta / 120;
                Point cur = panel.AutoScrollPosition;
                panel.AutoScrollPosition = new Point(-cur.X, -cur.Y - step);
                return true;
            }
        }
    }
}
