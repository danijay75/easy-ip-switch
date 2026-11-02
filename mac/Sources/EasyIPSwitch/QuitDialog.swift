import AppKit
import Carbon
import SwiftUI

enum QuitChoice {
    case primary
    case secondary
    case stay
}

/// Loads the donation QR code from the application bundle. During development
/// with `swift run` the bundle has no resources, so the source `assets` folder
/// is used as a fallback.
enum DonationQR {
    static let image: NSImage? = load()

    private static func load() -> NSImage? {
        if let url = Bundle.main.url(forResource: "donation-qr", withExtension: "png"),
           let image = NSImage(contentsOf: url) {
            return image
        }

        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let candidates = [
            current.appendingPathComponent("assets/donation-qr.png"),
            current.appendingPathComponent("mac/assets/donation-qr.png")
        ]
        for url in candidates {
            if let image = NSImage(contentsOf: url) {
                return image
            }
        }
        return nil
    }
}

struct QuitDialogView: View {
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    let title: String
    let message: String
    let primaryText: String
    let secondaryText: String?
    let stayText: String
    let hasPendingChanges: Bool
    let onChoice: (QuitChoice) -> Void

    private var palette: Palette { theme.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            header

            HStack(alignment: .top, spacing: 24) {
                Text(message)
                    .font(.system(size: 12.5))
                    .foregroundColor(palette.textMuted)
                    .lineSpacing(2)
                    .fixedSize(horizontal: false, vertical: true)
                    .frame(maxWidth: .infinity, alignment: .topLeading)

                donationPanel
                    .frame(width: 250)
            }

            Rectangle()
                .fill(palette.border)
                .frame(height: 1)

            buttons
        }
        .padding(26)
        .frame(width: 760, height: 440, alignment: .topLeading)
        .background(palette.surface)
        .preferredColorScheme(theme.isDark ? .dark : .light)
        .tint(palette.accent)
        .onExitCommand { onChoice(.stay) }
    }

    private var header: some View {
        HStack(spacing: 12) {
            ZStack {
                RoundedRectangle(cornerRadius: 9, style: .continuous)
                    .fill(hasPendingChanges ? palette.warningSoft : palette.accentSoft)
                Text(hasPendingChanges ? "!" : "♥")
                    .font(.system(size: hasPendingChanges ? 20 : 18, weight: .bold))
                    .foregroundColor(hasPendingChanges ? palette.warning : palette.accent)
            }
            .frame(width: 34, height: 34)

            Text(title)
                .font(.system(size: 17, weight: .bold))
                .foregroundColor(palette.text)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private var donationPanel: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(language.t("Soutenir Easy IP-Switch"))
                .font(.system(size: 12, weight: .semibold))
                .foregroundColor(palette.text)

            Text(donationHint)
                .font(.system(size: 11.5))
                .foregroundColor(palette.textMuted)
                .fixedSize(horizontal: false, vertical: true)

            if let qr = DonationQR.image {
                Button {
                    openDonation()
                } label: {
                    Image(nsImage: qr)
                        .resizable()
                        .interpolation(.none)
                        .scaledToFit()
                        .frame(width: 170, height: 170)
                        .padding(6)
                        .background(Color.white)
                        .clipShape(RoundedRectangle(cornerRadius: 4, style: .continuous))
                        .overlay(
                            RoundedRectangle(cornerRadius: 4, style: .continuous)
                                .stroke(palette.border, lineWidth: 1)
                        )
                }
                .buttonStyle(.plain)
                .help(language.t("Ouvrir la page de don PayPal"))
                .accessibilityLabel(language.t("QR code pour faire un don"))
            }

            Button(language.t("Ouvrir la page de don")) {
                openDonation()
            }
            .buttonStyle(.plain)
            .font(.system(size: 12, weight: .semibold))
            .foregroundColor(palette.accent)
            .help(AppInfo.donationURL.absoluteString)
        }
    }

    private var donationHint: String {
        if DonationQR.image != nil {
            return language.t("Scannez ce QR code ou cliquez dessus pour faire un don via PayPal.")
        }
        return language.t("Ouvrez la page de don ci-dessous pour soutenir le projet.")
    }

    private var buttons: some View {
        HStack(spacing: 10) {
            Spacer(minLength: 0)

            defaultShortcut(
                FlatButton(stayText, kind: .ghost) {
                    onChoice(.stay)
                },
                enabled: secondaryText != nil
            )

            if let secondaryText {
                FlatButton(secondaryText, kind: .secondary) {
                    onChoice(.secondary)
                }
            }

            defaultShortcut(
                FlatButton(primaryText, kind: .primary) {
                    onChoice(.primary)
                },
                enabled: secondaryText == nil
            )
        }
    }

    @ViewBuilder
    private func defaultShortcut<Content: View>(_ content: Content, enabled: Bool) -> some View {
        if enabled {
            if #available(macOS 12.3, *) {
                content.keyboardShortcut(.defaultAction)
            } else {
                content
            }
        } else {
            content
        }
    }

    private func openDonation() {
        NSWorkspace.shared.open(AppInfo.donationURL)
    }
}

@MainActor
final class QuitDialogController: NSWindowController, NSWindowDelegate {
    private let onChoice: (QuitChoice) -> Void
    private var choiceMade = false

    init(
        theme: ThemeState,
        language: LanguageStore,
        title: String,
        message: String,
        primaryText: String,
        secondaryText: String?,
        stayText: String,
        hasPendingChanges: Bool,
        onChoice: @escaping (QuitChoice) -> Void
    ) {
        self.onChoice = onChoice

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 760, height: 440),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = AppInfo.productName
        window.isReleasedWhenClosed = false

        super.init(window: window)

        let view = QuitDialogView(
            title: title,
            message: message,
            primaryText: primaryText,
            secondaryText: secondaryText,
            stayText: stayText,
            hasPendingChanges: hasPendingChanges,
            onChoice: { [weak self] choice in
                self?.finish(choice)
            }
        )
        .environmentObject(theme)
        .environmentObject(language)

        let hosting = NSHostingView(rootView: view)
        hosting.frame = NSRect(x: 0, y: 0, width: 760, height: 440)
        window.contentView = hosting
        window.setContentSize(NSSize(width: 760, height: 440))
        window.delegate = self
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func show() {
        guard let window = window else { return }
        window.center()
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    private func finish(_ choice: QuitChoice) {
        guard !choiceMade else { return }
        choiceMade = true
        window?.close()
        onChoice(choice)
    }

    func windowWillClose(_ notification: Notification) {
        guard !choiceMade else { return }
        finish(.stay)
    }
}
