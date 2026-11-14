/**
 * @file Share.h
 * @brief The share sheet.
 *
 * Rules for the whole C ABI (C ABI design part 1, chapter 1): every function may be called from any
 * thread, except ntk_notification_install (main thread only), and none waits for the main thread;
 * completions, events and accepted releases arrive on the main thread; strings are NUL-terminated
 * UTF-8; this header is C99, includes only <stddef.h> and <stdint.h> (through Common.h), and is ASCII only. The
 * declarations are those of Appendix A of the C ABI design part 2, which
 * scripts/check_c_abi_contract_ios.py compares with this file.
 */
#ifndef NATIVETOOLKITC_SHARE_H
#define NATIVETOOLKITC_SHARE_H

#if defined(__has_include) && __has_include(<IosLibraryCApi/NativeToolkitC/Common.h>)
#include <IosLibraryCApi/NativeToolkitC/Common.h>
#else
#include "Common.h"
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* The rules of this header (design part 1, chapter 1):
   - Every function can be called from any thread and never waits for the main thread. Completions,
     events and an accepted call's release run on the main thread, and never inside the function when
     you call it on the main thread; called elsewhere, a completion can come before the function
     returns, before an output parameter is visible. Match results through user_data.
   - NONE from a function with a completion callback means accepted, and then the callback comes exactly once
     while the process lives (dialogs and shares: see their callback types). A listener gets zero or more
     events until it is removed. Any other value means rejected: no callback, and release (if not NULL) has
     already been called on your thread. Do not free user_data yourself then. A NULL callback is rejected.
   - After removing a listener off the main thread, events can still come until release. Free user_data
     in release, not when the function returns.
   - Callbacks must not let exceptions escape, and must not block the main thread for long.
   - Text comes back as UTF-8; invalid surrogates and U+0000 become U+FFFD.
   - Zero-filled means every field except struct_size.
   - Enumerations can grow: write a default branch. */
typedef int32_t ntk_share_error;
enum {
    NTK_SHARE_ERROR_NONE = 0,
    NTK_SHARE_ERROR_INVALID_PARAMETER = 1,
    NTK_SHARE_ERROR_NOT_INITIALIZED = 2,
    NTK_SHARE_ERROR_NOT_SUPPORTED = 3,
    NTK_SHARE_ERROR_UNKNOWN = 4,
    NTK_SHARE_ERROR_OUT_OF_MEMORY = 5,
    NTK_SHARE_ERROR_NOT_FOREGROUND = 6,
    NTK_SHARE_ERROR_EMPTY_CONTENT = 7,
    NTK_SHARE_ERROR_FILE_NOT_FOUND = 9,
    NTK_SHARE_ERROR_INVALID_URI = 17,
    NTK_SHARE_ERROR_IMAGE_LOAD_FAILED = 18,
    NTK_SHARE_ERROR_SHOW_FAILED = 19,
    NTK_SHARE_ERROR_CANCELED_BY_SYSTEM = 20,
    NTK_SHARE_ERROR_ACTIVITY_FAILED = 21
};

typedef int32_t ntk_share_item_kind;
enum {
    NTK_SHARE_ITEM_TEXT = 0,
    NTK_SHARE_ITEM_URL = 1,
    NTK_SHARE_ITEM_IMAGE_FILE = 2,
    NTK_SHARE_ITEM_FILE = 3
};

/** The result of a share. Owned by the receiver: free it with ntk_share_result_free. */
typedef struct ntk_share_result ntk_share_result;

/** result is NULL unless error is NTK_SHARE_ERROR_NONE. A share cannot be cancelled, so the callback and release,
    and with them user_data, can be held for as long as a host that replaced its window or root view controller,
    or a scene the system disconnected, keeps the sheet alive. On an iPad with several windows, which one shows
    the sheet is not decided. The order of the callbacks of several shares is not promised. */
typedef void (NTK_CALL *ntk_share_result_fn)(void* user_data, ntk_share_error error, uint32_t system_code,
                                             ntk_share_result* result);

#pragma pack(push, 8)
/** Every element of an array has the same struct_size. */
typedef struct ntk_share_item {
    uint32_t            struct_size;
    uint32_t            reserved0;
    ntk_share_item_kind kind;
    uint32_t            reserved1;
    const char*         value;                   /**< The text, the URL, or the file path. */
} ntk_share_item;

typedef struct ntk_share_request {
    uint32_t              struct_size;
    uint32_t              reserved0;
    const ntk_share_item* items;
    size_t                item_count;
    const char*           subject;               /**< NULL: none. */
    const char*           preview_title;         /**< NULL: none. */
    const char* const*    excluded_activity_types;
    size_t                excluded_activity_type_count;
} ntk_share_request;
#pragma pack(pop)

/** Needs the foreground: otherwise the callback gets NTK_SHARE_ERROR_NOT_FOREGROUND. */
ntk_share_error NTK_CALL ntk_share_content_async(const ntk_share_request* request, ntk_share_result_fn callback,
                                                 void* user_data, ntk_release_fn release);

/** 0 when the user closed the sheet. */
int32_t     NTK_CALL ntk_share_result_completed(const ntk_share_result* result);
/** The identifier of the chosen activity, or NULL. Valid until the handle is freed. out_size may be NULL. */
const char* NTK_CALL ntk_share_result_activity_type(const ntk_share_result* result, size_t* out_size);
void        NTK_CALL ntk_share_result_free(ntk_share_result* result);

#ifdef __cplusplus
}
#endif
#endif
