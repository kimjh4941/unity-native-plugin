import XCTest

/// Base of the UI tests (artifact/topics/ios-c-abi-2, UI automation design v4, 4.4).
///
/// The harness is a Unity app this project does not build: scripts/test_ios_ui.sh installs it before each
/// test, and the test launches it by bundle id. The scenario goes in the environment (NTK_SCENARIO):
/// launch arguments do not reach the iOS player. Rows are buttons `row.<id>.run`; results are labels
/// `result.line.<n>` reading `[<id>@<state>] <value>`.
///
/// An alert the test did not expect fails the test: the interruption monitor only marks it (it never
/// presses anything), and every step and tearDown check the mark and the springboard (native-toolkit's
/// CApiUITestCase). Failure messages never quote springboard labels.
class HarnessUITestCase: XCTestCase {
    static let harnessBundleId = "com.jonghyunkim.nativetoolkit.uiharness"
    static let timeout: TimeInterval = 10

    var app: XCUIApplication!
    let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
    private var unexpectedAlert = false
    private var monitor: NSObjectProtocol?

    override func setUp() {
        super.setUp()
        continueAfterFailure = false
        monitor = addUIInterruptionMonitor(withDescription: "unexpected alert") { [weak self] _ in
            self?.unexpectedAlert = true
            return true
        }
    }

    override func tearDown() {
        if let monitor { removeUIInterruptionMonitor(monitor) }
        let leftOver = springboard.alerts.count
        let leftInApp = (app?.alerts.count ?? 0) + (app?.sheets.count ?? 0)
        XCTAssertFalse(unexpectedAlert, "an unexpected alert appeared during the test")
        XCTAssertEqual(leftOver, 0, "a system alert is still on screen after the test")
        XCTAssertEqual(leftInApp, 0, "an alert of the harness is still on screen after the test")
        super.tearDown()
    }

    /// Launches the harness with a scenario and waits for its launch line.
    func launch(_ scenario: String) {
        app = XCUIApplication(bundleIdentifier: Self.harnessBundleId)
        app.launchEnvironment = ["NTK_SCENARIO": scenario]
        app.launch()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 30), "the harness did not come to the foreground")
        XCTAssertTrue(result("[launch@ok] scenario=\(scenario) source=env").waitForExistence(timeout: 30),
                      "the harness did not report the scenario")
    }

    /// The first result line that starts with the prefix.
    func result(_ prefix: String) -> XCUIElement {
        app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label BEGINSWITH %@", prefix)).firstMatch
    }

    /// Presses a row's button.
    func run(_ row: String) {
        let button = app.buttons["row.\(row).run"]
        XCTAssertTrue(button.waitForExistence(timeout: Self.timeout), "row \(row) is missing")
        button.tap()
        checkNoUnexpectedAlert()
    }

    /// Waits for a result line and fails with the row's name (not the screen's text) when it does not come.
    func expectResult(_ prefix: String, timeout: TimeInterval = HarnessUITestCase.timeout) {
        XCTAssertTrue(result(prefix).waitForExistence(timeout: timeout), "no result line starting with \(prefix)")
        checkNoUnexpectedAlert()
    }

    func checkNoUnexpectedAlert() {
        XCTAssertFalse(unexpectedAlert, "an unexpected alert appeared")
    }

    /// The finished result lines (state ok or fail) of a result id, top to bottom.
    func finishedLines(_ id: String) -> [String] {
        app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND (label BEGINSWITH %@ OR label BEGINSWITH %@)",
                                             "[\(id)@ok]", "[\(id)@fail]")).allElementsBoundByIndex.map(\.label)
    }

    /// Waits for the first expected line (whole-label match), lets late lines arrive for `settle` seconds,
    /// then requires the id's finished lines to be exactly `expected` - a callback that came twice, or
    /// one that did not come, fails (Dialog UI test plan v3: exactly once, compared in full).
    func expectLines(_ id: String, _ expected: [String], settle: TimeInterval = 3) {
        if let first = expected.first {
            let line = app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label == %@", first)).firstMatch
            XCTAssertTrue(line.waitForExistence(timeout: Self.timeout), "no result line \(first)")
        }
        Thread.sleep(forTimeInterval: settle)
        XCTAssertEqual(finishedLines(id), expected)
        checkNoUnexpectedAlert()
    }

    /// Simulator-only items (backgrounding, Control Center, login): fail on a device rather than act.
    func requireSimulator() throws {
        #if !targetEnvironment(simulator)
        throw XCTSkip("simulator only")
        #endif
    }
}
