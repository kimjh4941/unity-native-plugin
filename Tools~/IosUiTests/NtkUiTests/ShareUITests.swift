import XCTest

final class ShareUITests: HarnessUITestCase {
    /// Dismissing the share sheet reports a result without an activity.
    func test_dismiss() {
        launch("share")
        run("text")
        XCTAssertTrue(SystemUI.shareSheet(app), "the share sheet did not appear")
        XCTAssertTrue(SystemUI.closeShareSheet(app), "the share sheet has no Close button")
        expectResult("[text@ok] completed=False activity=none", timeout: 15)
    }
}
