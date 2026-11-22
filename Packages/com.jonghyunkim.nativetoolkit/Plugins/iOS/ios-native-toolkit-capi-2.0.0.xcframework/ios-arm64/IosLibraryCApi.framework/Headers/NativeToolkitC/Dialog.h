/**
 * @file Dialog.h
 * @brief Dialogs: alerts, confirmations, action sheets, text input and login, and cancelling them.
 *
 * Rules for the whole C ABI (C ABI design part 1, chapter 1): every function may be called from any
 * thread, except ntk_notification_install (main thread only), and none waits for the main thread;
 * completions, events and accepted releases arrive on the main thread; strings are NUL-terminated
 * UTF-8; this header is C99, includes only <stddef.h> and <stdint.h> (through Common.h), and is ASCII only. The
 * declarations are those of Appendix A of the C ABI design part 2, which
 * scripts/check_c_abi_contract_ios.py compares with this file.
 */
#ifndef NATIVETOOLKITC_DIALOG_H
#define NATIVETOOLKITC_DIALOG_H

#if defined(__has_include) && __has_include(<IosLibraryCApi/NativeToolkitC/Common.h>)
#include <IosLibraryCApi/NativeToolkitC/Common.h>
#else
#include "Common.h"
#endif

#ifdef __cplusplus
extern "C" {
#endif

/** @file
   The rules of this header (design part 1, chapter 1):
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

/** The error of a dialog function or callback: one of NTK_DIALOG_ERROR_*. */
typedef int32_t ntk_dialog_error;
/** The values of ntk_dialog_error. */
enum {
    NTK_DIALOG_ERROR_NONE = 0,
    NTK_DIALOG_ERROR_INVALID_PARAMETER = 1,
    NTK_DIALOG_ERROR_NOT_INITIALIZED = 2,
    NTK_DIALOG_ERROR_NOT_SUPPORTED = 3,
    NTK_DIALOG_ERROR_UNKNOWN = 4,
    NTK_DIALOG_ERROR_OUT_OF_MEMORY = 5,
    NTK_DIALOG_ERROR_CANCELED = 6,
    NTK_DIALOG_ERROR_CANCELED_BY_SYSTEM = 7,
    NTK_DIALOG_ERROR_NOT_FOREGROUND = 8,
    NTK_DIALOG_ERROR_SHOW_FAILED = 10
};

/** The button the user chose (ntk_dialog_result_button): one of NTK_DIALOG_BUTTON_*. */
typedef int32_t ntk_dialog_button;
/** The values of ntk_dialog_button. */
enum {
    NTK_DIALOG_BUTTON_POSITIVE = 0,
    NTK_DIALOG_BUTTON_NEGATIVE = 1,
    NTK_DIALOG_BUTTON_DESTRUCTIVE = 2,
    NTK_DIALOG_BUTTON_OPTION = 3
};

/** The answer of a dialog. Owned by the receiver: free it with ntk_dialog_result_free. */
typedef struct ntk_dialog_result ntk_dialog_result;

/** Completes a request of the ntk_dialog_show_*_async functions. result is NULL unless error is
    NTK_DIALOG_ERROR_NONE. The callback and release do not come while a host
    that replaced its window or root view controller, or a scene the system disconnected, keeps the dialog
    alive: ntk_dialog_cancel ends the request then. On an iPad with several windows, which one shows the
    dialog is not decided. */
typedef void (NTK_CALL *ntk_dialog_result_fn)(void* user_data, uint64_t request_id, ntk_dialog_error error,
                                              uint32_t system_code, ntk_dialog_result* result);

#pragma pack(push, 8)
/** An alert with one button. NULL button texts take the library's defaults; NULL title and message mean
    none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_alert_request {
    uint32_t    struct_size;                 /**< Set to sizeof(ntk_dialog_alert_request). */
    uint32_t    reserved0;                   /**< Reserved; must be 0. */
    const char* title;                       /**< NULL: none. */
    const char* message;                     /**< NULL: none. */
    const char* button_text;                 /**< NULL: "OK". */
} ntk_dialog_alert_request;

/** A confirmation with a confirm and a cancel button. NULL button texts take the library's defaults; NULL
    title and message mean none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_confirm_request {
    uint32_t    struct_size;                 /**< Set to sizeof(ntk_dialog_confirm_request). */
    uint32_t    reserved0;                   /**< Reserved; must be 0. */
    const char* title;                       /**< NULL: none. */
    const char* message;                     /**< NULL: none. */
    const char* confirm_text;                /**< NULL: "OK". */
    const char* cancel_text;                 /**< NULL: "Cancel". */
} ntk_dialog_confirm_request;

/** A confirmation with a destructive and a cancel button. NULL button texts take the library's defaults;
    NULL title and message mean none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_destructive_request {
    uint32_t    struct_size;                 /**< Set to sizeof(ntk_dialog_destructive_request). */
    uint32_t    reserved0;                   /**< Reserved; must be 0. */
    const char* title;                       /**< NULL: none. */
    const char* message;                     /**< NULL: none. */
    const char* destructive_text;            /**< NULL: "Delete". */
    const char* cancel_text;                 /**< NULL: "Cancel". */
} ntk_dialog_destructive_request;

/** An action sheet of options and a cancel button. NULL button texts take the library's defaults; NULL
    title and message mean none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_action_sheet_request {
    uint32_t           struct_size;          /**< Set to sizeof(ntk_dialog_action_sheet_request). */
    uint32_t           reserved0;            /**< Reserved; must be 0. */
    const char*        title;                /**< NULL: none. */
    const char*        message;              /**< NULL: none. */
    const char* const* options;              /**< The texts of the options. May be NULL when option_count is 0. */
    size_t             option_count;         /**< The number of options; 0 is allowed (only the cancel button). */
    const char*        cancel_text;          /**< NULL: "Cancel". */
} ntk_dialog_action_sheet_request;

