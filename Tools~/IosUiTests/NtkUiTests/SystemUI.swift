import XCTest

/// The system screens the tests press (native-toolkit's CApiUITestApp.swift, the same element queries).
/// Labels are matched in English and Japanese.
enum SystemUI {
    /// The major iOS version the runner runs on: iOS 26 changed the share sheet (native-toolkit's U-16).
    static var osMajorVersion: Int { ProcessInfo.processInfo.operatingSystemVersion.majorVersion }

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

    /// Closes the share sheet without choosing an activity. On iOS 18 only the Close button is pressed;
    /// iOS 26 has none, and the sheet's own dismiss region (`PopoverDismissRegion`) is pressed when there
    /// is exactly one (native-toolkit's ShareSheet.close, U-16). Never a guessed point: it could land on a
    /// share target (a real app or contact) on a device.
    static func closeShareSheet(_ app: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        if osMajorVersion >= 26 {
            let region = app.otherElements.matching(identifier: "PopoverDismissRegion")
            guard region.firstMatch.waitForExistence(timeout: timeout), region.count == 1 else { return false }
            region.element.tap()
            return true
        }
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

    /// The share sheet header's title, read every 0.1 s for `duration` seconds. On iOS 26 the element
    /// `LP.CaptionBar.TopCaption` ("" unless exactly one). On iOS 18 the header is the one navigation bar
    /// holding Close: with two `Other` children (title, subtitle) the first one's label, with one `Other`
    /// child the bar's own identifier unless it is `UIActivityContentView`; any other shape reads ""
    /// (native-toolkit's ShareSheet.observeHeaderTitle).
    static func headerTitles(_ app: XCUIApplication, for duration: TimeInterval) -> [String] {
        let caption = app.descendants(matching: .any).matching(identifier: "LP.CaptionBar.TopCaption")
        let header = app.navigationBars.containing(NSPredicate(
            format: "elementType == %lu AND label IN {'Close', '閉じる'}", XCUIElement.ElementType.button.rawValue))
        let isIOS26 = osMajorVersion >= 26
        var seen: [String] = []
        let deadline = Date().addingTimeInterval(duration)
        repeat {
            var label = ""
            if isIOS26 {
                label = caption.count == 1 ? caption.element.label : ""
            } else if header.count == 1 {
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
