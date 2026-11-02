# Easy IP-Switch — état du projet et passation vers macOS

**Dernière mise à jour :** 2 novembre 2026 (soir)  
**Version macOS actuelle :** 1.1.2 — release **v1.1.2-macos**  
**Version Windows actuelle :** 1.1.1  
**Projet local Windows :** `P:\Projets IA\Easy IPSwitch`  
**Dépôt GitHub public :** <https://github.com/danijay75/easy-ip-switch> (licence MIT)  
**Cible macOS :** macOS 12 (Monterey) ou plus récent, Intel x86_64 et Apple Silicon arm64  
**Statut général :** Windows 1.1.1 est publié sur GitHub ; macOS **1.1.2** compile, se signe et se lance avec les quatre langues, le sélecteur de drapeaux, le QR code de don et la fenêtre de fermeture. **La release macOS `v1.1.2-macos` est publiée.** Voir la section 22.

---

## 1. Objet de ce document

Ce document sert de **point de reprise complet** pour terminer le portage macOS avec OpenCode sur un Mac.

Il contient :

- l'état exact du projet Windows et macOS ;
- ce qui est terminé, ce qui reste à faire et ce qui bloque ;
- les décisions techniques à respecter ;
- la procédure de bascule d'OpenCode de Windows vers macOS ;
- le prompt à coller dans la première session OpenCode sur le Mac ;
- les commandes de compilation, de signature et de lancement ;
- la checklist de tests manuels ;
- le dépannage des erreurs probables ;
- l'inventaire des fichiers et les critères de fin.

Le projet n'est pas un dépôt Git. Il vit sur pCloud, qui peut produire des fichiers fantômes avec des variantes de casse. **Pour toute compilation, travailler sur une copie locale du dossier `mac`, jamais directement sur pCloud.**

---

## 2. Résumé exécutif

| Élément | État |
|---|---|
| Application Windows v1.1.1 | Terminée et publiée sur GitHub |
| Binaire Windows livré | `dist\easy ip-switch v1.1.1.exe` — 262 656 octets, version `1.1.1.0` |
| Release Windows | GitHub `v1.1.1` avec l’exécutable et son SHA-256 |
| Source macOS SwiftUI | 17 fichiers Swift (2 691 lignes) et 4 fichiers de tests (392 lignes) |
| Projet Swift Package | Présent : `mac/Package.swift`, cible de tests incluse |
| Script de build macOS | Présent : `mac/scripts/build-mac.sh`, version lue dans `Info.plist`, DMG inclus |
| Info.plist | Valide, version `1.1.1`, minimum macOS `12.0` |
| Vérifications statiques | Réussies (voir section 7) |
| Compilation Swift réelle | **Réussie** sur macOS 26.6.2 (Apple Silicon), Swift 6.3.3 |
| Applications `.app` Intel / Apple Silicon | **Produites** en local, plus la version universelle ; zips et DMG dans `mac/dist` |
| Tests automatisés | **34 tests** Swift Testing réussis |
| Interface macOS | **Français uniquement** ; les quatre langues ne sont pas encore portées |
| Prochaine action | Section 22 : résultats de la release macOS 1.1.2 ; prochaine étape éventuelle : validation sur Mac Intel physique et notarisation |

**Conclusion :** Windows 1.1.1 est publié et le portage macOS compile, se signe et se lance. La prochaine étape est la **parité des langues et la release macOS 1.1.2**, décrites en section 21.

---

## 3. Où se trouvent les fichiers