/** A dialog with a text field. NULL button texts take the library's defaults; NULL title, message and
    placeholder mean none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_text_input_request {
    uint32_t    struct_size;                 /**< Set to sizeof(ntk_dialog_text_input_request). */
    uint32_t    reserved0;                   /**< Reserved; must be 0. */
    const char* title;                       /**< NULL: none. */
    const char* message;                     /**< NULL: none. */
    const char* placeholder;                 /**< NULL: none. */
    const char* confirm_text;                /**< NULL: "OK". */
    const char* cancel_text;                 /**< NULL: "Cancel". */
    int32_t     confirm_disabled_when_empty; /**< Nonzero: the input is required. */
    uint32_t    reserved1;                   /**< Reserved; must be 0. */
} ntk_dialog_text_input_request;

/** A dialog with a username and a password field. NULL button texts and placeholders take the library's
    defaults; NULL title and message mean none. A zero-filled struct is the default dialog. */
typedef struct ntk_dialog_login_request {
    uint32_t    struct_size;                 /**< Set to sizeof(ntk_dialog_login_request). */
    uint32_t    reserved0;                   /**< Reserved; must be 0. */
    const char* title;                       /**< NULL: none. */
    const char* message;                     /**< NULL: none. */
    const char* username_placeholder;        /**< NULL: "Username". */
    const char* password_placeholder;        /**< NULL: "Password". */
    const char* login_text;                  /**< NULL: "Login". */
    const char* cancel_text;                 /**< NULL: "Cancel". */
    int32_t     login_disabled_when_empty;   /**< Nonzero: both fields are required. */
    uint32_t    reserved1;                   /**< Reserved; must be 0. */
} ntk_dialog_login_request;
#pragma pack(pop)

/** @name Showing a dialog
    Needs the foreground: otherwise the callback gets NTK_DIALOG_ERROR_NOT_FOREGROUND. request must not be NULL.
    out_request_id may be NULL; then the request cannot be cancelled. */
/** @{ */
/** Shows an alert (ntk_dialog_alert_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_alert_async(const ntk_dialog_alert_request* request,
                                                      ntk_dialog_result_fn callback, void* user_data,
                                                      ntk_release_fn release, uint64_t* out_request_id);
/** Shows a confirmation (ntk_dialog_confirm_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_confirm_async(const ntk_dialog_confirm_request* request,
                                                        ntk_dialog_result_fn callback, void* user_data,
                                                        ntk_release_fn release, uint64_t* out_request_id);
/** Shows a confirmation with a destructive button (ntk_dialog_destructive_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_destructive_async(const ntk_dialog_destructive_request* request,
                                                            ntk_dialog_result_fn callback, void* user_data,
                                                            ntk_release_fn release, uint64_t* out_request_id);
/** Shows an action sheet (ntk_dialog_action_sheet_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_action_sheet_async(const ntk_dialog_action_sheet_request* request,
                                                             ntk_dialog_result_fn callback, void* user_data,
                                                             ntk_release_fn release, uint64_t* out_request_id);
/** Shows a text input dialog (ntk_dialog_text_input_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_text_input_async(const ntk_dialog_text_input_request* request,
                                                           ntk_dialog_result_fn callback, void* user_data,
                                                           ntk_release_fn release, uint64_t* out_request_id);
/** Shows a login dialog (ntk_dialog_login_request). */
ntk_dialog_error NTK_CALL ntk_dialog_show_login_async(const ntk_dialog_login_request* request,
                                                      ntk_dialog_result_fn callback, void* user_data,
                                                      ntk_release_fn release, uint64_t* out_request_id);
/** @} */
/** Cancels a request of the ntk_dialog_show_*_async functions. Always returns NTK_DIALOG_ERROR_NONE.
    From any thread; does not wait. The request ends with NTK_DIALOG_ERROR_CANCELED; a dialog covered by
    another screen stays. An unknown or finished request_id does nothing. */
ntk_dialog_error NTK_CALL ntk_dialog_cancel(uint64_t request_id);

/** @name Reading the result
    Readers: pointers stay valid until the handle is freed. out_size may be NULL. */
/** @{ */
/** The button the user chose. -1 for NULL: check error first. */
ntk_dialog_button NTK_CALL ntk_dialog_result_button(const ntk_dialog_result* result);
/** The index of the chosen option from 0, or -1 unless the button is OPTION or for NULL. */
int64_t     NTK_CALL ntk_dialog_result_option_index(const ntk_dialog_result* result);
/** The text of the chosen button. */
const char* NTK_CALL ntk_dialog_result_button_text(const ntk_dialog_result* result, size_t* out_size);
/** The input of a confirmed text input dialog, or NULL. */
const char* NTK_CALL ntk_dialog_result_text(const ntk_dialog_result* result, size_t* out_size);
/** The username of a confirmed login dialog, or NULL. */
const char* NTK_CALL ntk_dialog_result_username(const ntk_dialog_result* result, size_t* out_size);
/** The password of a confirmed login dialog, or NULL. */
const char* NTK_CALL ntk_dialog_result_password(const ntk_dialog_result* result, size_t* out_size);
/** Releases the result. Does nothing for NULL. */
void        NTK_CALL ntk_dialog_result_free(ntk_dialog_result* result);
/** @} */

#ifdef __cplusplus
}
#endif
#endif
