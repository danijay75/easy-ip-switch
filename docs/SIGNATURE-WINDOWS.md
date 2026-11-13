# Signature Windows — Easy IP-Switch

## État actuel

- L'exécutable Windows **1.1.3** est **non signé** (`NotSigned`).
- Le scan Microsoft Defender du binaire publié ne détecte aucune menace.
- Les avertissements viennent de **SmartScreen** et de la réputation du
  fichier, pas d'une détection antivirus.
- Windows peut afficher « Windows a protégé votre PC » ou « Éditeur
  inconnu ». Chaque nouvelle version non signée repart de zéro.

La signature de code est la seule solution durable. Elle ne supprime pas
forcément l'avertissement dès le premier jour : la réputation se construit
avec le temps, sur une identité de signature stable.

## Option recommandée — Azure Artifact Signing

Azure Artifact Signing (anciennement Trusted Signing) est un service
Microsoft entièrement géré :

- environ **9,99 $/mois HT** pour l'offre Basic ;
- pas de token matériel ni de clé privée à stocker ;
- certificat au nom de l'organisation validée (**Angeli Visions**) ;
- horodatage RFC 3161 ;
- disponible pour les organisations de l'Union européenne.

### Prérequis

1. Un abonnement Azure et un tenant Microsoft Entra ID.
2. Une validation d'identité de l'organisation Angeli Visions (documents
   légaux, adresse, historique d'activité). La validation se fait dans le
   portail Azure et peut prendre quelques jours.
3. Le Windows SDK, pour disposer de `signtool.exe`.
4. La bibliothèque cliente Artifact Signing (`Azure.CodeSigning.Dlib.dll`),
   fournie par Microsoft.
5. Pour la signature interactive : Azure CLI (`az login`) ou une identité
   managée. La dlib utilise `DefaultAzureCredential`.

### Mise en place dans le portail Azure

1. Créer un compte **Artifact Signing** dans une région européenne
   (par exemple West Europe).
2. Créer une **validation d'identité** de type organisation pour Angeli
   Visions, puis attendre son approbation.
3. Créer un **profil de certificat** de type **Public Trust** rattaché à
   cette validation. Le sujet du certificat reprend le nom légal validé.
4. Attribuer à l'utilisateur ou au principal de service le rôle de
   signataire du profil de certificat (*Certificate Profile Signer*).
5. Récupérer le point de terminaison régional, par exemple
   `https://weu.codesigning.azure.net/`.

### Métadonnées de signature

Créer un fichier `packaging/windows/artifact-signing-metadata.json` à
partir de l'exemple :

```json
{
  "Endpoint": "https://weu.codesigning.azure.net/",
  "CodeSigningAccountName": "votre-compte-artifact-signing",
  "CertificateProfileName": "easy-ip-switch",
  "ExcludeCredentials": []
}
```

Ce fichier ne contient pas de secret. Il peut être versionné, ou conservé
localement si les noms de ressources ne doivent pas être publics.

### Signer et vérifier

Avec Azure CLI connecté :

```powershell
az login

.\build.ps1 `
  -Sign `
  -SignMode Azure `
  -AzureDlib "C:\outils\Azure.CodeSigning.Dlib.dll" `
  -AzureMetadata ".\packaging\windows\artifact-signing-metadata.json" `
  -Package `
  -WebsiteDirectory "C:\Users\danij\Projets IA\angeli-visions-website\public\downloads\easy-ip-switch" `
  -WebsiteReleaseJson "C:\Users\danij\Projets IA\angeli-visions-website\app\easy-ip-switch\release.json"
```

Ou signer un binaire déjà compilé :

```powershell
.\sign.ps1 `
  -Path ".\dist\easy ip-switch v1.1.3.exe" `
  -Mode Azure `
  -AzureDlib "C:\outils\Azure.CodeSigning.Dlib.dll" `
  -AzureMetadata ".\packaging\windows\artifact-signing-metadata.json"
```

Vérification manuelle :

```powershell
signtool verify /pa /v ".\dist\easy ip-switch v1.1.3.exe"
Get-AuthenticodeSignature ".\dist\easy ip-switch v1.1.3.exe" |
  Format-List Status, StatusMessage, SignerCertificate
```

Le script `sign.ps1` vérifie automatiquement la signature et refuse un
résultat non valide.

## Option de repli — certificat PFX ou magasin Windows

### PFX (Certum, par exemple)

```powershell
.\build.ps1 `
  -Sign `
  -SignMode Pfx `
  -PfxPath "C:\certs\angeli-visions.pfx" `
  -PfxPassword "mot-de-passe" `
  -Package
```

Le mot de passe passé en paramètre peut apparaître dans l'historique
PowerShell. Pour un poste partagé, préférer une invite ou un coffre de
secrets.

### Certificat installé dans le magasin Windows

```powershell
.\build.ps1 `
  -Sign `
  -SignMode Store `
  -Thumbprint "ABCDEF0123456789..." `
  -Package
```

## Procédure de remplacement de la version 1.1.3

La version 1.1.3 ne change pas de fonctionnalités : on remplace le binaire
et tous ses hashes, sans créer une 1.1.4.

1. Signer l'exécutable avec l'identité Angeli Visions.
2. Vérifier la signature et l'horodatage.
3. Regénérer le ZIP, le README et `SHA256SUMS.txt` avec `-Package`.
4. Remplacer l'asset Windows de la release GitHub `v1.1.3` par le nouvel
   exécutable signé, et ajouter le ZIP et `SHA256SUMS.txt`.
5. Copier l'exécutable signé et `SHA256SUMS.txt` dans
   `public/downloads/easy-ip-switch/`.
6. Mettre à jour `app/easy-ip-switch/release.json` (le script le fait avec
   `-WebsiteReleaseJson`).
7. Mettre à jour la section « Avertissement Windows » du site : une fois
   le binaire signé, elle doit indiquer l'éditeur **Angeli Visions** et ne
   plus présenter la signature comme à venir.
8. Redéployer le site et vérifier les quatre langues, les liens et les
   empreintes.

## Règles à ne pas enfreindre

- **Même identité de signature** pour toutes les releases. Changer de
  certificat remet la réputation à zéro.
- **Horodater chaque signature** (RFC 3161) pour qu'elle reste valide après
  l'expiration du certificat.
- **Ne jamais publier deux binaires différents sous le même numéro de
  version.** Si un binaire est remplacé, remplacer aussi le site, GitHub,
  les hashes et les notes de release.
- **Ne pas publier de version non signée** une fois la signature en place.
  Une version non signée ne peut pas hériter de la réputation.
- **Ne pas utiliser un certificat auto-signé** : il ne résout pas
  SmartScreen et dégrade la confiance.

## Notes

- Un certificat EV n'est plus nécessaire pour SmartScreen : depuis 2024,
  il n'apporte plus de contournement immédiat. Un certificat OV standard
  suffit.
- Microsoft Store est le seul canal qui évite SmartScreen à l'installation,
  mais l'application demande `requireAdministrator` ; MSIX avec élévation
  est une capacité restreinte, peu adaptée à cet utilitaire. La distribution
  Store Win32 exigerait un installeur signé et tous les PE signés.
- Le ZIP et le README sont une mesure d'attente : ils aident les
  navigateurs et donnent la procédure de vérification, mais ne remplacent
  pas la signature.

## Références

- Azure Artifact Signing : <https://learn.microsoft.com/azure/artifact-signing/>
- SmartScreen et réputation : <https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation>
- Options de signature Windows : <https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options>