| Chemin | Rôle |
|---|---|
| `dist\easy ip-switch v1.1.exe` | Application Windows finale, à ne pas modifier pour le portage |
| `dist\Easy IP-Switch macOS 1.1.1 source.zip` | Archive de transfert — 37 entrées : sources, tests, scripts et documents |
| `mac\` | Projet macOS complet |
| `mac\README.md` | Compilation, signature, limites, checklist Mac |
| `mac\ETAT-DU-PROJET-MACOS.md` | Le présent document de passation |
| `mac\PLAN-ACTION-MACOS.md` | Plan de validation visuelle, réseau et distribution |
| `mac\Package.swift` | Définition Swift Package, cible macOS 12 et cible de tests |
| `mac\Info.plist` | Métadonnées du bundle `.app` |
| `mac\Sources\EasyIPSwitch\` | Sources SwiftUI, AppKit et SystemConfiguration |
| `mac\Tests\EasyIPSwitchTests\` | 34 tests Swift Testing |
| `mac\scripts\build-mac.sh` | Compilation Intel + Apple Silicon, assemblage, signature, zip |
| `mac\scripts\test.sh` | Exécution des tests avec les Command Line Tools |
| `mac\scripts\make-icns.sh` | Génération de `AppIcon.icns` à partir du PNG 1024×1024 |
| `mac\assets\AppIcon-1024.png` | Icône source |
| `mac\dist\` | Contient les trois zip signés (Intel, Apple Silicon, universelle) |
| `mac\build\` | Absent de pCloud — binaires intermédiaires gardés sur le disque local |

### Archive de transfert

Le zip source contient le dossier `mac/` complet : sources, tests, scripts, icône et documents. Il est préférable au transfert direct du dossier pCloud, car il évite :

- les variantes de casse (`Easy IP-Switch` / `easy ip-switch`) ;
- les fichiers fantômes ;
- les attributs étendus qui peuvent faire échouer `codesign`.

---

## 4. Ce qui est terminé

### 4.1 Application Windows v1.1

- Interface WinForms française complète.
- Cartes réseau, DHCP/adresse fixe, activation/désactivation.
- Boutons **Changer**, **Annuler**, **Tester** par carte.
- Validation IPv4 avec bordures rouges et messages.
- Onglet ping.
- Thème clair/sombre.
- Pied de page avec crédits, site et don.
- Titre et en-tête **Easy IP-Switch v1.1**.
- Binaire final : `dist\easy ip-switch v1.1.exe`, 173 568 octets, version `1.1.0.0`.

Lors de la finalisation Windows, la suite de tests locale avait passé **256 assertions**. Elle n'a pas été rejouée dans la dernière passe de vérification macOS.

### 4.2 Interface macOS

Le portage est une application **SwiftUI native**, sans Electron, sans Avalonia et sans projet Xcode.

Fonctionnalités implémentées :

- en-tête avec le nom **Easy IP-Switch**, le badge **v1.1.1** et le sous-titre français ;
- bouton **Actualiser** et raccourci `⌘R` ;
- cartes réseau avec icône Wi-Fi/Ethernet, nom du service, **nom matériel et périphérique** (par exemple « Wi-Fi — en0 »), état de connexion et interrupteur d'activation ;
- choix **DHCP** / **Adresse fixe** ;
- champs **Adresse IP**, **Masque**, **Passerelle** ;
- validation en direct après une tentative d'application, bordures rouges et message inline ;
- boutons **Tester**, **Annuler**, **Changer** par carte ;
- onglet **Test de ping** avec le titre exact **« Tester une adresse IP sur le réseau »** ;
- sélection de la carte source, adresse cible, journal horodaté, bouton **Effacer** ;
- bouton **Réglages Réseau** avec icône réseau ;
- thème clair/sombre mémorisé dans `UserDefaults` ;
- pied de page avec copyright, lien site et lien de don ;
- fenêtre de fermeture avec rappel de don : QR code PayPal, lien de don, et trois choix selon l'état (appliquer et quitter, quitter sans appliquer, rester) ;
- fenêtre minimale 880×620, taille initiale adaptée à l'écran.

Le titre de la fenêtre et l'en-tête affichent bien **Easy IP-Switch v1.1.1**.

### 4.3 Couche réseau macOS

Lecture, sans privilège :

- `SCPreferences` / `SCNetworkService` pour les services réseau ;
- `SCDynamicStore` pour l'état IPv4 réellement actif ;
- `SCNetworkInterface` pour le nom matériel et le nom BSD (`en0`, `en1`, etc.) ;
- `/sbin/ifconfig` pour l'état du lien ;
- filtrage des tunnels et services non éditables (`utun*`, `ipsec*`, `ppp*`, `gif*`, `stf*`, `awdl*`, `llw*`, `p2p*`, `anpi*`, `ap*`) ;
- conservation des bridges, VLAN et bonds ;
- normalisation de `0.0.0.0` en passerelle vide ;
- état « Connectée » fondé sur le lien, pas sur la seule présence d'une adresse IPv4.

Écriture, avec autorisation administrateur :

- `networksetup -setdhcp` ;
- `networksetup -setmanual` ;
- `networksetup -setnetworkserviceenabled` ;
- exécution via `/usr/bin/osascript` et `do shell script … with administrator privileges` ;
- demande d'autorisation à chaque modification, sans relancer toute l'application en root ;
- lecture et ping sans élévation.

Robustesse :

- relecture après écriture avec plusieurs tentatives pour laisser SystemConfiguration se mettre à jour ;
- conservation du brouillon si la vérification échoue ;
- conservation des autres cartes lors d'une lecture partielle ;
- conservation de la dernière carte observée si une relecture ultérieure l'omet ;
- passerelle vide : premier essai avec un argument vide, puis second essai avec `0.0.0.0` si macOS rejette l'argument.

### 4.4 Construction et distribution

- `Package.swift` : Swift tools 5.7, macOS 12, exécutable `EasyIPSwitch`.
- `assets/donation-qr.png` copié dans `Contents/Resources` pour le rappel de don.
- `Info.plist` :
  - `CFBundleIdentifier` : `com.angelivisions.easyipswitch` ;
  - `CFBundleShortVersionString` : `1.1.1` ;
  - `CFBundleVersion` : `1.1.1` ;
  - `LSMinimumSystemVersion` : `12.0` ;
  - `CFBundleExecutable` : `EasyIPSwitch` ;
  - `CFBundleIconFile` : `AppIcon` ;
  - description d'usage du réseau local.
- `build-mac.sh` :
  - compile x86_64 et arm64 avec `swiftc` ;
  - cible `-apple-macosx12.0` ;
  - mode Swift 5 ;
  - assemble les bundles `.app` ;
  - copie l'icône `.icns` ;
  - retire les attributs étendus avant signature ;
  - signe en ad-hoc (`codesign --sign -`) ;
  - vérifie la signature ;
  - crée les zip avec `ditto` ;
  - option `--universal` avec `lipo`.
- `make-icns.sh` : génère les tailles 16, 32, 128, 256, 512 et leurs versions `@2x`.

### 4.5 Dernières corrections de la passe du 2 novembre 2026

- **Réglages Réseau macOS 12** : utilisation de `com.apple.preference.network` sur Monterey ; `com.apple.Network-Settings.extension` sur macOS 13 et plus récent.
- **Ping** : la touche Entrée dans le champ d'adresse lance le test ; une modification après un échec efface l'état d'erreur.
- **Carte statique sans masque enregistré** : le champ reste vide au chargement et la carte n'apparaît plus artificiellement modifiée. Les valeurs par défaut ne sont proposées que lors du passage DHCP → adresse fixe, comme dans la version Windows.
- Les corrections précédentes sont conservées : relectures multiples, conservation des brouillons, `latestTarget`, repli `0.0.0.0`, titre de ping exact, ligne matériel/périphérique, icône des réglages.

---

## 5. Décisions techniques à respecter

| Sujet | Décision |
|---|---|
| Interface | SwiftUI natif, français, cohérent avec Windows |
| Lecture réseau | SystemConfiguration, pas d'analyse de sortie localisée |
| Écriture réseau | `networksetup` via `osascript` avec autorisation admin |
| Élévation | À chaque modification, pas de relance globale en root |
| Activation/désactivation | Au niveau du service réseau macOS, pas du pilote |
| Configuration fixe en parallèle du DHCP | Non supportée : macOS ne conserve pas cet état comme le registre Windows |
| Passerelle vide | Champ facultatif ; repli `0.0.0.0` à valider sur Mac |
| Signature | Ad-hoc pour usage local ; Developer ID et notarisation documentés dans `README.md` |
| Projet Xcode | Aucun : Swift Package + script bash |
| Dépendances | Aucune dépendance externe |
| Build pCloud | Toujours copier sur le disque local du Mac avant de compiler |
| Casse des fichiers | Éviter pCloud ; le zip source est l'artefact de transfert recommandé |

---

## 6. Ce qui n'est pas encore fait

La compilation, la signature, le lancement et les tests automatisés sont terminés.
Il reste les validations qui exigent une utilisation réelle de l'interface :

1. **Vérifier visuellement** l'en-tête, les cartes, la mise en page adaptative et le pied de page.
2. **Tester les écritures réseau réelles** avec autorisation administrateur : DHCP → adresse fixe, adresse fixe → DHCP.
3. **Confirmer le comportement d'une passerelle vide** et le repli `0.0.0.0`.
4. **Tester le ping depuis l'onglet** et l'autorisation Réseau local de macOS 15+.
5. **Tester l'activation/désactivation** d'un service.
6. **Vérifier les Réglages Réseau** sur macOS 12 et sur macOS 13+.
7. **Vérifier Gatekeeper** et l'ouverture après téléchargement.
8. **Tester sur un Mac Intel physique** (les builds Intel ont été lancés via Rosetta 2).
9. **Mettre à jour ce document et `README.md`** avec les résultats de ces tests.

Le détail, les précautions et l'ordre recommandé figurent dans `PLAN-ACTION-MACOS.md`.

---

## 7. Vérifications statiques déjà effectuées

| Vérification | Résultat |
|---|---|
| Analyse syntaxique Swift | 16 fichiers, aucune erreur de syntaxe (tree-sitter) |
| Délimiteurs `{}`, `()`, `[]` | Équilibrés sur les 16 fichiers |
| Syntaxe bash | `build-mac.sh` et `make-icns.sh` valides (`bash -n`) |
| `Info.plist` | XML valide ; version `1.1.1` ; minimum macOS `12.0` |
| Encodage | UTF-8 sans BOM |
| Fins de ligne | LF pour les sources et scripts |
| Marqueurs de debug | Aucun `TODO`, `FIXME`, `fatalError` ou `preconditionFailure` |
| Archive source | 37 entrées ; 16 sources Swift ; 4 fichiers de tests ; documents inclus |
| Cohérence de l'archive | Tous les fichiers Swift du dossier sont présents |
| Compilation réelle | Réussie : Intel `x86_64`, Apple Silicon `arm64`, universelle |
| Signature | `codesign --verify --deep --strict` valide sur les trois bundles |
| Tests automatisés | 34 tests Swift Testing réussis |
| `mac/dist` | Contient les trois zip signés ; les `.app` restent dans la copie locale de build |
| `mac/build` | Absent de pCloud : binaires intermédiaires gardés sur le disque local du Mac |

**Limite importante :** la compilation a été faite avec les Command Line Tools, mais les tests visuels et les écritures réseau réelles restent à exécuter.

---

## 8. Bascule d'OpenCode de Windows vers macOS

### 8.1 Principe

OpenCode est un client qui travaille sur un **dossier de projet**. La session Windows et l'historique de conversation ne font pas partie du dossier `mac`. La reprise se fait donc avec :

1. le code source ;
2. ce document de passation ;
3. une nouvelle session OpenCode sur le Mac ;
4. le prompt de reprise fourni en section 9.

Les préférences globales d'OpenCode peuvent être recopiées si elles sont personnalisées. Les identifiants de fournisseurs doivent être reconnectés sur le Mac.

### 8.2 Prérequis sur le Mac

- macOS 12 ou plus récent.
- Xcode 14.2 ou plus récent, ou au minimum les **Xcode Command Line Tools**.
- OpenCode V2 (CLI ou application de bureau).
- Un accès réseau pour télécharger les outils et joindre le fournisseur du modèle.
- Un compte administrateur pour les tests de modification réseau.

Vérifications dans le Terminal :

```bash
sw_vers
uname -m
xcode-select -p
xcrun --show-sdk-path --sdk macosx
swiftc --version
```

Si `swiftc` ou le SDK est introuvable :

```bash
xcode-select --install
```

Avec Xcode complet :

```bash
sudo xcodebuild -license accept
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
```

Sur un Mac Apple Silicon, pour exécuter la version Intel :

```bash
softwareupdate --install-rosetta --agree-to-license
```

### 8.3 Installer OpenCode sur le Mac

OpenCode V2 peut être installé par Homebrew :

```bash
brew install anomalyco/tap/opencode-v2
```

ou par le script officiel :

```bash
curl -fsSL https://opencode.ai/v2/install | bash
```

Vérifier :

```bash
opencode --version
```

L'application de bureau OpenCode est aussi disponible pour macOS (Apple Silicon et Intel) sur la page officielle :

```text
https://opencode.ai/v2/docs/
```

Pour le travail de compilation et de terminal, la CLI est recommandée.

### 8.4 Transférer le projet

1. Copier `dist\Easy IP-Switch macOS 1.1 source.zip` sur le Mac, par exemple dans `~/Downloads`.
2. Extraire **sur le disque local**, pas dans un dossier pCloud/Dropbox synchronisé.
3. Vérifier le contenu.

```bash
cd ~/Downloads
unzip "Easy IP-Switch macOS 1.1 source.zip"
cd mac
ls -la
```

Le dossier doit contenir :

```text
Info.plist
Package.swift
README.md
ETAT-DU-PROJET-MACOS.md
assets/
scripts/
Sources/
```

### 8.5 Ouvrir le dossier dans OpenCode

Depuis le Terminal :

```bash
cd ~/Downloads/mac
opencode
```

Ou en passant le dossier en argument :

```bash
opencode ~/Downloads/mac
```

Si le dossier parent `Easy IP-Switch` a été recréé et contient `mac/`, ouvrir le parent et adapter les chemins :

```bash
opencode ~/Downloads/Easy\ IP-Switch
```

Dans la session OpenCode :

1. se connecter au fournisseur de modèle avec `/connect` ;
2. choisir le modèle souhaité ;
3. vérifier que le dossier de travail est bien `mac` (ou le parent contenant `mac`) ;
4. coller le prompt de la section 9.

### 8.6 Reprendre une configuration OpenCode personnalisée (facultatif)

Si la configuration Windows d'OpenCode est personnalisée, copier uniquement les fichiers de configuration utiles :

```text
~/.config/opencode/opencode.json(c)
~/.config/opencode/cli.json
```

Ne pas copier les secrets en clair. Refaire l'authentification du fournisseur sur le Mac avec `/connect`.

Les sessions et l'historique ne sont pas nécessaires : ce document contient le contexte fonctionnel et technique.

### 8.7 Vérifier le service OpenCode en cas de problème

```bash
opencode service status
opencode api get /api/info
```

En cas de blocage du service :

```bash
opencode service restart
```

Les journaux se trouvent sous :

```text
~/.local/share/opencode/log/opencode.log
```

---

## 9. Prompt de reprise à coller dans OpenCode sur le Mac

```text
Tu reprends le portage macOS de Easy IP-Switch v1.1.1.

