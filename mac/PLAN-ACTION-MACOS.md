# Plan d'action macOS — Easy IP-Switch v1.1.2

**Date :** 2 novembre 2026
**Machine de validation :** Mac Apple Silicon, macOS 26.6.2 (25G83), Swift 6.3.3, Xcode Command Line Tools
**Objectif :** terminer la validation des builds Intel et Apple Silicon, puis préparer la distribution.

---

## 1. Ce qui est déjà fait sur ce Mac

| Étape | État | Détail |
|---|---|---|
| Outils macOS vérifiés | ✅ | `swiftc` 6.3.3, SDK macOS, `codesign`, Rosetta 2 |
| Erreur de compilation corrigée | ✅ | `NetworkReader.swift` : `kSCProp…` → `kSCValNetIPv4ConfigMethod…` |
| Build Intel x86_64 | ✅ | `Mach-O 64-bit executable x86_64` |
| Build Apple Silicon arm64 | ✅ | `Mach-O 64-bit executable arm64` |
| Build universel | ✅ | `x86_64 + arm64` via `lipo` |
| Signature ad-hoc | ✅ | `codesign --verify --deep --strict` valide sur les 3 bundles |
| `Info.plist` | ✅ | Version `1.1.2`, minimum macOS `12.0`, bundle ID correct |
| Lancement Apple Silicon | ✅ | Application lancée, fenêtre 1120 × 836 détectée |
| Lancement Intel | ✅ | Lancée via Rosetta 2 sur Apple Silicon |
| Lecture réseau | ✅ | 6 services lus, dont Wi-Fi `en0` et USB Ethernet `en7` |
| Swift Package Manager | ✅ | `swift build -c release` réussi |
| Tests automatisés | ✅ | 34 tests Swift Testing, 4 suites, tous réussis |
| Zip extrait et relancé | ✅ | Extraction locale, signature valide, fenêtre 1120 × 836 |
| Ping réseau | ✅ | 3/3 paquets vers la passerelle, 1,393 ms de moyenne |

Les `.zip` et les `.dmg` sont livrés dans `mac/dist/`. Les `.app` restent dans la
copie locale pour éviter de synchroniser des bundles signés via pCloud :

```text
mac/dist/
├── Easy IP-Switch v1.1.2 (Apple Silicon).dmg
├── Easy IP-Switch v1.1.2 (Apple Silicon).zip
├── Easy IP-Switch v1.1.2 (Intel).dmg
├── Easy IP-Switch v1.1.2 (Intel).zip
├── Easy IP-Switch v1.1.2 (Universelle).dmg
└── Easy IP-Switch v1.1.2 (Universelle).zip

~/EasyIPSwitch-mac-build/dist/
├── Easy IP-Switch v1.1.2 (Apple Silicon).app
├── Easy IP-Switch v1.1.2 (Intel).app
└── Easy IP-Switch v1.1.2 (Universelle).app
```

Chaque DMG contient l'application, un raccourci vers **Applications** et le
helper **Installer sur le Bureau.app** (copie dans `/Applications` et crée un
raccourci sur le Bureau du compte connecté).

> Le projet source reste sur pCloud. La compilation a été faite sur une copie locale,
> conformément à la procédure de passation.

---

## 2. Phase A — Validation locale sans modification réseau

**Risque : aucun.** Cette phase ne change pas la configuration réseau.

