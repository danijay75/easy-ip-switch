# Easy IP-Switch pour macOS

Portage natif SwiftUI de l'application Windows **Easy IP-Switch v1.1**.
Version macOS : **v1.1.2**. Même interface, mêmes règles de validation,
adaptées aux services réseau de macOS.

> **État vérifié le 2 novembre 2026** (macOS 26.6.2, Apple Silicon) : compilation
> Intel, Apple Silicon et universelle réussie ; signature ad-hoc valide ; lancement
> des versions ARM et Intel (via Rosetta 2) confirmé. Les tests visuels et les
> écritures réseau réelles restent à exécuter. Voir `PLAN-ACTION-MACOS.md`.

> **Reprise du projet** : pour l'état complet, les blocages, la bascule
> d'OpenCode vers macOS, le prompt de reprise et la checklist de tests, lire
> [`ETAT-DU-PROJET-MACOS.md`](ETAT-DU-PROJET-MACOS.md).

## Prérequis

- macOS 12 (Monterey) ou plus récent.
- Xcode 14.2 ou plus récent (Xcode 15 recommandé), ou au minimum les
  **Xcode Command Line Tools** :
  ```bash
  xcode-select --install
  ```
- Aucune dépendance externe : SwiftUI, AppKit et SystemConfiguration sont fournis par macOS.

## Compiler les deux versions (Intel et Apple Silicon)

Depuis le dossier `mac` :

```bash
bash scripts/build-mac.sh
```

> **Volume synchronisé** : si le projet se trouve sur pCloud, Dropbox ou un
> autre volume virtuel, copiez d'abord le dossier `mac` sur le disque local du
> Mac, puis compilez depuis cette copie. Cela évite les variantes de casse et
> les attributs étendus qui peuvent perturber `swiftc` ou `codesign`. Le script
> retire déjà les attributs étendus du bundle avant de le signer.

Le script produit :

```
mac/dist/Easy IP-Switch v1.1.2 (Intel).app
mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).app
mac/dist/Easy IP-Switch v1.1.2 (Intel).dmg
mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).dmg
mac/dist/Easy IP-Switch v1.1.2 (Intel).zip
mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).zip
```

Chaque application est signée en ad-hoc (`codesign --sign -`), ce qui est
obligatoire pour lancer un binaire arm64. Aucun certificat Apple n'est requis
pour un usage local.

Chaque DMG est un installateur par glisser-déposer : il contient l'application
et un raccourci vers **Applications**.

Version universelle facultative (un seul binaire Intel + Apple Silicon) :

```bash
bash scripts/build-mac.sh --universal
```

Cela ajoute `Easy IP-Switch v1.1.2 (Universelle).app`, son zip et son DMG.

## Installer depuis un DMG

1. Double-cliquer sur le `.dmg` correspondant au Mac :
   - **Apple Silicon** : Mac avec puce M1, M2, M3, M4…
   - **Intel** : Mac avec processeur Intel ;
   - **Universelle** : fonctionne sur les deux.
2. Dans la fenêtre qui s'ouvre, glisser **Easy IP-Switch** dans le dossier
   **Applications**. Le fichier `Lisez-moi.txt` rappelle la procédure.
3. Éjecter le volume, puis lancer l'application depuis **Applications**.

Pour créer en plus un raccourci sur le Bureau, double-cliquer sur
**Installer sur le Bureau** : l'application est copiée dans **Applications** et
un lien est créé sur le Bureau du compte connecté.


> Un raccourci Bureau ne peut pas être livré tel quel dans le DMG : chaque
> compte macOS possède son propre dossier personnel. Le helper le crée donc au
> moment de l'installation, pour l'utilisateur courant.

> Le DMG universel est recommandé si le même fichier doit être distribué à
> des Mac Intel et Apple Silicon.

## Lancer l'application

1. Ouvrir **Applications** (ou le dossier `mac/dist` pour la version compilée).
2. Double-cliquer sur l'application correspondant au Mac :
   - **Apple Silicon** : Mac avec puce M1, M2, M3, M4…
   - **Intel** : Mac avec processeur Intel.
3. macOS demande l'autorisation administrateur uniquement lors d'une
   modification réseau (adresse, DHCP, activation/désactivation).

> **macOS 15 et plus récent** : au premier test de ping, macOS peut demander
> l'accès au **réseau local**. Acceptez pour permettre le test des adresses du
> réseau. Cette autorisation se règle ensuite dans
> **Réglages Système → Confidentialité et sécurité → Réseau local**.

> **Mac Apple Silicon et version Intel** : le binaire Intel ne peut s'exécuter
> sur un Mac Apple Silicon que si **Rosetta 2** est installé. macOS le propose
> automatiquement au premier lancement ; sinon :
> `softwareupdate --install-rosetta --agree-to-license`.
> Sur un Mac Apple Silicon, préférez la version **Apple Silicon** ou
> **Universelle**.