Le dossier de travail contient le projet SwiftUI et le script de build. Lis d'abord
ETAT-DU-PROJET-MACOS.md et README.md, puis inspecte Package.swift et les 16 sources
Swift. Si le dossier ouvert est la racine du projet, adapte les chemins en conséquence.

Objectif de la session : produire et valider les applications macOS Intel et Apple
Silicon, corriger les erreurs de compilation, puis exécuter la checklist de tests
manuels.

Procède dans cet ordre :
1. Vérifie macOS, l'architecture, Xcode/Command Line Tools, le SDK et swiftc.
2. Lance `bash scripts/build-mac.sh`.
3. Corrige les erreurs de compilation une par une, sans changer le comportement
   Windows ni la langue française de l'interface.
4. Vérifie les deux bundles avec `file`, `codesign --verify --deep --strict` et
   `plutil`.
5. Lance l'application correspondant à l'architecture du Mac.
6. Exécute la checklist de README.md, en particulier : cartes et noms matériels,
   DHCP → adresse fixe, adresse fixe → DHCP, passerelle vide, validation,
   activation/désactivation, Tester, ping, Réglages Réseau, thème, liens du pied
   de page et confirmation de fermeture.
7. Note les résultats réels dans ETAT-DU-PROJET-MACOS.md et README.md.
8. Si tout passe, construis éventuellement la version universelle avec
   `bash scripts/build-mac.sh --universal`.

Contraintes :
- pas de projet Xcode, pas de nouvelle dépendance externe ;
- garder le script bash et la signature ad-hoc pour l'usage local ;
- ne pas modifier le binaire Windows ;
- ne pas déclarer un test réussi sans l'avoir exécuté ;
- ne pas travailler directement sur un volume pCloud/Dropbox ;
- commencer par le build et montrer la première erreur éventuelle.

Après le build, indique-moi :
- le chemin des deux .app et des zip ;
- la version de macOS et de Xcode ;
- le résultat de `file` et `codesign` ;
- les tests passés, échoués ou non testés ;
- la première erreur à corriger s'il en reste.
```

---

## 10. Compiler et lancer

### 10.1 Build standard

Depuis le dossier `mac` :

```bash
bash scripts/build-mac.sh
```

Le script vérifie :

- que la plateforme est Darwin ;
- que `xcode-select -p` fonctionne ;
- que le SDK macOS et `swiftc` sont disponibles.

Il produit :

```text
mac/dist/Easy IP-Switch v1.1.1 (Intel).app
mac/dist/Easy IP-Switch v1.1.1 (Apple Silicon).app
mac/dist/Easy IP-Switch v1.1.1 (Intel).dmg
mac/dist/Easy IP-Switch v1.1.1 (Apple Silicon).dmg
mac/dist/Easy IP-Switch v1.1.1 (Intel).zip
mac/dist/Easy IP-Switch v1.1.1 (Apple Silicon).zip
```

### 10.2 Build universel facultatif

```bash
bash scripts/build-mac.sh --universal
```

Ajoute :

```text
mac/dist/Easy IP-Switch v1.1.1 (Universelle).app
mac/dist/Easy IP-Switch v1.1.1 (Universelle).dmg
mac/dist/Easy IP-Switch v1.1.1 (Universelle).zip
```

### 10.3 Vérifier les bundles

```bash
cd mac/dist

file "Easy IP-Switch v1.1.1 (Intel).app/Contents/MacOS/EasyIPSwitch"
file "Easy IP-Switch v1.1.1 (Apple Silicon).app/Contents/MacOS/EasyIPSwitch"

codesign --verify --deep --strict --verbose=2 "Easy IP-Switch v1.1.1 (Intel).app"
codesign --verify --deep --strict --verbose=2 "Easy IP-Switch v1.1.1 (Apple Silicon).app"

codesign -dv --verbose=4 "Easy IP-Switch v1.1.1 (Apple Silicon).app"
```

Attendu :

- Intel : `Mach-O 64-bit executable x86_64` ;
- Apple Silicon : `Mach-O 64-bit executable arm64` ;
- universel : `Mach-O universal binary with 2 architectures` ;
- signature ad-hoc valide.

### 10.4 Installer depuis un DMG

1. Double-cliquer sur le `.dmg` correspondant au Mac.
2. Glisser **Easy IP-Switch** dans le dossier **Applications**.
3. Éjecter le volume.
4. Lancer l'application depuis **Applications**.

Le DMG contient un raccourci `Applications`, le helper
**Installer sur le Bureau.app**, un fichier `Lisez-moi.txt` et peut être copié
sur n'importe quel Mac compatible.

- **Installer sur le Bureau** copie l'application dans `/Applications` et crée
  un lien `~/Bureau/Easy IP-Switch` vers l'application installée. Un raccourci
  Bureau fixe ne peut pas être livré dans le DMG : chaque compte macOS possède
  un dossier personnel différent. Le helper le crée donc pour la session
  ouverte.

Pour distribuer un seul fichier aux Mac Intel et Apple Silicon, utiliser le DMG
**Universelle**.

### 10.5 Lancer

```bash
open "Easy IP-Switch v1.1.1 (Apple Silicon).app"
```

ou, sur un Mac Intel :

```bash
open "Easy IP-Switch v1.1.1 (Intel).app"
```

Sur un Mac Apple Silicon, la version Intel nécessite Rosetta 2.

### 10.6 Gatekeeper après téléchargement

Si macOS bloque l'application téléchargée :

```bash
xattr -dr com.apple.quarantine "Easy IP-Switch v1.1.1 (Apple Silicon).app"
```

Ou faire un clic droit sur l'application, puis **Ouvrir**.

Une signature ad-hoc ne remplace pas une signature Developer ID. Pour une distribution publique sans avertissement, suivre la section notarisation de `README.md`.

---

## 11. Checklist de tests manuels sur Mac

À exécuter dans l'ordre. Noter chaque résultat dans ce document.

| ID | Test | Résultat attendu |
|---|---|---|
| T01 | `bash scripts/build-mac.sh` | Les deux `.app` et les deux zip sont créés sans erreur |
| T02 | `plutil -p` sur les `Info.plist` | Version `1.1.1`, minimum macOS `12.0`, bundle ID correct |
| T03 | Lancer l'application correspondant au Mac | Fenêtre visible, pas de crash |
| T04 | Titre de fenêtre et en-tête | **Easy IP-Switch v1.1.1** |
| T05 | Cartes réseau | Wi-Fi/Ethernet visibles avec adresse actuelle |
| T06 | Ligne matériel/périphérique | Par exemple « Wi-Fi — en0 » sous le nom |
| T07 | DHCP → adresse fixe | Autorisation admin, configuration appliquée et vérifiée |
| T08 | Adresse fixe → DHCP | Autorisation admin, adresse DHCP relue |
| T09 | Adresse fixe avec passerelle vide | Application réussie, ou repli `0.0.0.0` réussi |
| T10 | Validation IP invalide | Bordure rouge et message sous la carte |
| T11 | Validation masque non contigu | Bordure rouge et message explicite |
| T12 | Validation passerelle hors sous-réseau | Bordure rouge et message explicite |
| T13 | Validation `0.0.0.0`, `255.255.255.255`, `169.254.x.x` | Adresses refusées |
| T14 | Activation/désactivation d'un service | Interrupteur et état mis à jour après relecture |
| T15 | Bouton **Tester** d'une carte | Ping de l'adresse IPv4 actuelle ; passerelle testée si définie |
| T16 | Onglet ping | Titre exact **« Tester une adresse IP sur le réseau »** |
| T17 | Ping vers la passerelle | Réponse ou message d'échec clair ; autorisation Réseau local si demandée |
| T18 | Bouton **Réglages Réseau** | Ouvre les réglages réseau (Mac 12 : panneau Network ; Mac 13+ : extension) |
| T19 | Thème clair/sombre | Bascule immédiate, choix mémorisé après relance |
| T20 | Liens du pied de page | Site et don ouverts dans le navigateur |
| T21 | Fermeture avec modifications | Trois choix : appliquer et quitter, quitter sans appliquer, rester |
| T22 | Annulation de l'autorisation admin | Message « Opération annulée », aucune modification |
| T23 | Actualisation automatique avec brouillon | Le brouillon saisi n'est pas écrasé |
| T24 | Signature et Gatekeeper | `codesign --verify` passe ; ouverture après quarantine |
| T25 | Version universelle (option) | `lipo -info` indique x86_64 et arm64 ; lancement correct |

### Précautions pour les tests réseau

- Noter la configuration actuelle avant toute modification.
- Tester d'abord sur un réseau dont on peut restaurer le DHCP.
- Ne pas tester la passerelle vide sur une connexion indispensable sans avoir un moyen de revenir en DHCP.
- Les modifications demandent le mot de passe administrateur macOS ; l'application ne le stocke pas.
- Ne jamais coller le mot de passe administrateur dans la conversation OpenCode.

---

## 12. Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| `xcrun: error: unable to find utility "swiftc"` | Xcode/CLT absent ou mal sélectionné | `xcode-select --install`, puis `sudo xcode-select -s …` |
| `unable to find SDK "macosx"` | SDK manquant ou licence non acceptée | Installer Xcode/CLT, `sudo xcodebuild -license accept` |
| `swiftc` signale une API indisponible | API introduite après macOS 12 | Remplacer par une API macOS 12 ou protéger par `if #available` |
| `codesign` : resource fork / detritus | Attributs étendus ou volume synchronisé | Compiler sur disque local ; le script fait `xattr -cr` |
| Fichiers en double avec casse différente | pCloud | Utiliser le zip source ou une copie locale |
| L'application Intel ne démarre pas sur Apple Silicon | Rosetta 2 absent | `softwareupdate --install-rosetta --agree-to-license` |
| Gatekeeper bloque l'application | Quarantaine du zip téléchargé | Clic droit → Ouvrir, ou `xattr -dr com.apple.quarantine` |
| Aucune carte détectée | Services désactivés, tunnels filtrés ou lecture échouée | Vérifier Réglages Réseau, actualiser, consulter les logs |
| La passerelle vide est refusée | `networksetup` exige un routeur | Vérifier le repli `0.0.0.0` ; sinon saisir une passerelle |
| Le ping ne répond pas | Autorisation Réseau local, pare-feu ou interface | Autoriser l'application ; tester depuis le Terminal |
| `osascript` renvoie `User canceled` | Autorisation annulée | Comportement attendu ; message d'annulation |
| L'application se ferme avec la fenêtre | `applicationShouldTerminateAfterLastWindowClosed` | Comportement voulu ; la confirmation de quit s'affiche s'il y a des brouillons |
| `lipo` échoue | Un des deux binaires manque | Reconstruire Intel et Apple Silicon avant `--universal` |

