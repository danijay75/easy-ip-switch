using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    // Optional integration test: uses the real read-only refresh path, never
    // invokes Enable/Disable or changes the network configuration.
    internal static class RefreshIntegrationTests
    {
        public static int Run()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("EASY_IP_SWITCH_TEST_REFRESH"), "1", StringComparison.Ordinal))
            {
                return 0;
            }

            int stage = 0;
            int assertions = 0;
            Exception failure = null;

            using (MainForm form = new MainForm())
            using (ApplicationContext context = new ApplicationContext())
            using (Timer timeout = new Timer())
            {
                // Creating a handle without showing the form allows BeginInvoke
                // callbacks to run without opening a window or stealing focus.
                IntPtr handle = form.Handle;
                FlatButton refresh = Field<FlatButton>(form, "_btnRefresh");
                timeout.Interval = 45000;
                timeout.Tick += delegate
                {
                    timeout.Stop();
                    failure = new InvalidOperationException("The read-only refresh integration test timed out.");
                    context.ExitThread();
                };

                refresh.EnabledChanged += delegate
                {
                    if (!refresh.Enabled)
                    {
                        return;
                    }
                    if (stage == 0)
                    {
                        stage = 1;
                        form.BeginInvoke(new Action(delegate
                        {
                            try
                            {
                                List<AdapterCard> cards = Field<List<AdapterCard>>(form, "_order");
                                if (cards.Count > 0)
                                {
                                    cards[0].SetMessage("Synthetic old error.", MessageKind.Error);
                                }
                                Method(refresh, "OnClick").Invoke(refresh, new object[] { EventArgs.Empty });
                            }
                            catch (Exception ex)
                            {
                                failure = ex;
                                context.ExitThread();
                            }
                        }));
                    }
                    else if (stage == 1)
                    {
                        stage = 2;
                        form.BeginInvoke(new Action(delegate
                        {
                            try
                            {
                                string status = Field<Label>(form, "_lblStatus").Text;
                                if (status.IndexOf("Actualisation terminée", StringComparison.Ordinal) < 0)
                                {
                                    throw new InvalidOperationException("The real refresh did not report completion: " + status);
                                }
                                assertions++;

                                List<AdapterCard> cards = Field<List<AdapterCard>>(form, "_order");
                                if (cards.Count > 0)
                                {
                                    string message = Field<Label>(cards[0], "_lblMessage").Text;
                                    if (message == "Synthetic old error.")
                                    {
                                        throw new InvalidOperationException("The real refresh retained the old card error.");
                                    }
                                    assertions++;
                                }
                            }
                            catch (Exception ex)
                            {
                                failure = ex;
                            }
                            context.ExitThread();
                        }));
                    }
                };

                timeout.Start();
                Method(form, "RefreshAdapters").Invoke(form, new object[] { true, false });
                Application.Run(context);
                timeout.Stop();
                if (failure != null)
                {
                    throw new InvalidOperationException("Read-only refresh integration test failed.", failure);
                }
                if (stage != 2)
                {
                    throw new InvalidOperationException("The real refresh path did not complete.");
                }
            }

            Console.WriteLine("Read-only refresh button integration test passed.");
            return assertions;
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
    }
}
