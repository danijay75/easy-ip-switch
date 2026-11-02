import Foundation

enum PingRunner {
    static func send(address: String, device: String, count: Int = 4) async -> PingOutcome {
        await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                continuation.resume(returning: run(address: address, device: device, count: count))
            }
        }
    }

    private static func run(address: String, device: String, count: Int) -> PingOutcome {
        var arguments = ["-n", "-c", String(count), "-W", "1200"]
        if !device.isEmpty {
            arguments += ["-b", device]
        }
        arguments.append(address)

        var result = Shell.run("/sbin/ping", arguments)

        // Some macOS versions reject -b for certain interface types, and binding
        // to an interface requires root. Fall back to the normal route so the
        // ping test still works without administrator privileges.
        if result.status != 0 && !device.isEmpty {
            let diagnostic = (result.output + result.error).lowercased()
            if diagnostic.contains("usage:") || diagnostic.contains("illegal option")
                || diagnostic.contains("invalid option")
                || diagnostic.contains("permission denied")
                || diagnostic.contains("operation not permitted")
                || diagnostic.contains("bind:") {
                var fallback = ["-n", "-c", String(count), "-W", "1200"]
                fallback.append(address)
                result = Shell.run("/sbin/ping", fallback)
            }
        }

        if let statistics = parse(result.output) {
            let success = statistics.received > 0
            var parts: [String] = []
            if statistics.received > 1 {
                parts.append(L.f("{0} paquets reçus sur {1}", statistics.received, statistics.transmitted))
            } else {
                parts.append(L.f("{0} paquet reçu sur {1}", statistics.received, statistics.transmitted))
            }
            parts.append(L.f("{0} % de perte", L.number(statistics.loss, digits: 1)))
            if let average = statistics.average {
                parts.append(L.f("{0} ms en moyenne", L.number(average, digits: 1)))
            }
            return PingOutcome(success: success, detail: parts.joined(separator: " · "), raw: result.output)
        }

        if result.status == 0 {
            return PingOutcome(success: true, detail: L.t("réponse reçue"), raw: result.output)
        }

        let diagnostic = result.combined
        return PingOutcome(
            success: false,
            detail: localizedFailure(diagnostic),
            raw: diagnostic
        )
    }

    private static func localizedFailure(_ diagnostic: String) -> String {
        let line = lastLine(diagnostic).lowercased()
        if line.contains("request timeout") || line.contains("timeout") {
            return L.t("Délai dépassé")
        }
        if line.contains("no route to host") {
            return L.t("Hôte inaccessible")
        }
        if line.contains("unknown host") || line.contains("cannot resolve") {
            return L.t("Hôte introuvable")
        }
        if line.contains("network is unreachable") || line.contains("network unreachable") {
            return L.t("Réseau inaccessible")
        }
        if line.isEmpty || line.contains("no answer") {
            return L.t("aucune réponse")
        }
        return line
    }

    static func parse(_ output: String) -> (transmitted: Int, received: Int, loss: Double, average: Double?)? {
        guard let statistics = try? NSRegularExpression(
            pattern: "([0-9]+) packets transmitted, ([0-9]+) packets received, ([0-9.,]+)% packet loss"
        ) else {
            return nil
        }

        let range = NSRange(output.startIndex..<output.endIndex, in: output)
        guard let match = statistics.firstMatch(in: output, options: [], range: range) else {
            return nil
        }

        func value(_ index: Int) -> String? {
            guard let captured = Range(match.range(at: index), in: output) else { return nil }
            return String(output[captured])
        }

        guard let transmittedText = value(1), let receivedText = value(2), let lossText = value(3),
              let transmitted = Int(transmittedText), let received = Int(receivedText) else {
            return nil
        }

        let loss = Double(lossText.replacingOccurrences(of: ",", with: ".")) ?? 0
        var average: Double?
        if let timing = try? NSRegularExpression(pattern: "= ([0-9.,]+)/([0-9.,]+)/([0-9.,]+)"),
           let timingMatch = timing.firstMatch(in: output, options: [], range: range),
           let averageRange = Range(timingMatch.range(at: 2), in: output) {
            let averageText = String(output[averageRange])
            average = Double(averageText.replacingOccurrences(of: ",", with: "."))
        }

        return (transmitted, received, loss, average)
    }

    private static func lastLine(_ text: String) -> String {
        let lines = text
            .split(whereSeparator: { $0 == "\n" || $0 == "\r" })
            .map { String($0).trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
        return lines.last ?? "aucune réponse"
    }
}