### Vérifications utiles

```bash
# Architecture d'un binaire
file "Easy IP-Switch v1.1.1 (Apple Silicon).app/Contents/MacOS/EasyIPSwitch"

# Architecture d'un universel
lipo -info "Easy IP-Switch v1.1.1 (Universelle).app/Contents/MacOS/EasyIPSwitch"

# Métadonnées du bundle
plutil -p "Easy IP-Switch v1.1.1 (Apple Silicon).app/Contents/Info.plist"

# Signature
codesign --verify --deep --strict --verbose=2 "Easy IP-Switch v1.1.1 (Apple Silicon).app"

# Quarantaine
xattr -l "Easy IP-Switch v1.1.1 (Apple Silicon).app"
```

---

## 13. Limites connues et différences avec Windows

1. **Pas de « DHCP actif + adresse fixe enregistrée »** : macOS ne conserve pas une configuration fixe parallèle au DHCP comme le registre Windows. L'application affiche l'état réellement actif.
2. **Activation/désactivation au niveau du service** : macOS ne propose pas la même désactivation du pilote que Windows.
3. **Services VPN/tunnels filtrés** : `utun*`, PPP, modem et interfaces sans IPv4 éditable ne sont pas affichés.
4. **Noms de cartes** : les noms viennent des services macOS (« Wi-Fi », « Ethernet », « Thunderbolt Bridge »…), pas des pilotes Windows.
5. **Autorisation admin par modification** : choix assumé pour garder la lecture et le ping sans privilège.
6. **Passerelle vide** : le comportement exact de `networksetup -setmanual` avec un argument vide et le repli `0.0.0.0` restent à confirmer sur Mac.
7. **Signature ad-hoc** : suffisante pour un usage local, pas pour une distribution publique sans avertissement Gatekeeper.
8. **Pas de notarisation** : à faire avec un compte Apple Developer si nécessaire.
9. **Pas d'édition IPv6** : le portage se concentre sur IPv4, comme la version Windows.
10. **Bridges, VLAN et bonds** : inclus car ils portent une configuration IPv4 éditable, même s'ils ne sont pas des cartes physiques.

---

## 14. Inventaire des fichiers macOS

| Fichier | Rôle |
|---|---|
| `Package.swift` | Swift Package, cible macOS 12 |
| `Info.plist` | Bundle, version, identifiant, icône, réseau local |
| `Sources/EasyIPSwitch/EasyIPSwitchApp.swift` | Point d'entrée `@main`, fenêtre, delegate, confirmation de fermeture |
| `Sources/EasyIPSwitch/AppInfo.swift` | Nom, version, URL du site et du don, copyright |
| `Sources/EasyIPSwitch/AppTheme.swift` | Palettes clair/sombre, `ThemeState`, extension `Color(hex:)` |
| `Sources/EasyIPSwitch/Models.swift` | `AdapterInfo`, modes, messages, `CommandResult`, `PingOutcome` |
| `Sources/EasyIPSwitch/IPv4.swift` | Analyse IPv4, masque contigu, APIPA, adresses interdites, passerelle suggérée |
| `Sources/EasyIPSwitch/Shell.swift` | Exécution de commandes, environnement anglais, quoting POSIX |
| `Sources/EasyIPSwitch/NetworkReader.swift` | Lecture SystemConfiguration, état live, filtrage, lien |
| `Sources/EasyIPSwitch/NetworkWriter.swift` | `networksetup` via `osascript`, autorisation admin, repli passerelle |
| `Sources/EasyIPSwitch/PingRunner.swift` | `/sbin/ping`, analyse des paquets et des temps |
| `Sources/EasyIPSwitch/SystemSettings.swift` | Ouverture des Réglages Réseau macOS 12/13+ |
| `Sources/EasyIPSwitch/AdapterCardModel.swift` | Brouillon, mode, validation, dirty, passerelle automatique |
| `Sources/EasyIPSwitch/AppModel.swift` | Actualisation, application, activation, test, fusion, vérification |
| `Sources/EasyIPSwitch/QuitDialog.swift` | Fermeture, QR code de don, lien PayPal |
| `Sources/EasyIPSwitch/Views/Components.swift` | Boutons, champs, segments, switch, pill, logo |
| `Sources/EasyIPSwitch/Views/ContentView.swift` | En-tête, onglets, cartes, barre d'état, pied de page |
| `Sources/EasyIPSwitch/Views/AdapterCardView.swift` | Carte réseau complète et mise en page adaptative |
| `Sources/EasyIPSwitch/Views/PingView.swift` | Onglet ping, source, cible, journal |
| `Tests/EasyIPSwitchTests/IPv4Tests.swift` | Tests d'analyse, masques et passerelle suggérée |
| `Tests/EasyIPSwitchTests/AdapterCardModelTests.swift` | Tests DHCP/fixe, validation, brouillon, passerelle |
| `Tests/EasyIPSwitchTests/PingRunnerTests.swift` | Tests d'analyse des résultats de ping |
| `Tests/EasyIPSwitchTests/AppInfoTests.swift` | Tests de version, liens et copyright |
| `scripts/build-mac.sh` | Compilation Intel/ARM/universel, bundle, signature, zip |
| `scripts/test.sh` | Tests Swift Testing avec les Command Line Tools |
| `scripts/make-icns.sh` | PNG → `.icns` |
| `assets/AppIcon-1024.png` | Icône source 1024×1024 |
| `README.md` | Guide de compilation, signature et checklist |
| `PLAN-ACTION-MACOS.md` | Plan de validation et de distribution |
| `ETAT-DU-PROJET-MACOS.md` | Le présent document |

### Volumétrie

- 17 fichiers Swift d'application : 2 691 lignes ;
- 4 fichiers de tests : 392 lignes ;
- 31 fichiers sources dans `mac/` (hors `mac/dist`) ;
- 409 521 octets pour l'ensemble des sources, tests, scripts, documents et assets.

---

## 15. Critères de fin

Le portage macOS sera considéré comme terminé lorsque :

- [x] Les `.app` Intel, Apple Silicon et universelle sont produits dans la copie locale de build et se lancent (Intel via Rosetta 2).
- [x] Les trois bundles passent `codesign --verify --deep --strict`.
- [x] Les trois zip sont créés dans `mac/dist` et testés après extraction.
- [ ] La checklist T01–T25 est passée, ou chaque échec est documenté avec une raison.
- [ ] Le comportement de la passerelle vide est confirmé et documenté.
- [ ] Le ping depuis l'interface et l'autorisation Réseau local sont confirmés.
- [ ] Les Réglages Réseau s'ouvrent sur le macOS testé.
- [ ] Le thème clair/sombre est mémorisé.
- [ ] La confirmation de fermeture fonctionne avec les trois choix.
- [ ] Les résultats réels sont ajoutés à ce document et à `README.md`.
- [x] La version universelle est construite.
- [x] Le zip source est régénéré après les changements de sources.
- [x] Aucun avertissement de compilation bloquant ne reste.
- [ ] Le portage est validé sur un Mac Intel physique.

