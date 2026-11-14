import XCTest

/// The Dialog UI test plan v3 (artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md).
/// Expected lines are the 1.x record (artifact/ios/dialog/results/2026-11-14-ios-dialog-1x-record-result-v1.md).
/// Every finished line of all six dialog kinds is compared, so an extra event of another kind fails.
final class DialogUITests: HarnessUITestCase {
    private var alert: XCUIElement { app.alerts.firstMatch }
    private static let kinds = ["dialog", "confirm", "destructive", "sheet", "text", "login"]

    private func expectDialog(_ expected: [String]) { expectLines(ofIds: Self.kinds, expected) }

    private func press(_ label: String, in element: XCUIElement? = nil) {
        let container = element ?? alert
        XCTAssertTrue(container.waitForExistence(timeout: Self.timeout), "the dialog did not appear")
        let button = container.buttons[label]
        XCTAssertTrue(button.waitForExistence(timeout: Self.timeout), "no button \(label)")
        button.tap()
    }

    func test_ID1() {
        launch("dialog"); run("show-dialog"); press("OK")
        expectDialog(["[dialog@ok] button=OK"])
    }

    func test_ID2() {
        launch("dialog"); run("show-dialog-ja"); press("了解")
        expectDialog(["[dialog@ok] button=了解"])
    }

    func test_ID3() {
        launch("dialog"); run("show-confirm"); press("OK")
        expectDialog(["[confirm@ok] button=OK"])
    }

    func test_ID4() {
        launch("dialog"); run("show-confirm"); press("Cancel")
        expectDialog(["[confirm@ok] button=Cancel"])
    }

    func test_ID5() {
        launch("dialog"); run("show-destructive"); press("Delete")
        expectDialog(["[destructive@ok] button=Delete"])
    }

    func test_ID6() {
        launch("dialog"); run("show-destructive"); press("Cancel")
        expectDialog(["[destructive@ok] button=Cancel"])
    }

    func test_ID7() {
        launch("dialog"); run("show-sheet"); press("B", in: app.sheets.firstMatch)
        expectDialog(["[sheet@ok] button=B"])
    }

    func test_ID8() {
        launch("dialog"); run("show-sheet"); press("Cancel", in: app.sheets.firstMatch)
        expectDialog(["[sheet@ok] button=Cancel"])
    }

    func test_ID9() {
        launch("dialog"); run("show-text")
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout))
        alert.textFields.firstMatch.tap()
        alert.textFields.firstMatch.typeText("ntk42")
        press("OK")
        expectDialog(["[text@ok] button=OK input=ntk42"])
    }

    func test_ID10() {
        launch("dialog"); run("show-text")
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout))
        XCTAssertFalse(alert.buttons["OK"].isEnabled, "OK should be disabled while the field is empty")
        press("Cancel")
        expectDialog(["[text@ok] button=Cancel input=<null>"])
    }

    func test_ID11() {
        launch("dialog"); run("show-text-empty-ok"); press("OK")
        expectDialog(["[text@ok] button=OK input=\"\""])
    }

    func test_ID12() throws {
        try requireSimulator()
        launch("dialog"); run("show-login")
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout))
        alert.textFields.firstMatch.tap()
        alert.textFields.firstMatch.typeText("user1")
        alert.secureTextFields.firstMatch.tap()
        alert.secureTextFields.firstMatch.typeText("pw1234")
        press("Login")
        expectDialog(["[login@ok] button=Login username=user1 password-length=6"])
    }

    func test_ID13() throws {
        try requireSimulator()
        launch("dialog"); run("show-login")
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout))
        XCTAssertFalse(alert.buttons["Login"].isEnabled, "Login should be disabled while the fields are empty")
        press("Cancel")
        expectDialog(["[login@ok] button=Cancel username=<null> password=<null>"])
    }

    /// Recorded on 1.x (2026-11-14, simulator): both dialogs appear, the second on top - the plan's
    /// prediction (the second dropped) was wrong. Both are pressed, top first.
    func test_ID14a() {
        launch("dialog"); run("twice-now")
        XCTAssertTrue(app.buttons["OK2"].waitForExistence(timeout: Self.timeout), "the second dialog did not appear")
        press("OK2", in: app)
        press("OK1", in: app)
        expectDialog(["[confirm@ok] button=OK2", "[confirm@ok] button=OK1"])
    }

    func test_ID14b() {
        launch("dialog"); run("twice-later")
        XCTAssertTrue(result("[twice@event] first-shown=yes").waitForExistence(timeout: Self.timeout), "the first dialog was not on screen when the second was requested")
        XCTAssertTrue(app.buttons["OK2"].waitForExistence(timeout: Self.timeout), "the second dialog did not appear")
        press("OK2", in: app)
        press("OK1", in: app)
        expectDialog(["[confirm@ok] button=OK2", "[confirm@ok] button=OK1"])
    }

    func test_ID15() throws {
        try requireSimulator()
        launch("dialog"); run("show-dialog")
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout))
        XCUIDevice.shared.press(.home)
        Thread.sleep(forTimeInterval: 3)
        app.activate()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15))
        press("OK")
        expectDialog(["[dialog@ok] button=OK"])
    }

    func test_ID16() throws {
        try requireSimulator()
        launch("dialog"); run("show-login-empty-ok"); press("Login")
        expectDialog(["[login@ok] button=Login username=\"\" password-length=0"])
    }

    func test_ID17() throws {
        try requireSimulator()
        launch("dialog"); run("late")
        let springboard = self.springboard
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.005))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.6)))
        Thread.sleep(forTimeInterval: 5)
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.95))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.2)))
        let lateLine = app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label == %@", "[late@event] state=foregroundInactive")).firstMatch
        XCTAssertTrue(lateLine.waitForExistence(timeout: Self.timeout), "the delayed call did not run while inactive")
        if alert.waitForExistence(timeout: 3) { press("OK") }
        expectDialog(["[dialog@fail] button=<null> error=Failed to get root view controller"])
    }
}
