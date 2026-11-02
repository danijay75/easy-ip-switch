import Foundation

enum NetworkWriter {
    static func setDHCP(service: String) async -> CommandResult {
        return await runPrivileged(["-setdhcp", service])
    }

    static func setManual(service: String, ip: String, mask: String, router: String) async -> CommandResult {
        let result = await runPrivileged(["-setmanual", service, ip, mask, router])

        // Leaving the gateway empty is supported by the Windows app. Some
        // macOS versions reject an empty router argument; in that case retry
        // with 0.0.0.0, which SystemConfiguration treats as "no router".
        guard result.status != 0, router.isEmpty else { return result }
        let diagnostic = result.combined.lowercased()
        let rejectedArgument = diagnostic.contains("parameter")
            || diagnostic.contains("usage")
            || diagnostic.contains("invalid")
            || diagnostic.contains("router")
            || diagnostic.contains("gateway")
            || diagnostic.contains("empty")
        guard rejectedArgument else { return result }

        return await runPrivileged(["-setmanual", service, ip, mask, "0.0.0.0"])
    }

    static func setEnabled(service: String, enabled: Bool) async -> CommandResult {
        return await runPrivileged(["-setnetworkserviceenabled", service, enabled ? "on" : "off"])
    }

    static func friendlyMessage(_ result: CommandResult) -> String {
        let text = result.combined
        let lower = text.lowercased()

        if lower.contains("cancel") || lower.contains("annul") || lower.contains("-128") {
            return "Opération annulée : aucune modification n'a été appliquée."
        }
        if lower.contains("not authorized") || lower.contains("authorization")
            || lower.contains("must be root") || lower.contains("administrator privileges") {
            return "Autorisation refusée : la configuration n'a pas été modifiée."
        }
        if text.isEmpty {
            return "La commande réseau a échoué (code \(result.status))."
        }
        return "Échec de la commande réseau : \(text)"
    }

    /// networksetup must run as root. Rather than relaunching the whole app,
    /// each change asks macOS for the administrator authorization, which keeps
    /// the read-only part of the app usable without privileges.
    private static func runPrivileged(_ arguments: [String]) async -> CommandResult {
        let command = (["/usr/sbin/networksetup"] + arguments)
            .map(Shell.quote)
            .joined(separator: " ")

        let escaped = command
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")

        let script = "do shell script \"\(escaped)\" with administrator privileges"
        return await Shell.runAsync("/usr/bin/osascript", ["-e", script])
    }
}
