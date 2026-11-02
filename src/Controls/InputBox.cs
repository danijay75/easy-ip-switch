using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class InputBox : Control, IThemeAware
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TextBox _text = new TextBox();
        private bool _focused;
        private bool _invalid;
        private bool _suppress;
        private bool _chevronHover;
        private Timer _invalidTimer;
        private int _invalidSince;
        private string _placeholder = "";
        private string[] _suggestions;

        public event EventHandler ValueChanged;
        public event EventHandler FocusLost;

        public InputBox()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Height = Theme.S(36);
            TabStop = false;

            _text.BorderStyle = BorderStyle.None;
            _text.Font = Theme.Body;
            _text.BackColor = Theme.Surface;
            _text.ForeColor = Theme.Text;
            _text.TabStop = true;
            _text.TextChanged += delegate
            {
                if (_suppress)
                {
                    return;
                }
                Invalidate();
                if (ValueChanged != null)
                {
                    ValueChanged(this, EventArgs.Empty);
                }
            };
            _text.GotFocus += delegate
            {
                _focused = true;
                Invalidate();
            };
            _text.LostFocus += delegate
            {
                _focused = false;
                Invalidate();
                if (FocusLost != null)
                {
                    FocusLost(this, EventArgs.Empty);
                }
            };
            _text.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!NumericOnly || char.IsControl(e.KeyChar))
                {
                    return;
                }
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.')
                {
                    e.Handled = true;
                }
            };
            Controls.Add(_text);
            Cursor = Cursors.IBeam;
        }

        public bool NumericOnly { get; set; }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                ApplyPlaceholder();
            }
        }

        public string[] Suggestions
        {
            get { return _suggestions; }
            set
            {
                _suggestions = value;
                Invalidate();
            }
        }

        public string Value
        {
            get { return _text.Text.Trim(); }
            set
            {
                _suppress = true;
                _text.Text = value == null ? "" : value;
                _suppress = false;
                Invalidate();
            }
        }

        public bool Invalid
        {
            get { return _invalid; }
            set
            {
                if (_invalid != value)
                {
                    _invalid = value;
                    if (value)
                    {
                        _invalidSince = Environment.TickCount;
                    }
                    UpdateInvalidAnimation();
                    Invalidate();
                }
            }
        }

        public void FocusText()
        {
            if (!Enabled)
            {
                return;
            }
            _text.Focus();
            _text.SelectionStart = _text.TextLength;
        }

        public bool ReadOnly
        {
            get { return _text.ReadOnly; }
            set
            {
                _text.ReadOnly = value;
                _text.ForeColor = value ? Theme.TextMuted : Theme.Text;
                _text.BackColor = value ? Theme.SurfaceMuted : Theme.Surface;
                Invalidate();
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            _text.BackColor = _text.ReadOnly ? Theme.SurfaceMuted : Theme.Surface;
            _text.ForeColor = _text.ReadOnly ? Theme.TextMuted : Theme.Text;
            Invalidate();
        }

        private bool HasChevron
        {
            get { return _suggestions != null && _suggestions.Length > 0 && Enabled && !_text.ReadOnly; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyPlaceholder();
            PositionText();
            UpdateInvalidAnimation();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_invalidTimer != null)
            {
                _invalidTimer.Stop();
            }
            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateInvalidAnimation();
        }

        private void ApplyPlaceholder()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            SendMessage(_text.Handle, EM_SETCUEBANNER, IntPtr.Zero, _placeholder);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _text.Enabled = Enabled;
            Cursor = Enabled ? Cursors.IBeam : Cursors.Default;
            UpdateInvalidAnimation();
            Invalidate();
        }

        private void UpdateInvalidAnimation()
        {
            if (!_invalid || !Enabled || !Visible || !IsHandleCreated || IsDisposed || Disposing)
            {
                if (_invalidTimer != null)
                {
                    _invalidTimer.Stop();
                }
                return;
            }

            if (_invalidTimer == null)
            {
                _invalidTimer = new Timer();
                _invalidTimer.Interval = 80;
                _invalidTimer.Tick += delegate { Invalidate(); };
            }
            if (!_invalidTimer.Enabled)
            {
                _invalidTimer.Start();
            }
        }

        internal static Color InvalidBorderColor(int elapsedMilliseconds)
        {
            double phase = ((uint)elapsedMilliseconds % 2200u) / 2200.0 * Math.PI * 2.0;
            float strength = 0.48f + (float)((Math.Sin(phase) + 1.0) * 0.15);
            Color background = Theme.Surface;
            Color danger = Theme.Danger;
            return Color.FromArgb(
                (int)Math.Round(background.R + (danger.R - background.R) * strength),
                (int)Math.Round(background.G + (danger.G - background.G) * strength),
                (int)Math.Round(background.B + (danger.B - background.B) * strength));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _invalidTimer != null)
            {
                _invalidTimer.Stop();
                _invalidTimer.Dispose();
                _invalidTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionText();
        }

        private void PositionText()
        {
            int left = Theme.S(11);
            int right = Theme.S(11) + (HasChevron ? Theme.S(22) : 0);
            int height = _text.PreferredHeight;
            int width = Math.Max(Theme.S(10), Width - left - right);
            _text.SetBounds(left, Math.Max(0, (Height - height) / 2), width, height);
        }

        private Rectangle ChevronRect
        {
            get { return new Rectangle(Width - Theme.S(26), 0, Theme.S(26), Height); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool active = Enabled;
            Color fill = !active ? Theme.SurfaceSunken : (_text.ReadOnly ? Theme.SurfaceMuted : Theme.Surface);
            Color border = _invalid
                ? InvalidBorderColor(unchecked(Environment.TickCount - _invalidSince))
                : _focused ? Theme.Accent : Theme.Border;

            using (GraphicsPath path = Theme.RoundedRect(rect, Theme.S(8)))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(border, _invalid ? Math.Max(1, Theme.S(1.3f)) : _focused ? 2f : 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (HasChevron)
            {
                Rectangle chevron = ChevronRect;
                Glyphs.ChevronDown(g, chevron, _chevronHover ? Theme.Accent : Theme.TextFaint, 1.6f);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = HasChevron && ChevronRect.Contains(e.Location);
            if (over != _chevronHover)
            {
                _chevronHover = over;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_chevronHover)
            {
                _chevronHover = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled)
            {
                return;
            }
            if (HasChevron && ChevronRect.Contains(e.Location))
            {
                ShowSuggestions();
                return;
            }
            _text.Focus();
        }

        private void ShowSuggestions()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Font = Theme.Body;
            menu.BackColor = Theme.Surface;
            menu.Renderer = new ToolStripProfessionalRenderer(new CleanColorTable());
            menu.Padding = new Padding(Theme.S(4));

            foreach (string suggestion in _suggestions)
            {
                string value = suggestion;
                ToolStripMenuItem item = new ToolStripMenuItem(value);
                item.Padding = new Padding(Theme.S(6), Theme.S(2), Theme.S(6), Theme.S(2));
                item.Click += delegate
                {
                    Value = value;
                    Invalid = false;
                    _text.Focus();
                    _text.SelectionStart = _text.TextLength;
                    if (ValueChanged != null)
                    {
                        ValueChanged(this, EventArgs.Empty);
                    }
                };
                menu.Items.Add(item);
            }

            menu.Show(this, new Point(0, Height + Theme.S(2)));
        }

        private sealed class CleanColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected
            {
                get { return Theme.SurfaceSunken; }
            }

            public override Color MenuItemBorder
            {
                get { return Theme.SurfaceSunken; }
            }

            public override Color ToolStripDropDownBackground
            {
                get { return Theme.Surface; }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Theme.Surface; }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Theme.Surface; }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Theme.Surface; }
            }
        }
    }
}
