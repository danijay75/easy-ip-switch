using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost
    }

    internal sealed class FlatButton : Button, IThemeAware
    {
        private bool _hover;
        private bool _pressed;
        private ButtonKind _kind = ButtonKind.Primary;
        private Icon _systemIcon;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Surface;
            Font = Theme.ButtonFont;
            Cursor = Cursors.Hand;
            Height = Theme.S(40);
        }

        public ButtonKind Kind
        {
            get { return _kind; }
            set
            {
                _kind = value;
                Invalidate();
            }
        }

        public bool ShowRefreshIcon { get; set; }

        // The button owns the cloned Windows icon and disposes it with itself.
        public Icon SystemIcon
        {
            get { return _systemIcon; }
            set
            {
                if (object.ReferenceEquals(_systemIcon, value))
                {
                    return;
                }
                if (_systemIcon != null)
                {
                    _systemIcon.Dispose();
                }
                _systemIcon = value;
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _systemIcon != null)
            {
                _systemIcon.Dispose();
                _systemIcon = null;
            }
            base.Dispose(disposing);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
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
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Theme.S(9);
            Color fill;
            Color border = Color.Empty;
            Color text;

            if (_kind == ButtonKind.Primary)
            {
                fill = !Enabled ? Theme.AccentSoft : _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent;
                text = !Enabled ? Color.FromArgb(170, Color.White) : Color.White;
            }
            else if (_kind == ButtonKind.Secondary)
            {
                fill = !Enabled ? Theme.SurfaceMuted : _pressed ? Theme.SurfaceSunken : _hover ? Theme.SurfaceMuted : Theme.Surface;
                border = _hover && Enabled ? Theme.BorderStrong : Theme.Border;
                text = !Enabled ? Theme.TextFaint : Theme.Text;
            }
            else
            {
                fill = !Enabled ? Color.Empty : _pressed ? Theme.SurfaceSunken : _hover ? Theme.SurfaceMuted : Color.Empty;
                text = !Enabled ? Theme.TextFaint : Theme.TextMuted;
            }

            if (fill != Color.Empty)
            {
                using (GraphicsPath path = Theme.RoundedRect(rect, radius))
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
            }

            if (border != Color.Empty)
            {
                using (GraphicsPath path = Theme.RoundedRect(rect, radius))
                using (Pen pen = new Pen(border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            Size textSize = TextRenderer.MeasureText(Text, Font, new Size(1000, Height), TextFormatFlags.NoPadding);
            bool hasIcon = ShowRefreshIcon || _systemIcon != null;
            int iconSize = _systemIcon != null ? Theme.S(20) : Theme.S(14);
            int gap = Theme.S(8);
            int contentWidth = textSize.Width + (hasIcon ? iconSize + gap : 0);
            int x = Math.Max(Theme.S(8), (Width - contentWidth) / 2);

            if (ShowRefreshIcon)
            {
                Glyphs.Refresh(g, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize), text, Theme.S(1.5f));
                x += iconSize + gap;
            }
            else if (_systemIcon != null)
            {
                g.DrawIcon(_systemIcon, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize));
                x += iconSize + gap;
            }

            Rectangle textRect = new Rectangle(x, 0, Math.Max(0, Width - x - Theme.S(4)), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(rect, -Theme.S(4), -Theme.S(4));
                ControlPaint.DrawFocusRectangle(g, focus, text, fill == Color.Empty ? Theme.Surface : fill);
            }
        }
    }

    internal sealed class Pill : Control, IThemeAware
    {
        private Color _fill = Theme.SuccessSoft;
        private Color _text = Theme.Success;

        public Pill()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            Font = Theme.SmallBold;
            BackColor = Theme.Surface;
            Height = Theme.S(30);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
        }

        public void SetColors(Color fill, Color text)
        {
            _fill = fill;
            _text = text;
            Invalidate();
        }

        public void AutoFit()
        {
            Size measured = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Width = Theme.S(10) + Theme.S(6) + Theme.S(6) + measured.Width + Theme.S(10);
            Height = Theme.S(30);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height / 2;
            using (GraphicsPath path = Theme.RoundedRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(_fill))
            {
                g.FillPath(brush, path);
            }

            Rectangle dot = new Rectangle(Theme.S(10), (Height - Theme.S(6)) / 2, Theme.S(6), Theme.S(6));
            using (SolidBrush brush = new SolidBrush(_text))
            {
                g.FillEllipse(brush, dot);
            }

            int textLeft = dot.Right + Theme.S(6);
            Rectangle textRect = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft - Theme.S(10)), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, _text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
