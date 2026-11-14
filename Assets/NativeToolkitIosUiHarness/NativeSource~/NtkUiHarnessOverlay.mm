// A UIKit overlay over the Unity view that XCUITest can see (artifact/topics/ios-c-abi-2, UI automation design v4, 4.2).
// Rows are buttons row.<id>.run; results are labels result.line.<n>. Called from IosUiHarness.cs on the main thread.
#import <UIKit/UIKit.h>
#import "UnityAppController.h"
#include <dlfcn.h>
#include <stdatomic.h>

typedef void (*NtkhRowCallback)(const char* rowId);
static NtkhRowCallback gCallback;
static UIStackView* gStack;
static int gLine;

@interface NtkhTarget : NSObject
- (void)tap:(UIButton*)sender;
@end
@implementation NtkhTarget
- (void)tap:(UIButton*)sender
{
    NSString* rowId = [sender titleForState:UIControlStateNormal];
    if (gCallback != NULL && rowId != nil) gCallback(rowId.UTF8String);
}
@end
static NtkhTarget* gTarget;

extern "C" void ntkh_overlay_init(NtkhRowCallback callback)
{
    gCallback = callback;
    if (gStack != nil) return;
    UIView* root = UnityGetGLViewController().view;
    UIScrollView* scroll = [[UIScrollView alloc] init];
    scroll.translatesAutoresizingMaskIntoConstraints = NO;
    scroll.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.9];
    [root addSubview:scroll];
    [NSLayoutConstraint activateConstraints:@[
        [scroll.leadingAnchor constraintEqualToAnchor:root.leadingAnchor],
        [scroll.trailingAnchor constraintEqualToAnchor:root.trailingAnchor],
        [scroll.bottomAnchor constraintEqualToAnchor:root.bottomAnchor],
        [scroll.heightAnchor constraintEqualToAnchor:root.heightAnchor multiplier:0.5],
    ]];
    gStack = [[UIStackView alloc] init];
    gStack.axis = UILayoutConstraintAxisVertical;
    gStack.spacing = 6;
    gStack.translatesAutoresizingMaskIntoConstraints = NO;
    [scroll addSubview:gStack];
    [NSLayoutConstraint activateConstraints:@[
        [gStack.leadingAnchor constraintEqualToAnchor:scroll.contentLayoutGuide.leadingAnchor constant:12],
        [gStack.topAnchor constraintEqualToAnchor:scroll.contentLayoutGuide.topAnchor constant:12],
        [gStack.bottomAnchor constraintEqualToAnchor:scroll.contentLayoutGuide.bottomAnchor constant:-12],
        [gStack.widthAnchor constraintEqualToAnchor:scroll.frameLayoutGuide.widthAnchor constant:-24],
    ]];
    gTarget = [[NtkhTarget alloc] init];
}

extern "C" void ntkh_overlay_add_row(const char* rowId)
{
    NSString* name = [NSString stringWithUTF8String:rowId];
    UIButton* button = [UIButton buttonWithType:UIButtonTypeSystem];
    [button setTitle:name forState:UIControlStateNormal];
    button.accessibilityIdentifier = [NSString stringWithFormat:@"row.%@.run", name];
    [button addTarget:gTarget action:@selector(tap:) forControlEvents:UIControlEventTouchUpInside];
    [gStack addArrangedSubview:button];
}

extern "C" void ntkh_overlay_append(const char* rowId, const char* state, const char* value)
{
    UILabel* label = [[UILabel alloc] init];
    label.numberOfLines = 0;
    label.font = [UIFont monospacedSystemFontOfSize:12 weight:UIFontWeightRegular];
    label.text = [NSString stringWithFormat:@"[%@@%@] %@", [NSString stringWithUTF8String:rowId],
                  [NSString stringWithUTF8String:state], [NSString stringWithUTF8String:value]];
    label.accessibilityIdentifier = [NSString stringWithFormat:@"result.line.%d", gLine++];
    [gStack addArrangedSubview:label];
}

// Calls the row callback with rowId after the delay, from the main queue. The delay is native because
// Unity pauses its player loop while the app is not active (applicationWillResignActive), so a C#
// delay would not run while Control Center is open (Dialog UI test plan v3, ID-17).
extern "C" void ntkh_overlay_after(double seconds, const char* rowId)
{
    NSString* name = [NSString stringWithUTF8String:rowId];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(seconds * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (gCallback != NULL) gCallback(name.UTF8String);
    });
}