Si l'application a été téléchargée (DMG ou zip), Gatekeeper peut afficher un
message, car la signature ad-hoc ne remplace pas une signature Developer ID.
Utiliser **clic droit → Ouvrir**, ou retirer l'attribut de quarantaine :

```bash
xattr -dr com.apple.quarantine "Easy IP-Switch v1.1.2 (Apple Silicon).app"
```

## Langues

L'interface est disponible en **français, anglais, allemand et espagnol**.

- Français par défaut au premier lancement.
- Sélecteur de drapeaux dans l'en-tête, à côté du thème.
- Changement immédiat, sans redémarrage.
- Choix mémorisé dans `UserDefaults` (`EasyIPSwitch.language`).
- Drapeaux dans `assets/flags/`, copiés dans `Contents/Resources/flags` par le
  script de build.
- Toutes les chaînes visibles utilisent le catalogue `LocalizationCatalog`
  (traductions Windows) et les compléments macOS de `LocalizationExtras`.

## Fermeture et don

À la fermeture, une fenêtre rappelle les modifications en attente et propose de
soutenir le projet :

- QR code PayPal, chargé depuis `assets/donation-qr.png` et copié dans les
  ressources du bundle ;
- lien **Ouvrir la page de don** ;
- un clic sur le QR code ouvre la même page.

La fenêtre propose **Rester**, et selon le cas **Quitter** ou
**Appliquer et quitter** / **Quitter sans appliquer**. Un arrêt, une
déconnexion ou un redémarrage de macOS ne sont jamais bloqués par ce rappel.

## Développement dans Xcode

Le projet est un **Swift Package** :

1. Ouvrir `mac/Package.swift` dans Xcode (ou `Fichier → Ouvrir` puis choisir le dossier `mac`).
2. Sélectionner le schéma `EasyIPSwitch`.
3. `Run` (⌘R).

En ligne de commande :

```bash
cd mac
swift run
```

La version lancée par Xcode ou `swift run` n'a pas de bundle `.app`, mais elle
fonctionne pour développer l'interface et tester les appels réseau.

## Tests automatisés

Le projet contient une cible `EasyIPSwitchTests` (Swift Testing) qui couvre la
validation IPv4, le modèle de carte réseau et l'analyse des résultats de ping.

Depuis le dossier `mac` :

```bash
bash scripts/test.sh
```

Le script est nécessaire avec les **Command Line Tools seuls** : il ajoute les
chemins de `Testing.framework`. Avec Xcode complet, `swift test` fonctionne
directement. Swift Testing est construit pour macOS 14 : les tests demandent donc
un Mac récent, tandis que l'application reste ciblée macOS 12.

## Signature et notarisation (distribution publique)

Pour distribuer sans avertissement Gatekeeper, il faut un compte Apple
Developer et un certificat **Developer ID Application** :

```bash
codesign --force --deep --options runtime \
  --sign "Developer ID Application: VOTRE NOM (TEAMID)" \
  "Easy IP-Switch v1.1.2 (Apple Silicon).app"

ditto -c -k --keepParent "Easy IP-Switch v1.1.2 (Apple Silicon).app" archive.zip

xcrun notarytool submit archive.zip \
  --apple-id "votre@compte" \
  --team-id "TEAMID" \
  --password "mot-de-passe-application" \
  --wait

xcrun stapler staple "Easy IP-Switch v1.1.2 (Apple Silicon).app"
```

Répéter pour la version Intel, ou notariser une version universelle.

## Fonctionnement réseau

| Action | Mécanisme macOS |
|---|---|
| Lire les cartes et leur adressage | `SystemConfiguration` (`SCNetworkService`, `SCDynamicStore`) |
| Passer en DHCP | `networksetup -setdhcp` |
| Passer en adresse fixe | `networksetup -setmanual` |
| Activer / désactiver un service | `networksetup -setnetworkserviceenabled` |
| Tester une adresse | `/sbin/ping` |
| Ouvrir les Réglages Réseau | `x-apple.systempreferences` (macOS 13+) / panneau Network (macOS 12) |
| Autorisation | `osascript … with administrator privileges` |

Les commandes d'écriture sont exécutées avec l'autorisation macOS ; les
lectures et le ping restent sans privilège.

Si la passerelle est laissée vide, l'application tente de l'appliquer sans
routeur ; si `networksetup` refuse l'argument vide, elle réessaie avec
`0.0.0.0`, que macOS interprète comme « aucune passerelle ».

### Différences assumées avec la version Windows

- **Mise en page adaptative** : les noms de cartes s'affichent en entier et
  passent à la ligne si nécessaire. Quand la fenêtre devient étroite, la carte
  bascule en disposition verticale et les champs Adresse IP / Masque / Passerelle
  se réorganisent sur deux ou trois colonnes.
