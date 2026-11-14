// A UIKit overlay over the Unity view that XCUITest can see (artifact/topics/ios-c-abi-2, UI automation design v4, 4.2).
// Rows are buttons row.<id>.run; results are labels result.line.<n>. Called from IosUiHarness.cs on the main thread.
#import <UIKit/UIKit.h>
#import "UnityAppController.h"

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
