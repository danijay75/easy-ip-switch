import SwiftUI
import Foundation
import Combine

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    private let refreshTimer = Timer.publish(every: 30, on: .main, in: .common).autoconnect()

    var body: some View {
        VStack(spacing: 0) {
            HeaderView()
            TabBarView()
            ZStack {
                CardsView()
                    .opacity(model.tab == .cards ? 1 : 0)
                    .allowsHitTesting(model.tab == .cards)
                    .disabled(model.tab != .cards)
                    .accessibilityHidden(model.tab != .cards)

                PingView()
                    .opacity(model.tab == .ping ? 1 : 0)
                    .allowsHitTesting(model.tab == .ping)
                    .disabled(model.tab != .ping)
                    .accessibilityHidden(model.tab != .ping)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            StatusBarView()
            FooterView()
        }
        .background(theme.palette.bg)
        .preferredColorScheme(theme.isDark ? .dark : .light)
        .tint(theme.palette.accent)
        .task {
            await model.start()
        }
        .onReceive(refreshTimer) { _ in
            Task { await model.autoRefresh() }
        }
        .onChange(of: language.current) { _ in
            model.languageChanged()
        }
    }
}

struct HeaderView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    @State private var measuredWidth: CGFloat = 1000

    var body: some View {
        HStack(alignment: .center, spacing: 14) {
            BrandLogo(size: 40)

            VStack(alignment: .leading, spacing: 2) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text(AppInfo.productName)
                        .font(.system(size: 24, weight: .bold))
                        .foregroundColor(theme.palette.text)
                        .lineLimit(1)

                    Text(AppInfo.versionLabel)
                        .font(.system(size: 13, weight: .medium))
                        .foregroundColor(theme.palette.textMuted)
                }

                Text(language.t("Changement d'adresse IP en quelques clics"))
                    .font(.system(size: 12.5))
                    .foregroundColor(theme.palette.textMuted)
                    .lineLimit(1)
            }

            Spacer(minLength: 12)

            LanguageSelectorView()

            ThemeSwitch()

            if measuredWidth >= 940 {
                StatusPill(
                    icon: "lock.shield.fill",
                    text: language.t("Autorisation admin."),
                    tint: theme.palette.accent,
                    soft: theme.palette.accentSoft
                )
                .help(language.t("Les modifications réseau demandent l'autorisation de macOS. La lecture reste sans privilège."))
            }

            FlatButton(language.t("Actualiser"), icon: "arrow.clockwise", kind: .secondary, enabled: !model.loading && !model.isBusy) {
                Task { await model.refresh(manual: true) }
            }
            .help(language.t("Mettre à jour la liste et l'état des cartes réseau (⌘R)"))
        }
        .padding(.horizontal, 28)
        .padding(.vertical, 14)
        .frame(maxWidth: .infinity)
        .background(
            GeometryReader { proxy in
                Color.clear
                    .onAppear { measuredWidth = proxy.size.width }
                    .onChange(of: proxy.size.width) { width in
                        measuredWidth = width
                    }
            }
        )
        .background(theme.palette.surface)
        .overlay(
            Rectangle()
                .fill(theme.palette.border)
                .frame(height: 1),
            alignment: .bottom
        )
    }
}

