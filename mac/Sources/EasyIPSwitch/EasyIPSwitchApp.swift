import SwiftUI
import AppKit

@main
struct EasyIPSwitchApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var theme: ThemeState
    @StateObject private var model: AppModel
    @StateObject private var language: LanguageStore

    init() {
        let theme = ThemeState()
        _theme = StateObject(wrappedValue: theme)
        _model = StateObject(wrappedValue: AppModel())
        _language = StateObject(wrappedValue: LanguageStore.shared)
    }

    var body: some Scene {
        WindowGroup(AppInfo.productName + " " + AppInfo.versionLabel) {
            ContentView()
                .environmentObject(model)
                .environmentObject(theme)
                .environmentObject(language)
                .frame(minWidth: 880, minHeight: 620)
                .onAppear {
                    appDelegate.model = model
                    appDelegate.theme = theme
                    appDelegate.language = language
                    appDelegate.setupWindowIfNeeded()
                }
        }
        .commands {
            CommandGroup(replacing: .newItem) { }

            CommandGroup(after: .appInfo) {
                Button(L.t("Actualiser les cartes réseau")) {
                    Task { await model.refresh(manual: true) }
                }
                .keyboardShortcut("r", modifiers: .command)
            }
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var model: AppModel?
    weak var theme: ThemeState?
    weak var language: LanguageStore?

    private var didSetupWindow = false
    private var quitDialog: QuitDialogController?
    private var termsWindow: TermsWindowController?
    private var termsPending = false
    private var termsPresented = false
    private var termsDeclined = false
    private var terminationPending = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        _ = NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        setupWindowIfNeeded()
    }

    func setupWindowIfNeeded() {
        guard !didSetupWindow else { return }
        didSetupWindow = true

        Task { @MainActor in
            guard let window = NSApp.windows.first(where: {
                $0.isVisible && $0.canBecomeMain && !($0 is NSPanel)
            }) else {
                self.didSetupWindow = false
                return
            }

            let screenFrame = (window.screen ?? NSScreen.main)?.visibleFrame
                ?? NSRect(x: 0, y: 0, width: 1440, height: 900)
            let width = min(1120, max(880, screenFrame.width - 60))
            let height = min(880, max(620, screenFrame.height - 60))

            window.setContentSize(NSSize(width: width, height: height))
            window.minSize = NSSize(width: 880, height: 620)
            window.title = AppInfo.productName + " " + AppInfo.versionLabel
            window.center()

            self.presentTermsIfNeeded(for: window)
        }
    }

    /// Presents the Terms of Use and GDPR notice before the main window when
    /// the user has not accepted it yet. Declining quits the application
    /// without remembering the refusal, so it is asked again next time.
    private func presentTermsIfNeeded(for mainWindow: NSWindow) {
        guard !termsPresented else { return }
        guard !UserDefaultsTermsConsentStore().isAccepted() else { return }

        termsPresented = true
        termsPending = true
        mainWindow.orderOut(nil)

        let controller = TermsWindowController(
            theme: theme ?? ThemeState(),
            language: language ?? LanguageStore.shared
        ) { [weak self] accepted in
            guard let self else { return }
            self.termsWindow = nil
            self.termsPending = false
            UserDefaultsTermsConsentStore().saveAccepted(accepted)

            if accepted {
                mainWindow.makeKeyAndOrderFront(nil)
                NSApp.activate(ignoringOtherApps: true)
            } else {
                self.termsDeclined = true
                NSApp.terminate(nil)
            }
        }

        termsWindow = controller
        controller.show()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // While the consent window is closing, `finish` already decides whether
        // to open the main window or to quit.
        return !termsPending
    }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        // The consent gate must be able to close the application immediately,
        // and quitting during the terms must not open the donation dialog.
        if termsDeclined || termsPending {
            return .terminateNow
        }

        // Never block a logout, restart or shutdown with the donation dialog.
        if isSystemShutdown() {
            return .terminateNow
        }

        guard let model = model else {
            return .terminateNow
        }

        guard !terminationPending else {
            return .terminateCancel
        }

        let dirtyCount = model.cards.filter { $0.isDirty }.count
        let hasPendingChanges = dirtyCount > 0
        let title = L.t("Quitter Easy IP-Switch ?")
        let message: String
        let primaryText: String
        let secondaryText: String?

        if hasPendingChanges {
            let introduction = dirtyCount == 1
                ? L.t("Une carte réseau a été modifiée mais la nouvelle configuration n'a pas encore été appliquée.")
                : L.f("{0} cartes réseau ont été modifiées mais leurs nouvelles configurations n'ont pas encore été appliquées.", dirtyCount)
            let guidance = L.t("Vous pouvez appliquer ces changements avant de fermer, quitter sans les appliquer (les saisies seront perdues) ou rester dans l'application.")
            message = introduction + "\n\n" + guidance
            primaryText = L.t("Appliquer et quitter")
            secondaryText = L.t("Quitter sans appliquer")
        } else {
            message = L.t("Aucune modification n'est en attente.")
                + "\n\n"
                + L.t("Merci d'utiliser Easy IP-Switch. Avant de fermer, vous pouvez soutenir le projet : scannez le QR code ou ouvrez la page de don.")
            primaryText = L.t("Quitter")
            secondaryText = nil
        }

        terminationPending = true

        let dialog = QuitDialogController(
            theme: theme ?? ThemeState(),
            language: language ?? LanguageStore.shared,
            title: title,
            message: message,
            primaryText: primaryText,
            secondaryText: secondaryText,
            stayText: L.t("Rester"),
            hasPendingChanges: hasPendingChanges
        ) { [weak self] choice in
            guard let self = self else { return }
            self.quitDialog = nil

            switch choice {
            case .stay:
                self.terminationPending = false
                NSApp.reply(toApplicationShouldTerminate: false)
                // The last window may already be closed; keep the app visible
                // and usable when the user chooses to stay.
                self.showMainWindow()

            case .secondary:
                self.terminationPending = false
                NSApp.reply(toApplicationShouldTerminate: true)

            case .primary:
                if hasPendingChanges {
                    Task { @MainActor in
                        let success = await model.applyAllPending()
                        self.terminationPending = false
                        NSApp.reply(toApplicationShouldTerminate: success)
                        if !success {
                            // Keep the app usable so the user can fix or cancel
                            // the pending change instead of leaving a
                            // window-less app.
                            self.showMainWindow()
                        }
                    }
                } else {
                    self.terminationPending = false
                    NSApp.reply(toApplicationShouldTerminate: true)
                }
            }
        }

        quitDialog = dialog
        dialog.show()
        return .terminateLater
    }

    /// Detects a logout, restart or shutdown so the donation dialog never
    /// delays the system. Matches the Windows behaviour for shutdown and
    /// task-manager termination.
    private func isSystemShutdown() -> Bool {
        guard let event = NSAppleEventManager.shared().currentAppleEvent,
              event.eventClass == AEEventClass(kCoreEventClass),
              event.eventID == AEEventID(kAEQuitApplication) else {
            return false
        }
        let keywords: [AEKeyword] = [kAELogOut, kAEReallyLogOut, kAEShutDown, kAERestart]
        return keywords.contains { event.attributeDescriptor(forKeyword: $0) != nil }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if termsPending {
            termsWindow?.window?.makeKeyAndOrderFront(nil)
            return true
        }
        if !flag {
            showMainWindow()
        }
        return true
    }

    private func showMainWindow() {
        guard let window = NSApp.windows.first(where: {
            $0.canBecomeMain && !($0 is NSPanel)
        }) else {
            return
        }
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}
