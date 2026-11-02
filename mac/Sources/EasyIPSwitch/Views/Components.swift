import SwiftUI

func color(for kind: MessageKind, in palette: Palette) -> Color {
    switch kind {
    case .success:
        return palette.success
    case .warning:
        return palette.warning
    case .error:
        return palette.danger
    case .info, .none:
        return palette.textMuted
    }
}

struct BrandLogo: View {
    var size: CGFloat = 40

    var body: some View {
        RoundedRectangle(cornerRadius: size * 0.24, style: .continuous)
            .fill(
                LinearGradient(
                    colors: [Color(hex: 0x3B82F6), Color(hex: 0x2563EB)],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
            )
            .frame(width: size, height: size)
            .overlay(
                Image(systemName: "arrow.left.arrow.right")
                    .font(.system(size: size * 0.44, weight: .bold))
                    .foregroundColor(.white)
            )
            .shadow(color: Color(hex: 0x2563EB).opacity(0.35), radius: 8, x: 0, y: 3)
    }
}

enum ButtonKind {
    case primary
    case secondary
    case ghost
}

struct FlatButton: View {
    @EnvironmentObject private var theme: ThemeState

    private let title: String
    private let icon: String?
    private let kind: ButtonKind
    private let enabled: Bool
    private let action: () -> Void

    @State private var hovering = false

    init(
        _ title: String,
        icon: String? = nil,
        kind: ButtonKind = .secondary,
        enabled: Bool = true,
        action: @escaping () -> Void
    ) {
        self.title = title
        self.icon = icon
        self.kind = kind
        self.enabled = enabled
        self.action = action
    }

    var body: some View {
        Button(action: action) {
            HStack(spacing: 7) {
                if let icon = icon {
                    Image(systemName: icon)
                        .font(.system(size: 12, weight: .semibold))
                }
                Text(title)
                    .font(.system(size: 13, weight: .semibold))
            }
            .padding(.horizontal, 14)
            .frame(height: 34)
            .foregroundColor(foregroundColor)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(backgroundColor)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .stroke(borderColor, lineWidth: kind == .secondary ? 1 : 0)
            )
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .opacity(enabled ? 1 : 0.45)
        .onHover { hovering = $0 }
    }

    private var palette: Palette { theme.palette }

    private var foregroundColor: Color {
        switch kind {
        case .primary: return .white
        case .secondary: return palette.text
        case .ghost: return palette.textMuted
        }
    }

    private var backgroundColor: Color {
        switch kind {
        case .primary:
            return hovering ? palette.accentHover : palette.accent
        case .secondary:
            return hovering ? palette.surfaceMuted : palette.surface
        case .ghost:
            return hovering ? palette.surfaceMuted : Color.clear
        }
    }

    private var borderColor: Color {
        guard kind == .secondary else { return Color.clear }
        return hovering ? palette.borderStrong : palette.border
    }
}

struct ThemeSwitch: View {
    @EnvironmentObject private var theme: ThemeState

    var body: some View {
        HStack(spacing: 2) {
            segment(icon: "sun.max.fill", selected: !theme.isDark) {
                theme.isDark = false
            }
            segment(icon: "moon.fill", selected: theme.isDark) {
                theme.isDark = true
            }
        }
        .padding(3)
        .background(Capsule().fill(theme.palette.surfaceSunken))
        .overlay(Capsule().stroke(theme.palette.border, lineWidth: 1))
        .help(L.t("Basculer entre le thème clair et sombre"))
    }

    private func segment(icon: String, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: icon)
                .font(.system(size: 12.5, weight: .semibold))
                .foregroundColor(selected ? theme.palette.text : theme.palette.textMuted)
                .frame(width: 34, height: 26)
                .background(Capsule().fill(selected ? theme.palette.surface : Color.clear))
        }
        .buttonStyle(.plain)
    }
}

struct StatusPill: View {
    let icon: String
    let text: String
    let tint: Color
    let soft: Color

