import Darwin
import XCTest

/// The iOS UI driver (artifact/topics/ios-c-abi-2, UI driver design v3): runs next to the Unity player tests,
/// takes their requests over 127.0.0.1 and presses the system screens they open. scripts/verify_unity_ios.sh
/// starts it with -only-testing:NtkUiTests/DriverUITests/test_drive; it is not in DeviceRequirements.json and
/// scripts/test_ios_ui.sh never runs it.
///
/// Environment (xcodebuild passes TEST_RUNNER_<name> as <name>):
/// - NTK_DRIVER_BUNDLE: the player's bundle id (required). A HELLO naming another app is refused.
/// - NTK_DRIVER_LIMIT: seconds to wait for BYE (default 900).
/// - NTK_DRIVER_DEVICE: "1" on a device run; the commands of design chapter 7 are refused there.
///
/// The test-only command `drop-next` makes the driver handle the next request and then drop the connection
/// once before answering it (V-11: the player connects again and sends the request again; it is answered from
/// memory, not pressed again).
///
/// Privacy (design chapter 7): nothing read from a screen is printed or kept: no element tree, no screenshot,
/// no label. Failures answer with fixed words only.
final class DriverUITests: XCTestCase {
    private static let allowLabels = ["Allow", "許可"]
    private static let dontAllowLabels = ["Don’t Allow", "Don't Allow", "許可しない"]
    private static let settingsBundle = "com.apple.Preferences"

    private let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
    private var app: XCUIApplication!
    private var onDevice = false

    private func log(_ text: String) {
        FileHandle.standardError.write(Data("[NTK-UI-DRIVER] \(text)\n".utf8))
    }

    override func setUp() {
        super.setUp()
        continueAfterFailure = false
    }

    func test_drive() throws {
        let env = ProcessInfo.processInfo.environment
        guard let bundle = env["NTK_DRIVER_BUNDLE"], !bundle.isEmpty else {
            XCTFail("NTK_DRIVER_BUNDLE is not set (scripts/verify_unity_ios.sh starts this test)")
            return
        }
        let limit = TimeInterval(env["NTK_DRIVER_LIMIT"] ?? "") ?? 900
        onDevice = env["NTK_DRIVER_DEVICE"] == "1"
        var dropNext = false
        app = XCUIApplication(bundleIdentifier: bundle)

        let server = try LoopbackServer()
        defer { server.close() }
        // Both streams: scripts/verify_unity_ios.sh reads the port from xcodebuild's output (the spike saw stdout relayed).
        log("listening \(server.port)")
        print("[NTK-UI-DRIVER] listening \(server.port)")

        let deadline = Date().addingTimeInterval(limit)
        var run: String?
        var answers: [Int: String] = [:]
        var handled = 0
        var connections = 0

        while Date() < deadline {
            guard let connection = server.accept(until: deadline) else { break }
            defer { connection.close() }
            guard let hello = connection.readLine(until: Date().addingTimeInterval(10)) else { continue }
            let parts = hello.split(separator: " ", omittingEmptySubsequences: false).map(String.init)
            guard parts.count == 3, parts[0] == "HELLO", parts[2] == bundle,
                  parts[1].count == 32, parts[1].allSatisfy({ $0.isHexDigit && !$0.isUppercase }) else {
                log("refused a connection")
                continue
            }
            if let current = run, current != parts[1] {
                log("refused another run")
                continue
            }
            run = parts[1]
            connections += 1
            guard connection.write("READY \(parts[1])") else { continue }

            while let line = connection.readLine(until: deadline) {
                if line == "BYE \(parts[1])" {
                    _ = connection.write("BYE ok")
                    log("bye connections=\(connections) handled=\(handled)")
                    return
                }
                let words = line.split(separator: " ").map(String.init)
                guard words.count >= 2, let number = Int(words[0]), number > 0 else {
                    log("ignored a line that is not a request")
                    continue
                }
                let answer: String
                var drop = false
                if let remembered = answers[number] {
                    answer = remembered
                    log("\(number) answered from memory")
                } else if words[1] == "drop-next" && words.count == 2 {
                    dropNext = true
                    answer = "ok"
                    answers[number] = answer
                } else {
                    answer = handle(command: words[1], args: Array(words.dropFirst(2)))
                    answers[number] = answer
                    handled += 1
                    log("\(number) \(words[1]) -> \(answer.hasPrefix("ok") ? "ok" : answer)")
                    drop = dropNext
                    dropNext = false
                }
                if drop {
                    log("dropping the connection before answering \(number)")
                    break
                }
                guard connection.write("\(number) \(answer)") else { break }
            }
        }
        XCTFail("no BYE within \(Int(limit)) s (connections=\(connections), handled=\(handled))")
    }

