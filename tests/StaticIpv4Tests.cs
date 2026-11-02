using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    // The fake backend records requests instead of changing a real interface.
    internal static class StaticIpv4Tests
    {
        private static int _assertions;
        private const string Ip = "192.168.0.45";
        private const string Mask = "255.255.255.0";
        private const string Gateway = "192.168.0.1";
        private const string GuidText = "{8C836DC5-4E9A-4924-B06C-A6A7C35D14F3}";

        public static int Run()
        {
            OfflineStaticConfigurationIsVerifiedFromSavedValues();
            MixedDhcpAndStaticIsNotReportedAsSuccess();
            ConnectedConfigurationRequiresLiveAndSavedMatch();
            ErrorsAndUnknownResultsDoNotChangeDhcp();
            InvalidValuesNeverReachTheBackend();
            SavedValuesAreComparedSafely();
            SavedOfflineConfigurationIsDisplayedHonestly();
            ApplyResultsKeepDraftsAndExplainPartialState();
            PhysicalEthernetNameIsDisplayed();
            OptionalReadOnlyDisconnectedProbe();
            return _assertions;
        }

        private static AdapterIdentity Identity()
        {
            return new AdapterIdentity(14, "Ethernet 2", GuidText, "7");
        }

        private static AdapterInfo Adapter(bool connected, bool dhcp)
        {
            AdapterInfo info = new AdapterInfo();
            info.Name = "Ethernet 2";
            info.Description = "Realtek Gaming USB 2.5GbE Family Controller";
            info.Index = 14;
            info.InterfaceGuid = GuidText;
            info.Enabled = true;
            info.EnabledKnown = true;
            info.Connected = connected;
            info.DhcpKnown = true;
            info.Dhcp = dhcp;
            info.Ip = connected ? Ip : "";
            info.Mask = connected ? Mask : "";
            info.Gateway = connected ? Gateway : "";
            return info;
        }

        private static SavedIpv4Configuration Saved(bool dhcp, string ip, string mask, string gateway)
        {
            return SavedIpv4Configuration.FromValues(dhcp ? 1 : 0,
                new string[] { ip }, new string[] { mask }, new string[] { gateway });
        }

        private static StaticIpv4Snapshot Snapshot(bool connected, bool activeDhcp, bool savedDhcp)
        {
            StaticIpv4Snapshot snapshot = new StaticIpv4Snapshot();
            snapshot.Adapter = Adapter(connected, activeDhcp);
            snapshot.Saved = Saved(savedDhcp, Ip, Mask, Gateway);
            return snapshot;
        }

        private static void OfflineStaticConfigurationIsVerifiedFromSavedValues()
        {
            FakeBackend backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, false, false);
            int delays = 0;
            OpResult result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { delays++; });
            Assert(result.Success && !result.Unconfirmed, "A saved static configuration must be accepted without a physical link.");
            Assert(result.Message.IndexOf("enregistrée", StringComparison.Ordinal) >= 0
                && result.Message.IndexOf("non connectée", StringComparison.Ordinal) >= 0, "Offline success must distinguish saved settings from connectivity.");
            Assert(backend.SetCalls == 1 && backend.ReadCalls == 1 && delays == 0, "A successful static request must be sent only once.");
            Assert(backend.LastIp == Ip && backend.LastMask == Mask && backend.LastGateway == Gateway, "The exact intended static values must be sent.");
        }

        private static void MixedDhcpAndStaticIsNotReportedAsSuccess()
        {
            FakeBackend backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, true, true);
            int delays = 0;
            OpResult result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { delays++; });
            Assert(!result.Success && result.Unconfirmed && result.PartiallyApplied, "Saved static values with DHCP enabled must be reported as partial, not as a complete success.");
            Assert(result.Message.IndexOf("DHCP", StringComparison.Ordinal) >= 0, "The actual DHCP conflict must be explained.");
            Assert(result.Message.IndexOf("non connectée", StringComparison.Ordinal) >= 0, "The disconnected condition must be explained.");
            Assert(result.Message.IndexOf("enregistrée", StringComparison.Ordinal) >= 0, "The saved address must be acknowledged.");
            Assert(backend.SetCalls == 1 && backend.ReadCalls == 8 && delays == 7, "Verification must never issue a DHCP fallback or repeat the command.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, false, true);
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && result.Message.IndexOf("DHCP", StringComparison.Ordinal) >= 0, "A disagreement between active and saved DHCP state must not be accepted.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, true, false);
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && result.Unconfirmed && result.Message.IndexOf("enregistrée", StringComparison.Ordinal) >= 0, "Saved static settings with a stale active DHCP state must remain unconfirmed.");
            Assert(backend.SetCalls == 1, "An unconfirmed active DHCP state must not trigger a retry.");
        }

        private static void ConnectedConfigurationRequiresLiveAndSavedMatch()
        {
            FakeBackend backend = new FakeBackend();
            backend.Snapshot = Snapshot(true, false, false);
            OpResult result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(result.Success, "A connected interface with matching live and saved settings must succeed.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(true, false, false);
            backend.Snapshot.Adapter.Gateway = "192.168.0.254";
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && result.Unconfirmed, "A different live gateway must not be reported as success.");
            Assert(backend.SetCalls == 1, "An unconfirmed configuration must not trigger additional changes.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, false, false);
            backend.Snapshot.Saved = Saved(false, Ip, "255.255.0.0", Gateway);
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && !result.Unconfirmed, "A mismatched saved mask must be reported as incomplete.");
            Assert(result.Message.IndexOf("ne correspondent pas", StringComparison.Ordinal) >= 0, "A saved mismatch must be explained.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, false, false);
            backend.Snapshot.Saved = Saved(false, Ip, Mask, "0.0.0.0");
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, "", backend, delegate(int ms) { });
            Assert(result.Success, "An optional gateway may be absent in saved settings.");
        }

        private static void ErrorsAndUnknownResultsDoNotChangeDhcp()
        {
            FakeBackend backend = new FakeBackend();
            backend.Command.ExitCode = 5;
            backend.Command.Detail = "Accès refusé";
            OpResult result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && !result.Unconfirmed && result.Message.IndexOf("Accès refusé", StringComparison.Ordinal) >= 0, "Windows errors must be surfaced.");
            Assert(backend.SetCalls == 1 && backend.ReadCalls == 0, "A failed command must not cause a DHCP retry.");

            backend = new FakeBackend();
            backend.Command.ExitCode = -2;
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && result.Unconfirmed, "A timed-out command may have partially changed Windows and must remain unconfirmed.");
            Assert(backend.SetCalls == 1 && backend.ReadCalls == 0, "A timeout must not trigger another configuration command.");

            backend = new FakeBackend();
            backend.Snapshot = Snapshot(false, false, false);
            backend.Snapshot.Saved = null;
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && result.Unconfirmed, "Missing saved settings must not become a false success.");
            Assert(backend.SetCalls == 1 && backend.ReadCalls == 8, "Read failures must not trigger DHCP.");
        }

        private static void InvalidValuesNeverReachTheBackend()
        {
            FakeBackend backend = new FakeBackend();
            OpResult result = StaticIpv4Configuration.Apply(Identity(), Gateway, Mask, Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && backend.SetCalls == 0, "An IP equal to its gateway must be rejected before any command.");
            result = StaticIpv4Configuration.Apply(Identity(), Ip, "255.0.255.0", Gateway, backend, delegate(int ms) { });
            Assert(!result.Success && backend.SetCalls == 0, "A non-contiguous mask must be rejected before any command.");
            result = StaticIpv4Configuration.Apply(Identity(), Ip, Mask, "192.168.1.1", backend, delegate(int ms) { });
            Assert(!result.Success && backend.SetCalls == 0, "A gateway outside the subnet must be rejected before any command.");
        }

        private static void SavedValuesAreComparedSafely()
        {
            SavedIpv4Configuration saved = SavedIpv4Configuration.FromValues(0,
                new string[] { "10.0.0.5", "192.168.0.45" },
                new string[] { "255.0.0.0", "255.255.255.0" },
                new string[] { "0.0.0.0", "192.168.0.1" });
            Assert(saved.DhcpEnabled == false, "Saved DHCP mode must be decoded.");
            Assert(saved.MatchesAddressAndMask("192.168.0.045", Mask), "Matching must compare IPv4 values, not only formatting.");
            Assert(saved.MatchesGateway(Gateway), "Any configured matching gateway must be found.");
            Assert(!saved.MatchesGateway(""), "An existing real gateway must not be mistaken for no gateway.");
        }

        private static void SavedOfflineConfigurationIsDisplayedHonestly()
        {
            AdapterInfo staticInfo = Adapter(false, false);
            NetworkService.ApplySavedOfflineConfiguration(staticInfo, Saved(false, Ip, Mask, Gateway));
            Assert(staticInfo.Ip == Ip && staticInfo.Mask == Mask && staticInfo.Gateway == Gateway, "Saved static values must remain visible when a card is disconnected.");

            AdapterInfo mixed = Adapter(false, true);
            NetworkService.ApplySavedOfflineConfiguration(mixed, Saved(true, Ip, Mask, Gateway));
            Assert(mixed.Ip.Length == 0 && mixed.SavedStaticIp == Ip && mixed.Dhcp, "A saved static address must not be presented as the active DHCP address.");
            using (AdapterCard card = new AdapterCard(mixed))
            {
                string message = Field<Label>(card, "_lblSaved").Text;
                Assert(message.IndexOf("DHCP actif", StringComparison.Ordinal) >= 0 && message.IndexOf(Ip, StringComparison.Ordinal) >= 0, "A mixed DHCP/static configuration must be visible to the user.");
                Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
                Assert(Field<InputBox>(card, "_ip").Value == Ip && Field<InputBox>(card, "_mask").Value == Mask
                    && Field<InputBox>(card, "_gateway").Value == Gateway, "Switching to static must restore existing saved settings.");
                Assert(Field<Label>(card, "_lblSaved").Text.IndexOf("Adresse fixe enregistrée", StringComparison.Ordinal) >= 0,
                    "The static card must explain both the saved address and active DHCP.");
                Assert(Field<Label>(card, "_lblMessage").Text == "Mode fixe non confirmé",
                    "An already saved address must not be described as wholly unapplied.");
            }

            AdapterInfo unknown = Adapter(false, true);
            unknown.DhcpKnown = false;
            NetworkService.ApplySavedOfflineConfiguration(unknown, Saved(false, Ip, Mask, Gateway));
            Assert(unknown.DhcpKnown && !unknown.Dhcp && unknown.Ip == Ip, "Saved mode may be used when the live DHCP mode is unavailable.");
        }

        private static void ApplyResultsKeepDraftsAndExplainPartialState()
        {
            string partial = "Configuration incomplète : DHCP actif.";
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddDraftCard(form);
                FinishApply(form, card, OpResult.Fail(partial), Adapter(false, true));
                Assert(card.IsDirty && Field<InputBox>(card, "_ip").Value == Ip, "A partial failure must preserve the user's static draft.");
                Assert(Field<Label>(card, "_lblMessage").Text == partial, "The card must show the real failure detail.");
                Assert(Field<Label>(form, "_lblStatus").Text == partial, "The footer must show the real failure detail.");
                Assert(Field<Label>(card, "_lblMessage").ForeColor == Theme.Danger, "A confirmed partial failure must be red.");
            }

            string awaiting = "Demande envoyée, état non confirmé.";
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddDraftCard(form);
                FinishApply(form, card, OpResult.Awaiting(awaiting), Adapter(false, true));
                Assert(card.IsDirty && Field<InputBox>(card, "_ip").Value == Ip, "An unconfirmed result must preserve the draft.");
                Assert(Field<Label>(form, "_lblStatus").Text == awaiting, "An unconfirmed result must be reported accurately.");
                Assert(Field<Label>(card, "_lblMessage").ForeColor != Theme.Danger, "An unconfirmed result must not be presented as a definite failure.");
            }

            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddDraftCard(form);
                AdapterInfo mixed = Adapter(false, true);
                NetworkService.ApplySavedOfflineConfiguration(mixed, Saved(true, Ip, Mask, Gateway));
                string message = "Adresse fixe enregistrée : 192.168.0.45. Carte non connectée ; DHCP encore actif.";
                FinishApply(form, card, OpResult.Partial(message), mixed);
                Assert(card.IsDirty && Field<InputBox>(card, "_ip").Value == Ip, "Partial success must keep the unconfirmed mode as pending.");
                Assert(Field<Label>(form, "_lblStatus").ForeColor == Theme.Warning, "Partial results must use a warning, not an error.");
                Assert(Field<Label>(card, "_lblSaved").Text.IndexOf("Adresse fixe enregistrée", StringComparison.Ordinal) >= 0,
                    "The card must display the recorded address and DHCP state without a false failure.");
                Assert(Field<Label>(card, "_lblMessage").Text == "Mode fixe non confirmé",
                    "Partial success must distinguish the saved address from the unconfirmed mode.");
                List<AdapterInfo> refreshed = new List<AdapterInfo> { mixed };
                Task<List<AdapterInfo>> task = Task.Factory.StartNew<List<AdapterInfo>>(delegate { return refreshed; });
                task.Wait();
                typeof(MainForm).GetMethod("OnAdaptersLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { task, false, true });
                Assert(Field<Label>(card, "_lblSaved").Text.IndexOf("Adresse fixe enregistrée", StringComparison.Ordinal) >= 0,
                    "Refreshing must not hide the saved-address and active-DHCP warning.");
            }

            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddDraftCard(form);
                AdapterInfo saved = Adapter(false, false);
                saved.Ip = Ip;
                saved.Mask = Mask;
                saved.Gateway = Gateway;
                string message = "Adresse fixe enregistrée : 192.168.0.45. Carte non connectée.";
                FinishApply(form, card, OpResult.Ok(message), saved);
                Assert(!card.IsDirty && Field<InputBox>(card, "_ip").Value == Ip, "Confirmed offline settings must become the current card configuration.");
                Assert(Field<Label>(form, "_lblStatus").Text == message, "Offline success must not claim network connectivity.");
            }
        }

        private static AdapterCard AddDraftCard(MainForm form)
        {
            AdapterInfo info = Adapter(false, true);
            AdapterCard card = new AdapterCard(info);
            Field<List<AdapterCard>>(form, "_order").Add(card);
            Field<Dictionary<string, AdapterCard>>(form, "_cards").Add(info.Name, card);
            Field<FlowLayoutPanel>(form, "_flow").Controls.Add(card);
            Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
            InputBox ip = Field<InputBox>(card, "_ip");
            Field<TextBox>(ip, "_text").Text = Ip;
            Assert(card.IsDirty && card.ValidateInputs().IsValid, "The synthetic static draft must be valid and pending.");
            return card;
        }

        private static void FinishApply(MainForm form, AdapterCard card, OpResult result, AdapterInfo fresh)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo setBusy = typeof(MainForm).GetMethod("SetBusy", flags);
            setBusy.Invoke(form, new object[] { true, null });
            Type itemType = typeof(MainForm).GetNestedType("ChangeResult", BindingFlags.NonPublic);
            object item = Activator.CreateInstance(itemType, true);
            itemType.GetField("Change").SetValue(item, card.BuildPending());
            itemType.GetField("Result").SetValue(item, result);
            Type outcomeType = typeof(MainForm).GetNestedType("ApplyOutcome", BindingFlags.NonPublic);
            object outcome = Activator.CreateInstance(outcomeType, true);
            ((IList)outcomeType.GetField("Items").GetValue(outcome)).Add(item);
            outcomeType.GetField("Adapters").SetValue(outcome, new List<AdapterInfo> { fresh });
            typeof(MainForm).GetMethod("FinishApply", flags).Invoke(form, new object[] { outcome });
        }

        private static void PhysicalEthernetNameIsDisplayed()
        {
            AdapterInfo info = Adapter(false, true);
            string expected = "Ethernet (câble) - Realtek Gaming USB 2.5GbE Family Controller";
            Assert(info.MetaLabel == expected, "The Ethernet label must include the physical adapter name.");
            using (AdapterCard card = new AdapterCard(info))
            {
                card.Width = Theme.S(1050);
                Label label = Field<Label>(card, "_lblMeta");
                ToolTip tip = Field<ToolTip>(card, "_tip");
                Assert(label.Text == expected, "The card must display the physical adapter name next to Ethernet.");
                Assert(label.Height >= Theme.S(30), "The hardware label must have room for two lines.");
                Assert(tip.GetToolTip(label).IndexOf(info.Description, StringComparison.Ordinal) >= 0, "The complete hardware name must remain available in a tooltip.");
            }
            info.Description = "";
            Assert(info.MetaLabel == "Ethernet (câble)", "An empty hardware name must not leave a dangling separator.");
        }

        private static void OptionalReadOnlyDisconnectedProbe()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("EASY_IP_SWITCH_TEST_WMI"), "1", StringComparison.Ordinal))
            {
                return;
            }

            List<AdapterInfo> adapters = NetworkService.GetAdapters();
            for (int i = 0; i < adapters.Count; i++)
            {
                AdapterInfo info = adapters[i];
                Guid guid;
                if (info.Connected || !Guid.TryParse(info.InterfaceGuid, out guid) || guid == Guid.Empty)
                {
                    continue;
                }

                AdapterIdentity identity = new AdapterIdentity(info.Index, info.Name, info.InterfaceGuid, info.DeviceId);
                StaticIpv4Snapshot snapshot = new WindowsStaticIpv4Backend().Read(identity);
                Assert(snapshot.Adapter != null && snapshot.Saved != null, "The real disconnected adapter must be readable without changing it.");
                Assert(snapshot.Adapter.DhcpKnown && snapshot.Saved.DhcpEnabled.HasValue, "The real DHCP mode must be readable from Windows.");
                string address;
                string mask;
                if (snapshot.Adapter.Dhcp && snapshot.Saved.TryGetFirstAddress(out address, out mask))
                {
                    Assert(snapshot.Adapter.SavedStaticIp == address, "The real mixed DHCP/static state must be displayed without treating the static IP as active.");
                    string gateway = snapshot.Saved.FirstGateway();
                    uint ipValue = 0;
                    uint maskValue = 0;
                    uint gatewayValue = 0;
                    bool valid = IpUtil.TryParse(address, out ipValue) && IpUtil.TryParse(mask, out maskValue)
                        && IpUtil.IsValidMask(maskValue) && !IpUtil.IsApipa(ipValue) && ipValue != 0u && ipValue != 0xFFFFFFFFu;
                    if (valid && gateway.Length > 0)
                    {
                        valid = IpUtil.TryParse(gateway, out gatewayValue) && gatewayValue != ipValue
                            && (gatewayValue & maskValue) == (ipValue & maskValue);
                    }
                    if (valid && snapshot.Saved.DhcpEnabled == true)
                    {
                        FakeBackend simulated = new FakeBackend();
                        simulated.Snapshot = snapshot;
                        OpResult result = StaticIpv4Configuration.Apply(identity, address, mask, gateway, simulated, delegate(int ms) { });
                        Assert(!result.Success && result.Message.IndexOf("DHCP", StringComparison.Ordinal) >= 0,
                            "The real mixed state must not be mistaken for a successful static configuration.");
                        Assert(simulated.SetCalls == 1, "The simulated real-state check must issue no DHCP fallback.");
                    }
                }
                Console.WriteLine("Read-only disconnected adapter snapshot passed.");
                return;
            }
            Console.WriteLine("Read-only disconnected adapter snapshot skipped: no suitable adapter.");
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static void Assert(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeBackend : IStaticIpv4Backend
        {
            public int SetCalls;
            public int ReadCalls;
            public string LastIp = "";
            public string LastMask = "";
            public string LastGateway = "";
            public StaticCommandResult Command = new StaticCommandResult();
            public StaticIpv4Snapshot Snapshot;

            public StaticCommandResult SetStatic(AdapterIdentity identity, string ip, string mask, string gateway)
            {
                SetCalls++;
                LastIp = ip;
                LastMask = mask;
                LastGateway = gateway;
                return Command;
            }

            public StaticIpv4Snapshot Read(AdapterIdentity identity)
            {
                ReadCalls++;
                return Snapshot;
            }
        }
    }
}
