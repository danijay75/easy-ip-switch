import Testing
@testable import EasyIPSwitch

@Suite("Analyse du ping")
struct PingRunnerTests {
    @Test("Statistiques complètes")
    func parsesCompleteStatistics() throws {
        let output = """
        PING 192.168.1.1 (192.168.1.1): 56 data bytes
        64 bytes from 192.168.1.1: icmp_seq=0 ttl=64 time=1.234 ms
        64 bytes from 192.168.1.1: icmp_seq=1 ttl=64 time=2.345 ms
        64 bytes from 192.168.1.1: icmp_seq=2 ttl=64 time=3.456 ms

        --- 192.168.1.1 ping statistics ---
        3 packets transmitted, 3 packets received, 0.0% packet loss
        round-trip min/avg/max/stddev = 1.234/2.345/3.456/0.567 ms
        """

        let statistics = try #require(PingRunner.parse(output))
        #expect(statistics.transmitted == 3)
        #expect(statistics.received == 3)
        #expect(statistics.loss == 0.0)
        #expect(statistics.average == 2.345)
    }

    @Test("Perte totale de paquets")
    func parsesTotalPacketLoss() throws {
        let output = """
        --- 10.0.0.99 ping statistics ---
        4 packets transmitted, 0 packets received, 100.0% packet loss
        """

        let statistics = try #require(PingRunner.parse(output))
        #expect(statistics.transmitted == 4)
        #expect(statistics.received == 0)
        #expect(statistics.loss == 100.0)
        #expect(statistics.average == nil)
    }

    @Test("Sortie sans statistiques")
    func rejectsOutputWithoutStatistics() {
        #expect(PingRunner.parse("ping: sendto: No route to host") == nil)
    }
}
