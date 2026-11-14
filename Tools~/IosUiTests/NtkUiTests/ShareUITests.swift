import XCTest

/// Share UI test plan v3 (artifact/ios/share/designs/2026-11-14-ios-share-ui-test-plan-v3.md), section 1.
/// Lines are the harness's ShareScenario rows; only Close (and Copy on the simulator) is pressed, and no
/// cell of the sheet is read on a device.
///
/// Every result is checked twice: the per-call callback (`share`) and the ShareCompleted event
/// (`share-event`), in that order on the screen - the event comes first - and exactly once each. Items
/// whose call fails also check that no share sheet appears.
final class ShareUITests: HarnessUITestCase {
    private static let closedValue = "completed=False activity=<null>"
    private static let noRootValue = "completed=False activity=<null> error=No root view controller available to present the share sheet."

    override func tearDown() {
        if let app, app.state == .runningForeground {
            XCTAssertFalse(SystemUI.shareSheet(app, timeout: 0), "a share sheet is still on screen after the test")
        }
        super.tearDown()
    }

    private func sheet(timeout: TimeInterval = 15) -> Bool { SystemUI.shareSheet(app, timeout: timeout) }

    /// Waits for the sheet and closes it with Close.
    private func openAndClose() {
        XCTAssertTrue(sheet(), "the share sheet did not appear")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        checkNoUnexpectedAlert()
    }

    /// For calls that fail: no sheet may appear within `wait` seconds. One that does is closed, then fails the test.
    private func expectNoSheet(wait: TimeInterval = 3) {
        if sheet(timeout: wait) {
            _ = SystemUI.closeShareSheet(app)
            XCTFail("a share sheet appeared")
        }
        checkNoUnexpectedAlert()
    }

    private func waitLine(_ line: String) {
        let element = app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label == %@", line)).firstMatch
        XCTAssertTrue(element.waitForExistence(timeout: Self.timeout), "no result line \(line); lines: \(harnessLines())")
    }

    /// The harness's own result lines (its text only, never the system's), for failure messages.
    private func harnessLines() -> [String] {
        app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.'")).allElementsBoundByIndex.map(\.label)
    }

    /// The event line and then the callback line, each exactly once.
    private func expectShare(_ state: String, _ value: String) {
        expectLines(ofIds: ["share-event", "share"], ["[share-event@\(state)] \(value)", "[share@\(state)] \(value)"])
    }

    private func dismissCase(_ row: String) {
        launch("share"); run(row)
        openAndClose()
        expectShare("ok", Self.closedValue)
    }

    func test_SH1() { dismissCase("sh-text") }
    func test_SH2() { dismissCase("sh-url") }
    func test_SH3() { dismissCase("sh-text-url") }
    func test_SH4() { dismissCase("sh-image") }
    func test_SH5() { dismissCase("sh-file") }

    func test_SH6() throws {
        try requireSimulator()
        launch("share"); run("sh-text")
        XCTAssertTrue(sheet(), "the share sheet did not appear")
        XCTAssertTrue(SystemUI.pressCopy(app), "Copy was not shown exactly once")
        expectShare("ok", "completed=True activity=com.apple.UIKit.activity.CopyToPasteboard")
    }

    /// Copy must be absent. The list is read 2 s after the header (`UIActivityContentView`) and Close are
    /// shown (native-toolkit's S12), and the same test checks that other action cells are there (the list
    /// was drawn); SH-6 is the control for the Copy query.
    func test_SH7() throws {
        try requireSimulator()
        launch("share"); run("sh-exclude-copy")
        let header = app.navigationBars.matching(identifier: "UIActivityContentView").firstMatch
        XCTAssertTrue(header.waitForExistence(timeout: 15), "the share sheet header did not appear")
        XCTAssertTrue(app.buttons.matching(NSPredicate(format: "label IN {'Close', '閉じる'}")).firstMatch.waitForExistence(timeout: 10))
        Thread.sleep(forTimeInterval: 2)
        XCTAssertGreaterThan(app.cells.count, 0, "no action cell was drawn")
        XCTAssertEqual(SystemUI.copyCells(app).count, 0, "Copy is shown although it was excluded")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        expectShare("ok", Self.closedValue)
    }

    func test_SH8() throws {
        try requireSimulator()
        launch("share"); run("sh-preview")
        XCTAssertTrue(sheet(), "the share sheet did not appear")
        let titles = SystemUI.headerTitles(app, for: 3)
        let shown = app.descendants(matching: .any).matching(NSPredicate(format: "label == 'NTK Preview'")).allElementsBoundByIndex
            .map { "\($0.elementType.rawValue):\($0.identifier)" }
        XCTAssertTrue(titles.contains("NTK Preview"), "the header title was \(titles); elements labelled NTK Preview: \(shown); bars: \(app.navigationBars.allElementsBoundByIndex.map { "\($0.identifier)/\($0.children(matching: .other).count)" })")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        expectShare("ok", Self.closedValue)
    }

