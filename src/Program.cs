using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            L.Initialize();
            Theme.Init();

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ShowError(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ShowError(e.ExceptionObject as Exception);
            };

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private static void ShowError(Exception ex)
        {
            string message = ex == null ? L.T("Erreur inconnue.") : ex.Message;
            try
            {
                string details = ex == null
                    ? L.T("Exception inconnue")
                    : ex.GetType().FullName + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EasyIpSwitch-erreurs.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + details + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
            }

            MessageBox.Show(L.F("Une erreur inattendue est survenue :\r\n\r\n{0}", message),
                AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
