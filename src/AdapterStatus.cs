using System;
using System.Globalization;
using System.Management;

namespace EasyIpSwitch
{
    internal static class AdapterStatus
    {
        // IF_ADMIN_STATUS: Up = 1, Down = 2, Testing = 3.
        public static bool? FromAdminStatus(object value)
        {
            if (value == null)
            {
                return null;
            }
            try
            {
                int status = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (status == 1)
                {
                    return true;
                }
                if (status == 2)
                {
                    return false;
                }
            }
            catch
            {
            }
            return null;
        }

        public static bool? ReadModernEnabled(AdapterIdentity identity)
        {
            bool? enabled;
            return TryReadModernEnabled(identity, out enabled) ? enabled : null;
        }

        public static bool TryReadModernEnabled(AdapterIdentity identity, out bool? enabled)
        {
            enabled = null;
            Guid guid;
            if (identity == null || !Guid.TryParse(identity.InterfaceGuid, out guid) || guid == Guid.Empty)
            {
                return false;
            }

            string query = "SELECT InterfaceAdminStatus FROM MSFT_NetAdapter WHERE InterfaceGuid = '"
                + guid.ToString("B").ToUpperInvariant() + "'";
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\StandardCimv2", query))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementBaseObject adapter in results)
                        {
                            object status = adapter["InterfaceAdminStatus"];
                            if (status == null)
                            {
                                return false;
                            }
                            enabled = FromAdminStatus(status);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Le fournisseur moderne n'est pas disponible sur tous les
                // systèmes ; l'appelant peut alors consulter Win32_NetworkAdapter.
            }
            return false;
        }
    }
}
