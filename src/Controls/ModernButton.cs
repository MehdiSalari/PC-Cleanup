// ModernButton.cs - rounded, animated, custom painted button
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    public enum ButtonLook { Primary, Secondary, Ghost, Danger }

    public class ModernButton : Button
    {
        ButtonLook look = ButtonLook.Secondary;
        Color gradFrom = Theme.Accent;
        Color gradTo = Theme.Sky;
        IconKind icon = IconKind.None;
        int radius = 10;
        Tween hover;
        Tween press;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(9.5f);
            BackColor = Theme.Bg;
            hover = new Tween(delegate { Invalidate(); });
            press = new Tween(delegate { Invalidate(); });
            press.Speed = 0.35f;
        }

        public ButtonLook Look { get { return look; } set { look = value; Invalidate(); } }
        public IconKind Icon { get { return icon; } set { icon = value; Invalidate(); } }
        public int Radius { get { return radius; } set { radius = value; Invalidate(); } }

        public void SetGradient(Color from, Color to)
        {
            gradFrom = from;
            gradTo = to;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f); press.To(0f); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) press.To(1f); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); press.To(0f); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hover.Dispose();
                press.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (r.Width < 4f || r.Height < 4f) return;
            float rad = Theme.Sf(radius);
            float h = hover.Value;
            float p = press.Value;
            bool en = Enabled;
            Color textColor;

            if (look == ButtonLook.Primary)
            {
                Color a = Theme.Blend(gradFrom, Color.White, 0.12f * h);
                Color b = Theme.Blend(gradTo, Color.White, 0.12f * h);
                a = Theme.Blend(a, Color.Black, 0.20f * p);
                b = Theme.Blend(b, Color.Black, 0.20f * p);
                if (!en)
                {
                    a = Theme.Blend(a, BackColor, 0.68f);
                    b = Theme.Blend(b, BackColor, 0.68f);
                }
                using (LinearGradientBrush br = new LinearGradientBrush(r, a, b, 20f))
                    Theme.FillRound(g, br, r, rad);
                using (Pen gloss = new Pen(Color.FromArgb(en ? 52 : 14, 255, 255, 255), 1f))
                    Theme.DrawRound(g, gloss, new RectangleF(r.X + 1f, r.Y + 1f, r.Width - 2f, r.Height - 2f), Math.Max(1f, rad - 1f));
                textColor = en ? Color.White : Theme.Blend(Color.White, BackColor, 0.55f);
            }
            else if (look == ButtonLook.Danger)
            {
                Color fill = Theme.A(Theme.Red, (int)(26 + 40 * h + 30 * p));
                Color edge = Theme.A(Theme.Red, (int)(90 + 90 * h));
                if (!en)
                {
                    fill = Theme.A(Theme.Red, 8);
                    edge = Theme.A(Theme.Red, 34);
                }
                using (SolidBrush br = new SolidBrush(fill)) Theme.FillRound(g, br, r, rad);
                using (Pen pen = new Pen(edge, 1f)) Theme.DrawRound(g, pen, r, rad);
                textColor = en ? Theme.Blend(Theme.Red, Color.White, 0.25f * h) : Theme.A(Theme.Red, 90);
            }
            else if (look == ButtonLook.Ghost)
            {
                if (en && (h > 0.01f || p > 0.01f))
                    using (SolidBrush br = new SolidBrush(Theme.A(Theme.SurfaceHi, (int)(255 * Math.Min(1f, h + p * 0.4f)))))
                        Theme.FillRound(g, br, r, rad);
                textColor = en ? Theme.Blend(Theme.Dim, Theme.Text, h) : Theme.Faint;
            }
            else
            {
                Color fill = Theme.Blend(Theme.SurfaceHi, Color.FromArgb(38, 50, 80), h);
                fill = Theme.Blend(fill, Color.Black, 0.18f * p);
                Color edge = Theme.Blend(Theme.Border, Theme.Accent, 0.65f * h);
                if (!en)
                {
                    fill = Theme.Blend(fill, BackColor, 0.55f);
                    edge = Theme.Blend(edge, BackColor, 0.55f);
                }
                using (SolidBrush br = new SolidBrush(fill)) Theme.FillRound(g, br, r, rad);
                using (Pen pen = new Pen(edge, 1f)) Theme.DrawRound(g, pen, r, rad);
                textColor = en ? Theme.Text : Theme.Faint;
            }

            bool hasIcon = icon != IconKind.None;
            float iconSize = hasIcon ? Theme.Sf(15f) : 0f;
            float gap = (hasIcon && !string.IsNullOrEmpty(Text)) ? Theme.Sf(8f) : 0f;
            float tw = Math.Min(Theme.TextWidth(g, Text, Font), Width - iconSize - gap - Theme.Sf(12f));
            float x = (Width - (iconSize + gap + tw)) / 2f;
            if (hasIcon)
                Icons.Draw(g, icon, new RectangleF(x, (Height - iconSize) / 2f, iconSize, iconSize), textColor, Theme.Sf(1.6f));
            Theme.DrawText(g, Text, Font, textColor, new RectangleF(x + iconSize + gap, 0f, tw + 4f, Height), StringAlignment.Near, StringAlignment.Center);

            if (Focused && ShowFocusCues)
                using (Pen fp = new Pen(Theme.A(Theme.Accent, 200), 1.5f))
                    Theme.DrawRound(g, fp, new RectangleF(r.X + 2f, r.Y + 2f, r.Width - 4f, r.Height - 4f), Math.Max(1f, rad - 2f));
        }
    }
}
