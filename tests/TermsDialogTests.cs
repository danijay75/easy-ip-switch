using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EasyIpSwitch
{
    internal static class TermsDialogTests
    {
        private static int _assertions;

        private sealed class MemoryConsentStore : ITermsConsentStore
        {
            public bool Accepted;
            public int SaveCount;

            public bool IsAccepted()
            {
                return Accepted;
            }

            public void SaveAccepted(bool accepted)
            {
                Accepted = accepted;
                SaveCount++;
            }
        }

        public static int Run()
        {
            try
            {
                TermsAndGdprContentIsPresentAndScrollIsShort();
                OneClickButtonsReturnExpectedChoice();
                AcceptedChoiceIsRememberedAndDeclinedChoiceIsReAsked();
                RegistryStorePersistsAndClearsConsent();
                TermsDialogFollowsLanguage();
            }
            finally
            {
                L.SetLanguage(AppLanguage.French, false);
            }
            return _assertions;
        }

        private static void TermsAndGdprContentIsPresentAndScrollIsShort()
        {
            L.SetLanguage(AppLanguage.French, false);
            using (TermsDialog dialog = new TermsDialog())
            {
                Label title = Field<Label>(dialog, "_lblTitle");
                Label agreement = Field<Label>(dialog, "_lblAgreement");
                Panel scrollPanel = Field<Panel>(dialog, "_scrollPanel");
                FlatButton accept = Field<FlatButton>(dialog, "_btnAccept");
                FlatButton decline = Field<FlatButton>(dialog, "_btnDecline");

                Assert(title.Text.IndexOf("RGPD", StringComparison.Ordinal) >= 0,
                    "The dialog title must mention GDPR (RGPD).");
                Assert(agreement.Text.IndexOf("RGPD", StringComparison.Ordinal) >= 0
                    && agreement.Text.IndexOf("2016/679", StringComparison.Ordinal) >= 0
                    && agreement.Text.IndexOf("MIT", StringComparison.Ordinal) >= 0,
                    "The agreement text must cover the Terms of Use and GDPR compliance.");

                Assert(accept.Parent == dialog && accept.Enabled && accept.Text == "J'accepte",
                    "The accept button must be immediately clickable in a single click.");
                Assert(decline.Parent == dialog && decline.Enabled && decline.Text == "Je refuse",
                    "The decline button must be immediately clickable in a single click.");
                Assert(decline.Right < accept.Left && accept.Left - decline.Right == Theme.S(10),
                    "The decline and accept buttons must be properly aligned without overlapping.");
                Assert(object.ReferenceEquals(dialog.AcceptButton, accept)
                    && object.ReferenceEquals(dialog.CancelButton, decline),
                    "Enter must accept and Escape must decline in a single action.");

                int totalContentHeight = scrollPanel.AutoScrollMinSize.Height;
                int viewportHeight = scrollPanel.Height;
                int scrollTravel = Math.Max(0, totalContentHeight - viewportHeight);
                Assert(totalContentHeight > 0 && viewportHeight >= Theme.S(180),
                    "The agreement viewport must be comfortably readable.");
                Assert(scrollTravel <= Theme.S(160),
                    "The agreement scroll distance must remain short.");
            }
        }

        private static void OneClickButtonsReturnExpectedChoice()
        {
            using (TermsDialog dialog = new TermsDialog())
            {
                Assert(Field<int>(dialog, "_choice") == TermsDialog.ChoiceDecline,
                    "Closing the dialog without accepting must default to decline.");

                FlatButton accept = Field<FlatButton>(dialog, "_btnAccept");
                MethodInfo onClick = accept.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                onClick.Invoke(accept, new object[] { EventArgs.Empty });
                Assert(Field<int>(dialog, "_choice") == TermsDialog.ChoiceAccept,
                    "Clicking J'accepte must record ChoiceAccept in one click.");
            }

            using (TermsDialog dialog = new TermsDialog())
            {
                typeof(TermsDialog).GetField("_choice", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dialog, -1);
                FlatButton decline = Field<FlatButton>(dialog, "_btnDecline");
                MethodInfo onClick = decline.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                onClick.Invoke(decline, new object[] { EventArgs.Empty });
                Assert(Field<int>(dialog, "_choice") == TermsDialog.ChoiceDecline,
                    "Clicking Je refuse must record ChoiceDecline in one click.");
            }
        }

        private static void AcceptedChoiceIsRememberedAndDeclinedChoiceIsReAsked()
        {
            MemoryConsentStore store = new MemoryConsentStore();
            int promptCalls = 0;

            bool firstLaunch = TermsConsent.EnsureAccepted(null, store, delegate
            {
                promptCalls++;
                return TermsDialog.ChoiceDecline;
            });
            Assert(!firstLaunch && !store.IsAccepted() && promptCalls == 1,
                "Declining the terms must block startup and must not mark terms as accepted.");

            bool secondLaunch = TermsConsent.EnsureAccepted(null, store, delegate
            {
                promptCalls++;
                return TermsDialog.ChoiceAccept;
            });
            Assert(secondLaunch && store.IsAccepted() && promptCalls == 2,
                "After a previous refusal, the next launch must ask again and remember acceptance.");

            bool thirdLaunch = TermsConsent.EnsureAccepted(null, store, delegate
            {
                promptCalls++;
                return TermsDialog.ChoiceDecline;
            });
            Assert(thirdLaunch && store.IsAccepted() && promptCalls == 2,
                "Once accepted, subsequent launches must remember the choice without prompting again.");
        }

        private static void RegistryStorePersistsAndClearsConsent()
        {
            string testSubKey = RegistryTermsConsentStore.DefaultRegistryPath + @"\Tests-" + Guid.NewGuid().ToString("N");
            string testFile = Path.Combine(Path.GetTempPath(), "EasyIpSwitch-terms-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                RegistryTermsConsentStore store = new RegistryTermsConsentStore(testSubKey, testFile);
                Assert(!store.IsAccepted(), "A fresh store must report not accepted.");

                store.SaveAccepted(true);
                Assert(store.IsAccepted(), "Saving acceptance must persist across store reads.");

                RegistryTermsConsentStore reloaded = new RegistryTermsConsentStore(testSubKey, testFile);
                Assert(reloaded.IsAccepted(), "A newly created store instance must remember acceptance.");

                reloaded.SaveAccepted(false);
                Assert(!reloaded.IsAccepted(), "Saving a refusal must clear acceptance so the user is asked again.");
            }
            finally
            {
                try
                {
                    if (File.Exists(testFile))
                    {
                        File.Delete(testFile);
                    }
                }
                catch
                {
                }
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testSubKey, false);
                }
                catch
                {
                }
            }
        }

        private static void TermsDialogFollowsLanguage()
        {
            using (TermsDialog dialog = new TermsDialog())
            {
                Panel scrollPanel = Field<Panel>(dialog, "_scrollPanel");
                FlatButton accept = Field<FlatButton>(dialog, "_btnAccept");
                FlatButton decline = Field<FlatButton>(dialog, "_btnDecline");
                Label privacyNote = Field<Label>(dialog, "_lblPrivacyNote");

                L.SetLanguage(AppLanguage.English, false);
                dialog.ApplyLanguage();
                Assert(Field<Label>(dialog, "_lblTitle").Text == "Terms of Use and GDPR",
                    "The title must switch to English.");
                Assert(accept.Text == "I accept" && decline.Text == "I decline",
                    "The buttons must switch to English.");
                Assert(Field<Label>(dialog, "_lblAgreement").Text.IndexOf("GDPR compliance", StringComparison.Ordinal) >= 0,
                    "The agreement body must switch to English.");
                Assert(Math.Max(0, scrollPanel.AutoScrollMinSize.Height - scrollPanel.Height) <= Theme.S(160)
                    && privacyNote.Right <= decline.Left,
                    "English layout must keep a short scroll and non-overlapping footer.");

                L.SetLanguage(AppLanguage.German, false);
                dialog.ApplyLanguage();
                Assert(Field<Label>(dialog, "_lblTitle").Text == "Nutzungsbedingungen und DSGVO",
                    "The title must switch to German.");
                Assert(accept.Text == "Ich akzeptiere" && decline.Text == "Ich lehne ab",
                    "The buttons must switch to German.");
                Assert(Math.Max(0, scrollPanel.AutoScrollMinSize.Height - scrollPanel.Height) <= Theme.S(160)
                    && privacyNote.Right <= decline.Left,
                    "German layout must keep a short scroll and non-overlapping footer.");

                L.SetLanguage(AppLanguage.Spanish, false);
                dialog.ApplyLanguage();
                Assert(Field<Label>(dialog, "_lblTitle").Text == "Condiciones de uso y RGPD",
                    "The title must switch to Spanish.");
                Assert(accept.Text == "Acepto" && decline.Text == "Rechazo",
                    "The buttons must switch to Spanish.");
                Assert(Math.Max(0, scrollPanel.AutoScrollMinSize.Height - scrollPanel.Height) <= Theme.S(160)
                    && privacyNote.Right <= decline.Left,
                    "Spanish layout must keep a short scroll and non-overlapping footer.");
            }
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
    }
}