    func test_SH9() {
        launch("share"); run("sh-bad-url")
        expectNoSheet()
        expectShare("fail", "completed=False activity=<null> error=Invalid URL: not a url.")
    }

    func test_SH10() {
        launch("share"); run("sh-text-bad-url")
        expectNoSheet()
        expectShare("fail", "completed=False activity=<null> error=Invalid URL: not a url.")
    }

    func test_SH11() {
        launch("share"); run("sh-missing-image")
        expectNoSheet()
        expectShare("fail", "completed=False activity=<null> error=Failed to load image at path: <data>/missing.png.")
    }

    func test_SH12() {
        launch("share"); run("sh-missing-file")
        expectNoSheet()
        expectShare("fail", "completed=False activity=<null> error=File not found at path: <data>/missing.txt.")
    }

    /// Two shares in one frame. Each callback and the event are written under their own ids and compared
    /// as sets (Share.h does not order separate shares). 1.x, measured on the simulator (2026-11-14): one
    /// sheet, and its result reaches only the second callback and the event; the other call never
    /// completes (the plan v3 expected a presentation failure for the second call - it did not come).
    func test_SH13() {
        launch("share"); run("sh-twice")
        openAndClose()
        if sheet(timeout: 2) {
            XCTAssertTrue(SystemUI.closeShareSheet(app), "the second share sheet has no Close button")
        }
        waitLine("[share2@ok] \(Self.closedValue)")
        Thread.sleep(forTimeInterval: 3)
        XCTAssertEqual(finishedLines("share1"), [], "lines: \(harnessLines())")
        XCTAssertEqual(finishedLines("share2"), ["[share2@ok] \(Self.closedValue)"], "lines: \(harnessLines())")
        XCTAssertEqual(finishedLines("share-event"), ["[share-event@ok] \(Self.closedValue)"], "lines: \(harnessLines())")
        checkNoUnexpectedAlert()
    }

    /// The sheet survives a trip to the home screen, with no result yet; it is closed after coming back.
    func test_SH14() throws {
        try requireSimulator()
        launch("share"); run("sh-text")
        XCTAssertTrue(sheet(), "the share sheet did not appear")
        XCUIDevice.shared.press(.home)
        Thread.sleep(forTimeInterval: 3)
        app.activate()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15))
        XCTAssertTrue(sheet(timeout: 5), "the share sheet did not survive the trip to the home screen")
        XCTAssertEqual(finishedLines(ofIds: ["share", "share-event"]), [], "a result came before Close")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        expectShare("ok", Self.closedValue)
    }

    /// The call runs 3 s after the row, natively, while Control Center is over the app.
    func test_SH15() throws {
        try requireSimulator()
        launch("share"); run("sh-late")
        let springboard = self.springboard
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.005))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.6)))
        Thread.sleep(forTimeInterval: 5)
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.95))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.2)))
        waitLine("[late@event] state=foregroundInactive")
        expectNoSheet()
        expectShare("fail", Self.noRootValue)
    }

    func test_SH16() {
        launch("share"); run("sh-unknown-type")
        openAndClose()
        expectShare("ok", Self.closedValue)
    }

    /// A null in the excluded list: 1.x drops the whole list, so the sheet appears with Copy.
    func test_SH17() throws {
        try requireSimulator()
        launch("share"); run("sh-exclude-null")
        XCTAssertTrue(sheet(), "the share sheet did not appear")
        Thread.sleep(forTimeInterval: 2)
        XCTAssertEqual(SystemUI.copyCells(app).count, 1, "Copy was expected (1.x drops the excluded list)")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        expectShare("ok", Self.closedValue)
    }

    /// The call is made when the harness enters the background (a background task), and the harness is
    /// brought back 5 s later. The state when the result arrived is written as `[late@result]`.
    func test_SH18() throws {
        try requireSimulator()
        launch("share"); run("sh-background")
        XCUIDevice.shared.press(.home)
        Thread.sleep(forTimeInterval: 5)
        app.activate()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15))
        waitLine("[late@event] state=background")
        expectNoSheet()
        expectShare("fail", Self.noRootValue)
        let resultState = app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label BEGINSWITH '[late@result]'"))
            .allElementsBoundByIndex.map(\.label)
        // 1.x, measured on the simulator (2026-11-14): the failure arrives after the harness is back.
        XCTAssertEqual(resultState, ["[late@result] state=foregroundActive"], "lines: \(harnessLines())")
    }
}