---

## 16. Ce qu'il faut rapporter après la session Mac

Pour permettre une correction rapide, rapporter :

1. version de macOS (`sw_vers`) ;
2. architecture (`uname -m`) ;
3. version de Xcode (`xcodebuild -version`) ou des CLT ;
4. chemin du SDK (`xcrun --show-sdk-path --sdk macosx`) ;
5. sortie complète de `bash scripts/build-mac.sh` en cas d'échec ;
6. **première erreur `swiftc` avec fichier et ligne** ;
7. résultat de `file` sur les deux binaires ;
8. résultat de `codesign --verify` ;
9. tests réussis, échoués ou non exécutés ;
10. capture d'écran de l'interface si un problème visuel est constaté.

Ne pas joindre de mot de passe administrateur, de clé API ou de secret.

---

## 17. Journal des passes

**2 novembre 2026 — passe macOS réelle :** voir la section 20 pour la compilation,
la signature, les tests automatisés, les artefacts produits et les deux améliorations
de la version 1.1.1.

**2 novembre 2026 — vérifications et corrections sans compilation :**

- ajout de la gestion macOS 12 pour le bouton **Réglages Réseau** ;
- ajout de la touche Entrée pour lancer le ping ;
- effacement de l'état d'erreur du ping lors d'une nouvelle saisie ;
- conservation d'un masque vide pour les cartes statiques sans masque enregistré, afin d'éviter un faux état « modification non appliquée » ;
- vérification des 16 sources Swift ;
- vérification des scripts bash ;
- vérification de `Info.plist` ;
- vérification de l'encodage UTF-8/LF ;
- recréation de `dist\Easy IP-Switch macOS 1.1.1 source.zip` avec les dernières sources et les documents.

**Aucune compilation macOS ni exécution réelle n'a été effectuée pendant cette passe.**

---

## 18. Rappel des commandes essentielles

```bash
# Se placer dans le projet
cd ~/Downloads/mac

# Vérifier les outils
sw_vers
uname -m
xcode-select -p
xcrun --show-sdk-path --sdk macosx
swiftc --version

# Compiler Intel + Apple Silicon
bash scripts/build-mac.sh

# Compiler aussi l'universel
bash scripts/build-mac.sh --universal

# Vérifier
file "dist/Easy IP-Switch v1.1.1 (Apple Silicon).app/Contents/MacOS/EasyIPSwitch"
codesign --verify --deep --strict --verbose=2 "dist/Easy IP-Switch v1.1.1 (Apple Silicon).app"

# Lancer
open "dist/Easy IP-Switch v1.1.1 (Apple Silicon).app"

# Lever la quarantaine si nécessaire
xattr -dr com.apple.quarantine "dist/Easy IP-Switch v1.1.1 (Apple Silicon).app"
```

---

## 19. Dernier point de contrôle avant de commencer

> La passe de compilation a déjà été réalisée le 2 novembre 2026 sur macOS 26.6.2.
> Cette liste reste utile pour une nouvelle machine ou une nouvelle session.

- [ ] Le zip source a été copié sur le disque local du Mac.
- [ ] Le dossier a été extrait, pas lancé depuis pCloud.
- [ ] Xcode ou les Command Line Tools sont installés.
- [ ] `swiftc --version` fonctionne.
- [ ] OpenCode est installé et le fournisseur est reconnecté.
- [ ] Le dossier `mac` est ouvert dans OpenCode.
- [ ] Ce document et `README.md` sont lus par la nouvelle session.
- [ ] Le prompt de la section 9 est collé.
- [ ] La configuration réseau actuelle a été notée avant les tests d'écriture.

**Prochaine action concrète :** exécuter la checklist visuelle et réseau de `PLAN-ACTION-MACOS.md`.

---

## 20. Passe réelle sur macOS — 2 novembre 2026

Cette section complète les sections 2, 6, 7 et 17 avec les résultats réels.

### 20.1 Environnement

| Élément | Valeur |
|---|---|
| macOS | 26.6.2 (25G83) |
| Architecture | Apple Silicon arm64 |
| Swift | 6.3.3 |
| Xcode | Command Line Tools uniquement (`/Library/Developer/CommandLineTools`) |
| SDK | `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk` |
| Rosetta 2 | Disponible |

### 20.2 Correction de compilation

Une seule erreur bloquante a été rencontrée :

```text
NetworkReader.swift: cannot find 'kSCPropNetIPv4ConfigMethodDHCP' in scope
NetworkReader.swift: cannot find 'kSCPropNetIPv4ConfigMethodBOOTP' in scope
```

Correction appliquée :

```swift
// avant
dhcp = method == (kSCPropNetIPv4ConfigMethodDHCP as String)
    || method == (kSCPropNetIPv4ConfigMethodBOOTP as String)

// après
dhcp = method == (kSCValNetIPv4ConfigMethodDHCP as String)
    || method == (kSCValNetIPv4ConfigMethodBOOTP as String)
```

Le fichier concerné est `Sources/EasyIPSwitch/NetworkReader.swift`.

### 20.3 Résultats de build

Compilation effectuée depuis une copie locale du dossier `mac`, jamais directement
sur pCloud.

| Bundle | Architecture | Signature | Taille |
|---|---|---|---|
| `Easy IP-Switch v1.1.1 (Intel).app` | `x86_64` | ad-hoc valide | 1,5 Mo |
| `Easy IP-Switch v1.1.1 (Apple Silicon).app` | `arm64` | ad-hoc valide | 1,4 Mo |
| `Easy IP-Switch v1.1.1 (Universelle).app` | `x86_64 + arm64` | ad-hoc valide | 2,5 Mo |

Les trois `.app` et leurs `.zip` ont été produits. `codesign --verify --deep --strict`
passe sur les trois bundles. `plutil` confirme la version `1.1.1`, le minimum macOS
`12.0` et l'identifiant `com.angelivisions.easyipswitch`.

`swift build -c release` passe également via Swift Package Manager.

### 20.4 Résultats de lancement

- La version Apple Silicon se lance et affiche une fenêtre de 1120 × 836.
- La version Intel se lance via Rosetta 2 sur le Mac Apple Silicon.
- La lecture réseau retourne les services attendus, notamment :
  - Wi-Fi (`en0`) — DHCP, 192.0.2.6, masque 255.255.255.0, passerelle 192.0.2.1 ;
  - USB Ethernet (`en7`) — DHCP, 192.0.2.10 ;
  - plusieurs adaptateurs Ethernet et le Thunderbolt Bridge.
- Les adresses locales observées pendant les tests ont été remplacées par des
  adresses de documentation (`192.0.2.0/24`).

### 20.5 Tests automatisés

Une cible `EasyIPSwitchTests` a été ajoutée dans `Package.swift`, avec **34 tests
Swift Testing** répartis en quatre suites :

| Suite | Couverture | Résultat |
|---|---|---|
| Validation IPv4 | analyse, masques, APIPA, adresses interdites | ✅ |
| Modèle de carte réseau | DHCP/fixe, valeurs par défaut, validation, brouillon | ✅ |
| Analyse du ping | statistiques, perte totale, sortie invalide | ✅ |
| Informations application | version, liens, copyright | ✅ |

Commande utilisée sur ce Mac :

```bash
bash scripts/test.sh
```

Résultat :

```text
Test run with 34 tests in 4 suites passed after 0.003 seconds.
```

Le script `scripts/test.sh` ajoute les chemins de `Testing.framework` nécessaires
aux Command Line Tools. `PingRunner.parse` a été rendu interne pour être testable.

### 20.6 Zip extrait et ping réseau

- Les trois `.zip` ont été extraits dans des dossiers temporaires locaux.
- `codesign --verify --deep --strict` passe sur chaque application extraite.
- Les applications Apple Silicon, Intel (via Rosetta) et universelle se lancent ;
  la fenêtre Apple Silicon mesure 1120 × 836.
- L'architecture de chaque binaire extrait correspond au zip annoncé.
- Ping direct vers la passerelle `192.0.2.1` : 3 paquets transmis, 3 reçus,
  0 % de perte, moyenne 1,393 ms.
- Les trois DMG ont été montés en lecture seule, inspectés puis démontés :
  chacun contient l'application `1.1.1`, un lien `Applications → /Applications`,
  le helper `Installer sur le Bureau.app`, ainsi qu'une signature ad-hoc
  valide.
- Le helper **Installer sur le Bureau** a été exécuté depuis le DMG universel :
  l'application est bien copiée dans `/Applications` et le lien
  `~/Bureau/Easy IP-Switch` pointe vers elle.
- Les trois `.zip` et les trois `.dmg` sont copiés dans `mac/dist/` ; les `.app`
  restent dans la copie locale de build.

### 20.7 Passerelle proposée automatiquement

La passerelle est désormais déduite de l'adresse et du masque, comme le demande
la version Windows, mais avec une valeur toujours valide :

- `192.168.2.101` / `255.255.255.0` → `192.168.2.1` ;
- `10.0.0.50` / `255.255.255.0` → `10.0.0.1` ;
- `172.16.5.9` / `255.255.0.0` → `172.16.0.1` ;
- si l'adresse est déjà la première hôte (`192.168.2.1`), la proposition devient
  `192.168.2.2` pour rester différente de l'IP ;
