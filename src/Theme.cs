using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EasyIpSwitch
{
    internal interface IThemeAware
    {
        void ApplyTheme();
    }

    internal sealed class Palette
    {
        public Color Bg;
        public Color Surface;
        public Color SurfaceMuted;
        public Color SurfaceSunken;
        public Color Border;
        public Color BorderStrong;
        public Color Text;
        public Color TextMuted;
        public Color TextFaint;
        public Color Accent;
        public Color AccentHover;
        public Color AccentPressed;
        public Color AccentSoft;
        public Color Wifi;
        public Color WifiSoft;
        public Color Success;
        public Color SuccessSoft;
        public Color Danger;
        public Color DangerSoft;
        public Color Warning;
        public Color WarningSoft;
    }

    internal static class Theme
    {
        public static Color Hex(string value)
        {
            return ColorTranslator.FromHtml(value);
        }

        private static Palette _palette = CreateLight();
        private static bool _dark;

        public static event EventHandler Changed;

        public static Color Bg { get { return _palette.Bg; } }
        public static Color Surface { get { return _palette.Surface; } }
        public static Color SurfaceMuted { get { return _palette.SurfaceMuted; } }
        public static Color SurfaceSunken { get { return _palette.SurfaceSunken; } }
        public static Color Border { get { return _palette.Border; } }
        public static Color BorderStrong { get { return _palette.BorderStrong; } }
        public static Color Text { get { return _palette.Text; } }
        public static Color TextMuted { get { return _palette.TextMuted; } }
        public static Color TextFaint { get { return _palette.TextFaint; } }
        public static Color Accent { get { return _palette.Accent; } }
        public static Color AccentHover { get { return _palette.AccentHover; } }
        public static Color AccentPressed { get { return _palette.AccentPressed; } }
        public static Color AccentSoft { get { return _palette.AccentSoft; } }
        public static Color Wifi { get { return _palette.Wifi; } }
        public static Color WifiSoft { get { return _palette.WifiSoft; } }
        public static Color Success { get { return _palette.Success; } }
        public static Color SuccessSoft { get { return _palette.SuccessSoft; } }
        public static Color Danger { get { return _palette.Danger; } }
        public static Color DangerSoft { get { return _palette.DangerSoft; } }
        public static Color Warning { get { return _palette.Warning; } }
        public static Color WarningSoft { get { return _palette.WarningSoft; } }

        public static bool IsDark
        {
            get { return _dark; }
        }

        public static void SetDark(bool dark)
        {
            if (_dark == dark)
            {
                return;
            }
            _dark = dark;
            _palette = dark ? CreateDark() : CreateLight();
            if (Changed != null)
            {
                Changed(null, EventArgs.Empty);
            }
        }

        private static Palette CreateLight()
        {
            Palette p = new Palette();
            p.Bg = Hex("#EDF1F7");
            p.Surface = Color.White;
            p.SurfaceMuted = Hex("#F8FAFC");
            p.SurfaceSunken = Hex("#F1F5F9");
            p.Border = Hex("#E3E8EF");
            p.BorderStrong = Hex("#C7D0DD");
            p.Text = Hex("#111827");
            p.TextMuted = Hex("#667085");
            p.TextFaint = Hex("#98A2B3");
            p.Accent = Hex("#2563EB");
            p.AccentHover = Hex("#1D4ED8");
            p.AccentPressed = Hex("#1E40AF");
            p.AccentSoft = Hex("#DBEAFE");
            p.Wifi = Hex("#7C3AED");
            p.WifiSoft = Hex("#F1EAFE");
            p.Success = Hex("#15803D");
            p.SuccessSoft = Hex("#DCFCE7");
            p.Danger = Hex("#DC2626");
            p.DangerSoft = Hex("#FEE2E2");
            p.Warning = Hex("#B45309");
            p.WarningSoft = Hex("#FEF3C7");
            return p;
        }

        private static Palette CreateDark()
        {
            Palette p = new Palette();
            p.Bg = Hex("#0B1220");
            p.Surface = Hex("#141D2C");
            p.SurfaceMuted = Hex("#1A2434");
            p.SurfaceSunken = Hex("#1E293B");
            p.Border = Hex("#263448");
            p.BorderStrong = Hex("#3B4B63");
            p.Text = Hex("#E8EEF8");
            p.TextMuted = Hex("#9AA9BE");
            p.TextFaint = Hex("#6B7A90");
            p.Accent = Hex("#3B82F6");
            p.AccentHover = Hex("#2F6FE0");
            p.AccentPressed = Hex("#2563EB");
            p.AccentSoft = Hex("#1B2E4F");
            p.Wifi = Hex("#A78BFA");
            p.WifiSoft = Hex("#2A2050");
            p.Success = Hex("#4ADE80");
            p.SuccessSoft = Hex("#123524");
            p.Danger = Hex("#F87171");
            p.DangerSoft = Hex("#3C1A1A");
            p.Warning = Hex("#FBBF24");
            p.WarningSoft = Hex("#3A2A12");
            return p;
        }

        public static bool DetectSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int)
                        {
                            return (int)value == 0;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static float Dpi = 96f;

        public static Font Title;
        public static Font Subtitle;
        public static Font Body;
        public static Font BodyBold;
        public static Font Small;
        public static Font SmallBold;
        public static Font CardTitle;
        public static Font ButtonFont;

        private static string _family;

        public static int S(int value)
        {
            return (int)Math.Round(value * (Dpi / 96.0));
        }

        public static int S(float value)
        {
            return (int)Math.Round(value * (Dpi / 96.0));
        }

        public static string FontFamilyName
        {
            get
            {
                if (_family == null)
                {
                    try
                    {
                        using (FontFamily probe = new FontFamily("Segoe UI"))
                        {
                            _family = probe.Name;
                        }
                    }
                    catch
                    {
                        _family = SystemFonts.MessageBoxFont.FontFamily.Name;
                    }
                }
                return _family;
            }
        }

        public static Font F(float size, FontStyle style)
        {
            return new Font(FontFamilyName, size, style, GraphicsUnit.Point);
        }

        public static void Init()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    Dpi = g.DpiX;
                }
            }
            catch
            {
                Dpi = 96f;
            }
            if (Dpi < 96f)
            {
                Dpi = 96f;
            }

            Title = F(15f, FontStyle.Bold);
            Subtitle = F(8.75f, FontStyle.Regular);
            Body = F(9.5f, FontStyle.Regular);
            BodyBold = F(9.5f, FontStyle.Bold);
            Small = F(8.5f, FontStyle.Regular);
            SmallBold = F(8.5f, FontStyle.Bold);
            CardTitle = F(11.5f, FontStyle.Bold);
            ButtonFont = F(9.75f, FontStyle.Bold);
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return path;
            }
            int d = radius * 2;
            if (d > bounds.Width)
            {
                d = bounds.Width;
            }
            if (d > bounds.Height)
            {
                d = bounds.Height;
            }
            if (d <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0f)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Rectangle Deflate(Rectangle r, int amount)
        {
            return new Rectangle(r.X + amount, r.Y + amount, Math.Max(0, r.Width - amount * 2), Math.Max(0, r.Height - amount * 2));
        }

        public const TextFormatFlags TextLeft = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags TextCenter = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
    }
}
