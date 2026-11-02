using System;
using System.Collections.Generic;
using System.Threading;

namespace EasyIpSwitch
{
    internal sealed class StaticCommandResult
    {
        public int ExitCode;
        public string Detail = "";
    }

    internal sealed class StaticIpv4Snapshot
    {
        public AdapterInfo Adapter;
        public SavedIpv4Configuration Saved;
    }

    internal interface IStaticIpv4Backend
    {
        StaticCommandResult SetStatic(AdapterIdentity identity, string ip, string mask, string gateway);
        StaticIpv4Snapshot Read(AdapterIdentity identity);
    }

    internal static class StaticIpv4Configuration
    {
        public static OpResult Apply(AdapterIdentity identity, string ip, string mask, string gateway)
        {
            return Apply(identity, ip, mask, gateway, new WindowsStaticIpv4Backend(),
                delegate(int milliseconds) { Thread.Sleep(milliseconds); });
        }

        internal static OpResult Apply(AdapterIdentity identity, string ip, string mask, string gateway,
            IStaticIpv4Backend backend, Action<int> delay)
        {
            if (identity == null || string.IsNullOrWhiteSpace(identity.Name))
            {
                return OpResult.Fail(L.T("Carte réseau introuvable. Actualisez la liste avant de réessayer."));
            }

            uint address;
            uint subnet;
            uint router;
            bool hasGateway = !string.IsNullOrWhiteSpace(gateway);
            if (!IpUtil.TryParse(ip, out address) || address == 0u || address == 0xFFFFFFFFu || IpUtil.IsApipa(address)
                || !IpUtil.TryParse(mask, out subnet) || !IpUtil.IsValidMask(subnet)
                || (hasGateway && (!IpUtil.TryParse(gateway, out router) || router == address
                    || (router & subnet) != (address & subnet))))
            {
                return OpResult.Fail(L.T("Configuration IPv4 invalide. Vérifiez l'adresse, le masque et la passerelle."));
            }

            string normalizedIp = IpUtil.Format(address);
            string normalizedMask = IpUtil.Format(subnet);
            string normalizedGateway = "";
            if (hasGateway)
            {
                IpUtil.TryParse(gateway, out router);
                normalizedGateway = IpUtil.Format(router);
            }

            StaticCommandResult command;
            try
            {
                // Une seule demande : ne jamais repasser en DHCP à titre de retry.
                command = backend.SetStatic(identity, normalizedIp, normalizedMask, normalizedGateway);
            }
            catch (Exception ex)
            {
                return OpResult.Awaiting(L.F("Impossible de confirmer la commande Windows : {0} Vérifiez l'état actuel avant de réessayer.",
                    Short(ex.Message)));
            }

            if (command == null)
            {
                return OpResult.Awaiting(L.T("Windows n'a pas confirmé la commande. Vérifiez l'état actuel avant de réessayer."));
            }
            if (command.ExitCode == -2)
            {
                return OpResult.Awaiting(L.T("Délai dépassé : la configuration a peut-être changé. Actualisez avant de réessayer."));
            }
            if (command.ExitCode != 0)
            {
                string detail = Short(command.Detail);
                if (detail.Length == 0)
                {
                    detail = L.F("Code Windows : {0}.", command.ExitCode);
                }
                return OpResult.Fail(L.F("Impossible d'appliquer l'adresse fixe. {0} Vérifiez l'état actuel avant de réessayer.", detail));
            }

            StaticIpv4Snapshot snapshot = null;
            string lastError = "";
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    snapshot = backend.Read(identity);
                    if (IsConfirmed(snapshot, normalizedIp, normalizedMask, normalizedGateway))
                    {
                        return OpResult.Ok(snapshot.Adapter.Connected
                            ? L.F("Adresse fixe appliquée : {0}", normalizedIp)
                            : L.F("Adresse fixe enregistrée : {0}. Carte non connectée ; vérifiez la liaison après branchement.", normalizedIp));
                    }
                }
                catch (Exception ex)
                {
                    lastError = Short(ex.Message);
                }
                if (i < 7)
                {
                    delay(350);
                }
            }

