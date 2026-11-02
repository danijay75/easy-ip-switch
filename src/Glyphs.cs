using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EasyIpSwitch
{
    internal static class Glyphs
    {
        public static void Wifi(Graphics g, Rectangle box, Color color, float stroke)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height * 0.80f;
            float unit = box.Width;

            using (Pen pen = new Pen(color, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int i = 1; i <= 3; i++)
                {
                    float radius = unit * (0.15f * i) + unit * 0.07f;
                    RectangleF arc = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);
                    g.DrawArc(pen, arc, 212f, 116f);
                }
            }

            using (SolidBrush brush = new SolidBrush(color))
            {
                float d = unit * 0.11f;
                g.FillEllipse(brush, cx - d / 2f, cy - d / 2f, d, d);
            }

            g.SmoothingMode = old;
        }

        public static void Ethernet(Graphics g, Rectangle box, Color color, float stroke)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = box.X + box.Width * 0.13f;
            float w = box.Width * 0.74f;
            float y = box.Y + box.Height * 0.08f;
            float h = box.Height * 0.42f;

            using (Pen pen = new Pen(color, stroke))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                RectangleF body = new RectangleF(x, y, w, h);
                using (GraphicsPath path = Theme.RoundedRect(body, box.Width * 0.07f))
                {
                    g.DrawPath(pen, path);
                }

                float pinTop = y + h * 0.40f;
                float pinBottom = y + h * 0.80f;
                for (int i = 0; i < 4; i++)
                {
                    float px = x + w * (0.28f + i * 0.147f);
                    g.DrawLine(pen, px, pinTop, px, pinBottom);
                }

                float cableTop = y + h + stroke * 0.4f;
                float cableBottom = box.Y + box.Height * 0.92f;
                g.DrawLine(pen, x + w / 2f, cableTop, x + w / 2f, cableBottom);
            }

            g.SmoothingMode = old;
        }

        public static void ChevronDown(Graphics g, Rectangle box, Color color, float stroke)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;
            float half = box.Width * 0.22f;
            float drop = box.Height * 0.13f;
            using (Pen pen = new Pen(color, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(cx - half, cy - drop),
                    new PointF(cx, cy + drop),
                    new PointF(cx + half, cy - drop)
                });
            }
            g.SmoothingMode = old;
        }

        public static void Refresh(Graphics g, Rectangle box, Color color, float stroke)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF arc = new RectangleF(box.X + stroke, box.Y + stroke,
                box.Width - stroke * 2f, box.Height - stroke * 2f);

            using (Pen pen = new Pen(color, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, arc, -55f, 285f);

                float cx = arc.X + arc.Width / 2f;
                float cy = arc.Y + arc.Height / 2f;
                double angle = -55.0 * Math.PI / 180.0;
                float px = cx + (float)Math.Cos(angle) * arc.Width / 2f;
                float py = cy + (float)Math.Sin(angle) * arc.Height / 2f;
                float head = box.Width * 0.22f;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(px - head * 1.05f, py - head * 0.15f),
                    new PointF(px, py),
                    new PointF(px - head * 0.20f, py + head * 1.05f)
                });
            }
            g.SmoothingMode = old;
        }

        public static void Logo(Graphics g, Rectangle box)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = Theme.RoundedRect(box, Theme.S(10)))
            using (LinearGradientBrush brush = new LinearGradientBrush(box, Theme.Accent, Theme.AccentPressed, 55f))
            {
                g.FillPath(brush, path);
            }

            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;
            float len = box.Width * 0.46f;
            float gap = box.Height * 0.17f;
            float thick = box.Width * 0.085f;
            float head = box.Width * 0.15f;

            using (Pen pen = new Pen(Color.White, thick))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - len / 2f, cy - gap, cx + len / 2f, cy - gap);
                g.DrawLine(pen, cx - len / 2f, cy + gap, cx + len / 2f, cy + gap);
            }

            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                g.FillPolygon(brush, new PointF[]
                {
                    new PointF(cx + len / 2f + head * 0.2f, cy - gap),
                    new PointF(cx + len / 2f - head, cy - gap - head * 0.75f),
                    new PointF(cx + len / 2f - head, cy - gap + head * 0.75f)
                });
                g.FillPolygon(brush, new PointF[]
                {
                    new PointF(cx - len / 2f - head * 0.2f, cy + gap),
                    new PointF(cx - len / 2f + head, cy + gap - head * 0.75f),
                    new PointF(cx - len / 2f + head, cy + gap + head * 0.75f)
                });
            }

            g.SmoothingMode = old;
        }
    }
}
