// Imported at the top of the copied NtkHandoff2Installer.mm (with #define NSLog NtkProbeNSLog), so that
// its [NTK-HANDOFF2] lines also reach the probe log file. A cold start from a banner tap has no console.
#pragma once
#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C"
#endif
void NtkProbeNSLog(NSString* format, ...) NS_FORMAT_FUNCTION(1, 2);
