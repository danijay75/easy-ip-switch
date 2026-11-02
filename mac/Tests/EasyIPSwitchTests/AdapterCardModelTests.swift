import Testing
@testable import EasyIPSwitch

@MainActor
@Suite("Modèle de carte réseau")
struct AdapterCardModelTests {
    private func makeInfo(
        id: String = "service-1",
        serviceName: String = "Wi-Fi",
        device: String = "en0",
        hardwareName: String = "Wi-Fi",
        kind: AdapterKind = .wifi,
        enabled: Bool = true,
        connected: Bool = true,
        dhcp: Bool = true,
        ip: String = "192.168.1.6",
        mask: String = "255.255.255.0",
        router: String = "192.168.1.1"
    ) -> AdapterInfo {
        AdapterInfo(
            id: id,
            serviceName: serviceName,
            device: device,
            hardwareName: hardwareName,
            kind: kind,
            enabled: enabled,
            connected: connected,
            dhcp: dhcp,
            ip: ip,
            mask: mask,
            router: router
        )
    }

    private func isMode(_ card: AdapterCardModel, _ expected: ConfigMode) -> Bool {
        switch (card.mode, expected) {
        case (.dhcp, .dhcp), (.manual, .manual):
            return true
        default:
            return false
        }
    }

    private func isKind(_ message: CardMessage?, _ expected: MessageKind) -> Bool {
        guard let message = message else { return false }
        switch (message.kind, expected) {
        case (.none, .none), (.info, .info), (.success, .success),
             (.warning, .warning), (.error, .error):
            return true
        default:
            return false
        }
    }

    @Test("Une carte DHCP démarre propre")
    func dhcpCardStartsClean() {
        let card = AdapterCardModel(info: makeInfo())

        #expect(isMode(card, .dhcp))
        #expect(card.ip == "192.168.1.6")
        #expect(card.mask == "255.255.255.0")
        #expect(card.router == "192.168.1.1")
        #expect(!card.isDirty)
        #expect(card.canEdit)
        #expect(card.firstIssue == nil)
    }

    @Test("DHCP vers fixe conserve les valeurs et devient modifié")
    func dhcpToManualKeepsCurrentValuesAndBecomesDirty() {
        let card = AdapterCardModel(info: makeInfo())
        card.changeMode(.manual)

        #expect(isMode(card, .manual))
        #expect(card.ip == "192.168.1.6")
        #expect(card.mask == "255.255.255.0")
        #expect(card.router == "192.168.1.1")
        #expect(card.isDirty)
    }

