import XCTest

/// The system screens the tests press (native-toolkit's CApiUITestApp.swift, the same element queries).
/// Labels are matched in English and Japanese.
enum SystemUI {
    /// Allows the notification permission alert (shown by springboard).
    static func allowNotifications(_ springboard: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        let allow = springboard.alerts.buttons.matching(NSPredicate(format: "label IN {'Allow', '許可'}")).firstMatch
        guard allow.waitForExistence(timeout: timeout) else { return false }
        allow.tap()
        return true
    }

    /// Waits for the share sheet in the app.
    static func shareSheet(_ app: XCUIApplication, timeout: TimeInterval = 15) -> Bool {
        app.descendants(matching: .any)
            .matching(NSPredicate(format: "identifier IN {'ActivityListView', 'UIActivityContentView'}"))
            .firstMatch.waitForExistence(timeout: timeout)
    }

    /// Closes the share sheet without choosing an activity. Only the Close button is pressed: a tap at
    /// a guessed point could land on a share target (a real app or contact) on a device.
    static func closeShareSheet(_ app: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        let close = app.buttons.matching(NSPredicate(format: "label IN {'Close', '閉じる'}")).firstMatch
        guard close.waitForExistence(timeout: timeout) else { return false }
        close.tap()
        return true
    }

    /// Taps the banner of the notification with this title, from springboard.
    static func tapBanner(_ springboard: XCUIApplication, title: String, timeout: TimeInterval = 25) -> Bool {
        let banner = springboard.descendants(matching: .any)
            .matching(NSPredicate(format: "identifier == 'NotificationShortLookView' AND label CONTAINS %@", title))
            .firstMatch
        guard banner.waitForExistence(timeout: timeout) else { return false }
        banner.tap()
        return true
    }
}
