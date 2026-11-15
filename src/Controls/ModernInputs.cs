// ModernInputs.cs - toggle switch, checkbox and stepper with smooth animations
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    /// <summary>iOS-style switch with a title and an optional hint line. Behaves like a CheckBox.</summary>
    public class ToggleSwitch : CheckBox
    {
        string hint;
        Color textColor = Theme.Text;
        Color onFrom = Theme.Accent;
        Color onTo = Theme.Sky;
        Tween t;
        Tween hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(9.5f);
            BackColor = Theme.Surface;
            Height = Theme.S(44);
            t = new Tween(delegate { Invalidate(); });
            hover = new Tween(delegate { Invalidate(); });
        }

        public string Hint { get { return hint; } set { hint = value; Invalidate(); } }
        public Color TextColor { get { return textColor; } set { textColor = value; Invalidate(); } }

        public void SetOnColors(Color from, Color to)
        {
            onFrom = from;
            onTo = to;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            t.Set(Checked ? 1f : 0f);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            t.To(Checked ? 1f : 0f);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                t.Dispose();
                hover.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            bool en = Enabled;
            float v = t.Value;
            float hv = hover.Value;

            float tw = Theme.Sf(40f), th = Theme.Sf(22f);
            RectangleF track = new RectangleF(Width - tw - 1f, (Height - th) / 2f, tw, th);
            Color off = Theme.Blend(Color.FromArgb(42, 54, 84), Color.FromArgb(56, 70, 106), hv);
            Color a = Theme.Blend(off, onFrom, v);
            Color b = Theme.Blend(off, onTo, v);
            if (!en)
            {
                a = Theme.Blend(a, BackColor, 0.55f);
                b = Theme.Blend(b, BackColor, 0.55f);
            }
            using (LinearGradientBrush br = new LinearGradientBrush(track, a, b, LinearGradientMode.Horizontal))
                Theme.FillRound(g, br, track, th / 2f);

            float knob = th - Theme.Sf(6f);
            float kx = track.X + Theme.Sf(3f) + (tw - knob - Theme.Sf(6f)) * v;
            RectangleF k = new RectangleF(kx, track.Y + Theme.Sf(3f), knob, knob);
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(en ? 70 : 20, 0, 0, 0)))
                g.FillEllipse(sh, k.X, k.Y + Theme.Sf(1.2f), k.Width, k.Height);
            Color kc = Theme.Blend(Color.FromArgb(196, 205, 224), Color.White, v);
            if (!en) kc = Theme.Blend(kc, BackColor, 0.5f);
            using (SolidBrush kb = new SolidBrush(kc)) g.FillEllipse(kb, k);

            Font titleFont = Font;
            Font hintFont = Theme.Ui(8.25f);
            float titleH = titleFont.GetHeight(g);
            float hintH = string.IsNullOrEmpty(hint) ? 0f : hintFont.GetHeight(g);
            float block = titleH + hintH;
            float y0 = (Height - block) / 2f;
            float maxW = track.X - Theme.Sf(12f);
            Theme.DrawText(g, Text, titleFont, en ? textColor : Theme.Faint, new RectangleF(0f, y0, maxW, titleH), StringAlignment.Near, StringAlignment.Center);
            if (hintH > 0f)
                Theme.DrawText(g, hint, hintFont, Theme.Faint, new RectangleF(0f, y0 + titleH, maxW, hintH), StringAlignment.Near, StringAlignment.Center);

            if (Focused && ShowFocusCues)
                using (Pen fp = new Pen(Theme.A(Theme.Accent, 200), 1.5f))
                    Theme.DrawRound(g, fp, new RectangleF(track.X - 2f, track.Y - 2f, track.Width + 4f, track.Height + 4f), th / 2f + 2f);
        }
    }

    /// <summary>Rounded checkbox that fills the whole row. Behaves like a CheckBox.</summary>
    public class ModernCheckBox : CheckBox
    {
        bool locked;
        Tween t;
        Tween hover;

        public ModernCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = Theme.Ui(10f);
            BackColor = Theme.Surface;
            t = new Tween(delegate { Invalidate(); });
            hover = new Tween(delegate { Invalidate(); });
        }

        /// <summary>Shows a lock marker for targets that need administrator rights.</summary>
        public bool Locked { get { return locked; } set { locked = value; Invalidate(); } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            t.Set(Checked ? 1f : 0f);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            t.To(Checked ? 1f : 0f);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                t.Dispose();
                hover.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            RowPanel row = Parent as RowPanel;
            Color bg = (row != null && row.Hot) ? Theme.RowHot : BackColor;
            g.Clear(bg);
            Theme.Quality(g);
            bool en = Enabled;
            float v = t.Value;
            float hv = hover.Value;

            float bs = Theme.Sf(20f);
            RectangleF box = new RectangleF(Theme.Sf(20f), (Height - bs) / 2f, bs, bs);
            float rad = Theme.Sf(6f);
            RectangleF edge = new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1f, box.Height - 1f);

            Color borderOff = Theme.Blend(Color.FromArgb(72, 86, 120), Theme.Accent, hv * 0.9f);
            if (!en) borderOff = Theme.Blend(borderOff, bg, 0.5f);
            if (v < 0.999f)
                using (Pen pen = new Pen(borderOff, 1.5f)) Theme.DrawRound(g, pen, edge, rad);
            if (v > 0.001f)
            {
                Color a = Theme.A(en ? Theme.Accent : Theme.Blend(Theme.Accent, bg, 0.55f), (int)(255 * v));
                Color b = Theme.A(en ? Theme.Sky : Theme.Blend(Theme.Sky, bg, 0.55f), (int)(255 * v));
                using (LinearGradientBrush br = new LinearGradientBrush(box, a, b, 45f))
                    Theme.FillRound(g, br, box, rad);
                RectangleF ci = new RectangleF(box.X + Theme.Sf(3.5f), box.Y + Theme.Sf(3.5f), box.Width - Theme.Sf(7f), box.Height - Theme.Sf(7f));
                Icons.Draw(g, IconKind.Check, ci, Color.FromArgb((int)(255 * v), 255, 255, 255), Theme.Sf(2f));
            }

            Color tc = en ? Theme.Blend(Theme.Dim, Theme.Text, v) : Theme.Faint;
            float tx = box.Right + Theme.Sf(12f);
            float reserve = locked ? Theme.Sf(30f) : 0f;
            float tw = Math.Min(Theme.TextWidth(g, Text, Font), Width - tx - reserve - Theme.Sf(8f));
            Theme.DrawText(g, Text, Font, tc, new RectangleF(tx, 0f, tw + 4f, Height), StringAlignment.Near, StringAlignment.Center);

            if (locked)
            {
                float ls = Theme.Sf(14f);
                Icons.Draw(g, IconKind.Lock, new RectangleF(tx + tw + Theme.Sf(10f), (Height - ls) / 2f, ls, ls), Theme.A(Theme.Amber, 210), Theme.Sf(1.4f));
            }

            if (Focused && ShowFocusCues)
                using (Pen fp = new Pen(Theme.A(Theme.Accent, 200), 1.5f))
                    Theme.DrawRound(g, fp, new RectangleF(box.X - 3f, box.Y - 3f, box.Width + 6f, box.Height + 6f), rad + 2f);
        }
    }

    /// <summary>Compact numeric stepper: [ - ] 1 day [ + ].</summary>
    public class Stepper : Control
    {
        decimal val = 1;
        decimal min = 0;
        decimal max = 100;
        string unitSingular = "";
        string unitPlural = "";
        int hot;          // -1 minus, 0 none, 1 plus
        int pressed;      // -1 minus, 0 none, 1 plus
        Timer repeat;
        int repeatTicks;

        public event EventHandler ValueChanged;

        public Stepper()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Font = Theme.Semi(9.5f);
            BackColor = Theme.Surface;
            Size = new Size(Theme.S(116), Theme.S(32));
            repeat = new Timer();
            repeat.Interval = 60;
            repeat.Tick += delegate
            {
                repeatTicks++;
                if (repeatTicks > 6 && pressed != 0) Nudge(pressed);
            };
        }

        public decimal Minimum { get { return min; } set { min = value; if (val < min) Value = min; Invalidate(); } }
        public decimal Maximum { get { return max; } set { max = value; if (val > max) Value = max; Invalidate(); } }

        public decimal Value
        {
            get { return val; }
            set
            {
                decimal v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetUnits(string singular, string plural)
        {
            unitSingular = singular;
            unitPlural = plural;
            Invalidate();
        }

        void Nudge(int dir)
        {
            if (!Enabled) return;
            Value = val + dir;
        }

        int ZoneAt(Point p)
        {
            int bw = Theme.S(32);
            if (p.X < bw) return -1;
            if (p.X >= Width - bw) return 1;
            return 0;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right) { Nudge(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left) { Nudge(-1); e.Handled = true; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Nudge(e.Delta > 0 ? 1 : -1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int z = ZoneAt(e.Location);
            if (z != hot) { hot = z; Invalidate(); }
            Cursor = z != 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hot = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int z = ZoneAt(e.Location);
            if (e.Button == MouseButtons.Left && z != 0)
            {
                pressed = z;
                Nudge(z);
                repeatTicks = 0;
                repeat.Start();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = 0;
            repeat.Stop();
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && repeat != null)
            {
                repeat.Stop();
                repeat.Dispose();
                repeat = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            bool en = Enabled;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.Sf(9f);
            float bw = Theme.Sf(32f);

            Color fill = en ? Theme.SurfaceHi : Theme.Blend(Theme.SurfaceHi, BackColor, 0.5f);
            Color edge = en ? Theme.Border : Theme.Blend(Theme.Border, BackColor, 0.5f);
            if (Focused && ShowFocusCues) edge = Theme.A(Theme.Accent, 200);
            using (SolidBrush br = new SolidBrush(fill)) Theme.FillRound(g, br, r, rad);

            // pressed / hovered halves
            if (en && hot != 0)
            {
                RectangleF half = hot < 0 ? new RectangleF(1f, 1f, bw, Height - 2f) : new RectangleF(Width - bw - 1f, 1f, bw, Height - 2f);
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Theme.Round(r, rad)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush hb = new SolidBrush(Theme.A(Theme.Accent, pressed == hot ? 70 : 38))) g.FillRectangle(hb, half);
                g.Restore(st);
            }
            using (Pen pen = new Pen(edge, 1f)) Theme.DrawRound(g, pen, r, rad);

            float isz = Theme.Sf(16f);
            Color ic = en ? Theme.Text : Theme.Faint;
            Icons.Draw(g, IconKind.Minus, new RectangleF((bw - isz) / 2f, (Height - isz) / 2f, isz, isz), (en && val <= min) ? Theme.Faint : ic, Theme.Sf(1.8f));
            Icons.Draw(g, IconKind.Plus, new RectangleF(Width - bw + (bw - isz) / 2f, (Height - isz) / 2f, isz, isz), (en && val >= max) ? Theme.Faint : ic, Theme.Sf(1.8f));

            string s = val.ToString();
            string unit = val == 1 ? unitSingular : unitPlural;
            if (!string.IsNullOrEmpty(unit)) s += " " + unit;
            Theme.DrawText(g, s, Font, en ? Theme.Text : Theme.Faint, new RectangleF(bw, 0f, Width - bw * 2f, Height), StringAlignment.Center, StringAlignment.Center);
        }
    }
}
