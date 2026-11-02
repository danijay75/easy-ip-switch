# Easy IP-Switch

Application de changement rapide d’adresse IP pour **Windows** et **macOS**.

Version actuelle : **1.1.1**

Site officiel : <https://angelivisions.com/easy-ip-switch>

## Fonctionnalités

- Cartes réseau Ethernet, Wi-Fi et interfaces virtuelles.
- Passage DHCP / adresse fixe.
- Validation IPv4 : format, masque contigu, passerelle dans le sous-réseau.
- Passerelle proposée automatiquement depuis l’adresse et le masque.
- Activation et désactivation des cartes.
- Test de connectivité et onglet de ping.
- Interface responsive et thèmes clair/sombre.
- Interface en **français, anglais, allemand et espagnol**, changeable sans redémarrage.
- Fermeture avec rappel de don et QR code PayPal.

## Structure

```text
src/                  Application Windows (C# / WinForms)
tests/                Tests Windows
tools/                Générateur d’icône
assets/               Icône, QR code et drapeaux
mac/                  Portage macOS (SwiftUI / Swift Package)
build.ps1             Compilation Windows
```

Les dossiers `dist/` et `mac/dist/` contiennent des binaires générés et ne sont
pas versionnés. Les versions publiées sont disponibles depuis la page officielle.

## Compiler sous Windows

Prérequis : Windows et le compilateur C# du .NET Framework (`csc.exe`).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Le binaire est produit dans :

```text
dist/easy ip-switch v1.1.1.exe
```

## Tests Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run.ps1
```

## macOS

Le portage macOS se compile exclusivement sur un Mac avec Xcode ou les
Command Line Tools. Consulter :

- `mac/README.md`
- `mac/ETAT-DU-PROJET-MACOS.md`

## Soutenir le projet

<https://www.paypal.com/donate/?hosted_button_id=WZBCQXPEHHDDC>

## Licence

Distribué sous licence **MIT**. Voir [`LICENSE`](LICENSE).

---

© 2026 Angeli Visions.
