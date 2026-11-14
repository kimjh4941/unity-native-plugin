import XCTest

/// Notification test plan v4 (artifact/ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v4.md), section 2.
///
/// The harness is installed again before every test (scripts/test_ios_ui.sh), so the permission starts
/// undecided. Banners and Notification Center cells are found by the harness's title and the test's own
/// body (`ntk NU-<n>`), exactly one, and the expanded notification is checked before an action is pressed
/// (native-toolkit's CApiUITestApp and Springboard). Failure messages name no springboard label; the
/// harness's own lines are printed for the record ([NTK-NOTIFICATION-UI]).
final class NotificationUITests: HarnessUITestCase {
    private static let title = "NTK UI Harness"
    private static let replyPlaceholder = "NTK UI Harness reply"
    private static let bodyTap = "com.apple.UNNotificationDefaultActionIdentifier"
    private static let dismiss = "com.apple.UNNotificationDismissActionIdentifier"
    private static let allow = ["Allow", "許可"]
    private static let dontAllow = ["Don’t Allow", "Don't Allow", "許可しない"]
    private let settings = XCUIApplication(bundleIdentifier: "com.apple.Preferences")

    override func tearDown() {
        if let app, app.state == .runningForeground, app.buttons["row.cleanup.run"].exists {
            run("cleanup")
        }
        super.tearDown()
    }

    // MARK: Helpers

    private func record(_ line: String) { print("[NTK-NOTIFICATION-UI] \(line)") }

