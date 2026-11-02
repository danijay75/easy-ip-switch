import Foundation
import Testing
@testable import EasyIPSwitch

@MainActor
@Suite("Localisation")
struct LocalizationTests {
    private func makeDefaults() throws -> (UserDefaults, String) {
        let name = "EasyIPSwitchTests.language.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: name))
        defaults.removePersistentDomain(forName: name)
        return (defaults, name)
    }

    @Test("Français par défaut")
    func frenchByDefault() throws {
        let (defaults, name) = try makeDefaults()
        defer { defaults.removePersistentDomain(forName: name) }

        let store = LanguageStore(defaults: defaults, applyGlobally: false)
        #expect(store.current == .french)
    }

    @Test("Langue enregistrée rechargée")
    func savedLanguageIsRestored() throws {
        let (defaults, name) = try makeDefaults()
        defer { defaults.removePersistentDomain(forName: name) }

        defaults.set("de", forKey: LanguageStore.defaultsKey)
        let store = LanguageStore(defaults: defaults, applyGlobally: false)
        #expect(store.current == .german)
    }

    @Test("Persistance du changement de langue")
    func languageChangePersists() throws {
        let (defaults, name) = try makeDefaults()
        defer { defaults.removePersistentDomain(forName: name) }

        let first = LanguageStore(defaults: defaults, applyGlobally: false)
        first.set(.spanish)
        let second = LanguageStore(defaults: defaults, applyGlobally: false)
        #expect(second.current == .spanish)
        #expect(defaults.string(forKey: LanguageStore.defaultsKey) == "es")
    }

    @Test("Repli français pour une clé inconnue")
    func unknownKeyFallsBackToFrench() {
        #expect(L.t("Cette clé n'existe pas.", language: .english) == "Cette clé n'existe pas.")
        #expect(L.t("Cette clé n'existe pas.", language: .german) == "Cette clé n'existe pas.")
        #expect(L.t("Cette clé n'existe pas.", language: .spanish) == "Cette clé n'existe pas.")
    }

    @Test("Textes principaux traduits dans les quatre langues")
    func mainTextsAreTranslated() {
        let keys = [
            "Actualiser",
            "Cartes réseau",
            "Test de ping",
            "Adresse IP",
            "Masque",
            "Passerelle",
            "Carte active",
            "Tester une adresse IP sur le réseau",
            "Quitter Easy IP-Switch ?",
            "Soutenir Easy IP-Switch",
            "Faire un don",
            "Réglages Réseau"
        ]

        for language in [AppLanguage.english, .german, .spanish] {
            for key in keys {
                #expect(L.t(key, language: language) != key, "Missing \(language.rawValue) translation for \(key)")
            }
        }
    }

    @Test("Formatage avec arguments et locale")
    func formattingUsesArgumentsAndLocale() {
        #expect(
            L.f(
                "La passerelle n'est pas dans le sous-réseau {0}.",
                language: .english,
                ["192.168.2.0"]
            ) == "The gateway is not in subnet 192.168.2.0."
        )
        #expect(
            L.f(
                "La passerelle n'est pas dans le sous-réseau {0}.",
                language: .french,
                ["192.168.2.0"]
            ) == "La passerelle n'est pas dans le sous-réseau 192.168.2.0."
        )
        #expect(L.number(3.5, digits: 1, language: .english) == "3.5")
        #expect(L.number(3.5, digits: 1, language: .french).contains(","))
    }

    @Test("Drapeaux disponibles")
    func flagImagesExist() {
        for language in AppLanguage.allCases {
            #expect(FlagImage.image(for: language) != nil, "Missing flag for \(language.rawValue)")
        }
    }

    @Test("Copyright localisé")
    func copyrightIsLocalized() {
        let english = L.f("© {0} Angeli Visions. Tous droits réservés.", language: .english, [2026])
        #expect(english.contains("All rights reserved"))
        #expect(english.contains("2026"))
    }
}
