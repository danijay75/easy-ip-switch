import Combine
import Foundation

enum FieldKind {
    case ip
    case mask
    case router
}

struct FieldIssue {
    let field: FieldKind
    let message: String
}

@MainActor
final class AdapterCardModel: ObservableObject, Identifiable {
    static let defaultIP = "192.168.0.1"
    static let defaultMask = "255.255.255.0"

    let id: String

    @Published private(set) var info: AdapterInfo
    @Published var mode: ConfigMode
    @Published var ip: String {
        didSet { refreshSuggestedGateway() }
    }
    @Published var mask: String {
        didSet { refreshSuggestedGateway() }
    }
    @Published var router: String {
        didSet {
            guard !isSettingRouterInternally else { return }
            // A value typed by the user (or cleared) must never be replaced by
            // the automatic suggestion.
            gatewayIsSuggested = false
            gatewayIsInherited = false
            userClearedGateway = router.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
    }
    @Published var busy = false
    @Published var showErrors = false

    @Published private(set) var message = ""
    @Published private(set) var messageKind: MessageKind = .info

    private var hasManualDraft = false
    private var gatewayIsSuggested = false
    /// Gateway copied from the current DHCP configuration when switching to
    /// static mode. It stays untouched while it remains valid for the new
    /// address, then follows the subnet as soon as the address changes.
    private var gatewayIsInherited = false
    private var userClearedGateway = false
    private var isSettingRouterInternally = false

    init(info: AdapterInfo) {
        self.id = info.id
        self.info = info
        self.mode = info.dhcp ? .dhcp : .manual
        self.ip = info.ip
        self.mask = info.mask
        self.router = info.router
        self.hasManualDraft = !info.dhcp && !info.ip.isEmpty
    }

    var canEdit: Bool {
        info.enabled && !busy
    }

    var isDirty: Bool {
        if mode == .dhcp {
            return !info.dhcp
        }
        // A DHCP card switched to static mode is a real change even when the
        // prefilled address happens to equal the current leased address.
        return info.dhcp || ip != info.ip || mask != info.mask || router != info.router
    }

    var hintText: String {
        if !info.enabled {
            return L.t("Carte désactivée")
        }
        if mode == .dhcp {
            return L.t("Adresse attribuée automatiquement par le réseau")
        }
        return isDirty ? L.t("Configuration fixe en attente") : L.t("Adresse configurée manuellement")
    }

    var ipError: String? {
        guard mode == .manual else { return nil }
        guard let value = IPv4.parse(ip) else {
            return L.t("Adresse IP invalide. Format attendu : 192.168.1.50")
        }
        if IPv4.isForbidden(value) {
            return L.t("Adresse IP invalide : 0.0.0.0 et 255.255.255.255 sont interdites.")
        }
        if IPv4.isAPIPA(value) {
            return L.t("Adresse 169.254.x.x réservée au réseau local : choisissez une autre adresse.")
        }
        return nil
    }

    var maskError: String? {
        guard mode == .manual else { return nil }
        guard let value = IPv4.parse(mask) else {
            return L.t("Masque invalide. Format attendu : 255.255.255.0")
        }
        if !IPv4.isValidMask(value) {
            return L.t("Masque non contigu : utilisez par exemple 255.255.255.0 ou 255.255.0.0.")
        }
        return nil
    }

    var routerError: String? {
        guard mode == .manual else { return nil }
        let value = router.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { return nil }
        guard let gateway = IPv4.parse(value) else {
            return L.t("Passerelle invalide. Format attendu : 192.168.1.1")
        }
        guard let address = IPv4.parse(ip) else { return nil }
        if gateway == address {
            return L.t("La passerelle doit être différente de l'adresse IP.")
        }
        if let netmask = IPv4.parse(mask), IPv4.isValidMask(netmask),
           (gateway & netmask) != (address & netmask) {
            return L.f("La passerelle n'est pas dans le sous-réseau {0}.", IPv4.format(address & netmask))
        }
        return nil
    }

    var firstIssue: FieldIssue? {
        if let error = ipError { return FieldIssue(field: .ip, message: error) }
        if let error = maskError { return FieldIssue(field: .mask, message: error) }
        if let error = routerError { return FieldIssue(field: .router, message: error) }
        return nil
    }

    var displayMessage: CardMessage? {
        if showErrors, let issue = firstIssue {
            return CardMessage(text: issue.message, kind: .error)
        }
        if isDirty, messageKind != .warning, messageKind != .error {
            return CardMessage(text: L.t("Modification non appliquée"), kind: .warning)
        }
        if !message.isEmpty {
            return CardMessage(text: message, kind: messageKind)
        }
        if isDirty {
            return CardMessage(text: L.t("Modification non appliquée"), kind: .warning)
        }
        return nil
    }

    var testAddress: String {
        info.ip.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var gatewayAddress: String {
        info.router.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    func changeMode(_ newMode: ConfigMode) {
        guard newMode != mode else { return }

        if newMode == .manual && !hasManualDraft {
            ip = validAddress(info.ip) ? info.ip : AdapterCardModel.defaultIP
            mask = validMask(info.mask) ? info.mask : AdapterCardModel.defaultMask
            if validAddress(info.router) {
                router = info.router
                // The DHCP gateway is kept, but it must follow the address if
                // the user moves the card to another subnet.
                gatewayIsInherited = true
                gatewayIsSuggested = false
            } else {
                // Let the suggestion fill the gateway from the address/mask.
                setRouter("")
                gatewayIsSuggested = true
            }
            userClearedGateway = false
            hasManualDraft = true
        }

        mode = newMode
        if newMode == .manual {
            refreshSuggestedGateway()
        }
        showErrors = false
        clearMessage()
    }

    func resetDraft() {
        mode = info.dhcp ? .dhcp : .manual
        ip = info.ip
        mask = info.mask
        router = info.router
        hasManualDraft = !info.dhcp && !info.ip.isEmpty
        gatewayIsSuggested = false
        gatewayIsInherited = false
        userClearedGateway = false
        showErrors = false
        clearMessage()
    }

    func update(info newInfo: AdapterInfo, reset: Bool) {
        let preserve = !reset && isDirty
        let enabledChanged = newInfo.enabled != info.enabled
        info = newInfo

        if reset {
            hasManualDraft = false
        }
        if enabledChanged {
            clearMessage()
        }
        guard !preserve else { return }

        mode = newInfo.dhcp ? .dhcp : .manual
        ip = newInfo.ip
        mask = newInfo.mask
        router = newInfo.router
        gatewayIsSuggested = false
        gatewayIsInherited = false
        userClearedGateway = false
        if mode == .manual && !newInfo.ip.isEmpty {
            hasManualDraft = true
        }
    }

    func validateForApply() -> String? {
        showErrors = true
        return firstIssue?.message
    }

    func setMessage(_ text: String, _ kind: MessageKind) {
        message = text
        messageKind = kind
    }

    func clearMessage() {
        message = ""
        messageKind = .info
    }

    /// Clears the stored message so the next render uses the new language.
    /// Field errors and hints are computed and translate themselves.
    func languageChanged() {
        clearMessage()
    }

    /// Keeps the gateway aligned with the subnet while it is empty or still
    /// holds a previous suggestion. A manually entered gateway is preserved.
    private func refreshSuggestedGateway() {
        guard mode == .manual else { return }

        let current = router.trimmingCharacters(in: .whitespacesAndNewlines)

        // A gateway inherited from the current DHCP lease stays untouched while
        // it belongs to the new subnet. Moving the address to another network
        // (for example 192.168.1.1 → 192.168.2.101) replaces it with the first
        // host of the new subnet.
        if gatewayIsInherited {
            if current.isEmpty {
                gatewayIsInherited = false
                gatewayIsSuggested = true
            } else if !gatewayBelongsToSubnet(current) || current == addressText {
                guard let suggestion = IPv4.suggestedGateway(address: ip, mask: mask) else { return }
                setRouter(suggestion)
                gatewayIsInherited = false
                gatewayIsSuggested = true
                return
            } else {
                return
            }
        }

        let autoManaged = gatewayIsSuggested
            || (current.isEmpty && !userClearedGateway)
        guard autoManaged else { return }

        guard let suggestion = IPv4.suggestedGateway(address: ip, mask: mask) else {
            if gatewayIsSuggested {
                setRouter("")
                gatewayIsSuggested = false
            }
            return
        }

        if current == suggestion {
            gatewayIsSuggested = true
            return
        }

        setRouter(suggestion)
        gatewayIsSuggested = true
    }

    private var addressText: String {
        ip.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func gatewayBelongsToSubnet(_ gateway: String) -> Bool {
        guard let gatewayValue = IPv4.parse(gateway),
              let address = IPv4.parse(ip),
              let netmask = IPv4.parse(mask),
              IPv4.isValidMask(netmask) else {
            return false
        }
        return (gatewayValue & netmask) == (address & netmask) && gatewayValue != address
    }

    private func setRouter(_ value: String) {
        guard router != value else { return }
        isSettingRouterInternally = true
        router = value
        isSettingRouterInternally = false
    }

    private func validAddress(_ text: String) -> Bool {
        guard let value = IPv4.parse(text) else { return false }
        return !IPv4.isForbidden(value) && !IPv4.isAPIPA(value)
    }

    private func validMask(_ text: String) -> Bool {
        guard let value = IPv4.parse(text) else { return false }
        return IPv4.isValidMask(value)
    }
}