    // MARK: Commands (design 3.2)

    private func handle(command: String, args: [String]) -> String {
        switch command {
        case "alert-tap":
            guard args.count == 1, let label = decode(args[0]) else { return "fail bad-arg" }
            return alertTap(label)
        case "alert-type":
            guard args.count == 3, ["text", "secure"].contains(args[0]), let index = Int(args[1]), (0...3).contains(index),
                  let text = decode(args[2]), (1...64).contains(text.count),
                  text.unicodeScalars.allSatisfy({ (0x20...0x7E).contains($0.value) }) else { return "fail bad-arg" }
            return alertType(secure: args[0] == "secure", index: index, text: text)
        case "alert-absent":
            guard args.count == 2, let ms = Int(args[0]), (1000...30000).contains(ms), let close = decode(args[1]) else { return "fail bad-arg" }
            return alertAbsent(ms: ms, closeLabel: close)
        case "sheet-close":
            guard args.isEmpty else { return "fail bad-arg" }
            return screenCommand { sheetClose() }
        case "sheet-absent":
            guard args.count == 1, let ms = Int(args[0]), (1000...30000).contains(ms) else { return "fail bad-arg" }
            return screenCommand { sheetAbsent(ms: ms) }
        case "permission-answer":
            guard args.count == 1, ["allow", "deny"].contains(args[0]) else { return "fail bad-arg" }
            return screenCommand { permissionAnswer(allow: args[0] == "allow") }
        case "settings-close":
            guard args.isEmpty else { return "fail bad-arg" }
            return screenCommand { settingsClose() }
        case "banner-tap":
            if onDevice { return "fail device-only-refused" }
            guard args.count == 1, let title = decode(args[0]) else { return "fail bad-arg" }
            return screenCommand { bannerTap(title) }
        case "background":
            if onDevice { return "fail device-only-refused" }
            guard args.count == 1, let ms = Int(args[0]), (100...5000).contains(ms) else { return "fail bad-arg" }
            return background(ms: ms)
        case "front":
            guard args.isEmpty else { return "fail bad-arg" }
            return front() ? "ok" : "fail not-in-front"
        default:
            return "fail bad-arg"
        }
    }

    private func decode(_ base64: String) -> String? {
        guard let data = Data(base64Encoded: base64) else { return nil }
        return String(data: data, encoding: .utf8)
    }

    /// The app's alert, or its action sheet. A fresh query each time (V-8).
    private func currentAlert() -> XCUIElement? {
        let alert = app.alerts.firstMatch
        if alert.exists { return alert }
        let sheet = app.sheets.firstMatch
        return sheet.exists ? sheet : nil
    }

    private func waitForAlert(_ seconds: TimeInterval) -> XCUIElement? {
        let end = Date().addingTimeInterval(seconds)
        repeat {
            if let alert = currentAlert() { return alert }
            Thread.sleep(forTimeInterval: 0.25)
        } while Date() < end
        return nil
    }

    private func waitForNoAlert(_ seconds: TimeInterval) -> Bool {
        let end = Date().addingTimeInterval(seconds)
        repeat {
            if currentAlert() == nil { return true }
            Thread.sleep(forTimeInterval: 0.25)
        } while Date() < end
        return false
    }

    private func alertTap(_ label: String) -> String {
        guard let alert = waitForAlert(10) else { return "fail not-found" }
        let button = alert.buttons.matching(NSPredicate(format: "label == %@", label))
        guard button.count == 1 else { return "fail not-found" }
        button.firstMatch.tap()
        return waitForNoAlert(10) ? "ok" : "fail did-not-close"
    }

