// MainForm.Workers.cs - background scanning and cleanup execution
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public partial class MainForm
    {
        sealed class ScanItem
        {
            public CleanupOp Op;
            public long Bytes;
        }

        sealed class RunProgress
        {
            public int Percent;
            public string Message;
            public LogKind Kind;
            public bool HasLog;
        }

        void ScanStart()
        {
            if (scanWorker.IsBusy || runWorker.IsBusy) return;
            scanAge = (int)ageUpDown.Value;
            rescanBtn.Enabled = false;
            startBtn.Enabled = false;
            currentLabel.Text = "SCANNING CLEANUP TARGETS…";
            bar.Value = 0;
            scanWorker.RunWorkerAsync();
        }

        void ScanDoWork(object sender, DoWorkEventArgs e)
        {
            List<ScanItem> result = new List<ScanItem>();
            int count = ops.Count;
            for (int i = 0; i < count; i++)
            {
                CleanupOp op = ops[i];
                long bytes = -1;
                try { if (op.Measure != null) bytes = op.Measure(); }
                catch { bytes = 0; }
                result.Add(new ScanItem { Op = op, Bytes = bytes });
                int pct = count == 0 ? 100 : (int)Math.Round((i + 1) * 100.0 / count);
                scanWorker.ReportProgress(pct, op.Name);
            }
            e.Result = result;
        }

        void ScanProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            bar.Value = e.ProgressPercentage;
            currentLabel.Text = "SCANNING  ·  " + Convert.ToString(e.UserState);
        }

        void ScanCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            rescanBtn.Enabled = true;
            if (e.Error != null)
            {
                AppendLog("Scan failed: " + e.Error.Message, LogKind.Error);
            }
            else if (!e.Cancelled && e.Result is List<ScanItem>)
            {
                foreach (ScanItem item in (List<ScanItem>)e.Result)
                {
                    item.Op.Measured = item.Bytes;
                    Label lbl;
                    if (sizeLabels.TryGetValue(item.Op, out lbl))
                        lbl.Text = item.Bytes < 0 ? "—" : FileSystemService.FmtBytes(item.Bytes);
                }
                RefreshGroupTitles();
                UpdateTotal();
                AppendLog("Target scan completed.", LogKind.Success);
            }
            bar.Value = 0;
            currentLabel.Text = "READY";
            startBtn.Enabled = true;
            RefreshFree();
        }

        void StartRun()
        {
            if (runWorker.IsBusy || scanWorker.IsBusy) return;
            List<CleanupOp> selected = new List<CleanupOp>();
            foreach (CleanupOp op in ops)
            {
                CheckBox cb;
                if (checks.TryGetValue(op, out cb) && cb.Checked && cb.Enabled)
                    selected.Add(op);
            }
            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one cleanup target.", "PC Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool dry = dryCheck.Checked;
            if (!dry)
            {
                string warning = "The selected cleanup operations will run and their target files may be deleted. Continue?";
                if (resetCheck.Checked)
                    warning += Environment.NewLine + Environment.NewLine + "IMPORTANT: /ResetBase permanently removes the ability to uninstall installed updates.";
                if (forceCheck.Checked)
                    warning += Environment.NewLine + Environment.NewLine + "Windows Update protection will be overridden. An active update may need to be downloaded again.";
                if (MessageBox.Show(warning, "Confirm cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
            }

            RunOptions options = new RunOptions();
            options.Dry = dry;
            options.AgeDays = (int)ageUpDown.Value;
            options.ResetBase = resetCheck.Checked;
            options.ForceWu = forceCheck.Checked;
            options.Selected = selected;

            totalOps = selected.Count;
            fraction = 0;
            freedLabel.Text = "";
            bar.Value = 0;
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC Cleanup", "Logs");
            logFile = Path.Combine(logDir, "cleanup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            try
            {
                Directory.CreateDirectory(logDir);
                File.WriteAllText(logFile, "PC Cleanup - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
            }
            catch { logFile = null; }

            SetOpsEnabled(false);
            startBtn.Enabled = false;
            cancelBtn.Enabled = true;
            rescanBtn.Enabled = false;
            AppendLog(dry ? "Preview started. No files will be deleted." : "Live cleanup started.", LogKind.Accent);
            AppendLog("Selected targets: " + selected.Count, LogKind.Dim);
            runWorker.RunWorkerAsync(options);
        }

        void RunDoWork(object sender, DoWorkEventArgs e)
        {
            RunOptions options = (RunOptions)e.Argument;
            RunSummary summary = new RunSummary();
            summary.Dry = options.Dry;
            summary.FreeBefore = FileSystemService.FreeC();
            List<string> wuReasons = WuBusyReasons();
            bool lowDisk = summary.FreeBefore < 20L * 1024L * 1024L * 1024L;
            int count = options.Selected.Count;
            long estimated = 0;
            foreach (CleanupOp op in options.Selected) if (op.Measured > 0) estimated += op.Measured;

            OpContext ctx = new OpContext();
            ctx.DryRun = options.Dry;
            ctx.AgeDays = options.AgeDays;
            ctx.ResetBase = options.ResetBase;
            ctx.ForceWu = options.ForceWu;
            ctx.IsAdmin = isAdmin;
            ctx.WuBusyReasons = wuReasons;
            ctx.CancelRequested = delegate { return runWorker.CancellationPending; };
            ctx.Pulse = delegate { if (runWorker.CancellationPending) summary.Cancelled = true; };
            ctx.Log = delegate(string message, LogKind kind)
            {
                runWorker.ReportProgress(Math.Min(99, (int)fraction), new RunProgress { Percent = Math.Min(99, (int)fraction), Message = message, Kind = kind, HasLog = true });
            };

            long totalFreed = 0;
            for (int i = 0; i < count; i++)
            {
                if (runWorker.CancellationPending) { summary.Cancelled = true; break; }
                CleanupOp op = options.Selected[i];
                int index = i;
                ctx.ReportFraction = delegate(double local)
                {
                    double f = (index + Math.Max(0, Math.Min(1, local))) / count;
                    fraction = f * 100.0;
                    runWorker.ReportProgress(Math.Min(99, (int)fraction), new RunProgress { Percent = (int)fraction, Message = op.Name, Kind = LogKind.Dim, HasLog = false });
                };

                runWorker.ReportProgress((int)(i * 100.0 / count), new RunProgress { Percent = (int)(i * 100.0 / count), Message = "Running: " + op.Name, Kind = LogKind.Accent, HasLog = true });
                OpResult result = null;
                try
                {
                    if (op.Admin && !isAdmin)
                    {
                        result = new OpResult { Skipped = true, Note = "Administrator privileges required" };
                    }
                    else if (op.Admin && lowDisk && !options.ForceWu)
                    {
                        result = new OpResult { Skipped = true, Note = "Less than 20 GB is free on C:. Windows Update is protected." };
                    }
                    else
                    {
                        result = op.Execute(ctx);
                    }
                }
                catch (Exception ex)
                {
                    result = new OpResult { Note = "Error: " + ex.Message };
                }
                if (result == null) result = new OpResult { Note = "No result returned" };
                totalFreed += result.BytesFreed;
                string status = result.Skipped ? "Skipped" : (result.Note != null && result.Note.StartsWith("Error:") ? "Error" : (options.Dry ? "Can be freed" : "Completed"));
                string detail = op.Name + " — " + status + " · " + FileSystemService.FmtBytes(result.BytesFreed) + (result.Items > 0 ? " · " + result.Items + " items" : "");
                if (!string.IsNullOrEmpty(result.Note)) detail += " · " + result.Note;
                LogKind kind = result.Skipped ? LogKind.Warning : (status == "Error" ? LogKind.Error : LogKind.Success);
                runWorker.ReportProgress(Math.Min(99, (int)(((i + 1) * 100.0) / count)), new RunProgress { Percent = (int)(((i + 1) * 100.0) / count), Message = detail, Kind = kind, HasLog = true });
            }

            summary.TotalFreed = totalFreed;
            summary.FreeAfter = FileSystemService.FreeC();
            summary.Actual = summary.FreeAfter - summary.FreeBefore;
            summary.Cancelled = summary.Cancelled || runWorker.CancellationPending;
            e.Result = summary;
        }

        void RunProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            RunProgress p = e.UserState as RunProgress;
            if (p == null) return;
            bar.Value = p.Percent;
            if (!p.HasLog) currentLabel.Text = p.Message;
            else
            {
                AppendLog(p.Message, p.Kind);
                currentLabel.Text = p.Message;
                if (logFile != null)
                {
                    try { File.AppendAllText(logFile, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + p.Message + Environment.NewLine); }
                    catch { }
                }
            }
        }

        void RunCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            cancelBtn.Enabled = false;
            startBtn.Enabled = true;
            rescanBtn.Enabled = true;
            SetOpsEnabled(true);
            if (e.Error != null)
            {
                AppendLog("Cleanup stopped due to an error: " + e.Error.Message, LogKind.Error);
                currentLabel.Text = "ERROR";
            }
            else if (e.Result is RunSummary)
            {
                RunSummary s = (RunSummary)e.Result;
                bar.Value = s.Cancelled ? bar.Value : 100;
                string amount = s.Dry ? FileSystemService.FmtBytes(s.TotalFreed) : FileSystemService.FmtBytes(Math.Max(0, s.Actual));
                freedLabel.Text = (s.Dry ? "ESTIMATED RECOVERY: " : "SPACE RECLAIMED: ") + amount;
                currentLabel.Text = s.Cancelled ? "OPERATION STOPPED" : "OPERATION COMPLETE";
                string completion = s.Cancelled
                    ? "Operation was cancelled by the user."
                    : (s.Dry ? "Preview completed successfully. No files were deleted." : "Live cleanup completed successfully.");
                AppendLog(completion, s.Cancelled ? LogKind.Warning : LogKind.Success);
                AppendLog("Free space on C: " + FileSystemService.FmtBytes(s.FreeAfter), LogKind.Info);
                if (logFile != null) AppendLog("Log file: " + logFile, LogKind.Dim);
            }
            else
            {
                currentLabel.Text = "OPERATION COMPLETE";
            }
            RefreshFree();
        }
    }
}

