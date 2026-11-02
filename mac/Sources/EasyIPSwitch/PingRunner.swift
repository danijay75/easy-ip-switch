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
                parts.append("\(statistics.received) paquets reçus sur \(statistics.transmitted)")
            } else {
                parts.append("\(statistics.received) paquet reçu sur \(statistics.transmitted)")
            }
            parts.append("\(decimal(statistics.loss, digits: 1)) % de perte")
            if let average = statistics.average {
                parts.append("\(decimal(average, digits: 1)) ms en moyenne")
            }
            return PingOutcome(success: success, detail: parts.joined(separator: " · "), raw: result.output)
        }

        if result.status == 0 {
            return PingOutcome(success: true, detail: "réponse reçue", raw: result.output)
        }

        let diagnostic = result.combined
        return PingOutcome(
            success: false,
            detail: diagnostic.isEmpty ? "aucune réponse" : lastLine(diagnostic).lowercased(),
            raw: diagnostic
        )
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

    private static func decimal(_ value: Double, digits: Int) -> String {
        return String(format: "%.\(digits)f", value).replacingOccurrences(of: ".", with: ",")
    }

    private static func lastLine(_ text: String) -> String {
        let lines = text
            .split(whereSeparator: { $0 == "\n" || $0 == "\r" })
            .map { String($0).trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
        return lines.last ?? "aucune réponse"
    }
}
