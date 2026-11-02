import SwiftUI
import AppKit

@main
struct EasyIPSwitchApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var theme: ThemeState
    @StateObject private var model: AppModel

    init() {
        let theme = ThemeState()
        _theme = StateObject(wrappedValue: theme)
        _model = StateObject(wrappedValue: AppModel())
    }

    var body: some Scene {
        WindowGroup(AppInfo.productName + " " + AppInfo.versionLabel) {
            ContentView()
                .environmentObject(model)
                .environmentObject(theme)
                .frame(minWidth: 880, minHeight: 620)
                .onAppear {
                    appDelegate.model = model
                    appDelegate.theme = theme
                    appDelegate.setupWindowIfNeeded()
                }
        }
        .commands {
            CommandGroup(replacing: .newItem) { }

            CommandGroup(after: .appInfo) {
                Button("Actualiser les cartes réseau") {
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

    private var didSetupWindow = false
    private var quitDialog: QuitDialogController?
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
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return true
    }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
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
        let title = "Quitter Easy IP-Switch ?"
        let message: String
        let primaryText: String
        let secondaryText: String?

        if hasPendingChanges {
            if dirtyCount == 1 {
                message = """
                Une carte réseau a été modifiée mais la nouvelle configuration n'a pas encore été appliquée.

                Vous pouvez appliquer ces changements avant de fermer, quitter sans les appliquer \
                (les saisies seront perdues) ou rester dans l'application.
                """
            } else {
                message = """
                \(dirtyCount) cartes réseau ont été modifiées mais leurs nouvelles configurations n'ont pas encore été appliquées.

                Vous pouvez appliquer ces changements avant de fermer, quitter sans les appliquer \
                (les saisies seront perdues) ou rester dans l'application.
                """
            }
            primaryText = "Appliquer et quitter"
            secondaryText = "Quitter sans appliquer"
        } else {
            message = """
            Aucune modification n'est en attente.

            Merci d'utiliser Easy IP-Switch. Avant de fermer, vous pouvez soutenir le projet : \
            scannez le QR code ou ouvrez la page de don.
            """
            primaryText = "Quitter"
            secondaryText = nil
        }

        terminationPending = true

        let dialog = QuitDialogController(
            theme: theme ?? ThemeState(),
            title: title,
            message: message,
            primaryText: primaryText,
            secondaryText: secondaryText,
            stayText: "Rester",
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