    private func alertType(secure: Bool, index: Int, text: String) -> String {
        guard let alert = waitForAlert(10) else { return "fail not-found" }
        // A device run never types into an alert that has a secure field (design chapter 7): password saving
        // and AutoFill would reach the user's keychain.
        if onDevice && alert.secureTextFields.count > 0 { return "fail device-only-refused" }
        let fields = secure ? alert.secureTextFields : alert.textFields
        guard index < fields.count else { return "fail not-found" }
        let field = fields.element(boundBy: index)
        field.tap()
        field.typeText(text)
        return "ok"
    }

    private func alertAbsent(ms: Int, closeLabel: String) -> String {
        let end = Date().addingTimeInterval(TimeInterval(ms) / 1000)
        repeat {
            if let alert = currentAlert() {
                let close = alert.buttons.matching(NSPredicate(format: "label == %@", closeLabel))
                if close.count == 1 { close.firstMatch.tap() }
                _ = waitForNoAlert(10)
                return "fail alert-opened"
            }
            Thread.sleep(forTimeInterval: 0.25)
        } while Date() < end
        return "ok"
    }

    /// A screen command: on any failure, close what is showing and bring the player back before answering
    /// (design 3.2), so a later test is not left behind a sheet or a prompt.
    private func screenCommand(_ body: () -> String) -> String {
        let answer = body()
        if answer.hasPrefix("fail") {
            if SystemUI.shareSheet(app, timeout: 1) { _ = SystemUI.closeShareSheet(app, timeout: 5) }
            let prompt = springboard.alerts.buttons.matching(NSPredicate(format: "label IN %@", Self.dontAllowLabels))
            if prompt.firstMatch.exists { prompt.firstMatch.tap() }
            _ = front()
        }
        return answer
    }

    private func sheetClose() -> String {
        guard SystemUI.shareSheet(app, timeout: 15) else { return "fail sheet-did-not-open" }
        guard SystemUI.closeShareSheet(app, timeout: 10) else { return "fail did-not-close" }
        let end = Date().addingTimeInterval(10)
        while SystemUI.shareSheet(app, timeout: 0.5) {
            if Date() > end { return "fail did-not-close" }
        }
        return "ok"
    }

    private func sheetAbsent(ms: Int) -> String {
        let end = Date().addingTimeInterval(TimeInterval(ms) / 1000)
        repeat {
            if SystemUI.shareSheet(app, timeout: 0.25) { return "fail sheet-opened" }
        } while Date() < end
        return "ok"
    }

    private func permissionAnswer(allow: Bool) -> String {
        let prompt = springboard.alerts.firstMatch
        guard prompt.waitForExistence(timeout: 15) else { return "fail dialog-did-not-open" }
        let button = springboard.alerts.buttons.matching(NSPredicate(format: "label IN %@", allow ? Self.allowLabels : Self.dontAllowLabels))
        guard button.count == 1 else { return "fail not-found" }
        button.firstMatch.tap()
        let end = Date().addingTimeInterval(10)
        while springboard.alerts.firstMatch.exists {
            if Date() > end { return "fail did-not-close" }
            Thread.sleep(forTimeInterval: 0.25)
        }
        return front() ? "ok" : "fail not-in-front"
    }

    /// Settings can take more than 10 s to come to the front (2026-11-15, simulator: a cold start), so wait 30 s,
    /// and when it did not come, watch 5 s more before answering, so a late Settings does not strand the player.
    /// The player's `state` is not trusted here: for an app this runner did not launch it read as in front while
    /// SpringBoard had the player in the background (2026-11-15, three runs). So the player is activated
    /// unconditionally, and "back" means Settings is no longer in front, checked again after 3 s, up to three
    /// times. The steps are logged with their times and states only (nothing read from a screen).
    private func settingsClose() -> String {
        let settings = XCUIApplication(bundleIdentifier: Self.settingsBundle)
        let start = Date()
        var opened = settings.wait(for: .runningForeground, timeout: 30)
        if !opened { opened = settings.wait(for: .runningForeground, timeout: 5) }
        log(String(format: "settings-close: opened=%@ after %.1fs", opened ? "yes" : "no", Date().timeIntervalSince(start)))
        if opened { Thread.sleep(forTimeInterval: 3) }
        for attempt in 1...3 {
            app.activate()
            _ = settings.wait(for: .runningBackground, timeout: 10)
            Thread.sleep(forTimeInterval: 3)
            let settingsFront = settings.state == .runningForeground
            log("settings-close: attempt \(attempt) settings-state=\(settings.state.rawValue) player-state=\(app.state.rawValue)")
            if !settingsFront { return opened ? "ok opened" : "ok not-opened" }
        }
        return "fail not-in-front"
    }

