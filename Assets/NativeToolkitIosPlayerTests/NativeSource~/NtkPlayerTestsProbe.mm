// The native side of the iOS player tests' checks (artifact/topics/ios-c-abi-2, 3-S design v3, 1.7).
// IosPlayerProbeInstaller adds this file to the exported project of a player test run
// (-ntkIosPlayerSdk) only; it is never part of the package.

#import <Foundation/Foundation.h>
#import <objc/message.h>
#include <string.h>

// The class name of UNUserNotificationCenter.currentNotificationCenter.delegate, or NULL when it has no
// delegate. The center is looked up by name, so this file does not link UserNotifications itself. The
// caller (IL2CPP's string marshalling) frees the copy.
extern "C" char* ntkpt_notification_delegate_class(void)
{
    Class centerClass = NSClassFromString(@"UNUserNotificationCenter");
    if (centerClass == Nil) return NULL;
    id center = ((id (*)(id, SEL))objc_msgSend)(centerClass, NSSelectorFromString(@"currentNotificationCenter"));
    if (center == nil) return NULL;
    id delegate = ((id (*)(id, SEL))objc_msgSend)(center, NSSelectorFromString(@"delegate"));
    if (delegate == nil) return NULL;
    return strdup(NSStringFromClass([delegate class]).UTF8String);
}
