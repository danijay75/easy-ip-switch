using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    // These tests use synthetic adapters only: no network command is executed.
    internal static class AdapterCardToggleTests
    {
        private static int _assertions;

        [STAThread]
        private static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Init();

                DisabledCardCanBeEnabled();
                UnknownStateIsNotPresentedAsConfirmed();
                ToggleRoundTripAndBusyState();
                RefreshKeepsDraftAndUpdatesToggle();
                FailedOperationRestoresToggle();
                VerifiedOperationWithoutRefreshUpdatesCard();
                FailedOperationWithDraftAndRefreshRestoresToggle();
                UnconfirmedActivationResolvesAfterRefresh();
                ManualRefreshReportsSuccessAndClearsOldError();
                ManualRefreshIsQueuedDuringAutomaticRead();
                int controlAssertions = AdapterControlTests.Run();
                int integrationAssertions = RefreshIntegrationTests.Run();
                int staticAssertions = StaticDefaultsTests.Run();
                int staticOperationAssertions = StaticIpv4Tests.Run();
                int shortcutAssertions = NetworkConnectionsTests.Run();
                int dialogAssertions = ConfirmDialogTests.Run();
                int quitAssertions = QuitDialogTests.Run();
                int localizationAssertions = LocalizationTests.Run();
                int versionAssertions = HeaderVersionTests.Run();
                int termsAssertions = TermsDialogTests.Run();

                Console.WriteLine("Easy IP-Switch tests passed ({0} assertions).", _assertions + controlAssertions + integrationAssertions + staticAssertions + staticOperationAssertions + shortcutAssertions + dialogAssertions + quitAssertions + localizationAssertions + versionAssertions + termsAssertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static AdapterInfo Info(bool enabled)
        {
            AdapterInfo info = new AdapterInfo();
            info.Name = "Synthetic adapter";
            info.Description = "Synthetic Ethernet adapter";
            info.Index = 9999;
            info.Enabled = enabled;
            info.Connected = enabled;
            info.Dhcp = true;
            info.Ip = "192.0.2.10";
            info.Mask = "255.255.255.0";
            info.Gateway = "192.0.2.1";
            return info;
        }

        private static void DisabledCardCanBeEnabled()
        {
            using (AdapterCard card = new AdapterCard(Info(false)))
            {
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");
                int requests = 0;
                card.ToggleRequested += delegate { requests++; };

                Assert(toggle.Enabled, "A disabled adapter must keep its toggle enabled.");
                Assert(!toggle.Checked, "A disabled adapter must show the off state.");
                Assert(!Field<InputBox>(card, "_ip").Enabled, "IP editing must remain disabled.");
                Assert(!Field<SegmentedControl>(card, "_segment").Enabled, "Mode editing must remain disabled.");

                Click(toggle);
                Assert(requests == 1, "Clicking the off toggle must request activation.");
                Assert(card.PendingEnabled, "The requested state must be enabled.");
            }
        }

        private static void ToggleRoundTripAndBusyState()
        {
            using (AdapterCard card = new AdapterCard(Info(true)))
            {
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");
                int requests = 0;
                card.ToggleRequested += delegate { requests++; };

                Assert(toggle.Enabled && toggle.Checked, "An active adapter starts on and clickable.");
                Click(toggle);
                Assert(requests == 1 && !card.PendingEnabled, "Clicking requests deactivation.");

                card.SetBusy(true);
                Assert(!toggle.Enabled, "The toggle must be disabled during an operation.");
                Click(toggle);
                Assert(requests == 1 && !card.PendingEnabled, "A busy toggle must not issue a request.");

                card.UpdateInfo(Info(false), false);
                Assert(!toggle.Enabled, "A refresh must not unlock a busy toggle.");
                card.SetBusy(false);
                Assert(toggle.Enabled && !toggle.Checked, "The disabled adapter must be clickable again.");

                Click(toggle);
                Assert(requests == 2 && card.PendingEnabled, "Clicking again requests activation.");
                card.SetBusy(true);
                card.UpdateInfo(Info(true), false);
                card.SetBusy(false);
                Assert(toggle.Enabled && toggle.Checked, "The active state must be restored after activation.");
            }
        }

        private static void UnknownStateIsNotPresentedAsConfirmed()
        {
            AdapterInfo info = Info(true);
            info.EnabledKnown = false;
            using (AdapterCard card = new AdapterCard(info))
            {
                Assert(info.StatusLabel == "État indisponible", "A transitional state must not appear as enabled.");
                Assert(!Field<ToggleSwitch>(card, "_toggle").Enabled, "The toggle must wait for a confirmed state.");
                Assert(!Field<InputBox>(card, "_ip").Enabled, "IP controls must wait for a confirmed state.");
            }
        }

        private static void RefreshKeepsDraftAndUpdatesToggle()
        {
            using (AdapterCard card = new AdapterCard(Info(true)))
            {
                SetDraft(card);
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");

                card.UpdateInfo(Info(false), false);
                Assert(!toggle.Checked && toggle.Enabled, "Refresh must show an externally disabled card.");
                AssertDraft(card);

                card.UpdateInfo(Info(true), false);
                Assert(toggle.Checked && toggle.Enabled, "Refresh must show an externally enabled card.");
                AssertDraft(card);
            }
        }

        private static void FailedOperationRestoresToggle()
        {
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddCard(form, Info(true));
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");
                Click(toggle);
                SetBusy(form, true);

                FinishToggle(form, card, OpResult.Fail("Synthetic failure"), false, null);
                Assert(card.AdapterEnabled, "A failed operation must keep the previous adapter state.");
                Assert(toggle.Enabled && toggle.Checked, "A failed operation must restore and unlock the toggle.");
            }
        }

        private static void VerifiedOperationWithoutRefreshUpdatesCard()
        {
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddCard(form, Info(true));
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");

                Click(toggle);
                SetBusy(form, true);
                FinishToggle(form, card, OpResult.Ok("Synthetic success"), false, null);
                Assert(!card.AdapterEnabled, "A verified deactivation must update the card without a refresh.");
                Assert(toggle.Enabled && !toggle.Checked, "A verified deactivation must remain reversible.");
                Assert(!Field<InputBox>(card, "_ip").Enabled, "The disabled card must not allow IP editing.");

                Click(toggle);
                SetBusy(form, true);
                FinishToggle(form, card, OpResult.Ok("Synthetic success"), true, null);
                Assert(card.AdapterEnabled, "A verified activation must update the card without a refresh.");
                Assert(toggle.Enabled && toggle.Checked, "The activated card must remain clickable.");
                Assert(Field<InputBox>(card, "_ip").Enabled, "The enabled card must allow IP controls again.");
            }
        }

        private static void FailedOperationWithDraftAndRefreshRestoresToggle()
        {
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddCard(form, Info(true));
                SetDraft(card);
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");
                Click(toggle);
                SetBusy(form, true);

                List<AdapterInfo> refreshed = new List<AdapterInfo>();
                refreshed.Add(Info(true));
                FinishToggle(form, card, OpResult.Fail("Synthetic failure"), false, refreshed);

                Assert(toggle.Enabled && toggle.Checked, "A failed operation must show the refreshed real state.");
                AssertDraft(card);
            }
        }

        private static void UnconfirmedActivationResolvesAfterRefresh()
        {
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddCard(form, Info(false));
                ToggleSwitch toggle = Field<ToggleSwitch>(card, "_toggle");
                Click(toggle);
                SetBusy(form, true);

                List<AdapterInfo> stale = new List<AdapterInfo>();
                stale.Add(Info(false));
                FinishToggle(form, card, OpResult.Awaiting("État en cours de vérification."), true, stale);
                Assert(!toggle.Checked && toggle.Enabled, "An unconfirmed operation must show the last observed state.");
                Assert(((System.Collections.IDictionary)Field<object>(form, "_pendingToggles")).Count == 1, "The activation must remain pending.");

                List<AdapterInfo> refreshed = new List<AdapterInfo>();
                refreshed.Add(Info(true));
                Method(form, "ApplyAdapters").Invoke(form, new object[] { refreshed, false });
                Assert(toggle.Checked && toggle.Enabled, "A later refresh must show the confirmed activation.");
                Assert(((System.Collections.IDictionary)Field<object>(form, "_pendingToggles")).Count == 0, "A confirmed activation must leave the pending state.");
                Assert(Field<Label>(card, "_lblMessage").Text == "Carte activée.", "The stale message must be replaced by success.");
            }
        }

        private static void ManualRefreshReportsSuccessAndClearsOldError()
        {
            using (MainForm form = new MainForm())
            {
                AdapterCard card = AddCard(form, Info(true));
                card.SetMessage("Ancienne erreur.", MessageKind.Error);
                List<AdapterInfo> refreshed = new List<AdapterInfo>();
                refreshed.Add(Info(true));
                Task<List<AdapterInfo>> task = Task.Factory.StartNew<List<AdapterInfo>>(delegate { return refreshed; });
                task.Wait();
                Method(form, "OnAdaptersLoaded").Invoke(form, new object[] { task, false, true });

                Assert(Field<Label>(card, "_lblMessage").Text != "Ancienne erreur.", "A manual refresh must clear outdated errors.");
                Assert(Field<Label>(form, "_lblStatus").Text.IndexOf("Actualisation terminée", StringComparison.Ordinal) >= 0, "A manual refresh must provide visible feedback.");
            }
        }

        private static void ManualRefreshIsQueuedDuringAutomaticRead()
        {
            using (MainForm form = new MainForm())
            {
                form.GetType().GetField("_loading", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
                Method(form, "RefreshAdapters").Invoke(form, new object[] { true, true });
                Assert(Field<bool>(form, "_manualRefreshQueued"), "A manual refresh must not be discarded while another read is running.");
                Assert(Field<Label>(form, "_lblStatus").Text.IndexOf("actualisation", StringComparison.OrdinalIgnoreCase) >= 0, "A queued refresh must provide feedback.");
            }
        }

        private static void SetDraft(AdapterCard card)
        {
            Field<SegmentedControl>(card, "_segment").SelectedIndex = 1;
            Field<InputBox>(card, "_ip").Value = "192.0.2.99";
            Field<InputBox>(card, "_mask").Value = "255.255.255.0";
            Field<InputBox>(card, "_gateway").Value = "192.0.2.1";
            Assert(card.IsDirty, "The synthetic static configuration must be pending.");
        }

        private static void AssertDraft(AdapterCard card)
        {
            Assert(card.IsDirty, "The pending configuration must be retained.");
            Assert(Field<SegmentedControl>(card, "_segment").SelectedIndex == 1, "The selected mode must be retained.");
            Assert(Field<InputBox>(card, "_ip").Value == "192.0.2.99", "The pending address must be retained.");
            Assert(Field<InputBox>(card, "_mask").Value == "255.255.255.0", "The pending mask must be retained.");
            Assert(Field<InputBox>(card, "_gateway").Value == "192.0.2.1", "The pending gateway must be retained.");
        }

        private static AdapterCard AddCard(MainForm form, AdapterInfo info)
        {
            AdapterCard card = new AdapterCard(info);
            Field<List<AdapterCard>>(form, "_order").Add(card);
            Field<Dictionary<string, AdapterCard>>(form, "_cards").Add(info.Name, card);
            Field<FlowLayoutPanel>(form, "_flow").Controls.Add(card);
            return card;
        }

        private static void SetBusy(MainForm form, bool busy)
        {
            Method(form, "SetBusy").Invoke(form, new object[] { busy, null });
        }

        private static void FinishToggle(MainForm form, AdapterCard card, OpResult result,
            bool enabled, List<AdapterInfo> adapters)
        {
            Type type = typeof(MainForm).GetNestedType("ToggleOutcome", BindingFlags.NonPublic);
            object outcome = Activator.CreateInstance(type, true);
            type.GetField("Result").SetValue(outcome, result);
            type.GetField("Enabled").SetValue(outcome, enabled);
            type.GetField("Adapters").SetValue(outcome, adapters);
            Method(form, "FinishToggle").Invoke(form, new object[] { card, outcome });
        }

        private static void Click(ToggleSwitch toggle)
        {
            Method(toggle, "OnMouseDown").Invoke(toggle,
                new object[] { new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0) });
        }

        private static T Field<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Missing field: " + name);
            }
            return (T)field.GetValue(instance);
        }

        private static MethodInfo Method(object instance, string name)
        {
            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("Missing method: " + name);
            }
            return method;
        }

        private static void Assert(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
