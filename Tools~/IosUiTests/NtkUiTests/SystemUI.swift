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

    /// The share sheet's Copy cells. Only cells are matched: each cell also holds a title text with the
    /// same label (native-toolkit's ShareSheet.swift).
    static func copyCells(_ app: XCUIApplication) -> XCUIElementQuery {
        app.cells.matching(NSPredicate(format: "label IN {'Copy', 'コピー'}"))
    }

    /// Taps Copy when exactly one Copy cell is shown (native-toolkit's U-13: with two or more, nothing is
    /// pressed). Simulator only: Copy writes the general pasteboard.
    static func pressCopy(_ app: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        let copy = copyCells(app)
        guard copy.firstMatch.waitForExistence(timeout: timeout), copy.count == 1 else { return false }
        copy.element.tap()
        return true
    }

    /// The share sheet header's title on iOS 18, read every 0.1 s for `duration` seconds. The header is
    /// the one navigation bar holding Close: with two `Other` children (title, subtitle) the first one's
    /// label, with one `Other` child the bar's own identifier unless it is `UIActivityContentView`; any
    /// other shape reads "" (native-toolkit's ShareSheet.observeHeaderTitle; iOS 26 is not read here,
    /// the simulator is iOS 18.3 until stage 4).
    static func headerTitles(_ app: XCUIApplication, for duration: TimeInterval) -> [String] {
        let header = app.navigationBars.containing(NSPredicate(
            format: "elementType == %lu AND label IN {'Close', '閉じる'}", XCUIElement.ElementType.button.rawValue))
        var seen: [String] = []
        let deadline = Date().addingTimeInterval(duration)
        repeat {
            var label = ""
            if header.count == 1 {
                let bar = header.element
                let parts = bar.children(matching: .other)
                if parts.count == 2 {
                    label = parts.element(boundBy: 0).label
                } else if parts.count == 1, bar.identifier != "UIActivityContentView" {
                    // With a preview title, iOS 18.3 names the header bar after the title and gives it one
                    // `Other` child (simulator, 2026-11-14, Share SH-8; native-toolkit saw the same on 26.2).
                    label = bar.identifier
                }
            }
            if seen.last != label { seen.append(label) }
            Thread.sleep(forTimeInterval: 0.1)
        } while Date() < deadline
        return seen
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