struct TabBarView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    var body: some View {
        HStack(spacing: 28) {
            tab(language.t("Cartes réseau"), tab: .cards)
            tab(language.t("Test de ping"), tab: .ping)
            Spacer()
            FlatButton(language.t("Réglages Réseau"), icon: "network", kind: .secondary) {
                SystemSettings.openNetwork()
            }
            .help(language.t("Ouvrir les Réglages Réseau de macOS"))
        }
        .padding(.horizontal, 28)
        .frame(maxWidth: .infinity)
        .background(theme.palette.bg)
        .overlay(
            Rectangle()
                .fill(theme.palette.border)
                .frame(height: 1),
            alignment: .bottom
        )
    }

    private func tab(_ title: String, tab: MainTab) -> some View {
        Button {
            withAnimation(.easeInOut(duration: 0.15)) {
                model.tab = tab
            }
        } label: {
            VStack(spacing: 0) {
                Text(title)
                    .font(.system(size: 13.5, weight: model.tab == tab ? .semibold : .regular))
                    .foregroundColor(model.tab == tab ? theme.palette.text : theme.palette.textMuted)
                    .padding(.vertical, 13)

                Rectangle()
                    .fill(model.tab == tab ? theme.palette.accent : Color.clear)
                    .frame(height: 2)
                    .cornerRadius(1)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

struct CardsView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                if model.cards.isEmpty && model.loading {
                    emptyCard {
                        HStack(spacing: 12) {
                            ProgressView()
                                .controlSize(.small)
                            Text(language.t("Recherche des cartes réseau…"))
                                .font(.system(size: 13))
                                .foregroundColor(theme.palette.textMuted)
                        }
                    }
                } else if model.cards.isEmpty {
                    emptyCard {
                        VStack(spacing: 10) {
                            Text(language.t("Aucune carte réseau détectée."))
                                .font(.system(size: 14, weight: .semibold))
                                .foregroundColor(theme.palette.text)
                            Text(language.t("Vérifiez les réglages Réseau de macOS, puis cliquez sur Actualiser."))
                                .font(.system(size: 12.5))
                                .foregroundColor(theme.palette.textMuted)
                                .multilineTextAlignment(.center)
                            FlatButton(language.t("Actualiser"), icon: "arrow.clockwise", kind: .secondary) {
                                Task { await model.refresh(manual: true) }
                            }
                            .padding(.top, 4)
                        }
                    }
                } else {
                    ForEach(model.cards) { card in
                        AdapterCardView(card: card)
                    }
                }
            }
            .padding(.horizontal, 28)
            .padding(.vertical, 20)
            .frame(maxWidth: 1120)
            .frame(maxWidth: .infinity)
        }
    }

    private func emptyCard<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        content()
            .frame(maxWidth: .infinity)
            .padding(.vertical, 32)
            .padding(.horizontal, 24)
            .background(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .fill(theme.palette.surface)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .stroke(theme.palette.border, lineWidth: 1)
            )
    }
}

struct StatusBarView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState

    var body: some View {
        HStack(spacing: 16) {
            if !model.statusText.isEmpty {
                Text(model.statusText)
                    .font(.system(size: 11.5))
                    .foregroundColor(color(for: model.statusKind, in: theme.palette))
                    .lineLimit(1)
                    .truncationMode(.tail)
            } else {
                Text(model.pendingHint)
                    .font(.system(size: 11.5))
                    .foregroundColor(theme.palette.textMuted)
                    .lineLimit(1)
            }

            Spacer(minLength: 8)

            if !model.statusText.isEmpty {
                Text(model.pendingHint)
                    .font(.system(size: 11.5))
                    .foregroundColor(theme.palette.textMuted)
                    .lineLimit(1)
            }
        }
        .padding(.horizontal, 28)
        .padding(.vertical, 9)
        .frame(maxWidth: .infinity)
        .background(theme.palette.bg)
    }
}

struct FooterView: View {
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore

    var body: some View {
        VStack(spacing: 0) {
            Hairline(color: theme.palette.border)

            HStack(spacing: 10) {
                Text(AppInfo.copyrightLine)
                    .font(.system(size: 11.5))
                    .foregroundColor(theme.palette.textMuted)
                    .lineLimit(1)

                Spacer(minLength: 8)

                Link("angelivisions.com", destination: AppInfo.websiteURL)
                    .font(.system(size: 11.5))
                    .foregroundColor(theme.palette.textMuted)

                Text("·")
                    .font(.system(size: 11.5))
                    .foregroundColor(theme.palette.textFaint)

                Link(language.t("Faire un don"), destination: AppInfo.donationURL)
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundColor(theme.palette.accent)
            }
            .padding(.horizontal, 28)
            .frame(height: 44)
        }
        .background(theme.palette.surface)
    }
}
