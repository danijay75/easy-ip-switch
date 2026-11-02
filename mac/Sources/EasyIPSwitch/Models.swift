import Foundation
import SwiftUI

enum ConfigMode {
    case dhcp
    case manual
}

enum AdapterKind {
    case wifi
    case ethernet
    case other
}

struct AdapterInfo: Identifiable {
    let id: String
    let serviceName: String
    let device: String
    let hardwareName: String
    let kind: AdapterKind
    let enabled: Bool
    let connected: Bool
    let dhcp: Bool
    let ip: String
    let mask: String
    let router: String

    var metaLabel: String {
        if device.isEmpty {
            return hardwareName
        }
        return "\(hardwareName) — \(device)"
    }

    var statusLabel: String {
        if !enabled {
            return L.t("Désactivée")
        }
        return connected ? L.t("Connectée") : L.t("Non connectée")
    }

    var summaryText: String {
        if !enabled {
            return L.t("carte désactivée")
        }
        if ip.isEmpty {
            return dhcp ? L.t("DHCP actif, aucune adresse") : L.t("adresse fixe, aucune adresse")
        }
        if dhcp {
            return L.f("DHCP actif, {0}", ip)
        }
        return L.f("adresse fixe {0}/{1}", ip, mask)
    }
}

enum MessageKind {
    case none
    case info
    case success
    case warning
    case error
}

enum MainTab {
    case cards
    case ping
}

struct CardMessage {
    let text: String
    let kind: MessageKind
}

struct CommandResult {
    let status: Int32
    let output: String
    let error: String

    var combined: String {
        [error, output]
            .filter { !$0.isEmpty }
            .joined(separator: "\n")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }
}

struct PingOutcome {
    let success: Bool
    let detail: String
    let raw: String
}
