using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EasyIpSwitch
{
    internal static class AdapterControlTests
    {
        private static int _assertions;
        private const string TestGuid = "{152B7C25-DF5D-41C5-9620-1858DC7FD200}";

        public static int Run()
        {
            StableGuidIsPreferredToNameAndIndex();
            SafeFallbackIdentifiers();
            AdministrativeStateIsInterpretedCorrectly();
            ModernStateWinsOverStaleLegacyState();
            EnableAndDisableUseBackendAndVerify();
            FailureAndUnconfirmedStateAreReported();
            UnusableIdentityIsRejected();
            DisabledAndInvalidSpeedsAreHidden();
            OutputEncodingIsDetected();
            OptionalReadOnlyWmiProbe();
            return _assertions;
        }

        private static void StableGuidIsPreferredToNameAndIndex()
        {
            AdapterIdentity identity = new AdapterIdentity(-1, "Old alias", TestGuid, "1");
            string query;
            bool verifyName;
            Assert(identity.TryGetWmiQuery(out query, out verifyName), "A GUID identifies a disabled adapter without an interface index.");
            Assert(query.IndexOf("GUID = '" + TestGuid + "'", StringComparison.Ordinal) >= 0, "The GUID must identify the adapter.");
            Assert(query.IndexOf("Old alias", StringComparison.Ordinal) < 0 && !verifyName, "GUID lookup must not depend on the connection name.");
        }

        private static void SafeFallbackIdentifiers()
        {
            string query;
            bool verifyName;
            AdapterIdentity device = new AdapterIdentity(-1, "Wi-Fi", "", "12");
            Assert(device.TryGetWmiQuery(out query, out verifyName), "Device ID must be usable when the GUID is absent.");
            Assert(query.IndexOf("DeviceID = '12'", StringComparison.Ordinal) >= 0 && verifyName, "Device lookup must verify the connection name.");

            AdapterIdentity indexed = new AdapterIdentity(4, "Wi-Fi", "", "invalid");
            Assert(indexed.TryGetWmiQuery(out query, out verifyName), "The interface index is the last fallback.");
            Assert(query.IndexOf("InterfaceIndex = 4", StringComparison.Ordinal) >= 0 && verifyName, "Index lookup must verify the connection name.");

            AdapterIdentity unsafeName = new AdapterIdentity(4, "name' OR 'x'='x", "", "12");
            Assert(unsafeName.TryGetWmiQuery(out query, out verifyName), "The fallback must handle names as data.");
            Assert(query.IndexOf("name'", StringComparison.Ordinal) < 0 && verifyName, "A connection name must never be interpolated into WMI queries.");
        }

        private static void AdministrativeStateIsInterpretedCorrectly()
        {
            Assert(AdapterStatus.FromAdminStatus((uint)1) == true, "Administrative up means enabled.");
            Assert(AdapterStatus.FromAdminStatus((uint)2) == false, "Administrative down means disabled.");
            Assert(!AdapterStatus.FromAdminStatus((uint)3).HasValue, "Testing is not a confirmed state.");
            Assert(!AdapterStatus.FromAdminStatus(null).HasValue, "A missing status is not a confirmed state.");
        }

        private static void ModernStateWinsOverStaleLegacyState()
        {
            Type metaType = typeof(NetworkService).GetNestedType("AdapterMeta", BindingFlags.NonPublic);
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(int), metaType);
            IDictionary modern = (IDictionary)Activator.CreateInstance(dictionaryType);
            IDictionary legacy = (IDictionary)Activator.CreateInstance(dictionaryType);
            object current = Activator.CreateInstance(metaType, true);
            object older = Activator.CreateInstance(metaType, true);
            SetMeta(metaType, current, "Name", "Wi-Fi");
            SetMeta(metaType, current, "Index", 4);
            SetMeta(metaType, current, "InterfaceGuid", TestGuid);
            SetMeta(metaType, current, "StateKnown", true);
            SetMeta(metaType, current, "Enabled", true);
            SetMeta(metaType, older, "Name", "Wi-Fi");
            SetMeta(metaType, older, "Index", 15);
            SetMeta(metaType, older, "InterfaceGuid", TestGuid);
            SetMeta(metaType, older, "DeviceId", "1");
            SetMeta(metaType, older, "StateKnown", true);
            SetMeta(metaType, older, "Enabled", false);
            modern.Add(4, current);
            legacy.Add(15, older);

            MethodInfo merge = typeof(NetworkService).GetMethod("MergeLegacy", BindingFlags.NonPublic | BindingFlags.Static);
            merge.Invoke(null, new object[] { modern, legacy });
            Assert(modern.Count == 1, "The same GUID must be merged even if the index changed.");
            Assert((bool)metaType.GetField("Enabled").GetValue(current), "Stale legacy state must not overwrite the modern enabled state.");
            Assert((string)metaType.GetField("DeviceId").GetValue(current) == "1", "The legacy device ID must be retained.");

            SetMeta(metaType, current, "StateKnown", false);
            merge.Invoke(null, new object[] { modern, legacy });
            Assert(!(bool)metaType.GetField("Enabled").GetValue(current), "Legacy state must be used when modern state is unknown.");

            SetMeta(metaType, current, "StateKnown", false);
            SetMeta(metaType, current, "ModernStatusPresent", true);
            SetMeta(metaType, current, "Enabled", true);
            merge.Invoke(null, new object[] { modern, legacy });
            Assert(!(bool)metaType.GetField("StateKnown").GetValue(current), "A transitional modern state must not be overwritten by stale legacy data.");
        }

        private static void SetMeta(Type type, object instance, string field, object value)
        {
            type.GetField(field).SetValue(instance, value);
        }

        private static void EnableAndDisableUseBackendAndVerify()
        {
            AdapterIdentity identity = new AdapterIdentity(-1, "Wi-Fi", TestGuid, "1");
            FakeBackend backend = new FakeBackend();
            backend.State = false;
            backend.ApplyRequest = true;
            int delays = 0;

            OpResult result = AdapterControl.SetEnabled(identity, true, backend, delegate(int ms) { delays++; });
            Assert(result.Success, "The backend must be able to enable an adapter without a current index.");
            Assert(backend.Requests == 1 && backend.LastTarget && object.ReferenceEquals(backend.LastIdentity, identity), "The exact stable identity must be used for activation.");
            Assert(backend.Reads == 1 && delays == 0, "Success must be based on a confirmed state.");

            result = AdapterControl.SetEnabled(identity, false, backend, delegate(int ms) { delays++; });
            Assert(result.Success && !backend.LastTarget, "The same backend must disable the selected adapter.");
            Assert(backend.State == false && backend.Reads == 2, "Deactivation must also be verified.");
        }

        private static void FailureAndUnconfirmedStateAreReported()
        {
            AdapterIdentity identity = new AdapterIdentity(4, "Wi-Fi", TestGuid, "1");
            FakeBackend denied = new FakeBackend();
            denied.Failure = "Accès refusé";
            OpResult result = AdapterControl.SetEnabled(identity, true, denied, delegate(int ms) { });
            Assert(!result.Success && result.Message.IndexOf("Accès refusé", StringComparison.Ordinal) >= 0, "Windows errors must be shown inside the application.");
            Assert(denied.Requests == 1 && denied.Reads == 0, "A rejected request must not be reported as success.");

            FakeBackend stale = new FakeBackend();
            stale.State = false;
            int delays = 0;
            result = AdapterControl.SetEnabled(identity, true, stale, delegate(int ms) { delays++; });
            Assert(!result.Success && result.Unconfirmed && result.Message.IndexOf("confirmé", StringComparison.Ordinal) >= 0, "An unconfirmed state must be reported as pending, not a definite failure.");
            Assert(stale.Reads == 20 && delays == 19, "The state must be retried before reporting failure.");
        }

        private static void UnusableIdentityIsRejected()
        {
            AdapterIdentity identity = new AdapterIdentity(-1, "Wi-Fi", "", "");
            FakeBackend backend = new FakeBackend();
            OpResult result = AdapterControl.SetEnabled(identity, true, backend, delegate(int ms) { });
            Assert(!result.Success && backend.Requests == 0, "An unidentifiable adapter must never be changed.");
        }

        private static void DisabledAndInvalidSpeedsAreHidden()
        {
            AdapterInfo info = new AdapterInfo();
            info.Wireless = true;
            info.Enabled = false;
            info.Speed = long.MaxValue;
            Assert(info.MetaLabel == "Wi-Fi", "Disabled cards must not show an invalid link speed.");
            info.Enabled = true;
            Assert(info.MetaLabel == "Wi-Fi", "The invalid sentinel must not be displayed when enabled either.");
            info.Speed = 1000000000L;
            Assert(info.SpeedLabel.IndexOf("Gbit/s", StringComparison.Ordinal) >= 0, "A real link speed must remain visible.");
            Assert(IpUtil.FormatMac("70A8D31AB166") == "70:A8:D3:1A:B1:66", "Modern WMI MAC addresses must remain readable.");
        }

        private static void OutputEncodingIsDetected()
        {
            Encoding oem = Encoding.GetEncoding(850);
            string modern = "État réseau n’existe pas";
            string legacy = "Etat du réseau";
            Assert(NetworkService.DecodeToolOutput(Encoding.UTF8.GetBytes(modern), oem) == modern, "UTF-8 Windows output must retain accents.");
            Assert(NetworkService.DecodeToolOutput(oem.GetBytes(legacy), oem) == legacy, "Legacy OEM output must retain accents.");

            byte[] body = Encoding.Unicode.GetBytes(modern);
            byte[] utf16 = new byte[body.Length + 2];
            utf16[0] = 0xFF;
            utf16[1] = 0xFE;
            Array.Copy(body, 0, utf16, 2, body.Length);
            Assert(NetworkService.DecodeToolOutput(utf16, oem) == modern, "UTF-16 output with a BOM must be decoded correctly.");
        }

        private static void OptionalReadOnlyWmiProbe()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("EASY_IP_SWITCH_TEST_WMI"), "1", StringComparison.Ordinal))
            {
                return;
            }

            List<AdapterInfo> adapters = NetworkService.GetAdapters();
            for (int i = 0; i < adapters.Count; i++)
            {
                AdapterInfo info = adapters[i];
                Guid guid;
                if (!info.EnabledKnown || !info.Enabled || !Guid.TryParse(info.InterfaceGuid, out guid) || guid == Guid.Empty)
                {
                    continue;
                }

                AdapterIdentity identity = new AdapterIdentity(info.Index, info.Name, info.InterfaceGuid, info.DeviceId);
                bool? modern = AdapterStatus.ReadModernEnabled(identity);
                if (modern.HasValue)
                {
                    Assert(modern.Value, "Modern WMI must report the real enabled adapter as up.");
                    Assert(info.EnabledKnown && info.Enabled == modern.Value, "Displayed adapter state must match modern WMI.");
                }
                bool? state = new WmiAdapterControlBackend().ReadEnabled(identity);
                Assert(state.HasValue && state.Value, "Read-only WMI lookup must find the real enabled adapter by GUID.");
                Console.WriteLine("Read-only WMI adapter probe passed.");
                return;
            }
            Console.WriteLine("Read-only WMI adapter probe skipped: no suitable enabled adapter.");
        }

        private static void Assert(bool condition, string message)
        {
            _assertions++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeBackend : IAdapterControlBackend
        {
            public bool? State;
            public bool ApplyRequest;
            public string Failure = "";
            public int Requests;
            public int Reads;
            public bool LastTarget;
            public AdapterIdentity LastIdentity;

            public OpResult Request(AdapterIdentity identity, bool enabled)
            {
                Requests++;
                LastIdentity = identity;
                LastTarget = enabled;
                if (Failure.Length > 0)
                {
                    return OpResult.Fail(Failure);
                }
                if (ApplyRequest)
                {
                    State = enabled;
                }
                return OpResult.Ok("");
            }

            public bool? ReadEnabled(AdapterIdentity identity)
            {
                Reads++;
                return State;
            }
        }
    }
}
