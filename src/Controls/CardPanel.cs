using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal class CardPanel : Panel, IThemeAware
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            DoubleBuffered = true;
        }

        public virtual void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Painting is handled in OnPaint so the rounded corners show the
            // background of the parent instead of a white square.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, Theme.S(14)))
            {
                using (SolidBrush brush = new SolidBrush(Theme.Surface))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }
}
