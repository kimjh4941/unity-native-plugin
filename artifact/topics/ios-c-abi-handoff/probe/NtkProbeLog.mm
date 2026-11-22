#import "NtkProbeLog.h"
#import <UserNotifications/UserNotifications.h>

// Logs with NSLog and appends the line to Documents/probe-log.txt, the file Probe.cs writes
// (Application.persistentDataPath is the Documents folder on iOS).
extern "C" void NtkProbeNSLog(NSString* format, ...)
{
    va_list args;
    va_start(args, format);
    NSString* message = [[NSString alloc] initWithFormat:format arguments:args];
    va_end(args);
    NSLog(@"%@", message);

    static NSDateFormatter* formatter;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        formatter = [NSDateFormatter new];
        formatter.dateFormat = @"HH:mm:ss.SSS";
    });
    NSString* line = [NSString stringWithFormat:@"%@ native %@\n", [formatter stringFromDate:[NSDate date]], message];
    NSString* documents = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES).firstObject;
    NSString* path = [documents stringByAppendingPathComponent:@"probe-log.txt"];
    @synchronized ([NSFileManager class])
    {
        NSFileHandle* handle = [NSFileHandle fileHandleForWritingAtPath:path];
        if (handle == nil)
        {
            [line writeToFile:path atomically:NO encoding:NSUTF8StringEncoding error:nil];
            return;
        }
        [handle seekToEndOfFile];
        [handle writeData:[line dataUsingEncoding:NSUTF8StringEncoding]];
        [handle closeFile];
    }
}

// Called from C# (U-4) to log which delegate is in front at a given point.
extern "C" void NtkProbeLogDelegate(const char* point)
{
    id delegate = UNUserNotificationCenter.currentNotificationCenter.delegate;
    NtkProbeNSLog(@"[NTK-HANDOFF2] %s delegate=%@", point, delegate ? NSStringFromClass([delegate class]) : @"nil");
}
