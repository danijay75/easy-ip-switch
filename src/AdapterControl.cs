using System;
using System.Globalization;
using System.Management;
using System.Threading;

namespace EasyIpSwitch
{
    internal sealed class AdapterIdentity
    {
        public readonly int Index;
        public readonly string Name;
        public readonly string InterfaceGuid;
        public readonly string DeviceId;

        public AdapterIdentity(int index, string name, string interfaceGuid, string deviceId)
        {
            Index = index;
            Name = name ?? "";
            InterfaceGuid = interfaceGuid ?? "";
            DeviceId = deviceId ?? "";
        }

        public string StableKey
        {
            get
            {
                Guid guid;
                if (Guid.TryParse(InterfaceGuid, out guid) && guid != Guid.Empty)
                {
                    return "guid:" + guid.ToString("D");
                }
                if (DeviceId.Length > 0)
                {
                    return "device:" + DeviceId + ":" + Name;
                }
                return "index:" + Index.ToString(CultureInfo.InvariantCulture) + ":" + Name;
            }
        }

        public bool Matches(AdapterInfo info)
        {
            if (info == null)
            {
                return false;
            }
            Guid expected;
            if (Guid.TryParse(InterfaceGuid, out expected) && expected != Guid.Empty)
            {
                Guid actual;
                return Guid.TryParse(info.InterfaceGuid, out actual) && actual == expected;
            }
            if (!string.Equals(Name, info.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (DeviceId.Length > 0)
            {
                return string.Equals(DeviceId, info.DeviceId, StringComparison.OrdinalIgnoreCase);
            }
            return Index >= 0 && Index == info.Index;
        }

        internal bool TryGetWmiQuery(out string query, out bool verifyName)
        {
            query = "";
            verifyName = false;

            Guid guid;
            if (Guid.TryParse(InterfaceGuid, out guid) && guid != Guid.Empty)
            {
                query = "SELECT * FROM Win32_NetworkAdapter WHERE GUID = '"
                    + guid.ToString("B").ToUpperInvariant() + "'";
                return true;
            }

            int device;
            if (Name.Length > 0 && int.TryParse(DeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out device) && device >= 0)
            {
                query = "SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '"
                    + device.ToString(CultureInfo.InvariantCulture) + "'";
                verifyName = true;
                return true;
            }

            if (Index >= 0 && Name.Length > 0)
            {
                query = "SELECT * FROM Win32_NetworkAdapter WHERE InterfaceIndex = "
                    + Index.ToString(CultureInfo.InvariantCulture);
                verifyName = true;
                return true;
            }

            return false;
        }
    }

    internal interface IAdapterControlBackend
    {
        OpResult Request(AdapterIdentity identity, bool enabled);
        bool? ReadEnabled(AdapterIdentity identity);
    }

    internal static class AdapterControl
    {
        public static OpResult SetEnabled(AdapterIdentity identity, bool enabled)
        {
            return SetEnabled(identity, enabled, new WmiAdapterControlBackend(),
                delegate(int milliseconds) { Thread.Sleep(milliseconds); });
        }

        // The injectable backend lets tests exercise the complete decision path
        // without enabling or disabling a real adapter.
        internal static OpResult SetEnabled(AdapterIdentity identity, bool enabled,
            IAdapterControlBackend backend, Action<int> delay)
        {
            string prefix = enabled ? L.T("Impossible d'activer la carte.") : L.T("Impossible de désactiver la carte.");
            string query;
            bool verifyName;
            if (identity == null || !identity.TryGetWmiQuery(out query, out verifyName))
            {
                return OpResult.Fail(prefix + " " + L.T("Identifiant de carte indisponible. Actualisez la liste et réessayez."));
            }

            OpResult request;
            try
            {
                request = backend.Request(identity, enabled);
            }
            catch (Exception ex)
            {
                return OpResult.Fail(prefix + " " + Detail(ex));
            }

            if (request == null || !request.Success)
            {
                string message = request == null || string.IsNullOrEmpty(request.Message)
                    ? L.T("La demande a été refusée par Windows.")
                    : request.Message;
                return OpResult.Fail(prefix + " " + message);
            }

            string lastError = "";
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    bool? state = backend.ReadEnabled(identity);
                    if (state.HasValue && state.Value == enabled)
                    {
                        return OpResult.Ok(enabled ? L.T("Carte activée.") : L.T("Carte désactivée."));
                    }
                }
                catch (Exception ex)
                {
                    lastError = Detail(ex);
                }

                if (i < 19)
                {
                    delay(300);
                }
            }

            string failure = L.T("Demande envoyée. L'état de la carte n'est pas encore confirmé ; une nouvelle actualisation sera effectuée.");
            if (lastError.Length > 0)
            {
                failure += " " + lastError;
            }
            return OpResult.Awaiting(failure);
        }

        private static string Detail(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }
            string message = (current.Message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (message.Length == 0)
            {
                return L.T("Erreur Windows inconnue.");
            }
            return message.Length > 180 ? message.Substring(0, 180) + "…" : message;
        }
    }

    internal sealed class WmiAdapterControlBackend : IAdapterControlBackend
    {
        public OpResult Request(AdapterIdentity identity, bool enabled)
        {
            return WithAdapter<OpResult>(identity, delegate(ManagementObject adapter)
            {
                object value = adapter.InvokeMethod(enabled ? "Enable" : "Disable", null);
                if (value == null)
                {
                    return OpResult.Fail(L.T("Windows n'a pas confirmé la demande."));
                }
                uint code = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                return code == 0
                    ? OpResult.Ok("")
                    : OpResult.Fail(L.F("Windows a refusé la demande (code WMI {0}).", code.ToString(CultureInfo.InvariantCulture)));
            });
        }

        public bool? ReadEnabled(AdapterIdentity identity)
        {
            bool? modern;
            if (AdapterStatus.TryReadModernEnabled(identity, out modern))
            {
                return modern;
            }

            return WithAdapter<bool?>(identity, delegate(ManagementObject adapter)
            {
                object value = adapter["NetEnabled"];
                return value == null ? (bool?)null : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            });
        }

        private static T WithAdapter<T>(AdapterIdentity identity, Func<ManagementObject, T> action)
        {
            string query;
            bool verifyName;
            if (identity == null || !identity.TryGetWmiQuery(out query, out verifyName))
            {
                throw new InvalidOperationException(L.T("Identifiant de carte indisponible. Actualisez la liste."));
            }

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\CIMV2", query))
            {
                // Active uniquement les privilèges déjà accordés au processus
                // élevé, comme recommandé pour les opérations WMI système.
                searcher.Scope.Options.EnablePrivileges = true;
                searcher.Options.Timeout = TimeSpan.FromSeconds(12);
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject adapter in results)
                    {
                        using (adapter)
                        {
                            if (verifyName)
                            {
                                string name = Convert.ToString(adapter["NetConnectionID"], CultureInfo.InvariantCulture) ?? "";
                                if (!string.Equals(name, identity.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                            }
                            return action(adapter);
                        }
                    }
                }
            }

            throw new InvalidOperationException(L.T("Carte introuvable dans Windows. Actualisez la liste puis réessayez."));
        }
    }
}
