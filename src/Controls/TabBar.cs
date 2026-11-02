using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class TabBar : Control, IThemeAware
    {
        private string[] _items = new string[0];
        private int _selected;
        private int _hover = -1;

        public event EventHandler SelectedIndexChanged;

        public TabBar()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.S(40);
        }

        public string[] Items
        {
            get { return _items; }
            set
            {
                _items = value ?? new string[0];
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int index = value;
                if (index < 0)
                {
                    index = 0;
                }
                if (_items.Length > 0 && index >= _items.Length)
                {
                    index = _items.Length - 1;
                }
                if (_selected == index)
                {
                    return;
                }
                _selected = index;
                Invalidate();
                if (SelectedIndexChanged != null)
                {
                    SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            int x = 0;
            for (int i = 0; i < _items.Length; i++)
            {
                int width = TabWidth(_items[i]);
                Rectangle cell = new Rectangle(x, 0, width, Height);
                bool isSelected = i == _selected;
                bool isHover = i == _hover && !isSelected && Enabled;

                if (isHover)
                {
                    using (GraphicsPath path = Theme.RoundedRect(
                        new Rectangle(cell.X + Theme.S(2), cell.Y + Theme.S(2), cell.Width - Theme.S(4), cell.Height - Theme.S(6)),
                        Theme.S(8)))
                    using (SolidBrush brush = new SolidBrush(Theme.SurfaceMuted))
                    {
                        g.FillPath(brush, path);
                    }
                }

                Color color = !Enabled ? Theme.TextFaint : isSelected ? Theme.Text : Theme.TextMuted;
                TextRenderer.DrawText(g, _items[i], Font, cell, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                if (isSelected)
                {
                    using (Pen pen = new Pen(Theme.Accent, Theme.S(2.5f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        int inset = Theme.S(10);
                        g.DrawLine(pen, cell.Left + inset, Height - Theme.S(4),
                            cell.Right - inset, Height - Theme.S(4));
                    }
                }

                x += width;
            }
        }

        private int TabWidth(string text)
        {
            Size size = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return size.Width + Theme.S(34);
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
                SelectedIndex = Math.Max(0, _selected - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SelectedIndex = Math.Min(_items.Length - 1, _selected + 1);
                e.Handled = true;
            }
        }

        private int HitTest(int x)
        {
            int left = 0;
            for (int i = 0; i < _items.Length; i++)
            {
                int width = TabWidth(_items[i]);
                if (x >= left && x < left + width)
                {
                    return i;
                }
                left += width;
            }
            return -1;
        }
    }
}
