import Foundation

/// Terms of Use and GDPR notice shown once at first launch.
///
/// The four texts mirror `src/TermsDialog.cs` on Windows, adapted to the macOS
/// network settings and user session.
enum TermsContent {
    static func text(for language: AppLanguage) -> String {
        switch language {
        case .french: return french
        case .english: return english
        case .german: return german
        case .spanish: return spanish
        }
    }

    private static let french = """
    1. Objet et licence (MIT)
    Easy IP-Switch, édité par Angeli Visions, permet de consulter et de modifier localement la configuration IPv4 des cartes réseau macOS (DHCP, adresse fixe, masque, passerelle, activation) et d'effectuer des tests de ping.

    2. Responsabilité d'utilisation
    L'application modifie directement les paramètres réseau de ce poste. Vous êtes seul responsable des configurations appliquées ; des réglages inadaptés peuvent interrompre votre accès au réseau ou à Internet. Le logiciel est fourni « en l'état », sans garantie.

    3. Protection des données et respect du RGPD (UE 2016/679)
    • Aucune collecte : Easy IP-Switch fonctionne 100 % en local. Aucune donnée personnelle, adresse IP ou télémétrie n'est collectée ni transmise à Angeli Visions ou à un tiers.
    • Stockage local uniquement : seuls la langue choisie et votre accord sont enregistrés dans votre session macOS locale, ainsi qu'un journal local en cas d'erreur technique.
    • Liens externes : le site officiel et la page de don PayPal ne s'ouvrent que sur clic explicite de votre part.

    4. Votre choix
    Cliquez sur « J'accepte » pour mémoriser votre accord et ouvrir l'application, ou sur « Je refuse » pour fermer l'application (votre accord vous sera redemandé au prochain lancement).
    """

    private static let english = """
    1. Purpose and license (MIT)
    Easy IP-Switch, published by Angeli Visions, lets you view and locally modify the IPv4 configuration of macOS network cards (DHCP, static address, mask, gateway, activation) and run ping tests.

    2. Responsibility of use
    The application directly modifies this computer's network settings. You are solely responsible for the configurations applied; inappropriate settings may interrupt your network or Internet access. The software is provided "as is", without warranty.

    3. Data protection and GDPR compliance (EU 2016/679)
    • No data collection: Easy IP-Switch operates 100% locally. No personal data, IP address or telemetry is collected or transmitted to Angeli Visions or any third party.
    • Local storage only: only your selected language and your consent are stored in your local macOS user session, along with a local log in case of a technical error.
    • External links: the official website and the PayPal donation page open only upon your explicit click.

    4. Your choice
    Click "I accept" to remember your consent and open the application, or "I decline" to close the application (you will be asked again at the next launch).
    """

    private static let german = """
    1. Zweck und Lizenz (MIT)
    Easy IP-Switch, herausgegeben von Angeli Visions, ermöglicht das lokale Anzeigen und Ändern der IPv4-Konfiguration von macOS-Netzwerkkarten (DHCP, feste Adresse, Maske, Gateway, Aktivierung) sowie das Ausführen von Ping-Tests.

    2. Nutzungsverantwortung
    Die Anwendung ändert direkt die Netzwerkeinstellungen dieses Computers. Sie sind allein für die angewendeten Konfigurationen verantwortlich; ungeeignete Einstellungen können Ihren Netzwerk- oder Internetzugang unterbrechen. Die Software wird „wie besehen“ ohne Gewährleistung bereitgestellt.

    3. Datenschutz und DSGVO-Konformität (EU 2016/679)
    • Keine Datenerhebung: Easy IP-Switch arbeitet zu 100 % lokal. Es werden keine personenbezogenen Daten, IP-Adressen oder Telemetriedaten erhoben oder an Angeli Visions oder Dritte übermittelt.
    • Nur lokale Speicherung: Lediglich die gewählte Sprache und Ihre Zustimmung werden in Ihrer lokalen macOS-Benutzersitzung gespeichert, ergänzt durch ein lokales Protokoll bei technischen Fehlern.
    • Externe Links: Die offizielle Website und die PayPal-Spendenseite öffnen sich nur nach Ihrem ausdrücklichen Klick.

    4. Ihre Wahl
    Klicken Sie auf „Ich akzeptiere“, um Ihre Zustimmung zu speichern und die Anwendung zu öffnen, oder auf „Ich lehne ab“, um die Anwendung zu schließen (beim nächsten Start werden Sie erneut gefragt).
    """

    private static let spanish = """
    1. Objeto y licencia (MIT)
    Easy IP-Switch, editado por Angeli Visions, permite consultar y modificar localmente la configuración IPv4 de las tarjetas de red de macOS (DHCP, dirección fija, máscara, puerta de enlace, activación) y realizar pruebas de ping.

    2. Responsabilidad de uso
    La aplicación modifica directamente los ajustes de red de este equipo. Usted es el único responsable de las configuraciones aplicadas; unos ajustes inadecuados pueden interrumpir su acceso a la red o a Internet. El software se proporciona «tal cual», sin garantía.

    3. Protección de datos y cumplimiento del RGPD (UE 2016/679)
    • Sin recopilación de datos: Easy IP-Switch funciona 100 % en local. No se recopila ni transmite ningún dato personal, dirección IP ni telemetría a Angeli Visions ni a terceros.
    • Almacenamiento exclusivamente local: solo el idioma elegido y su aceptación se guardan en su sesión local de macOS, junto con un registro local en caso de error técnico.
    • Enlaces externos: el sitio web oficial y la página de donación de PayPal solo se abren mediante un clic explícito por su parte.

    4. Su elección
    Haga clic en «Acepto» para recordar su consentimiento y abrir la aplicación, o en «Rechazo» para cerrar la aplicación (se le volverá a preguntar en el próximo inicio).
    """
}
