using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyIpSwitch
{
    internal sealed class AdapterInfo
    {
        public string Name = "";
        public string Description = "";
        public int Index = -1;
        public string InterfaceGuid = "";
        public string DeviceId = "";
        public bool Wireless;
        public bool EnabledKnown = true;
        public bool Enabled;
        public bool Connected;
        public bool DhcpKnown = true;
        public bool Dhcp = true;
        public string Ip = "";
        public string Mask = "";
        public string Gateway = "";
        public string SavedStaticIp = "";
        public string SavedStaticMask = "";
        public string SavedStaticGateway = "";
        public string Mac = "";
        public long Speed;

        public string TypeLabel
        {
            get { return Wireless ? L.T("Wi-Fi") : L.T("Ethernet (câble)"); }
        }

        public string SpeedLabel
        {
            get
            {
                if (!EnabledKnown || !Enabled || Speed <= 0 || Speed == long.MaxValue)
                {
                    return "";
                }
                double gbit = Speed / 1000000000.0;
                if (gbit >= 1.0)
                {
                    return string.Format(L.Culture, "{0:0.##} Gbit/s", gbit);
                }
                double mbit = Speed / 1000000.0;
                if (mbit >= 1.0)
                {
                    return string.Format(L.Culture, "{0:0.#} Mbit/s", mbit);
                }
                return "";
            }
        }

        public string StatusLabel
        {
            get
            {
                if (!EnabledKnown)
                {
                    return L.T("État indisponible");
                }
                if (!Enabled)
                {
                    return L.T("Désactivée");
                }
                return Connected ? L.T("Connectée") : L.T("Non connectée");
            }
        }

        public string MetaLabel
        {
            get
            {
                string type = TypeLabel;
                if (!Wireless && !string.IsNullOrWhiteSpace(Description))
                {
                    type += " - " + Description.Trim();
                }
                string speed = SpeedLabel;
                return speed.Length == 0 ? type : type + "  ·  " + speed;
            }
        }
    }

    internal sealed class OpResult
    {
        public bool Success;
        public bool Unconfirmed;
        public bool PartiallyApplied;
        public string Message = "";

        public static OpResult Ok(string message)
        {
            OpResult result = new OpResult();
            result.Success = true;
            result.Message = message;
            return result;
        }

        public static OpResult Fail(string message)
        {
            OpResult result = new OpResult();
            result.Success = false;
            result.Message = message;
            return result;
        }

        public static OpResult Awaiting(string message)
        {
            OpResult result = new OpResult();
            result.Unconfirmed = true;
            result.Message = message;
            return result;
        }

        public static OpResult Partial(string message)
        {
            OpResult result = Awaiting(message);
            result.PartiallyApplied = true;
            return result;
        }
    }

    internal static class NetworkService
    {
        private sealed class AdapterMeta
        {
            public string Name = "";
            public string Description = "";
            public int Index = -1;
            public string InterfaceGuid = "";
            public string DeviceId = "";
            public bool Wireless;
            public bool Enabled;
            public bool StateKnown;
            public bool ModernStatusPresent;
            public bool Connected;
            public string Mac = "";
            public long Speed;
        }

        private sealed class CfgData
        {
            public bool Dhcp = true;
            public bool DhcpKnown;
            public string Ip = "";
            public string Mask = "";
            public string Gateway = "";
        }

        public static List<AdapterInfo> GetAdapters()
        {
            Dictionary<int, AdapterMeta> metas = new Dictionary<int, AdapterMeta>();
            Dictionary<int, AdapterMeta> legacy = new Dictionary<int, AdapterMeta>();
            bool modernOk = false;
            bool legacyOk = false;
            string modernError = "";
            string legacyError = "";

            try
            {
                ReadModern(metas);
                modernOk = true;
            }
            catch (Exception ex)
            {
                metas.Clear();
                modernError = ex.Message;
            }

            try
            {
                ReadLegacy(legacy);
                legacyOk = true;
            }
            catch (Exception ex)
            {
                legacy.Clear();
                legacyError = ex.Message;
            }

            if (!modernOk && !legacyOk)
            {
                throw new InvalidOperationException(L.F("Lecture des cartes réseau impossible. {0}", Short(modernError + " " + legacyError)));
            }

            if (legacyOk)
            {
                MergeLegacy(metas, legacy);
            }

            Dictionary<int, CfgData> configs = ReadConfigs();
            List<AdapterInfo> list = new List<AdapterInfo>();

            foreach (AdapterMeta meta in metas.Values)
            {
                AdapterInfo info = new AdapterInfo();
                info.Name = meta.Name;
                info.Description = meta.Description;
                info.Index = meta.Index;
                info.InterfaceGuid = meta.InterfaceGuid;
                info.DeviceId = meta.DeviceId;
                info.Wireless = meta.Wireless;
                info.EnabledKnown = meta.StateKnown;
                info.Enabled = meta.Enabled;
                info.Connected = meta.Enabled && meta.Connected;
                info.Mac = meta.Mac;
                info.Speed = meta.Speed;

                CfgData cfg;
                info.DhcpKnown = false;
                if (configs.TryGetValue(meta.Index, out cfg))
                {
                    info.Dhcp = cfg.Dhcp;
                    info.DhcpKnown = cfg.DhcpKnown;
                    info.Ip = cfg.Ip;
                    info.Mask = cfg.Mask;
                    info.Gateway = cfg.Gateway;
                }

                if (!info.Connected)
                {
                    try
                    {
                        SavedIpv4Configuration saved = SavedIpv4Configuration.Read(meta.InterfaceGuid);
                        ApplySavedOfflineConfiguration(info, saved);
                    }
                    catch
                    {
                        // Une lecture indisponible ne doit jamais modifier la configuration.
                    }
                }
                list.Add(info);
            }

            list.Sort(delegate(AdapterInfo a, AdapterInfo b)
            {
                if (a.Enabled != b.Enabled)
                {
                    return a.Enabled ? -1 : 1;
                }
                if (a.Wireless != b.Wireless)
                {
                    return a.Wireless ? 1 : -1;
                }
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return list;
        }

        internal static void ApplySavedOfflineConfiguration(AdapterInfo info, SavedIpv4Configuration saved)
        {
            if (info == null || info.Connected || saved == null || !saved.DhcpEnabled.HasValue)
            {
                return;
            }
            if (!info.DhcpKnown)
            {
                info.Dhcp = saved.DhcpEnabled.Value;
                info.DhcpKnown = true;
            }

            string address;
            string mask;
            if (!saved.TryGetFirstAddress(out address, out mask))
            {
                return;
            }
            if (info.DhcpKnown && info.Dhcp)
            {
                info.SavedStaticIp = address;
                info.SavedStaticMask = mask;
                info.SavedStaticGateway = saved.FirstGateway();
            }
            else if (info.DhcpKnown && !info.Dhcp && !saved.DhcpEnabled.Value)
            {
                info.Ip = address;
                info.Mask = mask;
                info.Gateway = saved.FirstGateway();
            }
        }

        private static void ReadModern(Dictionary<int, AdapterMeta> metas)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                @"root\StandardCimv2",
                "SELECT Name, InterfaceDescription, InterfaceIndex, InterfaceGuid, InterfaceType, NdisPhysicalMedium, Virtual, HardwareInterface, InterfaceAdminStatus, InterfaceOperationalStatus, MediaConnectState, NetworkAddresses, PermanentAddress, Speed FROM MSFT_NetAdapter"))
            {
                searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject raw in results)
                    {
                        int index = ToInt(raw["InterfaceIndex"], -1);
                        string name = ToText(raw["Name"]);
                        string desc = ToText(raw["InterfaceDescription"]);
                        string guid = ToText(raw["InterfaceGuid"]);
                        if (name.Length == 0 || desc.Length == 0 || (index < 0 && guid.Length == 0))
                        {
                            continue;
                        }
                        if (ToBool(raw["Virtual"], false) || !ToBool(raw["HardwareInterface"], false)
                            || IsIgnored(name) || IsIgnored(desc))
                        {
                            continue;
                        }

                        object adminStatus = raw["InterfaceAdminStatus"];
                        bool? enabled = AdapterStatus.FromAdminStatus(adminStatus);
                        AdapterMeta meta = new AdapterMeta();
                        meta.Name = name;
                        meta.Description = desc;
                        meta.Index = index;
                        meta.InterfaceGuid = guid;
                        meta.Wireless = ToInt(raw["InterfaceType"], 0) == 71
                            || ToInt(raw["NdisPhysicalMedium"], 0) == 9 || LooksWireless(desc) || LooksWireless(name);
                        meta.StateKnown = enabled.HasValue;
                        meta.ModernStatusPresent = adminStatus != null;
                        meta.Enabled = enabled.GetValueOrDefault(true);
                        int operationalStatus = ToInt(raw["InterfaceOperationalStatus"], -1);
                        meta.Connected = operationalStatus == 1
                            || (operationalStatus < 0 && ToInt(raw["MediaConnectState"], 0) == 1);
                        string[] addresses = raw["NetworkAddresses"] as string[];
                        meta.Mac = addresses != null && addresses.Length > 0 ? ToText(addresses[0]) : "";
                        if (meta.Mac.Length == 0)
                        {
                            meta.Mac = ToText(raw["PermanentAddress"]);
                        }
                        meta.Speed = ToLong(raw["Speed"], 0);
                        AddMeta(metas, meta);
                    }
                }
            }
        }

        private static void MergeLegacy(Dictionary<int, AdapterMeta> metas, Dictionary<int, AdapterMeta> legacy)
        {
            foreach (AdapterMeta older in legacy.Values)
            {
                AdapterMeta current = FindMatchingMeta(metas, older);
                if (current == null)
                {
                    AddMeta(metas, older);
                    continue;
                }

                current.DeviceId = older.DeviceId;
                if (current.InterfaceGuid.Length == 0)
                {
                    current.InterfaceGuid = older.InterfaceGuid;
                }
                if (current.Index < 0)
                {
                    current.Index = older.Index;
                }
                if (current.Mac.Length == 0)
                {
                    current.Mac = older.Mac;
                }
                if ((current.Speed <= 0 || current.Speed == long.MaxValue)
                    && older.Speed > 0 && older.Speed != long.MaxValue)
                {
                    current.Speed = older.Speed;
                }
                if (!current.StateKnown && !current.ModernStatusPresent && older.StateKnown)
                {
                    current.Enabled = older.Enabled;
                    current.StateKnown = true;
                }
            }
        }

        private static AdapterMeta FindMatchingMeta(Dictionary<int, AdapterMeta> metas, AdapterMeta candidate)
        {
            Guid candidateGuid;
            bool candidateHasGuid = Guid.TryParse(candidate.InterfaceGuid, out candidateGuid) && candidateGuid != Guid.Empty;
            foreach (AdapterMeta current in metas.Values)
            {
                Guid currentGuid;
                bool currentHasGuid = Guid.TryParse(current.InterfaceGuid, out currentGuid) && currentGuid != Guid.Empty;
                if (candidateHasGuid && currentHasGuid)
                {
                    if (candidateGuid == currentGuid)
                    {
                        return current;
                    }
                    continue;
                }

                if (string.Equals(current.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)
                    && (current.Index == candidate.Index || current.Index < 0 || candidate.Index < 0))
                {
                    return current;
                }
            }
            return null;
        }

        private static void AddMeta(Dictionary<int, AdapterMeta> metas, AdapterMeta meta)
        {
            int key = meta.Index;
            if (key < 0 || metas.ContainsKey(key))
            {
                key = -1;
                while (metas.ContainsKey(key))
                {
                    key--;
                }
            }
            metas[key] = meta;
        }

        private static Dictionary<int, CfgData> ReadConfigs()
        {
            Dictionary<int, CfgData> result = new Dictionary<int, CfgData>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT InterfaceIndex, DHCPEnabled, IPAddress, IPSubnet, DefaultIPGateway FROM Win32_NetworkAdapterConfiguration"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementBaseObject raw in results)
                        {
                            int index = ToInt(raw["InterfaceIndex"], -1);
                            if (index < 0 || result.ContainsKey(index))
                            {
                                continue;
                            }

                            CfgData cfg = new CfgData();
                            cfg.DhcpKnown = raw["DHCPEnabled"] != null;
                            cfg.Dhcp = ToBool(raw["DHCPEnabled"], true);

                            string[] addresses = raw["IPAddress"] as string[];
                            string[] masks = raw["IPSubnet"] as string[];
                            string[] gateways = raw["DefaultIPGateway"] as string[];

                            if (addresses != null)
                            {
                                for (int i = 0; i < addresses.Length; i++)
                                {
                                    if (addresses[i] == null || addresses[i].IndexOf(':') >= 0)
                                    {
                                        continue;
                                    }
                                    cfg.Ip = addresses[i];
                                    if (masks != null && i < masks.Length && masks[i] != null && masks[i].IndexOf('.') >= 0)
                                    {
                                        cfg.Mask = masks[i];
                                    }
                                    break;
                                }
                            }

                            if (gateways != null)
                            {
                                for (int i = 0; i < gateways.Length; i++)
                                {
                                    if (gateways[i] != null && gateways[i].IndexOf(':') < 0)
                                    {
                                        cfg.Gateway = gateways[i];
                                        break;
                                    }
                                }
                            }

                            result[index] = cfg;
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT InterfaceIndex, Dhcp FROM MSFT_NetIPInterface WHERE AddressFamily = 2"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementBaseObject raw in results)
                        {
                            int index = ToInt(raw["InterfaceIndex"], -1);
                            int dhcp = ToInt(raw["Dhcp"], -1);
                            if (index < 0 || (dhcp != 0 && dhcp != 1))
                            {
                                continue;
                            }
                            CfgData cfg;
                            if (!result.TryGetValue(index, out cfg))
                            {
                                cfg = new CfgData();
                                result[index] = cfg;
                            }
                            cfg.Dhcp = dhcp == 1;
                            cfg.DhcpKnown = true;
                        }
                    }
                }
            }
            catch
            {
                // Le fournisseur historique reste disponible en secours.
            }
            return result;
        }

        private static void ReadLegacy(Dictionary<int, AdapterMeta> metas)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT InterfaceIndex, GUID, DeviceID, NetConnectionID, Description, NetEnabled, NetConnectionStatus, PhysicalAdapter, MACAddress, Speed FROM Win32_NetworkAdapter"))
            {
                searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementBaseObject raw in results)
                    {
                        string name = ToText(raw["NetConnectionID"]);
                        string desc = ToText(raw["Description"]);
                        int index = ToInt(raw["InterfaceIndex"], -1);
                        string guid = ToText(raw["GUID"]);
                        string deviceId = ToText(raw["DeviceID"]);
                        if (name.Length == 0 || desc.Length == 0
                            || (index < 0 && guid.Length == 0 && deviceId.Length == 0))
                        {
                            continue;
                        }
                        if (!ToBool(raw["PhysicalAdapter"], false) || IsIgnored(desc))
                        {
                            continue;
                        }

                        AdapterMeta meta = new AdapterMeta();
                        meta.Name = name;
                        meta.Description = desc;
                        meta.Index = index;
                        meta.InterfaceGuid = guid;
                        meta.DeviceId = deviceId;
                        meta.Wireless = LooksWireless(desc) || LooksWireless(name);
                        meta.StateKnown = raw["NetEnabled"] != null;
                        meta.Enabled = ToBool(raw["NetEnabled"], true);
                        meta.Connected = ToInt(raw["NetConnectionStatus"], 0) == 2;
                        meta.Mac = ToText(raw["MACAddress"]);
                        meta.Speed = ToLong(raw["Speed"], 0);
                        AddMeta(metas, meta);
                    }
                }
            }
        }

        public static OpResult ApplyStatic(AdapterIdentity identity, string ip, string mask, string gateway)
        {
            return StaticIpv4Configuration.Apply(identity, ip, mask, gateway);
        }

        public static OpResult ApplyDhcp(int index, string name)
        {
            string detail = "";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int code;
                detail = RunNetsh("interface ipv4 set address name=\"" + name + "\" source=dhcp", out code);
                int ignored;
                RunNetsh("interface ipv4 set dnsservers name=\"" + name + "\" source=dhcp", out ignored);

                if (code == 0 && WaitForDhcp(index, 8))
                {
                    AdapterInfo info = Find(index);
                    if (info != null && info.Ip.Length > 0)
                    {
                        return OpResult.Ok(L.F("DHCP activé. Adresse obtenue : {0}", info.Ip));
                    }
                    return OpResult.Ok(L.T("DHCP activé. En attente d'une adresse du serveur DHCP."));
                }
                Thread.Sleep(400);
            }
            return OpResult.Fail(L.F("Impossible d'activer le DHCP. {0}", Short(detail)));
        }

        internal static string BuildStaticArgs(string ip, string mask, string gateway)
        {
            string args = "source=static address=" + ip + " mask=" + mask;
            if (!string.IsNullOrEmpty(gateway))
            {
                args += " gateway=" + gateway + " gwmetric=1";
            }
            else
            {
                args += " gateway=none";
            }
            return args;
        }

        private static bool WaitForDhcp(int index, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                AdapterInfo info = Find(index);
                if (info != null && info.Dhcp)
                {
                    return true;
                }
                Thread.Sleep(350);
            }
            return false;
        }

        private static AdapterInfo Find(int index)
        {
            try
            {
                List<AdapterInfo> adapters = GetAdapters();
                for (int i = 0; i < adapters.Count; i++)
                {
                    if (adapters[i].Index == index)
                    {
                        return adapters[i];
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        internal static string RunNetsh(string arguments, out int exitCode)
        {
            exitCode = -1;
            try
            {
                string executable = Path.Combine(Environment.SystemDirectory, "netsh.exe");
                ProcessStartInfo info = new ProcessStartInfo(executable, arguments);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.WorkingDirectory = Environment.SystemDirectory;

                using (Process process = Process.Start(info))
                {
                    if (process == null)
                    {
                        return L.T("Impossible de démarrer netsh.");
                    }

                    Task<byte[]> stdout = Task.Factory.StartNew<byte[]>(delegate
                    {
                        return ReadAllBytes(process.StandardOutput.BaseStream);
                    });
                    Task<byte[]> stderr = Task.Factory.StartNew<byte[]>(delegate
                    {
                        return ReadAllBytes(process.StandardError.BaseStream);
                    });
                    if (!process.WaitForExit(60000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        try
                        {
                            Task.WaitAll(new Task[] { stdout, stderr }, 1000);
                        }
                        catch
                        {
                        }
                        exitCode = -2;
                        return L.T("Délai dépassé.");
                    }

                    exitCode = process.ExitCode;
                    Encoding fallback;
                    try
                    {
                        fallback = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                    }
                    catch
                    {
                        fallback = Encoding.Default;
                    }
                    return (DecodeToolOutput(stdout.Result, fallback) + "\n"
                        + DecodeToolOutput(stderr.Result, fallback)).Trim();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        internal static string DecodeToolOutput(byte[] bytes, Encoding fallback)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                string value = new UTF8Encoding(false, true).GetString(bytes);
                return value.TrimStart('\uFEFF');
            }
            catch (DecoderFallbackException)
            {
                return (fallback ?? Encoding.Default).GetString(bytes);
            }
        }

        private static bool IsIgnored(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string value = text.ToLowerInvariant();
            string[] blocked = new string[]
            {
                "bluetooth", "wi-fi direct", "wifi direct", "virtual", "vmware", "hyper-v",
                "vethernet", "tap-", "loopback", "wan miniport", "vpn", "tunnel",
                "npcap", "kernel debug", "teredo", "isatap"
            };
            for (int i = 0; i < blocked.Length; i++)
            {
                if (value.IndexOf(blocked[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool LooksWireless(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string value = text.ToLowerInvariant();
            return value.IndexOf("wi-fi", StringComparison.Ordinal) >= 0
                || value.IndexOf("wifi", StringComparison.Ordinal) >= 0
                || value.IndexOf("wireless", StringComparison.Ordinal) >= 0
                || value.IndexOf("802.11", StringComparison.Ordinal) >= 0
                || value.IndexOf("wlan", StringComparison.Ordinal) >= 0
                || value.IndexOf("sans fil", StringComparison.Ordinal) >= 0
                || value.IndexOf("sans-fil", StringComparison.Ordinal) >= 0;
        }

        private static string Short(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }
            string text = message.Replace("\r", " ").Replace("\n", " ");
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                text = text.Replace("  ", " ");
            }
            text = text.Trim();
            if (text.Length > 180)
            {
                text = text.Substring(0, 180) + "…";
            }
            return text;
        }

        private static string ToText(object value)
        {
            if (value == null)
            {
                return "";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        private static int ToInt(object value, int fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static long ToLong(object value, long fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ToBool(object value, bool fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
