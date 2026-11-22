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

    /// The frames of the share sheet's activity lists (`ActivityListView`), the number of its views
    /// (`ActivityListView`, `UIActivityContentView`), and the frames of the dismiss regions, read from one
    /// snapshot of the app. A snapshot records no test failure when the sheet is going away, unlike reading
    /// elements bound by index (implementation review I-3). Only the list's frame is used for the sheet's place:
    /// in a snapshot the header (`UIActivityContentView`) reported its top as 0 while the element read 480, the
    /// list's top (iOS 26.2, Unity player, 2026-11-21); the list read 480 both ways.
    private static func shareSheetFrames(_ app: XCUIApplication) -> (markers: [CGRect], views: Int, regions: [CGRect])? {
        guard let root = try? app.snapshot() else { return nil }
        var markers: [CGRect] = []
        var views = 0
        var regions: [CGRect] = []
        var stack: [XCUIElementSnapshot] = [root]
        while let node = stack.popLast() {
            if node.identifier == "ActivityListView" || node.identifier == "UIActivityContentView" {
                views += 1
                if node.identifier == "ActivityListView" { markers.append(node.frame) }
            } else if node.identifier == "PopoverDismissRegion" && node.elementType == .other {
                regions.append(node.frame)
            }
            stack.append(contentsOf: node.children)
        }
        return (markers, views, regions)
    }

    /// The counts of the share sheet's views and dismiss regions, for a failure message on the simulator (a
    /// device run answers with fixed words: implementation review I-7).
    static func shareSheetCounts(_ app: XCUIApplication) -> String {
        guard let frames = shareSheetFrames(app) else { return "no-snapshot" }
        return "views-\(frames.views)-lists-\(frames.markers.count)-regions-\(frames.regions.count)-press-\(lastPressNote)"
    }

    /// Closes the share sheet without choosing an activity. On iOS 18 only the Close button is pressed. iOS 26
    /// has none: the sheet's own dismiss region (`PopoverDismissRegion`) is pressed when there is exactly one
    /// (native-toolkit's ShareSheet.close, U-16), on the simulator only.
    ///
    /// The region spans the screen behind the sheet, so its center can be under the sheet: in the Unity
    /// player's sample screens on iOS 26.2 a tap at the center did not close it (2026-11-21, the sheet stayed
    /// and every later share was refused). The point pressed is in the region, halfway between its top and the
    /// sheet's top. It is pressed only when the sheet's top has held still for two reads 0.3 s apart, is at
    /// least 88 points below the region's top and above its bottom, and a last read just before the press finds
    /// the same top, one region, and the point outside every view of the sheet. The point comes from the
    /// region's own coordinates, so a press after the region has gone cannot reach the app's screen. On a device
    /// nothing is pressed on iOS 26 (the iPhone XS stays on iOS 18): a frame read too early could still put the
    /// point on a share target (implementation review I-1).
    static func closeShareSheet(_ app: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        if osMajorVersion >= 26 {
            #if targetEnvironment(simulator)
            return pressAboveTheSheet(app, timeout: timeout)
            #else
            return false
            #endif
        }
        let close = app.buttons.matching(NSPredicate(format: "label IN {'Close', '閉じる'}")).firstMatch
        guard close.waitForExistence(timeout: timeout) else { return false }
        close.tap()
        return true
    }

    /// Why the last pressAboveTheSheet stopped, as words and whole points (for the simulator's failure answer).
    private(set) static var lastPressNote = "none"

    private static func pressAboveTheSheet(_ app: XCUIApplication, timeout: TimeInterval) -> Bool {
        let end = Date().addingTimeInterval(timeout)
        var previous: CGFloat?
        lastPressNote = "no-read"
        repeat {
            if let frames = shareSheetFrames(app), frames.regions.count == 1,
               let top = sheetViews(frames.markers, in: frames.regions[0]).map(\.minY).min() {
                let area = frames.regions[0]
                func whole(_ value: CGFloat) -> Int { Int(max(0, value)) }
                lastPressNote = "top-\(whole(top))-area-\(whole(area.minY))-\(whole(area.maxY))-\(whole(area.width))"
                    + (previous == nil ? "-first" : "")
                if let previous, abs(top - previous) < 1, top - area.minY >= 88, top <= area.maxY - 88 {
                    let point = CGPoint(x: area.midX, y: (area.minY + top) / 2)
                    if let again = shareSheetFrames(app), again.regions.count == 1,
                       let topAgain = sheetViews(again.markers, in: again.regions[0]).map(\.minY).min(), abs(topAgain - top) < 1,
                       !sheetViews(again.markers, in: again.regions[0]).contains(where: { $0.contains(point) }) {
                        let region = app.otherElements.matching(identifier: "PopoverDismissRegion")
                        guard region.count == 1 else { return false }
                        region.element.coordinate(withNormalizedOffset: .zero)
                            .withOffset(CGVector(dx: point.x - area.minX, dy: point.y - area.minY)).tap()
                        lastPressNote = "pressed-\(Int(max(0, point.y)))"
                        return true
                    }
                    lastPressNote += "-again-differs"
                }
                previous = top
            } else {
                previous = nil
            }
            Thread.sleep(forTimeInterval: 0.3)
        } while Date() < end
        return false
    }

    /// The sheet's lists: a view about as tall as the region would be a container, not the sheet.
    private static func sheetViews(_ markers: [CGRect], in area: CGRect) -> [CGRect] {
        markers.filter { $0.height < area.height - 1 }
    }

    /// Closes the share sheet and waits until it has gone. On iOS 26 the first share of a run can miss its
    /// press (2026-11-21), so when the sheet is still there after 5 s it is pressed once more; closeShareSheet
    /// reads the sheet and its one region again before it presses, so nothing is pressed once they have gone.
    static func closeShareSheetAndWait(_ app: XCUIApplication, timeout: TimeInterval = 10) -> Bool {
        guard closeShareSheet(app, timeout: timeout) else { return false }
        if waitForNoShareSheet(app, 5) { return true }
        if osMajorVersion >= 26 { _ = closeShareSheet(app, timeout: 5) }
        return waitForNoShareSheet(app, 10)
    }

    /// Whether the share sheet's views have gone within `seconds`.
    static func waitForNoShareSheet(_ app: XCUIApplication, _ seconds: TimeInterval) -> Bool {
        let end = Date().addingTimeInterval(seconds)
        while shareSheet(app, timeout: 0.5) {
            if Date() > end { return false }
        }
        return true
    }

    /// Cancels the action sheet without choosing an option. iOS 18: its `cancel` button. iOS 26.2 shows the
    /// sheet as a popover with no usable cancel button; its `PopoverDismissRegion` is pressed when there is
    /// exactly one and no hittable `cancel` button is there, and UIKit runs the cancel action (native-toolkit's
    /// DialogUITests D7, stage 0a 3.1).
    static func cancelActionSheet(_ app: XCUIApplication, cancel: String, timeout: TimeInterval = 10) -> Bool {
        let sheet = app.sheets.firstMatch
        guard sheet.waitForExistence(timeout: timeout) else { return false }
        let button = sheet.buttons.matching(NSPredicate(format: "label == %@", cancel))
        if osMajorVersion >= 26 {
            // A hittable cancel button where none is expected is a different sheet: nothing is pressed (as
            // native-toolkit's tapOutsideTheSheet).
            if button.count > 0 && button.firstMatch.isHittable { return false }
            let region = app.otherElements.matching(identifier: "PopoverDismissRegion")
            guard region.firstMatch.waitForExistence(timeout: 5), region.count == 1 else { return false }
            region.element.tap()
            return true
        }
        guard button.count == 1 else { return false }
        button.firstMatch.tap()
        return true
    }

    /// Declines iOS 26's offer to save a password typed into the app's login alert (simulator only: the tests
    /// type into a secure field there alone). It comes seconds after the alert closed, as an alert or a sheet of
    /// the app; a tap while it is still appearing is lost, so it is pressed again, up to three times. iOS 18 does
    /// not offer it: true at once.
    static func declinePasswordSaving(_ app: XCUIApplication, timeout: TimeInterval = 15) -> Bool {
        guard osMajorVersion >= 26 else { return true }
        let notNow = app.buttons.matching(NSPredicate(format: "label IN {'Not Now', '今はしない'}")).firstMatch
        guard notNow.waitForExistence(timeout: timeout) else { return false }
        for _ in 0..<3 where notNow.exists {
            if notNow.isHittable { notNow.tap() }
            _ = notNow.waitForNonExistence(timeout: 3)
        }
        return !notNow.exists
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
