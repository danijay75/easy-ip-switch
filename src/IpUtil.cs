using System;
using System.Globalization;

namespace EasyIpSwitch
{
    internal static class IpUtil
    {
        public static bool TryParse(string text, out uint value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] parts = text.Trim().Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            uint result = 0;
            for (int i = 0; i < 4; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || part.Length > 3)
                {
                    return false;
                }
                int number = 0;
                for (int j = 0; j < part.Length; j++)
                {
                    char c = part[j];
                    if (c < '0' || c > '9')
                    {
                        return false;
                    }
                    number = number * 10 + (c - '0');
                }
                if (number > 255)
                {
                    return false;
                }
                result = (result << 8) | (uint)number;
            }

            value = result;
            return true;
        }

        public static bool IsValidMask(uint mask)
        {
            if (mask == 0u)
            {
                return false;
            }
            uint inverted = ~mask;
            return (inverted & (inverted + 1u)) == 0u;
        }

        public static bool IsApipa(uint address)
        {
            return (address & 0xFFFF0000u) == 0xA9FE0000u;
        }

        /// <summary>
        /// Proposes the conventional gateway for a static IPv4 configuration:
        /// the first usable host of the subnet (network + 1). When that host is
        /// the address itself, the next usable host is proposed so the gateway
        /// stays different from the IP. Returns null for invalid input, APIPA,
        /// forbidden addresses and masks without a usable host range (/31, /32).
        /// </summary>
        public static string SuggestGateway(string addressText, string maskText)
        {
            uint address;
            uint mask;
            if (!TryParse(addressText, out address) || !TryParse(maskText, out mask))
            {
                return null;
            }
            if (address == 0u || address == 0xFFFFFFFFu || IsApipa(address) || !IsValidMask(mask))
            {
                return null;
            }

            uint hostMask = ~mask;
            if (hostMask <= 1u)
            {
                return null;
            }

            uint network = address & mask;
            uint broadcast = network | hostMask;
            uint candidate = network + 1u;
            if (candidate == address)
            {
                candidate = network + 2u;
            }
            if (candidate == address || candidate >= broadcast)
            {
                return null;
            }
            return Format(candidate);
        }

        public static string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac))
            {
                return "";
            }
            string compact = mac.Replace("-", "").Replace(":", "");
            if (compact.Length == 12)
            {
                bool hex = true;
                for (int i = 0; i < compact.Length; i++)
                {
                    char c = compact[i];
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    {
                        hex = false;
                        break;
                    }
                }
                if (hex)
                {
                    compact = compact.ToUpperInvariant();
                    return string.Join(":", new string[]
                    {
                        compact.Substring(0, 2), compact.Substring(2, 2), compact.Substring(4, 2),
                        compact.Substring(6, 2), compact.Substring(8, 2), compact.Substring(10, 2)
                    });
                }
            }
            return mac.Replace("-", ":").ToUpperInvariant();
        }

        public static string Format(uint address)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}",
                (address >> 24) & 255,
                (address >> 16) & 255,
                (address >> 8) & 255,
                address & 255);
        }
    }
}
