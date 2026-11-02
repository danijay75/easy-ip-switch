using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    /// <summary>Boîte de dialogue thématisée à trois choix.</summary>
    internal sealed class ConfirmDialog : Form, IThemeAware
    {
        public const int ChoicePrimary = 1;
        public const int ChoiceSecondary = 2;
        public const int ChoiceStay = 0;

        private readonly Label _lblTitle = new Label();
        private readonly Label _lblBody = new Label();
        private readonly Label _lblBadge = new Label();
        private readonly FlatButton _btnPrimary = new FlatButton();
        private readonly FlatButton _btnSecondary = new FlatButton();
        private readonly FlatButton _btnStay = new FlatButton();

        private int _choice;

        private ConfirmDialog(string title, string message, string primaryText, string secondaryText, string stayText)
        {
            Text = AppInfo.ProductName;
            Font = Theme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _lblBadge.AutoSize = false;
            _lblBadge.Text = "!";
            _lblBadge.Font = Theme.Title;
            _lblBadge.TextAlign = ContentAlignment.MiddleCenter;
            _lblBadge.ForeColor = Theme.Warning;
            _lblBadge.BackColor = Theme.WarningSoft;
            Controls.Add(_lblBadge);

            _lblTitle.AutoSize = false;
            _lblTitle.Font = Theme.CardTitle;
            _lblTitle.ForeColor = Theme.Text;
            _lblTitle.BackColor = Theme.Surface;
            _lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblTitle.UseMnemonic = false;
            _lblTitle.Text = title;
            Controls.Add(_lblTitle);

            _lblBody.AutoSize = false;
            _lblBody.Font = Theme.Body;
            _lblBody.ForeColor = Theme.TextMuted;
            _lblBody.BackColor = Theme.Surface;
            _lblBody.TextAlign = ContentAlignment.TopLeft;
            _lblBody.UseMnemonic = false;
            _lblBody.Text = message;
            Controls.Add(_lblBody);

            if (!string.IsNullOrEmpty(primaryText))
            {
                SetupButton(_btnPrimary, ButtonKind.Primary, primaryText);
                _btnPrimary.Click += delegate { Finish(ChoicePrimary); };
                Controls.Add(_btnPrimary);
            }

            if (!string.IsNullOrEmpty(secondaryText))
            {
                SetupButton(_btnSecondary, ButtonKind.Secondary, secondaryText);
                _btnSecondary.Click += delegate { Finish(ChoiceSecondary); };
                Controls.Add(_btnSecondary);
            }

            if (!string.IsNullOrEmpty(stayText))
            {
                SetupButton(_btnStay, ButtonKind.Ghost, stayText);
                _btnStay.Click += delegate { Finish(ChoiceStay); };
                Controls.Add(_btnStay);
                AcceptButton = _btnStay;
                CancelButton = _btnStay;
            }
            else if (!string.IsNullOrEmpty(primaryText))
            {
                AcceptButton = _btnPrimary;
            }

            ClientSize = new Size(Theme.S(640), Theme.S(320));
            LayoutDialog();
            Theme.Changed += ThemeChangedHandler;
        }

        private void ThemeChangedHandler(object sender, EventArgs e)
        {
            ApplyTheme();
            Invalidate(true);
        }

        private static void SetupButton(FlatButton button, ButtonKind kind, string text)
        {
            button.Kind = kind;
            button.Text = text;
            button.Height = Theme.S(38);
        }

        public static int Show(IWin32Window owner, string title, string message,
            string primaryText, string secondaryText, string stayText)
        {
            using (ConfirmDialog dialog = new ConfirmDialog(title, message, primaryText, secondaryText, stayText))
            {
                dialog.ShowDialog(owner);
                return dialog._choice == 0 ? ChoiceStay : dialog._choice;
            }
        }

        private void Finish(int choice)
        {
            _choice = choice;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= ThemeChangedHandler;
            base.OnFormClosed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Recalculer après l'affichage, y compris à forte mise à l'échelle.
            LayoutDialog();
            if (_btnStay.Parent == this)
            {
                _btnStay.Focus();
            }
            else if (_btnPrimary.Parent == this)
            {
                _btnPrimary.Focus();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutDialog();
        }

        private void LayoutDialog()
        {
            if (_btnStay == null)
            {
                return;
            }

            int pad = Theme.S(26);
            int top = Theme.S(24);

            _lblBadge.SetBounds(pad, top, Theme.S(34), Theme.S(34));

            _lblTitle.SetBounds(pad + Theme.S(46), top, ClientSize.Width - pad * 2 - Theme.S(46), Theme.S(34));

            int bodyTop = top + Theme.S(46);
            int buttonsHeight = Theme.S(38);
            int buttonsTop = ClientSize.Height - pad - buttonsHeight;
            _lblBody.SetBounds(pad, bodyTop, ClientSize.Width - pad * 2,
                Math.Max(Theme.S(40), buttonsTop - bodyTop - Theme.S(16)));

            int gap = Theme.S(10);
            int right = ClientSize.Width - pad;

            FlatButton[] buttons = new FlatButton[] { _btnStay, _btnSecondary, _btnPrimary };
            for (int i = 0; i < buttons.Length; i++)
            {
                FlatButton button = buttons[i];
                // Visible dépend aussi de la visibilité de la fenêtre parente.
                // Pendant la construction, elle est encore masquée : tester
                // plutôt si ce bouton facultatif appartient à la boîte.
                if (button.Parent != this)
                {
                    continue;
                }
                button.Width = MeasureWidth(button.Text) + Theme.S(28);
                button.SetBounds(right - button.Width, buttonsTop, button.Width, buttonsHeight);
                right = button.Left - gap;
            }
        }

        private static int MeasureWidth(string text)
        {
            Size size = TextRenderer.MeasureText(text ?? "", Theme.ButtonFont, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return size.Width;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                e.Graphics.DrawLine(pen, 0, ClientSize.Height - Theme.S(62), ClientSize.Width, ClientSize.Height - Theme.S(62));
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            _lblBadge.BackColor = Theme.WarningSoft;
            _lblBadge.ForeColor = Theme.Warning;
            _lblTitle.BackColor = Theme.Surface;
            _lblTitle.ForeColor = Theme.Text;
            _lblBody.BackColor = Theme.Surface;
            _lblBody.ForeColor = Theme.TextMuted;
            Invalidate();
        }
    }
}
