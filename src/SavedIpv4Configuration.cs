using System;
using System.Globalization;
using Microsoft.Win32;

namespace EasyIpSwitch
{
    // Lecture seule des paramètres persistants. Aucune valeur du registre
    // n'est modifiée par l'application.
    internal sealed class SavedIpv4Configuration
    {
        public bool? DhcpEnabled;
        public string[] Addresses = new string[0];
        public string[] Masks = new string[0];
        public string[] Gateways = new string[0];

        public static SavedIpv4Configuration Read(string interfaceGuid)
        {
            Guid guid;
            if (!Guid.TryParse(interfaceGuid, out guid) || guid == Guid.Empty)
            {
                return null;
            }

            string path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\"
                + guid.ToString("B").ToUpperInvariant();
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path, false))
            {
                if (key == null)
                {
                    return null;
                }
                return FromValues(key.GetValue("EnableDHCP"), key.GetValue("IPAddress"),
                    key.GetValue("SubnetMask"), key.GetValue("DefaultGateway"));
            }
        }

        internal static SavedIpv4Configuration FromValues(object dhcp, object addresses, object masks, object gateways)
        {
            SavedIpv4Configuration result = new SavedIpv4Configuration();
            if (dhcp != null)
            {
                try
                {
                    int value = Convert.ToInt32(dhcp, CultureInfo.InvariantCulture);
                    if (value == 0 || value == 1)
                    {
                        result.DhcpEnabled = value == 1;
                    }
                }
                catch
                {
                }
            }
            result.Addresses = AsStrings(addresses);
            result.Masks = AsStrings(masks);
            result.Gateways = AsStrings(gateways);
            return result;
        }

        public bool MatchesAddressAndMask(string address, string mask)
        {
            for (int i = 0; i < Addresses.Length; i++)
            {
                if (SameIp(Addresses[i], address) && i < Masks.Length && SameIp(Masks[i], mask))
                {
                    return true;
                }
            }
            return false;
        }

        public bool MatchesGateway(string gateway)
        {
            bool wantsNone = IsEmptyGateway(gateway);
            bool hasGateway = false;
            for (int i = 0; i < Gateways.Length; i++)
            {
                if (IsEmptyGateway(Gateways[i]))
                {
                    continue;
                }
                hasGateway = true;
                if (!wantsNone && SameIp(Gateways[i], gateway))
                {
                    return true;
                }
            }
            return wantsNone && !hasGateway;
        }

        public bool TryGetFirstAddress(out string address, out string mask)
        {
            address = "";
            mask = "";
            for (int i = 0; i < Addresses.Length && i < Masks.Length; i++)
            {
                uint ip;
                uint subnet;
                if (IpUtil.TryParse(Addresses[i], out ip) && ip != 0u
                    && IpUtil.TryParse(Masks[i], out subnet) && IpUtil.IsValidMask(subnet))
                {
                    address = IpUtil.Format(ip);
                    mask = IpUtil.Format(subnet);
                    return true;
                }
            }
            return false;
        }

        public string FirstGateway()
        {
            for (int i = 0; i < Gateways.Length; i++)
            {
                uint value;
                if (IpUtil.TryParse(Gateways[i], out value) && value != 0u)
                {
                    return IpUtil.Format(value);
                }
            }
            return "";
        }

        internal static bool SameIp(string left, string right)
        {
            uint a;
            uint b;
            return IpUtil.TryParse(left, out a) && IpUtil.TryParse(right, out b) && a == b;
        }

        internal static bool IsEmptyGateway(string value)
        {
            uint parsed;
            return string.IsNullOrWhiteSpace(value) || (IpUtil.TryParse(value, out parsed) && parsed == 0u);
        }

        private static string[] AsStrings(object value)
        {
            string[] strings = value as string[];
            if (strings != null)
            {
                return strings;
            }
            string single = value as string;
            return single == null ? new string[0] : new string[] { single };
        }
    }
}
