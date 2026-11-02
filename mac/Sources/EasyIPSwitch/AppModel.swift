import Combine
import Foundation

@MainActor
final class AppModel: ObservableObject {
    @Published var cards: [AdapterCardModel] = []
    @Published var loading = false
    @Published var statusText = ""
    @Published var statusKind: MessageKind = .none
    @Published var tab: MainTab = .cards

    /// Bumped when a card draft becomes dirty or clean so the footer status
    /// hint, which is derived from all cards, is refreshed.
    @Published private(set) var draftRevision = 0

    private var refreshInFlight = false
    private var hasLoadedOnce = false

    var hasPendingChanges: Bool {
        cards.contains { $0.isDirty }
    }

    var isBusy: Bool {
        cards.contains { $0.busy }
    }

    var pendingHint: String {
        if loading && cards.isEmpty {
            return L.t("Chargement des cartes réseau…")
        }
        let dirty = cards.filter { $0.isDirty }.count
        if dirty == 0 {
            return L.t("Aucune modification en attente.")
        }
        if dirty == 1 {
            return L.t("1 carte modifiée — utilisez le bouton Changer de la carte concernée.")
        }
        return L.f("{0} cartes modifiées — utilisez le bouton Changer de chaque carte.", dirty)
    }

    func start() async {
        guard !hasLoadedOnce else { return }
        await refresh(manual: false)
    }

    func refresh(manual: Bool) async {
        guard !refreshInFlight, !cards.contains(where: { $0.busy }) else { return }
        refreshInFlight = true
        loading = true

        if manual {
            setStatus(L.t("Actualisation des cartes réseau…"), .info)
        }

        let infos = await NetworkReader.readAsync()
        refreshInFlight = false
        loading = false

        guard !infos.isEmpty else {
            let firstLoad = !hasLoadedOnce
            hasLoadedOnce = true
            if cards.isEmpty {
                if manual || firstLoad {
                    setStatus(L.t("Aucune carte réseau détectée. Vérifiez les réglages Réseau de macOS."), .warning)
                }
            } else {
                setStatus(L.t("Lecture réseau incomplète : les cartes affichées sont conservées. Réessayez avec Actualiser."), .warning)
            }
            return
        }

        merge(infos)
        hasLoadedOnce = true
        if manual {
            setStatus(L.t("Cartes réseau actualisées."), .success)
        }
    }

    func autoRefresh() async {
        guard !refreshInFlight, !cards.contains(where: { $0.busy }) else { return }
        await refresh(manual: false)
    }

