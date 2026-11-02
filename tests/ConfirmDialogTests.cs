using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal static class ConfirmDialogTests
    {
        private static int _assertions;

        public static int Run()
        {
            ThreeButtonsAreLaidOutBeforeShowing();
            OptionalButtonDoesNotTakeSpace();
            EachChoiceIsWiredCorrectly();
            return _assertions;
        }

        private static ConfirmDialog Create(string primary, string secondary, string stay)
        {
            ConstructorInfo constructor = typeof(ConfirmDialog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, null);
            return (ConfirmDialog)constructor.Invoke(new object[] { "Quitter ?", "Modifications en attente.", primary, secondary, stay });
        }

        private static void ThreeButtonsAreLaidOutBeforeShowing()
        {
            using (ConfirmDialog dialog = Create("Appliquer et quitter", "Quitter sans appliquer", "Rester"))
            {
                FlatButton primary = Field<FlatButton>(dialog, "_btnPrimary");
                FlatButton secondary = Field<FlatButton>(dialog, "_btnSecondary");
                FlatButton stay = Field<FlatButton>(dialog, "_btnStay");
                Assert(!dialog.Visible && !primary.Visible, "The test must cover the hidden parent state.");
                Assert(primary.Parent == dialog && secondary.Parent == dialog && stay.Parent == dialog, "All three choices must be present.");
                AssertInsideBottomRow(dialog, primary);
                AssertInsideBottomRow(dialog, secondary);
                AssertInsideBottomRow(dialog, stay);
                Assert(primary.Right < secondary.Left && secondary.Right < stay.Left, "The three buttons must not overlap.");
                Assert(object.ReferenceEquals(dialog.AcceptButton, stay) && object.ReferenceEquals(dialog.CancelButton, stay), "Enter and Escape must keep the user in the application.");
            }
        }

        private static void OptionalButtonDoesNotTakeSpace()
        {
            using (ConfirmDialog dialog = Create("Relancer en administrateur", null, "Rester"))
            {
                FlatButton primary = Field<FlatButton>(dialog, "_btnPrimary");
                FlatButton secondary = Field<FlatButton>(dialog, "_btnSecondary");
                FlatButton stay = Field<FlatButton>(dialog, "_btnStay");
                Assert(secondary.Parent == null, "An omitted optional button must not be added.");
                AssertInsideBottomRow(dialog, primary);
                AssertInsideBottomRow(dialog, stay);
                Assert(stay.Left - primary.Right == Theme.S(10), "An omitted button must not leave a blank gap.");
            }
        }

        private static void EachChoiceIsWiredCorrectly()
        {
            AssertChoice("_btnPrimary", ConfirmDialog.ChoicePrimary);
            AssertChoice("_btnSecondary", ConfirmDialog.ChoiceSecondary);
            AssertChoice("_btnStay", ConfirmDialog.ChoiceStay);
        }

        private static void AssertChoice(string fieldName, int expected)
        {
            using (ConfirmDialog dialog = Create("Appliquer et quitter", "Quitter sans appliquer", "Rester"))
            {
                typeof(ConfirmDialog).GetField("_choice", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dialog, -1);
                FlatButton button = Field<FlatButton>(dialog, fieldName);
                MethodInfo click = button.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                click.Invoke(button, new object[] { EventArgs.Empty });
                int choice = Field<int>(dialog, "_choice");
                Assert(choice == expected, fieldName + " must return its own choice.");
            }
        }

        private static void AssertInsideBottomRow(ConfirmDialog dialog, Control button)
        {
            Rectangle area = new Rectangle(Point.Empty, dialog.ClientSize);
            Assert(area.Contains(button.Bounds), "A confirmation button must fit inside the dialog.");
            Assert(button.Top >= dialog.ClientSize.Height - Theme.S(100), "A confirmation button must be placed in the bottom row.");
            Assert(button.Width > Theme.S(50) && button.Height == Theme.S(38), "A confirmation button must have its intended size.");
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
