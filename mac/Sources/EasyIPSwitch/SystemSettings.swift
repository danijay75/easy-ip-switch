import AppKit
import Foundation

enum SystemSettings {
    /// Opens the macOS Network settings. Ventura and later use the System
    /// Settings extension URL; Monterey falls back to the legacy prefPane.
    @MainActor
    static func openNetwork() {
        // macOS 13+ uses the Network Settings extension; Monterey still uses
        // the legacy preference pane identifier.
        let majorVersion = ProcessInfo.processInfo.operatingSystemVersion.majorVersion
        let identifier = majorVersion >= 13
            ? "x-apple.systempreferences:com.apple.Network-Settings.extension"
            : "x-apple.systempreferences:com.apple.preference.network"

        if let url = URL(string: identifier), NSWorkspace.shared.open(url) {
            return
        }

        let legacy = URL(fileURLWithPath: "/System/Library/PreferencePanes/Network.prefPane")
        _ = NSWorkspace.shared.open(legacy)
    }
}
