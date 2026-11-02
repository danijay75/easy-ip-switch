using System;

namespace EasyIpSwitch
{
    internal static class AppInfo
    {
        public const string ProductName = "Easy IP-Switch";
        public const string Version = "1.1.1";
        public const string VersionLabel = "v" + Version;
        public const string WebsiteUrl = "https://www.angelivisions.com";

        public const string DonationUrl = "https://www.paypal.com/donate/?hosted_button_id=WZBCQXPEHHDDC";

        public static string CopyrightLine
        {
            get
            {
                return L.F("© {0} Angeli Visions. Tous droits réservés.", DateTime.Now.Year);
            }
        }
    }
}
