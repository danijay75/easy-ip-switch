using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    /// <summary>
    /// Confirmation de fermeture avec un rappel de don. Le QR code et le lien
    /// ouvrent la page PayPal ; les boutons restent ceux de la fermeture
    /// habituelle, avec un seul clic pour quitter lorsque rien n'est en attente.
    /// </summary>
    internal sealed class QuitDialog : Form, IThemeAware
    {
        public const int ChoicePrimary = 1;
        public const int ChoiceSecondary = 2;
        public const int ChoiceStay = 0;

        private readonly Label _lblBadge = new Label();
        private readonly Label _lblTitle = new Label();
        private readonly Label _lblBody = new Label();
        private readonly Label _lblDonateTitle = new Label();
        private readonly Label _lblDonateText = new Label();
        private readonly PictureBox _qr = new PictureBox();
        private readonly LinkLabel _lnkDonate = new LinkLabel();
        private readonly FlatButton _btnPrimary = new FlatButton();
        private readonly FlatButton _btnSecondary = new FlatButton();
        private readonly FlatButton _btnStay = new FlatButton();

        private readonly bool _pendingChanges;
        private Image _qrImage;
        private int _choice;

        private QuitDialog(string title, string message, string primaryText, string secondaryText, string stayText)
        {
            _pendingChanges = !string.IsNullOrEmpty(secondaryText);
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
            _lblBadge.Text = _pendingChanges ? "!" : "♥";
            _lblBadge.Font = Theme.Title;
            _lblBadge.TextAlign = ContentAlignment.MiddleCenter;
            _lblBadge.ForeColor = _pendingChanges ? Theme.Warning : Theme.Accent;
            _lblBadge.BackColor = _pendingChanges ? Theme.WarningSoft : Theme.AccentSoft;
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

            _lblDonateTitle.AutoSize = false;
            _lblDonateTitle.Font = Theme.SmallBold;
            _lblDonateTitle.ForeColor = Theme.Text;
            _lblDonateTitle.BackColor = Theme.Surface;
            _lblDonateTitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblDonateTitle.UseMnemonic = false;
            _lblDonateTitle.Text = L.T("Soutenir Easy IP-Switch");
            Controls.Add(_lblDonateTitle);

            _lblDonateText.AutoSize = false;
            _lblDonateText.Font = Theme.Small;
            _lblDonateText.ForeColor = Theme.TextMuted;
            _lblDonateText.BackColor = Theme.Surface;
            _lblDonateText.TextAlign = ContentAlignment.TopLeft;
            _lblDonateText.UseMnemonic = false;
            _lblDonateText.Text = L.T("Scannez ce QR code ou cliquez dessus pour faire un don via PayPal.");
            Controls.Add(_lblDonateText);

            _qr.SizeMode = PictureBoxSizeMode.Zoom;
            _qr.BackColor = Color.White;
            _qr.BorderStyle = BorderStyle.FixedSingle;
            _qr.Cursor = Cursors.Hand;
            _qr.TabStop = false;
            _qrImage = LoadQrImage();
            if (_qrImage != null)
            {
                _qr.Image = _qrImage;
                _qr.AccessibleName = L.T("QR code pour faire un don");
                _qr.AccessibleDescription = L.T("Ouvre la page de don PayPal.");
                _qr.Click += delegate { OpenDonation(); };
                Controls.Add(_qr);
            }
            else
            {
                _lblDonateText.Text = L.T("Ouvrez la page de don ci-dessous pour soutenir le projet.");
            }

            _lnkDonate.AutoSize = false;
            _lnkDonate.Font = Theme.SmallBold;
            _lnkDonate.ForeColor = Theme.Accent;
            _lnkDonate.LinkColor = Theme.Accent;
            _lnkDonate.ActiveLinkColor = Theme.AccentPressed;
            _lnkDonate.VisitedLinkColor = Theme.Accent;
            _lnkDonate.LinkBehavior = LinkBehavior.HoverUnderline;
            _lnkDonate.BackColor = Theme.Surface;
            _lnkDonate.UseMnemonic = false;
            _lnkDonate.TextAlign = ContentAlignment.MiddleLeft;
            _lnkDonate.Text = L.T("Ouvrir la page de don");
            _lnkDonate.LinkClicked += delegate { OpenDonation(); };
            Controls.Add(_lnkDonate);

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

            ClientSize = new Size(Theme.S(760), Theme.S(440));
            LayoutDialog();
            Theme.Changed += ThemeChangedHandler;
        }

        public static int Show(IWin32Window owner, string title, string message,
            string primaryText, string secondaryText, string stayText)
        {
            using (QuitDialog dialog = new QuitDialog(title, message, primaryText, secondaryText, stayText))
            {
                dialog.ShowDialog(owner);
                return dialog._choice == 0 ? ChoiceStay : dialog._choice;
            }
        }

        private static Image LoadQrImage()
        {
            try
            {
                using (Stream stream = typeof(QuitDialog).Assembly.GetManifestResourceStream("EasyIpSwitch.DonationQr.png"))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Image image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static void SetupButton(FlatButton button, ButtonKind kind, string text)
        {
            button.Kind = kind;
            button.Text = text;
            button.Height = Theme.S(38);
        }

        private void OpenDonation()
        {
            try
            {
                Process.Start(new ProcessStartInfo(AppInfo.DonationUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    L.F("Impossible d'ouvrir la page de don :\r\n\r\n{0}", ex.Message),
                    AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Finish(int choice)
        {
            _choice = choice;
            Close();
        }

        private void ThemeChangedHandler(object sender, EventArgs e)
        {
            ApplyTheme();
            Invalidate(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= ThemeChangedHandler;
            _qr.Image = null;
            if (_qrImage != null)
            {
                _qrImage.Dispose();
                _qrImage = null;
            }
            base.OnFormClosed(e);
        }

        private void LayoutDialog()
        {
            if (_btnStay == null || _lblBody == null)
            {
                return;
            }

            int pad = Theme.S(26);
            int top = Theme.S(24);
            _lblBadge.SetBounds(pad, top, Theme.S(34), Theme.S(34));
            _lblTitle.SetBounds(pad + Theme.S(46), top,
                ClientSize.Width - pad * 2 - Theme.S(46), Theme.S(34));

            int bodyTop = top + Theme.S(46);
            int buttonsHeight = Theme.S(38);
            int buttonsTop = ClientSize.Height - pad - buttonsHeight;
            int contentBottom = buttonsTop - Theme.S(18);

            int donateWidth = Theme.S(250);
            int donateLeft = ClientSize.Width - pad - donateWidth;
            int bodyWidth = Math.Max(Theme.S(200), donateLeft - Theme.S(20) - pad);
            _lblBody.SetBounds(pad, bodyTop, bodyWidth,
                Math.Max(Theme.S(80), contentBottom - bodyTop));

            _lblDonateTitle.SetBounds(donateLeft, bodyTop, donateWidth, Theme.S(20));
            _lblDonateText.SetBounds(donateLeft, bodyTop + Theme.S(24), donateWidth, Theme.S(44));

            int qrSize = Theme.S(170);
            int qrTop = bodyTop + Theme.S(74);
            if (_qr.Parent == this)
            {
                _qr.SetBounds(donateLeft + (donateWidth - qrSize) / 2, qrTop, qrSize, qrSize);
                _lnkDonate.SetBounds(donateLeft, qrTop + qrSize + Theme.S(6), donateWidth, Theme.S(20));
            }
            else
            {
                _lnkDonate.SetBounds(donateLeft, bodyTop + Theme.S(80), donateWidth, Theme.S(20));
            }

            int gap = Theme.S(10);
            int right = ClientSize.Width - pad;
            FlatButton[] buttons = new FlatButton[] { _btnStay, _btnSecondary, _btnPrimary };
            for (int i = 0; i < buttons.Length; i++)
            {
                FlatButton button = buttons[i];
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
                int y = ClientSize.Height - Theme.S(26) - Theme.S(38) - Theme.S(14);
                e.Graphics.DrawLine(pen, 0, y, ClientSize.Width, y);
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            _lblBadge.BackColor = _pendingChanges ? Theme.WarningSoft : Theme.AccentSoft;
            _lblBadge.ForeColor = _pendingChanges ? Theme.Warning : Theme.Accent;
            _lblTitle.BackColor = Theme.Surface;
            _lblTitle.ForeColor = Theme.Text;
            _lblBody.BackColor = Theme.Surface;
            _lblBody.ForeColor = Theme.TextMuted;
            _lblDonateTitle.BackColor = Theme.Surface;
            _lblDonateTitle.ForeColor = Theme.Text;
            _lblDonateText.BackColor = Theme.Surface;
            _lblDonateText.ForeColor = Theme.TextMuted;
            _lnkDonate.BackColor = Theme.Surface;
            _lnkDonate.ForeColor = Theme.Accent;
            _lnkDonate.LinkColor = Theme.Accent;
            _lnkDonate.ActiveLinkColor = Theme.AccentPressed;
            _lnkDonate.VisitedLinkColor = Theme.Accent;
            Invalidate();
        }
    }
}