- masque invalide, `/31`, `/32`, adresse APIPA ou interdite → aucune proposition.

Règles de préservation :

- une passerelle héritée du DHCP reste affichée tant qu'elle appartient au
  sous-réseau ; elle est remplacée dès que l'adresse passe dans un autre réseau
  (par exemple `192.168.1.1` devient `192.168.2.1` pour `192.168.2.101`) ;
- une passerelle saisie manuellement n'est jamais écrasée ;
- une passerelle effacée volontairement reste vide, même si l'adresse change ;
- une passerelle encore suggérée suit les modifications d'adresse et de masque ;
- « Annuler » et une actualisation réinitialisent la proposition.

Tests ajoutés : 10, soit **34 tests** au total.

### 20.8 Mise en page adaptative

Les noms de cartes ne sont plus tronqués :

- le nom du service et la ligne matériel/périphérique s'affichent entièrement et
  passent à la ligne si nécessaire ;
- la colonne d'identité s'adapte entre 240 et 340 points ;
- au-dessous de 900 points de largeur de carte, la carte bascule en disposition
  verticale (identité, état, séparateur, configuration) ;
- les champs Adresse IP / Masque / Passerelle passent de trois colonnes à deux,
  puis à une seule, et la passerelle occupe toute la largeur en mode compact ;
- la ligne DHCP / Adresse fixe et son texte d'aide s'empilent quand la largeur
  descend sous 620 points.

Vérifié visuellement à 1120 points (deux colonnes) et à 880 points (disposition
verticale), sur le build Apple Silicon.

### 20.9 Version 1.1.1

Le 2 novembre 2026, la version macOS passe de `1.1` à `1.1.1` pour refléter :

- la correction de compilation SystemConfiguration ;
- la passerelle proposée automatiquement depuis l'adresse et le masque ;
- la prise en charge du changement de sous-réseau pour une passerelle héritée ;
- la mise en page adaptative et l'affichage complet des noms de cartes ;
- les 34 tests automatisés.

Fichiers alignés sur `1.1.1` :

| Fichier | Valeur |
|---|---|
| `Sources/EasyIPSwitch/AppInfo.swift` | `version = "1.1.1"` |
| `Info.plist` | `CFBundleShortVersionString` et `CFBundleVersion` |
| `scripts/build-mac.sh` | version lue depuis `Info.plist` |
| `Tests/EasyIPSwitchTests/AppInfoTests.swift` | `v1.1.1` |

À cette date, l'application Windows était encore en `1.1`. Elle est depuis passée
en `1.1.1` et publiée sur GitHub ; voir la section 21.

### 20.10 Fenêtre de fermeture et don

- `assets/donation-qr.png` a été copié dans `mac/assets/` puis dans
  `Contents/Resources/donation-qr.png` à chaque build.
- Le lien de don PayPal utilise désormais l'identifiant de campagne
  `hosted_button_id=WZBCQXPEHHDDC`, comme la version Windows.
- La fenêtre de fermeture a été vérifiée visuellement : badge, message,
  QR code, lien **Ouvrir la page de don**, boutons **Rester** et **Quitter**.
- Un clic sur le QR code ouvre la page PayPal dans le navigateur.
- La variante avec modifications en attente utilise les mêmes composants avec
  les boutons **Appliquer et quitter**, **Quitter sans appliquer** et
  **Rester**. Un arrêt, une déconnexion ou un redémarrage de macOS ignore
  cette fenêtre.

### 20.11 Ce qui reste à valider

Les tests visuels et les écritures réseau réelles n'ont pas encore été exécutés :

- apparence exacte de l'en-tête, des cartes et du pied de page ;
- ping depuis l'onglet de l'interface et autorisation Réseau local ;
- thème clair/sombre après relance ;
- liens du pied de page ;
- ouverture des Réglages Réseau ;
- confirmation de fermeture avec trois choix ;
- DHCP → adresse fixe ;
- adresse fixe → DHCP ;
- passerelle vide et repli `0.0.0.0` ;
- activation/désactivation d'un service ;
- annulation de l'autorisation administrateur ;
- actualisation automatique avec un brouillon ;
- comportement après quarantaine Gatekeeper ;
- validation sur un Mac Intel physique.

Le plan détaillé, les précautions et les décisions à prendre sont dans
`PLAN-ACTION-MACOS.md`.

---

## 21. Passe du 2 novembre 2026 (soir) — open source, langues et release macOS

Cette section est la référence pour la prochaine session OpenCode sur le Mac.
Elle complète la section 20, qui décrit la première passe de compilation et de
validation macOS.

### 21.1 État consolidé

| Élément | État |
|---|---|
| Application Windows | **1.1.1** finale : responsive, passerelle automatique, 4 langues, drapeaux, QR de don |
| Dépôt GitHub | **public**, licence MIT : <https://github.com/danijay75/easy-ip-switch> |
| Release GitHub Windows | `v1.1.1` : `easy-ip-switch-1.1.1-windows.exe`, 262 656 octets, SHA-256 `18C664A5B3E711FDFE88207F05DDAB52B0BB908A6C4F041BE31073C95A0B1B3A` |
| Landing page | Mise à jour : <https://angelivisions.com/easy-ip-switch> |
| Application macOS | **1.1.1** compilée, signée et lancée sur macOS 26.6.2 ; QR de don et fenêtre de fermeture présents |
| Interface macOS | **Uniquement en français** : la localisation 4 langues n’est pas encore portée |
| Prochaine version macOS | **1.1.2** (langues + sélecteur de drapeaux) |
| Prochaine release GitHub | **`v1.1.2-macos`**, avec les DMG Intel, Apple Silicon et universel |

Ne pas déplacer ni réutiliser le tag Windows `v1.1.1`. La release macOS doit
avoir son propre tag afin que les archives source GitHub correspondent au code
réellement publié.

### 21.2 Ce qui existe déjà côté macOS

La section 20 donne le détail. Points utiles pour la suite :

- `mac/Package.swift` : cible macOS 12, cible de tests `EasyIPSwitchTests`.
- `mac/scripts/build-mac.sh` : compilation Intel, Apple Silicon et universelle,
  assemblage des `.app`, signature ad-hoc, zip et **DMG**.
- `mac/scripts/test.sh` : tests Swift Testing avec les Command Line Tools.
- `mac/Sources/EasyIPSwitch/QuitDialog.swift` : fenêtre de fermeture avec QR
  code, lien PayPal, boutons **Rester** / **Quitter** et variante avec
  modifications en attente.
- `mac/assets/donation-qr.png` : QR de don, copié dans
  `Contents/Resources/donation-qr.png` par le script de build.
- `mac/Sources/EasyIPSwitch/AdapterCardModel.swift` : validation, brouillons,
  passerelle suggérée et messages en français.
- `mac/Sources/EasyIPSwitch/AppModel.swift` : actualisation, application,
  activation, ping et messages de statut en français.
- 34 tests automatisés passent.
- Aucun fichier `Localization.swift` ni sélecteur de langue n’existe encore.
- `AppInfo.version` et `Info.plist` sont en `1.1.1`.

Les chaînes françaises sont réparties principalement dans :

```text
Sources/EasyIPSwitch/AppInfo.swift
Sources/EasyIPSwitch/AppModel.swift
Sources/EasyIPSwitch/AdapterCardModel.swift
Sources/EasyIPSwitch/Models.swift
Sources/EasyIPSwitch/NetworkReader.swift
Sources/EasyIPSwitch/NetworkWriter.swift
Sources/EasyIPSwitch/PingRunner.swift
Sources/EasyIPSwitch/SystemSettings.swift
Sources/EasyIPSwitch/QuitDialog.swift
Sources/EasyIPSwitch/Views/ContentView.swift
Sources/EasyIPSwitch/Views/AdapterCardView.swift
Sources/EasyIPSwitch/Views/PingView.swift
Sources/EasyIPSwitch/Views/Components.swift
```

### 21.3 Objectif fonctionnel macOS

Obtenir la même expérience que Windows 1.1.1 :

1. **Français par défaut** au premier lancement.
2. Langues disponibles : **français, anglais, allemand, espagnol**.
3. Changement de langue **immédiat, sans redémarrage**.
4. Choix mémorisé dans `UserDefaults` et restauré au lancement suivant.
5. Sélecteur compact **à côté du switch clair/sombre**, avec drapeau + code
   (`FR`, `EN`, `DE`, `ES`).
6. Menu déroulant avec **vrais drapeaux** et noms natifs :
   `Français`, `English`, `Deutsch`, `Español`.
7. Tous les textes visibles traduits : en-tête, cartes, validation, ping,
   pied de page, messages de statut, erreurs, fenêtre de fermeture et don.
8. La mise en page responsive et la passerelle automatique existantes sont
   conservées.

### 21.4 Architecture de localisation recommandée

Créer `Sources/EasyIPSwitch/Localization.swift` avec :

