import XCTest
import UIKit

/// Clipboard test plan v4 (artifact/ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md), section 2.
///
/// CB-1 to CB-4 write the general pasteboard from this test runner (another app for iOS, so the harness
/// gets the paste prompt) and run on the simulator only: on a device they would overwrite the user's
/// clipboard. The prompt is pressed only when it names the harness pasting from this runner. CB-5 uses a
/// named pasteboard only.
final class ClipboardUITests: HarnessUITestCase {
    private static let allowPaste = ["Allow Paste", "ペーストを許可"]
    private static let dontAllowPaste = ["Don’t Allow Paste", "Don't Allow Paste", "ペーストを許可しない"]
    private static let harnessName = "NTK UI Harness"
    private static let runnerName = "NtkUiTests-Runner"

    /// The names quoted with “ and ” in the label, or nil when the quotes do not pair up (native-toolkit's
    /// Springboard.quotedNames).
    private static func quotedNames(_ label: String) -> [String]? {
        var names: [String] = []
        var current: String?
        for character in label {
            switch character {
            case "“":
                guard current == nil else { return nil }
                current = ""
            case "”":
                guard let name = current else { return nil }
                names.append(name)
                current = nil
            default:
                current?.append(character)
            }
        }
        return current == nil ? names : nil
    }

    /// Presses one of the labels on the paste prompt, only when the prompt is the only alert and names
    /// the harness pasting from this runner (native-toolkit's pasteAlertNamesKnownSource).
    private func pressPastePrompt(_ labels: [String], within seconds: TimeInterval = 5) {
        let alert = springboard.alerts.firstMatch
        XCTAssertTrue(alert.waitForExistence(timeout: seconds), "the paste prompt did not appear")
        XCTAssertEqual(springboard.alerts.count, 1, "more than one alert is shown")
        let names = Self.quotedNames(alert.label)
        XCTAssertEqual(names, [Self.harnessName, Self.runnerName], "the prompt is not the harness pasting from the runner")
        guard names == [Self.harnessName, Self.runnerName] else { return }
        let button = alert.buttons.matching(NSPredicate(format: "label IN %@", labels))
        XCTAssertEqual(button.count, 1, "the prompt has no single \(labels.first ?? "") button")
        button.firstMatch.tap()
    }

    /// The first finished line of the id, waited for; the harness's own lines go into the failure message.
    private func line(_ id: String, timeout: TimeInterval = HarnessUITestCase.timeout) -> String {
        let element = app.staticTexts.matching(NSPredicate(
            format: "identifier BEGINSWITH 'result.line.' AND (label BEGINSWITH %@ OR label BEGINSWITH %@)",
            "[\(id)@ok]", "[\(id)@fail]")).firstMatch
        XCTAssertTrue(element.waitForExistence(timeout: timeout), "no \(id) line; lines: \(finishedLines(ofIds: [id]))")
        let text = element.exists ? element.label : ""
        print("[NTK-CLIPBOARD-UI] \(text)") // for the record: the harness's own line (lengths and codes only)
        return text
    }

    private func writeGeneral(_ text: String) {
        UIPasteboard.general.string = text
    }

    func test_CB1() throws {
        try requireSimulator()
        writeGeneral("ntk ui harness paste")
        launch("clipboard"); run("read-general")
        pressPastePrompt(Self.allowPaste)
        let read = line("read")
        // 1.x, measured on the simulator (2026-11-14): one type.
        expectLines("read", ["[read@ok] items=1 types=1 text-length=20"])
        XCTAssertEqual(read, "[read@ok] items=1 types=1 text-length=20")
    }

    func test_CB2() throws {
        try requireSimulator()
        writeGeneral("ntk ui harness paste")
        launch("clipboard"); run("read-general")
        pressPastePrompt(Self.dontAllowPaste)
        expectLines("read", ["[read@ok] items=1 types=0 text-length=<null>"])
    }

    func test_CB2b() throws {
        try requireSimulator()
        writeGeneral("ntk ui harness paste")
        launch("clipboard"); run("load-general")
        pressPastePrompt(Self.dontAllowPaste)
        let load = line("load")
        XCTAssertTrue(load.hasPrefix("[load@fail] code=CLIPBOARD_LOAD_FAILED "), load)
        expectLines("load", [load])
    }

    /// The detection waits 5 s in 1.x, the prompt included: the prompt is pressed within that, and a
    /// timeout fails the test instead of being recorded as the refusal.
    func test_CB3() throws {
        try requireSimulator()
        writeGeneral("https://example.com/ntk")
        launch("clipboard"); run("detect-values-general")
        pressPastePrompt(Self.dontAllowPaste, within: 3)
        let detect = line("detect")
        XCTAssertFalse(detect.contains("CLIPBOARD_TIMED_OUT"), "the detection timed out: \(detect)")
        XCTAssertTrue(detect.hasPrefix("[detect@fail] code=CLIPBOARD_DETECTION_FAILED "), detect)
        expectLines("detect", [detect])
    }

    func test_CB4() throws {
        try requireSimulator()
        writeGeneral("https://example.com/ntk")
        launch("clipboard"); run("detect-patterns-general")
        expectLines("patterns", ["[patterns@ok] patterns=ProbableWebUrl"])
        XCTAssertEqual(springboard.alerts.count, 0, "a prompt appeared for detecting patterns")
    }

    /// After the manager is destroyed, a native write to the observed named pasteboard reaches nobody:
    /// no late change in C# and, with the C ABI, none on a listener of the harness's own.
    func test_CB5() {
        launch("clipboard"); run("after-destroy")
        let element = app.staticTexts.matching(NSPredicate(
            format: "identifier BEGINSWITH 'result.line.' AND label BEGINSWITH '[after-destroy@event]'")).firstMatch
        XCTAssertTrue(element.waitForExistence(timeout: 15), "no after-destroy line")
        let lineText = element.label
        print("[NTK-CLIPBOARD-UI] \(lineText)")
        if lineText.contains("abi=present") {
            XCTAssertEqual(lineText, "[after-destroy@event] terminated=True abi=present late-change=0 native-listener=0")
        } else {
            XCTAssertEqual(lineText, "[after-destroy@event] terminated=True abi=none late-change=0 native-listener=none")
        }
    }
}