- **Passerelle proposée automatiquement** : au passage en adresse fixe,
  l'application propose la première adresse hôte du sous-réseau (par exemple
  `192.168.2.101` / `255.255.255.0` → `192.168.2.1`). La passerelle héritée du
  DHCP suit l'adresse lorsque le sous-réseau change, et la proposition suit
  ensuite l'adresse et le masque tant qu'elle n'a pas été modifiée à la main.
  Une passerelle saisie ou effacée par l'utilisateur n'est jamais écrasée.
- **Pas de relance élevée de toute l'application** : macOS demande
  l'autorisation au moment de chaque changement. La fenêtre reste utilisable
  sans être administrateur.
- **Pas de « DHCP actif + adresse fixe enregistrée »** : macOS ne conserve pas
  d'adresse fixe parallèle au DHCP comme le registre Windows. L'état affiché
  correspond à la configuration réellement active.
- **Activation/désactivation** : macOS désactive le *service* réseau
  (et non le pilote de la carte, qui n'est pas exposé de la même manière).
- Les services VPN/tunnels (`utun*`, PPP, modem) sont filtrés : ils n'ont pas
  d'adressage IPv4 modifiable par cette application.
- Les noms de cartes proviennent des services macOS (« Wi-Fi », « Ethernet »,
  « Thunderbolt Bridge »…), pas des noms de pilotes Windows.

## Validation reprise de la version Windows

- IPv4 sur quatre octets, chaque octet entre 0 et 255.
- `0.0.0.0` et `255.255.255.255` interdites.
- `169.254.x.x` (APIPA) refusée.
- Masque de sous-réseau contigu obligatoire.
- Passerelle différente de l'adresse IP et appartenant au même sous-réseau.
- Champ passerelle facultatif.
- Passerelle suggérée automatiquement depuis l'adresse et le masque ; elle
  reste modifiable ou effaçable.

## Structure du projet

```
mac/
├── Package.swift                 # projet Swift Package (Xcode / swift run)
├── Info.plist                    # bundle des .app générées
├── assets/AppIcon-1024.png       # icône haute résolution
├── scripts/
│   ├── build-mac.sh              # Intel + Apple Silicon (+ universelle)
│   └── make-icns.sh              # PNG → .icns via sips/iconutil
└── Sources/EasyIPSwitch/
    ├── EasyIPSwitchApp.swift     # point d'entrée, fenêtre, confirmation de fermeture
    ├── AppInfo.swift             # nom, version v1.1.2, liens
    ├── AppTheme.swift            # palettes clair/sombre
    ├── Models.swift              # modèles et messages
    ├── IPv4.swift                # validation IPv4
    ├── Shell.swift               # exécution de commandes
    ├── NetworkReader.swift       # lecture SystemConfiguration
    ├── NetworkWriter.swift       # écriture networksetup + autorisation
    ├── PingRunner.swift          # ping et analyse des résultats
    ├── SystemSettings.swift      # ouverture des Réglages Réseau macOS
    ├── AdapterCardModel.swift    # état et validation d'une carte
    ├── AppModel.swift            # logique applicative
    └── Views/
        ├── Components.swift      # boutons, champs, segments, interrupteur
        ├── ContentView.swift     # en-tête, onglets, pied de page
        ├── AdapterCardView.swift # carte réseau
        └── PingView.swift        # test de ping
```

## Liste de vérification sur Mac

À faire lors du premier lancement sur le Mac (impossible depuis Windows) :

- [ ] L'en-tête affiche **Easy IP-Switch v1.1.2**.
- [ ] Les cartes Wi-Fi/Ethernet apparaissent avec leur adresse actuelle.
- [ ] Chaque carte affiche aussi le nom matériel et le périphérique (ex. « Wi-Fi — en0 »).
- [ ] Le passage DHCP → adresse fixe demande l'autorisation admin et s'applique.
- [ ] Une adresse fixe sans passerelle s'applique (champ passerelle vide).
- [ ] Les erreurs de validation affichent un message rouge et le champ concerné.
- [ ] L'activation/désactivation d'un service fonctionne.
- [ ] Le bouton **Tester** d'une carte joint son adresse IPv4 actuelle.
- [ ] L'onglet ping affiche bien « Tester une adresse IP sur le réseau ».
- [ ] Le test de ping vers la passerelle répond (autoriser le réseau local si demandé).
- [ ] Le bouton **Réglages Réseau** ouvre les réglages réseau de macOS.
- [ ] Le thème clair/sombre est mémorisé.
- [ ] La fermeture avec modifications en attente propose les trois choix.
- [ ] Les liens du pied de page (site et don) s'ouvrent dans le navigateur.
- [ ] L'application correspondant à l'architecture du Mac se lance (Intel ou Apple Silicon).
