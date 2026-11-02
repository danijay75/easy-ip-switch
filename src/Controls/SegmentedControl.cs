using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class SegmentedControl : Control, IThemeAware
    {
        private int _selected;
        private int _hover = -1;
        private string[] _items = new string[] { "Option 1", "Option 2" };

        public event EventHandler SelectedIndexChanged;

        public SegmentedControl()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(Theme.S(210), Theme.S(34));
        }

        public string[] Items
        {
            get { return _items; }
            set
            {
                _items = value;
                Invalidate();
            }
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
            using (GraphicsPath path = Theme.RoundedRect(track, Theme.S(9)))
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
            int segmentWidth = (Width - inset * 2) / _items.Length;
            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle seg = new Rectangle(inset + i * segmentWidth, inset, segmentWidth, Height - inset * 2);
                bool isSelected = i == _selected;
                bool isHover = i == _hover && !isSelected && Enabled;

                if (isSelected)
                {
                    using (GraphicsPath path = Theme.RoundedRect(seg, Theme.S(7)))
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
                else if (isHover)
                {
                    using (GraphicsPath path = Theme.RoundedRect(seg, Theme.S(7)))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(60, Color.White)))
                    {
                        g.FillPath(brush, path);
                    }
                }

                Color text = !Enabled ? Theme.TextFaint : isSelected ? Theme.Text : Theme.TextMuted;
                Font font = isSelected ? Theme.SmallBold : Theme.Small;
                TextRenderer.DrawText(g, _items[i], font, seg, text, Theme.TextCenter);
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
            if (_items.Length == 0)
            {
                return -1;
            }
            int inset = Theme.S(3);
            int segmentWidth = (Width - inset * 2) / _items.Length;
            int index = (x - inset) / Math.Max(1, segmentWidth);
            if (index < 0 || index >= _items.Length)
            {
                return -1;
            }
            return index;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }
}
