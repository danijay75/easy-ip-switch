import Foundation
import Testing
@testable import EasyIPSwitch

@Suite("Conditions d'utilisation")
struct TermsConsentTests {
    private final class MemoryStore: TermsConsentStore {
        var accepted = false
        var saveCount = 0

        func isAccepted() -> Bool {
            accepted
        }

        func saveAccepted(_ accepted: Bool) {
            self.accepted = accepted
            saveCount += 1
        }
    }

    private func makeDefaults() throws -> (UserDefaults, String) {
        let name = "EasyIPSwitchTests.terms.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: name))
        defaults.removePersistentDomain(forName: name)
        return (defaults, name)
    }

    @Test("Un nouvel utilisateur n'a pas encore accepté")
    func freshUserHasNotAccepted() throws {
        let (defaults, name) = try makeDefaults()
        defer { defaults.removePersistentDomain(forName: name) }

        let store = UserDefaultsTermsConsentStore(defaults: defaults)
        #expect(store.isAccepted() == false)
    }

    @Test("L'acceptation est mémorisée et le refus l'efface")
    func acceptancePersistsAndRefusalClears() throws {
        let (defaults, name) = try makeDefaults()
        defer { defaults.removePersistentDomain(forName: name) }

        UserDefaultsTermsConsentStore(defaults: defaults).saveAccepted(true)
        #expect(UserDefaultsTermsConsentStore(defaults: defaults).isAccepted())

        UserDefaultsTermsConsentStore(defaults: defaults).saveAccepted(false)
        #expect(UserDefaultsTermsConsentStore(defaults: defaults).isAccepted() == false)
    }

    @Test("Le refus n'est pas mémorisé et la question revient")
    func declineIsAskedAgain() {
        let store = MemoryStore()
        var prompts = 0

        let first = TermsConsent.ensureAccepted(store: store) {
            prompts += 1
            return false
        }
        #expect(first == false)
        #expect(store.isAccepted() == false)
        #expect(prompts == 1)

        let second = TermsConsent.ensureAccepted(store: store) {
            prompts += 1
            return true
        }
        #expect(second == true)
        #expect(store.isAccepted())
        #expect(prompts == 2)
    }

    @Test("Une acceptation déjà enregistrée ne redemande rien")
    func acceptedConsentSkipsPrompt() {
        let store = MemoryStore()
        store.accepted = true
        var prompts = 0

        let result = TermsConsent.ensureAccepted(store: store) {
            prompts += 1
            return false
        }

        #expect(result == true)
        #expect(prompts == 0)
        #expect(store.saveCount == 0)
    }

    @Test("Les textes couvrent la licence, la responsabilité et le RGPD")
    func termsCoverLicenseLiabilityAndGdpr() {
        let languageMarks = [
            (AppLanguage.french, "RGPD"),
            (.english, "GDPR"),
            (.german, "DSGVO"),
            (.spanish, "RGPD")
        ]

        for (language, mark) in languageMarks {
            let text = TermsContent.text(for: language)
            #expect(text.contains("MIT"), "Missing MIT licence in \(language.rawValue)")
            #expect(text.contains(mark), "Missing \(mark) in \(language.rawValue)")
            #expect(text.contains("2016/679"), "Missing GDPR reference in \(language.rawValue)")
            #expect(text.contains("macOS"), "Text must mention macOS in \(language.rawValue)")
            #expect(!text.contains("Windows"), "Text must not mention Windows in \(language.rawValue)")
        }
    }

    @Test("Les libellés du dialogue sont traduits")
    func dialogLabelsAreTranslated() {
        let keys = [
            "Conditions d'utilisation et RGPD",
            "Veuillez lire et accepter les présentes conditions pour utiliser Easy IP-Switch.",
            "100 % local · Aucune donnée personnelle collectée",
            "J'accepte",
            "Je refuse"
        ]

        for language in [AppLanguage.english, .german, .spanish] {
            for key in keys {
                #expect(L.t(key, language: language) != key, "Missing \(language.rawValue) translation for \(key)")
            }
        }

        #expect(L.t("J'accepte", language: .english) == "I accept")
        #expect(L.t("Je refuse", language: .german) == "Ich lehne ab")
        #expect(L.t("Conditions d'utilisation et RGPD", language: .spanish) == "Condiciones de uso y RGPD")
    }
}