```swift
enum AppLanguage: String, CaseIterable, Identifiable {
    case french = "fr"
    case english = "en"
    case german = "de"
    case spanish = "es"

    var id: String { rawValue }
    var displayName: String { ... } // Français, English, Deutsch, Español
    var flagCode: String { ... }    // fr, gb, de, es
    var locale: Locale { ... }      // fr_FR, en_US, de_DE, es_ES
}

@MainActor
final class LanguageStore: ObservableObject {
    static let shared = LanguageStore()
    @Published private(set) var current: AppLanguage

    // Charge UserDefaults, français par défaut.
    // Sauvegarde à chaque changement.
    // t("texte français") et f("format {0}", args) avec repli français.
}
```

Points d’implémentation :

- Clé `UserDefaults` conseillée : `EasyIPSwitch.language`.
- Si la valeur stockée est absente ou invalide, utiliser `.french`.
- Les dictionnaires sont indexés par le **texte français source**. Une clé
  absente dans une langue retombe automatiquement sur le français.
- `f` doit utiliser `String(format:locale:arguments:)` avec la locale de la
  langue active pour les nombres.
- Réutiliser les traductions déjà présentes dans
  `src/Localization.cs` du dépôt : il contient les 226 clés français → anglais,
  allemand, espagnol utilisées par Windows. C’est la référence de traduction.
- Injecter le store dans l’application :

```swift
@StateObject private var language = LanguageStore()

WindowGroup(...) {
    ContentView()
        .environmentObject(model)
        .environmentObject(theme)
        .environmentObject(language)
}
```

- Dans les vues : `@EnvironmentObject private var language: LanguageStore`,
  puis `language.t("...")` / `language.f("...", args)`.
- Dans `AppModel` et `AdapterCardModel`, qui ne sont pas des vues, utiliser
  `LanguageStore.shared` pour les messages générés. Pour éviter de mélanger
  deux langues après un changement, ajouter une méthode
  `languageChanged()` qui efface les messages transitoires et force le
  recalcul des textes d’état.
- `QuitDialog` doit recevoir les chaînes traduites au moment de sa création ;
  ses textes internes de don doivent aussi utiliser `LanguageStore.shared`.
- `AppInfo.CopyrightLine` doit utiliser `LanguageStore.shared.f(...)`.

### 21.5 Drapeaux et sélecteur

1. Copier les drapeaux depuis le dépôt :

```bash
mkdir -p mac/assets/flags
cp assets/flags/fr.png mac/assets/flags/
cp assets/flags/gb.png mac/assets/flags/
cp assets/flags/de.png mac/assets/flags/
cp assets/flags/es.png mac/assets/flags/
```

2. Dans `mac/scripts/build-mac.sh`, à côté de la copie de `donation-qr.png`,
   copier les drapeaux dans le bundle :

```bash
if [ -d "$ROOT/assets/flags" ]; then
    mkdir -p "$app/Contents/Resources/flags"
    cp "$ROOT/assets/flags/"*.png "$app/Contents/Resources/flags/"
fi
```

3. Charger un drapeau avec le même principe que `DonationQR` :

```swift
Bundle.main.url(forResource: code, withExtension: "png", subdirectory: "flags")
```

4. Créer `Views/LanguageSelectorView.swift` :

- `Menu` SwiftUI dans l’en-tête, à gauche de `ThemeSwitch`.
- Affichage fermé : drapeau 24 × 16 + code (`FR`, `EN`, `DE`, `ES`) + chevron.
- Menu : `Label` avec drapeau et nom natif ; l’élément actif est en gras.
- Largeur compacte d’environ 82 points pour tenir à 880 points de large.
- Accessibilité : libellé « Langue » / description « Changer la langue de
  l’application ».

5. Ajouter dans `Info.plist` :

```xml
<key>CFBundleDevelopmentRegion</key>
<string>fr</string>
<key>CFBundleLocalizations</key>
<array>
    <string>fr</string>
    <string>en</string>
    <string>de</string>
    <string>es</string>
</array>
```

### 21.6 Fichiers et textes à traiter

| Fichier | Textes à localiser |
|---|---|
| `Views/ContentView.swift` | sous-titre, pastille d’état, bouton Actualiser, onglets, bouton Réglages Réseau, pied de page, états vides |
| `Views/AdapterCardView.swift` | Carte active, Adresse IP, Masque, Passerelle, DHCP, Adresse fixe, Changer, Annuler, Tester, aides |
| `Views/PingView.swift` | titre exact « Tester une adresse IP sur le réseau », sous-titre, libellés, Ping, Effacer, journal, états |
| `QuitDialog.swift` | titre, message, boutons, textes et aides du QR code / don |
| `AdapterCardModel.swift` | validation IP/masque/passerelle, hints, messages enregistrés, tests |
| `AppModel.swift` | actualisation, application, activation, erreurs, confirmations |
| `NetworkReader.swift` / `NetworkWriter.swift` | erreurs de lecture et d’écriture, messages DHCP |
| `PingRunner.swift` | détails « Réponse en … ms », délais, hôtes inaccessibles |
| `Models.swift` | libellés de type et d’état des cartes |
| `AppInfo.swift` | version et ligne de copyright |
| `EasyIPSwitchApp.swift` | injection du `LanguageStore`, titre de fenêtre |
| `Info.plist` | versions, région, langues |
| `Tests/EasyIPSwitchTests` | nouveaux tests de localisation et versions |

Le sélecteur doit rester accessible sur l’onglet **Cartes réseau** comme sur
l’onglet **Test de ping** ; il fait partie de l’en-tête, pas du contenu des
cartes.

### 21.7 Version macOS 1.1.2

Pour cette release macOS, incrémenter uniquement la version macOS :

- `Sources/EasyIPSwitch/AppInfo.swift` : `version = "1.1.2"`.
- `Info.plist` : `CFBundleShortVersionString` et `CFBundleVersion` = `1.1.2`.
- `Tests/EasyIPSwitchTests/AppInfoTests.swift` : attendre `v1.1.2`.
- `README.md` macOS et ce document : mentionner `1.1.2`.
- Le binaire Windows reste en `1.1.1`.

Le tag de release doit être **`v1.1.2-macos`**, pas `v1.1.1`, afin de ne pas
modifier la release Windows existante et de garder une archive source cohérente.

### 21.8 Tests à ajouter

Créer `Tests/EasyIPSwitchTests/LocalizationTests.swift` et couvrir :

- français par défaut si `UserDefaults` est vide ;
- chargement d’une langue enregistrée (`en`, `de`, `es`) ;
- repli français pour une clé inconnue ;
- traduction d’un texte de chaque écran : en-tête, carte, ping, fermeture ;
- `f` avec arguments et locale (par exemple un nombre) ;
- persistance : changer la langue, recréer le store, retrouver la langue ;
- présence des quatre images de drapeau dans le bundle de test ;
- non-régression des 34 tests existants.

Commande :

```bash
cd mac
bash scripts/test.sh
```

### 21.9 Construire et vérifier les DMG

```bash
cd mac
bash scripts/build-mac.sh --universal
```

Sorties attendues :

```text
mac/dist/Easy IP-Switch v1.1.2 (Intel).dmg
mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).dmg
mac/dist/Easy IP-Switch v1.1.2 (Universelle).dmg
mac/dist/Easy IP-Switch v1.1.2 (Intel).zip
mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).zip
mac/dist/Easy IP-Switch v1.1.2 (Universelle).zip
```

Vérifications :

```bash
file "dist/Easy IP-Switch v1.1.2 (Intel).app/Contents/MacOS/EasyIPSwitch"
file "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app/Contents/MacOS/EasyIPSwitch"
lipo -info "dist/Easy IP-Switch v1.1.2 (Universelle).app/Contents/MacOS/EasyIPSwitch"

codesign --verify --deep --strict --verbose=2 "dist/Easy IP-Switch v1.1.2 (Intel).app"
codesign --verify --deep --strict --verbose=2 "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app"
codesign --verify --deep --strict --verbose=2 "dist/Easy IP-Switch v1.1.2 (Universelle).app"

hdiutil verify "dist/Easy IP-Switch v1.1.2 (Intel).dmg"
hdiutil verify "dist/Easy IP-Switch v1.1.2 (Apple Silicon).dmg"
hdiutil verify "dist/Easy IP-Switch v1.1.2 (Universelle).dmg"
```

Tester aussi un DMG après montage : glisser l’application, la lancer, vérifier
le sélecteur de langue et la fenêtre de fermeture.

### 21.10 Publier la release macOS sur GitHub

Se connecter à GitHub sur le Mac :

```bash
gh auth status
# si nécessaire :
gh auth login
```

Se placer sur la branche `main` à jour, avec le commit des langues :

```bash
git status
git pull --ff-only
```

Créer la release et joindre les trois DMG :

```bash
gh release create v1.1.2-macos \
  "mac/dist/Easy IP-Switch v1.1.2 (Intel).dmg" \
  "mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).dmg" \
  "mac/dist/Easy IP-Switch v1.1.2 (Universelle).dmg" \
  --title "Easy IP-Switch v1.1.2 (macOS)" \
  --notes-file /tmp/easy-ip-switch-v1.1.2-macos.md \
  --target main
```

Notes de release à inclure :

- macOS 12 ou plus récent ;
- trois architectures : Intel, Apple Silicon, universelle ;
- version 1.1.2 ;
- quatre langues et sélecteur de drapeaux ;
- QR code de don dans la fenêtre de fermeture ;
- signature ad-hoc : Gatekeeper peut demander un clic droit → **Ouvrir** ;
- SHA-256 des trois DMG.

