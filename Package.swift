// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "RekordboxSetupCloner",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "RekordboxSetupCloner", targets: ["RekordboxSetupCloner"])],
    targets: [.executableTarget(name: "RekordboxSetupCloner")]
)
