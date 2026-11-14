import XCTest

final class NotificationUITests: HarnessUITestCase {
    /// Needs a fresh install (the permission not yet asked): scripts/test_ios_ui.sh reinstalls the harness
    /// before every test.
    func test_permissionAndBannerTap() {
        launch("notification")
        run("permission")
        XCTAssertTrue(SystemUI.allowNotifications(springboard), "the permission alert did not appear")
        expectResult("[permission@ok] granted")
        run("schedule")
        expectResult("[schedule@ok] scheduled")
        // The notification fires 10 seconds after it was scheduled; the harness must be in the
        // background by then for the banner to show on the springboard.
        XCUIDevice.shared.press(.home)
        XCTAssertTrue(SystemUI.tapBanner(springboard, title: "NTK UI Harness", timeout: 30), "the banner did not appear")
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15), "the harness did not come back")
        expectResult("[tap@event] id=ntkh-1 action=com.apple.UNNotificationDefaultActionIdentifier", timeout: 15)
    }
}
