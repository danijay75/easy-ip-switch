import AppKit
import SwiftUI

enum FlagImage {
    static func image(for language: AppLanguage) -> NSImage? {
        if let url = Bundle.main.url(
            forResource: language.flagCode,
            withExtension: "png",
            subdirectory: "flags"
        ), let image = NSImage(contentsOf: url) {
            return image
        }

        let current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let candidates = [
            current.appendingPathComponent("assets/flags/\(language.flagCode).png"),
            current.appendingPathComponent("mac/assets/flags/\(language.flagCode).png")
        ]
        for url in candidates {
            if let image = NSImage(contentsOf: url) {
                return image
            }
        }
        return nil
    }
}

struct LanguageSelectorView: View {
    @EnvironmentObject private var language: LanguageStore
    @EnvironmentObject private var theme: ThemeState

    var body: some View {
        Menu {
            ForEach(AppLanguage.allCases) { item in
                Button {
                    language.set(item)
                } label: {
                    HStack(spacing: 8) {
                        flag(item, width: 20, height: 13)
                        Text(item.displayName)
                            .font(.system(size: 12, weight: language.current == item ? .semibold : .regular))
                        if language.current == item {
                            Image(systemName: "checkmark")
                                .font(.system(size: 10, weight: .bold))
                        }
                    }
                }
            }
        } label: {
            HStack(spacing: 6) {
                flag(language.current, width: 24, height: 16)
                Text(language.current.codeLabel)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundColor(theme.palette.text)
                Image(systemName: "chevron.down")
                    .font(.system(size: 9, weight: .semibold))
                    .foregroundColor(theme.palette.textMuted)
            }
            .padding(.horizontal, 9)
            .frame(height: 32)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(theme.palette.surfaceSunken)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .stroke(theme.palette.border, lineWidth: 1)
            )
            .contentShape(Rectangle())
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
        .help(language.t("Changer la langue de l'application"))
        .accessibilityLabel(language.t("Langue"))
        .accessibilityHint(language.t("Changer la langue de l'application"))
    }

    @ViewBuilder
    private func flag(_ language: AppLanguage, width: CGFloat, height: CGFloat) -> some View {
        if let image = FlagImage.image(for: language) {
            Image(nsImage: image)
                .resizable()
                .interpolation(.high)
                .aspectRatio(contentMode: .fill)
                .frame(width: width, height: height)
                .clipShape(RoundedRectangle(cornerRadius: 2, style: .continuous))
                .overlay(
                    RoundedRectangle(cornerRadius: 2, style: .continuous)
                        .stroke(Color.black.opacity(0.15), lineWidth: 0.5)
                )
        } else {
            Text(language.codeLabel)
                .font(.system(size: 9, weight: .bold))
        }
    }
}
