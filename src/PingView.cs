using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal sealed class PingView : CardPanel
    {
        private sealed class Entry
        {
            public AdapterInfo Info;
            public string Display = "";
            public override string ToString()
            {
                return Display;
            }
        }

        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _lblAdapter;
        private readonly Label _lblTarget;
        private readonly Label _lblLog;
        private readonly Label _lblState;
        private readonly SelectBox _combo;
        private readonly InputBox _target;
        private readonly FlatButton _btnPing;
        private readonly FlatButton _btnClear;
        private readonly RichTextBox _log;

        private readonly List<Entry> _entries = new List<Entry>();
        private bool _busy;
        private string _selection = "";

        public PingView()
        {
            BackColor = Theme.Surface;

            _title = MakeLabel(Theme.CardTitle, Theme.Text);
            _title.Text = L.T("Tester une adresse IP sur le réseau");

            _subtitle = MakeLabel(Theme.Small, Theme.TextMuted);
            _subtitle.Text = L.T("Choisissez la carte qui doit envoyer le test, puis saisissez une adresse IPv4 à joindre.");

            _lblAdapter = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblAdapter.Text = L.T("Carte réseau (source)");

            _lblTarget = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblTarget.Text = L.T("Adresse IPv4 à tester");

            _lblLog = MakeLabel(Theme.SmallBold, Theme.TextMuted);
            _lblLog.Text = L.T("Journal des tests");

            _lblState = MakeLabel(Theme.Small, Theme.TextMuted);
            _lblState.Text = L.T("Aucun test lancé pour le moment.");

            _combo = new SelectBox();
            _combo.SelectedIndexChanged += delegate { UpdateSource(); };

            _target = new InputBox();
            _target.Placeholder = L.T("ex. 192.168.1.1");
            _target.NumericOnly = true;
            _target.Value = "";
            _target.ValueChanged += delegate
            {
                _target.Invalid = false;
                if (_lblState.ForeColor == Theme.Danger)
                {
                    SetState(L.T("Aucun test lancé pour le moment."), Theme.TextMuted);
                }
            };

            _btnPing = new FlatButton();
            _btnPing.Kind = ButtonKind.Primary;
            _btnPing.Text = "Ping";
            _btnPing.Click += delegate { StartPing(); };

            _btnClear = new FlatButton();
            _btnClear.Kind = ButtonKind.Ghost;
            _btnClear.Text = L.T("Effacer");
            _btnClear.Click += delegate
            {
                _log.Clear();
                SetState(L.T("Journal effacé."), Theme.TextMuted);
            };

            _log = new RichTextBox();
            _log.BorderStyle = BorderStyle.None;
            _log.ReadOnly = true;
            _log.DetectUrls = false;
            _log.BackColor = Theme.SurfaceSunken;
            _log.ForeColor = Theme.Text;
            _log.Font = Theme.Small;
            _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.WordWrap = true;
            _log.TabStop = false;

            Controls.Add(_title);
            Controls.Add(_subtitle);
            Controls.Add(_lblAdapter);
            Controls.Add(_lblTarget);
            Controls.Add(_lblLog);
            Controls.Add(_lblState);
            Controls.Add(_combo);
            Controls.Add(_target);
            Controls.Add(_btnPing);
            Controls.Add(_btnClear);
            Controls.Add(_log);

            SetState(L.T("Aucun test lancé pour le moment."), Theme.TextMuted);
            LayoutView();
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

        private static string DisplayFor(AdapterInfo info)
        {
            return info.Name + "  —  " + info.Ip + (info.Dhcp ? " (DHCP)" : " " + L.T("(fixe)"));
        }

        public void SetAdapters(List<AdapterInfo> adapters)
        {
            _entries.Clear();
            if (adapters != null)
            {
                for (int i = 0; i < adapters.Count; i++)
                {
                    AdapterInfo info = adapters[i];
                    if (!info.EnabledKnown || !info.Enabled || info.Ip.Length == 0)
                    {
                        continue;
                    }
                    Entry entry = new Entry();
                    entry.Info = info;
                    entry.Display = DisplayFor(info);
                    _entries.Add(entry);
                }
            }

            string previous = _selection;
            _combo.ClearItems();
            for (int i = 0; i < _entries.Count; i++)
            {
                _combo.AddItem(_entries[i]);
            }
            if (_entries.Count == 0)
            {
                _combo.AddItem(L.T("Aucune carte avec adresse IPv4"));
            }

            int index = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Info.Name, previous, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0 && _entries.Count > 0)
            {
                index = 0;
            }
            if (_entries.Count == 0)
            {
                _combo.SelectedIndex = 0;
                UpdateSource();
                return;
            }
            _combo.SelectedIndex = index;
            UpdateSource();
        }

        private void UpdateSource()
        {
            Entry entry = SelectedEntry();
            _selection = entry == null ? "" : entry.Info.Name;
            if (entry != null && _target.Value.Length == 0)
            {
                string guess = entry.Info.Gateway.Length > 0 ? entry.Info.Gateway : entry.Info.Ip;
                if (guess.Length > 0 && guess != entry.Info.Ip)
                {
                    _target.Value = guess;
                }
            }
        }

        private Entry SelectedEntry()
        {
            int index = _combo.SelectedIndex;
            if (index < 0 || index >= _entries.Count)
            {
                return null;
            }
            return _entries[index];
        }

        private void StartPing()
        {
            if (_busy)
            {
                return;
            }

            Entry entry = SelectedEntry();
            if (entry == null)
            {
                SetState(L.T("Aucune carte réseau avec une adresse IPv4 n'est disponible."), Theme.Danger);
                return;
            }

            string target = (_target.Value ?? "").Trim();
            uint parsed;
            if (!IpUtil.TryParse(target, out parsed))
            {
                _target.Invalid = true;
                SetState(L.T("Adresse invalide : utilisez le format 192.168.1.1"), Theme.Danger);
                _target.FocusText();
                return;
            }
            if (parsed == 0u || parsed == 0xFFFFFFFFu)
            {
                _target.Invalid = true;
                SetState(L.T("Cette adresse ne peut pas être testée."), Theme.Danger);
                _target.FocusText();
                return;
            }
            if (entry.Info.Ip.Length == 0)
            {
                SetState(L.T("La carte sélectionnée n'a pas d'adresse IPv4."), Theme.Danger);
                return;
            }

            _target.Invalid = false;
            _busy = true;
            _btnPing.Enabled = false;
            _btnPing.Text = L.T("Test…");
            _combo.Enabled = false;
            _target.Enabled = false;
            SetState(L.F("Ping en cours vers {0} via {1}…", target, entry.Info.Name), Theme.TextMuted);

            string source = entry.Info.Ip;
            string adapterName = entry.Info.Name;

            Task.Factory.StartNew<PingOutcome>(delegate
            {
                return NetPing.Send(source, target, 1200);
            }).ContinueWith(delegate(Task<PingOutcome> task)
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        FinishPing(task, target, adapterName, source);
                    }));
                }
                catch
                {
                }
            });
        }

        private void FinishPing(Task<PingOutcome> task, string target, string adapterName, string source)
        {
            _busy = false;
            _btnPing.Enabled = true;
            _btnPing.Text = "Ping";
            _combo.Enabled = true;
            _target.Enabled = true;

            if (task.IsFaulted || task.IsCanceled)
            {
                string message = task.Exception != null ? ErrorText(task.Exception) : L.T("Erreur inconnue.");
                AppendLine(L.F("Échec du test vers {0} : {1}", target, message), Theme.Danger);
                SetState(L.F("Le test a échoué : {0}", message), Theme.Danger);
                return;
            }

            PingOutcome outcome = task.Result;
            string stamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            string sourceText = source.Length > 0 ? " via " + adapterName + " (" + source + ")" : "";
            string detail = outcome.Detail;

            if (outcome.Success)
            {
                AppendLine(stamp + "   →   " + target + sourceText + "   ·   " + detail, Theme.Success);
                SetState(L.F("Réponse de {0} {1}.", target, detail.ToLowerInvariant()), Theme.Success);
            }
            else
            {
                AppendLine(stamp + "   →   " + target + sourceText + "   ·   " + detail, Theme.Danger);
                SetState(L.F("Aucune réponse de {0} : {1}.", target, detail), Theme.Danger);
            }
        }

        private void AppendLine(string text, Color color)
        {
            if (_log.TextLength > 0)
            {
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = _log.ForeColor;
                _log.AppendText(Environment.NewLine);
            }
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = color;
            _log.AppendText(text);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void SetState(string text, Color color)
        {
            _lblState.Text = text;
            _lblState.ForeColor = color;
            _lblState.BackColor = Theme.Surface;
        }

        private static string ErrorText(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }
            return current.Message;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && !_busy)
            {
                StartPing();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutView();
        }

        private void LayoutView()
        {
            if (_title == null || _log == null)
            {
                return;
            }

            int pad = Theme.S(24);
            int inner = Math.Max(Theme.S(200), Width - pad * 2);
            int gap = Theme.S(14);

            _title.SetBounds(pad, Theme.S(18), inner, Theme.S(26));
            _subtitle.SetBounds(pad, Theme.S(46), inner, Theme.S(20));

            int labelTop = Theme.S(84);
            int fieldTop = Theme.S(104);
            int fieldHeight = Theme.S(36);

            int comboWidth = (int)(inner * 0.42f);
            int targetWidth = (int)(inner * 0.27f);
            int buttonWidth = Theme.S(120);
            if (comboWidth + targetWidth + buttonWidth + gap * 2 > inner)
            {
                comboWidth = Math.Max(Theme.S(160), inner - targetWidth - buttonWidth - gap * 2);
            }

            _lblAdapter.SetBounds(pad, labelTop, comboWidth, Theme.S(18));
            _lblTarget.SetBounds(pad + comboWidth + gap, labelTop, targetWidth, Theme.S(18));

            _combo.SetBounds(pad, fieldTop, comboWidth, Theme.S(34));
            _target.SetBounds(pad + comboWidth + gap, fieldTop, targetWidth, fieldHeight);

            _btnPing.SetBounds(pad + comboWidth + gap + targetWidth + gap, fieldTop, buttonWidth, fieldHeight);
            _btnClear.SetBounds(pad + inner - Theme.S(92), Theme.S(142), Theme.S(92), Theme.S(30));

            _lblLog.SetBounds(pad, Theme.S(147), inner - Theme.S(110), Theme.S(20));

            int logTop = Theme.S(172);
            int stateHeight = Theme.S(22);
            int logHeight = Math.Max(Theme.S(60), Height - logTop - pad - stateHeight - Theme.S(6));
            _log.SetBounds(pad, logTop, inner, logHeight);

            _lblState.SetBounds(pad, logTop + logHeight + Theme.S(6), inner, stateHeight);
        }

        public void ApplyLanguage()
        {
            _title.Text = L.T("Tester une adresse IP sur le réseau");
            _subtitle.Text = L.T("Choisissez la carte qui doit envoyer le test, puis saisissez une adresse IPv4 à joindre.");
            _lblAdapter.Text = L.T("Carte réseau (source)");
            _lblTarget.Text = L.T("Adresse IPv4 à tester");
            _lblLog.Text = L.T("Journal des tests");
            _target.Placeholder = L.T("ex. 192.168.1.1");
            _btnClear.Text = L.T("Effacer");
            _btnPing.Text = _busy ? L.T("Test…") : "Ping";

            int selected = _combo.SelectedIndex;
            _combo.ClearItems();
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Display = DisplayFor(_entries[i].Info);
                _combo.AddItem(_entries[i]);
            }
            if (_entries.Count == 0)
            {
                _combo.AddItem(L.T("Aucune carte avec adresse IPv4"));
                _combo.SelectedIndex = 0;
            }
            else
            {
                _combo.SelectedIndex = selected < 0 || selected >= _entries.Count ? 0 : selected;
            }

            if (!_busy)
            {
                SetState(L.T("Aucun test lancé pour le moment."), Theme.TextMuted);
            }
            LayoutView();
            Invalidate();
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();

            _title.BackColor = Theme.Surface;
            _title.ForeColor = Theme.Text;
            _subtitle.BackColor = Theme.Surface;
            _subtitle.ForeColor = Theme.TextMuted;
            _lblAdapter.BackColor = Theme.Surface;
            _lblAdapter.ForeColor = Theme.TextMuted;
            _lblTarget.BackColor = Theme.Surface;
            _lblTarget.ForeColor = Theme.TextMuted;
            _lblLog.BackColor = Theme.Surface;
            _lblLog.ForeColor = Theme.TextMuted;
            _lblState.BackColor = Theme.Surface;
            _log.BackColor = Theme.SurfaceSunken;
            _log.ForeColor = Theme.Text;

            Invalidate();
        }
    }
}