            if (snapshot != null && snapshot.Adapter != null)
            {
                bool activeDhcp = snapshot.Adapter.DhcpKnown && snapshot.Adapter.Dhcp;
                bool savedDhcp = snapshot.Saved != null && snapshot.Saved.DhcpEnabled == true;
                bool savedStaticMatches = snapshot.Saved != null && snapshot.Saved.DhcpEnabled == false
                    && snapshot.Saved.MatchesAddressAndMask(normalizedIp, normalizedMask)
                    && snapshot.Saved.MatchesGateway(normalizedGateway);
                bool savedValuesMatch = snapshot.Saved != null
                    && snapshot.Saved.MatchesAddressAndMask(normalizedIp, normalizedMask)
                    && snapshot.Saved.MatchesGateway(normalizedGateway);
                if (!snapshot.Adapter.Connected && savedValuesMatch && (activeDhcp || savedDhcp))
                {
                    string dhcp = activeDhcp ? L.T("DHCP encore actif") : L.T("DHCP encore enregistré");
                    return OpResult.Partial(L.F("Adresse fixe enregistrée : {0}. Carte non connectée ; {1}. Vérifiez après branchement.",
                        normalizedIp, dhcp));
                }
                if (activeDhcp && savedStaticMatches)
                {
                    return OpResult.Awaiting(L.T("Adresse fixe enregistrée, mais Windows indique encore DHCP actif. Actualisez et vérifiez la liaison avant de réessayer."));
                }
                if (activeDhcp || savedDhcp)
                {
                    string message = L.T("Configuration incomplète : Windows indique que le DHCP est encore actif ou enregistré.");
                    if (!snapshot.Adapter.Connected)
                    {
                        message += L.T(" Carte non connectée : branchez le câble, actualisez puis réessayez.");
                    }
                    else
                    {
                        message += L.T(" Actualisez et vérifiez la carte avant de réessayer.");
                    }
                    return OpResult.Fail(message);
                }

                if (snapshot.Saved != null && snapshot.Saved.DhcpEnabled == false
                    && (!snapshot.Saved.MatchesAddressAndMask(normalizedIp, normalizedMask)
                        || !snapshot.Saved.MatchesGateway(normalizedGateway)))
                {
                    return OpResult.Fail(L.T("Windows indique que les paramètres enregistrés ne correspondent pas à la demande. Actualisez avant de réessayer."));
                }
            }

            string unconfirmed = L.T("Demande envoyée, mais les paramètres IPv4 n'ont pas pu être entièrement confirmés. Actualisez avant de réessayer.");
            if (lastError.Length > 0)
            {
                unconfirmed += " " + lastError;
            }
            return OpResult.Awaiting(unconfirmed);
        }

        private static bool IsConfirmed(StaticIpv4Snapshot snapshot, string ip, string mask, string gateway)
        {
            if (snapshot == null || snapshot.Adapter == null || snapshot.Saved == null
                || !snapshot.Adapter.DhcpKnown || snapshot.Adapter.Dhcp
                || snapshot.Saved.DhcpEnabled != false
                || !snapshot.Saved.MatchesAddressAndMask(ip, mask)
                || !snapshot.Saved.MatchesGateway(gateway))
            {
                return false;
            }

            if (!snapshot.Adapter.Connected)
            {
                // Hors liaison, Windows peut laisser l'adresse en état tentative.
                // Les paramètres enregistrés sont confirmés, pas la connectivité.
                return true;
            }

            return SavedIpv4Configuration.SameIp(snapshot.Adapter.Ip, ip)
                && SavedIpv4Configuration.SameIp(snapshot.Adapter.Mask, mask)
                && (SavedIpv4Configuration.IsEmptyGateway(gateway)
                    ? SavedIpv4Configuration.IsEmptyGateway(snapshot.Adapter.Gateway)
                    : SavedIpv4Configuration.SameIp(snapshot.Adapter.Gateway, gateway));
        }

        private static string Short(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }
            string result = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return result.Length > 180 ? result.Substring(0, 180) + "…" : result;
        }
    }

    internal sealed class WindowsStaticIpv4Backend : IStaticIpv4Backend
    {
        public StaticCommandResult SetStatic(AdapterIdentity identity, string ip, string mask, string gateway)
        {
            int code;
            string detail = NetworkService.RunNetsh("interface ipv4 set address name=\"" + identity.Name + "\" "
                + NetworkService.BuildStaticArgs(ip, mask, gateway), out code);
            StaticCommandResult result = new StaticCommandResult();
            result.ExitCode = code;
            result.Detail = detail;
            return result;
        }

        public StaticIpv4Snapshot Read(AdapterIdentity identity)
        {
            List<AdapterInfo> adapters = NetworkService.GetAdapters();
            AdapterInfo found = null;
            for (int i = 0; i < adapters.Count; i++)
            {
                if (identity.Matches(adapters[i]))
                {
                    found = adapters[i];
                    break;
                }
            }

            StaticIpv4Snapshot snapshot = new StaticIpv4Snapshot();
            snapshot.Adapter = found;
            snapshot.Saved = SavedIpv4Configuration.Read(identity.InterfaceGuid);
            return snapshot;
        }
    }
}
