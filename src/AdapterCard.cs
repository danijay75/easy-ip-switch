using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal enum MessageKind
    {
        None,
        Info,
        Success,
        Warning,
        Error
    }

    internal sealed class ValidationResult
    {
        public bool IsValid = true;
        public string Message = "";
        public InputBox Field;
    }

    internal sealed class PendingChange
    {
        public AdapterCard Card;
        public string Name = "";
        public int Index = -1;
        public AdapterIdentity Identity;
        public bool Dhcp;
        public string Ip = "";
        public string Mask = "";
        public string Gateway = "";
    }

    internal sealed class AdapterCard : CardPanel
    {
        private const string DefaultStaticAddress = "192.168.0.1";
        private const string DefaultSubnetMask = "255.255.255.0";

        private readonly Label _lblName;
        private readonly Label _lblMeta;
        private readonly Label _lblStatus;
        private readonly Label _lblToggle;
        private readonly Label _lblHint;
        private readonly Label _lblMessage;
        private readonly Label _lblError;
        private readonly Label _lblSaved;
        private readonly Label _lblIp;
        private readonly Label _lblMask;
        private readonly Label _lblGateway;
        private readonly ToggleSwitch _toggle;
        private readonly SegmentedControl _segment;
        private readonly InputBox _ip;
        private readonly InputBox _mask;
        private readonly InputBox _gateway;
        private readonly FlatButton _btnApply = new FlatButton();
        private readonly FlatButton _btnCancel = new FlatButton();
        private readonly FlatButton _btnTest = new FlatButton();
        private readonly ToolTip _tip = new ToolTip();
        private readonly ToolTip _errorTip = new ToolTip();

        private AdapterInfo _info;
        private bool _busy;
        private bool _testing;
        private bool _suppress;
        private bool _hasStaticDraft;
        private string _staticDraftIp = "";
        private string _staticDraftMask = "";
        private string _staticDraftGateway = "";
        private bool _staticDraftGatewayAuto;
        private bool _staticDraftGatewayFromConfig;
        private bool _staticDraftGatewayCleared;
        private bool _gatewayAuto;
        private bool _gatewayFromConfig;
        private bool _userClearedGateway;
        private string _message;
        private MessageKind _messageKind = MessageKind.None;
        private InputBox _errorField;

        public event EventHandler StateChanged;
        public event EventHandler ToggleRequested;
        public event EventHandler ApplyRequested;

        public AdapterCard(AdapterInfo info)
        {
            _info = info;
            Height = Theme.S(196);
            Margin = new Padding(0, 0, 0, Theme.S(12));
            BackColor = Theme.Surface;

            _lblName = MakeLabel(Theme.CardTitle, Theme.Text);
            // Long service and hardware names must wrap instead of being
            // truncated when the window is narrow.
            _lblName.AutoEllipsis = false;
            _lblMeta = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblMeta.AutoEllipsis = false;
            _lblStatus = MakeLabel(Theme.SmallBold, Theme.Success);
            _lblToggle = MakeLabel(Theme.Body, Theme.TextMuted);
            _lblToggle.Text = L.T("Carte active");
            _lblHint = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblMessage = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblError = MakeLabel(Theme.Small, Theme.Danger);
            _lblSaved = MakeLabel(Theme.Small, Theme.Warning);
            _lblIp = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblIp.Text = L.T("Adresse IP");
            _lblMask = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblMask.Text = L.T("Masque");
            _lblGateway = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblGateway.Text = L.T("Passerelle");

            _toggle = new ToggleSwitch();
            _toggle.Size = new Size(Theme.S(46), Theme.S(26));
            _toggle.CheckedChanged += ToggleChanged;
            _tip.SetToolTip(_toggle, L.T("Activer ou désactiver la carte réseau"));

            _segment = new SegmentedControl();
            _segment.Items = new string[] { "DHCP", L.T("Adresse fixe") };
            _segment.SelectedIndexChanged += SegmentChanged;
            _tip.SetToolTip(_segment, L.T("Choisir entre l'attribution automatique et une adresse fixe"));

            _ip = MakeInput(L.T("ex. 192.168.1.50"));
            _mask = MakeInput("255.255.255.0");
            _gateway = MakeInput(L.T("ex. 192.168.1.1"));
            _mask.Suggestions = new string[]
            {
                "255.255.255.0",
                "255.255.0.0",
                "255.0.0.0",
                "255.255.255.128",
                "255.255.255.192",
                "255.255.255.224",
                "255.255.255.240",
                "255.255.255.248",
                "255.255.255.252"
            };

            _btnApply.Kind = ButtonKind.Primary;
            _btnApply.Text = L.T("Changer");
            _btnApply.Click += delegate
            {
                if (ApplyRequested != null)
                {
                    ApplyRequested(this, EventArgs.Empty);
                }
            };
            _tip.SetToolTip(_btnApply, L.T("Appliquer la configuration de cette carte"));

            _btnCancel.Kind = ButtonKind.Ghost;
            _btnCancel.Text = L.T("Annuler");
            _btnCancel.Click += delegate
            {
                ClearMessage();
                ResetToCurrent();
            };
            _tip.SetToolTip(_btnCancel, L.T("Revenir à la configuration actuelle de cette carte"));

            _btnTest.Kind = ButtonKind.Secondary;
            _btnTest.Text = L.T("Tester");
            _btnTest.Click += delegate { TestCard(); };
            _tip.SetToolTip(_btnTest, L.T("Vérifier que l'adresse IP de cette carte et sa passerelle répondent"));

            _ip.ValueChanged += InputChanged;
            _mask.ValueChanged += InputChanged;
            _gateway.ValueChanged += InputChanged;

            _ip.FocusLost += FieldFocusLost;
            _mask.FocusLost += FieldFocusLost;
            _gateway.FocusLost += FieldFocusLost;

            _errorTip.IsBalloon = true;
            _errorTip.ToolTipIcon = ToolTipIcon.Error;
            _errorTip.ToolTipTitle = L.T("Vérifiez la saisie");

            _tip.SetToolTip(_ip, L.T("Adresse IPv4 de la carte, par exemple 192.168.1.50"));
            _tip.SetToolTip(_mask, L.T("Masque de sous-réseau, par exemple 255.255.255.0"));
            _tip.SetToolTip(_gateway, L.T("Passerelle par défaut, par exemple 192.168.1.1 (facultative)"));

            Controls.Add(_lblName);
            Controls.Add(_lblMeta);
            Controls.Add(_lblStatus);
            Controls.Add(_lblToggle);
            Controls.Add(_lblHint);
            Controls.Add(_lblMessage);
            Controls.Add(_lblError);
            Controls.Add(_lblSaved);
            Controls.Add(_lblIp);
            Controls.Add(_lblMask);
            Controls.Add(_lblGateway);
            Controls.Add(_toggle);
            Controls.Add(_segment);
            Controls.Add(_ip);
            Controls.Add(_mask);
            Controls.Add(_gateway);
            Controls.Add(_btnCancel);
            Controls.Add(_btnApply);
            Controls.Add(_btnTest);

            UpdateInfo(info, true);
        }

        public string AdapterName
        {
            get { return _info.Name; }
        }

        public int AdapterIndex
        {
            get { return _info.Index; }
        }

        public AdapterIdentity AdapterIdentity
        {
            get { return new AdapterIdentity(_info.Index, _info.Name, _info.InterfaceGuid, _info.DeviceId); }
        }

        public bool AdapterEnabled
        {
            get { return _info.EnabledKnown && _info.Enabled; }
        }

        public bool PendingEnabled
        {
            get { return _toggle.Checked; }
        }

        public bool IsDirty
        {
            get
            {
                bool wantDhcp = _segment.SelectedIndex == 0;
                if (wantDhcp != _info.Dhcp)
                {
                    return true;
                }
                if (wantDhcp)
                {
                    return false;
                }
                return !Same(_ip.Value, _info.Ip)
                    || !Same(_mask.Value, _info.Mask)
                    || !Same(_gateway.Value, _info.Gateway);
            }
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
        }

        private static Label MakeLabel(Font font, Color color)
        {
            Label label = new Label();
            label.AutoSize = false;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Theme.Surface;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;
            label.UseMnemonic = false;
            return label;
        }

        private static InputBox MakeInput(string placeholder)
        {
            InputBox box = new InputBox();
            box.Placeholder = placeholder;
            box.NumericOnly = true;
            return box;
        }

        public void UpdateInfo(AdapterInfo info, bool resetFields)
        {
            bool wasDirty = IsDirty;
            bool preserve = !resetFields && wasDirty;
            bool enabledChanged = _info.Enabled != info.Enabled || _info.EnabledKnown != info.EnabledKnown;
            _info = info;

            if (resetFields)
            {
                _hasStaticDraft = false;
                _staticDraftGatewayAuto = false;
                _staticDraftGatewayFromConfig = false;
                _staticDraftGatewayCleared = false;
            }

            if (enabledChanged)
            {
                _message = null;
                _messageKind = MessageKind.None;
            }

            _suppress = true;
            try
            {
                _lblName.Text = info.Name;
                _lblMeta.Text = info.MetaLabel;
                _lblStatus.Text = "●  " + info.StatusLabel;
                UpdateStatusColor();

                string tip = info.Description;
                if (info.Mac.Length > 0)
                {
                    tip += "\r\nMAC : " + IpUtil.FormatMac(info.Mac);
                }
                _tip.SetToolTip(_lblName, tip);
                _tip.SetToolTip(_lblMeta, tip);

                // L'état de la carte doit toujours refléter Windows, même si
                // une configuration IP non appliquée est conservée.
                _toggle.SetCheckedSilent(info.Enabled);
                if (!preserve)
                {
                    _segment.SetSelectedSilent(info.Dhcp ? 0 : 1);
                }

                if (!preserve)
                {
                    _ip.Value = info.Ip;
                    _mask.Value = info.Mask;
                    _gateway.Value = info.Gateway;
                    _gatewayAuto = false;
                    // A gateway read from a static configuration may follow a
                    // subnet change until the user edits it explicitly.
                    _gatewayFromConfig = !info.Dhcp && info.Gateway.Length > 0;
                    _userClearedGateway = false;
                }
            }
            finally
            {
                _suppress = false;
            }

            ClearValidationError();
            UpdateHint();
            UpdateInputs();
            UpdateLiveValidation(null);
            UpdateMessageLabel();
            Invalidate();
            OnStateChanged();
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            UpdateInputs();
        }

        public void SetMessage(string message, MessageKind kind)
        {
            _message = message;
            _messageKind = kind;
            UpdateMessageLabel();
        }

        public void ClearMessage()
        {
            _message = null;
            _messageKind = MessageKind.None;
            UpdateMessageLabel();
        }

        public void RevertToggle()
        {
            _suppress = true;
            _toggle.SetCheckedSilent(_info.Enabled);
            _suppress = false;
            UpdateHint();
        }

        public void UpdateEnabledFromVerifiedOperation(bool enabled)
        {
            _info.EnabledKnown = true;
            _info.Enabled = enabled;
            if (!enabled)
            {
                _info.Connected = false;
            }
            UpdateInfo(_info, false);
        }

        public void ResetToCurrent()
        {
            UpdateInfo(_info, true);
            ClearMessage();
        }

        public ValidationResult ValidateInputs()
        {
            return UpdateLiveValidation(null);
        }

        private ValidationResult UpdateLiveValidation(InputBox preferred)
        {
            ValidationResult result = new ValidationResult();
            if (_segment.SelectedIndex == 0)
            {
                ClearValidationError();
                ClearInvalid();
                return result;
            }

            string ipError = CheckIp();
            string maskError = CheckMask();
            string gatewayError = CheckGateway();
            _ip.Invalid = ipError != null;
            _mask.Invalid = maskError != null;
            _gateway.Invalid = gatewayError != null;

            if (ipError != null)
            {
                result.IsValid = false;
                result.Field = _ip;
                result.Message = ipError;
            }
            else if (maskError != null)
            {
                result.IsValid = false;
                result.Field = _mask;
                result.Message = maskError;
            }
            else if (gatewayError != null)
            {
                result.IsValid = false;
                result.Field = _gateway;
                result.Message = gatewayError;
            }

            if (result.IsValid)
            {
                ClearValidationError();
                return result;
            }

            InputBox field = result.Field;
            string message = result.Message;
            if (preferred == _ip && ipError != null)
            {
                field = _ip;
                message = ipError;
            }
            else if (preferred == _mask && maskError != null)
            {
                field = _mask;
                message = maskError;
            }
            else if (preferred == _gateway && gatewayError != null)
            {
                field = _gateway;
                message = gatewayError;
            }
            SetInlineError(field, message);
            return result;
        }

        public void ShowValidationError(ValidationResult result)
        {
            if (result == null || result.IsValid)
            {
                ClearValidationError();
                return;
            }
            if (result.Field != null)
            {
                ShowFieldError(result.Field, result.Message);
                return;
            }
            _lblError.Text = result.Message;
            _lblError.ForeColor = Theme.Danger;
            _tip.SetToolTip(_lblError, result.Message);
        }

        public void FocusError(ValidationResult result)
        {
            if (result == null || result.Field == null)
            {
                return;
            }
            result.Field.FocusText();
        }

        private void ShowFieldError(InputBox field, string message)
        {
            SetInlineError(field, message);
            field.Invalid = true;
            _errorTip.Show(message, field, field.Width / 2, field.Height + Theme.S(6), 7000);
        }

        private void SetInlineError(InputBox field, string message)
        {
            if (_errorField != null && _errorField != field)
            {
                _errorTip.Hide(_errorField);
            }
            _errorField = field;
            _lblError.Text = message;
            _lblError.ForeColor = Theme.Danger;
            _tip.SetToolTip(_lblError, message);
            LayoutValidationError();
        }

        private void ClearValidationError()
        {
            if (_errorField != null)
            {
                _errorTip.Hide(_errorField);
                _errorField = null;
            }
            _lblError.Text = "";
            _tip.SetToolTip(_lblError, "");
        }

        private void FieldFocusLost(object sender, EventArgs e)
        {
            InputBox box = sender as InputBox;
            if (box == null || _segment.SelectedIndex == 0)
            {
                return;
            }
            UpdateLiveValidation(box);
            string error = box == _ip ? CheckIp() : box == _mask ? CheckMask() : CheckGateway();
            if (error != null)
            {
                ShowFieldError(box, error);
            }
        }

        private string CheckIp()
        {
            uint ip;
            if (!IpUtil.TryParse(_ip.Value, out ip))
            {
                return L.T("Adresse IP invalide. Format attendu : 192.168.1.50");
            }
            if (ip == 0u || ip == 0xFFFFFFFFu)
            {
                return L.T("Adresse IP invalide : 0.0.0.0 et 255.255.255.255 sont interdites.");
            }
            if (IpUtil.IsApipa(ip))
            {
                return L.T("Adresse 169.254.x.x réservée par Windows : choisissez une autre adresse.");
            }
            return null;
        }

        private string CheckMask()
        {
            uint mask;
            if (!IpUtil.TryParse(_mask.Value, out mask))
            {
                return L.T("Masque invalide. Format attendu : 255.255.255.0");
            }
            if (!IpUtil.IsValidMask(mask))
            {
                return L.T("Masque non contigu : utilisez par exemple 255.255.255.0 ou 255.255.0.0.");
            }
            return null;
        }

        private string CheckGateway()
        {
            if (_gateway.Value.Length == 0)
            {
                return null;
            }

            uint gateway;
            if (!IpUtil.TryParse(_gateway.Value, out gateway))
            {
                return L.T("Passerelle invalide. Format attendu : 192.168.1.1");
            }

            uint ip;
            if (IpUtil.TryParse(_ip.Value, out ip))
            {
                if (gateway == ip)
                {
                    return L.T("La passerelle doit être différente de l'adresse IP.");
                }

                uint mask;
                if (IpUtil.TryParse(_mask.Value, out mask) && IpUtil.IsValidMask(mask)
                    && (gateway & mask) != (ip & mask))
                {
                    return L.F("La passerelle n'est pas dans le sous-réseau {0}.", IpUtil.Format(ip & mask));
                }
            }
            return null;
        }

        /// <summary>
        /// Keeps the gateway aligned with the static address and mask while it
        /// is empty or still holds a previous suggestion. A gateway inherited
        /// from the current configuration follows a subnet change; a manually
        /// entered or deliberately cleared gateway is preserved.
        /// </summary>
        private void RefreshSuggestedGateway()
        {
            if (_segment.SelectedIndex != 1)
            {
                return;
            }

            string current = _gateway.Value;
            if (_gatewayFromConfig)
            {
                if (current.Length == 0)
                {
                    _gatewayFromConfig = false;
                    _gatewayAuto = true;
                }
                else if (!GatewayBelongsToSubnet(current) || current == _ip.Value)
                {
                    string inheritedSuggestion = IpUtil.SuggestGateway(_ip.Value, _mask.Value);
                    if (inheritedSuggestion != null)
                    {
                        _gateway.Value = inheritedSuggestion;
                        _gatewayAuto = true;
                        _gatewayFromConfig = false;
                    }
                    return;
                }
                else
                {
                    return;
                }
            }

            bool autoManaged = _gatewayAuto || (current.Length == 0 && !_userClearedGateway);
            if (!autoManaged)
            {
                return;
            }

            string suggestion = IpUtil.SuggestGateway(_ip.Value, _mask.Value);
            if (suggestion == null)
            {
                if (_gatewayAuto)
                {
                    _gateway.Value = "";
                    _gatewayAuto = false;
                }
                return;
            }

            if (current != suggestion)
            {
                _gateway.Value = suggestion;
            }
            _gatewayAuto = true;
        }

        private bool GatewayBelongsToSubnet(string gatewayText)
        {
            uint gateway;
            uint ip;
            uint mask;
            if (!IpUtil.TryParse(gatewayText, out gateway)
                || !IpUtil.TryParse(_ip.Value, out ip)
                || !IpUtil.TryParse(_mask.Value, out mask)
                || !IpUtil.IsValidMask(mask))
            {
                return false;
            }
            return gateway != ip && (gateway & mask) == (ip & mask);
        }

        public PendingChange BuildPending()
        {
            PendingChange pending = new PendingChange();
            pending.Card = this;
            pending.Name = _info.Name;
            pending.Index = _info.Index;
            pending.Identity = AdapterIdentity;
            pending.Dhcp = _segment.SelectedIndex == 0;
            pending.Ip = _ip.Value;
            pending.Mask = _mask.Value;
            pending.Gateway = _gateway.Value;
            return pending;
        }

        private void ClearInvalid()
        {
            _ip.Invalid = false;
            _mask.Invalid = false;
            _gateway.Invalid = false;
        }

        private void UpdateHint()
        {
            if (!_info.EnabledKnown)
            {
                _lblHint.Text = L.T("État de la carte indisponible");
            }
            else if (!_info.Enabled)
            {
                _lblHint.Text = L.T("Carte désactivée");
            }
            else if (_segment.SelectedIndex == 0)
            {
                _lblHint.Text = L.T("Adresse attribuée automatiquement par le réseau");
            }
            else
            {
                _lblHint.Text = IsDirty ? L.T("Configuration fixe en attente") : L.T("Adresse configurée manuellement");
            }
        }

        private void UpdateInputs()
        {
            bool canEdit = _info.EnabledKnown && _info.Enabled && !_busy;
            bool staticMode = _segment.SelectedIndex == 1;

            _ip.ReadOnly = !staticMode;
            _mask.ReadOnly = !staticMode;
            _gateway.ReadOnly = !staticMode;

            _ip.Enabled = canEdit;
            _mask.Enabled = canEdit;
            _gateway.Enabled = canEdit;

            _segment.Enabled = canEdit;
            // Une carte désactivée doit pouvoir être réactivée ; seul un
            // traitement en cours rend l'interrupteur temporairement inactif.
            _toggle.Enabled = !_busy && _info.EnabledKnown;

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool dirty = IsDirty;
            bool ready = !_busy;
            _btnApply.Enabled = ready && dirty && _info.EnabledKnown && _info.Enabled;
            _btnCancel.Enabled = ready && dirty;
            _btnTest.Enabled = ready && !_testing && _info.EnabledKnown && _info.Enabled && _info.Ip.Length > 0;
            _btnTest.Text = _testing ? L.T("Test…") : L.T("Tester");
        }

        private void TestCard()
        {
            if (_testing || _busy || !_info.Enabled)
            {
                return;
            }

            string address = _info.Ip;
            if (address.Length == 0)
            {
                SetMessage(L.T("Cette carte n'a pas d'adresse IPv4 à tester."), MessageKind.Error);
                return;
            }

            _testing = true;
            string gateway = _info.Gateway;
            SetMessage(L.F("Test de « {0} » en cours…", address), MessageKind.Info);
            UpdateButtons();

            Task.Factory.StartNew<object[]>(delegate
            {
                PingOutcome self = NetPing.Send("", address, 1000);
                PingOutcome route = null;
                if (gateway.Length > 0)
                {
                    route = NetPing.Send(address, gateway, 1000);
                }
                return new object[] { self, route };
            }).ContinueWith(delegate(Task<object[]> task)
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        FinishTest(task, address, gateway);
                    }));
                }
                catch
                {
                }
            });
        }

        private void FinishTest(Task<object[]> task, string address, string gateway)
        {
            _testing = false;
            UpdateButtons();

            if (task.IsFaulted || task.IsCanceled)
            {
                SetMessage(L.T("Le test a échoué."), MessageKind.Error);
                return;
            }

            PingOutcome self = task.Result[0] as PingOutcome;
            PingOutcome route = task.Result[1] as PingOutcome;

            if (self == null || !self.Success)
            {
                SetMessage(L.F("Aucune réponse de l'adresse {0}.", address), MessageKind.Error);
                return;
            }

            if (route == null)
            {
                SetMessage(L.F("L'adresse {0} répond ({1} ms). Aucune passerelle définie.", address, self.Milliseconds), MessageKind.Success);
                return;
            }

            if (route.Success)
            {
                SetMessage(L.F("Adresse et passerelle joignables ({0} ms).", route.Milliseconds), MessageKind.Success);
            }
            else
            {
                SetMessage(L.F("L'adresse répond, mais la passerelle {0} ne répond pas.", gateway), MessageKind.Error);
            }
        }

        public void RequestApply()
        {
            if (_busy || !_info.Enabled || !IsDirty)
            {
                return;
            }
            if (ApplyRequested != null)
            {
                ApplyRequested(this, EventArgs.Empty);
            }
        }

        private void UpdateMessageLabel()
        {
            bool showSaved = _segment.SelectedIndex == 0 && _info.DhcpKnown && _info.Dhcp
                && _info.SavedStaticIp.Length > 0 && !IsDirty;
            bool sameGateway = (SavedIpv4Configuration.IsEmptyGateway(_info.SavedStaticGateway)
                && SavedIpv4Configuration.IsEmptyGateway(_gateway.Value))
                || SavedIpv4Configuration.SameIp(_info.SavedStaticGateway, _gateway.Value);
            bool showSavedDraft = _segment.SelectedIndex == 1 && !_info.Connected && _info.DhcpKnown && _info.Dhcp
                && !_ip.Invalid && !_mask.Invalid && !_gateway.Invalid
                && SavedIpv4Configuration.SameIp(_info.SavedStaticIp, _ip.Value)
                && SavedIpv4Configuration.SameIp(_info.SavedStaticMask, _mask.Value) && sameGateway;
            _lblSaved.Visible = showSaved || showSavedDraft;
            _lblSaved.Text = showSavedDraft
                ? L.F("Adresse fixe enregistrée : {0} ; DHCP actif, carte non connectée.", _info.SavedStaticIp)
                : showSaved ? L.F("DHCP actif ; adresse fixe enregistrée : {0}", _info.SavedStaticIp) : "";
            _tip.SetToolTip(_lblSaved, _lblSaved.Text);

            if (!string.IsNullOrEmpty(_message))
            {
                if (_messageKind == MessageKind.Warning && showSavedDraft)
                {
                    _lblMessage.Text = L.T("Mode fixe non confirmé");
                    _lblMessage.ForeColor = Theme.Warning;
                    _tip.SetToolTip(_lblMessage, L.T("L'adresse est enregistrée, mais Windows indique que le DHCP est encore actif."));
                    return;
                }
                _lblMessage.Text = _message;
                _lblMessage.ForeColor = _messageKind == MessageKind.Success
                    ? Theme.Success
                    : _messageKind == MessageKind.Warning
                        ? Theme.Warning
                    : _messageKind == MessageKind.Error
                        ? Theme.Danger
                        : Theme.TextMuted;
                _tip.SetToolTip(_lblMessage, _message);
                return;
            }

            if (!_info.EnabledKnown)
            {
                _lblMessage.Text = L.T("Actualisez pour vérifier l'état de cette carte.");
                _lblMessage.ForeColor = Theme.Warning;
                _tip.SetToolTip(_lblMessage, "");
                return;
            }

            if (!_info.Enabled)
            {
                _lblMessage.Text = L.T("Activez la carte pour modifier sa configuration.");
                _lblMessage.ForeColor = Theme.TextFaint;
                _tip.SetToolTip(_lblMessage, "");
                return;
            }

            if (IsDirty)
            {
                _lblMessage.Text = showSavedDraft ? L.T("Mode fixe non confirmé") : L.T("Modification non appliquée");
                _lblMessage.ForeColor = Theme.Warning;
                _tip.SetToolTip(_lblMessage, showSavedDraft
                    ? L.T("L'adresse est enregistrée, mais Windows indique que le DHCP est encore actif.")
                    : L.T("Cliquez sur Changer pour appliquer cette configuration."));
                return;
            }

            _lblMessage.Text = "";
            _tip.SetToolTip(_lblMessage, "");
        }

        private void OnStateChanged()
        {
            if (StateChanged != null)
            {
                StateChanged(this, EventArgs.Empty);
            }
        }

        private void ToggleChanged(object sender, EventArgs e)
        {
            if (_suppress)
            {
                return;
            }
            ClearMessage();
            UpdateHint();
            if (ToggleRequested != null)
            {
                ToggleRequested(this, EventArgs.Empty);
            }
        }

        private void SegmentChanged(object sender, EventArgs e)
        {
            if (_suppress)
            {
                return;
            }

            if (_segment.SelectedIndex == 0)
            {
                _staticDraftIp = _ip.Value;
                _staticDraftMask = _mask.Value;
                _staticDraftGateway = _gateway.Value;
                _staticDraftGatewayAuto = _gatewayAuto;
                _staticDraftGatewayFromConfig = _gatewayFromConfig;
                _staticDraftGatewayCleared = _userClearedGateway;
                _hasStaticDraft = true;

                if (_info.Dhcp)
                {
                    _ip.Value = _info.Ip;
                    _mask.Value = _info.Mask;
                    _gateway.Value = _info.Gateway;
                }

                _gatewayAuto = false;
                _gatewayFromConfig = false;
                _userClearedGateway = false;
            }
            else if (_hasStaticDraft)
            {
                _ip.Value = _staticDraftIp;
                _mask.Value = _staticDraftMask;
                _gateway.Value = _staticDraftGateway;
                _gatewayAuto = _staticDraftGatewayAuto;
                _gatewayFromConfig = _staticDraftGatewayFromConfig;
                _userClearedGateway = _staticDraftGatewayCleared;
            }
            else if (_info.Dhcp)
            {
                if (_info.SavedStaticIp.Length > 0 && _info.SavedStaticMask.Length > 0)
                {
                    _ip.Value = _info.SavedStaticIp;
                    _mask.Value = _info.SavedStaticMask;
                    _gateway.Value = _info.SavedStaticGateway;
                    _gatewayAuto = _info.SavedStaticGateway.Length == 0;
                    _gatewayFromConfig = _info.SavedStaticGateway.Length > 0;
                    _userClearedGateway = false;
                }
                else
                {
                    _ip.Value = DefaultStaticAddress;
                    if (_mask.Value.Length == 0)
                    {
                        _mask.Value = DefaultSubnetMask;
                    }
                    // Leave the gateway empty so the suggestion can fill it
                    // from the address and the mask.
                    _gateway.Value = "";
                    _gatewayAuto = true;
                    _gatewayFromConfig = false;
                    _userClearedGateway = false;
                }
            }
            else
            {
                _gatewayAuto = false;
                _gatewayFromConfig = _gateway.Value.Length > 0;
                _userClearedGateway = false;
            }

            RefreshSuggestedGateway();
            ClearMessage();
            ClearValidationError();
            UpdateHint();
            UpdateInputs();
            UpdateLiveValidation(null);
            UpdateMessageLabel();
            OnStateChanged();
        }

        private void InputChanged(object sender, EventArgs e)
        {
            if (_suppress)
            {
                return;
            }
            InputBox box = sender as InputBox;
            if (_errorField != null)
            {
                _errorTip.Hide(_errorField);
            }
            if (box == _gateway)
            {
                // A value typed (or cleared) by the user must never be replaced
                // by the automatic suggestion.
                _gatewayAuto = false;
                _gatewayFromConfig = false;
                _userClearedGateway = _gateway.Value.Length == 0;
            }
            else if (box == _ip || box == _mask)
            {
                RefreshSuggestedGateway();
            }
            ClearMessage();
            UpdateLiveValidation(box);
            UpdateHint();
            UpdateButtons();
            UpdateMessageLabel();
            OnStateChanged();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutCard();
        }

        private bool IsWideLayout
        {
            get { return Width >= Theme.S(900); }
        }

        private bool IsNarrowMode
        {
            get { return Width < Theme.S(620); }
        }

        private int CompactContentTop
        {
            get { return Theme.S(154); }
        }

        private int CompactFieldsTop
        {
            get { return CompactContentTop + (IsNarrowMode ? Theme.S(70) : Theme.S(50)); }
        }

        private int CompactFieldBottom
        {
            get
            {
                int rows = IsNarrowMode ? 3 : 2;
                int rowHeight = Theme.S(18) + Theme.S(36);
                return CompactFieldsTop + rows * rowHeight + (rows - 1) * Theme.S(12);
            }
        }

        private int CompactSavedTop
        {
            get { return CompactFieldBottom + Theme.S(6); }
        }

        private int CompactErrorTop
        {
            get { return CompactSavedTop + Theme.S(24); }
        }

        private int CompactActionsTop
        {
            get { return CompactErrorTop + Theme.S(24); }
        }

        private bool CompactMessageAboveButtons
        {
            get
            {
                int inner = Width - Theme.S(36);
                int buttons = Theme.S(124) + Theme.S(94) + Theme.S(94) + Theme.S(12);
                return inner < buttons + Theme.S(140);
            }
        }

        private int CompactHeight
        {
            get
            {
                int buttonTop = CompactActionsTop + (CompactMessageAboveButtons ? Theme.S(28) : 0);
                return buttonTop + Theme.S(36) + Theme.S(18);
            }
        }

        private void LayoutCard()
        {
            if (_lblName == null || _lblError == null || _lblSaved == null)
            {
                return;
            }

            int pad = Theme.S(18);
            int iconSize = Theme.S(44);
            int nameX = pad + iconSize + Theme.S(14);

            if (IsWideLayout)
            {
                int leftWidth = LeftColumnWidth();
                int nameWidth = Math.Max(Theme.S(80), leftWidth - iconSize - Theme.S(20));
                int dividerX = pad + leftWidth + Theme.S(11);
                int rightX = dividerX + Theme.S(11);
                int rightWidth = Math.Max(Theme.S(260), Width - rightX - pad);

                // Two lines are available for long service and hardware names.
                _lblName.SetBounds(nameX, Theme.S(11), nameWidth, Theme.S(42));
                _lblMeta.SetBounds(nameX, Theme.S(55), nameWidth, Theme.S(32));
                _lblStatus.SetBounds(nameX, Theme.S(90), nameWidth, Theme.S(18));

                _toggle.SetBounds(pad, Theme.S(114), Theme.S(46), Theme.S(26));
                _lblToggle.SetBounds(pad + Theme.S(56), Theme.S(118), Math.Max(Theme.S(80), leftWidth - Theme.S(74)), Theme.S(18));

                _segment.SetBounds(rightX, Theme.S(14), Theme.S(206), Theme.S(34));
                int hintX = rightX + Theme.S(220);
                int hintWidth = Math.Max(0, Width - pad - hintX);
                _lblHint.SetBounds(hintX, Theme.S(20), hintWidth, Theme.S(20));
                _lblHint.Visible = hintWidth > Theme.S(70);

                int gap = Theme.S(12);
                int total = rightWidth - gap * 2;
                int ipWidth = (int)(total * 0.36f);
                int maskWidth = (int)(total * 0.30f);
                int gatewayWidth = total - ipWidth - maskWidth;

                _lblIp.SetBounds(rightX, Theme.S(58), ipWidth, Theme.S(16));
                _lblMask.SetBounds(rightX + ipWidth + gap, Theme.S(58), maskWidth, Theme.S(16));
                _lblGateway.SetBounds(rightX + ipWidth + maskWidth + gap * 2, Theme.S(58), gatewayWidth, Theme.S(16));

                _ip.SetBounds(rightX, Theme.S(76), ipWidth, Theme.S(36));
                _mask.SetBounds(rightX + ipWidth + gap, Theme.S(76), maskWidth, Theme.S(36));
                _gateway.SetBounds(rightX + ipWidth + maskWidth + gap * 2, Theme.S(76), gatewayWidth, Theme.S(36));

                int buttonHeight = Theme.S(36);
                int buttonTop = Height - Theme.S(18) - buttonHeight;
                int applyWidth = Theme.S(124);
                int cancelWidth = Theme.S(94);
                int testWidth = Theme.S(94);

                _btnApply.SetBounds(Width - pad - applyWidth, buttonTop, applyWidth, buttonHeight);
                _btnCancel.SetBounds(_btnApply.Left - Theme.S(6) - cancelWidth, buttonTop, cancelWidth, buttonHeight);
                _btnTest.SetBounds(_btnCancel.Left - Theme.S(6) - testWidth, buttonTop, testWidth, buttonHeight);

                int messageWidth = Math.Max(0, _btnTest.Left - Theme.S(16) - rightX);
                _lblMessage.SetBounds(rightX, buttonTop + Theme.S(6), messageWidth, Theme.S(24));
                _lblSaved.SetBounds(rightX, Theme.S(113), rightWidth, Theme.S(20));

                if (Height != Theme.S(196))
                {
                    Height = Theme.S(196);
                }
                LayoutValidationError();
                return;
            }

            // Compact layout: identity on top, configuration underneath. Long
            // names wrap instead of being truncated, and the fields reflow from
            // two columns to one when the window gets narrow.
            int compactNameWidth = Math.Max(Theme.S(80), Width - nameX - pad);
            _lblName.SetBounds(nameX, Theme.S(11), compactNameWidth, Theme.S(42));
            _lblMeta.SetBounds(nameX, Theme.S(55), compactNameWidth, Theme.S(32));
            _lblStatus.SetBounds(nameX, Theme.S(90), compactNameWidth, Theme.S(18));

            _toggle.SetBounds(pad, Theme.S(114), Theme.S(46), Theme.S(26));
            _lblToggle.SetBounds(pad + Theme.S(56), Theme.S(118), Math.Max(Theme.S(80), Width - pad * 2 - Theme.S(56)), Theme.S(18));

            _segment.SetBounds(pad, CompactContentTop, Theme.S(206), Theme.S(34));
            int compactHintX = pad + Theme.S(220);
            int compactHintWidth = Width - pad - compactHintX;
            if (!IsNarrowMode && compactHintWidth >= Theme.S(80))
            {
                _lblHint.SetBounds(compactHintX, CompactContentTop + Theme.S(6), compactHintWidth, Theme.S(20));
            }
            else
            {
                _lblHint.SetBounds(pad, CompactContentTop + Theme.S(40), Width - pad * 2, Theme.S(20));
            }
            _lblHint.Visible = true;

            int compactInner = Width - pad * 2;
            int fieldsTop = CompactFieldsTop;
            int fieldGap = Theme.S(12);
            if (!IsNarrowMode)
            {
                int columnWidth = Math.Max(Theme.S(120), (compactInner - fieldGap) / 2);
                _lblIp.SetBounds(pad, fieldsTop, columnWidth, Theme.S(16));
                _ip.SetBounds(pad, fieldsTop + Theme.S(18), columnWidth, Theme.S(36));
                int secondX = pad + columnWidth + fieldGap;
                _lblMask.SetBounds(secondX, fieldsTop, columnWidth, Theme.S(16));
                _mask.SetBounds(secondX, fieldsTop + Theme.S(18), columnWidth, Theme.S(36));

                int secondRow = fieldsTop + Theme.S(18) + Theme.S(36) + fieldGap;
                _lblGateway.SetBounds(pad, secondRow, compactInner, Theme.S(16));
                _gateway.SetBounds(pad, secondRow + Theme.S(18), compactInner, Theme.S(36));
            }
            else
            {
                _lblIp.SetBounds(pad, fieldsTop, compactInner, Theme.S(16));
                _ip.SetBounds(pad, fieldsTop + Theme.S(18), compactInner, Theme.S(36));

                int secondRow = fieldsTop + Theme.S(18) + Theme.S(36) + fieldGap;
                _lblMask.SetBounds(pad, secondRow, compactInner, Theme.S(16));
                _mask.SetBounds(pad, secondRow + Theme.S(18), compactInner, Theme.S(36));

                int thirdRow = secondRow + Theme.S(18) + Theme.S(36) + fieldGap;
                _lblGateway.SetBounds(pad, thirdRow, compactInner, Theme.S(16));
                _gateway.SetBounds(pad, thirdRow + Theme.S(18), compactInner, Theme.S(36));
            }

            _lblSaved.SetBounds(pad, CompactSavedTop, compactInner, Theme.S(20));

            int compactButtonTop = CompactActionsTop + (CompactMessageAboveButtons ? Theme.S(28) : 0);
            int compactApplyWidth = Theme.S(124);
            int compactCancelWidth = Theme.S(94);
            int compactTestWidth = Theme.S(94);
            int compactButtonHeight = Theme.S(36);

            _btnApply.SetBounds(Width - pad - compactApplyWidth, compactButtonTop, compactApplyWidth, compactButtonHeight);
            _btnCancel.SetBounds(_btnApply.Left - Theme.S(6) - compactCancelWidth, compactButtonTop, compactCancelWidth, compactButtonHeight);
            _btnTest.SetBounds(_btnCancel.Left - Theme.S(6) - compactTestWidth, compactButtonTop, compactTestWidth, compactButtonHeight);

            if (CompactMessageAboveButtons)
            {
                _lblMessage.SetBounds(pad, CompactActionsTop, compactInner, Theme.S(24));
            }
            else
            {
                int messageWidth = Math.Max(0, _btnTest.Left - Theme.S(16) - pad);
                _lblMessage.SetBounds(pad, compactButtonTop + Theme.S(6), messageWidth, Theme.S(24));
            }

            int compactHeight = CompactHeight;
            if (Height != compactHeight)
            {
                Height = compactHeight;
            }
            LayoutValidationError();
        }

        private void LayoutValidationError()
        {
            if (_lblError == null)
            {
                return;
            }
            int pad = Theme.S(18);
            if (!IsWideLayout)
            {
                _lblError.SetBounds(pad, CompactErrorTop, Math.Max(0, Width - pad * 2), Theme.S(20));
                return;
            }

            int rightX = pad + LeftColumnWidth() + Theme.S(22);
            int rightEdge = Math.Max(rightX, Width - pad);
            int latestLeft = Math.Max(rightX, rightEdge - Theme.S(360));
            int left = _errorField == null ? rightX : Math.Max(rightX, Math.Min(_errorField.Left, latestLeft));
            _lblError.SetBounds(left, Theme.S(113), Math.Max(0, rightEdge - left), Theme.S(20));
        }

        private int LeftColumnWidth()
        {
            return Math.Min(Theme.S(420), Math.Max(Theme.S(300), Width - Theme.S(620)));
        }

        private void UpdateStatusColor()
        {
            _lblStatus.ForeColor = !_info.Enabled
                ? Theme.Warning
                : _info.Connected ? Theme.Success : Theme.TextFaint;
        }

        public void ApplyLanguage()
        {
            int selectedMode = _segment.SelectedIndex;
            _suppress = true;
            try
            {
                _lblName.Text = _info.Name;
                _lblMeta.Text = _info.MetaLabel;
                _lblStatus.Text = "●  " + _info.StatusLabel;
                _lblToggle.Text = L.T("Carte active");
                _lblIp.Text = L.T("Adresse IP");
                _lblMask.Text = L.T("Masque");
                _lblGateway.Text = L.T("Passerelle");
                _segment.Items = new string[] { "DHCP", L.T("Adresse fixe") };
                _segment.SetSelectedSilent(selectedMode);
                _ip.Placeholder = L.T("ex. 192.168.1.50");
                _gateway.Placeholder = L.T("ex. 192.168.1.1");
                _btnApply.Text = L.T("Changer");
                _btnCancel.Text = L.T("Annuler");
                _btnTest.Text = _testing ? L.T("Test…") : L.T("Tester");
                _errorTip.ToolTipTitle = L.T("Vérifiez la saisie");

                _tip.SetToolTip(_toggle, L.T("Activer ou désactiver la carte réseau"));
                _tip.SetToolTip(_segment, L.T("Choisir entre l'attribution automatique et une adresse fixe"));
                _tip.SetToolTip(_btnApply, L.T("Appliquer la configuration de cette carte"));
                _tip.SetToolTip(_btnCancel, L.T("Revenir à la configuration actuelle de cette carte"));
                _tip.SetToolTip(_btnTest, L.T("Vérifier que l'adresse IP de cette carte et sa passerelle répondent"));
                _tip.SetToolTip(_ip, L.T("Adresse IPv4 de la carte, par exemple 192.168.1.50"));
                _tip.SetToolTip(_mask, L.T("Masque de sous-réseau, par exemple 255.255.255.0"));
                _tip.SetToolTip(_gateway, L.T("Passerelle par défaut, par exemple 192.168.1.1 (facultative)"));
            }
            finally
            {
                _suppress = false;
            }

            // Les messages transitoires sont effacés pour éviter de mélanger
            // deux langues dans la même carte.
            _message = null;
            _messageKind = MessageKind.None;
            ClearValidationError();
            UpdateStatusColor();
            UpdateHint();
            UpdateLiveValidation(null);
            UpdateMessageLabel();
            UpdateButtons();
            Invalidate();
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();

            _lblName.BackColor = Theme.Surface;
            _lblName.ForeColor = Theme.Text;
            _lblMeta.BackColor = Theme.Surface;
            _lblMeta.ForeColor = Theme.TextMuted;
            _lblStatus.BackColor = Theme.Surface;
            UpdateStatusColor();
            _lblToggle.BackColor = Theme.Surface;
            _lblToggle.ForeColor = Theme.TextMuted;
            _lblHint.BackColor = Theme.Surface;
            _lblHint.ForeColor = Theme.TextMuted;
            _lblMessage.BackColor = Theme.Surface;
            _lblError.BackColor = Theme.Surface;
            _lblError.ForeColor = Theme.Danger;
            _lblSaved.BackColor = Theme.Surface;
            _lblSaved.ForeColor = Theme.Warning;
            _lblIp.BackColor = Theme.Surface;
            _lblIp.ForeColor = Theme.TextMuted;
            _lblMask.BackColor = Theme.Surface;
            _lblMask.ForeColor = Theme.TextMuted;
            _lblGateway.BackColor = Theme.Surface;
            _lblGateway.ForeColor = Theme.TextMuted;

            UpdateMessageLabel();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int pad = Theme.S(18);
            Rectangle iconBox = new Rectangle(pad, Theme.S(12), Theme.S(44), Theme.S(44));
            Color soft = !_info.Enabled ? Theme.SurfaceSunken : _info.Wireless ? Theme.WifiSoft : Theme.AccentSoft;
            Color strong = !_info.Enabled ? Theme.TextFaint : _info.Wireless ? Theme.Wifi : Theme.Accent;

            using (SolidBrush brush = new SolidBrush(soft))
            {
                g.FillEllipse(brush, iconBox);
            }

            Rectangle glyph = Theme.Deflate(iconBox, Theme.S(12));
            if (_info.Wireless)
            {
                Glyphs.Wifi(g, glyph, strong, Theme.S(1.8f));
            }
            else
            {
                Glyphs.Ethernet(g, glyph, strong, Theme.S(1.8f));
            }

            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                if (IsWideLayout)
                {
                    int dividerX = pad + LeftColumnWidth() + Theme.S(11);
                    g.DrawLine(pen, dividerX, Theme.S(16), dividerX, Height - Theme.S(16));
                }
                else
                {
                    g.DrawLine(pen, pad, Theme.S(146), Width - pad, Theme.S(146));
                }
            }
        }
    }
}
