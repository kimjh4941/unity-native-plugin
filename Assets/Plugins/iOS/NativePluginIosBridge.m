#import "NativePluginIosBridge.h"
#import "unity_native_plugin-Swift.h" // 自動生成されるSwiftヘッダー

void ShowNativeAlert(const char* title, const char* message) {
    NSString* nsTitle = [NSString stringWithUTF8String:title];
    NSString* nsMessage = [NSString stringWithUTF8String:message];
    [NativePluginIos showNativeAlertWithTitle:nsTitle message:nsMessage];
}