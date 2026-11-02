using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class ToggleSwitch : Control, IThemeAware
    {
        private bool _checked;
        private bool _hover;
        private float _position;
        private Timer _timer;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(Theme.S(46), Theme.S(26));
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value)
                {
                    return;
                }
                _checked = value;
                Invalidate();
                if (CheckedChanged != null)
                {
                    CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetCheckedSilent(bool value)
        {
            _checked = value;
            StopAnimation();
            _position = value ? 1f : 0f;
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

            int height = Math.Min(Height, Theme.S(26));
            int width = Math.Min(Width, Theme.S(46));
            int top = (Height - height) / 2;
            Rectangle track = new Rectangle(0, top, width - 1, height - 1);

            Color trackColor;
            if (!Enabled)
            {
                trackColor = _checked ? Color.FromArgb(160, Theme.Accent) : Theme.Border;
            }
            else
            {
                trackColor = _checked ? (_hover ? Theme.AccentHover : Theme.Accent) : (_hover ? Theme.BorderStrong : Color.FromArgb(226, 232, 240));
            }

            using (GraphicsPath path = Theme.RoundedRect(track, height / 2))
            using (SolidBrush brush = new SolidBrush(trackColor))
            {
                g.FillPath(brush, path);
            }

            int knob = height - Theme.S(4);
            float travel = width - knob - Theme.S(4);
            float x = Theme.S(2) + travel * _position;
            float y = top + Theme.S(2);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
            {
                g.FillEllipse(shadow, x, y + Theme.S(1), knob, knob);
            }
            using (SolidBrush brush = new SolidBrush(Enabled ? Color.White : Color.FromArgb(248, 250, 252)))
            {
                g.FillEllipse(brush, x, y, knob, knob);
            }
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
            Focus();
            if (Enabled && e.Button == MouseButtons.Left)
            {
                Toggle();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                Toggle();
                e.Handled = true;
            }
        }

        private void Toggle()
        {
            _checked = !_checked;
            AnimateTo(_checked ? 1f : 0f);
            if (CheckedChanged != null)
            {
                CheckedChanged(this, EventArgs.Empty);
            }
        }

        private void AnimateTo(float target)
        {
            StopAnimation();
            if (Theme.Dpi < 120f)
            {
                _position = target;
                Invalidate();
                return;
            }
            _timer = new Timer();
            _timer.Interval = 15;
            _timer.Tick += delegate
            {
                float delta = target - _position;
                if (Math.Abs(delta) <= 0.12f)
                {
                    _position = target;
                    StopAnimation();
                }
                else
                {
                    _position += delta * 0.35f;
                }
                Invalidate();
            };
            _timer.Start();
        }

        private void StopAnimation()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }
}
