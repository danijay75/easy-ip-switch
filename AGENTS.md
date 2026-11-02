# Règles du projet

## Portage macOS

- La version macOS est gérée **exclusivement sur un autre Mac**.
- Depuis ce poste Windows, ne jamais intervenir sur le dossier `mac/` : pas de
  modification de code, compilation, test, signature, empaquetage ni mise à jour
  de documentation macOS.
- Ne pas régénérer les archives macOS (`mac/dist/*`, `dist/*macOS*.zip`).
- Ne pas supprimer les artefacts macOS existants : ils servent d'historique.
- Le travail sur ce poste concerne uniquement l'application Windows : `src/`,
  `build.ps1`, `tests/` Windows et `dist/easy ip-switch v1.1.exe`.
- Si une demande concerne macOS, indiquer qu'elle doit être traitée sur le Mac
  dédié, sans effectuer d'action depuis Windows.