    @discardableResult
    func apply(_ card: AdapterCardModel) async -> Bool {
        guard !card.busy else { return false }
        guard card.info.enabled else {
            let text = L.t("La carte est désactivée ; activez-la avant de changer son adressage.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
            return false
        }

        if let error = card.validateForApply() {
            setStatus(error, .error)
            return false
        }

        let service = card.info.serviceName
        let identifier = card.id
        let wantsDHCP = card.mode == .dhcp
        let requestedIP = card.ip.trimmingCharacters(in: .whitespacesAndNewlines)
        let requestedMask = card.mask.trimmingCharacters(in: .whitespacesAndNewlines)
        let requestedRouter = card.router.trimmingCharacters(in: .whitespacesAndNewlines)

        card.busy = true
        card.clearMessage()
        setStatus(L.f("Application de la configuration sur « {0} »…", service), .info)

        let result: CommandResult
        if wantsDHCP {
            result = await NetworkWriter.setDHCP(service: service)
        } else {
            result = await NetworkWriter.setManual(
                service: service,
                ip: requestedIP,
                mask: requestedMask,
                router: requestedRouter
            )
        }

        guard result.status == 0 else {
            let text = NetworkWriter.friendlyMessage(result)
            card.busy = false
            card.setMessage(text, .error)
            setStatus(text, .error)
            return false
        }

        // Let SystemConfiguration publish the new state before verifying it.
        // A short retry window avoids false warnings while the dynamic store
        // catches up with networksetup.
        var lastInfos: [AdapterInfo] = []
        var latestTarget: AdapterInfo?

        for _ in 0..<4 {
            try? await Task.sleep(nanoseconds: 300_000_000)
            let infos = await NetworkReader.readAsync()
            // Keep the last non-empty read: a transient empty result must not
            // hide a configuration that a previous iteration already saw.
            if !infos.isEmpty {
                lastInfos = infos
            }
            guard let candidate = infos.first(where: { $0.id == identifier }) else { continue }
            // Remember the card even if a later partial read omits it.
            latestTarget = candidate
            if matches(candidate, wantsDHCP: wantsDHCP, ip: requestedIP, mask: requestedMask, router: requestedRouter) {
                break
            }
        }
        card.busy = false

        guard !lastInfos.isEmpty else {
            let text = L.t("Configuration envoyée, mais la relecture des cartes réseau a échoué. Utilisez Actualiser pour vérifier.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
            return false
        }

        guard let verifiedAdapter = latestTarget else {
            let text = L.t("Configuration envoyée, mais la carte n'a pas été retrouvée. Utilisez Actualiser pour vérifier.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
            return false
        }

        let verified = matches(
            verifiedAdapter,
            wantsDHCP: wantsDHCP,
            ip: requestedIP,
            mask: requestedMask,
            router: requestedRouter
        )

        // Keep the draft when macOS has not confirmed it yet; otherwise reset
        // the fields to the freshly read state. Never drop other cards when a
        // verification read is partial.
        var mergeInfos = lastInfos
        if !mergeInfos.contains(where: { $0.id == identifier }) {
            mergeInfos.append(verifiedAdapter)
        }
        merge(mergeInfos, resetIDs: verified ? [identifier] : [], preserveMissing: true)

        if verified {
            let text = L.t("Configuration appliquée et vérifiée.")
            card.setMessage(text, .success)
            setStatus(L.f("Configuration appliquée sur « {0} ».", verifiedAdapter.serviceName), .success)
            return true
        }

        let text = L.f("Configuration envoyée, mais macOS affiche encore : {0}.", verifiedAdapter.summaryText)
        card.setMessage(text, .warning)
        setStatus(L.t("Configuration envoyée ; vérification incomplète."), .warning)
        return false
    }

    func setEnabled(_ card: AdapterCardModel, _ enabled: Bool) async {
        guard !card.busy else { return }

        let service = card.info.serviceName
        let identifier = card.id

        card.busy = true
        setStatus(
            enabled
                ? L.f("Activation de « {0} »…", service)
                : L.f("Désactivation de « {0} »…", service),
            .info
        )

        let result = await NetworkWriter.setEnabled(service: service, enabled: enabled)
        guard result.status == 0 else {
            let text = NetworkWriter.friendlyMessage(result)
            card.busy = false
            card.setMessage(text, .error)
            setStatus(text, .error)
            return
        }

        try? await Task.sleep(nanoseconds: 300_000_000)
        var lastInfos: [AdapterInfo] = []
        var latestTarget: AdapterInfo?

        for _ in 0..<3 {
            let infos = await NetworkReader.readAsync()
            if !infos.isEmpty {
                lastInfos = infos
            }
            guard let candidate = infos.first(where: { $0.id == identifier }) else {
                try? await Task.sleep(nanoseconds: 300_000_000)
                continue
            }
            // Remember the card even if a later partial read omits it.
            latestTarget = candidate
            if candidate.enabled == enabled {
                break
            }
            try? await Task.sleep(nanoseconds: 300_000_000)
        }
        card.busy = false

        guard !lastInfos.isEmpty else {
            let text = L.t("L'état de la carte n'a pas pu être relu. Utilisez Actualiser pour vérifier.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
            return
        }

        guard let fresh = latestTarget else {
            let text = L.t("L'état de la carte n'a pas pu être relu. Utilisez Actualiser pour vérifier.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
            return
        }

        var mergeInfos = lastInfos
        if !mergeInfos.contains(where: { $0.id == identifier }) {
            mergeInfos.append(fresh)
        }
        merge(mergeInfos, preserveMissing: true)

        if fresh.enabled == enabled {
            let text = enabled ? L.t("Carte activée.") : L.t("Carte désactivée.")
            card.setMessage(text, .success)
            setStatus(
                enabled
                    ? L.f("« {0} » est activée.", fresh.serviceName)
                    : L.f("« {0} » est désactivée.", fresh.serviceName),
                .success
            )
        } else {
            let text = L.t("macOS n'a pas confirmé le changement d'état.")
            card.setMessage(text, .warning)
            setStatus(text, .warning)
        }
    }

    func test(_ card: AdapterCardModel) async {
        guard !card.busy else { return }

        let address = card.testAddress
        guard let parsed = IPv4.parse(address), !IPv4.isForbidden(parsed) else {
            let text = L.t("Cette carte n'a pas d'adresse IPv4 valide à tester.")
            card.setMessage(text, .error)
            setStatus(text, .error)
            return
        }

        let testing = L.f("Test de « {0} » en cours…", address)
        card.busy = true
        card.setMessage(testing, .info)
        setStatus(testing, .info)

        let outcome = await PingRunner.send(address: address, device: card.info.device, count: 3)

        var gatewayFailure = ""
        let gateway = card.gatewayAddress
        if outcome.success, !gateway.isEmpty, gateway != address, IPv4.parse(gateway) != nil {
            let gatewayOutcome = await PingRunner.send(address: gateway, device: card.info.device, count: 2)
            if !gatewayOutcome.success {
                gatewayFailure = " " + L.f("La passerelle {0} ne répond pas.", gateway)
            }
        }

        card.busy = false

        if outcome.success {
            let text = L.f("L'adresse {0} répond ({1}).", address, outcome.detail)
            if gatewayFailure.isEmpty {
                card.setMessage(text, .success)
                setStatus(text, .success)
            } else {
                card.setMessage(text + gatewayFailure, .error)
                setStatus(text + gatewayFailure, .error)
            }
        } else {
            let text = L.f("Aucune réponse de {0} : {1}.", address, outcome.detail)
            card.setMessage(text, .error)
            setStatus(text, .error)
        }
    }

    func applyAllPending() async -> Bool {
        let pending = cards.filter { $0.isDirty }
        for card in pending {
            let success = await apply(card)
            if !success {
                return false
            }
        }
        return true
    }

    func setStatus(_ text: String, _ kind: MessageKind) {
        statusText = text
        statusKind = kind
    }

    func cardDraftStateChanged() {
        draftRevision &+= 1
    }

    /// Refreshes the transient messages that were generated in the previous
    /// language. Computed hints and field errors translate themselves.
    func languageChanged() {
        setStatus("", .none)
        for card in cards {
            card.languageChanged()
        }
        draftRevision &+= 1
    }

    private func matches(
        _ adapter: AdapterInfo,
        wantsDHCP: Bool,
        ip: String,
        mask: String,
        router: String
    ) -> Bool {
        if wantsDHCP {
            return adapter.dhcp
        }
        let sameRouter = router.isEmpty || adapter.router == router
        return !adapter.dhcp
            && adapter.ip == ip
            && adapter.mask == mask
            && sameRouter
    }

    private func merge(
        _ infos: [AdapterInfo],
        resetIDs: Set<String> = [],
        preserveMissing: Bool = false
    ) {
        let identifiers = Set(infos.map { $0.id })
        var next: [AdapterCardModel] = []

        for info in infos {
            if let existing = cards.first(where: { $0.id == info.id }) {
                existing.update(info: info, reset: resetIDs.contains(info.id))
                next.append(existing)
            } else {
                next.append(AdapterCardModel(info: info))
            }
        }

        if preserveMissing {
            next.append(contentsOf: cards.filter { !identifiers.contains($0.id) })
        }

        cards = next
    }
}