    /// The finished lines of the id, filtered in the query (reading every line of the screen made a step of
    /// NU-11 take tens of seconds, 2026-11-15).
    private func query(_ id: String) -> XCUIElementQuery {
        app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.' AND label BEGINSWITH %@ AND NOT (label BEGINSWITH %@)",
                                             "[\(id)@", "[\(id)@pending]"))
    }

    private func lines(_ id: String) -> [String] { query(id).allElementsBoundByIndex.map(\.label) }

    /// Waits until the id has `count` finished lines and returns the last one.
    @discardableResult
    private func waitLines(_ id: String, count: Int, timeout: TimeInterval = HarnessUITestCase.timeout) -> String {
        let deadline = Date().addingTimeInterval(timeout)
        while query(id).count < count, Date() < deadline { Thread.sleep(forTimeInterval: 0.25) }
        let found = lines(id)
        XCTAssertGreaterThanOrEqual(found.count, count, "no \(id) line #\(count); lines: \(harnessLines())")
        let last = found.last ?? ""
        record(last)
        return last
    }

    /// The harness's own result lines (its text only), for failure messages.
    private func harnessLines() -> [String] {
        app.staticTexts.matching(NSPredicate(format: "identifier BEGINSWITH 'result.line.'")).allElementsBoundByIndex.map(\.label)
    }

    private func alertButton(_ labels: [String]) -> XCUIElementQuery {
        springboard.alerts.buttons.matching(NSPredicate(format: "label IN %@", labels))
    }

    /// Answers the permission prompt, which must name the harness.
    private func answerPermission(_ labels: [String]) {
        let alert = springboard.alerts.firstMatch
        XCTAssertTrue(alert.waitForExistence(timeout: Self.timeout), "the permission prompt did not appear")
        XCTAssertEqual(springboard.alerts.count, 1)
        XCTAssertTrue(alert.label.contains("“\(Self.title)”"), "the prompt does not name the harness")
        let button = alertButton(labels)
        XCTAssertEqual(button.count, 1)
        button.firstMatch.tap()
    }

    private func grant() {
        run("permission")
        answerPermission(Self.allow)
        XCTAssertEqual(waitLines("permission", count: 1), "[permission@ok] granted")
    }

    private func deny() -> String {
        run("permission")
        answerPermission(Self.dontAllow)
        let line = waitLines("permission", count: 1)
        XCTAssertTrue(line.hasPrefix("[permission@fail] error="), line)
        return line
    }

    private func banners(_ body: String) -> XCUIElementQuery {
        springboard.descendants(matching: .any).matching(NSPredicate(
            format: "identifier == 'NotificationShortLookView' AND label CONTAINS %@ AND label ENDSWITH %@",
            ", \(Self.title), ", ", \(body)"))
    }

    /// The one banner of the body, waited for.
    private func banner(_ body: String, timeout: TimeInterval = 20) -> XCUIElement? {
        let query = banners(body)
        guard query.firstMatch.waitForExistence(timeout: timeout) else {
            XCTFail("the banner \(body) did not appear")
            return nil
        }
        XCTAssertEqual(query.count, 1, "the banner \(body) is not exactly one")
        return query.count == 1 ? query.element(boundBy: 0) : nil
    }

    /// Long-presses the banner; the expansion is shown by the action buttons of its category.
    private func expand(_ body: String, marker: String) -> Bool {
        guard let banner = banner(body) else { return false }
        banner.press(forDuration: 1.5)
        return springboard.buttons.matching(identifier: marker).firstMatch.waitForExistence(timeout: Self.timeout)
    }

    private func tapAction(_ title: String) {
        let buttons = springboard.buttons.matching(identifier: title)
        XCTAssertTrue(buttons.firstMatch.waitForExistence(timeout: Self.timeout), "the action \(title) did not appear")
        XCTAssertEqual(buttons.count, 1, "the action \(title) is not exactly one")
        if buttons.count == 1 { buttons.element(boundBy: 0).tap() }
    }

    private func sendReply(_ text: String) {
        let textView = NSPredicate(format: "elementType == %lu AND placeholderValue == %@",
                                   XCUIElement.ElementType.textView.rawValue, Self.replyPlaceholder)
        let windows = springboard.windows.containing(textView)
        XCTAssertTrue(windows.firstMatch.waitForExistence(timeout: Self.timeout), "the reply window did not appear")
        guard windows.count == 1 else { return XCTFail("the reply window is not exactly one") }
        let views = windows.element(boundBy: 0).textViews.matching(textView)
        guard views.count == 1 else { return XCTFail("the reply text view is not exactly one") }
        views.element(boundBy: 0).typeText(text)
        let send = springboard.buttons.matching(identifier: "Send")
        XCTAssertTrue(send.firstMatch.waitForExistence(timeout: Self.timeout))
        guard send.count == 1 else { return XCTFail("Send is not exactly one") }
        send.element(boundBy: 0).tap()
    }

    private func pressHome() { XCUIDevice.shared.press(.home) }

    private func backToHarness() {
        app.activate()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15), "the harness did not come back")
    }

    /// The finished lines of the id after `seconds`, for "exactly n" checks.
    private func settle(_ id: String, _ seconds: TimeInterval = 3) -> [String] {
        Thread.sleep(forTimeInterval: seconds)
        return lines(id)
    }

    private func openControlCenterBriefly() {
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.005))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.6)))
        Thread.sleep(forTimeInterval: 5)
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.95))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.2)))
    }

    // MARK: NU-1 to NU-3b: the permission

    func test_NU1() throws {
        launch("notification")
        grant()
        run("status"); XCTAssertEqual(waitLines("status", count: 1), "[status@ok] Authorized")
        run("has"); XCTAssertEqual(waitLines("has", count: 1), "[has@ok] True")
    }

    func test_NU2() throws {
        try requireSimulator()
        launch("notification")
        _ = deny()
        run("status"); XCTAssertEqual(waitLines("status", count: 1), "[status@ok] Denied")
        run("has"); XCTAssertEqual(waitLines("has", count: 1), "[has@ok] False")
    }

    func test_NU2b() throws {
        try requireSimulator()
        launch("notification")
        _ = deny()
        run("denied-ops")
        for id in ["denied-show", "denied-schedule", "denied-update"] { waitLines(id, count: 1) }
        Thread.sleep(forTimeInterval: 1)
        run("pending"); waitLines("pending", count: 1)
        run("delivered"); waitLines("delivered", count: 1)
    }

    func test_NU3() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("permission")
        XCTAssertFalse(springboard.alerts.firstMatch.waitForExistence(timeout: 3), "the prompt appeared again")
        XCTAssertEqual(waitLines("permission", count: 2), "[permission@ok] granted")
        XCTAssertEqual(settle("permission").count, 2)
    }

    func test_NU3b() throws {
        try requireSimulator()
        launch("notification")
        let first = deny()
        run("permission")
        XCTAssertFalse(springboard.alerts.firstMatch.waitForExistence(timeout: 3), "the prompt appeared again")
        XCTAssertEqual(waitLines("permission", count: 2), first)
        XCTAssertEqual(settle("permission").count, 2)
    }

    // MARK: NU-4 to NU-8: banners and events

    func test_NU4() {
        launch("notification")
        grant()
        run("schedule-4"); waitLines("schedule", count: 1)
        pressHome()
        banner("ntk NU-4")?.tap()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15))
        let tap = waitLines("tap", count: 1, timeout: 15)
        XCTAssertTrue(tap.hasPrefix("[tap@event] id=ntkh-4 action=\(Self.bodyTap) userInfo="), tap)
        run("schedule-4n"); waitLines("schedule", count: 2)
        pressHome()
        banner("ntk NU-4n")?.tap()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15))
        XCTAssertEqual(waitLines("tap", count: 2, timeout: 15), "[tap@event] id=ntkh-4n action=\(Self.bodyTap) userInfo=<null>")
        XCTAssertEqual(settle("tap").count, 2)
    }

    func test_NU5() {
        launch("notification")
        grant()
        run("category"); waitLines("category", count: 1)
        Thread.sleep(forTimeInterval: 1)
        run("schedule-5"); waitLines("schedule", count: 1)
        pressHome()
        XCTAssertTrue(expand("ntk NU-5", marker: "Open"), "the notification did not expand")
        tapAction("Open")
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 15), "Open did not bring the harness to the front")
        let tap = waitLines("tap", count: 1, timeout: 15)
        XCTAssertTrue(tap.hasPrefix("[tap@event] id=ntkh-5 action=open userInfo="), tap)
        XCTAssertEqual(settle("tap").count, 1)
        XCTAssertEqual(lines("reply").count, 0)
    }

    func test_NU6() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("category"); waitLines("category", count: 1)
        Thread.sleep(forTimeInterval: 1)
        run("schedule-6"); waitLines("schedule", count: 1)
        pressHome()
        XCTAssertTrue(expand("ntk NU-6", marker: "Reply"), "the notification did not expand")
        tapAction("Reply")
        sendReply("ntk reply")
        Thread.sleep(forTimeInterval: 3)
        backToHarness()
        XCTAssertEqual(waitLines("reply", count: 1, timeout: 15), "[reply@event] id=ntkh-6 action=reply text=ntk reply")
        XCTAssertEqual(settle("reply").count, 1)
        XCTAssertEqual(lines("tap").count, 0)
    }

    func test_NU7() {
        launch("notification")
        grant()
        run("show-7"); waitLines("show", count: 1)
        Thread.sleep(forTimeInterval: 3)
        run("delivered")
        let delivered = waitLines("delivered", count: 1)
        XCTAssertTrue(delivered.contains("ntkh-7(title=True,category=\"\",subtitle=False,body=True)"), delivered)
    }

    func test_NU8() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("schedule-8"); waitLines("schedule", count: 1)
        app.terminate()
        banner("ntk NU-8")?.tap()
        XCTAssertTrue(app.wait(for: .runningForeground, timeout: 20), "the banner did not start the harness")
        XCTAssertTrue(result("[launch@ok] scenario=notification source=file").waitForExistence(timeout: 20), "the harness did not start from the file")
        let taps = settle("tap", 5)
        record("NU-8 taps=\(taps)")
        XCTAssertEqual(taps, [], "1.x loses the tap that starts the app (expected)")
    }

    // MARK: NU-9, NU-10, NU-14 to NU-15: Settings, inactive and background

    func test_NU9() throws {
        try requireSimulator()
        launch("notification")
        run("open-settings")
        let opened = settings.wait(for: .runningForeground, timeout: 10)
        record("NU-9 settings-foreground=\(opened)")
        XCTAssertTrue(opened, "the Settings app did not come to the front")
        backToHarness()
    }

    func test_NU10() throws {
        try requireSimulator()
        launch("notification")
        run("late-permission")
        openControlCenterBriefly()
        XCTAssertEqual(waitLines("late", count: 1), "[late@event] state=foregroundInactive")
        // 1.x, measured on the simulator twice (2026-11-15): the prompt appears, and after it is answered
        // no result comes at all.
        let prompted = springboard.alerts.firstMatch.waitForExistence(timeout: 5)
        record("NU-10 prompt=\(prompted)")
        XCTAssertTrue(prompted, "1.x shows the prompt for a call made while inactive")
        if prompted { answerPermission(Self.dontAllow) }
        XCTAssertEqual(settle("permission", 15), [], "1.x gives no result after the prompt is answered")
    }

    func test_NU14() throws {
        try requireSimulator()
        launch("notification")
        run("bg-permission")
        pressHome()
        Thread.sleep(forTimeInterval: 5)
        backToHarness()
        XCTAssertEqual(waitLines("late", count: 1), "[late@event] state=background")
        let prompted = springboard.alerts.firstMatch.waitForExistence(timeout: 5)
        record("NU-14 prompt=\(prompted)")
        if prompted { answerPermission(Self.dontAllow) }
        waitLines("permission", count: 1, timeout: 15)
        XCTAssertEqual(settle("permission").count, 1)
    }

    func test_NU14b_granted() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("bg-permission")
        pressHome()
        Thread.sleep(forTimeInterval: 5)
        backToHarness()
        XCTAssertEqual(waitLines("late", count: 1), "[late@event] state=background")
        XCTAssertFalse(springboard.alerts.firstMatch.waitForExistence(timeout: 3))
        XCTAssertEqual(waitLines("permission", count: 2, timeout: 15), "[permission@ok] granted")
        XCTAssertEqual(settle("permission").count, 2)
    }

    func test_NU14b_denied() throws {
        try requireSimulator()
        launch("notification")
        let first = deny()
        run("bg-permission")
        pressHome()
        Thread.sleep(forTimeInterval: 5)
        backToHarness()
        XCTAssertEqual(waitLines("late", count: 1), "[late@event] state=background")
        XCTAssertFalse(springboard.alerts.firstMatch.waitForExistence(timeout: 3))
        XCTAssertEqual(waitLines("permission", count: 2, timeout: 15), first)
        XCTAssertEqual(settle("permission").count, 2)
    }

    func test_NU15() throws {
        try requireSimulator()
        launch("notification")
        run("bg-settings")
        pressHome()
        Thread.sleep(forTimeInterval: 3)
        let opened = settings.state == .runningForeground
        record("NU-15 settings-foreground=\(opened)")
        XCTAssertFalse(opened, "the Settings app came to the front from the background")
        backToHarness()
        XCTAssertEqual(waitLines("late", count: 1), "[late@event] state=background")
    }

    // MARK: NU-11: the request lifecycle (plan 2.3)

    private func step(_ row: String, then expected: [String]? = nil) -> String {
        let before = lines("lists").count
        run(row)
        Thread.sleep(forTimeInterval: 1)
        run("lists")
        let last = waitLines("lists", count: before + 1)
        return last
    }

    func test_NU11() {
        launch("notification")
        grant()
        let a = "ntkh-a(title=True,category=ntkh.events,subtitle=True,body=True)"
        let a2 = "ntkh-a(title=False,category=\"\",subtitle=False,body=True)"
        let b = "ntkh-b(title=True,category=\"\",subtitle=False,body=True)"
        XCTAssertEqual(step("l1"), "[lists@ok] step=now pending-ids=\(a);\(b) delivered-ids=none")
        XCTAssertEqual(step("l2"), "[lists@ok] step=now pending-ids=\(a2);\(b) delivered-ids=none")
        let afterNullTrigger = step("l2b")
        record("NU-11 step 2b: \(afterNullTrigger)")
        XCTAssertEqual(step("l2c"), "[lists@ok] step=now pending-ids=\(a2);\(b) delivered-ids=none")
        XCTAssertEqual(step("l3"), "[lists@ok] step=now pending-ids=\(b) delivered-ids=none")
        // Step 4: CancelAll and a new request in the same frame, 5 times (10 did not fit in the 600 s limit).
        var kept = 0, lost = 0
        for _ in 0..<5 {
            let c0 = step("l4")
            XCTAssertTrue(c0.contains("ntkh-c0("), "c0 was not pending before the race: \(c0)")
            let raced = step("l4-race")
            if raced.contains("pending-ids=ntkh-c(") { kept += 1 } else if raced.contains("pending-ids=none") { lost += 1 }
        }
        record("NU-11 step 4: kept=\(kept) lost=\(lost) of 5")
        XCTAssertEqual(kept + lost, 5)
        _ = step("l4-clear")
        let c = "ntkh-c(title=True,category=\"\",subtitle=False,body=True)"
        XCTAssertEqual(step("l4-c"), "[lists@ok] step=now pending-ids=\(c) delivered-ids=none")
        XCTAssertEqual(step("l5"), "[lists@ok] step=now pending-ids=none delivered-ids=none")
        XCTAssertTrue(step("l5-de").contains("pending-ids=ntkh-d("))
        XCTAssertEqual(step("l5-all"), "[lists@ok] step=now pending-ids=none delivered-ids=none")
        let f = "ntkh-f(title=True,category=\"\",subtitle=False,body=True)"
        let g = "ntkh-g(title=True,category=\"\",subtitle=False,body=True)"
        XCTAssertEqual(step("l6-fg"), "[lists@ok] step=now pending-ids=none delivered-ids=\(f);\(g)")
        XCTAssertEqual(step("l6-f"), "[lists@ok] step=now pending-ids=none delivered-ids=\(g)")
        XCTAssertEqual(step("l6-all"), "[lists@ok] step=now pending-ids=none delivered-ids=none")
    }

    // MARK: NU-12, NU-13: categories

    private func actionsShown(_ body: String, candidates: [String]) -> [String] {
        guard let banner = banner(body) else { return [] }
        banner.press(forDuration: 1.5)
        Thread.sleep(forTimeInterval: 2)
        let shown = candidates.filter { springboard.buttons.matching(identifier: $0).count > 0 }
        pressHome()
        return shown
    }

    func test_NU12() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("cat-x-a"); waitLines("cat", count: 1); Thread.sleep(forTimeInterval: 1)
        run("cat-x-b"); waitLines("cat", count: 2); Thread.sleep(forTimeInterval: 1)
        run("schedule-12x"); waitLines("schedule", count: 1)
        pressHome()
        let x1 = actionsShown("ntk NU-12 x", candidates: ["A", "B", "C"])
        record("NU-12 x after A then B: \(x1)")
        backToHarness()
        run("cat-y"); waitLines("cat", count: 3); Thread.sleep(forTimeInterval: 1)
        run("schedule-12x2"); waitLines("schedule", count: 2)
        pressHome()
        let x2 = actionsShown("ntk NU-12 x2", candidates: ["A", "B", "C"])
        record("NU-12 x after Y: \(x2)")
        XCTAssertFalse(x2.isEmpty, "x has no action before it is removed")
        backToHarness()
        run("cat-remove-x"); waitLines("cat", count: 4); Thread.sleep(forTimeInterval: 1)
        // One notification at a time: two at once hid the first banner (2026-11-15).
        run("schedule-12x3"); waitLines("schedule", count: 3)
        pressHome()
        let x3 = actionsShown("ntk NU-12 x3", candidates: ["A", "B", "C"])
        backToHarness()
        run("schedule-12y"); waitLines("schedule", count: 4)
        pressHome()
        let y = actionsShown("ntk NU-12 y", candidates: ["A", "B", "C"])
        record("NU-12 after removing x: x=\(x3) y=\(y)")
        XCTAssertEqual(x3, [], "the removed category still has actions")
        XCTAssertEqual(y, ["C"], "the remaining category lost its action")
        backToHarness()
    }

    func test_NU13() throws {
        try requireSimulator()
        launch("notification")
        grant()
        run("category"); waitLines("category", count: 1)
        Thread.sleep(forTimeInterval: 1)
        run("schedule-13"); waitLines("schedule", count: 1)
        pressHome()
        guard banner("ntk NU-13") != nil else { return }
        XCTAssertTrue(banners("ntk NU-13").firstMatch.waitForNonExistence(timeout: 15), "the banner did not go")
        springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.0))
            .press(forDuration: 0.1, thenDragTo: springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.7)))
        let cells = springboard.buttons.matching(NSPredicate(format: "identifier == 'ListCell' AND label ENDSWITH %@", ", \(Self.title), ntk NU-13"))
        XCTAssertTrue(cells.firstMatch.waitForExistence(timeout: Self.timeout), "the notification is not in Notification Center")
        guard cells.count == 1 else { return XCTFail("the notification is not exactly one cell") }
        cells.element(boundBy: 0).swipeLeft()
        let clear = springboard.buttons.matching(NSPredicate(format: "label IN {'Clear', '消去'}"))
        XCTAssertTrue(clear.firstMatch.waitForExistence(timeout: Self.timeout), "Clear did not appear")
        guard clear.count == 1 else { return XCTFail("Clear is not exactly one") }
        clear.element(boundBy: 0).tap()
        XCTAssertTrue(cells.firstMatch.waitForNonExistence(timeout: Self.timeout))
        pressHome()
        backToHarness()
        let tap = waitLines("tap", count: 1, timeout: 15)
        XCTAssertTrue(tap.hasPrefix("[tap@event] id=ntkh-13 action=\(Self.dismiss) userInfo="), tap)
        XCTAssertEqual(settle("tap").count, 1)
        XCTAssertEqual(lines("reply").count, 0)
    }

    // MARK: NU-16: values 1.x was never measured with (one launch each, undecided permission)

    private func unmeasured(_ row: String) {
        launch("notification")
        run(row)
        Thread.sleep(forTimeInterval: 3)
        let alive = app.state == .runningForeground
        record("NU-16 \(row) alive=\(alive) lines=\(alive ? lines("unmeasured") : [])")
        XCTAssertTrue(alive, "the harness crashed")
        if alive { waitLines("unmeasured", count: 1) }
    }

    func test_NU16_lat91() throws { try requireSimulator(); unmeasured("u-lat91") }
    func test_NU16_noEntryExit() throws { try requireSimulator(); unmeasured("u-no-entry-exit") }
    func test_NU16_nestedNull() throws { try requireSimulator(); unmeasured("u-nested-null") }
}
