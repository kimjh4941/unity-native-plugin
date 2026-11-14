import XCTest

final class DialogUITests: HarnessUITestCase {
    /// A confirm dialog answered with OK reports the OK button.
    func test_confirmOk() {
        launch("dialog")
        run("confirm")
        let alert = app.alerts.firstMatch
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout), "the dialog did not appear")
        alert.buttons["OK"].tap()
        expectResult("[confirm@ok] OK")
    }
}
