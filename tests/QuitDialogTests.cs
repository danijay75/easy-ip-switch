using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal static class QuitDialogTests
    {
        private static int _assertions;

        public static int Run()
        {
            DonationContentIsPresent();
            QuickQuitKeepsOnlyTwoChoices();
            PendingChangesKeepThreeChoices();
            return _assertions;
        }

        private static QuitDialog Create(string primary, string secondary, string stay)
        {
            ConstructorInfo constructor = typeof(QuitDialog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
            return (QuitDialog)constructor.Invoke(new object[] { "Quitter ?", "Message.", primary, secondary, stay });
        }

        private static void DonationContentIsPresent()
        {
            using (QuitDialog dialog = Create("Quitter", null, "Rester"))
            {
                Assert(AppInfo.DonationUrl == "https://www.paypal.com/donate/?hosted_button_id=WZBCQXPEHHDDC",
                    "The donation link must target the hosted PayPal campaign.");

                PictureBox qr = Field<PictureBox>(dialog, "_qr");
                Assert(qr.Parent == dialog && qr.Image != null,
                    "The embedded donation QR code must be displayed.");
                Assert(qr.Cursor == Cursors.Hand, "The QR code must look clickable.");

                LinkLabel link = Field<LinkLabel>(dialog, "_lnkDonate");
                Assert(link.Parent == dialog && link.Text.IndexOf("don", StringComparison.OrdinalIgnoreCase) >= 0,
                    "A visible donation link must accompany the QR code.");
                Assert(link.LinkColor == Theme.Accent, "The donation link must use the accent color.");
            }
        }

        private static void QuickQuitKeepsOnlyTwoChoices()
        {
            using (QuitDialog dialog = Create("Quitter", null, "Rester"))
            {
                FlatButton primary = Field<FlatButton>(dialog, "_btnPrimary");
                FlatButton secondary = Field<FlatButton>(dialog, "_btnSecondary");
                FlatButton stay = Field<FlatButton>(dialog, "_btnStay");

                Assert(primary.Parent == dialog && stay.Parent == dialog, "A quick quit must offer Quitter and Rester.");
                Assert(secondary.Parent == null, "A quick quit must not add an unnecessary third button.");
                AssertInsideBottomRow(dialog, primary);
                AssertInsideBottomRow(dialog, stay);
                Assert(stay.Left - primary.Right == Theme.S(10), "The two quick-quit buttons must not leave a gap.");
                Assert(object.ReferenceEquals(dialog.AcceptButton, stay) && object.ReferenceEquals(dialog.CancelButton, stay),
                    "Enter and Escape must keep the user in the application.");
            }
        }

        private static void PendingChangesKeepThreeChoices()
        {
            using (QuitDialog dialog = Create("Appliquer et quitter", "Quitter sans appliquer", "Rester"))
            {
                FlatButton primary = Field<FlatButton>(dialog, "_btnPrimary");
                FlatButton secondary = Field<FlatButton>(dialog, "_btnSecondary");
                FlatButton stay = Field<FlatButton>(dialog, "_btnStay");

                Assert(primary.Parent == dialog && secondary.Parent == dialog && stay.Parent == dialog,
                    "Pending changes must keep all three choices.");
                Assert(primary.Right < secondary.Left && secondary.Right < stay.Left,
                    "The three buttons must not overlap.");
                AssertInsideBottomRow(dialog, primary);
                AssertInsideBottomRow(dialog, secondary);
                AssertInsideBottomRow(dialog, stay);
            }
        }

        private static void AssertInsideBottomRow(QuitDialog dialog, Control button)
        {
            Rectangle area = new Rectangle(Point.Empty, dialog.ClientSize);
            Assert(area.Contains(button.Bounds), "A quit button must fit inside the dialog.");
            Assert(button.Top >= dialog.ClientSize.Height - Theme.S(100),
                "A quit button must be placed in the bottom row.");
            Assert(button.Width > Theme.S(50) && button.Height == Theme.S(38),
                "A quit button must have its intended size.");
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
