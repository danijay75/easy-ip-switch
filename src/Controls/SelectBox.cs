using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class SelectBox : Control, IThemeAware
    {
        private readonly List<object> _items = new List<object>();
        private int _selected = -1;
        private bool _hover;
        private bool _open;
        private ContextMenuStrip _menu;

        public event EventHandler SelectedIndexChanged;

        public SelectBox()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.SurfaceSunken;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.S(36);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int index = value;
                if (index < -1)
                {
                    index = -1;
                }
                if (index >= _items.Count)
                {
                    index = _items.Count - 1;
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

        public object SelectedItem
        {
            get
            {
                if (_selected < 0 || _selected >= _items.Count)
                {
                    return null;
                }
                return _items[_selected];
            }
        }

        public void ClearItems()
        {
            _items.Clear();
            _selected = -1;
            CloseMenu();
            Invalidate();
        }

        public void AddItem(object item)
        {
            _items.Add(item);
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.SurfaceSunken;
            ForeColor = Theme.Text;
            if (_menu != null)
            {
                StyleMenu();
            }
            Invalidate();
        }

        private string TextFor(object item)
        {
            return item == null ? "" : item.ToString();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, Theme.S(9)))
            {
                using (SolidBrush brush = new SolidBrush(_hover || _open ? Theme.SurfaceMuted : Theme.SurfaceSunken))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(_hover || _open ? Theme.BorderStrong : Theme.Border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            int arrow = Theme.S(30);
            string text = _selected >= 0 && _selected < _items.Count
                ? TextFor(_items[_selected])
                : (_items.Count == 0 ? L.T("Aucune carte disponible") : "");

            Rectangle textRect = new Rectangle(Theme.S(12), 0,
                Math.Max(0, Width - arrow - Theme.S(16)), Height);
            TextRenderer.DrawText(g, text, Font, textRect,
                Enabled ? Theme.Text : Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                | TextFormatFlags.EndEllipsis);

            Rectangle chevron = new Rectangle(Width - arrow, (Height - Theme.S(16)) / 2, Theme.S(16), Theme.S(16));
            Glyphs.ChevronDown(g, chevron, Enabled ? Theme.TextMuted : Theme.TextFaint, Theme.S(1.6f));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
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
            if (_open)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled)
            {
                return;
            }
            if (e.KeyCode == Keys.Down && _selected < _items.Count - 1)
            {
                SelectedIndex = _selected + 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && _selected > 0)
            {
                SelectedIndex = _selected - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || (e.KeyCode == Keys.Enter && !_open))
            {
                OpenMenu();
                e.Handled = true;
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        private void OpenMenu()
        {
            if (_items.Count == 0)
            {
                return;
            }

            CloseMenu();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(0, Theme.S(6), 0, Theme.S(6));
            menu.Font = Font;
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Text;
            menu.DropShadowEnabled = true;

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(TextFor(_items[i]));
                item.AutoSize = false;
                item.Height = Theme.S(30);
                item.Width = Math.Max(Theme.S(180), Width - Theme.S(4));
                item.Padding = new Padding(Theme.S(10), 0, Theme.S(10), 0);
                item.BackColor = Theme.Surface;
                item.ForeColor = i == _selected ? Theme.Accent : Theme.Text;
                item.Font = i == _selected ? Theme.BodyBold : Theme.Body;
                item.Click += delegate
                {
                    SelectedIndex = index;
                };
                menu.Items.Add(item);
            }

            menu.Closed += delegate
            {
                _open = false;
                _menu = null;
                Invalidate();
            };

            _menu = menu;
            _open = true;
            StyleMenu();
            menu.Show(this, new Point(0, Height + Theme.S(4)));
            Invalidate();
        }

        private void StyleMenu()
        {
            if (_menu == null)
            {
                return;
            }
            _menu.BackColor = Theme.Surface;
            _menu.ForeColor = Theme.Text;
            _menu.Renderer = new MenuRenderer();
        }

        private void CloseMenu()
        {
            if (_menu != null)
            {
                _menu.Close();
                _menu = null;
            }
            _open = false;
        }

        private sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer()
                : base(new MenuColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    Rectangle rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(0, 0, e.Item.Width, e.Item.Height);
                if (e.Item.Selected && e.Item.Enabled)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.AccentSoft))
                    {
                        e.Graphics.FillRectangle(brush, rect);
                    }
                }
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color MenuItemBorder { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelected { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.AccentSoft; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.AccentSoft; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
        }
    }
}