Calculer les empreintes :

```bash
shasum -a 256 "mac/dist/Easy IP-Switch v1.1.2 (Intel).dmg" \
              "mac/dist/Easy IP-Switch v1.1.2 (Apple Silicon).dmg" \
              "mac/dist/Easy IP-Switch v1.1.2 (Universelle).dmg"
```

Vérifier la release :

```bash
gh release view v1.1.2-macos
```

Ne pas ajouter les DMG à la release Windows `v1.1.1`. Si l’équipe veut ensuite
une version unique `1.1.2` pour Windows et macOS, elle pourra aligner Windows
et créer un tag commun ; ce n’est pas l’objectif de cette passe.

La landing page <https://angelivisions.com/easy-ip-switch> pointe encore vers
les DMG 1.1.1. Sa mise à jour est gérée séparément, depuis le dépôt du site
sur le poste Windows ; la session macOS ne doit pas modifier le site.

### 21.11 Règles Git et confidentialité

- Le dépôt est **public** : ne jamais committer de secret, mot de passe, token,
  clé privée, adresse IP locale réelle ou donnée personnelle.
- `mac/dist/` et `mac/build/` sont ignorés par `.gitignore` ; ne pas les forcer.
- Les DMG et zip sont des **assets de release**, pas des fichiers du dépôt.
- Travailler sur une branche, par exemple `feature/macos-languages`, puis
  ouvrir une pull request. Fusionner seulement après tests et build réussis.
- Les adresses IP des tests doivent rester dans des plages de documentation
  (`192.0.2.0/24`) ou des exemples génériques.

Workflow conseillé :

```bash
git clone https://github.com/danijay75/easy-ip-switch.git
cd easy-ip-switch
git checkout -b feature/macos-languages

# modifier, tester, construire
cd mac
bash scripts/test.sh
bash scripts/build-mac.sh --universal
cd ..

git add .
git commit -m "feat(macos): ajouter les langues et le sélecteur de drapeaux"
git push -u origin feature/macos-languages
gh pr create --base main --head feature/macos-languages \
  --title "macOS : langues FR/EN/DE/ES et release 1.1.2" \
  --body "Portage des quatre langues, sélecteur avec drapeaux, tests et build DMG."
```

Après fusion :

```bash
git checkout main
git pull --ff-only
gh release create v1.1.2-macos ... # commande de la section 21.10
```

### 21.12 Prompt de reprise à coller dans OpenCode sur le Mac

```text
Tu reprends Easy IP-Switch macOS dans le dépôt public :
https://github.com/danijay75/easy-ip-switch

Lis d'abord mac/ETAT-DU-PROJET-MACOS.md, en particulier la section 21,
puis mac/README.md et mac/PLAN-ACTION-MACOS.md.

Objectif : porter sur macOS les quatre langues déjà présentes dans Windows
(français, anglais, allemand, espagnol), avec un sélecteur de drapeaux dans
l'en-tête, sans redémarrage. Français par défaut, choix mémorisé. Ensuite,
construire les DMG Intel, Apple Silicon et universel, puis publier une release
GitHub macOS 1.1.2.

Contraintes :
- ne pas modifier le binaire Windows ni sa release v1.1.1 ;
- ne pas committer mac/dist, mac/build, secrets ou données personnelles ;
- conserver la mise en page responsive, la passerelle automatique et le QR de don ;
- utiliser les traductions de src/Localization.cs comme référence ;
- tester sur le Mac avant de publier ;
- créer le tag v1.1.2-macos et joindre les trois DMG à la release.

Procède dans cet ordre :
1. Vérifier Xcode/CLT, swiftc, gh auth status et l'état git.
2. Créer une branche feature/macos-languages.
3. Ajouter Localization.swift, LanguageStore, les drapeaux et le sélecteur.
4. Traduire tous les écrans et messages, puis ajouter les tests.
5. Passer bash scripts/test.sh.
6. Passer bash scripts/build-mac.sh --universal.
7. Vérifier architectures, signatures et DMG.
8. Fusionner la branche, puis publier v1.1.2-macos avec les trois DMG.
9. Mettre à jour ce document avec les résultats réels et les SHA-256.

Rapporte : version macOS, résultat des tests, chemins des DMG, résultat de
codesign/hdiutil, URL de la release et toute erreur restante.
```

### 21.13 Définition de fin

La passe macOS sera terminée lorsque :

- [ ] Les quatre langues sont disponibles et le français est la langue par défaut.
- [ ] Le changement de langue est immédiat et persiste après relance.
- [ ] Le sélecteur avec drapeaux est dans l’en-tête, à côté du thème.
- [ ] Tous les écrans et messages visibles sont traduits.
- [ ] Les tests existants et les nouveaux tests de localisation passent.
- [ ] Les DMG Intel, Apple Silicon et universel sont construits et vérifiés.
- [ ] La release `v1.1.2-macos` est publiée avec les trois DMG et les SHA-256.
- [ ] Ce document et `mac/README.md` sont mis à jour avec les résultats réels.
- [ ] Aucune donnée privée ni binaire n’a été ajouté au dépôt public.

---

## 22. Passe du 2 novembre 2026 (nuit) — langues et release 1.1.2

Cette section remplace l'objectif de la section 21 par les résultats réels.

### 22.1 Résultat

| Élément | État |
|---|---|
| Version macOS | **1.1.2** |
| Langues | **Français, English, Deutsch, Español** |
| Langue par défaut | Français |
| Sélecteur | Drapeau + code dans l'en-tête, à côté du thème |
| Changement de langue | Immédiat, sans redémarrage |
| Persistance | `UserDefaults`, clé `EasyIPSwitch.language` |
| Tests automatisés | **42 tests** Swift Testing, 5 suites, réussis |
| DMG | Intel, Apple Silicon et universel en `1.1.2` |
| Release GitHub | **`v1.1.2-macos`** — <https://github.com/danijay75/easy-ip-switch/releases/tag/v1.1.2-macos> |

### 22.2 Fichiers ajoutés ou modifiés

Ajouts :

```text
Sources/EasyIPSwitch/Localization.swift
Sources/EasyIPSwitch/LocalizationCatalog.swift
Sources/EasyIPSwitch/LocalizationExtras.swift
Sources/EasyIPSwitch/Views/LanguageSelectorView.swift
Tests/EasyIPSwitchTests/LocalizationTests.swift
assets/flags/fr.png, gb.png, de.png, es.png
```

Modifications principales :

- `Info.plist` : version `1.1.2`, `CFBundleLocalizations` fr/en/de/es.
- `AppInfo.swift` : version `1.1.2`, copyright localisé.
- `AppModel.swift`, `AdapterCardModel.swift`, `Models.swift`,
  `NetworkReader.swift`, `NetworkWriter.swift`, `PingRunner.swift` : messages
  localisés et nettoyage des messages transitoires au changement de langue.
- `ContentView.swift`, `AdapterCardView.swift`, `PingView.swift`,
  `Components.swift`, `QuitDialog.swift` : textes et aides localisés ;
  sélecteur ajouté à l'en-tête.
- `EasyIPSwitchApp.swift` : injection du `LanguageStore`, messages de fermeture
  localisés.
- `scripts/build-mac.sh` : copie des drapeaux dans
  `Contents/Resources/flags`.
- `README.md`, `PLAN-ACTION-MACOS.md` : version et langues.

### 22.3 Vérifications

```text
Test run with 42 tests in 5 suites passed.
```

- Compilation Intel x86_64, Apple Silicon arm64 et universelle.
- `codesign --verify --deep --strict` valide sur les trois `.app`.
- `hdiutil verify` valide sur les trois DMG.
- DMG universel monté : application `1.1.2`, QR code, drapeaux et helper
  **Installer sur le Bureau** présents.
- Interface vérifiée en français, anglais et espagnol ; l'allemand est couvert
  par les tests de traduction.
- Le sélecteur reste dans l'en-tête ; le rappel « Autorisation admin. » est
  masqué sous 940 points pour laisser la place aux drapeaux.

### 22.4 SHA-256 des DMG

```text
fe7a55393c5f910ca10cc30544ff54895416c41406f4fc87aff071d655b1a6f2  Easy IP-Switch v1.1.2 (Intel).dmg
92af7f5dd2ada7aeb6610137760070fcf9feb5b70e2b0ffc3df6bc52b97d4806  Easy IP-Switch v1.1.2 (Apple Silicon).dmg
f18e7f3331bfa4416e381124a455cec301e5caa7eb3198342aec29a4f8cae7dc  Easy IP-Switch v1.1.2 (Universelle).dmg
```

### 22.5 Publication

La release **`v1.1.2-macos`** contient :

- `Easy IP-Switch v1.1.2 (Intel).dmg` ;
- `Easy IP-Switch v1.1.2 (Apple Silicon).dmg` ;
- `Easy IP-Switch v1.1.2 (Universelle).dmg`.

La signature reste ad-hoc : sur un Mac où le DMG a été téléchargé, Gatekeeper
peut demander un clic droit → **Ouvrir**. La release Windows `v1.1.1` n'a pas
été modifiée.
