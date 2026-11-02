using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal static class NetworkConnectionsTests
    {
        private static int _assertions;

        public static int Run()
        {
            RealShortcutIsCreatedAndReused();
            ExistingUserFileIsNeverOverwritten();
            OpeningAndCreationHaveIndependentErrors();
            ButtonUsesWindowsNetworkIcon();
            MainFormButtonInvokesBothActions();
            return _assertions;
        }

        private static string TestDirectory()
        {
            string temp = Path.GetTempPath();
            string approved = Path.Combine(temp, "opencode");
            if (Directory.Exists(approved))
            {
                temp = approved;
            }
            string path = Path.Combine(temp, "EasyIpSwitch-shortcut-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void RealShortcutIsCreatedAndReused()
        {
            string directory = TestDirectory();
            string path = Path.Combine(directory, "Connexions réseau.lnk");
            try
            {
                List<bool> notifications = new List<bool>();
                Action<string, bool> notify = delegate(string target, bool created)
                {
                    Assert(target == path, "Desktop notification must identify the actual shortcut.");
                    notifications.Add(created);
                };
                NetworkConnectionsShortcutResult first = NetworkConnectionsShortcut.EnsureWithNotifier(directory, notify);
                Assert(first.Created && first.Path == path && File.Exists(path), "A real Windows shortcut must be created in the requested folder.");
                Assert(NetworkConnectionsShortcut.Matches(path, Path.Combine(Environment.SystemDirectory, "control.exe")), "The shortcut must target control.exe with ncpa.cpl.");
                MethodInfo notifyDesktop = typeof(NetworkConnectionsShortcut).GetMethod("NotifyDesktop", BindingFlags.Static | BindingFlags.NonPublic);
                notifyDesktop.Invoke(null, new object[] { path, true });
                Assert(true, "The real Windows desktop notification must be available.");

                NetworkConnectionsShortcutResult second = NetworkConnectionsShortcut.EnsureWithNotifier(directory, notify);
                Assert(!second.Created && second.Path == path, "An existing matching shortcut must be reused.");
                Assert(Directory.GetFiles(directory).Length == 1, "Repeated clicks must not create duplicates or temporary files.");

                File.Delete(path);
                NetworkConnectionsShortcutResult third = NetworkConnectionsShortcut.EnsureWithNotifier(directory, notify);
                Assert(third.Created && third.Path == path && File.Exists(path), "A deleted shortcut must be recreated on the next click.");
                Assert(notifications.Count == 3 && notifications[0] && !notifications[1] && notifications[2], "Explorer must be notified for new, existing and recreated shortcuts.");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                Directory.Delete(directory, false);
            }
        }

        private static void ExistingUserFileIsNeverOverwritten()
        {
            string directory = TestDirectory();
            string original = Path.Combine(directory, "Connexions réseau.lnk");
            string alternative = Path.Combine(directory, "Connexions réseau - Easy IP-Switch.lnk");
            const string content = "Existing user file; do not replace.";
            try
            {
                File.WriteAllText(original, content);
                NetworkConnectionsShortcutResult first = NetworkConnectionsShortcut.Ensure(directory);
                Assert(first.Created && first.Path == alternative, "A different existing shortcut must receive a separate filename.");
                Assert(File.ReadAllText(original) == content, "The existing user file must remain unchanged.");
                Assert(NetworkConnectionsShortcut.Matches(alternative, Path.Combine(Environment.SystemDirectory, "control.exe")), "The alternate shortcut must point to Network Connections.");

                NetworkConnectionsShortcutResult second = NetworkConnectionsShortcut.Ensure(directory);
                Assert(!second.Created && second.Path == alternative, "The alternate shortcut must be reused on subsequent clicks.");
                Assert(Directory.GetFiles(directory).Length == 2, "No additional shortcut or temporary file must remain.");
            }
            finally
            {
                if (File.Exists(original))
                {
                    File.Delete(original);
                }
                if (File.Exists(alternative))
                {
                    File.Delete(alternative);
                }
                Directory.Delete(directory, false);
            }
        }

        private static void OpeningAndCreationHaveIndependentErrors()
        {
            FakeBackend both = new FakeBackend();
            NetworkConnectionsResult result = NetworkConnections.Execute(both);
            Assert(result.Success && both.Creates == 1 && both.Opens == 1, "One action must both create the shortcut and open the window.");

            FakeBackend noDesktop = new FakeBackend();
            noDesktop.ShortcutFails = true;
            result = NetworkConnections.Execute(noDesktop);
            Assert(!result.Success && result.Opened && noDesktop.Opens == 1, "Opening must still be attempted when shortcut creation fails.");
            Assert(result.Message.IndexOf("Bureau inaccessible", StringComparison.Ordinal) >= 0, "Shortcut errors must be explained.");

            FakeBackend noWindow = new FakeBackend();
            noWindow.OpenFails = true;
            result = NetworkConnections.Execute(noWindow);
            Assert(!result.Success && result.Shortcut != null && !result.Opened, "A successfully created shortcut must be reported even if opening fails.");
            Assert(result.Message.IndexOf("Ouverture refusée", StringComparison.Ordinal) >= 0, "Opening errors must be explained.");
        }

        private static void ButtonUsesWindowsNetworkIcon()
        {
            using (MainForm form = new MainForm(new FakeBackend()))
            {
                FlatButton button = Field<FlatButton>(form, "_btnConnections");
                Assert(button.Text == "Connexions réseau", "The button must use the requested name.");
                if (File.Exists(Path.Combine(Environment.SystemDirectory, "netshell.dll")))
                {
                    Assert(button.SystemIcon != null, "The button must use the Windows network-connections icon when available.");
                }
            }
        }

        private static void MainFormButtonInvokesBothActions()
        {
            VerifyMainFormButton(false, false, "raccourci « synthetic » créé", true);
            VerifyMainFormButton(true, false, "Bureau inaccessible", false);
            VerifyMainFormButton(false, true, "Ouverture refusée", false);
        }

        private static void VerifyMainFormButton(bool shortcutFails, bool openFails, string expectedText, bool success)
        {
            FakeBackend backend = new FakeBackend();
            backend.ShortcutFails = shortcutFails;
            backend.OpenFails = openFails;
            Exception failure = null;
            bool complete = false;
            using (MainForm form = new MainForm(backend))
            using (ApplicationContext context = new ApplicationContext())
            using (System.Windows.Forms.Timer timeout = new System.Windows.Forms.Timer())
            {
                IntPtr handle = form.Handle;
                FlatButton button = Field<FlatButton>(form, "_btnConnections");
                Label status = Field<Label>(form, "_lblStatus");
                status.TextChanged += delegate
                {
                    if (status.Text.IndexOf(expectedText, StringComparison.Ordinal) < 0)
                    {
                        return;
                    }
                    try
                    {
                        Assert(backend.Creates == 1 && backend.Opens == 1, "The real button must invoke both actions once.");
                        Assert(backend.Apartment == ApartmentState.STA, "Windows shortcut creation must run on an STA worker.");
                        Assert(button.Enabled && !Field<bool>(form, "_shortcutBusy"), "The button must be available again after completion.");
                        Assert(status.ForeColor == (success ? Theme.Success : Theme.Danger), "The action result must use the correct success or error color.");
                        complete = true;
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    context.ExitThread();
                };

                timeout.Interval = 10000;
                timeout.Tick += delegate
                {
                    timeout.Stop();
                    failure = new InvalidOperationException("The shortcut button test timed out.");
                    context.ExitThread();
                };
                timeout.Start();
                form.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        MethodInfo click = button.GetType().GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                        click.Invoke(button, new object[] { EventArgs.Empty });
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        context.ExitThread();
                    }
                }));
                Application.Run(context);
                timeout.Stop();
            }
            if (failure != null)
            {
                throw new InvalidOperationException("The shortcut button test failed.", failure);
            }
            Assert(complete, "The real button must report the completed action.");
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

        private sealed class FakeBackend : INetworkConnectionsBackend
        {
            public int Creates;
            public int Opens;
            public bool ShortcutFails;
            public bool OpenFails;
            public ApartmentState Apartment;

            public NetworkConnectionsShortcutResult EnsureShortcut()
            {
                Creates++;
                Apartment = Thread.CurrentThread.GetApartmentState();
                if (ShortcutFails)
                {
                    throw new InvalidOperationException("Bureau inaccessible");
                }
                NetworkConnectionsShortcutResult result = new NetworkConnectionsShortcutResult();
                result.Path = "synthetic.lnk";
                result.Created = true;
                return result;
            }

            public void OpenConnections()
            {
                Opens++;
                if (OpenFails)
                {
                    throw new InvalidOperationException("Ouverture refusée");
                }
            }
        }
    }
}
