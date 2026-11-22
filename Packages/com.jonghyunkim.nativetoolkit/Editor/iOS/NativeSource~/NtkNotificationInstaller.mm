// Installs native-toolkit's notification delegate when the app finishes launching
// (artifact/topics/ios-c-abi-2, decision I-2; 3-S design v3, 1.2).
//
// Editor/iOS/IosXcodeProjectSetup.cs copies this file into the exported Xcode project and builds it into
// UnityFramework, unless NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL is defined (an app that installs
// the delegate itself, for example through another notification package, then calls Initialize() again).
//
// The listener registers itself in +load, before UIApplicationMain, and runs on Unity's
// didFinishLaunching notification - the form native-toolkit's stage 1a measured. It does not override
// UnityAppController (a category method would replace Unity's own implementation), and it does not use
// UserNotifications itself, so the app needs no extra framework (stage 1, S-7).
#import <Foundation/Foundation.h>
#import "AppDelegateListener.h"
#include <IosLibraryCApi/NativeToolkitC/Notification.h>

@interface NtkNotificationInstaller : NSObject <AppDelegateListener>
@end

@implementation NtkNotificationInstaller

+ (void)load
{
    static NtkNotificationInstaller* installer;
    installer = [NtkNotificationInstaller new];
    UnityRegisterAppDelegateListener(installer);
}

- (void)didFinishLaunching:(NSNotification*)notification
{
    ntk_notification_error error = ntk_notification_install();
    NSLog(@"[NativeToolkit][iOS] ntk_notification_install -> %d", (int)error);
}

@end
