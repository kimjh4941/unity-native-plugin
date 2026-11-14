import XCTest

final class ClipboardUITests: HarnessUITestCase {
    /// A named pasteboard round trip (the general pasteboard is never touched).
    func test_namedRoundTrip() {
        launch("clipboard")
        run("named")
        expectResult("[named@ok] items=1 match=True", timeout: 15)
    }
}
