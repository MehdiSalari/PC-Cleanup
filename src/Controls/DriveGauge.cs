// DriveGauge.cs - animated ring gauge showing drive usage and the space a cleanup can recover
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public class DriveGauge : Control
    {
        long free;
        long totalBytes;
        long recoverable;
        string driveLabel = "DRIVE C:";
        Tween usedT;
        Tween recT;

        public DriveGauge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            usedT = new Tween(delegate { Invalidate(); });
            usedT.Speed = 0.12f;
            recT = new Tween(delegate { Invalidate(); });
            recT.Speed = 0.14f;
        }

        public string DriveLabel { get { return driveLabel; } set { driveLabel = value; Invalidate(); } }

        public void SetDrive(long freeBytes, long total)
        {
            free = freeBytes;
            totalBytes = total;
            usedT.To(total > 0 ? (float)(1.0 - (double)freeBytes / (double)total) : 0f);
            UpdateRec();
            Invalidate();
        }

        public void SetRecoverable(long bytes)
        {
            recoverable = Math.Max(0, bytes);
            UpdateRec();
        }

        void UpdateRec()
        {
            float f = 0f;
            if (totalBytes > 0) f = (float)Math.Min(1.0, (double)recoverable / (double)totalBytes);
            recT.To(f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                usedT.Dispose();
                recT.Dispose();
            }
            base.Dispose(disposing);
        }

        static void Split(long b, out string num, out string unit)
        {
            double abs = Math.Abs((double)b);
            if (abs >= 1073741824.0) { num = (b / 1073741824.0).ToString("0.0"); unit = "GB"; }
            else if (abs >= 1048576.0) { num = (b / 1048576.0).ToString("0.0"); unit = "MB"; }
            else if (abs >= 1024.0) { num = (b / 1024.0).ToString("0.0"); unit = "KB"; }
            else { num = b.ToString(); unit = "B"; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);

            float pad = Theme.Sf(2f);
            float capH = Theme.Sf(18f);
            float legendH = Theme.Sf(22f);

            Theme.DrawText(g, driveLabel, Theme.Bold(8.25f), Theme.Faint, new RectangleF(pad, 0f, Width / 2f, capH), StringAlignment.Near, StringAlignment.Center);
            if (totalBytes > 0)
                Theme.DrawText(g, FileSystemService.FmtBytes(totalBytes) + " total", Theme.Ui(8.5f), Theme.Dim, new RectangleF(Width / 2f, 0f, Width / 2f - pad, capH), StringAlignment.Far, StringAlignment.Center);

            RectangleF area = new RectangleF(0f, capH + Theme.Sf(4f), Width, Height - capH - legendH - Theme.Sf(8f));
            float d = Math.Min(area.Width - Theme.Sf(8f), area.Height);
            float cx = area.X + area.Width / 2f;
            float cy = area.Y + area.Height / 2f;

            if (d >= Theme.Sf(70f))
            {
                float pw = Math.Max(Theme.Sf(9f), d * 0.085f);
                float inset = pw / 2f + Theme.Sf(3f);
                RectangleF ring = new RectangleF(cx - d / 2f + inset, cy - d / 2f + inset, d - inset * 2f, d - inset * 2f);

                using (Pen track = new Pen(Theme.A(Theme.Border, 230), pw))
                    g.DrawEllipse(track, ring);

                float used = Math.Max(0f, Math.Min(1f, usedT.Value));
                float sweep = 360f * used;
                if (sweep > 0.6f)
                {
                    Color c1 = Theme.Accent, c2 = Theme.Sky;
                    if (used >= 0.9f) { c1 = Theme.Red; c2 = Theme.Orange; }
                    else if (used >= 0.8f) { c1 = Theme.Orange; c2 = Theme.Amber; }

                    using (Pen glow = new Pen(Theme.A(c1, 34), pw + Theme.Sf(7f)))
                    {
                        glow.StartCap = LineCap.Round;
                        glow.EndCap = LineCap.Round;
                        g.DrawArc(glow, ring, -90f, sweep);
                    }
                    using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(ring.X - 1f, ring.Y - 1f, ring.Width + 2f, ring.Height + 2f), c1, c2, 45f))
                    using (Pen arc = new Pen(lb, pw))
                    {
                        arc.StartCap = LineCap.Round;
                        arc.EndCap = LineCap.Round;
                        g.DrawArc(arc, ring, -90f, sweep);
                    }

                    float recSweep = Math.Min(sweep, 360f * recT.Value);
                    if (recSweep > 0.8f)
                    {
                        using (Pen rp = new Pen(Theme.Green, pw * 0.62f))
                        {
                            rp.StartCap = LineCap.Round;
                            rp.EndCap = LineCap.Round;
                            g.DrawArc(rp, ring, -90f + sweep - recSweep, recSweep);
                        }
                    }
                }

                // centre text
                string num, unit;
                if (totalBytes > 0) Split(free, out num, out unit);
                else { num = "—"; unit = ""; }
                float pt = (float)Math.Round(Math.Max(15f, Math.Min(28f, d / Theme.Scale * 0.17f)));
                Font big = Theme.Semi(pt);
                Font small = Theme.Ui(8.75f);
                float bigH = big.GetHeight(g);
                float smallH = small.GetHeight(g);
                float top = cy - (bigH + smallH) / 2f;
                float innerW = ring.Width - pw * 1.4f;
                Theme.DrawText(g, num, big, Theme.Text, new RectangleF(cx - innerW / 2f, top, innerW, bigH), StringAlignment.Center, StringAlignment.Center);
                Theme.DrawText(g, unit.Length > 0 ? unit + " free" : "no data", small, Theme.Dim, new RectangleF(cx - innerW / 2f, top + bigH - Theme.Sf(2f), innerW, smallH), StringAlignment.Center, StringAlignment.Center);
            }

            // legend
            if (totalBytes > 0)
            {
                string usedText = "Used " + (usedT.Target * 100f).ToString("0.0") + "%";
                string recText = recoverable > 0 ? "Cleanable " + FileSystemService.FmtBytes(recoverable) : "Cleanable —";
                Font lf = Theme.Ui(8.75f);
                float d1 = Theme.Sf(8f);
                float w1 = Theme.TextWidth(g, usedText, lf) + d1 + Theme.Sf(7f);
                float w2 = Theme.TextWidth(g, recText, lf) + d1 + Theme.Sf(7f);
                float gap = Theme.Sf(16f);
                float x = (Width - (w1 + gap + w2)) / 2f;
                float y = Height - legendH;
                if (x < 0f) x = 0f;
                using (SolidBrush b1 = new SolidBrush(Theme.Accent)) g.FillEllipse(b1, x, y + (legendH - d1) / 2f, d1, d1);
                Theme.DrawText(g, usedText, lf, Theme.Dim, new RectangleF(x + d1 + Theme.Sf(7f), y, w1, legendH), StringAlignment.Near, StringAlignment.Center);
                x += w1 + gap;
                using (SolidBrush b2 = new SolidBrush(recoverable > 0 ? Theme.Green : Theme.Faint)) g.FillEllipse(b2, x, y + (legendH - d1) / 2f, d1, d1);
                Theme.DrawText(g, recText, lf, recoverable > 0 ? Theme.Text : Theme.Faint, new RectangleF(x + d1 + Theme.Sf(7f), y, w2, legendH), StringAlignment.Near, StringAlignment.Center);
            }
        }
    }
}
