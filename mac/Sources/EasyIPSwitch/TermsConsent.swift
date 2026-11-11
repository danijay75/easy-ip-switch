import Foundation

/// Persists the Terms of Use and GDPR consent. The Windows build stores the
/// same flag in the registry; macOS uses `UserDefaults`.
protocol TermsConsentStore {
    func isAccepted() -> Bool
    func saveAccepted(_ accepted: Bool)
}

final class UserDefaultsTermsConsentStore: TermsConsentStore {
    static let defaultsKey = "EasyIPSwitch.termsAccepted"

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func isAccepted() -> Bool {
        defaults.bool(forKey: Self.defaultsKey)
    }

    func saveAccepted(_ accepted: Bool) {
        if accepted {
            defaults.set(true, forKey: Self.defaultsKey)
        } else {
            defaults.removeObject(forKey: Self.defaultsKey)
        }
    }
}

/// Consent gate shown before the main window. A refusal is not remembered, so
/// the next launch asks again, exactly like the Windows application.
enum TermsConsent {
    @discardableResult
    static func ensureAccepted(
        store: TermsConsentStore = UserDefaultsTermsConsentStore(),
        prompt: () -> Bool
    ) -> Bool {
        if store.isAccepted() {
            return true
        }

        let accepted = prompt()
        store.saveAccepted(accepted)
        return accepted
    }
}
