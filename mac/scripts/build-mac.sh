#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_BASENAME="Easy IP-Switch"
EXECUTABLE="EasyIPSwitch"
MIN_MACOS="12.0"
BUILD_DIR="$ROOT/build"
DIST_DIR="$ROOT/dist"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$ROOT/Info.plist" 2>/dev/null || echo '1.1.1')"
APP_NAME="$APP_BASENAME v$VERSION"

UNIVERSAL=0
if [ "${1:-}" = "--universal" ]; then
    UNIVERSAL=1
fi

if [ "$(uname -s)" != "Darwin" ]; then
    echo "Ce script doit etre execute sur macOS." >&2
    exit 1
fi

if ! xcode-select -p >/dev/null 2>&1; then
    echo "Xcode Command Line Tools requis : xcode-select --install" >&2
    exit 1
fi

SDK="$(xcrun --show-sdk-path --sdk macosx)"
SWIFTC="$(xcrun --find swiftc)"

echo "Compilation de $APP_NAME"
echo "SDK : $SDK"

SOURCES=()
while IFS= read -r file; do
    SOURCES+=("$file")
done < <(find "$ROOT/Sources/EasyIPSwitch" -name '*.swift' | sort)

if [ "${#SOURCES[@]}" -eq 0 ]; then
    echo "Aucune source Swift trouvee." >&2
    exit 1
fi

if [ ! -f "$ROOT/assets/AppIcon.icns" ] && [ -f "$ROOT/assets/AppIcon-1024.png" ]; then
    bash "$ROOT/scripts/make-icns.sh"
fi

mkdir -p "$BUILD_DIR" "$DIST_DIR"

build_arch() {
    local arch="$1"
    local label="$2"
    local output="$BUILD_DIR/$arch/$EXECUTABLE"

    echo ""
    echo "→ Compilation $label ($arch)…"
    mkdir -p "$BUILD_DIR/$arch"

    "$SWIFTC" \
        -swift-version 5 \
        -parse-as-library \
        -emit-executable \
        -module-name EasyIPSwitch \
        -O \
        -whole-module-optimization \
        -sdk "$SDK" \
        -target "$arch-apple-macosx$MIN_MACOS" \
        -framework SwiftUI \
        -framework AppKit \
        -framework SystemConfiguration \
        "${SOURCES[@]}" \
        -o "$output"

    file "$output"
    assemble_app "$output" "$label"
}

assemble_app() {
    local binary="$1"
    local label="$2"
    local app="$DIST_DIR/$APP_NAME ($label).app"

    rm -rf "$app"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

    cp "$binary" "$app/Contents/MacOS/$EXECUTABLE"
    chmod +x "$app/Contents/MacOS/$EXECUTABLE"
    cp "$ROOT/Info.plist" "$app/Contents/Info.plist"

    if [ -f "$ROOT/assets/AppIcon.icns" ]; then
        cp "$ROOT/assets/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
    fi

    if [ -f "$ROOT/assets/donation-qr.png" ]; then
        cp "$ROOT/assets/donation-qr.png" "$app/Contents/Resources/donation-qr.png"
    fi

    if [ -d "$ROOT/assets/flags" ]; then
        mkdir -p "$app/Contents/Resources/flags"
        cp "$ROOT/assets/flags/"*.png "$app/Contents/Resources/flags/"
    fi

    # Remove Finder/resource metadata that can make codesign fail, especially
    # when the project is built from a synced or external volume.
    /usr/bin/xattr -cr "$app" 2>/dev/null || true

    /usr/bin/codesign --force --deep --sign - "$app" >/dev/null
    /usr/bin/codesign --verify --deep --strict "$app"

    local zip="$DIST_DIR/$APP_NAME ($label).zip"
    rm -f "$zip"
    /usr/bin/ditto -c -k --sequesterRsrc --keepParent "$app" "$zip"

    local size
    size="$(du -sh "$app" | cut -f1)"
    echo "   $app ($size)"
    echo "   $zip"

    make_dmg "$label"
}

