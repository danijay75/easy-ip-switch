import Foundation

enum IPv4 {
    static func parse(_ text: String) -> UInt32? {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        let parts = trimmed.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 4 else { return nil }

        var result: UInt32 = 0
        for part in parts {
            guard !part.isEmpty, part.count <= 3 else { return nil }
            guard part.allSatisfy({ $0 >= "0" && $0 <= "9" }) else { return nil }
            guard let number = UInt32(part), number <= 255 else { return nil }
            result = (result << 8) | number
        }
        return result
    }

    static func format(_ value: UInt32) -> String {
        let a = (value >> 24) & 0xFF
        let b = (value >> 16) & 0xFF
        let c = (value >> 8) & 0xFF
        let d = value & 0xFF
        return "\(a).\(b).\(c).\(d)"
    }

    static func isValidMask(_ mask: UInt32) -> Bool {
        guard mask != 0 else { return false }
        let inverted = ~mask
        return (inverted & (inverted &+ 1)) == 0
    }

    static func isAPIPA(_ address: UInt32) -> Bool {
        return (address & 0xFFFF0000) == 0xA9FE0000
    }

    static func isForbidden(_ address: UInt32) -> Bool {
        return address == 0 || address == UInt32.max
    }

    /// Suggests the conventional gateway for an IPv4 address: the first usable
    /// host of its subnet (network + 1). When that host is the address itself,
    /// the next usable host is suggested so the gateway stays valid.
    ///
    /// Returns nil when the address, the mask, or the usable-host range cannot
    /// produce a gateway (invalid input, /31, /32).
    static func suggestedGateway(address: String, mask: String) -> String? {
        guard let ip = parse(address), !isForbidden(ip), !isAPIPA(ip),
              let netmask = parse(mask), isValidMask(netmask) else {
            return nil
        }

        let network = ip & netmask
        let hostMask = ~netmask
        // /31 and /32 have no usable host range.
        guard hostMask > 1 else { return nil }

        let broadcast = network | hostMask
        var candidate = network &+ 1
        if candidate == ip {
            candidate = network &+ 2
        }

        guard candidate != ip, candidate < broadcast else { return nil }
        return format(candidate)
    }
}
