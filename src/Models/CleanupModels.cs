using System;
using System.Collections.Generic;

namespace PcCleanupApp
{
    public enum LogKind { Info, Success, Warning, Error, Dim, Accent }

    public class LogMsg
    {
        public string Text;
        public LogKind Kind;
        public LogMsg(string text, LogKind kind) { Text = text; Kind = kind; }
    }

    public class OpContext
    {
        public bool DryRun;
        public int AgeDays;
        public bool ResetBase;
        public bool ForceWu;
        public bool IsAdmin;
        public List<string> WuBusyReasons;
        public Action<double> ReportFraction;
        public Action Pulse;
        public Action<string, LogKind> Log;
        public Func<bool> CancelRequested;
    }

    public class OpResult
    {
        public long BytesFreed;
        public int Items;
        public bool Skipped;
        public string Note;
    }

    public class RunSummary
    {
        public long TotalFreed;
        public long Actual;
        public bool Dry;
        public bool Cancelled;
        public long FreeBefore;
        public long FreeAfter;
    }

    public class RunOptions
    {
        public bool Dry;
        public int AgeDays;
        public bool ResetBase;
        public bool ForceWu;
        public List<CleanupOp> Selected;
    }

    public class CleanupOp
    {
        public string Group;
        public string Name;
        public bool Admin;
        public bool DefaultChecked = true;
        public string Hint;
        public Func<OpContext, OpResult> Execute;
        public Func<long> Measure;
        public long Measured = -1;
    }
}

