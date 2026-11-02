#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DEVELOPER_DIR_PATH="$(xcode-select -p)"

# Avec Xcode complet, SwiftPM trouve XCTest et Swift Testing tout seul.
if [[ "$DEVELOPER_DIR_PATH" == *"Xcode.app"* ]]; then
    exec swift test --enable-swift-testing "$@"
fi

# Avec les Command Line Tools seuls, Swift Testing.framework est présent mais
# son chemin n'est pas ajouté automatiquement par SwiftPM.
FRAMEWORKS="$DEVELOPER_DIR_PATH/Library/Developer/Frameworks"
INTEROP_LIB="$DEVELOPER_DIR_PATH/Library/Developer/usr/lib"

if [ ! -d "$FRAMEWORKS/Testing.framework" ]; then
    echo "Swift Testing introuvable. Installez Xcode complet ou vérifiez les Command Line Tools." >&2
    exit 1
fi

exec swift test \
    --disable-xctest \
    --enable-swift-testing \
    -Xswiftc -F -Xswiftc "$FRAMEWORKS" \
    -Xlinker -F"$FRAMEWORKS" \
    -Xlinker -rpath -Xlinker "$FRAMEWORKS" \
    -Xlinker -rpath -Xlinker "$INTEROP_LIB" \
    "$@"
