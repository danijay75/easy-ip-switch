import AppKit
import SwiftUI

struct TermsView: View {
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    let onChoice: (Bool) -> Void

    private var palette: Palette { theme.palette }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            header

            Text(language.t("Veuillez lire et accepter les présentes conditions pour utiliser Easy IP-Switch."))
                .font(.system(size: 12.5))
                .foregroundColor(palette.textMuted)
                .fixedSize(horizontal: false, vertical: true)

            agreement

            footer
        }
        .padding(26)
        .frame(width: 760, height: 600, alignment: .topLeading)
        .background(palette.surface)
        .preferredColorScheme(theme.isDark ? .dark : .light)
        .tint(palette.accent)
        .onExitCommand { onChoice(false) }
    }

    private var header: some View {
        HStack(spacing: 12) {
            ZStack {
                RoundedRectangle(cornerRadius: 9, style: .continuous)
                    .fill(palette.accentSoft)
                Text("§")
                    .font(.system(size: 19, weight: .bold))
                    .foregroundColor(palette.accent)
            }
            .frame(width: 34, height: 34)

            Text(language.t("Conditions d'utilisation et RGPD"))
                .font(.system(size: 17, weight: .bold))
                .foregroundColor(palette.text)
                .fixedSize(horizontal: false, vertical: true)

            Spacer(minLength: 12)

            LanguageSelectorView()
        }
    }

    private var agreement: some View {
        ScrollView {
            Text(TermsContent.text(for: language.current))
                .font(.system(size: 12.5))
                .foregroundColor(palette.text)
                .lineSpacing(3)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .topLeading)
                .padding(14)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(palette.surfaceMuted)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(palette.border, lineWidth: 1)
        )
        .accessibilityLabel(language.t("Conditions d'utilisation et RGPD"))
    }

    private var footer: some View {
        VStack(alignment: .leading, spacing: 14) {
            Rectangle()
                .fill(palette.border)
                .frame(height: 1)

            HStack(spacing: 10) {
                Text(language.t("100 % local · Aucune donnée personnelle collectée"))
                    .font(.system(size: 11.5))
                    .foregroundColor(palette.textMuted)
                    .lineLimit(1)
                    .truncationMode(.tail)

                Spacer(minLength: 12)

                FlatButton(language.t("Je refuse"), kind: .secondary) {
                    onChoice(false)
                }
                .modifier(CancelShortcut())
                .help(language.t("Fermer l'application sans accepter les conditions"))

                defaultShortcut(
                    FlatButton(language.t("J'accepte"), kind: .primary) {
                        onChoice(true)
                    },
                    enabled: true
                )
                .help(language.t("Mémoriser votre accord et ouvrir l'application"))
            }
        }
    }

    @ViewBuilder
    private func defaultShortcut<Content: View>(_ content: Content, enabled: Bool) -> some View {
        if enabled, #available(macOS 12.3, *) {
            content.keyboardShortcut(.defaultAction)
        } else {
            content
        }
    }
}

/// Escape declines the terms. `onExitCommand` alone is not enough because the
/// scroll view may keep the focus; a registered cancel shortcut always fires.
private struct CancelShortcut: ViewModifier {
    func body(content: Content) -> some View {
        if #available(macOS 12.3, *) {
            content.keyboardShortcut(.cancelAction)
        } else {
            content
        }
    }
}

@MainActor
final class TermsWindowController: NSWindowController, NSWindowDelegate {
    private let onChoice: (Bool) -> Void
    private var choiceMade = false

    init(theme: ThemeState, language: LanguageStore, onChoice: @escaping (Bool) -> Void) {
        self.onChoice = onChoice

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 760, height: 600),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = AppInfo.productName + " " + AppInfo.versionLabel
        window.isReleasedWhenClosed = false

        super.init(window: window)

        let view = TermsView { [weak self] accepted in
            self?.finish(accepted)
        }
        .environmentObject(theme)
        .environmentObject(language)

        let hosting = NSHostingView(rootView: view)
        hosting.frame = NSRect(x: 0, y: 0, width: 760, height: 600)
        window.contentView = hosting
        window.setContentSize(NSSize(width: 760, height: 600))
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

    private func finish(_ accepted: Bool) {
        guard !choiceMade else { return }
        choiceMade = true
        window?.close()
        onChoice(accepted)
    }

    func windowWillClose(_ notification: Notification) {
        guard !choiceMade else { return }
        // Closing the window behaves like the Windows cancel button: the
        // application exits without remembering a consent.
        finish(false)
    }
}
