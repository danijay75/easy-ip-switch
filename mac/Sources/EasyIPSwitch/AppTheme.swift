import SwiftUI
import AppKit
import Combine

struct Palette {
    let bg: Color
    let surface: Color
    let surfaceMuted: Color
    let surfaceSunken: Color
    let border: Color
    let borderStrong: Color
    let text: Color
    let textMuted: Color
    let textFaint: Color
    let accent: Color
    let accentHover: Color
    let accentPressed: Color
    let accentSoft: Color
    let wifi: Color
    let wifiSoft: Color
    let success: Color
    let successSoft: Color
    let danger: Color
    let dangerSoft: Color
    let warning: Color
    let warningSoft: Color
}

enum AppTheme {
    static let light = Palette(
        bg: Color(hex: 0xEDF1F7),
        surface: .white,
        surfaceMuted: Color(hex: 0xF8FAFC),
        surfaceSunken: Color(hex: 0xF1F5F9),
        border: Color(hex: 0xE3E8EF),
        borderStrong: Color(hex: 0xC7D0DD),
        text: Color(hex: 0x111827),
        textMuted: Color(hex: 0x667085),
        textFaint: Color(hex: 0x98A2B3),
        accent: Color(hex: 0x2563EB),
        accentHover: Color(hex: 0x1D4ED8),
        accentPressed: Color(hex: 0x1E40AF),
        accentSoft: Color(hex: 0xDBEAFE),
        wifi: Color(hex: 0x7C3AED),
        wifiSoft: Color(hex: 0xF1EAFE),
        success: Color(hex: 0x15803D),
        successSoft: Color(hex: 0xDCFCE7),
        danger: Color(hex: 0xDC2626),
        dangerSoft: Color(hex: 0xFEE2E2),
        warning: Color(hex: 0xB45309),
        warningSoft: Color(hex: 0xFEF3C7)
    )

    static let dark = Palette(
        bg: Color(hex: 0x0B1220),
        surface: Color(hex: 0x141D2C),
        surfaceMuted: Color(hex: 0x1A2434),
        surfaceSunken: Color(hex: 0x1E293B),
        border: Color(hex: 0x263448),
        borderStrong: Color(hex: 0x3B4B63),
        text: Color(hex: 0xE8EEF8),
        textMuted: Color(hex: 0x9AA9BE),
        textFaint: Color(hex: 0x6B7A90),
        accent: Color(hex: 0x3B82F6),
        accentHover: Color(hex: 0x2F6FE0),
        accentPressed: Color(hex: 0x2563EB),
        accentSoft: Color(hex: 0x1B2E4F),
        wifi: Color(hex: 0xA78BFA),
        wifiSoft: Color(hex: 0x2A2050),
        success: Color(hex: 0x4ADE80),
        successSoft: Color(hex: 0x123524),
        danger: Color(hex: 0xF87171),
        dangerSoft: Color(hex: 0x3C1A1A),
        warning: Color(hex: 0xFBBF24),
        warningSoft: Color(hex: 0x3A2A12)
    )
}

@MainActor
final class ThemeState: ObservableObject {
    private static let defaultsKey = "EasyIPSwitch.isDark"

    @Published var isDark: Bool {
        didSet {
            UserDefaults.standard.set(isDark, forKey: Self.defaultsKey)
        }
    }

    init() {
        if let stored = UserDefaults.standard.object(forKey: Self.defaultsKey) as? Bool {
            isDark = stored
        } else {
            let appearance = NSApplication.shared.effectiveAppearance
            isDark = appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        }
    }

    var palette: Palette {
        isDark ? AppTheme.dark : AppTheme.light
    }
}

extension Color {
    init(hex: UInt32) {
        self.init(
            .sRGB,
            red: Double((hex >> 16) & 0xFF) / 255.0,
            green: Double((hex >> 8) & 0xFF) / 255.0,
            blue: Double(hex & 0xFF) / 255.0,
            opacity: 1.0
        )
    }
}
