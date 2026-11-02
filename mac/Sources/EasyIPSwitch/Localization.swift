import Combine
import Foundation

enum AppLanguage: String, CaseIterable, Identifiable {
    case french = "fr"
    case english = "en"
    case german = "de"
    case spanish = "es"

    var id: String { rawValue }

    /// Native language name, as shown in the selector.
    var displayName: String {
        switch self {
        case .french: return "Français"
        case .english: return "English"
        case .german: return "Deutsch"
        case .spanish: return "Español"
        }
    }

    /// Short code shown next to the flag.
    var codeLabel: String {
        rawValue.uppercased()
    }

    /// Asset name of the flag in Resources/flags.
    var flagCode: String {
        switch self {
        case .french: return "fr"
        case .english: return "gb"
        case .german: return "de"
        case .spanish: return "es"
        }
    }

    var locale: Locale {
        switch self {
        case .french: return Locale(identifier: "fr_FR")
        case .english: return Locale(identifier: "en_US")
        case .german: return Locale(identifier: "de_DE")
        case .spanish: return Locale(identifier: "es_ES")
        }
    }
}

/// Thread-safe localization engine. Views observe LanguageStore; model code
/// and background ping parsing use L directly.
enum L {
    private static let lock = NSLock()
    private static var storage: AppLanguage = .french

    private static let catalog: [String: [String: String]] = {
        var merged = LocalizationCatalog.translations
        for (key, value) in LocalizationExtras.translations {
            merged[key] = value
        }
        return merged
    }()

    static var currentLanguage: AppLanguage {
        lock.lock()
        defer { lock.unlock() }
        return storage
    }

    static func setCurrent(_ language: AppLanguage) {
        lock.lock()
        storage = language
        lock.unlock()
    }

    static func t(_ french: String, language: AppLanguage) -> String {
        guard language != .french else { return french }
        return catalog[french]?[language.rawValue] ?? french
    }

    static func t(_ french: String) -> String {
        t(french, language: currentLanguage)
    }

    static func f(_ french: String, language: AppLanguage, _ args: [Any]) -> String {
        var result = t(french, language: language)
        for (index, argument) in args.enumerated() {
            result = result.replacingOccurrences(of: "{\(index)}", with: format(argument, language: language))
        }
        return result
    }

    static func f(_ french: String, _ args: [Any]) -> String {
        f(french, language: currentLanguage, args)
    }

    static func f(_ french: String, _ args: Any...) -> String {
        f(french, args)
    }

    static func number(_ value: Double, digits: Int, language: AppLanguage) -> String {
        let formatter = NumberFormatter()
        formatter.locale = language.locale
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = false
        formatter.minimumFractionDigits = digits
        formatter.maximumFractionDigits = digits
        return formatter.string(from: NSNumber(value: value)) ?? String(format: "%.\(digits)f", value)
    }

    static func number(_ value: Double, digits: Int) -> String {
        number(value, digits: digits, language: currentLanguage)
    }

    static func number(_ value: Int, language: AppLanguage) -> String {
        let formatter = NumberFormatter()
        formatter.locale = language.locale
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = false
        formatter.maximumFractionDigits = 0
        return formatter.string(from: NSNumber(value: value)) ?? "\(value)"
    }

    static func number(_ value: Int) -> String {
        number(value, language: currentLanguage)
    }

    private static func format(_ argument: Any, language: AppLanguage) -> String {
        if let value = argument as? Double {
            return number(value, digits: 1, language: language)
        }
        if let value = argument as? Int {
            return number(value, language: language)
        }
        return String(describing: argument)
    }
}

@MainActor
final class LanguageStore: ObservableObject {
    static let shared = LanguageStore()
    static let defaultsKey = "EasyIPSwitch.language"

    @Published private(set) var current: AppLanguage

    private let defaults: UserDefaults
    private let applyGlobally: Bool

    init(defaults: UserDefaults = .standard, applyGlobally: Bool = true) {
        self.defaults = defaults
        self.applyGlobally = applyGlobally
        let stored = defaults.string(forKey: Self.defaultsKey)
        let language = AppLanguage(rawValue: stored ?? "") ?? .french
        current = language
        if applyGlobally {
            L.setCurrent(language)
        }
    }

    func set(_ language: AppLanguage) {
        guard language != current else { return }
        current = language
        defaults.set(language.rawValue, forKey: Self.defaultsKey)
        if applyGlobally {
            L.setCurrent(language)
        }
    }

    func t(_ french: String) -> String {
        L.t(french)
    }

    func f(_ french: String, _ args: Any...) -> String {
        L.f(french, args)
    }
}
