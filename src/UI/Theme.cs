// Theme.cs - design tokens, DPI scaling, fonts, drawing helpers, animation and native glue
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public static class Theme
    {
        // ---- palette -------------------------------------------------------
        public static readonly Color Bg = Color.FromArgb(9, 13, 24);
        public static readonly Color Sidebar = Color.FromArgb(12, 17, 31);
        public static readonly Color Surface = Color.FromArgb(17, 24, 40);
        public static readonly Color SurfaceHi = Color.FromArgb(25, 34, 56);
        public static readonly Color RowHot = Color.FromArgb(21, 30, 50);
        public static readonly Color Border = Color.FromArgb(34, 45, 72);
        public static readonly Color Text = Color.FromArgb(234, 240, 250);
        public static readonly Color Dim = Color.FromArgb(141, 153, 178);
        public static readonly Color Faint = Color.FromArgb(90, 104, 136);
        public static readonly Color Accent = Color.FromArgb(124, 108, 255);
        public static readonly Color Sky = Color.FromArgb(56, 189, 248);
        public static readonly Color Green = Color.FromArgb(52, 211, 153);
        public static readonly Color Teal = Color.FromArgb(45, 212, 191);
        public static readonly Color Pink = Color.FromArgb(244, 114, 182);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Orange = Color.FromArgb(251, 146, 60);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);
        public static readonly Color LogBg = Color.FromArgb(7, 10, 19);

        // ---- DPI scaling ---------------------------------------------------
        static float scale;

        public static float Scale
        {
            get
            {
                if (scale <= 0f)
                {
                    try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f; }
                    catch { scale = 1f; }
                    if (scale < 1f) scale = 1f;
                }
                return scale;
            }
        }

        public static int S(int px) { return (int)Math.Round(px * Scale); }
        public static float Sf(float px) { return px * Scale; }

        // ---- fonts ---------------------------------------------------------
        static Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static string uiFamily, semiFamily, monoFamily;

        static bool Has(string name)
        {
            try
            {
                foreach (FontFamily f in FontFamily.Families)
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        static string First(params string[] names)
        {
            foreach (string n in names) if (Has(n)) return n;
            return SystemFonts.DefaultFont.FontFamily.Name;
        }

        static void ResolveFonts()
        {
            if (uiFamily != null) return;
            uiFamily = First("Segoe UI", "Tahoma", "Arial", "Liberation Sans");
            semiFamily = Has("Segoe UI Semibold") ? "Segoe UI Semibold" : null;
            monoFamily = First("Cascadia Mono", "Cascadia Code", "Consolas", "Courier New", "Liberation Mono");
        }

        static Font Get(string family, float pt, FontStyle style)
        {
            string key = family + "|" + pt.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font(family, pt, style, GraphicsUnit.Point);
                fonts[key] = f;
            }
            return f;
        }

        public static Font Ui(float pt) { ResolveFonts(); return Get(uiFamily, pt, FontStyle.Regular); }
        public static Font Bold(float pt) { ResolveFonts(); return Get(uiFamily, pt, FontStyle.Bold); }
        public static Font Semi(float pt)
        {
            ResolveFonts();
            return semiFamily != null ? Get(semiFamily, pt, FontStyle.Regular) : Get(uiFamily, pt, FontStyle.Bold);
        }
        public static Font Mono(float pt) { ResolveFonts(); return Get(monoFamily, pt, FontStyle.Regular); }

        // ---- colour helpers ------------------------------------------------
        public static Color Blend(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color A(Color c, int alpha)
        {
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        // ---- drawing helpers -----------------------------------------------
        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Brush b, RectangleF r, float rad)
        {
            using (GraphicsPath p = Round(r, rad)) g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Pen pen, RectangleF r, float rad)
        {
            using (GraphicsPath p = Round(r, rad)) g.DrawPath(pen, p);
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v)
        {
            if (string.IsNullOrEmpty(s) || r.Width <= 0 || r.Height <= 0) return;
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(s, f, b, r, sf);
            }
        }

        public static float TextWidth(Graphics g, string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                return g.MeasureString(s, f, 10000, sf).Width;
            }
        }
    }

    /// <summary>Eases a value towards a target using a timer that only runs while animating.</summary>
    public sealed class Tween : IDisposable
    {
        public float Value;
        public float Target;
        public float Speed = 0.22f;
        Timer timer;
        Action changed;

        public Tween(Action onChanged) { changed = onChanged; }

        public void Set(float v)
        {
            Value = v;
            Target = v;
            if (timer != null) timer.Stop();
        }

        public void To(float target)
        {
            Target = target;
            if (Math.Abs(Target - Value) < 0.001f)
            {
                Value = Target;
                if (changed != null) changed();
                return;
            }
            if (timer == null)
            {
                timer = new Timer();
                timer.Interval = 15;
                timer.Tick += Step;
            }
            if (!timer.Enabled) timer.Start();
        }

        void Step(object sender, EventArgs e)
        {
            Value += (Target - Value) * Speed;
            if (Math.Abs(Target - Value) < 0.004f)
            {
                Value = Target;
                timer.Stop();
            }
            if (changed != null) changed();
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }
    }

    static class Native
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        static int Rgb(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        /// <summary>Dark title bar on Windows 10/11, plus matching caption colours on Windows 11.</summary>
        public static void StyleTitleBar(IntPtr handle, Color caption, Color text, Color border)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(handle, 19, ref on, 4);
                if (Environment.OSVersion.Version.Build >= 22000)
                {
                    int v = Rgb(caption);
                    DwmSetWindowAttribute(handle, 35, ref v, 4);
                    v = Rgb(text);
                    DwmSetWindowAttribute(handle, 36, ref v, 4);
                    v = Rgb(border);
                    DwmSetWindowAttribute(handle, 34, ref v, 4);
                }
            }
            catch { }
        }

        /// <summary>Dark scrollbars for scrollable controls.</summary>
        public static void DarkScrollbars(Control c)
        {
            if (c.IsHandleCreated) ApplyTheme(c);
            else c.HandleCreated += delegate { ApplyTheme(c); };
        }

        static void ApplyTheme(Control c)
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); }
            catch { }
        }
    }
}
