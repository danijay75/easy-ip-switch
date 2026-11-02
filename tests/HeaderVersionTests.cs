using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace EasyIpSwitch
{
    // Versioning: the product name must be followed by the version in the UI.
    internal static class HeaderVersionTests
    {
        private static int _assertions;

        public static int Run()
        {
            VersionLabelMatchesRelease();
            WindowTitleCarriesVersion();
            HeaderPaintsVersionAfterName();
            return _assertions;
        }

        private static void VersionLabelMatchesRelease()
        {
            Assert(AppInfo.Version == "1.1.2", "The release must be versioned 1.1.2.");
            Assert(AppInfo.VersionLabel == "v1.1.2", "The displayed version must read v1.1.2.");
            Version assembly = typeof(AppInfo).Assembly.GetName().Version;
            Assert(assembly.Major == 1 && assembly.Minor == 1 && assembly.Build == 2,
                "The assembly must carry the 1.1.2 version.");
        }

        private static void WindowTitleCarriesVersion()
        {
            using (MainForm form = new MainForm())
            {
                Assert(form.Text == AppInfo.ProductName + " " + AppInfo.VersionLabel,
                    "The window title must show the name followed by the version.");
            }
        }

        private static void HeaderPaintsVersionAfterName()
        {
            using (MainForm form = new MainForm())
            {
                Panel header = Field<Panel>(form, "_header");
                using (Bitmap bitmap = new Bitmap(header.Width, header.Height, PixelFormat.Format32bppArgb))
                {
                    header.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    Color background = bitmap.GetPixel(2, 2);

                    Rectangle logo = new Rectangle(Theme.S(24), Theme.S(20), Theme.S(36), Theme.S(36));
                    int titleLeft = logo.Right + Theme.S(14);
                    Size nameSize = TextRenderer.MeasureText(AppInfo.ProductName, Theme.Title);
                    int versionLeft = titleLeft + nameSize.Width + Theme.S(8);
                    int versionRight = Math.Min(versionLeft + Theme.S(40), bitmap.Width - 1);

                    bool inkFound = false;
                    for (int y = Theme.S(14); y < Theme.S(40) && !inkFound; y++)
                    {
                        for (int x = versionLeft; x < versionRight; x++)
                        {
                            if (!ColorsClose(bitmap.GetPixel(x, y), background))
                            {
                                inkFound = true;
                            }
                        }
                    }
                    Assert(inkFound, "The header must draw the version right after the product name.");
                }
            }
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) <= 24 && Math.Abs(a.G - b.G) <= 24 && Math.Abs(a.B - b.B) <= 24;
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
