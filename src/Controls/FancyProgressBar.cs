// FancyProgressBar.cs - custom painted progress bar with centered percentage
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public class FancyProgressBar : Control
    {
        double val;
        public double Value
        {
            get { return val; }
            set { val = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        public FancyProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(35, 35, 46);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = ClientRectangle;
            if (r.Width < 2 || r.Height < 2) return;
            int rad = r.Height / 2 - 1;
            using (GraphicsPath bg = RoundRect(r, rad))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(35, 35, 46)))
                g.FillPath(bb, bg);
            int w = (int)(r.Width * val / 100.0);
            if (w > 0)
            {
                Rectangle fill = new Rectangle(r.X, r.Y, Math.Min(r.Width, Math.Max(w, r.Height)), r.Height);
                using (GraphicsPath fp = RoundRect(fill, rad))
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Color.FromArgb(36, 120, 200), Color.FromArgb(64, 190, 150), LinearGradientMode.Horizontal))
                    g.FillPath(lb, fp);
            }
            string txt = ((int)val).ToString() + "%";
            using (Font f = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                SizeF sz = g.MeasureString(txt, f);
                g.DrawString(txt, f, tb, (r.Width - sz.Width) / 2, (r.Height - sz.Height) / 2);
            }
        }
        static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 1 || r.Width < rad * 2 || r.Height < rad * 2) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2 - 1, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2 - 1, r.Bottom - rad * 2 - 1, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2 - 1, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}

