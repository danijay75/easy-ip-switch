using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class MainForm : Form, IThemeAware
    {
        private enum StatusKind
        {
            None,
            Info,
            Success,
            Warning,
            Error
        }

        private sealed class ChangeResult
        {
            public PendingChange Change;
            public OpResult Result;
        }

        private sealed class ApplyOutcome
        {
            public List<ChangeResult> Items = new List<ChangeResult>();
            public List<AdapterInfo> Adapters;
        }

        private sealed class ToggleOutcome
        {
            public OpResult Result;
            public bool Enabled;
            public List<AdapterInfo> Adapters;
        }

        private sealed class PendingToggle
        {
            public AdapterIdentity Identity;
            public bool Enabled;
        }

        private readonly Panel _header = new Panel();
        private readonly Panel _content = new Panel();
        private readonly Panel _footer = new Panel();
        private readonly Panel _credits = new Panel();
        private readonly Panel _cardsHost = new Panel();
        private readonly FlowLayoutPanel _flow = new FlowLayoutPanel();
        private readonly Label _placeholder = new Label();
        private readonly TabBar _tabs = new TabBar();
        private readonly PingView _pingView = new PingView();
        private readonly FlatButton _btnRefresh = new FlatButton();
        private readonly FlatButton _btnConnections = new FlatButton();
        private readonly LanguageSelector _languageSelector = new LanguageSelector();
        private readonly Label _lblStatus = new Label();
        private readonly Label _lblHint = new Label();
        private readonly Pill _pillAdmin = new Pill();
        private readonly LinkLabel _lblCopyright = new LinkLabel();
        private readonly LinkLabel _lnkSite = new LinkLabel();
        private readonly LinkLabel _lnkDonate = new LinkLabel();
        private readonly Label _lblSeparator = new Label();
        private readonly ThemeToggle _themeToggle = new ThemeToggle();
        private readonly ToolTip _tip = new ToolTip();

        private readonly Dictionary<string, AdapterCard> _cards = new Dictionary<string, AdapterCard>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AdapterCard> _order = new List<AdapterCard>();
        private readonly Dictionary<string, PendingToggle> _pendingToggles = new Dictionary<string, PendingToggle>(StringComparer.OrdinalIgnoreCase);
        private readonly INetworkConnectionsBackend _connectionsBackend;

        private bool _busy;
        private bool _loading;
        private bool _manualRefreshQueued;
        private bool _shortcutBusy;
        private bool _elevated;
        private bool _suppressLanguage;
        private bool _layingOutCards;
        private bool _allowClose;
        private bool _closeAfterApply;
        private System.Windows.Forms.Timer _refreshTimer;
        private System.Windows.Forms.Timer _autoTimer;
        private System.Windows.Forms.Timer _deviceTimer;
        private StatusKind _statusKind;

        public MainForm()
            : this(null)
        {
        }

        internal MainForm(INetworkConnectionsBackend connectionsBackend)
        {
            _connectionsBackend = connectionsBackend ?? new WindowsNetworkConnectionsBackend();
            Text = AppInfo.ProductName + " " + AppInfo.VersionLabel;
            Font = Theme.Body;
            BackColor = Theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Theme.S(880), Theme.S(620));
            DoubleBuffered = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildHeader();
            BuildContent();
            BuildFooter();
            BuildCredits();

            Controls.Add(_header);
            Controls.Add(_footer);
            Controls.Add(_credits);
            Controls.Add(_content);

            Theme.Changed += ThemeChangedHandler;
        }

        private void BuildHeader()
        {
            _header.BackColor = Theme.Surface;
            _header.Paint += HeaderPaint;

            _btnRefresh.Kind = ButtonKind.Secondary;
            _btnRefresh.Text = L.T("Actualiser");
            _btnRefresh.ShowRefreshIcon = true;
            _btnRefresh.Click += delegate { RefreshAdapters(true, true); };
            _tip.SetToolTip(_btnRefresh, L.T("Mettre à jour la liste et l'état des cartes réseau (F5)"));
            _header.Controls.Add(_btnRefresh);

            _pillAdmin.Visible = false;
            _pillAdmin.Cursor = Cursors.Hand;
            _pillAdmin.Click += delegate { PromptElevation(); };
            _tip.SetToolTip(_pillAdmin, L.T("Cliquez pour relancer Easy IP-Switch avec les droits administrateur"));
            _header.Controls.Add(_pillAdmin);

            _themeToggle.SetSelectedSilent(0);
            _themeToggle.SelectedIndexChanged += delegate
            {
                Theme.SetDark(_themeToggle.SelectedIndex == 1);
            };
            _tip.SetToolTip(_themeToggle, L.T("Mode clair ou sombre"));
            _header.Controls.Add(_themeToggle);

            _languageSelector.SetSelectedSilent(L.Current);
            _languageSelector.SelectedLanguageChanged += LanguageSelectionChanged;
            _languageSelector.AccessibleName = L.T("Langue");
            _languageSelector.AccessibleDescription = L.T("Changer la langue de l'application");
            _tip.SetToolTip(_languageSelector, L.T("Changer la langue de l'application"));
            _header.Controls.Add(_languageSelector);
        }

        private void BuildContent()
        {
            _content.BackColor = Theme.Bg;
            _content.Padding = Padding.Empty;

            _tabs.Items = new string[] { L.T("Cartes réseau"), L.T("Test de ping") };
            _tabs.SelectedIndexChanged += delegate { UpdateTab(); };
            _content.Controls.Add(_tabs);

            _btnConnections.Kind = ButtonKind.Secondary;
            _btnConnections.Text = L.T("Connexions réseau");
            _btnConnections.SystemIcon = WindowsNetworkIcon.Load();
            _btnConnections.AccessibleName = L.T("Connexions réseau");
            _btnConnections.AccessibleDescription = L.T("Ouvre les Connexions réseau Windows et crée un raccourci sur le Bureau.");
            _btnConnections.Click += delegate { OpenConnectionsAndCreateShortcut(); };
            _tip.SetToolTip(_btnConnections, L.T("Ouvre les Connexions réseau Windows et crée leur raccourci sur le Bureau."));
            _content.Controls.Add(_btnConnections);

            _cardsHost.BackColor = Theme.Bg;
            _content.Controls.Add(_cardsHost);

            _flow.BackColor = Theme.Bg;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.AutoScroll = true;
            _flow.Dock = DockStyle.Fill;
            _flow.Margin = new Padding(0);
            _flow.Padding = new Padding(0);
            _flow.Resize += delegate { LayoutCards(); };
            _flow.ClientSizeChanged += delegate { LayoutCards(); };
            _cardsHost.Controls.Add(_flow);

            _placeholder.BackColor = Theme.Bg;
            _placeholder.ForeColor = Theme.TextMuted;
            _placeholder.Font = Theme.Body;
            _placeholder.AutoSize = false;
            _placeholder.Dock = DockStyle.Fill;
            _placeholder.TextAlign = ContentAlignment.MiddleCenter;
            _placeholder.Text = L.T("Recherche des cartes réseau…");
            _cardsHost.Controls.Add(_placeholder);
            _placeholder.BringToFront();

            _pingView.Visible = false;
            _content.Controls.Add(_pingView);
        }

        private void UpdateTab()
        {
            bool cards = _tabs.SelectedIndex == 0;
            _cardsHost.Visible = cards;
            _pingView.Visible = !cards;
            if (cards)
            {
                LayoutCards();
            }
            else
            {
                _pingView.Focus();
            }
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
            _btnRefresh.Text = L.T("Actualiser");
            _btnConnections.Text = L.T("Connexions réseau");
            _btnConnections.AccessibleName = L.T("Connexions réseau");
            _btnConnections.AccessibleDescription = L.T("Ouvre les Connexions réseau Windows et crée un raccourci sur le Bureau.");
            _tabs.Items = new string[] { L.T("Cartes réseau"), L.T("Test de ping") };
            _lnkDonate.Text = L.T("Faire un don");
            _lblCopyright.Text = AppInfo.CopyrightLine;
            int start = _lblCopyright.Text.IndexOf("Angeli Visions", StringComparison.Ordinal);
            if (start >= 0)
            {
                _lblCopyright.LinkArea = new LinkArea(start, "Angeli Visions".Length);
            }

            _tip.SetToolTip(_btnRefresh, L.T("Mettre à jour la liste et l'état des cartes réseau (F5)"));
            _tip.SetToolTip(_btnConnections, L.T("Ouvre les Connexions réseau Windows et crée leur raccourci sur le Bureau."));
            _tip.SetToolTip(_languageSelector, L.T("Changer la langue de l'application"));
            _tip.SetToolTip(_themeToggle, L.T("Mode clair ou sombre"));

            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].ApplyLanguage();
            }
            _pingView.ApplyLanguage();
            UpdateAdminBadge();

            if (_order.Count == 0)
            {
                _placeholder.Text = _loading
                    ? L.T("Recherche des cartes réseau…")
                    : L.T("Aucune carte réseau détectée.\n\nBranchez un câble réseau, activez une carte, puis cliquez sur Actualiser.");
            }

            if (!_busy && !_loading)
            {
                SetStatus("", StatusKind.None);
            }
            UpdateFooter();
            _header.Invalidate();
            _footer.Invalidate();
            _credits.Invalidate();
            Invalidate(true);
        }

        private void BuildFooter()
        {
            _footer.BackColor = Theme.Surface;
            _footer.Paint += FooterPaint;

            _lblStatus.AutoSize = false;
            _lblStatus.Font = Theme.Body;
            _lblStatus.ForeColor = Theme.TextMuted;
            _lblStatus.BackColor = Theme.Surface;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.AutoEllipsis = true;
            _footer.Controls.Add(_lblStatus);

            _lblHint.AutoSize = false;
            _lblHint.Font = Theme.Small;
            _lblHint.ForeColor = Theme.TextFaint;
            _lblHint.BackColor = Theme.Surface;
            _lblHint.TextAlign = ContentAlignment.MiddleLeft;
            _lblHint.AutoEllipsis = true;
            _footer.Controls.Add(_lblHint);
        }

        private void BuildCredits()
        {
            _credits.BackColor = Theme.Surface;
            _credits.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _credits.Width, 0);
                }
            };

            _lblCopyright.AutoSize = false;
            _lblCopyright.Font = Theme.Small;
            _lblCopyright.ForeColor = Theme.TextFaint;
            _lblCopyright.LinkColor = Theme.TextMuted;
            _lblCopyright.ActiveLinkColor = Theme.Accent;
            _lblCopyright.VisitedLinkColor = Theme.TextMuted;
            _lblCopyright.LinkBehavior = LinkBehavior.HoverUnderline;
            _lblCopyright.BackColor = Theme.Surface;
            _lblCopyright.TextAlign = ContentAlignment.MiddleLeft;
            _lblCopyright.AutoEllipsis = true;
            _lblCopyright.UseMnemonic = false;
            _lblCopyright.Text = AppInfo.CopyrightLine;
            int start = _lblCopyright.Text.IndexOf("Angeli Visions", StringComparison.Ordinal);
            if (start >= 0)
            {
                _lblCopyright.LinkArea = new LinkArea(start, "Angeli Visions".Length);
            }
            _lblCopyright.LinkClicked += delegate { OpenUrl(AppInfo.WebsiteUrl); };
            _credits.Controls.Add(_lblCopyright);

            _lnkSite.AutoSize = true;
            _lnkSite.Font = Theme.Small;
            _lnkSite.ForeColor = Theme.TextMuted;
            _lnkSite.LinkColor = Theme.TextMuted;
            _lnkSite.ActiveLinkColor = Theme.Accent;
            _lnkSite.VisitedLinkColor = Theme.TextMuted;
            _lnkSite.LinkBehavior = LinkBehavior.HoverUnderline;
            _lnkSite.BackColor = Theme.Surface;
            _lnkSite.UseMnemonic = false;
            _lnkSite.Text = "angelivisions.com";
            _lnkSite.LinkClicked += delegate { OpenUrl(AppInfo.WebsiteUrl); };
            _credits.Controls.Add(_lnkSite);

            _lblSeparator.AutoSize = false;
            _lblSeparator.Font = Theme.Small;
            _lblSeparator.ForeColor = Theme.TextFaint;
            _lblSeparator.BackColor = Theme.Surface;
            _lblSeparator.TextAlign = ContentAlignment.MiddleCenter;
            _lblSeparator.Text = "·";
            _credits.Controls.Add(_lblSeparator);

            _lnkDonate.AutoSize = true;
            _lnkDonate.Font = Theme.SmallBold;
            _lnkDonate.ForeColor = Theme.Accent;
            _lnkDonate.LinkColor = Theme.Accent;
            _lnkDonate.ActiveLinkColor = Theme.AccentPressed;
            _lnkDonate.VisitedLinkColor = Theme.Accent;
            _lnkDonate.LinkBehavior = LinkBehavior.HoverUnderline;
            _lnkDonate.BackColor = Theme.Surface;
            _lnkDonate.UseMnemonic = false;
            _lnkDonate.Text = L.T("Faire un don");
            _lnkDonate.LinkClicked += delegate { OpenUrl(AppInfo.DonationUrl); };
            _credits.Controls.Add(_lnkDonate);
        }

        private void LayoutCredits()
        {
            int pad = Theme.S(24);
            int height = _credits.Height;

            _lblCopyright.SetBounds(pad, 0, Math.Max(Theme.S(150), _credits.Width - pad * 2 - Theme.S(300)), height);

            _lnkDonate.Top = (height - _lnkDonate.Height) / 2;
            _lnkDonate.Left = _credits.Width - pad - _lnkDonate.Width;

            _lblSeparator.SetBounds(_lnkDonate.Left - Theme.S(16), 0, Theme.S(8), height);

            _lnkSite.Top = (height - _lnkSite.Height) / 2;
            _lnkSite.Left = _lblSeparator.Left - Theme.S(8) - _lnkSite.Width;
        }

        private void OpenConnectionsAndCreateShortcut()
        {
            if (_busy || _shortcutBusy)
            {
                return;
            }

            _shortcutBusy = true;
            _btnConnections.Enabled = false;
            SetStatus(L.T("Ouverture des Connexions réseau et préparation du raccourci…"), StatusKind.Info);

            try
            {
                Thread worker = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        NetworkConnectionsResult result = NetworkConnections.Execute(_connectionsBackend);
                        TryInvoke(delegate
                        {
                            _shortcutBusy = false;
                            _btnConnections.Enabled = !_busy;
                            if (result.Shortcut != null)
                            {
                                _tip.SetToolTip(_btnConnections,
                                    L.F("Ouvre les Connexions réseau Windows.\r\nRaccourci : {0}", result.Shortcut.Path));
                            }
                            SetStatus(result.Message, result.Success ? StatusKind.Success : StatusKind.Error);
                        });
                    }
                    catch (Exception ex)
                    {
                        string message = ErrorMessage(ex);
                        TryInvoke(delegate
                        {
                            _shortcutBusy = false;
                            _btnConnections.Enabled = !_busy;
                            SetStatus(L.F("Impossible d'ouvrir les Connexions réseau : {0}", message), StatusKind.Error);
                        });
                    }
                }));
                worker.IsBackground = true;
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
            }
            catch (Exception ex)
            {
                _shortcutBusy = false;
                _btnConnections.Enabled = !_busy;
                SetStatus(L.F("Impossible de lancer l'opération : {0}", ErrorMessage(ex)), StatusKind.Error);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetStatus(L.F("Impossible d'ouvrir le lien : {0}", ex.Message), StatusKind.Error);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _elevated = IsElevated();
            bool dark = Theme.DetectSystemDark();
            _themeToggle.SetSelectedSilent(dark ? 1 : 0);
            Theme.SetDark(dark);
            UpdateAdminBadge();

            Rectangle area = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(Theme.S(1120), area.Width - Theme.S(50));
            int height = Math.Min(Theme.S(720), area.Height - Theme.S(70));
            width = Math.Max(width, MinimumSize.Width);
            height = Math.Max(height, MinimumSize.Height);
            ClientSize = new Size(width, height);

            LayoutChrome();
            UpdateFooter();
            RefreshAdapters(true, false);

            _autoTimer = new System.Windows.Forms.Timer();
            _autoTimer.Interval = 15000;
            _autoTimer.Tick += delegate
            {
                if (!_busy && !_loading)
                {
                    SilentRefresh();
                }
            };
            _autoTimer.Start();

            _deviceTimer = new System.Windows.Forms.Timer();
            _deviceTimer.Interval = 1800;
            _deviceTimer.Tick += delegate
            {
                _deviceTimer.Stop();
                if (!_busy && !_loading)
                {
                    SilentRefresh();
                }
            };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // WM_DEVICECHANGE / DBT_DEVNODES_CHANGED : un adaptateur a été branché ou retiré.
            if (m.Msg == 0x0219 && m.WParam.ToInt32() == 0x0007 && _deviceTimer != null)
            {
                _deviceTimer.Stop();
                _deviceTimer.Start();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                RefreshAdapters(true, true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChrome();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_allowClose)
            {
                base.OnFormClosing(e);
                return;
            }

            // Ne jamais bloquer un arrêt ou une fermeture imposée par Windows
            // avec la boîte de dialogue de don.
            if (e.CloseReason == CloseReason.WindowsShutDown
                || e.CloseReason == CloseReason.TaskManagerClosing
                || e.CloseReason == CloseReason.ApplicationExitCall)
            {
                base.OnFormClosing(e);
                return;
            }

            if (_busy || _loading || _shortcutBusy)
            {
                e.Cancel = true;
                SetStatus(L.T("Opération en cours : attendez la fin du traitement avant de fermer."), StatusKind.Error);
                return;
            }

            int dirty = CountDirty();
            string message;
            string primaryText;
            string secondaryText;
            if (dirty == 0)
            {
                message = L.T("Aucune modification n'est en attente.") + "\r\n\r\n"
                    + L.T("Merci d'utiliser Easy IP-Switch. Avant de fermer, vous pouvez soutenir le projet : scannez le QR code ou ouvrez la page de don.");
                primaryText = L.T("Quitter");
                secondaryText = null;
            }
            else
            {
                message = (dirty == 1
                    ? L.T("Une carte réseau a été modifiée mais la nouvelle configuration n'a pas encore été appliquée.")
                    : L.F("{0} cartes réseau ont été modifiées mais leurs nouvelles configurations n'ont pas encore été appliquées.", dirty))
                    + "\r\n\r\n"
                    + L.T("Vous pouvez appliquer ces changements avant de fermer, quitter sans les appliquer (les saisies seront perdues) ou rester dans l'application.");
                primaryText = L.T("Appliquer et quitter");
                secondaryText = L.T("Quitter sans appliquer");
            }

            int choice = QuitDialog.Show(this, L.T("Quitter Easy IP-Switch ?"), message,
                primaryText, secondaryText, L.T("Rester"));

            if (choice == QuitDialog.ChoiceStay)
            {
                e.Cancel = true;
                SetStatus(dirty == 0
                    ? L.T("Fermeture annulée.")
                    : L.T("Fermeture annulée : les modifications en attente sont conservées."), StatusKind.Info);
                return;
            }

            if (dirty == 0 || choice == QuitDialog.ChoiceSecondary)
            {
                base.OnFormClosing(e);
                return;
            }

            e.Cancel = true;
            ApplyAllThenClose();
        }

        private void ApplyAllThenClose()
        {
            if (_busy || _loading)
            {
                return;
            }

            List<PendingChange> pending = new List<PendingChange>();
            for (int i = 0; i < _order.Count; i++)
            {
                AdapterCard card = _order[i];
                if (!card.IsDirty)
                {
                    continue;
                }
                if (!card.AdapterEnabled)
                {
                    card.SetMessage(L.T("Activez la carte avant d'appliquer une configuration."), MessageKind.Error);
                    SetStatus(L.F("Fermeture annulée : activez d'abord « {0} ».", card.AdapterName), StatusKind.Error);
                    _flow.ScrollControlIntoView(card);
                    return;
                }

                ValidationResult validation = card.ValidateInputs();
                if (!validation.IsValid)
                {
                    card.ClearMessage();
                    card.ShowValidationError(validation);
                    SetStatus(L.F("Fermeture annulée : corrigez les champs signalés en rouge dans « {0} ».", card.AdapterName), StatusKind.Error);
                    _flow.ScrollControlIntoView(card);
                    card.FocusError(validation);
                    return;
                }
                card.ClearMessage();
                pending.Add(card.BuildPending());
            }

            if (pending.Count == 0)
            {
                _allowClose = true;
                Close();
                return;
            }

            if (!_elevated)
            {
                SetStatus(L.T("Droits administrateur requis pour appliquer les modifications : fermeture annulée."), StatusKind.Error);
                PromptElevation();
                return;
            }

            _closeAfterApply = true;
            SetBusy(true, L.T("Application des modifications avant fermeture…"));

            Task.Factory.StartNew<ApplyOutcome>(delegate { return RunPending(pending); })
                .ContinueWith(delegate(Task<ApplyOutcome> task)
                {
                    TryInvoke(delegate
                    {
                        if (task.IsFaulted || task.IsCanceled)
                        {
                            _closeAfterApply = false;
                            SetBusy(false, null);
                            SetStatus(L.F("Fermeture annulée : {0}", ErrorMessage(task.Exception)), StatusKind.Error);
                            return;
                        }
                        FinishApply(task.Result);
                    });
                });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.Changed -= ThemeChangedHandler;
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
            if (_autoTimer != null)
            {
                _autoTimer.Stop();
                _autoTimer.Dispose();
                _autoTimer = null;
            }
            if (_deviceTimer != null)
            {
                _deviceTimer.Stop();
                _deviceTimer.Dispose();
                _deviceTimer = null;
            }
            base.OnFormClosed(e);
        }

        private void LayoutChrome()
        {
            int headerHeight = Theme.S(76);
            int footerHeight = Theme.S(56);
            int creditsHeight = Theme.S(30);
            _header.SetBounds(0, 0, ClientSize.Width, headerHeight);
            _credits.SetBounds(0, ClientSize.Height - creditsHeight, ClientSize.Width, creditsHeight);
            _footer.SetBounds(0, ClientSize.Height - creditsHeight - footerHeight, ClientSize.Width, footerHeight);
            _content.SetBounds(0, headerHeight, ClientSize.Width,
                Math.Max(0, ClientSize.Height - headerHeight - footerHeight - creditsHeight));

            int pad = Theme.S(24);
            _btnRefresh.SetBounds(_header.ClientSize.Width - pad - Theme.S(120), Theme.S(19), Theme.S(120), Theme.S(38));
            _pillAdmin.Height = Theme.S(30);
            _pillAdmin.Left = _btnRefresh.Left - Theme.S(12) - _pillAdmin.Width;
            _pillAdmin.Top = Theme.S(23);
            _themeToggle.SetBounds(_pillAdmin.Left - Theme.S(10) - Theme.S(88), Theme.S(21), Theme.S(88), Theme.S(34));
            _languageSelector.SetBounds(
                _themeToggle.Left - Theme.S(10) - Theme.S(82),
                Theme.S(21), Theme.S(82), Theme.S(34));

            _tabs.SetBounds(pad, Theme.S(12), Math.Max(Theme.S(240), Theme.S(360)), Theme.S(40));
            _btnConnections.SetBounds(_content.Width - pad - Theme.S(190), Theme.S(15), Theme.S(190), Theme.S(34));

            int viewsTop = Theme.S(56);
            int viewsHeight = Math.Max(Theme.S(120), _content.Height - viewsTop - Theme.S(4));
            int viewsWidth = Math.Max(Theme.S(320), _content.Width - pad * 2);
            _cardsHost.SetBounds(pad, viewsTop, viewsWidth, viewsHeight);
            _pingView.SetBounds(pad, viewsTop, viewsWidth, viewsHeight);

            int textWidth = Math.Max(Theme.S(220), _footer.ClientSize.Width - pad * 2);
            _lblStatus.SetBounds(pad, Theme.S(8), textWidth, Theme.S(22));
            _lblHint.SetBounds(pad, Theme.S(28), textWidth, Theme.S(20));

            LayoutCredits();
            LayoutCards();
        }

        private void LayoutCards()
        {
            if (_layingOutCards)
            {
                return;
            }
            _layingOutCards = true;
            try
            {
                int width = CardsWidth();
                for (int i = 0; i < _order.Count; i++)
                {
                    if (_order[i].Width != width)
                    {
                        _order[i].Width = width;
                    }
                }
                // Forcer le recalcul de la zone défilante : sans cela, une
                // ancienne largeur de carte peut laisser une barre horizontale.
                _flow.PerformLayout();
                _flow.HorizontalScroll.Enabled = false;
                _flow.HorizontalScroll.Visible = false;
            }
            finally
            {
                _layingOutCards = false;
            }
        }

        private int CardsWidth()
        {
            // Réserver la largeur d'une barre verticale même avant son
            // apparition : sinon une barre horizontale parasite apparaît dès
            // qu'une carte est plus haute que la zone visible.
            int width = _flow.ClientSize.Width
                - SystemInformation.VerticalScrollBarWidth
                - Theme.S(2);
            return Math.Max(Theme.S(420), width);
        }

        private void HeaderPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }

            Rectangle logo = new Rectangle(Theme.S(24), Theme.S(20), Theme.S(36), Theme.S(36));
            Glyphs.Logo(g, logo);

            int titleLeft = logo.Right + Theme.S(14);
            int titleRight = _themeToggle.Visible ? _themeToggle.Left - Theme.S(16) : logo.Right + Theme.S(520);
            int titleWidth = Math.Max(Theme.S(120), titleRight - titleLeft);
            Rectangle title = new Rectangle(titleLeft, Theme.S(13), titleWidth, Theme.S(28));
            TextRenderer.DrawText(g, AppInfo.ProductName, Theme.Title, title, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Version affichée après le nom, sur la même ligne.
            Size nameSize = TextRenderer.MeasureText(g, AppInfo.ProductName, Theme.Title, title.Size,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            int versionLeft = title.Left + nameSize.Width + Theme.S(8);
            int versionWidth = title.Right - versionLeft;
            if (versionWidth >= Theme.S(40))
            {
                Rectangle version = new Rectangle(versionLeft, Theme.S(13), versionWidth, Theme.S(28));
                TextRenderer.DrawText(g, AppInfo.VersionLabel, Theme.Subtitle, version, Theme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            Rectangle subtitle = new Rectangle(titleLeft, Theme.S(41), titleWidth, Theme.S(20));
            TextRenderer.DrawText(g, L.T("Changement d'adresse IP en quelques clics"), Theme.Subtitle, subtitle, Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void FooterPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
            }
        }

        private static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAdminBadge()
        {
            if (_elevated)
            {
                _pillAdmin.Text = L.T("Administrateur");
                _pillAdmin.SetColors(Theme.SuccessSoft, Theme.Success);
                _pillAdmin.Cursor = Cursors.Default;
                _tip.SetToolTip(_pillAdmin, L.T("Easy IP-Switch dispose des droits administrateur."));
            }
            else
            {
                _pillAdmin.Text = L.T("Droits limités");
                _pillAdmin.SetColors(Theme.WarningSoft, Theme.Warning);
                _pillAdmin.Cursor = Cursors.Hand;
                _tip.SetToolTip(_pillAdmin, L.T("Cliquez ici pour relancer Easy IP-Switch avec les droits administrateur."));
            }
            _pillAdmin.AutoFit();
            _pillAdmin.Visible = true;
            LayoutChrome();
        }

        private void PromptElevation()
        {
            if (_elevated || _busy || _loading)
            {
                return;
            }

            string message = L.T("Windows n'a pas accordé les droits administrateur à cette session de l'application.\n\nSans ces droits, les changements d'adresse IP et l'activation des cartes sont bloqués.\n\nSouhaitez-vous relancer Easy IP-Switch en tant qu'administrateur ?");

            if (CountDirty() > 0)
            {
                message += L.T("\n\nAttention : les modifications non appliquées seront perdues.");
            }

            int choice = ConfirmDialog.Show(this,
                L.T("Droits administrateur requis"),
                message,
                L.T("Relancer en administrateur"),
                null,
                L.T("Rester"));

            if (choice != ConfirmDialog.ChoicePrimary)
            {
                return;
            }

            RelaunchElevated();
        }

        private void RelaunchElevated()
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(Application.ExecutablePath);
                start.UseShellExecute = true;
                start.Verb = "runas";
                Process.Start(start);
                _allowClose = true;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus(L.F("Impossible de relancer en administrateur : {0}", ex.Message), StatusKind.Error);
            }
        }

        private void RefreshAdapters(bool resetFields, bool manual)
        {
            if (_busy)
            {
                if (manual)
                {
                    SetStatus(L.T("Une opération est en cours ; réessayez dans un instant."), StatusKind.Info);
                }
                return;
            }
            if (_loading)
            {
                if (manual)
                {
                    _manualRefreshQueued = true;
                    SetStatus(L.T("Une actualisation est en cours ; une nouvelle lecture suivra."), StatusKind.Info);
                }
                return;
            }

            if (resetFields && CountDirty() > 0)
            {
                int choice = ConfirmDialog.Show(this,
                    L.T("Abandonner les modifications ?"),
                    L.T("Des saisies n'ont pas encore été appliquées. L'actualisation des cartes réseau effacera ces modifications."),
                    L.T("Actualiser et abandonner"),
                    L.T("Actualiser sans perdre les saisies"),
                    L.T("Annuler"));

                if (choice == ConfirmDialog.ChoiceStay)
                {
                    return;
                }
                if (choice == ConfirmDialog.ChoicePrimary)
                {
                    resetFields = true;
                }
                else
                {
                    resetFields = false;
                }
            }

            // Une actualisation automatique peut avoir démarré pendant la
            // boîte de confirmation ; ne pas lancer deux lectures à la fois.
            if (_busy || _loading)
            {
                if (manual && _loading)
                {
                    _manualRefreshQueued = true;
                    SetStatus(L.T("Une actualisation est en cours ; une nouvelle lecture suivra."), StatusKind.Info);
                }
                return;
            }

            _loading = true;
            _btnRefresh.Enabled = false;
            _placeholder.Visible = _order.Count == 0;
            _placeholder.Text = L.T("Recherche des cartes réseau…");
            SetStatus(manual ? L.T("Actualisation des cartes réseau…") : "", manual ? StatusKind.Info : StatusKind.None);
            UpdateFooter();

            Task.Factory.StartNew<List<AdapterInfo>>(delegate { return NetworkService.GetAdapters(); })
                .ContinueWith(delegate(Task<List<AdapterInfo>> task)
                {
                    TryInvoke(delegate { OnAdaptersLoaded(task, resetFields, manual); });
                });
        }

        private void OnAdaptersLoaded(Task<List<AdapterInfo>> task, bool resetFields, bool manual)
        {
            _loading = false;
            _btnRefresh.Enabled = true;

            if (task.IsFaulted || task.IsCanceled)
            {
                string message = task.Exception != null ? ErrorMessage(task.Exception) : L.T("Erreur inconnue.");
                _placeholder.Text = L.F("Impossible de lire les cartes réseau.\n\n{0}", message);
                _placeholder.Visible = _order.Count == 0;
                SetStatus(L.F("Impossible de lire les cartes réseau : {0}", message), StatusKind.Error);
                UpdateFooter();
                RunQueuedRefresh();
                return;
            }

            ApplyAdapters(task.Result, resetFields);
            LayoutCards();
            if (manual)
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    if (!_pendingToggles.ContainsKey(_order[i].AdapterIdentity.StableKey))
                    {
                        _order[i].ClearMessage();
                    }
                }
                if (_pendingToggles.Count > 0)
                {
                    SetStatus(L.T("Actualisation terminée ; l'état d'une carte est encore en cours de vérification."), StatusKind.Info);
                }
                else
                {
                    SetStatus(_order.Count == 1
                        ? L.T("Actualisation terminée : 1 carte réseau détectée.")
                        : L.F("Actualisation terminée : {0} cartes réseau détectées.", _order.Count), StatusKind.Success);
                }
            }
            UpdateFooter();
            RunQueuedRefresh();
        }

        private void RunQueuedRefresh()
        {
            if (!_manualRefreshQueued || _busy || _loading || IsDisposed)
            {
                return;
            }
            _manualRefreshQueued = false;
            RefreshAdapters(true, true);
        }

        private void ApplyAdapters(List<AdapterInfo> adapters, bool resetFields)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AdapterInfo info in adapters)
            {
                if (info.Name.Length == 0 || seen.Contains(info.Name))
                {
                    continue;
                }
                seen.Add(info.Name);

                AdapterCard card;
                if (!_cards.TryGetValue(info.Name, out card))
                {
                    card = new AdapterCard(info);
                    card.StateChanged += delegate { UpdateFooter(); };
                    card.ToggleRequested += CardToggleRequested;
                    card.ApplyRequested += CardApplyRequested;
                    _cards[info.Name] = card;
                    _order.Add(card);
                    _flow.Controls.Add(card);
                }
                else
                {
                    card.UpdateInfo(info, resetFields);
                }

                card.Width = CardsWidth();
            }

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                AdapterCard card = _order[i];
                if (seen.Contains(card.AdapterName))
                {
                    continue;
                }
                _flow.Controls.Remove(card);
                _cards.Remove(card.AdapterName);
                _order.RemoveAt(i);
                card.Dispose();
            }

            bool empty = _order.Count == 0;
            _placeholder.Visible = empty;
            if (empty)
            {
                _placeholder.Text = L.T("Aucune carte réseau détectée.\n\nBranchez un câble réseau, activez une carte, puis cliquez sur Actualiser.");
            }

            _pingView.SetAdapters(adapters);
            ResolvePendingToggles(adapters);
        }

        private void ResolvePendingToggles(List<AdapterInfo> adapters)
        {
            if (_pendingToggles.Count == 0 || adapters == null)
            {
                return;
            }

            List<string> resolved = new List<string>();
            foreach (KeyValuePair<string, PendingToggle> item in _pendingToggles)
            {
                for (int i = 0; i < adapters.Count; i++)
                {
                    AdapterInfo info = adapters[i];
                    if (!item.Value.Identity.Matches(info) || !info.EnabledKnown || info.Enabled != item.Value.Enabled)
                    {
                        continue;
                    }

                    string message = item.Value.Enabled ? "Carte activée." : "Carte désactivée.";
                    AdapterCard card;
                    if (_cards.TryGetValue(info.Name, out card))
                    {
                        card.SetMessage(message, MessageKind.Success);
                    }
                    SetStatus(message, StatusKind.Success);
                    resolved.Add(item.Key);
                    break;
                }
            }
            for (int i = 0; i < resolved.Count; i++)
            {
                _pendingToggles.Remove(resolved[i]);
            }
        }

        private void UpdateFooter()
        {
            int dirty = CountDirty();
            bool ready = !_busy && !_loading;

            if (_busy)
            {
                _lblHint.Text = L.T("Opération en cours, veuillez patienter…");
            }
            else if (_loading)
            {
                _lblHint.Text = L.T("Chargement des cartes réseau…");
            }
            else if (dirty == 0)
            {
                _lblHint.Text = L.T("Aucune modification en attente.");
            }
            else if (dirty == 1)
            {
                _lblHint.Text = L.T("1 carte modifiée — utilisez le bouton Changer de la carte concernée.");
            }
            else
            {
                _lblHint.Text = L.F("{0} cartes modifiées — utilisez le bouton Changer de chaque carte.", dirty);
            }
        }

        private int CountDirty()
        {
            int count = 0;
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].IsDirty)
                {
                    count++;
                }
            }
            return count;
        }

        private void SetStatus(string text, StatusKind kind)
        {
            _statusKind = kind;
            _lblStatus.ForeColor = kind == StatusKind.Success
                ? Theme.Success
                : kind == StatusKind.Warning
                    ? Theme.Warning
                : kind == StatusKind.Error
                    ? Theme.Danger
                    : kind == StatusKind.Info
                        ? Theme.Text
                        : Theme.TextMuted;
            _lblStatus.Text = text ?? "";
            _tip.SetToolTip(_lblStatus, _lblStatus.Text);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            _header.BackColor = Theme.Surface;
            _footer.BackColor = Theme.Surface;
            _credits.BackColor = Theme.Surface;
            _content.BackColor = Theme.Bg;
            _flow.BackColor = Theme.Bg;
            _cardsHost.BackColor = Theme.Bg;
            _placeholder.BackColor = Theme.Bg;
            _placeholder.ForeColor = Theme.TextMuted;

            _lblStatus.BackColor = Theme.Surface;
            SetStatus(_lblStatus.Text, _statusKind);

            _lblHint.BackColor = Theme.Surface;
            _lblHint.ForeColor = Theme.TextFaint;

            _lblCopyright.BackColor = Theme.Surface;
            _lblCopyright.ForeColor = Theme.TextFaint;
            _lblCopyright.LinkColor = Theme.TextMuted;
            _lblCopyright.ActiveLinkColor = Theme.Accent;
            _lblCopyright.VisitedLinkColor = Theme.TextMuted;

            _lnkSite.BackColor = Theme.Surface;
            _lnkSite.ForeColor = Theme.TextMuted;
            _lnkSite.LinkColor = Theme.TextMuted;
            _lnkSite.ActiveLinkColor = Theme.Accent;
            _lnkSite.VisitedLinkColor = Theme.TextMuted;

            _lnkDonate.BackColor = Theme.Surface;
            _lnkDonate.ForeColor = Theme.Accent;
            _lnkDonate.LinkColor = Theme.Accent;
            _lnkDonate.ActiveLinkColor = Theme.AccentPressed;
            _lnkDonate.VisitedLinkColor = Theme.Accent;

            _lblSeparator.BackColor = Theme.Surface;
            _lblSeparator.ForeColor = Theme.TextFaint;

            UpdateAdminBadge();
        }

        private void ThemeChangedHandler(object sender, EventArgs e)
        {
            ApplyThemeDeep(this);
            Invalidate(true);
        }

        private static void ApplyThemeDeep(Control root)
        {
            IThemeAware aware = root as IThemeAware;
            if (aware != null)
            {
                aware.ApplyTheme();
            }
            for (int i = 0; i < root.Controls.Count; i++)
            {
                ApplyThemeDeep(root.Controls[i]);
            }
        }

        private void SetBusy(bool busy, string statusText)
        {
            _busy = busy;
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].SetBusy(busy);
            }
            _btnRefresh.Enabled = !busy;
            _btnConnections.Enabled = !busy && !_shortcutBusy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (statusText != null)
            {
                SetStatus(statusText, StatusKind.Info);
            }
            UpdateFooter();
        }

        private void CardApplyRequested(object sender, EventArgs e)
        {
            AdapterCard card = sender as AdapterCard;
            if (card != null)
            {
                ApplyCard(card);
            }
        }

        private void ApplyCard(AdapterCard card)
        {
            if (_busy || _loading || card == null)
            {
                return;
            }
            if (!card.IsDirty)
            {
                return;
            }
            if (!card.AdapterEnabled)
            {
                card.SetMessage(L.T("Activez la carte avant d'appliquer une configuration."), MessageKind.Error);
                SetStatus(L.T("Changement refusé : activez d'abord la carte."), StatusKind.Error);
                return;
            }

            ValidationResult validation = card.ValidateInputs();
            if (!validation.IsValid)
            {
                card.ClearMessage();
                card.ShowValidationError(validation);
                SetStatus(L.F("Changement refusé pour « {0} » : corrigez les champs signalés en rouge.", card.AdapterName), StatusKind.Error);
                _flow.ScrollControlIntoView(card);
                card.FocusError(validation);
                return;
            }

            if (!_elevated)
            {
                SetStatus(L.T("Droits administrateur requis pour appliquer la configuration."), StatusKind.Error);
                PromptElevation();
                return;
            }

            card.ClearMessage();
            List<PendingChange> pending = new List<PendingChange>();
            pending.Add(card.BuildPending());

            SetBusy(true, L.F("Application de la configuration de « {0} »…", card.AdapterName));

            Task.Factory.StartNew<ApplyOutcome>(delegate { return RunPending(pending); })
                .ContinueWith(delegate(Task<ApplyOutcome> task)
                {
                    TryInvoke(delegate
                    {
                        if (task.IsFaulted || task.IsCanceled)
                        {
                            SetBusy(false, null);
                            SetStatus(L.F("Une erreur inattendue est survenue : {0}", ErrorMessage(task.Exception)), StatusKind.Error);
                            return;
                        }
                        FinishApply(task.Result);
                    });
                });
        }

        private static ApplyOutcome RunPending(List<PendingChange> pending)
        {
            ApplyOutcome outcome = new ApplyOutcome();
            for (int i = 0; i < pending.Count; i++)
            {
                PendingChange change = pending[i];
                OpResult result = change.Dhcp
                    ? NetworkService.ApplyDhcp(change.Index, change.Name)
                    : NetworkService.ApplyStatic(change.Identity, change.Ip, change.Mask, change.Gateway);

                ChangeResult item = new ChangeResult();
                item.Change = change;
                item.Result = result;
                outcome.Items.Add(item);
            }

            Thread.Sleep(300);
            try
            {
                outcome.Adapters = NetworkService.GetAdapters();
            }
            catch
            {
            }
            return outcome;
        }

        private void FinishApply(ApplyOutcome outcome)
        {
            int ok = 0;
            int unconfirmed = 0;
            int partial = 0;
            for (int i = 0; i < outcome.Items.Count; i++)
            {
                ChangeResult item = outcome.Items[i];
                AdapterInfo fresh = FindAdapter(outcome.Adapters, item.Change.Identity);
                if (fresh != null)
                {
                    item.Change.Card.UpdateInfo(fresh, item.Result.Success);
                }
                item.Change.Card.SetMessage(item.Result.Message, item.Result.Success
                    ? MessageKind.Success : item.Result.PartiallyApplied ? MessageKind.Warning
                    : item.Result.Unconfirmed ? MessageKind.Info : MessageKind.Error);
                if (item.Result.Success)
                {
                    ok++;
                }
                else if (item.Result.Unconfirmed)
                {
                    unconfirmed++;
                    if (item.Result.PartiallyApplied)
                    {
                        partial++;
                    }
                }
            }

            if (outcome.Adapters != null)
            {
                ApplyAdapters(outcome.Adapters, false);
            }

            SetBusy(false, null);
            if (outcome.Items.Count == 1)
            {
                OpResult result = outcome.Items[0].Result;
                SetStatus(result.Message, result.Success
                    ? StatusKind.Success : result.PartiallyApplied ? StatusKind.Warning
                    : result.Unconfirmed ? StatusKind.Info : StatusKind.Error);
            }
            else if (ok == outcome.Items.Count)
            {
                SetStatus(ok == 1 ? L.T("Modification appliquée avec succès.") : L.T("Modifications appliquées avec succès."), StatusKind.Success);
            }
            else if (ok + unconfirmed == outcome.Items.Count)
            {
                SetStatus(partial > 0
                    ? L.T("Certaines adresses sont enregistrées, mais leur configuration reste à vérifier.")
                    : L.T("Certaines configurations ne sont pas encore confirmées. Actualisez avant de réessayer."),
                    partial > 0 ? StatusKind.Warning : StatusKind.Info);
            }
            else if (ok == 0)
            {
                SetStatus(L.T("Les modifications ont échoué. Consultez le détail au bas des cartes."), StatusKind.Error);
            }
            else
            {
                SetStatus(L.F("{0} modification(s) sur {1} appliquée(s). Consultez le détail au bas des cartes.",
                    ok, outcome.Items.Count), StatusKind.Error);
            }
            UpdateFooter();

            if (_closeAfterApply)
            {
                _closeAfterApply = false;
                if (ok == outcome.Items.Count && CountDirty() == 0)
                {
                    _allowClose = true;
                    Close();
                }
                else
                {
                    SetStatus(unconfirmed > 0
                        ? L.T("Fermeture annulée : vérifiez les configurations non confirmées.")
                        : L.T("Fermeture annulée : certaines modifications n'ont pas pu être appliquées."), StatusKind.Error);
                }
            }
        }

        private void CardToggleRequested(object sender, EventArgs e)
        {
            AdapterCard card = sender as AdapterCard;
            if (card == null)
            {
                return;
            }
            if (_busy || _loading)
            {
                card.RevertToggle();
                return;
            }
            if (!_elevated)
            {
                card.RevertToggle();
                card.SetMessage(L.T("Droits administrateur requis pour activer ou désactiver une carte."), MessageKind.Error);
                SetStatus(L.T("Droits administrateur requis pour activer ou désactiver une carte."), StatusKind.Error);
                PromptElevation();
                return;
            }

            bool target = card.PendingEnabled;
            AdapterIdentity identity = card.AdapterIdentity;
            string name = card.AdapterName;
            _pendingToggles.Remove(identity.StableKey);

            SetBusy(true, target
                ? L.F("Activation de « {0} »…", name)
                : L.F("Désactivation de « {0} »…", name));
            card.SetMessage(target ? L.T("Activation en cours.") : L.T("Désactivation en cours."), MessageKind.Info);

            Task.Factory.StartNew<ToggleOutcome>(delegate
            {
                ToggleOutcome outcome = new ToggleOutcome();
                outcome.Result = AdapterControl.SetEnabled(identity, target);
                outcome.Enabled = target;
                Thread.Sleep(250);
                try
                {
                    outcome.Adapters = NetworkService.GetAdapters();
                }
                catch
                {
                }
                return outcome;
            }).ContinueWith(delegate(Task<ToggleOutcome> task)
            {
                TryInvoke(delegate
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        card.RevertToggle();
                        SetBusy(false, null);
                        card.SetMessage(L.F("Erreur : {0}", ErrorMessage(task.Exception)), MessageKind.Error);
                        SetStatus(L.T("Impossible de modifier l'état de la carte."), StatusKind.Error);
                        return;
                    }
                    FinishToggle(card, task.Result);
                });
            });
        }

        private void FinishToggle(AdapterCard card, ToggleOutcome outcome)
        {
            AdapterIdentity identity = card.AdapterIdentity;
            string key = identity.StableKey;
            if (outcome.Result.Unconfirmed)
            {
                PendingToggle pending = new PendingToggle();
                pending.Identity = identity;
                pending.Enabled = outcome.Enabled;
                _pendingToggles[key] = pending;
            }
            else
            {
                _pendingToggles.Remove(key);
            }

            if (outcome.Adapters != null)
            {
                ApplyAdapters(outcome.Adapters, false);
            }

            if (outcome.Result.Unconfirmed && !_pendingToggles.ContainsKey(key))
            {
                outcome.Result = OpResult.Ok(outcome.Enabled ? L.T("Carte activée.") : L.T("Carte désactivée."));
            }

            if (!card.IsDisposed)
            {
                if (!outcome.Result.Success)
                {
                    card.RevertToggle();
                }
                else if (outcome.Adapters == null)
                {
                    // SetEnabled a déjà vérifié l'état. Conserver celui-ci si
                    // la lecture de suivi a échoué, puis réessayer la lecture.
                    card.UpdateEnabledFromVerifiedOperation(outcome.Enabled);
                }
            }

            SetBusy(false, null);
            if (!card.IsDisposed)
            {
                card.SetMessage(outcome.Result.Message, outcome.Result.Success
                    ? MessageKind.Success : outcome.Result.Unconfirmed ? MessageKind.Info : MessageKind.Error);
            }
            SetStatus(outcome.Result.Message, outcome.Result.Success
                ? StatusKind.Success : outcome.Result.Unconfirmed ? StatusKind.Info : StatusKind.Error);

            if (outcome.Result.Unconfirmed || (outcome.Result.Success && (outcome.Enabled || outcome.Adapters == null)))
            {
                ScheduleSilentRefresh();
            }
            UpdateFooter();
        }

        private void ScheduleSilentRefresh()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
            }
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 2800;
            _refreshTimer.Tick += delegate
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
                SilentRefresh();
            };
            _refreshTimer.Start();
        }

        private void SilentRefresh()
        {
            if (_busy || _loading)
            {
                return;
            }
            _loading = true;
            Task.Factory.StartNew<List<AdapterInfo>>(delegate { return NetworkService.GetAdapters(); })
                .ContinueWith(delegate(Task<List<AdapterInfo>> task)
                {
                    TryInvoke(delegate
                    {
                        _loading = false;
                        if (!task.IsFaulted && !task.IsCanceled)
                        {
                            ApplyAdapters(task.Result, false);
                            LayoutCards();
                        }
                        UpdateFooter();
                        RunQueuedRefresh();
                    });
                });
        }

        private static AdapterInfo FindAdapter(List<AdapterInfo> adapters, AdapterIdentity identity)
        {
            if (adapters == null || identity == null)
            {
                return null;
            }
            for (int i = 0; i < adapters.Count; i++)
            {
                if (identity.Matches(adapters[i]))
                {
                    return adapters[i];
                }
            }
            return null;
        }

        private void TryInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(action);
            }
            catch
            {
            }
        }

        private static string ErrorMessage(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }
            return current.Message;
        }
    }
}