# Builds a drag-and-drop installer: the application and a shortcut to
# /Applications inside a compressed disk image. The volume name stays short
# because HFS+ limits it to 27 characters.
make_dmg() {
    local label="$1"
    local app="$DIST_DIR/$APP_NAME ($label).app"
    local dmg="$DIST_DIR/$APP_NAME ($label).dmg"
    local staging="$BUILD_DIR/dmg/$label"

    rm -rf "$staging"
    mkdir -p "$staging"
    /usr/bin/ditto "$app" "$staging/$(basename "$app")"
    ln -s /Applications "$staging/Applications"

    # A Desktop alias cannot be shipped statically: each macOS account has a
    # different home folder. This small helper copies the app to /Applications
    # and creates the Desktop shortcut for the current user.
    local script="$BUILD_DIR/dmg/$label.applescript"
    cat > "$script" <<EOF
on run
    set helperFolder to do shell script "dirname " & quoted form of (POSIX path of (path to me))
    set sourceApp to helperFolder & "/" & "$(basename "$app")"
    set targetApp to "/Applications/Easy IP-Switch.app"
    set desktopLink to (POSIX path of (path to desktop folder)) & "Easy IP-Switch"

    try
        do shell script "test -w /Applications"
        do shell script "ditto " & quoted form of sourceApp & " " & quoted form of targetApp
    on error
        do shell script "ditto " & quoted form of sourceApp & " " & quoted form of targetApp with administrator privileges
    end try

    do shell script "ln -sfn " & quoted form of targetApp & " " & quoted form of desktopLink

    display dialog "Easy IP-Switch est installé dans le dossier Applications." & return & return & "Un raccourci a été créé sur le Bureau." buttons {"OK"} default button "OK" with title "Easy IP-Switch"
end run
EOF

    local installer="$staging/Installer sur le Bureau.app"
    local compileLog="$BUILD_DIR/dmg/$label.osacompile.log"
    if ! /usr/bin/osacompile -o "$installer" "$script" >/dev/null 2>"$compileLog"; then
        cat "$compileLog" >&2
        exit 1
    fi
    rm -f "$script" "$compileLog"
    /usr/bin/xattr -cr "$installer" 2>/dev/null || true
    /usr/bin/codesign --force --deep --sign - "$installer" >/dev/null 2>&1

    cat > "$staging/Lisez-moi.txt" <<EOF
$APP_NAME ($label)

Installation
1. Glissez « $(basename "$app") » dans le dossier Applications.
2. Éjectez le volume « $APP_BASENAME $VERSION ».
3. Lancez l'application depuis le dossier Applications.

Raccourci Bureau
Double-cliquez sur « Installer sur le Bureau » : l'application est copiée
dans Applications et un raccourci est créé sur le Bureau de la session
ouverte. Le raccourci est recréé pour chaque compte utilisateur, ce qu'un
lien fixe dans le DMG ne peut pas faire.

Si macOS refuse l'ouverture (application téléchargée) :
clic droit sur l'application, puis « Ouvrir ».

Aucune dépendance externe. macOS 12 ou plus récent.
EOF

    rm -f "$dmg"
    /usr/bin/hdiutil create \
        -quiet \
        -ov \
        -format UDZO \
        -fs HFS+ \
        -volname "$APP_BASENAME $VERSION" \
        -srcfolder "$staging" \
        "$dmg"

    rm -rf "$staging"

    local size
    size="$(du -sh "$dmg" | cut -f1)"
    echo "   $dmg ($size)"
}

build_arch "x86_64" "Intel"
build_arch "arm64" "Apple Silicon"

if [ "$UNIVERSAL" -eq 1 ]; then
    echo ""
    echo "→ Creation de la version universelle…"
    mkdir -p "$BUILD_DIR/universal"
    /usr/bin/lipo -create \
        "$BUILD_DIR/x86_64/$EXECUTABLE" \
        "$BUILD_DIR/arm64/$EXECUTABLE" \
        -output "$BUILD_DIR/universal/$EXECUTABLE"
    /usr/bin/lipo -info "$BUILD_DIR/universal/$EXECUTABLE"
    assemble_app "$BUILD_DIR/universal/$EXECUTABLE" "Universelle"
fi

echo ""
echo "Termine."
echo "Les applications sont dans : $DIST_DIR"
