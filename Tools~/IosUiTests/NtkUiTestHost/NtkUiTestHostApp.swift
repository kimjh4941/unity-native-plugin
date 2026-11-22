import SwiftUI

// An empty host: a UI test target needs one. The tests drive the Unity UI harness
// (com.jonghyunkim.nativetoolkit.uiharness), which scripts/test_ios_ui.sh installs beforehand.
@main
struct NtkUiTestHostApp: App {
    var body: some Scene { WindowGroup { Text("NTK UI test host") } }
}
