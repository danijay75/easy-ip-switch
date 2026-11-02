import Foundation
import SystemConfiguration

/// Reads the macOS network services through SystemConfiguration.
/// The Windows app reads WMI; on macOS the equivalent live source is
/// SCPreferences, which does not depend on the system language.
struct NetworkReader {
    private let preferences: SCPreferences?
    private let store: SCDynamicStore?

    init() {
        preferences = SCPreferencesCreate(nil, "Easy IP-Switch" as CFString, nil)
        store = SCDynamicStoreCreate(nil, "Easy IP-Switch" as CFString, nil, nil)
    }

    func read() -> [AdapterInfo] {
        guard let preferences = preferences,
              let services = SCNetworkServiceCopyAll(preferences) as? [SCNetworkService] else {
            return []
        }

        var adapters: [AdapterInfo] = []
        for service in services {
            guard let interface = SCNetworkServiceGetInterface(service) else { continue }

            let name = (SCNetworkServiceGetName(service) as String?) ?? ""
            guard !name.isEmpty else { continue }

            let identifier = (SCNetworkServiceGetServiceID(service) as String?) ?? name
            let device = (SCNetworkInterfaceGetBSDName(interface) as String?) ?? ""
            let interfaceType = (SCNetworkInterfaceGetInterfaceType(interface) as String?) ?? ""

            guard isSupported(interfaceType: interfaceType, device: device) else { continue }

            let hardware = (SCNetworkInterfaceGetLocalizedDisplayName(interface) as String?)
                ?? (device.isEmpty ? "Réseau" : device)
            let kind: AdapterKind
            if interfaceType == (kSCNetworkInterfaceTypeIEEE80211 as String) {
                kind = .wifi
            } else if interfaceType == (kSCNetworkInterfaceTypeEthernet as String) {
                kind = .ethernet
            } else {
                kind = .other
            }

            let enabled = SCNetworkServiceGetEnabled(service)
            var dhcp = false
            var address = ""
            var mask = ""
            var router = ""

            if let ipv4 = SCNetworkServiceCopyProtocol(service, kSCNetworkProtocolTypeIPv4),
               let configuration = SCNetworkProtocolGetConfiguration(ipv4) as? [String: Any] {
                let method = (configuration[kSCPropNetIPv4ConfigMethod as String] as? String) ?? ""
                dhcp = method == (kSCValNetIPv4ConfigMethodDHCP as String)
                    || method == (kSCValNetIPv4ConfigMethodBOOTP as String)

                if let addresses = configuration[kSCPropNetIPv4Addresses as String] as? [String] {
                    address = addresses.first ?? ""
                }
                if let masks = configuration[kSCPropNetIPv4SubnetMasks as String] as? [String] {
                    mask = masks.first ?? ""
                }
                router = (configuration[kSCPropNetIPv4Router as String] as? String) ?? ""
            }

            // SCPreferences gives the saved configuration; for DHCP the actual
            // address lives in the dynamic store. Prefer the live values so the
            // card shows the address macOS is really using.
            if let live = liveIPv4(serviceID: identifier) {
                if let addresses = live[kSCPropNetIPv4Addresses as String] as? [String],
                   let first = addresses.first, !first.isEmpty {
                    address = first
                }
                if let masks = live[kSCPropNetIPv4SubnetMasks as String] as? [String],
                   let first = masks.first, !first.isEmpty {
                    mask = first
                }
                if let liveRouter = live[kSCPropNetIPv4Router as String] as? String,
                   !liveRouter.isEmpty {
                    router = liveRouter
                }
            }

            // macOS can publish an empty router as 0.0.0.0; show it as empty
            // so the optional gateway field stays consistent with Windows.
            if router == "0.0.0.0" {
                router = ""
            }

            let linkUp = enabled && interfaceIsActive(device)
            // Match Windows: the status reflects the link, not the presence of
            // an IPv4 address (a connected card can still be unconfigured).
            let connected = linkUp

            adapters.append(AdapterInfo(
                id: identifier,
                serviceName: name,
                device: device,
                hardwareName: hardware,
                kind: kind,
                enabled: enabled,
                connected: connected,
                dhcp: dhcp,
                ip: address,
                mask: mask,
                router: router
            ))
        }

        return adapters.sorted { first, second in
            if first.connected != second.connected { return first.connected && !second.connected }
            if first.enabled != second.enabled { return first.enabled && !second.enabled }
            return first.serviceName.localizedCaseInsensitiveCompare(second.serviceName) == .orderedAscending
        }
    }

    static func readAsync() async -> [AdapterInfo] {
        await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                continuation.resume(returning: NetworkReader().read())
            }
        }
    }

    /// Filters out VPN/tunnel services: they have no editable IPv4 adapter.
    private func isSupported(interfaceType: String, device: String) -> Bool {
        guard !device.isEmpty else { return false }

        let excludedPrefixes = ["utun", "ipsec", "ppp", "gif", "stf", "awdl", "llw", "p2p", "anpi", "ap"]
        if excludedPrefixes.contains(where: { device.hasPrefix($0) }) {
            return false
        }

        if interfaceType == (kSCNetworkInterfaceTypeEthernet as String)
            || interfaceType == (kSCNetworkInterfaceTypeIEEE80211 as String) {
            return true
        }

        // Bridge, VLAN and bond services also carry an editable IPv4 configuration.
        return interfaceType == "Bridge"
            || interfaceType == "VLAN"
            || interfaceType == "Bond"
    }

    private func liveIPv4(serviceID: String) -> [String: Any]? {
        guard let store = store else { return nil }
        let key = "State:/Network/Service/\(serviceID)/IPv4" as CFString
        guard let value = SCDynamicStoreCopyValue(store, key) else { return nil }
        return value as? [String: Any]
    }

    private func interfaceIsActive(_ device: String) -> Bool {
        guard !device.isEmpty else { return false }
        let result = Shell.run("/sbin/ifconfig", [device])
        guard result.status == 0 else { return false }
        if result.output.contains("status: active") { return true }
        if result.output.contains("status: inactive") { return false }
        // Bridges and some virtual interfaces do not publish a link status;
        // an enabled service is then treated as active.
        return true
    }
}
