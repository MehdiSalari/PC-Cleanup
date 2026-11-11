// MainForm.Operations.cs - cleanup operation definitions
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace PcCleanupApp
{
    public partial class MainForm
    {
        void BuildOps()
        {
            string u = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pdata = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            ops.Clear();
            groupOrder.Clear();
            groupTitles.Clear();
            gboxes.Clear();

            groupTitles["temp"] = "Temporary Files";
            groupTitles["tools"] = "Developer Tool Caches";
            groupTitles["apps"] = "Application & Browser Caches";
            groupTitles["junk"] = "System Cleanup";
            groupTitles["risk"] = "Optional Cleanup";
            groupTitles["win"] = "Windows System (Administrator)";
            groupOrder.Add("temp");
            groupOrder.Add("tools");
            groupOrder.Add("apps");
            groupOrder.Add("junk");
            groupOrder.Add("risk");
            groupOrder.Add("win");

            CleanupOp o;

            string tempDir = Path.Combine(local, "Temp");
            o = new CleanupOp();
            o.Group = "temp";
            o.Name = @"User temporary files (AppData\Local\Temp)";
            o.Hint = "Files older than the selected age are removed. Files locked by running apps are skipped.";
            o.Measure = delegate() { return FileSystemService.StaleSize(tempDir, scanAge); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(tempDir, ctx.AgeDays, null, ctx); };
            ops.Add(o);

            string uvCache = Path.Combine(local, "uv", "cache");
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "uv package cache (Python)";
            o.Measure = delegate() { return FileSystemService.PathSize(uvCache); };
            o.Execute = delegate(OpContext ctx)
            {
                string exe = FileSystemService.FindInPath("uv.exe");
                if (exe != null && !ctx.DryRun)
                {
                    OpResult r = new OpResult();
                    r.BytesFreed = FileSystemService.RunExternalMeasure(ctx, exe, "cache clean");
                    if (r.BytesFreed <= 0) r.Note = "Cache was already empty";
                    return r;
                }
                OpResult r2 = FileSystemService.ClearContents(uvCache, -1, null, ctx);
                if (exe == null && r2.Note == null) r2.Note = "uv was not found in PATH; cache contents removed directly";
                return r2;
            };
            ops.Add(o);

            string pipCache = Path.Combine(local, "pip", "cache");
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "pip package cache (Python)";
            o.Measure = delegate() { return FileSystemService.PathSize(pipCache); };
            o.Execute = delegate(OpContext ctx)
            {
                string exe = FileSystemService.FindInPath("pip.exe");
                if (exe != null && !ctx.DryRun)
                {
                    OpResult r = new OpResult();
                    r.BytesFreed = FileSystemService.RunExternalMeasure(ctx, exe, "cache purge");
                    if (r.BytesFreed <= 0) r.Note = "Cache was already empty";
                    return r;
                }
                OpResult r2 = FileSystemService.ClearContents(pipCache, -1, null, ctx);
                if (exe == null && r2.Note == null) r2.Note = "pip was not found in PATH; cache contents removed directly";
                return r2;
            };
            ops.Add(o);

            string[] npmPaths = new string[] {
                Path.Combine(local, "npm-cache", "_npx"),
                Path.Combine(local, "npm-cache", "_cacache"),
                Path.Combine(local, "npm-cache", "logs"),
                Path.Combine(u, ".npm", "_cacache")
            };
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "npm and npx cache";
            o.Measure = delegate() { return FileSystemService.SumSizes(npmPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(npmPaths, ctx); };
            ops.Add(o);

            string pubCache = Path.Combine(local, "Pub", "Cache");
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "Pub package cache (Flutter / Dart)";
            o.Measure = delegate() { return FileSystemService.PathSize(pubCache); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(pubCache, -1, null, ctx); };
            ops.Add(o);

            string dartDir = Path.Combine(local, ".dartServer");
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "Dart analysis server cache";
            o.Measure = delegate() { return FileSystemService.PathSize(dartDir); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(dartDir, -1, null, ctx); };
            ops.Add(o);

            string[] jsPaths = new string[] {
                Path.Combine(local, "Yarn", "Cache"),
                Path.Combine(local, "pnpm-store"),
                Path.Combine(local, ".bun", "install", "cache")
            };
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "Yarn / pnpm / bun cache";
            o.Measure = delegate() { return FileSystemService.SumSizes(jsPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(jsPaths, ctx); };
            ops.Add(o);

            string[] buildPaths = new string[] {
                Path.Combine(u, ".m2", "repository"),
                Path.Combine(u, ".gradle", "caches", "build-cache-1")
            };
            o = new CleanupOp();
            o.Group = "tools";
            o.Name = "Maven & Gradle caches (.m2 / build-cache)";
            o.DefaultChecked = false;
            o.Hint = "Can contain several GB and may take time to download again. Review before selecting.";
            o.Measure = delegate() { return FileSystemService.SumSizes(buildPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(buildPaths, ctx); };
            ops.Add(o);

            List<string> updaterDirs = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(local))
                    if (Path.GetFileName(d).ToLowerInvariant().Contains("updater")) updaterDirs.Add(d);
            }
            catch { }
            string[] ups = updaterDirs.ToArray();
            o = new CleanupOp();
            o.Group = "apps";
            o.Name = "Electron application updater cache";
            o.Hint = "Downloaded installer files left behind by Squirrel or electron-updater.";
            o.Measure = delegate() { return FileSystemService.SumSizes(ups); };
            o.Execute = delegate(OpContext ctx)
            {
                OpResult r = FileSystemService.ClearMany(ups, ctx);
                if (!r.Skipped && r.Note == null) r.Note = ups.Length + " folders";
                return r;
            };
            ops.Add(o);

            List<string> appDirs = BuildAppCacheDirs(local, roam);
            string[] ad = appDirs.ToArray();
            o = new CleanupOp();
            o.Group = "apps";
            o.Name = "Application & browser caches (Cache / GPUCache, etc.)";
            o.Hint = "Only cache folders are targeted. Cookies, sign-ins, profiles and settings are preserved.";
            o.Measure = delegate() { return FileSystemService.SumSizes(ad); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(ad, ctx); };
            ops.Add(o);

            string[] crashPaths = new string[] {
                Path.Combine(local, "CrashDumps"),
                Path.Combine(local, "Microsoft", "Windows", "WER"),
                Path.Combine(local, "Crashpad"),
                Path.Combine(pdata, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(pdata, "Microsoft", "Windows", "WER", "ReportQueue")
            };
            o = new CleanupOp();
            o.Group = "junk";
            o.Name = "Crash dumps & Windows error reports (WER)";
            o.Measure = delegate() { return FileSystemService.SumSizes(crashPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(crashPaths, ctx); };
            ops.Add(o);

            string explorerDir = Path.Combine(local, "Microsoft", "Windows", "Explorer");
            o = new CleanupOp();
            o.Group = "junk";
            o.Name = "Windows thumbnail cache (thumbcache)";
            o.Measure = delegate() { return FileSystemService.FilteredSize(explorerDir, "thumbcache_*.db"); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(explorerDir, -1, "thumbcache_*.db", ctx); };
            ops.Add(o);

            o = new CleanupOp();
            o.Group = "junk";
            o.Name = "Windows icon cache (iconcache)";
            o.Measure = delegate() { return FileSystemService.FilteredSize(explorerDir, "iconcache_*.db"); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(explorerDir, -1, "iconcache_*.db", ctx); };
            ops.Add(o);

            string[] gpuPaths = new string[] {
                Path.Combine(local, "NVIDIA", "GLCache"),
                Path.Combine(local, "NVIDIA", "DXCache"),
                Path.Combine(local, "NVIDIA", "ComputeCache"),
                Path.Combine(local, "AMD", "DxCache"),
                Path.Combine(local, "AMD", "DxcCache"),
                Path.Combine(local, "Intel", "ShaderCache")
            };
            o = new CleanupOp();
            o.Group = "junk";
            o.Name = "GPU shader caches (NVIDIA / AMD / Intel)";
            o.Hint = "Games may launch more slowly once while their shaders are rebuilt.";
            o.Measure = delegate() { return FileSystemService.SumSizes(gpuPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(gpuPaths, ctx); };
            ops.Add(o);

            string[] miscPaths = new string[] {
                Path.Combine(local, "D3DSCache"),
                Path.Combine(local, "Microsoft", "Windows", "INetCache"),
                Path.Combine(local, "Microsoft", "Windows", "WebCache")
            };
            o = new CleanupOp();
            o.Group = "junk";
            o.Name = "Windows web & Direct3D caches (INetCache / WebCache / D3DSCache)";
            o.Hint = "Some files may be locked by Windows. Successfully removed files are reported.";
            o.Measure = delegate() { return FileSystemService.SumSizes(miscPaths); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearMany(miscPaths, ctx); };
            ops.Add(o);

            o = new CleanupOp();
            o.Group = "risk";
            o.Name = "Recycle Bin (all drives)";
            o.Measure = delegate() { return RecycleBinService.SizeAllFixedDrives(); };
            o.Execute = delegate(OpContext ctx)
            {
                OpResult r = new OpResult();
                long sz = RecycleBinService.SizeAllFixedDrives();
                if (!ctx.DryRun) RecycleBinService.EmptyAll();
                r.BytesFreed = sz > 0 ? sz : 0;
                if (sz < 0) r.Note = "Recycle Bin size could not be read";
                return r;
            };
            ops.Add(o);

            string downloads = Path.Combine(u, "Downloads");
            o = new CleanupOp();
            o.Group = "risk";
            o.Name = "Old Downloads installers (EXE / ZIP, older than 7 days)";
            o.DefaultChecked = false;
            o.Hint = "Only EXE and ZIP files older than 7 days in your Downloads folder are targeted.";
            o.Measure = delegate() { return FileSystemService.StaleFilteredSize(downloads, 7, "*.exe|*.zip"); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(downloads, 7, "*.exe|*.zip", ctx); };
            ops.Add(o);

            string wuDir = Path.Combine(windir, "SoftwareDistribution", "Download");
            o = new CleanupOp();
            o.Group = "win";
            o.Admin = true;
            o.Name = "Windows Update download cache (SoftwareDistribution)";
            o.Hint = "The Windows Update and BITS services are stopped temporarily and restarted afterward.";
            o.Measure = delegate() { return FileSystemService.PathSize(wuDir); };
            o.Execute = delegate(OpContext ctx)
            {
                OpResult r = new OpResult();
                if (ctx.WuBusyReasons != null && ctx.WuBusyReasons.Count > 0 && !ctx.ForceWu)
                {
                    foreach (string why in ctx.WuBusyReasons) ctx.Log("Update protection: " + why, LogKind.Warning);
                    r.Skipped = true;
                    r.Note = "An update is in progress. Clearing this cache would trigger another download.";
                    return r;
                }
                if (ctx.DryRun) return FileSystemService.ClearContents(wuDir, -1, null, ctx);
                StopSvc("bits");
                StopSvc("wuauserv");
                try { r = FileSystemService.ClearContents(wuDir, -1, null, ctx); }
                finally { StartSvc("wuauserv"); StartSvc("bits"); }
                return r;
            };
            ops.Add(o);

            string winTemp = Path.Combine(windir, "Temp");
            o = new CleanupOp();
            o.Group = "win";
            o.Admin = true;
            o.Name = @"Windows temporary files (C:\Windows\Temp)";
            o.Measure = delegate() { return FileSystemService.PathSize(winTemp); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(winTemp, -1, null, ctx); };
            ops.Add(o);

            string dopt = Path.Combine(windir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache");
            o = new CleanupOp();
            o.Group = "win";
            o.Admin = true;
            o.Name = "Delivery Optimization cache";
            o.Measure = delegate() { return FileSystemService.PathSize(dopt); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(dopt, -1, null, ctx); };
            ops.Add(o);

            o = new CleanupOp();
            o.Group = "win";
            o.Admin = true;
            o.Name = "Windows component store cleanup (DISM)";
            o.Hint = "Uses Microsoft's DISM cleanup and may take several minutes. Preview mode does not run it.";
            o.Measure = null;
            o.Execute = delegate(OpContext ctx)
            {
                OpResult r = new OpResult();
                if (ctx.DryRun) { r.Skipped = true; r.Note = "Available during a live cleanup only"; return r; }
                long before = FileSystemService.FreeC();
                ctx.Log("DISM is running. This may take several minutes…", LogKind.Dim);
                try
                {
                    string args = "/Online /Cleanup-Image /StartComponentCleanup";
                    if (ctx.ResetBase) args = args + " /ResetBase";
                    ProcessStartInfo psi = new ProcessStartInfo("dism.exe", args);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    Process p = Process.Start(psi);
                    DataReceivedEventHandler progressHandler = delegate(object sender, DataReceivedEventArgs ev)
                    {
                        if (string.IsNullOrEmpty(ev.Data)) return;
                        Match m = Regex.Match(ev.Data, @"(\d{1,3}(?:\.\d+)?)\s*%");
                        double value;
                        if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                        {
                            if (ctx.ReportFraction != null) ctx.ReportFraction(Math.Max(0, Math.Min(100, value)) / 100.0);
                        }
                    };
                    p.OutputDataReceived += progressHandler;
                    p.ErrorDataReceived += progressHandler;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    while (!p.HasExited)
                    {
                        if (ctx.Pulse != null) ctx.Pulse();
                        Thread.Sleep(1000);
                    }
                    p.WaitForExit();
                    if (p.ExitCode != 0) r.Note = "DISM exited with code " + p.ExitCode;
                }
                catch (Exception ex)
                {
                    r.Note = "Error: " + ex.Message;
                    return r;
                }
                long delta = FileSystemService.FreeC() - before;
                r.BytesFreed = delta > 0 ? delta : 0;
                if (ctx.ResetBase) r.Note = "Executed with /ResetBase";
                return r;
            };
            ops.Add(o);

            string winOld = @"C:\Windows.old";
            o = new CleanupOp();
            o.Group = "win";
            o.Admin = true;
            o.Name = "Previous Windows installation (Windows.old, older than one day)";
            o.Measure = delegate() { return FileSystemService.PathSize(winOld); };
            o.Execute = delegate(OpContext ctx) { return FileSystemService.ClearContents(winOld, 1, null, ctx); };
            ops.Add(o);
        }

        List<string> BuildAppCacheDirs(string local, string roam)
        {
            List<string> list = new List<string>();
            string[] subs = new string[] {
                "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache",
                "CachedData", "CachedExtensionVSIXs", "logs", "Crashpad",
                Path.Combine("Service Worker", "CacheStorage"),
                Path.Combine("Service Worker", "ScriptCache")
            };
            foreach (string root in new string[] { roam, local })
            {
                string[] apps = null;
                try { apps = Directory.GetDirectories(root); } catch { continue; }
                foreach (string app in apps)
                {
                    foreach (string sub in subs)
                    {
                        string t = Path.Combine(app, sub);
                        if (Directory.Exists(t)) list.Add(t);
                    }
                }
            }
            string[] browserRoots = new string[] {
                Path.Combine(local, "Microsoft", "Edge", "User Data"),
                Path.Combine(local, "Google", "Chrome", "User Data"),
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")
            };
            string[] bsubs = new string[] { "Cache", "Code Cache", "GPUCache", Path.Combine("Service Worker", "CacheStorage") };
            foreach (string br in browserRoots)
            {
                if (!Directory.Exists(br)) continue;
                string[] profiles = null;
                try { profiles = Directory.GetDirectories(br); } catch { continue; }
                foreach (string pr in profiles)
                {
                    foreach (string sub in bsubs)
                    {
                        string t = Path.Combine(pr, sub);
                        if (Directory.Exists(t)) list.Add(t);
                    }
                }
            }
            return list;
        }

        void StopSvc(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }

        void StartSvc(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    }
                }
            }
            catch { }
        }

        List<string> WuBusyReasons()
        {
            List<string> reasons = new List<string>();
            try
            {
                string query = "*[System[Provider[@Name='Microsoft-Windows-WindowsUpdateClient'] and TimeCreated[timediff(@SystemTime) <= 3600000]]]";
                System.Diagnostics.Eventing.Reader.EventLogQuery q = new System.Diagnostics.Eventing.Reader.EventLogQuery("System", System.Diagnostics.Eventing.Reader.PathType.LogName, query);
                using (System.Diagnostics.Eventing.Reader.EventLogReader reader = new System.Diagnostics.Eventing.Reader.EventLogReader(q))
                {
                    int[] ids = new int[] { 19, 20, 21, 43, 44, 217 };
                    System.Diagnostics.Eventing.Reader.EventRecord rec = reader.ReadEvent();
                    while (rec != null)
                    {
                        if (Array.IndexOf(ids, rec.Id) >= 0)
                        {
                            reasons.Add("Windows Update activity was detected in the last hour (event " + rec.Id + ")");
                            rec.Dispose();
                            break;
                        }
                        rec.Dispose();
                        rec = reader.ReadEvent();
                    }
                }
            }
            catch { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    if (k != null) reasons.Add("A restart is required to complete an update");
            }
            catch { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    if (k != null) reasons.Add("Component servicing is waiting for a restart");
            }
            catch { }
            return reasons;
        }
    }
}



