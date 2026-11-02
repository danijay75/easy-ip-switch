using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class ThemeToggle : Control, IThemeAware
    {
        private int _selected;
        private int _hover = -1;

        public event EventHandler SelectedIndexChanged;

        public ThemeToggle()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(Theme.S(88), Theme.S(34));
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (_selected == value)
                {
                    return;
                }
                _selected = value;
                Invalidate();
                if (SelectedIndexChanged != null)
                {
                    SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetSelectedSilent(int index)
        {
            _selected = index;
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height / 2;
            using (GraphicsPath path = Theme.RoundedRect(track, radius))
            {
                using (SolidBrush brush = new SolidBrush(Theme.SurfaceSunken))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            int inset = Theme.S(3);
            int segmentWidth = (Width - inset * 2) / 2;
            int segmentHeight = Height - inset * 2;

            for (int i = 0; i < 2; i++)
            {
                Rectangle segment = new Rectangle(inset + i * segmentWidth, inset, segmentWidth, segmentHeight);
                bool isSelected = i == _selected;
                bool isHover = i == _hover && !isSelected;

                Color background = isSelected ? Theme.AccentSoft : Theme.SurfaceSunken;
                if (isHover)
                {
                    background = Theme.SurfaceMuted;
                }

                using (GraphicsPath path = Theme.RoundedRect(segment, segmentHeight / 2))
                using (SolidBrush brush = new SolidBrush(background))
                {
                    g.FillPath(brush, path);
                }

                Color icon = !Enabled ? Theme.TextFaint : isSelected ? Theme.Accent : Theme.TextMuted;
                if (i == 0)
                {
                    DrawSun(g, segment, icon);
                }
                else
                {
                    DrawMoon(g, segment, icon, background);
                }
            }
        }

        private static void DrawSun(Graphics g, Rectangle box, Color color)
        {
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;
            float r = box.Width * 0.15f;
            using (Pen pen = new Pen(color, Theme.S(1.6f)))
            {
                g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    float inner = r + box.Width * 0.08f;
                    float outer = r + box.Width * 0.19f;
                    g.DrawLine(pen,
                        cx + (float)Math.Cos(angle) * inner,
                        cy + (float)Math.Sin(angle) * inner,
                        cx + (float)Math.Cos(angle) * outer,
                        cy + (float)Math.Sin(angle) * outer);
                }
            }
        }

        private static void DrawMoon(Graphics g, Rectangle box, Color color, Color carve)
        {
            float cx = box.X + box.Width / 2f;
            float cy = box.Y + box.Height / 2f;
            float r = box.Width * 0.24f;
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, cx - r, cy - r, r * 2f, r * 2f);
            }
            using (SolidBrush brush = new SolidBrush(carve))
            {
                g.FillEllipse(brush, cx - r + r * 0.58f, cy - r - r * 0.20f, r * 2f, r * 2f);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.X);
            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left)
            {
                return;
            }
            Focus();
            int index = HitTest(e.X);
            if (index >= 0)
            {
                SelectedIndex = index;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled)
            {
                return;
            }
            if (e.KeyCode == Keys.Left)
            {
                SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SelectedIndex = 1;
                e.Handled = true;
            }
        }

        private int HitTest(int x)
        {
            int inset = Theme.S(3);
            int segmentWidth = (Width - inset * 2) / 2;
            if (segmentWidth <= 0 || x < inset)
            {
                return -1;
            }
            int index = (x - inset) / segmentWidth;
            if (index < 0 || index > 1)
            {
                return -1;
            }
            return index;
        }
    }
}
