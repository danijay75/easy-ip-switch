# Easy IP-Switch

Application de changement rapide d’adresse IP pour **Windows** et **macOS**.

Version actuelle : **1.1.3**

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
- Acceptation en un clic des conditions d’utilisation et de la notice RGPD au lancement (mémorisée après acceptation).
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
dist/easy ip-switch v1.1.3.exe
```

## Signature et paquet Windows

La version 1.1.3 est encore non signée : SmartScreen peut afficher un
avertissement. Le pipeline de signature est prêt dans `sign.ps1` et
`package-windows.ps1`.

Signature Azure Artifact Signing (recommandée) :

```powershell
.\build.ps1 -Sign -SignMode Azure `
  -AzureDlib "C:\outils\Azure.CodeSigning.Dlib.dll" `
  -AzureMetadata ".\packaging\windows\artifact-signing-metadata.json" `
  -Package
```

Le paquet `dist/easy-ip-switch-1.1.3-windows.zip` contient l'exécutable,
le README quadrilingue, la licence et les empreintes SHA-256. Le modèle
de README est choisi automatiquement selon la signature : version
« non signée » ou version « signée Angeli Visions ». Les détails de
mise en place et la procédure de remplacement sont dans
[`docs/SIGNATURE-WINDOWS.md`](docs/SIGNATURE-WINDOWS.md).

## Tests Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run.ps1
```

## macOS

Le portage macOS se compile exclusivement sur un Mac avec Xcode ou les
Command Line Tools. Consulter :

- `mac/README.md`
- `mac/ETAT-DU-PROJET-MACOS.md`

## Site web

La page <https://angelivisions.com/easy-ip-switch> est servie depuis le dépôt
`danijay75/angelivisions` (Next.js).

### Reste à faire depuis Windows

La release macOS **1.1.3** et le dépôt du site sont à jour sur GitHub, mais la
production n'a pas encore été déployée. Sur le poste Windows :

1. Récupérer la dernière version du site (le dossier local pCloud est en
   retard sur `main`) :
   ```powershell
   cd "P:\Projets IA\angeli-visions-website"
   git fetch origin
   git reset --hard origin/main
   ```
2. Vérifier que les deux DMG 1.1.3 sont présents :
   ```powershell
   Get-ChildItem public\downloads\easy-ip-switch\easy-ip-switch-1.1.3-macos-*.dmg
   ```
3. Déployer :
   ```powershell
   .\.agent\deploy.ps1
   ```
4. Contrôler la page : version **1.1.3**, liens Intel et Apple Silicon qui
   répondent, mention **Conditions et RGPD**.

Rappel : la page Windows utilise déjà `easy-ip-switch-1.1.3-windows.exe`.

## Soutenir le projet

<https://www.paypal.com/donate/?hosted_button_id=WZBCQXPEHHDDC>

## Licence

Distribué sous licence **MIT**. Voir [`LICENSE`](LICENSE).

---

© 2026 Angeli Visions.