    var body: some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 11, weight: .semibold))
            Text(text)
                .font(.system(size: 12, weight: .semibold))
                .lineLimit(1)
        }
        .foregroundColor(tint)
        .padding(.horizontal, 12)
        .frame(height: 28)
        .background(Capsule().fill(soft))
        .overlay(Capsule().stroke(tint.opacity(0.25), lineWidth: 1))
    }
}

struct LabeledField: View {
    @EnvironmentObject private var theme: ThemeState

    let label: String
    @Binding var text: String
    var placeholder: String
    var invalid: Bool
    var enabled: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(label)
                .font(.system(size: 12))
                .foregroundColor(theme.palette.textMuted)

            TextField(placeholder, text: filteredBinding)
                .textFieldStyle(.plain)
                .font(.system(size: 13, design: .monospaced))
                .foregroundColor(theme.palette.text)
                .padding(.horizontal, 12)
                .frame(height: 36)
                .background(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(theme.palette.surfaceSunken)
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .stroke(
                            invalid ? theme.palette.danger : theme.palette.border,
                            lineWidth: invalid ? 1.6 : 1
                        )
                )
                .shadow(color: invalid ? theme.palette.danger.opacity(0.18) : Color.clear, radius: 4)
                .disabled(!enabled)
                .opacity(enabled ? 1 : 0.6)
                .animation(.easeInOut(duration: 0.18), value: invalid)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var filteredBinding: Binding<String> {
        Binding(
            get: { text },
            set: { newValue in
                var allowed = newValue.filter { $0.isNumber || $0 == "." }
                if allowed.count > 15 {
                    allowed = String(allowed.prefix(15))
                }
                text = allowed
            }
        )
    }
}

struct ModeSegment: View {
    @EnvironmentObject private var theme: ThemeState

    @Binding var mode: ConfigMode
    var enabled: Bool

    var body: some View {
        HStack(spacing: 4) {
            item("DHCP", value: .dhcp)
            item(L.t("Adresse fixe"), value: .manual)
        }
        .padding(4)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(theme.palette.surfaceSunken)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(theme.palette.border, lineWidth: 1)
        )
        .opacity(enabled ? 1 : 0.55)
    }

    private func item(_ title: String, value: ConfigMode) -> some View {
        Button {
            withAnimation(.easeInOut(duration: 0.15)) {
                mode = value
            }
        } label: {
            Text(title)
                .font(.system(size: 12.5, weight: mode == value ? .semibold : .regular))
                .foregroundColor(mode == value ? theme.palette.text : theme.palette.textMuted)
                .frame(width: value == .dhcp ? 78 : 108, height: 28)
                .background(
                    RoundedRectangle(cornerRadius: 7, style: .continuous)
                        .fill(mode == value ? theme.palette.surface : Color.clear)
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 7, style: .continuous)
                        .stroke(mode == value ? theme.palette.borderStrong : Color.clear, lineWidth: 1)
                )
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }
}

struct CardSwitch: View {
    @EnvironmentObject private var theme: ThemeState

    let isOn: Bool
    let enabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            ZStack(alignment: isOn ? .trailing : .leading) {
                Capsule()
                    .fill(isOn ? theme.palette.accent : theme.palette.surfaceSunken)
                    .frame(width: 42, height: 24)
                    .overlay(
                        Capsule().stroke(theme.palette.border, lineWidth: isOn ? 0 : 1)
                    )

                Circle()
                    .fill(Color.white)
                    .frame(width: 18, height: 18)
                    .padding(3)
                    .shadow(color: Color.black.opacity(0.18), radius: 1.5, x: 0, y: 1)
            }
            .frame(width: 42, height: 24)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .opacity(enabled ? 1 : 0.55)
        .animation(.easeInOut(duration: 0.16), value: isOn)
        .help(Text(isOn ? L.t("Désactiver le service réseau") : L.t("Activer le service réseau")))
    }
}

struct Hairline: View {
    let color: Color

    var body: some View {
        Rectangle()
            .fill(color)
            .frame(height: 1)
    }
}
