using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EasyIpSwitch
{
    internal interface ITermsConsentStore
    {
        bool IsAccepted();
        void SaveAccepted(bool accepted);
    }

    internal sealed class RegistryTermsConsentStore : ITermsConsentStore
    {
        public const string DefaultRegistryPath = @"Software\Angeli Visions\Easy IP-Switch";
        public const string ValueName = "TermsAccepted";

        private readonly string _registryPath;
        private readonly string _filePath;

        public RegistryTermsConsentStore()
            : this(DefaultRegistryPath, DefaultFilePath())
        {
        }

        internal RegistryTermsConsentStore(string registryPath, string filePath)
        {
            _registryPath = registryPath;
            _filePath = filePath;
        }

        private static string DefaultFilePath()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    return Path.Combine(Path.Combine(localAppData, "Angeli Visions"), @"Easy IP-Switch\terms-accepted.txt");
                }
            }
            catch
            {
            }
            return null;
        }

        public bool IsAccepted()
        {
            if (!string.IsNullOrEmpty(_registryPath))
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryPath, false))
                    {
                        if (key != null)
                        {
                            object value = key.GetValue(ValueName);
                            if (value is int && (int)value == 1)
                            {
                                return true;
                            }
                            string text = value as string;
                            if (string.Equals(text, "1", StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(_filePath))
            {
                try
                {
                    if (File.Exists(_filePath))
                    {
                        string content = File.ReadAllText(_filePath).Trim();
                        return string.Equals(content, "1", StringComparison.Ordinal);
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        public void SaveAccepted(bool accepted)
        {
            if (!string.IsNullOrEmpty(_registryPath))
            {
                try
                {
                    if (accepted)
                    {
                        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_registryPath))
                        {
                            if (key != null)
                            {
                                key.SetValue(ValueName, 1, RegistryValueKind.DWord);
                            }
                        }
                    }
                    else
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryPath, true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue(ValueName, false);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(_filePath))
            {
                try
                {
                    if (accepted)
                    {
                        string directory = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        File.WriteAllText(_filePath, "1");
                    }
                    else if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                }
                catch
                {
                }
            }
        }
    }

    internal static class TermsConsent
    {
        public static bool EnsureAccepted(IWin32Window owner)
        {
            return EnsureAccepted(owner, new RegistryTermsConsentStore(), TermsDialog.Show);
        }

        internal static bool EnsureAccepted(IWin32Window owner, ITermsConsentStore store, Func<IWin32Window, int> prompt)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }
            if (prompt == null)
            {
                throw new ArgumentNullException("prompt");
            }

            if (store.IsAccepted())
            {
                return true;
            }

            int choice = prompt(owner);
            bool accepted = choice == TermsDialog.ChoiceAccept;
            store.SaveAccepted(accepted);
            return accepted;
        }
    }

    /// <summary>
    /// Fenêtre présentée au lancement pour accepter ou refuser en un clic les
    /// conditions d'utilisation et la notice RGPD.
    /// </summary>
    internal sealed class TermsDialog : Form, IThemeAware
    {
        public const int ChoiceDecline = 0;
        public const int ChoiceAccept = 1;

        internal const string FrenchAgreementKey =
            "1. Objet et licence (MIT)\r\n"
            + "Easy IP-Switch, édité par Angeli Visions, permet de consulter et de modifier localement la configuration IPv4 des cartes réseau Windows (DHCP, adresse fixe, masque, passerelle, activation) et d'effectuer des tests de ping.\r\n\r\n"
            + "2. Responsabilité d'utilisation\r\n"
            + "L'application modifie directement les paramètres réseau de ce poste. Vous êtes seul responsable des configurations appliquées ; des réglages inadaptés peuvent interrompre votre accès au réseau ou à Internet. Le logiciel est fourni « en l'état », sans garantie.\r\n\r\n"
            + "3. Protection des données et respect du RGPD (UE 2016/679)\r\n"
            + "• Aucune collecte : Easy IP-Switch fonctionne 100 % en local. Aucune donnée personnelle, adresse IP ou télémétrie n'est collectée ni transmise à Angeli Visions ou à un tiers.\r\n"
            + "• Stockage local uniquement : seuls la langue choisie et votre accord sont enregistrés dans votre session Windows locale, ainsi qu'un journal local en cas d'erreur technique.\r\n"
            + "• Liens externes : le site officiel et la page de don PayPal ne s'ouvrent que sur clic explicite de votre part.\r\n\r\n"
            + "4. Votre choix\r\n"
            + "Cliquez sur « J'accepte » pour mémoriser votre accord et ouvrir l'application, ou sur « Je refuse » pour fermer l'application (votre accord vous sera redemandé au prochain lancement).";

        private readonly Label _lblBadge = new Label();
        private readonly Label _lblTitle = new Label();
        private readonly LanguageSelector _languageSelector = new LanguageSelector();
        private readonly Label _lblIntro = new Label();
        private readonly Panel _agreementFrame = new Panel();
        private readonly Panel _scrollPanel = new Panel();
        private readonly Label _lblAgreement = new Label();
        private readonly Label _lblPrivacyNote = new Label();
        private readonly FlatButton _btnDecline = new FlatButton();
        private readonly FlatButton _btnAccept = new FlatButton();
        private readonly ToolTip _tip = new ToolTip();

        private bool _suppressLanguage;
        private int _choice = ChoiceDecline;

        internal TermsDialog()
            : this(false)
        {
        }

        private TermsDialog(bool hasOwner)
        {
            Text = AppInfo.ProductName + " " + AppInfo.VersionLabel;
            Font = Theme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = !hasOwner;
            StartPosition = hasOwner ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
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
            _lblBadge.Text = "§";
            _lblBadge.Font = Theme.Title;
            _lblBadge.TextAlign = ContentAlignment.MiddleCenter;
            _lblBadge.ForeColor = Theme.Accent;
            _lblBadge.BackColor = Theme.AccentSoft;
            Controls.Add(_lblBadge);

            _lblTitle.AutoSize = false;
            _lblTitle.Font = Theme.CardTitle;
            _lblTitle.ForeColor = Theme.Text;
            _lblTitle.BackColor = Theme.Surface;
            _lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            _lblTitle.UseMnemonic = false;
            Controls.Add(_lblTitle);

            _languageSelector.SetSelectedSilent(L.Current);
            _languageSelector.SelectedLanguageChanged += LanguageSelectionChanged;
            Controls.Add(_languageSelector);

            _lblIntro.AutoSize = false;
            _lblIntro.Font = Theme.Body;
            _lblIntro.ForeColor = Theme.TextMuted;
            _lblIntro.BackColor = Theme.Surface;
            _lblIntro.TextAlign = ContentAlignment.MiddleLeft;
            _lblIntro.UseMnemonic = false;
            Controls.Add(_lblIntro);

            _agreementFrame.BackColor = Theme.SurfaceMuted;
            _agreementFrame.Paint += AgreementFramePaint;
            Controls.Add(_agreementFrame);

            _scrollPanel.BackColor = Theme.SurfaceMuted;
            _scrollPanel.AutoScroll = true;
            _agreementFrame.Controls.Add(_scrollPanel);

            _lblAgreement.AutoSize = false;
            _lblAgreement.Font = Theme.Body;
            _lblAgreement.ForeColor = Theme.Text;
            _lblAgreement.BackColor = Theme.SurfaceMuted;
            _lblAgreement.TextAlign = ContentAlignment.TopLeft;
            _lblAgreement.UseMnemonic = false;
            _lblAgreement.Click += delegate { _scrollPanel.Focus(); };
            _scrollPanel.Controls.Add(_lblAgreement);

            _lblPrivacyNote.AutoSize = false;
            _lblPrivacyNote.Font = Theme.Small;
            _lblPrivacyNote.ForeColor = Theme.TextMuted;
            _lblPrivacyNote.BackColor = Theme.Surface;
            _lblPrivacyNote.TextAlign = ContentAlignment.MiddleLeft;
            _lblPrivacyNote.AutoEllipsis = true;
            _lblPrivacyNote.UseMnemonic = false;
            Controls.Add(_lblPrivacyNote);

            SetupButton(_btnDecline, ButtonKind.Secondary);
            _btnDecline.Click += delegate { Finish(ChoiceDecline); };
            Controls.Add(_btnDecline);

            SetupButton(_btnAccept, ButtonKind.Primary);
            _btnAccept.Click += delegate { Finish(ChoiceAccept); };
            Controls.Add(_btnAccept);

            AcceptButton = _btnAccept;
            CancelButton = _btnDecline;

            ClientSize = new Size(Theme.S(720), Theme.S(456));
            ApplyLanguage();
            Theme.Changed += ThemeChangedHandler;
        }

        public new static int Show(IWin32Window owner)
        {
            using (TermsDialog dialog = new TermsDialog(owner != null))
            {
                if (owner != null)
                {
                    dialog.ShowDialog(owner);
                }
                else
                {
                    dialog.ShowDialog();
                }
                return dialog._choice;
            }
        }

        internal static string AgreementText()
        {
            return L.T(FrenchAgreementKey);
        }

        private static void SetupButton(FlatButton button, ButtonKind kind)
        {
            button.Kind = kind;
            button.Height = Theme.S(38);
        }

        private void LanguageSelectionChanged(object sender, EventArgs e)
        {
            if (_suppressLanguage)
            {
                return;
            }
            L.SetLanguage(_languageSelector.SelectedLanguage);
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            _suppressLanguage = true;
            try
            {
                _languageSelector.SetSelectedSilent(L.Current);
                _languageSelector.AccessibleName = L.T("Langue");
                _languageSelector.AccessibleDescription = L.T("Changer la langue de l'application");
            }
            finally
            {
                _suppressLanguage = false;
            }

            Text = AppInfo.ProductName + " " + AppInfo.VersionLabel;
            _lblTitle.Text = L.T("Conditions d'utilisation et RGPD");
            _lblIntro.Text = L.T("Veuillez lire et accepter les présentes conditions pour utiliser Easy IP-Switch.");
            _lblAgreement.Text = AgreementText();
            _lblPrivacyNote.Text = L.T("100 % local · Aucune donnée personnelle collectée");
            _btnDecline.Text = L.T("Je refuse");
            _btnAccept.Text = L.T("J'accepte");
            _tip.SetToolTip(_languageSelector, L.T("Changer la langue de l'application"));

            LayoutDialog();
            Invalidate(true);
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
            _btnAccept.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutDialog();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_scrollPanel.Focused && _scrollPanel.VerticalScroll.Visible)
            {
                int step = Theme.S(36);
                int delta = e.Delta > 0 ? -step : step;
                int target = _scrollPanel.VerticalScroll.Value + delta;
                int max = Math.Max(0, _scrollPanel.VerticalScroll.Maximum - _scrollPanel.ClientSize.Height + 1);
                if (target < _scrollPanel.VerticalScroll.Minimum)
                {
                    target = _scrollPanel.VerticalScroll.Minimum;
                }
                if (target > max)
                {
                    target = max;
                }
                _scrollPanel.AutoScrollPosition = new Point(0, target);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= ThemeChangedHandler;
            base.OnFormClosed(e);
        }

        private void LayoutDialog()
        {
            if (_btnAccept == null || _lblAgreement == null)
            {
                return;
            }

            int pad = Theme.S(26);
            int top = Theme.S(22);

            _lblBadge.SetBounds(pad, top, Theme.S(34), Theme.S(34));
            _languageSelector.SetBounds(ClientSize.Width - pad - Theme.S(82), top, Theme.S(82), Theme.S(34));

            int titleLeft = pad + Theme.S(46);
            int titleWidth = Math.Max(Theme.S(160), _languageSelector.Left - titleLeft - Theme.S(12));
            _lblTitle.SetBounds(titleLeft, top, titleWidth, Theme.S(34));

            int introTop = top + Theme.S(42);
            _lblIntro.SetBounds(pad, introTop, ClientSize.Width - pad * 2, Theme.S(22));

            int buttonsHeight = Theme.S(38);
            int buttonsTop = ClientSize.Height - pad - buttonsHeight;
            int separatorY = buttonsTop - Theme.S(14);

            int frameTop = introTop + Theme.S(28);
            int frameHeight = Math.Max(Theme.S(120), separatorY - Theme.S(14) - frameTop);
            _agreementFrame.SetBounds(pad, frameTop, ClientSize.Width - pad * 2, frameHeight);
            _scrollPanel.SetBounds(1, 1, Math.Max(0, _agreementFrame.Width - 2), Math.Max(0, _agreementFrame.Height - 2));

            int innerPad = Theme.S(14);
            int textWidth = Math.Max(
                Theme.S(200),
                _scrollPanel.Width - SystemInformation.VerticalScrollBarWidth - innerPad * 2);
            Size measured = TextRenderer.MeasureText(
                _lblAgreement.Text ?? "",
                _lblAgreement.Font,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int textHeight = measured.Height + Theme.S(4);
            _lblAgreement.SetBounds(innerPad, innerPad, textWidth, textHeight);
            _scrollPanel.AutoScrollMinSize = new Size(0, textHeight + innerPad * 2);

            int gap = Theme.S(10);
            int right = ClientSize.Width - pad;

            _btnAccept.Width = MeasureWidth(_btnAccept.Text) + Theme.S(32);
            _btnAccept.SetBounds(right - _btnAccept.Width, buttonsTop, _btnAccept.Width, buttonsHeight);
            right = _btnAccept.Left - gap;

            _btnDecline.Width = MeasureWidth(_btnDecline.Text) + Theme.S(32);
            _btnDecline.SetBounds(right - _btnDecline.Width, buttonsTop, _btnDecline.Width, buttonsHeight);
            right = _btnDecline.Left - gap;

            int noteWidth = Math.Max(Theme.S(120), right - pad);
            _lblPrivacyNote.SetBounds(pad, buttonsTop, noteWidth, buttonsHeight);
        }

        private static int MeasureWidth(string text)
        {
            Size size = TextRenderer.MeasureText(text ?? "", Theme.ButtonFont, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return size.Width;
        }

        private void AgreementFramePaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, _agreementFrame.Width - 1, _agreementFrame.Height - 1);
            }
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
            _lblBadge.BackColor = Theme.AccentSoft;
            _lblBadge.ForeColor = Theme.Accent;
            _lblTitle.BackColor = Theme.Surface;
            _lblTitle.ForeColor = Theme.Text;
            _languageSelector.ApplyTheme();
            _lblIntro.BackColor = Theme.Surface;
            _lblIntro.ForeColor = Theme.TextMuted;
            _agreementFrame.BackColor = Theme.SurfaceMuted;
            _scrollPanel.BackColor = Theme.SurfaceMuted;
            _lblAgreement.BackColor = Theme.SurfaceMuted;
            _lblAgreement.ForeColor = Theme.Text;
            _lblPrivacyNote.BackColor = Theme.Surface;
            _lblPrivacyNote.ForeColor = Theme.TextMuted;
            _btnDecline.ApplyTheme();
            _btnAccept.ApplyTheme();
            _agreementFrame.Invalidate();
            Invalidate();
        }
    }
}
