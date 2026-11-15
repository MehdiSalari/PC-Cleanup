// FancyProgressBar.cs - smooth, gradient progress bar with shimmer and centered percentage
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public class FancyProgressBar : Control
    {
        double val;
        Tween shown;
        Timer shimmer;
        int phase;

        public FancyProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Height = Theme.S(22);
            shown = new Tween(delegate { Invalidate(); });
            shown.Speed = 0.18f;
            shimmer = new Timer();
            shimmer.Interval = 30;
            shimmer.Tick += delegate
            {
                phase = (phase + 5) % 200;
                Invalidate();
            };
        }

        public double Value
        {
            get { return val; }
            set
            {
                val = Math.Max(0, Math.Min(100, value));
                if (val <= 0.0) shown.Set(0f);
                else shown.To((float)val);
                bool active = val > 0.0 && val < 100.0;
                if (active && !shimmer.Enabled) shimmer.Start();
                else if (!active && shimmer.Enabled) shimmer.Stop();
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                shown.Dispose();
                shimmer.Stop();
                shimmer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            RectangleF r = new RectangleF(0f, 0f, Width, Height);
            if (r.Width < 4f || r.Height < 4f) return;
            float rad = r.Height / 2f;

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(10, 15, 28))) Theme.FillRound(g, bg, r, rad);
            using (Pen edge = new Pen(Theme.A(Theme.Border, 200), 1f))
                Theme.DrawRound(g, edge, new RectangleF(0.5f, 0.5f, r.Width - 1f, r.Height - 1f), rad);

            float pct = Math.Max(0f, Math.Min(100f, shown.Value));
            float w = r.Width * pct / 100f;
            if (w > 0.5f)
            {
                RectangleF fill = new RectangleF(r.X, r.Y, Math.Min(r.Width, Math.Max(w, r.Height)), r.Height);
                using (GraphicsPath fp = Theme.Round(fill, rad))
                {
                    using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0f, 0f, Math.Max(2f, r.Width), r.Height), Theme.Accent, Theme.Green, LinearGradientMode.Horizontal))
                    {
                        ColorBlend cb = new ColorBlend(3);
                        cb.Colors = new Color[] { Theme.Accent, Theme.Sky, Theme.Green };
                        cb.Positions = new float[] { 0f, 0.55f, 1f };
                        lb.InterpolationColors = cb;
                        g.FillPath(lb, fp);
                    }

                    if (val > 0.0 && val < 100.0)
                    {
                        GraphicsState st = g.Save();
                        g.SetClip(fp, CombineMode.Intersect);
                        float band = Theme.Sf(90f);
                        float x = fill.X - band + (fill.Width + band * 2f) * (phase / 200f);
                        using (LinearGradientBrush sh = new LinearGradientBrush(new RectangleF(x, 0f, band, r.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Horizontal))
                        {
                            ColorBlend sb = new ColorBlend(3);
                            sb.Colors = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                            sb.Positions = new float[] { 0f, 0.5f, 1f };
                            sh.InterpolationColors = sb;
                            g.FillRectangle(sh, x, 0f, band, r.Height);
                        }
                        g.Restore(st);
                    }
                }
            }

            string txt = ((int)Math.Round(pct)).ToString() + "%";
            Theme.DrawText(g, txt, Theme.Bold(8.5f), pct > 52f ? Color.White : Theme.Dim, r, StringAlignment.Center, StringAlignment.Center);
        }
    }
}
