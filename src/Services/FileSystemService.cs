// FileSystemService.cs - filesystem helpers and deletion engine
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PcCleanupApp
{
    public static class FileSystemService
    {
        public static long FreeC()
        {
            try { DriveInfo d = new DriveInfo("C"); if (d.IsReady) return d.AvailableFreeSpace; } catch { }
            return 0;
        }

        public static long TotalC()
        {
            try { DriveInfo d = new DriveInfo("C"); if (d.IsReady) return d.TotalSize; } catch { }
            return 0;
        }

        public static string FmtBytes(long b)
        {
            double abs = Math.Abs((double)b);
            if (abs >= 1073741824.0) return string.Format("{0:N2} GB", b / 1073741824.0);
            if (abs >= 1048576.0) return string.Format("{0:N1} MB", b / 1048576.0);
            if (abs >= 1024.0) return string.Format("{0:N1} KB", b / 1024.0);
            return b + " B";
        }

        public static bool IsReparse(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
        }

        public static List<string> SafeChildren(string dir)
        {
            List<string> list = new List<string>();
            try
            {
                string[] arr = Directory.GetFileSystemEntries(dir);
                foreach (string e in arr) if (!IsReparse(e)) list.Add(e);
            }
            catch { }
            return list;
        }

        public static long DirSize(string path)
        {
            long total = 0;
            Stack<string> stack = new Stack<string>();
            stack.Push(path);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] entries = null;
                try { entries = Directory.GetFileSystemEntries(dir); } catch { continue; }
                foreach (string e in entries)
                {
                    try
                    {
                        FileAttributes attr = File.GetAttributes(e);
                        if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attr & FileAttributes.Directory) != 0) stack.Push(e);
                        else total += new FileInfo(e).Length;
                    }
                    catch { }
                }
            }
            return total;
        }

        public static long PathSize(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                if ((attr & FileAttributes.Directory) != 0) return DirSize(path);
                return new FileInfo(path).Length;
            }
            catch { return 0; }
        }

        // Size of stale children only (like the PS script: children newer than minAgeDays are skipped).
        public static long StaleSize(string dir, int minAgeDays)
        {
            if (!Directory.Exists(dir)) return 0;
            if (minAgeDays < 0) return DirSize(dir);
            DateTime cutoff = DateTime.Now.AddDays(-minAgeDays);
            long total = 0;
            foreach (string kid in SafeChildren(dir))
            {
                try { if (Directory.GetLastWriteTime(kid) < cutoff) total += PathSize(kid); }
                catch { }
            }
            return total;
        }

        public static long SumSizes(string[] paths)
        {
            long s = 0;
            foreach (string p in paths)
            {
                try { if (Directory.Exists(p) || File.Exists(p)) s += PathSize(p); } catch { }
            }
            return s;
        }

        public static long FilteredSize(string dir, string filters)
        {
            long t = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                    if (Like(Path.GetFileName(f), filters)) t += new FileInfo(f).Length;
            }
            catch { }
            return t;
        }

        public static long StaleFilteredSize(string dir, int minAgeDays, string filters)
        {
            if (!Directory.Exists(dir)) return 0;
            DateTime cutoff = DateTime.Now.AddDays(-minAgeDays);
            long t = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(f);
                        if (fi.LastWriteTime < cutoff && Like(fi.Name, filters)) t += fi.Length;
                    }
                    catch { }
                }
            }
            catch { }
            return t;
        }

        public static bool Like(string name, string filters)
        {
            if (filters == null || filters == "*") return true;
            string[] parts = filters.Split('|');
            foreach (string p in parts)
            {
                string pat = "^" + Regex.Escape(p.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                if (Regex.IsMatch(name, pat, RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        // Deletes the CONTENTS of a folder (keeps the folder itself). In DryRun it only measures.
        // Locked files are skipped and reported instead of aborting.
        public static OpResult ClearContents(string dir, int minAgeDays, string filters, OpContext ctx)
        {
            OpResult res = new OpResult();
            if (!Directory.Exists(dir)) { res.Skipped = true; res.Note = "Directory not found"; return res; }
            List<string> kids = SafeChildren(dir);
            if (kids.Count == 0) return res;
            DateTime cutoff = minAgeDays >= 0 ? DateTime.Now.AddDays(-minAgeDays) : DateTime.MinValue;
            bool filterOn = filters != null && filters != "*";
            int i = 0;
            bool lockedSeen = false;
            foreach (string kid in kids)
            {
                if (ctx.CancelRequested != null && ctx.CancelRequested()) { res.Note = "Cancelled"; return res; }
                i++;
                if (ctx.ReportFraction != null) ctx.ReportFraction((double)i / kids.Count);
                bool isDir = false;
                DateTime lastWrite = DateTime.MinValue;
                try
                {
                    FileAttributes attr = File.GetAttributes(kid);
                    isDir = (attr & FileAttributes.Directory) != 0;
                    lastWrite = Directory.GetLastWriteTime(kid);
                }
                catch { continue; }
                if (minAgeDays >= 0 && lastWrite >= cutoff) continue;
                if (filterOn)
                {
                    if (isDir) continue;
                    if (!Like(Path.GetFileName(kid), filters)) continue;
                }
                long before = PathSize(kid);
                if (ctx.DryRun) { res.BytesFreed += before; res.Items++; continue; }
                try
                {
                    if (isDir) Directory.Delete(kid, true);
                    else File.Delete(kid);
                    res.BytesFreed += before;
                    res.Items++;
                }
                catch
                {
                    long after = 0;
                    try
                    {
                        bool stillDir = Directory.Exists(kid);
                        bool stillFile = File.Exists(kid);
                        if (stillDir) after = DirSize(kid);
                        else if (stillFile) after = new FileInfo(kid).Length;
                    }
                    catch { }
                    long gained = before - after;
                    if (gained < 0) gained = 0;
                    res.BytesFreed += gained;
                    if (gained > 0) res.Items++;
                    if (after > 0) lockedSeen = true;
                }
            }
            if (lockedSeen) res.Note = "Some files were locked and have been skipped";
            return res;
        }

        // Clears contents of many folders, spreading the progress bar across them.
        public static OpResult ClearMany(string[] dirs, OpContext ctx)
        {
            OpResult total = new OpResult();
            List<string> existing = new List<string>();
            foreach (string d in dirs) if (Directory.Exists(d)) existing.Add(d);
            if (existing.Count == 0) { total.Skipped = true; total.Note = "No matching paths found"; return total; }
            int n = existing.Count;
            int i = 0;
            foreach (string d in existing)
            {
                if (ctx.CancelRequested != null && ctx.CancelRequested()) { total.Note = "Cancelled"; return total; }
                i++;
                if (ctx.ReportFraction != null) ctx.ReportFraction((double)i / n);
                OpResult r = ClearContents(d, -1, null, ctx);
                total.BytesFreed += r.BytesFreed;
                total.Items += r.Items;
                if (r.Note != null && r.Note.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0) total.Note = r.Note;
            }
            return total;
        }

        public static string FindInPath(string exe)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            List<string> dirs = new List<string>();
            if (pathEnv != null)
                foreach (string d in pathEnv.Split(';'))
                    if (d != null && d.Trim().Length > 0) dirs.Add(d.Trim());
            dirs.Add(Path.Combine(profile, ".local", "bin"));
            foreach (string d in dirs)
            {
                try { string full = Path.Combine(d, exe); if (File.Exists(full)) return full; } catch { }
            }
            return null;
        }

        public static long RunExternalMeasure(OpContext ctx, string exe, string args)
        {
            if (ctx.DryRun) return 0;
            long before = FreeC();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process p = Process.Start(psi);
                p.WaitForExit();
            }
            catch { return 0; }
            long delta = FreeC() - before;
            return delta > 0 ? delta : 0;
        }
    }
}



