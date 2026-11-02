import Foundation

enum AppInfo {
    static let productName = "Easy IP-Switch"
    static let version = "1.1.1"
    static let versionLabel = "v\(version)"
    static let websiteURL = URL(string: "https://www.angelivisions.com")!
    static let donationURL = URL(string: "https://www.paypal.com/donate/?hosted_button_id=WZBCQXPEHHDDC")!

    static var copyrightLine: String {
        let year = Calendar.current.component(.year, from: Date())
        return "© \(year) Angeli Visions. Tous droits réservés."
    }
}
