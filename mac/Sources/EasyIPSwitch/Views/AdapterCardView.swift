import SwiftUI

struct AdapterCardView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var theme: ThemeState
    @EnvironmentObject private var language: LanguageStore
    @ObservedObject var card: AdapterCardModel

    /// Card width measured by the background geometry reader. The card uses a
    /// two-column layout on wide windows and a stacked layout when the window
    /// gets narrower, so long adapter names and fields always stay readable.
    @State private var measuredWidth: CGFloat = 980

    private let wideBreakpoint: CGFloat = 900

    var body: some View {
        Group {
            if measuredWidth >= wideBreakpoint {
                wideLayout
            } else {
                compactLayout
            }
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
        .background(
            GeometryReader { proxy in
                Color.clear
                    .onAppear { measuredWidth = proxy.size.width }
                    .onChange(of: proxy.size.width) { width in
                        measuredWidth = width
                    }
            }
        )
        .opacity(card.busy ? 0.92 : 1)
        .onChange(of: card.isDirty) { _ in
            model.cardDraftStateChanged()
        }
    }

    private var palette: Palette { theme.palette }

    // MARK: - Layouts

    private var wideLayout: some View {
        HStack(alignment: .top, spacing: 0) {
            VStack(alignment: .leading, spacing: 10) {
                identityBlock
                statusRow
                    .padding(.top, 3)
                activationRow
                    .padding(.top, 1)
                Spacer(minLength: 0)
            }
            .frame(minWidth: 240, idealWidth: 300, maxWidth: 340, alignment: .topLeading)

            Rectangle()
                .fill(theme.palette.border)
                .frame(width: 1)
                .padding(.vertical, 2)

            rightColumn
                .padding(.leading, 22)
        }
    }

    private var compactLayout: some View {
        VStack(alignment: .leading, spacing: 14) {
            identityBlock

            HStack(spacing: 18) {
                statusRow
                activationRow
                Spacer(minLength: 0)
            }

            Rectangle()
                .fill(theme.palette.border)
                .frame(height: 1)

            rightColumn
        }
    }

    // MARK: - Identity

    private var identityBlock: some View {
        HStack(alignment: .top, spacing: 13) {
            ZStack {
                Circle()
                    .fill(iconSoftColor)
                    .frame(width: 54, height: 54)
                Image(systemName: iconName)
                    .font(.system(size: 22, weight: .medium))
                    .foregroundColor(iconColor)
            }

            VStack(alignment: .leading, spacing: 3) {
                Text(card.info.serviceName)
                    .font(.system(size: 19, weight: .bold))
                    .foregroundColor(palette.text)
                    .fixedSize(horizontal: false, vertical: true)
                    .help(Text(card.info.serviceName))

                Text(card.info.metaLabel)
                    .font(.system(size: 11.5))
                    .foregroundColor(palette.textMuted)
                    .fixedSize(horizontal: false, vertical: true)
                    .help(Text(card.info.metaLabel))
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private var statusRow: some View {
        HStack(spacing: 7) {
            Circle()
                .fill(statusColor)
                .frame(width: 8, height: 8)
            Text(card.info.statusLabel)
                .font(.system(size: 12.5, weight: .semibold))
                .foregroundColor(statusColor)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private var activationRow: some View {
        HStack(spacing: 10) {
            CardSwitch(isOn: card.info.enabled, enabled: !card.busy) {
                Task { await model.setEnabled(card, !card.info.enabled) }
            }
            Text(language.t("Carte active"))
                .font(.system(size: 12.5))
                .foregroundColor(palette.textMuted)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: - Configuration

    private var rightColumn: some View {
        VStack(alignment: .leading, spacing: 12) {
            modeRow

            fieldsLayout

            messageArea

            actionRow
        }
        .frame(minHeight: 172, alignment: .topLeading)
    }

    @ViewBuilder
    private var modeRow: some View {
        if measuredWidth >= 620 {
            HStack(spacing: 12) {
                ModeSegment(mode: modeBinding, enabled: card.canEdit)
                hintText
                Spacer(minLength: 0)
            }
        } else {
            VStack(alignment: .leading, spacing: 8) {
                ModeSegment(mode: modeBinding, enabled: card.canEdit)
                hintText
            }
        }
    }

    private var hintText: some View {
        Text(card.hintText)
            .font(.system(size: 12))
            .foregroundColor(palette.textMuted)
            .fixedSize(horizontal: false, vertical: true)
    }

    @ViewBuilder
    private var fieldsLayout: some View {
        if measuredWidth >= wideBreakpoint {
            HStack(alignment: .top, spacing: 12) {
                ipField
                maskField
                gatewayField
            }
        } else if measuredWidth >= 460 {
            VStack(alignment: .leading, spacing: 12) {
                HStack(alignment: .top, spacing: 12) {
                    ipField
                    maskField
                }
                gatewayField
            }
        } else {
            VStack(alignment: .leading, spacing: 12) {
                ipField
                maskField
                gatewayField
            }
        }
    }

    private var ipField: some View {
        LabeledField(
            label: language.t("Adresse IP"),
            text: textBinding(\.ip),
            placeholder: "192.168.1.50",
            invalid: card.showErrors && card.ipError != nil,
            enabled: card.canEdit && card.mode == .manual
        )
    }

    private var maskField: some View {
        LabeledField(
            label: language.t("Masque"),
            text: textBinding(\.mask),
            placeholder: "255.255.255.0",
            invalid: card.showErrors && card.maskError != nil,
            enabled: card.canEdit && card.mode == .manual
        )
    }

    private var gatewayField: some View {
        LabeledField(
            label: language.t("Passerelle"),
            text: textBinding(\.router),
            placeholder: "192.168.1.1",
            invalid: card.showErrors && card.routerError != nil,
            enabled: card.canEdit && card.mode == .manual
        )
    }

    private var actionRow: some View {
        HStack(spacing: 10) {
            if card.busy {
                ProgressView()
                    .controlSize(.small)
            }

            Spacer(minLength: 0)

            FlatButton(
                language.t("Tester"),
                kind: .secondary,
                enabled: card.info.enabled && !card.busy && !card.testAddress.isEmpty
            ) {
                Task { await model.test(card) }
            }

            FlatButton(
                language.t("Annuler"),
                kind: .ghost,
                enabled: card.isDirty && !card.busy
            ) {
                card.resetDraft()
            }

            FlatButton(
                language.t("Changer"),
                kind: .primary,
                enabled: card.isDirty && !card.busy && card.info.enabled
            ) {
                Task { await model.apply(card) }
            }
        }
    }

    private var messageArea: some View {
        Group {
            if let message = card.displayMessage {
                Text(message.text)
                    .font(.system(size: 11.5))
                    .foregroundColor(color(for: message.kind, in: palette))
                    .fixedSize(horizontal: false, vertical: true)
                    .help(Text(message.text))
            } else {
                Text(" ")
                    .font(.system(size: 11.5))
            }
        }
        .frame(minHeight: 16, alignment: .leading)
    }

    // MARK: - Bindings

    private var modeBinding: Binding<ConfigMode> {
        Binding(
            get: { card.mode },
            set: { card.changeMode($0) }
        )
    }

    private func textBinding(_ keyPath: ReferenceWritableKeyPath<AdapterCardModel, String>) -> Binding<String> {
        Binding(
            get: { card[keyPath: keyPath] },
            set: { value in
                card[keyPath: keyPath] = value
                // An edit invalidates the previous success/error message, as in
                // the Windows card. Field errors are recomputed live.
                card.clearMessage()
            }
        )
    }

    // MARK: - Colors

    private var iconName: String {
        switch card.info.kind {
        case .wifi: return "wifi"
        case .ethernet, .other: return "network"
        }
    }

    private var iconColor: Color {
        switch card.info.kind {
        case .wifi: return palette.wifi
        case .ethernet: return palette.accent
        case .other: return palette.textMuted
        }
    }

    private var iconSoftColor: Color {
        switch card.info.kind {
        case .wifi: return palette.wifiSoft
        case .ethernet: return palette.accentSoft
        case .other: return palette.surfaceSunken
        }
    }

    private var statusColor: Color {
        if !card.info.enabled {
            return palette.textMuted
        }
        return card.info.connected ? palette.success : palette.warning
    }
}