    @Test("DHCP vers fixe sans adresse utilise les valeurs par défaut")
    func dhcpToManualWithoutAddressUsesDefaults() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "", mask: "", router: ""))
        card.changeMode(.manual)

        #expect(isMode(card, .manual))
        #expect(card.ip == AdapterCardModel.defaultIP)
        #expect(card.mask == AdapterCardModel.defaultMask)
        // The default address is the first host, so the suggested gateway is
        // the next usable host of the subnet.
        #expect(card.router == "192.168.0.2")
        #expect(card.isDirty)
    }

    @Test("Une carte fixe sans masque n'est pas modifiée")
    func staticCardWithoutMaskIsNotDirty() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50", mask: "", router: ""))

        #expect(isMode(card, .manual))
        #expect(card.mask == "")
        #expect(!card.isDirty)
        #expect(card.maskError != nil)
    }

    @Test("Validation des adresses IP")
    func invalidAddressValidation() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))

        card.ip = "192.168.1.256"
        #expect(card.ipError != nil)

        card.ip = "0.0.0.0"
        #expect(card.ipError != nil)

        card.ip = "255.255.255.255"
        #expect(card.ipError != nil)

        card.ip = "169.254.10.10"
        #expect(card.ipError != nil)

        card.ip = "192.168.1.51"
        #expect(card.ipError == nil)
    }

    @Test("Validation du masque")
    func invalidMaskValidation() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))

        card.mask = "255.0.255.0"
        #expect(card.maskError != nil)

        card.mask = "255.255.255.1"
        #expect(card.maskError != nil)

        card.mask = "255.255.255.0"
        #expect(card.maskError == nil)
    }

    @Test("Validation de la passerelle")
    func gatewayValidation() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50", router: "192.168.1.1"))

        card.router = ""
        #expect(card.routerError == nil)

        card.router = "192.168.2.1"
        #expect(card.routerError != nil)

        card.router = "192.168.1.50"
        #expect(card.routerError != nil)

        card.router = "192.168.1.1"
        #expect(card.routerError == nil)
    }

    @Test("validateForApply active l'affichage des erreurs")
    func validateForApplySetsShowErrors() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))
        card.ip = "192.168.1.999"

        #expect(!card.showErrors)
        #expect(card.validateForApply() != nil)
        #expect(card.showErrors)
    }

    @Test("Le brouillon est conservé lors d'une actualisation")
    func draftIsPreservedOnRefreshUntilReset() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))
        card.ip = "192.168.1.60"

        card.update(
            info: makeInfo(dhcp: false, ip: "192.168.1.70", router: "192.168.1.1"),
            reset: false
        )
        #expect(card.ip == "192.168.1.60")
        #expect(card.isDirty)

        card.update(
            info: makeInfo(dhcp: false, ip: "192.168.1.70", router: "192.168.1.1"),
            reset: true
        )
        #expect(card.ip == "192.168.1.70")
        #expect(!card.isDirty)
    }

    @Test("Annuler restaure la configuration courante")
    func resetDraftRestoresCurrentConfiguration() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))
        card.ip = "192.168.1.60"
        card.mask = "255.255.0.0"
        card.router = "192.168.1.254"

        card.resetDraft()

        #expect(card.ip == "192.168.1.50")
        #expect(card.mask == "255.255.255.0")
        #expect(card.router == "192.168.1.1")
        #expect(!card.isDirty)
    }

    @Test("Une carte modifiée affiche le message en attente")
    func dirtyCardShowsPendingMessage() {
        let card = AdapterCardModel(info: makeInfo())
        card.changeMode(.manual)

        #expect(isKind(card.displayMessage, .warning))
        #expect(card.displayMessage?.text == "Modification non appliquée")
    }

    @Test("Une carte désactivée ou occupée n'est pas éditable")
    func disabledAndBusyCardsCannotEdit() {
        let disabled = AdapterCardModel(info: makeInfo(enabled: false))
        #expect(!disabled.canEdit)
        #expect(disabled.hintText == "Carte désactivée")

        let busy = AdapterCardModel(info: makeInfo())
        busy.busy = true
        #expect(!busy.canEdit)
    }

    @Test("Une adresse APIPA est refusée")
    func apipaIsRejectedOnManualEntry() {
        let card = AdapterCardModel(info: makeInfo(dhcp: false, ip: "192.168.1.50"))
        card.ip = "169.254.0.1"
        #expect(card.ipError != nil)
    }

    @Test("La passerelle se déduit de l'adresse saisie")
    func gatewayIsSuggestedFromAddress() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "", mask: "", router: ""))
        card.changeMode(.manual)

        card.ip = "192.168.2.101"
        card.mask = "255.255.255.0"
        #expect(card.router == "192.168.2.1")
    }

    @Test("La passerelle suit le masque tant qu'elle est suggérée")
    func gatewayFollowsMaskWhileSuggested() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "", mask: "", router: ""))
        card.changeMode(.manual)

        card.ip = "192.168.2.101"
        card.mask = "255.255.255.0"
        #expect(card.router == "192.168.2.1")

        card.mask = "255.255.0.0"
        #expect(card.router == "192.168.0.1")

        card.ip = "10.5.0.20"
        #expect(card.router == "10.5.0.1")
    }

    @Test("Une passerelle saisie manuellement n'est pas écrasée")
    func manualGatewayIsNotOverwritten() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "", mask: "", router: ""))
        card.changeMode(.manual)

        card.router = "192.168.2.254"
        card.ip = "192.168.2.101"
        card.mask = "255.255.255.0"
        #expect(card.router == "192.168.2.254")
    }

    @Test("Une passerelle effacée reste vide")
    func clearedGatewayStaysEmpty() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "", mask: "", router: ""))
        card.changeMode(.manual)
        card.ip = "192.168.2.101"
        card.mask = "255.255.255.0"
        #expect(card.router == "192.168.2.1")

        card.router = ""
        card.ip = "192.168.3.101"
        #expect(card.router == "")

        card.resetDraft()
        card.changeMode(.manual)
        card.ip = "192.168.4.101"
        #expect(card.router == "192.168.4.1")
    }

    @Test("La passerelle actuelle est conservée au passage en fixe")
    func existingGatewayIsPreservedOnModeChange() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "192.168.1.6", router: "192.168.1.254"))
        card.changeMode(.manual)
        #expect(card.router == "192.168.1.254")
    }

    @Test("La passerelle héritée suit un changement de sous-réseau")
    func inheritedGatewayFollowsNewSubnet() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "192.168.1.6", router: "192.168.1.1"))
        card.changeMode(.manual)
        #expect(card.router == "192.168.1.1")

        card.ip = "192.168.2.101"
        #expect(card.router == "192.168.2.1")
    }

    @Test("La passerelle héritée reste stable dans le même sous-réseau")
    func inheritedGatewayStaysWithinSubnet() {
        let card = AdapterCardModel(info: makeInfo(dhcp: true, ip: "192.168.1.6", router: "192.168.1.254"))
        card.changeMode(.manual)

        card.ip = "192.168.1.20"
        #expect(card.router == "192.168.1.254")
    }
}
