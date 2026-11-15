// Surfaces.cs - cards, pills, status label and the collapsible category card
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PcCleanupApp
{
    /// <summary>Panel with double buffering and no background flicker.</summary>
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            base.OnPaint(e);
        }
    }

    /// <summary>Rounded card with a subtle border.</summary>
    public class SurfacePanel : Panel
    {
        int radius = 14;
        Color fill = Theme.Surface;
        Color edge = Theme.Border;

        public SurfacePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        public int Radius { get { return radius; } set { radius = value; Invalidate(); } }
        public Color Fill { get { return fill; } set { fill = value; BackColor = value; Invalidate(); } }
        public Color Edge { get { return edge; } set { edge = value; Invalidate(); } }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.Quality(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Theme.Sf(radius);
            using (SolidBrush b = new SolidBrush(fill)) Theme.FillRound(g, b, r, rad);
            using (Pen p = new Pen(edge, 1f)) Theme.DrawRound(g, p, r, rad);
        }
    }

    /// <summary>Rounded pill with a status dot, e.g. "Administrator".</summary>
    public class StatusPill : Control
    {
        Color tone = Theme.Green;

        public StatusPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.Semi(8.75f);
            BackColor = Theme.Bg;
            Height = Theme.S(30);
        }

        public Color Tone { get { return tone; } set { tone = value; Invalidate(); } }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmp))
                Width = (int)Math.Ceiling(Theme.TextWidth(g, Text, Font)) + Theme.S(40);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (SolidBrush b = new SolidBrush(Theme.A(tone, 26))) Theme.FillRound(g, b, r, r.Height / 2f);
            using (Pen p = new Pen(Theme.A(tone, 90), 1f)) Theme.DrawRound(g, p, r, r.Height / 2f);
            float d = Theme.Sf(8f);
            using (SolidBrush glow = new SolidBrush(Theme.A(tone, 50)))
                g.FillEllipse(glow, Theme.Sf(12f) - d * 0.4f, (Height - d) / 2f - d * 0.4f, d * 1.8f, d * 1.8f);
            using (SolidBrush dot = new SolidBrush(tone))
                g.FillEllipse(dot, Theme.Sf(12f), (Height - d) / 2f, d, d);
            Theme.DrawText(g, Text, Font, Theme.Blend(tone, Color.White, 0.35f), new RectangleF(Theme.Sf(26f), 0f, Width - Theme.Sf(30f), Height), StringAlignment.Near, StringAlignment.Center);
        }
    }

    /// <summary>Status text with a pulsing dot while work is in progress.</summary>
    public class StatusLabel : Control
    {
        Timer timer;
        bool lastBusy;
        public Func<bool> IsBusy;

        public StatusLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.Semi(10f);
            BackColor = Theme.Surface;
            timer = new Timer();
            timer.Interval = 45;
            timer.Tick += delegate
            {
                bool busy = IsBusy != null && IsBusy();
                if (busy || busy != lastBusy) Invalidate();
                lastBusy = busy;
            };
            timer.Start();
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            string t = (Text ?? "").ToUpperInvariant();
            bool busy = IsBusy != null && IsBusy();
            Color tone = Theme.Green;
            Color textColor = Theme.Text;
            if (t.Contains("ERROR")) { tone = Theme.Red; textColor = Theme.Red; }
            else if (t.Contains("STOPPED")) { tone = Theme.Amber; textColor = Theme.Amber; }
            else if (t.Contains("COMPLETE")) { tone = Theme.Green; textColor = Theme.Green; }
            else if (busy) tone = Theme.Accent;

            float d = Theme.Sf(9f);
            float cy = Height / 2f;
            if (busy)
            {
                double phase = (Environment.TickCount % 1400) / 1400.0;
                float pulse = (float)(0.5 + 0.5 * Math.Sin(phase * Math.PI * 2.0));
                float big = d * (1.6f + 1.2f * pulse);
                using (SolidBrush halo = new SolidBrush(Theme.A(tone, (int)(70 * (1f - pulse * 0.6f)))))
                    g.FillEllipse(halo, d / 2f + Theme.Sf(2f) - big / 2f, cy - big / 2f, big, big);
            }
            using (SolidBrush dot = new SolidBrush(tone))
                g.FillEllipse(dot, Theme.Sf(2f), cy - d / 2f, d, d);
            Theme.DrawText(g, Text, Font, textColor, new RectangleF(Theme.Sf(22f), 0f, Width - Theme.Sf(22f), Height), StringAlignment.Near, StringAlignment.Center);
        }
    }

    /// <summary>List row. Children report hover so the whole row highlights.</summary>
    public class RowPanel : Panel
    {
        bool hot;

        public RowPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        public bool Hot { get { return hot; } }

        public void Wire(Control c)
        {
            c.MouseEnter += HoverCheck;
            c.MouseLeave += HoverCheck;
        }

        void HoverCheck(object sender, EventArgs e)
        {
            bool h = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (h != hot)
            {
                hot = h;
                Invalidate(true);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); HoverCheck(this, e); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); HoverCheck(this, e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(hot ? Theme.RowHot : BackColor);
        }
    }

    /// <summary>Right-aligned size text with a small proportional meter.</summary>
    public class SizeLabel : Label
    {
        public Func<long> Bytes;
        public Func<long> MaxBytes;

        public SizeLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Font = Theme.Semi(9.5f);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            RowPanel row = Parent as RowPanel;
            g.Clear((row != null && row.Hot) ? Theme.RowHot : BackColor);
            Theme.Quality(g);

            long b = Bytes != null ? Bytes() : -1;
            long max = MaxBytes != null ? MaxBytes() : 0;
            Color c = Theme.Faint;
            if (b > 0) c = b >= 1073741824L ? Theme.Amber : (b >= 104857600L ? Theme.Text : Theme.Dim);

            float right = Width - Theme.Sf(20f);
            float textW = Theme.Sf(84f);
            Theme.DrawText(g, Text, Font, c, new RectangleF(right - textW, 0f, textW, Height), StringAlignment.Far, StringAlignment.Center);

            float meterW = Theme.Sf(64f), meterH = Theme.Sf(5f);
            RectangleF track = new RectangleF(right - textW - Theme.Sf(14f) - meterW, (Height - meterH) / 2f, meterW, meterH);
            if (track.X < 0f) return;
            using (SolidBrush tb = new SolidBrush(Theme.A(Theme.Border, 200))) Theme.FillRound(g, tb, track, meterH / 2f);
            if (b > 0 && max > 0)
            {
                float f = Math.Max(0.04f, Math.Min(1f, (float)((double)b / (double)max)));
                RectangleF fr = new RectangleF(track.X, track.Y, Math.Max(meterH, track.Width * f), track.Height);
                Color c1 = b >= 1073741824L ? Theme.Orange : Theme.Accent;
                Color c2 = b >= 1073741824L ? Theme.Amber : Theme.Sky;
                using (LinearGradientBrush fb = new LinearGradientBrush(track, c1, c2, LinearGradientMode.Horizontal))
                    Theme.FillRound(g, fb, fr, meterH / 2f);
            }
        }
    }

    /// <summary>Collapsible card: icon, title, summary, total size and a body of rows.</summary>
    public class CategoryCard : Panel
    {
        const int HeaderUnits = 60;
        const int RowUnits = 42;
        const int GapUnits = 14;
        const int PadUnits = 12;

        List<RowPanel> rows = new List<RowPanel>();
        BufferedPanel body;
        string title = "";
        string sub = "";
        Color accent = Theme.Accent;
        IconKind icon = IconKind.Sparkle;
        long total = -1;
        bool collapsed;
        bool headHot;
        Control action;
        Tween open;

        public CategoryCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            body = new BufferedPanel();
            body.BackColor = Theme.Surface;
            Controls.Add(body);
            open = new Tween(delegate { ApplyHeight(); });
            open.Set(1f);
            ApplyHeight();
        }

        public string Title { get { return title; } set { title = value; Invalidate(); } }
        public Color Accent { get { return accent; } set { accent = value; Invalidate(); } }
        public IconKind Icon { get { return icon; } set { icon = value; Invalidate(); } }

        public bool Collapsed
        {
            get { return collapsed; }
            set
            {
                collapsed = value;
                if (!collapsed) body.Visible = true;
                open.To(collapsed ? 0f : 1f);
            }
        }

        public Control HeaderAction
        {
            get { return action; }
            set
            {
                action = value;
                if (action != null)
                {
                    action.Parent = this;
                    action.BackColor = Theme.Surface;
                    action.Visible = true;
                    action.BringToFront();
                    PositionAction();
                }
            }
        }

        public void AddRow(RowPanel row)
        {
            rows.Add(row);
            body.Controls.Add(row);
            ApplyHeight();
        }

        public void SetInfo(int count, int selected, long measuredTotal)
        {
            sub = count + (count == 1 ? " target" : " targets") + "  ·  " + selected + " selected";
            total = measuredTotal;
            Invalidate();
        }

        public void InvalidateRows() { body.Invalidate(true); }

        public void Toggle() { Collapsed = !collapsed; }

        int HeaderH { get { return Theme.S(HeaderUnits); } }
        int RowsH { get { return rows.Count * Theme.S(RowUnits); } }
        int GapH { get { return Theme.S(GapUnits); } }

        void ApplyHeight()
        {
            int pad = rows.Count > 0 ? Theme.S(PadUnits) : 0;
            int visible = (int)Math.Round((RowsH + pad) * open.Value);
            int h = HeaderH + visible + GapH;
            if (Height != h) Height = h;
            int bodyH = Math.Max(0, Math.Min(RowsH, visible));
            body.SetBounds(1, HeaderH, Math.Max(0, Width - 2), bodyH);
            int y = 0;
            foreach (RowPanel r in rows)
            {
                r.SetBounds(0, y, body.Width, Theme.S(RowUnits));
                y += Theme.S(RowUnits);
            }
            if (open.Value <= 0.001f && collapsed) body.Visible = false;
            PositionAction();
            Invalidate();
        }

        float SizeColumnX { get { return Width - Theme.Sf(48f) - Theme.Sf(104f); } }

        void PositionAction()
        {
            if (action == null) return;
            int x = (int)SizeColumnX - Theme.S(14) - action.Width;
            action.Location = new Point(Math.Max(Theme.S(200), x), (HeaderH - action.Height) / 2);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyHeight();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = e.Y < HeaderH;
            if (h != headHot) { headHot = h; Invalidate(); }
            Cursor = h ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (headHot) { headHot = false; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && e.Y < HeaderH) Toggle();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) open.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.Quality(g);

            float rad = Theme.Sf(12f);
            RectangleF card = new RectangleF(0.5f, 0.5f, Width - 1f, Height - GapH - 1f);
            using (SolidBrush b = new SolidBrush(Theme.Surface)) Theme.FillRound(g, b, card, rad);
            if (headHot)
            {
                GraphicsState st = g.Save();
                using (GraphicsPath clip = Theme.Round(card, rad)) g.SetClip(clip, CombineMode.Intersect);
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(10, 255, 255, 255)))
                    g.FillRectangle(hb, 0f, 0f, Width, HeaderH);
                g.Restore(st);
            }
            using (Pen p = new Pen(Theme.Border, 1f)) Theme.DrawRound(g, p, card, rad);

            float hh = HeaderH;
            float ts = Theme.Sf(38f);
            RectangleF tile = new RectangleF(Theme.Sf(18f), (hh - ts) / 2f, ts, ts);
            using (LinearGradientBrush tb = new LinearGradientBrush(tile, Theme.A(accent, 52), Theme.A(accent, 20), 60f))
                Theme.FillRound(g, tb, tile, Theme.Sf(11f));
            using (Pen tp = new Pen(Theme.A(accent, 84), 1f)) Theme.DrawRound(g, tp, tile, Theme.Sf(11f));
            RectangleF ir = new RectangleF(tile.X + Theme.Sf(9f), tile.Y + Theme.Sf(9f), ts - Theme.Sf(18f), ts - Theme.Sf(18f));
            Icons.Draw(g, icon, ir, Theme.Blend(accent, Color.White, 0.18f), Theme.Sf(1.7f));

            float tx = tile.Right + Theme.Sf(14f);
            float rightLimit = SizeColumnX - (action != null ? action.Width + Theme.Sf(24f) : Theme.Sf(8f));
            float tw = Math.Max(Theme.Sf(60f), rightLimit - tx);
            Theme.DrawText(g, title, Theme.Semi(11f), Theme.Text, new RectangleF(tx, tile.Y - Theme.Sf(1f), tw, Theme.Sf(21f)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, sub, Theme.Ui(8.75f), Theme.Dim, new RectangleF(tx, tile.Y + Theme.Sf(21f), tw, Theme.Sf(17f)), StringAlignment.Near, StringAlignment.Center);

            string size = total >= 0 ? FileSystemService.FmtBytes(total) : "—";
            Theme.DrawText(g, size, Theme.Semi(11f), total > 0 ? Theme.Text : Theme.Faint,
                new RectangleF(SizeColumnX, 0f, Theme.Sf(104f), hh), StringAlignment.Far, StringAlignment.Center);

            float cs = Theme.Sf(20f);
            Icons.DrawChevron(g, new RectangleF(Width - Theme.Sf(18f) - cs, (hh - cs) / 2f, cs, cs),
                Theme.Blend(Theme.Faint, Theme.Text, headHot ? 1f : 0.35f), Theme.Sf(1.8f), open.Value);

            if (open.Value > 0.02f)
                using (Pen sp = new Pen(Theme.A(Theme.Border, (int)(255 * open.Value)), 1f))
                    g.DrawLine(sp, Theme.Sf(1f), hh, Width - Theme.Sf(1f), hh);
        }
    }

    /// <summary>App logo + name at the top of the sidebar.</summary>
    public class BrandPanel : Control
    {
        public string Subtitle = "";

        public BrandPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Sidebar;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Theme.Quality(g);
            float s = Theme.Sf(42f);
            RectangleF logo = new RectangleF(Theme.Sf(22f), (Height - s) / 2f, s, s);
            using (SolidBrush glow = new SolidBrush(Theme.A(Theme.Accent, 38)))
                Theme.FillRound(g, glow, new RectangleF(logo.X - Theme.Sf(4f), logo.Y - Theme.Sf(2f), s + Theme.Sf(8f), s + Theme.Sf(8f)), Theme.Sf(16f));
            using (LinearGradientBrush lb = new LinearGradientBrush(logo, Theme.Accent, Theme.Sky, 45f))
                Theme.FillRound(g, lb, logo, Theme.Sf(13f));
            Icons.Draw(g, IconKind.Sparkle, new RectangleF(logo.X + Theme.Sf(9f), logo.Y + Theme.Sf(9f), s - Theme.Sf(18f), s - Theme.Sf(18f)), Color.White, 1.5f);

            float tx = logo.Right + Theme.Sf(14f);
            Theme.DrawText(g, "PC Cleanup", Theme.Semi(15f), Theme.Text, new RectangleF(tx, logo.Y - Theme.Sf(2f), Width - tx, Theme.Sf(24f)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, Subtitle, Theme.Ui(8.75f), Theme.Dim, new RectangleF(tx, logo.Y + Theme.Sf(23f), Width - tx, Theme.Sf(18f)), StringAlignment.Near, StringAlignment.Center);
        }
    }
}