| ID | Action | Commande / méthode | État |
|---|---|---|---|
| A1 | Choisir l'emplacement de livraison des artefacts | `.zip` copiés dans `mac/dist/`, `.app` gardés en local | ✅ |
| A2 | Revalider les bundles | `file`, `lipo -info`, `codesign --verify --deep --strict`, `plutil -p` | ✅ |
| A3 | Lancer l'app Apple Silicon | `open "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app"` | ✅ |
| A4 | Vérifier l'en-tête et le titre | **Easy IP-Switch v1.1.2**, sous-titre français | ⏳ visuel |
| A5 | Vérifier les cartes | Wi-Fi/Ethernet, adresse actuelle, « Wi-Fi — en0 » | ⏳ visuel |
| A6 | Tester le ping depuis l'onglet | Adresse de la passerelle, autorisation Réseau local si demandée | ⏳ visuel |
| A7 | Tester le thème clair/sombre | Bascule + relance de l'app | ⏳ visuel |
| A8 | Tester les liens du pied de page | Site et don dans le navigateur | ⏳ visuel |
| A9 | Tester **Réglages Réseau** | Ouvre Réglages Système → Réseau | ⏳ visuel |
| A10 | Tester la confirmation de fermeture | Vérifier le QR code de don, le lien et les 3 choix | ⏳ visuel |
| A11 | Ajouter des tests Swift | 34 tests `IPv4`, `AdapterCardModel`, `PingRunner`, `AppInfo` | ✅ |
| A12 | Tester un zip après extraction | Extraction locale, signature valide, lancement OK | ✅ |
| A13 | Documenter les résultats | `ETAT-DU-PROJET-MACOS.md`, `README.md`, ce plan | ✅ |

### Tests automatisés ajoutés

La cible `EasyIPSwitchTests` est dans `Package.swift` et utilise **Swift Testing**.
Elle couvre :

- `IPv4.parse` : adresses valides, octets > 255, format incomplet, espaces ;
- `IPv4.isValidMask` : masques contigus et non contigus ;
- `IPv4.isAPIPA` / `isForbidden` ;
- `AdapterCardModel` : passage DHCP → fixe, valeurs par défaut, `isDirty`,
  validation IP/masque/passerelle, passerelle hors sous-réseau, conservation du
  brouillon, réinitialisation, **suggestion automatique de passerelle** ;
- `PingRunner.parse` : statistiques complètes, perte totale, sortie sans statistiques ;
- `AppInfo` : version, liens, copyright.

Passerelle suggérée : `192.168.2.101` / `255.255.255.0` → `192.168.2.1`. Une
passerelle héritée du DHCP suit un changement de sous-réseau ; une valeur saisie
manuellement n'est jamais écrasée ; une valeur effacée reste vide.

Commande sur ce Mac (Command Line Tools seuls) :

```bash
bash scripts/test.sh
```

Le script ajoute les chemins de `Testing.framework` nécessaires aux CLT. Avec Xcode
complet, `swift test` fonctionne directement. Le framework Swift Testing est construit
pour macOS 14 : les tests nécessitent donc un Mac récent, mais **l'application reste
ciblée macOS 12**.

---

## 3. Phase B — Tests réseau réels

**Risque : modification de la configuration réseau. Autorisation administrateur requise.**
Ne jamais saisir le mot de passe administrateur dans la conversation OpenCode : macOS
affiche lui-même la boîte de dialogue.

### Prérequis obligatoires

1. Noter la configuration actuelle de l'interface de test.
2. Utiliser une interface **de secours** (USB Ethernet, Thunderbolt Bridge ou adaptateur
   non utilisé), jamais le Wi-Fi qui porte la session si l'accès est distant.
3. Avoir un moyen de revenir en DHCP : câble réseau actif, autre Mac, ou accès physique.
4. Ne pas tester la passerelle vide sur une connexion indispensable.

### Ordre des tests

| ID | Test | Interface conseillée | État |
|---|---|---|---|
| B1 / T07 | DHCP → adresse fixe | Interface de secours | ⏳ |
| B2 / T08 | Adresse fixe → DHCP | La même interface | ⏳ |
| B3 / T09 | Adresse fixe sans passerelle | Interface de secours | ⏳ |
| B4 / T14 | Désactiver puis réactiver un service | Interface non utilisée | ⏳ |
| B5 / T22 | Annuler l'autorisation admin | Interface de secours | ⏳ |
| B6 / T23 | Actualisation auto avec brouillon | Interface de secours | ⏳ |
| B7 / T15 | Bouton **Tester** d'une carte | Interface active | ⏳ |
| B8 / T17 | Ping vers la passerelle | Wi-Fi ou Ethernet | ⏳ |

