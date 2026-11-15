// Icons.cs - small vector icon set drawn with GDI+ (no icon-font dependency)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PcCleanupApp
{
    public enum IconKind
    {
        None, Play, Stop, Refresh, Log, Shield, Clock, Code, Apps, Chip, Trash, Windows, Check, Sparkle, Lock, Minus, Plus
    }

    public static class Icons
    {
        static PointF P(RectangleF r, float x, float y)
        {
            return new PointF(r.X + r.Width * x, r.Y + r.Height * y);
        }

        static RectangleF Box(RectangleF r, float x, float y, float w, float h)
        {
            return new RectangleF(r.X + r.Width * x, r.Y + r.Height * y, r.Width * w, r.Height * h);
        }

        static Pen MakePen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        /// <summary>Draws an icon inside r. w is the stroke width used by outline icons.</summary>
        public static void Draw(Graphics g, IconKind kind, RectangleF r, Color c, float w)
        {
            using (Pen pen = MakePen(c, w))
            using (SolidBrush fill = new SolidBrush(c))
            {
                switch (kind)
                {
                    case IconKind.Play:
                        g.FillPolygon(fill, new PointF[] { P(r, 0.30f, 0.16f), P(r, 0.30f, 0.84f), P(r, 0.86f, 0.50f) });
                        break;

                    case IconKind.Stop:
                        Theme.FillRound(g, fill, Box(r, 0.22f, 0.22f, 0.56f, 0.56f), r.Width * 0.12f);
                        break;

                    case IconKind.Refresh:
                        {
                            RectangleF a = Box(r, 0.18f, 0.18f, 0.64f, 0.64f);
                            g.DrawArc(pen, a, 40, 280);
                            double t = 320.0 * Math.PI / 180.0;
                            float cx = a.X + a.Width / 2f, cy = a.Y + a.Height / 2f, rad = a.Width / 2f;
                            float ex = cx + rad * (float)Math.Cos(t), ey = cy + rad * (float)Math.Sin(t);
                            float tx = -(float)Math.Sin(t), ty = (float)Math.Cos(t);
                            float nx = (float)Math.Cos(t), ny = (float)Math.Sin(t);
                            float len = r.Width * 0.2f;
                            g.FillPolygon(fill, new PointF[]
                            {
                                new PointF(ex + tx * len * 1.1f, ey + ty * len * 1.1f),
                                new PointF(ex + nx * len * 0.85f, ey + ny * len * 0.85f),
                                new PointF(ex - nx * len * 0.85f, ey - ny * len * 0.85f)
                            });
                        }
                        break;

                    case IconKind.Log:
                        Theme.DrawRound(g, pen, Box(r, 0.2f, 0.12f, 0.6f, 0.76f), r.Width * 0.1f);
                        g.DrawLine(pen, P(r, 0.34f, 0.36f), P(r, 0.66f, 0.36f));
                        g.DrawLine(pen, P(r, 0.34f, 0.52f), P(r, 0.66f, 0.52f));
                        g.DrawLine(pen, P(r, 0.34f, 0.68f), P(r, 0.54f, 0.68f));
                        break;

                    case IconKind.Shield:
                        g.DrawPolygon(pen, new PointF[]
                        {
                            P(r, 0.5f, 0.08f), P(r, 0.86f, 0.22f), P(r, 0.86f, 0.5f),
                            P(r, 0.5f, 0.92f), P(r, 0.14f, 0.5f), P(r, 0.14f, 0.22f)
                        });
                        g.DrawLines(pen, new PointF[] { P(r, 0.34f, 0.5f), P(r, 0.46f, 0.62f), P(r, 0.68f, 0.38f) });
                        break;

                    case IconKind.Clock:
                        g.DrawEllipse(pen, Box(r, 0.12f, 0.12f, 0.76f, 0.76f));
                        g.DrawLines(pen, new PointF[] { P(r, 0.5f, 0.28f), P(r, 0.5f, 0.52f), P(r, 0.68f, 0.62f) });
                        break;

                    case IconKind.Code:
                        g.DrawLines(pen, new PointF[] { P(r, 0.34f, 0.28f), P(r, 0.12f, 0.5f), P(r, 0.34f, 0.72f) });
                        g.DrawLines(pen, new PointF[] { P(r, 0.66f, 0.28f), P(r, 0.88f, 0.5f), P(r, 0.66f, 0.72f) });
                        g.DrawLine(pen, P(r, 0.56f, 0.2f), P(r, 0.44f, 0.8f));
                        break;

                    case IconKind.Apps:
                        {
                            float rr = r.Width * 0.08f;
                            Theme.DrawRound(g, pen, Box(r, 0.14f, 0.14f, 0.30f, 0.30f), rr);
                            Theme.DrawRound(g, pen, Box(r, 0.56f, 0.14f, 0.30f, 0.30f), rr);
                            Theme.DrawRound(g, pen, Box(r, 0.14f, 0.56f, 0.30f, 0.30f), rr);
                            Theme.DrawRound(g, pen, Box(r, 0.56f, 0.56f, 0.30f, 0.30f), rr);
                        }
                        break;

                    case IconKind.Chip:
                        Theme.DrawRound(g, pen, Box(r, 0.26f, 0.26f, 0.48f, 0.48f), r.Width * 0.07f);
                        for (int i = 0; i < 2; i++)
                        {
                            float p = i == 0 ? 0.40f : 0.60f;
                            g.DrawLine(pen, P(r, p, 0.08f), P(r, p, 0.26f));
                            g.DrawLine(pen, P(r, p, 0.74f), P(r, p, 0.92f));
                            g.DrawLine(pen, P(r, 0.08f, p), P(r, 0.26f, p));
                            g.DrawLine(pen, P(r, 0.74f, p), P(r, 0.92f, p));
                        }
                        break;

                    case IconKind.Trash:
                        g.DrawLine(pen, P(r, 0.16f, 0.28f), P(r, 0.84f, 0.28f));
                        g.DrawLines(pen, new PointF[] { P(r, 0.38f, 0.28f), P(r, 0.38f, 0.16f), P(r, 0.62f, 0.16f), P(r, 0.62f, 0.28f) });
                        g.DrawLines(pen, new PointF[] { P(r, 0.24f, 0.28f), P(r, 0.30f, 0.86f), P(r, 0.70f, 0.86f), P(r, 0.76f, 0.28f) });
                        g.DrawLine(pen, P(r, 0.42f, 0.44f), P(r, 0.42f, 0.70f));
                        g.DrawLine(pen, P(r, 0.58f, 0.44f), P(r, 0.58f, 0.70f));
                        break;

                    case IconKind.Windows:
                        {
                            float rr = r.Width * 0.04f;
                            Theme.FillRound(g, fill, Box(r, 0.12f, 0.14f, 0.36f, 0.34f), rr);
                            Theme.FillRound(g, fill, Box(r, 0.52f, 0.10f, 0.36f, 0.38f), rr);
                            Theme.FillRound(g, fill, Box(r, 0.12f, 0.52f, 0.36f, 0.34f), rr);
                            Theme.FillRound(g, fill, Box(r, 0.52f, 0.52f, 0.36f, 0.38f), rr);
                        }
                        break;

                    case IconKind.Check:
                        g.DrawLines(pen, new PointF[] { P(r, 0.20f, 0.54f), P(r, 0.42f, 0.74f), P(r, 0.80f, 0.30f) });
                        break;

                    case IconKind.Sparkle:
                        g.FillPolygon(fill, new PointF[]
                        {
                            P(r, 0.50f, 0.04f), P(r, 0.60f, 0.40f), P(r, 0.96f, 0.50f), P(r, 0.60f, 0.60f),
                            P(r, 0.50f, 0.96f), P(r, 0.40f, 0.60f), P(r, 0.04f, 0.50f), P(r, 0.40f, 0.40f)
                        });
                        break;

                    case IconKind.Lock:
                        Theme.DrawRound(g, pen, Box(r, 0.22f, 0.44f, 0.56f, 0.42f), r.Width * 0.08f);
                        g.DrawArc(pen, Box(r, 0.32f, 0.14f, 0.36f, 0.5f), 180, 180);
                        break;

                    case IconKind.Minus:
                        g.DrawLine(pen, P(r, 0.24f, 0.5f), P(r, 0.76f, 0.5f));
                        break;

                    case IconKind.Plus:
                        g.DrawLine(pen, P(r, 0.24f, 0.5f), P(r, 0.76f, 0.5f));
                        g.DrawLine(pen, P(r, 0.5f, 0.24f), P(r, 0.5f, 0.76f));
                        break;
                }
            }
        }

        /// <summary>Chevron that rotates from pointing right (open = 0) to pointing down (open = 1).</summary>
        public static void DrawChevron(Graphics g, RectangleF r, Color c, float w, float open)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            g.RotateTransform(90f * open);
            using (Pen pen = MakePen(c, w))
            {
                float s = r.Width * 0.22f;
                g.DrawLines(pen, new PointF[] { new PointF(-s * 0.55f, -s), new PointF(s * 0.65f, 0f), new PointF(-s * 0.55f, s) });
            }
            g.Restore(state);
        }
    }
}