    private func bannerTap(_ title: String) -> String {
        guard SystemUI.tapBanner(springboard, title: title, timeout: 25) else { return "fail not-found" }
        return app.wait(for: .runningForeground, timeout: 10) ? "ok" : "fail not-in-front"
    }

    private func background(ms: Int) -> String {
        XCUIDevice.shared.press(.home)
        guard app.wait(for: .runningBackground, timeout: 10) || app.wait(for: .runningBackgroundSuspended, timeout: 1) else {
            _ = front()
            return "fail not-background"
        }
        Thread.sleep(forTimeInterval: TimeInterval(ms) / 1000)
        return front() ? "ok" : "fail not-in-front"
    }

    private func front() -> Bool {
        if app.state == .runningForeground { return true }
        app.activate()
        return app.wait(for: .runningForeground, timeout: 10)
    }
}

/// A TCP server bound to 127.0.0.1 only, on a port the system chooses (design D-1, D-3). BSD sockets, as in
/// the spike that showed the player's IL2CPP TcpClient reaching it.
private final class LoopbackServer {
    let port: UInt16
    private let fd: Int32

    init() throws {
        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw NSError(domain: "LoopbackServer", code: Int(errno)) }
        var address = sockaddr_in()
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = 0
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        let bound = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { bind(fd, $0, length) }
        }
        guard bound == 0, listen(fd, 4) == 0 else { Darwin.close(fd); throw NSError(domain: "LoopbackServer", code: Int(errno)) }
        let named = withUnsafeMutablePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { getsockname(fd, $0, &length) }
        }
        guard named == 0 else { Darwin.close(fd); throw NSError(domain: "LoopbackServer", code: Int(errno)) }
        self.fd = fd
        port = UInt16(bigEndian: address.sin_port)
    }

    func accept(until deadline: Date) -> LineConnection? {
        let ms = Int32(max(0, deadline.timeIntervalSinceNow) * 1000)
        var p = pollfd(fd: fd, events: Int16(POLLIN), revents: 0)
        guard poll(&p, 1, ms) == 1 else { return nil }
        let client = Darwin.accept(fd, nil, nil)
        return client >= 0 ? LineConnection(fd: client) : nil
    }

    func close() { Darwin.close(fd) }
}

/// One connection, read and written in UTF-8 lines.
private final class LineConnection {
    private let fd: Int32
    private var buffer = Data()

    init(fd: Int32) {
        self.fd = fd
        var on: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &on, socklen_t(MemoryLayout<Int32>.size))
    }

    /// The next line, or nil when the connection closed or the deadline passed.
    func readLine(until deadline: Date) -> String? {
        while true {
            if let newline = buffer.firstIndex(of: 0x0A) {
                let line = String(decoding: buffer[buffer.startIndex..<newline], as: UTF8.self)
                buffer.removeSubrange(buffer.startIndex...newline)
                return line.hasSuffix("\r") ? String(line.dropLast()) : line
            }
            let ms = Int32(max(0, min(deadline.timeIntervalSinceNow, 60)) * 1000)
            if ms == 0 { return nil }
            var p = pollfd(fd: fd, events: Int16(POLLIN), revents: 0)
            let ready = poll(&p, 1, ms)
            if ready < 0 { return nil }
            if ready == 0 { continue }
            var chunk = [UInt8](repeating: 0, count: 4096)
            let n = read(fd, &chunk, chunk.count)
            if n <= 0 { return nil }
            buffer.append(contentsOf: chunk[0..<n])
            if buffer.count > 8192 { return nil }
        }
    }

    func write(_ line: String) -> Bool {
        let bytes = Array((line + "\n").utf8)
        var sent = 0
        while sent < bytes.count {
            let n = bytes.withUnsafeBytes { Darwin.write(fd, $0.baseAddress! + sent, bytes.count - sent) }
            if n <= 0 { return false }
            sent += n
        }
        return true
    }

    func close() { Darwin.close(fd) }
}