### Précautions

- Le bouton **Changer** applique immédiatement la configuration ; vérifier deux fois
  l'adresse, le masque et la passerelle avant de valider.
- T14 peut couper l'accès réseau de l'interface choisie.
- T22 doit être testé en cliquant sur **Annuler** dans la boîte macOS, pas en quittant
  l'application.
- En cas d'échec, revenir en DHCP avec `networksetup` ou les Réglages Système avant
  de continuer.

---

## 4. Phase C — Distribution

| ID | Action | Détail | État |
|---|---|---|---|
| C1 | Créer un DMG par architecture | DMG HFS+ compressés avec raccourci Applications, générés par `build-mac.sh` | ✅ |
| C2 | Tester la quarantaine | Télécharger le DMG, l'ouvrir, vérifier Gatekeeper | ⏳ |
| C3 | Signature Developer ID | Compte Apple Developer + certificat | ⏳ optionnel |
| C4 | Notarisation | `notarytool submit` + `stapler` | ⏳ optionnel |
| C5 | Régénérer le zip source | Inclure `PLAN-ACTION-MACOS.md`, les tests et les corrections | ✅ |
| C6 | Vérifier les versions | `1.1.2` dans `Info.plist`, `AppInfo.swift` et les tests | ✅ |

Pour un usage local, la signature ad-hoc actuelle suffit. Pour une diffusion publique
sans avertissement Gatekeeper, il faut un certificat **Developer ID Application** et
une notarisation.

---

## 5. Phase D — Industrialisation

| ID | Action | Détail |
|---|---|---|
| D1 | Initialiser Git | `git init`, `.gitignore` pour `mac/build`, `mac/dist`, `.DS_Store` |
| D2 | CI macOS | GitHub Actions `macos-latest` : build universel + tests |
| D3 | Script de release | Version, build, zip, DMG, notes de version |
| D4 | Vérification pCloud | Toujours compiler depuis une copie locale |

---

## 6. Ordre recommandé

1. **Maintenant** : A1 à A4 (livraison des artefacts, lancement, vérification visuelle).
2. **Ensuite** : A5 à A13 (tests sans risque et tests Swift).
3. **Avec accord explicite** : Phase B (modifications réseau).
4. **Quand les tests sont verts** : Phase C (DMG, quarantaine, notarisation).
5. **Enfin** : Phase D (Git, CI, release).

---

## 7. Commandes utiles

```bash
# Se placer dans le dossier macOS
cd mac

# Compiler Intel + Apple Silicon
bash scripts/build-mac.sh

# Compiler aussi l'universel
bash scripts/build-mac.sh --universal

# Vérifier les architectures
file "dist/Easy IP-Switch v1.1.2 (Intel).app/Contents/MacOS/EasyIPSwitch"
file "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app/Contents/MacOS/EasyIPSwitch"
lipo -info "dist/Easy IP-Switch v1.1.2 (Universelle).app/Contents/MacOS/EasyIPSwitch"

# Vérifier la signature
codesign --verify --deep --strict --verbose=2 "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app"

# Vérifier le bundle
plutil -p "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app/Contents/Info.plist"

# Lancer
open "dist/Easy IP-Switch v1.1.2 (Apple Silicon).app"

# Tests Swift après ajout de la cible
swift test
```

---

## 8. Décisions à prendre

1. **Artefacts** : livrer les `.zip` dans `mac/dist/` ou tout garder en local ?
2. **Interface de test** : quelle interface peut être modifiée sans risque ?
3. **Distribution** : usage local uniquement, ou signature Developer ID + notarisation ?
4. **Tests Swift** : souhaitez-vous que je les ajoute maintenant ?

Tant que ces points ne sont pas tranchés, les phases A1, A11 et B ne peuvent pas
être finalisées.
