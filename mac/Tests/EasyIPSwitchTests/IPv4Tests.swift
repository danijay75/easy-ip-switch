import Testing
@testable import EasyIPSwitch

@Suite("Validation IPv4")
struct IPv4Tests {
    private func value(_ text: String) throws -> UInt32 {
        try #require(IPv4.parse(text))
    }

    @Test("Adresses valides")
    func parseAcceptsValidAddresses() throws {
        #expect(IPv4.format(try value("192.168.1.50")) == "192.168.1.50")
        #expect(IPv4.format(try value("10.0.0.1")) == "10.0.0.1")
        #expect(IPv4.format(try value("0.0.0.0")) == "0.0.0.0")
        #expect(IPv4.format(try value("255.255.255.255")) == "255.255.255.255")
        #expect(IPv4.format(try value(" 192.168.0.1 ")) == "192.168.0.1")
    }

    @Test("Adresses malformées refusées")
    func parseRejectsMalformedAddresses() {
        #expect(IPv4.parse("") == nil)
        #expect(IPv4.parse("   ") == nil)
        #expect(IPv4.parse("192.168.1") == nil)
        #expect(IPv4.parse("192.168.1.1.1") == nil)
        #expect(IPv4.parse("192.168.1.256") == nil)
        #expect(IPv4.parse("192.168.1.-1") == nil)
        #expect(IPv4.parse("192.168.1.a") == nil)
        #expect(IPv4.parse("192..1.1") == nil)
        #expect(IPv4.parse("192.168.1.") == nil)
    }

    @Test("Masques contigus et non contigus")
    func maskValidation() throws {
        #expect(IPv4.isValidMask(try value("255.255.255.0")))
        #expect(IPv4.isValidMask(try value("255.255.0.0")))
        #expect(IPv4.isValidMask(try value("255.255.255.128")))
        #expect(IPv4.isValidMask(try value("255.255.255.255")))
        #expect(IPv4.isValidMask(try value("255.255.128.0")))

        #expect(!IPv4.isValidMask(try value("0.0.0.0")))
        #expect(!IPv4.isValidMask(try value("255.0.255.0")))
        #expect(!IPv4.isValidMask(try value("255.255.255.1")))
        #expect(!IPv4.isValidMask(try value("255.255.128.1")))
    }

    @Test("APIPA et adresses interdites")
    func apipaAndForbiddenAddresses() throws {
        #expect(IPv4.isAPIPA(try value("169.254.1.1")))
        #expect(IPv4.isAPIPA(try value("169.254.255.255")))
        #expect(!IPv4.isAPIPA(try value("169.253.1.1")))
        #expect(!IPv4.isAPIPA(try value("192.168.1.1")))

        #expect(IPv4.isForbidden(try value("0.0.0.0")))
        #expect(IPv4.isForbidden(try value("255.255.255.255")))
        #expect(!IPv4.isForbidden(try value("192.168.1.1")))
    }

    @Test("Aller-retour de format")
    func formatRoundTrip() throws {
        for address in ["1.2.3.4", "127.0.0.1", "172.16.254.1", "224.0.0.1"] {
            #expect(IPv4.format(try value(address)) == address)
        }
    }

    @Test("Passerelle suggérée dans le même sous-réseau")
    func suggestedGateway() {
        #expect(IPv4.suggestedGateway(address: "192.168.2.101", mask: "255.255.255.0") == "192.168.2.1")
        #expect(IPv4.suggestedGateway(address: "10.0.0.50", mask: "255.255.255.0") == "10.0.0.1")
        #expect(IPv4.suggestedGateway(address: "172.16.5.9", mask: "255.255.0.0") == "172.16.0.1")
        #expect(IPv4.suggestedGateway(address: "192.168.2.200", mask: "255.255.255.128") == "192.168.2.129")
    }

    @Test("Passerelle suggérée différente de l'adresse")
    func suggestedGatewayAvoidsAddress() {
        #expect(IPv4.suggestedGateway(address: "192.168.2.1", mask: "255.255.255.0") == "192.168.2.2")
        #expect(IPv4.suggestedGateway(address: "10.0.0.1", mask: "255.255.255.252") == "10.0.0.2")
    }

    @Test("Aucune passerelle suggérée si impossible")
    func suggestedGatewayRejectsInvalidInput() {
        #expect(IPv4.suggestedGateway(address: "192.168.2.101", mask: "") == nil)
        #expect(IPv4.suggestedGateway(address: "", mask: "255.255.255.0") == nil)
        #expect(IPv4.suggestedGateway(address: "192.168.2.101", mask: "255.0.255.0") == nil)
        #expect(IPv4.suggestedGateway(address: "0.0.0.0", mask: "255.255.255.0") == nil)
        #expect(IPv4.suggestedGateway(address: "169.254.1.1", mask: "255.255.0.0") == nil)
        #expect(IPv4.suggestedGateway(address: "192.168.2.101", mask: "255.255.255.254") == nil)
        #expect(IPv4.suggestedGateway(address: "192.168.2.101", mask: "255.255.255.255") == nil)
    }
}
