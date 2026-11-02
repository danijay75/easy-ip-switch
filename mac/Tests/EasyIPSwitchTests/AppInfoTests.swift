import Testing
@testable import EasyIPSwitch

@Suite("Informations application")
struct AppInfoTests {
    @Test("Version affichée")
    func version() {
        #expect(AppInfo.productName == "Easy IP-Switch")
        #expect(AppInfo.version == "1.1.1")
        #expect(AppInfo.versionLabel == "v1.1.1")
    }

    @Test("Liens du pied de page")
    func links() {
        #expect(AppInfo.websiteURL.scheme == "https")
        #expect(AppInfo.donationURL.scheme == "https")
        #expect(AppInfo.websiteURL.host == "www.angelivisions.com")
        #expect(AppInfo.donationURL.host == "www.paypal.com")
        #expect(AppInfo.donationURL.absoluteString.contains("hosted_button_id=WZBCQXPEHHDDC"))
    }

    @Test("Ligne de copyright")
    func copyright() {
        #expect(AppInfo.copyrightLine.contains("Angeli Visions"))
        #expect(AppInfo.copyrightLine.contains("Tous droits réservés"))
    }
}