// The activation state of the scene that shows the Unity view, as a name.
extern "C" const char* ntkh_activation_state(void)
{
    UIWindowScene* scene = UnityGetGLViewController().view.window.windowScene;
    if (scene == nil) return "noScene";
    switch (scene.activationState)
    {
        case UISceneActivationStateForegroundActive: return "foregroundActive";
        case UISceneActivationStateForegroundInactive: return "foregroundInactive";
        case UISceneActivationStateBackground: return "background";
        default: return "unattached";
    }
}

// 1 when the Unity view controller is presenting another screen (an alert, a sheet), else 0.
extern "C" int ntkh_presenting(void)
{
    return UnityGetGLViewController().presentedViewController != nil ? 1 : 0;
}

// Calls the row callback with rowId once, when the app's scene next enters the background, inside a
// background task so it runs before iOS suspends the app (a dispatch_after delay would not fire in
// the background; Share UI test plan v3, SH-18).
extern "C" void ntkh_overlay_on_background(const char* rowId)
{
    NSString* name = [NSString stringWithUTF8String:rowId];
    __block id token = nil;
    token = [[NSNotificationCenter defaultCenter] addObserverForName:UISceneDidEnterBackgroundNotification
                                                              object:nil
                                                               queue:[NSOperationQueue mainQueue]
                                                          usingBlock:^(NSNotification* note) {
        [[NSNotificationCenter defaultCenter] removeObserver:token];
        // Ended once: by the expiration handler or 5 s later, whichever comes first (both on the main queue).
        __block UIBackgroundTaskIdentifier task = UIBackgroundTaskInvalid;
        void (^end)(void) = ^{
            if (task == UIBackgroundTaskInvalid) return;
            [[UIApplication sharedApplication] endBackgroundTask:task];
            task = UIBackgroundTaskInvalid;
        };
        task = [[UIApplication sharedApplication] beginBackgroundTaskWithExpirationHandler:end];
        if (gCallback != NULL) gCallback(name.UTF8String);
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(5 * NSEC_PER_SEC)), dispatch_get_main_queue(), end);
    }];
}

// --- Clipboard CB-5 (Clipboard test plan v4, 2.2): what happens to a named pasteboard after the manager is destroyed.

// Writes a string to an existing named pasteboard natively, outside the manager (destroyed by then).
// Never the general pasteboard.
extern "C" void ntkh_pasteboard_write(const char* name)
{
    UIPasteboard* pasteboard = [UIPasteboard pasteboardWithName:[NSString stringWithUTF8String:name] create:NO];
    if (pasteboard != nil) pasteboard.string = @"ntkh-after-destroy";
}

// 1 when the C ABI of native-toolkit 2.0.0 is in the process (ntk_version), else 0 (1.x).
extern "C" int ntkh_abi(void)
{
    return dlsym(RTLD_DEFAULT, "ntk_version") != NULL ? 1 : 0;
}

// A change listener of its own, through the C ABI when it is there. Looked up with dlsym and declared
// here, so this file builds and links against 1.x too (the 1.x frameworks have no ntk_clipboard symbol
// and NativeToolkitC/Clipboard.h is not there). Removing the manager's listener does not stop the
// observation in 2.0.0 (Clipboard.h, ntk_clipboard_listener_remove), so this listener tells whether the
// observation itself stopped.
typedef void (*NtkhChangeFn)(void* userData, void* change);
typedef void (*NtkhReleaseFn)(void* userData);
typedef int (*NtkhAddListenerFn)(NtkhChangeFn callback, void* userData, NtkhReleaseFn release, void** outListener);
typedef void (*NtkhChangeFreeFn)(void* change);
static atomic_int gNativeChanges;
static NtkhChangeFreeFn gChangeFree;

static void NtkhOnNativeChange(void* userData, void* change)
{
    atomic_fetch_add(&gNativeChanges, 1);
    if (gChangeFree != NULL && change != NULL) gChangeFree(change);
}

// Adds the listener: 0 when added, -1 when the C ABI is not there (1.x), -2 when adding failed.
extern "C" int ntkh_native_listener_add(void)
{
    NtkhAddListenerFn add = (NtkhAddListenerFn)dlsym(RTLD_DEFAULT, "ntk_clipboard_add_change_listener");
    if (add == NULL) return -1;
    gChangeFree = (NtkhChangeFreeFn)dlsym(RTLD_DEFAULT, "ntk_clipboard_change_free");
    void* listener = NULL;
    return add(NtkhOnNativeChange, NULL, NULL, &listener) == 0 && listener != NULL ? 0 : -2;
}

// Changes the listener has received since it was added.
extern "C" int ntkh_native_listener_count(void)
{
    return atomic_load(&gNativeChanges);
}

