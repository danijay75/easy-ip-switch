// swift-tools-version:5.7
import PackageDescription

let package = Package(
    name: "EasyIPSwitch",
    platforms: [
        .macOS(.v12)
    ],
    products: [
        .executable(name: "EasyIPSwitch", targets: ["EasyIPSwitch"])
    ],
    targets: [
        .executableTarget(
            name: "EasyIPSwitch",
            path: "Sources/EasyIPSwitch"
        ),
        .testTarget(
            name: "EasyIPSwitchTests",
            dependencies: ["EasyIPSwitch"],
            path: "Tests/EasyIPSwitchTests"
        )
    ]
)
