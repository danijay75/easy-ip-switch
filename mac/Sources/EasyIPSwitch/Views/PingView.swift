import SwiftUI
import Foundation
import Combine

struct PingView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState

    @State private var selectedID = ""
    @State private var target = ""
    @State private var targetInvalid = false
    @State private var running = false
    @State private var stateText = "Aucun test lancé pour le moment."
    @State private var stateKind: MessageKind = .info
    @State private var entries: [PingLogEntry] = []

    private static let timeFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        return formatter
    }()

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                Text("Tester une adresse IP sur le réseau")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundColor(theme.palette.text)

                Text("Choisissez la carte qui doit envoyer le test, puis saisissez une adresse IPv4 à joindre.")
                    .font(.system(size: 12.5))
                    .foregroundColor(theme.palette.textMuted)
                    .padding(.top, 4)

                HStack(alignment: .bottom, spacing: 14) {
                    VStack(alignment: .leading, spacing: 7) {
                        Text("Carte réseau (source)")
                            .font(.system(size: 12))
                            .foregroundColor(theme.palette.textMuted)

                        adapterMenu
                    }
                    .frame(maxWidth: .infinity)

                    LabeledField(
                        label: "Adresse IPv4 à tester",
                        text: $target,
                        placeholder: "ex. 192.168.1.1",
                        invalid: targetInvalid,
                        enabled: !running
                    )
                    .frame(width: 220)
                    .onSubmit { startPing() }

                    FlatButton(
                        "Ping",
                        kind: .primary,
                        enabled: !running && !selectableAdapters.isEmpty
                    ) {
                        startPing()
                    }
                }
                .padding(.top, 20)

                HStack(spacing: 10) {
                    Text("Journal des tests")
                        .font(.system(size: 12, weight: .semibold))
                        .foregroundColor(theme.palette.textMuted)

                    Spacer()

                    FlatButton("Effacer", kind: .ghost, enabled: !running) {
                        entries.removeAll()
                        setState("Journal effacé.", .info)
                    }
                }
                .padding(.top, 22)

                logView
                    .padding(.top, 8)

                Text(stateText)
                    .font(.system(size: 11.5))
                    .foregroundColor(color(for: stateKind, in: theme.palette))
                    .lineLimit(2)
                    .padding(.top, 8)
            }
            .padding(22)
            .background(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .fill(theme.palette.surface)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .stroke(theme.palette.border, lineWidth: 1)
            )
            .padding(.horizontal, 28)
            .padding(.vertical, 20)
            .frame(maxWidth: 1120)
            .frame(maxWidth: .infinity)
        }
        .onAppear {
            syncSelection()
        }
        .onReceive(model.$cards) { _ in
            syncSelection()
        }
        .onChange(of: target) { _ in
            targetInvalid = false
            if stateKind == .error {
                setState("Aucun test lancé pour le moment.", .info)
            }
        }
    }

    private var selectableAdapters: [AdapterCardModel] {
        model.cards.filter { $0.info.enabled && !$0.info.ip.isEmpty }
    }

    private var selectedAdapter: AdapterCardModel? {
        selectableAdapters.first(where: { $0.id == selectedID }) ?? selectableAdapters.first
    }

    private var adapterMenu: some View {
        Menu {
            if selectableAdapters.isEmpty {
                Text("Aucune carte avec adresse IPv4")
            } else {
                ForEach(selectableAdapters) { card in
                    Button {
                        selectedID = card.id
                        if target.isEmpty {
                            prefillTarget()
                        }
                    } label: {
                        Text("\(card.info.serviceName) — \(card.info.ip) (\(card.info.dhcp ? "DHCP" : "fixe"))")
                    }
                }
            }
        } label: {
            HStack(spacing: 8) {
                Text(selectedLabel)
                    .font(.system(size: 13))
                    .foregroundColor(theme.palette.text)
                    .lineLimit(1)
                Spacer(minLength: 4)
                Image(systemName: "chevron.down")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundColor(theme.palette.textMuted)
            }
            .padding(.horizontal, 12)
            .frame(maxWidth: .infinity, minHeight: 36, maxHeight: 36)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(theme.palette.surfaceSunken)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .stroke(theme.palette.border, lineWidth: 1)
            )
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .disabled(running || selectableAdapters.isEmpty)
    }

    private var selectedLabel: String {
        guard let card = selectedAdapter else {
            return "Aucune carte avec adresse IPv4"
        }
        return "\(card.info.serviceName) — \(card.info.ip) (\(card.info.dhcp ? "DHCP" : "fixe"))"
    }

    private var logView: some View {
        ScrollViewReader { proxy in
            ScrollView {
                VStack(alignment: .leading, spacing: 6) {
                    if entries.isEmpty {
                        Text("Les résultats des tests apparaîtront ici.")
                            .font(.system(size: 12))
                            .foregroundColor(theme.palette.textFaint)
                    } else {
                        ForEach(entries) { entry in
                            Text(entry.text)
                                .font(.system(size: 12, design: .monospaced))
                                .foregroundColor(color(for: entry.kind, in: theme.palette))
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .id(entry.id)
                        }
                    }
                }
                .padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .frame(height: 300)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(theme.palette.surfaceSunken)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .stroke(theme.palette.border, lineWidth: 1)
            )
            .onChange(of: entries.count) { _ in
                guard let last = entries.last else { return }
                withAnimation(.easeOut(duration: 0.15)) {
                    proxy.scrollTo(last.id, anchor: .bottom)
                }
            }
        }
    }

    private func syncSelection() {
        if !selectableAdapters.contains(where: { $0.id == selectedID }) {
            selectedID = selectableAdapters.first?.id ?? ""
        }
        if target.isEmpty {
            prefillTarget()
        }
    }

    private func prefillTarget() {
        guard let card = selectedAdapter else { return }
        let guess = card.info.router.isEmpty ? card.info.ip : card.info.router
        if !guess.isEmpty {
            target = guess
        }
    }

    private func startPing() {
        guard !running else { return }

        guard let card = selectedAdapter else {
            setState("Aucune carte réseau avec une adresse IPv4 n'est disponible.", .error)
            return
        }

        let address = target.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let parsed = IPv4.parse(address), !IPv4.isForbidden(parsed) else {
            targetInvalid = true
            setState("Adresse invalide : utilisez le format 192.168.1.1", .error)
            return
        }

        targetInvalid = false
        running = true
        setState("Ping en cours vers \(address) via \(card.info.serviceName)…", .info)

        Task { @MainActor in
            let outcome = await PingRunner.send(address: address, device: card.info.device, count: 4)
            running = false

            let stamp = PingView.timeFormatter.string(from: Date())
            let source = card.info.ip.isEmpty ? "" : " via \(card.info.serviceName) (\(card.info.ip))"
            entries.append(PingLogEntry(
                text: "\(stamp)   →   \(address)\(source)   ·   \(outcome.detail)",
                kind: outcome.success ? .success : .error
            ))

            if outcome.success {
                setState("Réponse de \(address) : \(outcome.detail.lowercased()).", .success)
            } else {
                setState("Aucune réponse de \(address) : \(outcome.detail).", .error)
            }
        }
    }

    private func setState(_ text: String, _ kind: MessageKind) {
        stateText = text
        stateKind = kind
    }
}

private struct PingLogEntry: Identifiable {
    let id = UUID()
    let text: String
    let kind: MessageKind
}
