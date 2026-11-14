/**
 * @file Clipboard.h
 * @brief The clipboard: copying, reading, the description of the contents and change events.
 *
 * Rules for the whole C ABI (C ABI design part 1, chapter 1): every function may be called from any
 * thread, except ntk_notification_install (main thread only), and none waits for the main thread;
 * completions, events and accepted releases arrive on the main thread; strings are NUL-terminated
 * UTF-8; this header is C99, includes only <stddef.h> and <stdint.h> (through Common.h), and is ASCII only. The
 * declarations are those of Appendix A of the C ABI design part 2, which
 * scripts/check_c_abi_contract_ios.py compares with this file.
 */
#ifndef NATIVETOOLKITC_CLIPBOARD_H
#define NATIVETOOLKITC_CLIPBOARD_H

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
typedef int32_t ntk_clipboard_error;
enum {
    NTK_CLIPBOARD_ERROR_NONE = 0,
    NTK_CLIPBOARD_ERROR_INVALID_PARAMETER = 1,
    NTK_CLIPBOARD_ERROR_NOT_INITIALIZED = 2,
    NTK_CLIPBOARD_ERROR_NOT_SUPPORTED = 3,
    NTK_CLIPBOARD_ERROR_UNKNOWN = 4,
    NTK_CLIPBOARD_ERROR_OUT_OF_MEMORY = 5,
    NTK_CLIPBOARD_ERROR_EMPTY_CONTENT = 6,
    NTK_CLIPBOARD_ERROR_EMPTY_ITEMS = 7,
    NTK_CLIPBOARD_ERROR_INVALID_URI = 8,
    NTK_CLIPBOARD_ERROR_UNAVAILABLE = 9,
    NTK_CLIPBOARD_ERROR_EMPTY_PATTERNS = 12,
    NTK_CLIPBOARD_ERROR_INVALID_TYPE = 13,
    NTK_CLIPBOARD_ERROR_INVALID_NAME = 14,
    NTK_CLIPBOARD_ERROR_INVALID_COLOR = 15,
    NTK_CLIPBOARD_ERROR_INVALID_IMAGE_DATA = 16,
    NTK_CLIPBOARD_ERROR_INVALID_EXPIRATION = 17,
    NTK_CLIPBOARD_ERROR_CONTENT_TOO_LARGE = 18,
    NTK_CLIPBOARD_ERROR_FILE_NOT_FOUND = 19,
    NTK_CLIPBOARD_ERROR_IMAGE_LOAD_FAILED = 20,
    NTK_CLIPBOARD_ERROR_IMAGE_ENCODE_FAILED = 21,
    NTK_CLIPBOARD_ERROR_CANNOT_REMOVE_GENERAL = 22,
    NTK_CLIPBOARD_ERROR_NO_MATCHING_ITEM = 23,
    NTK_CLIPBOARD_ERROR_LOAD_FAILED = 24,
    NTK_CLIPBOARD_ERROR_UNEXPECTED_TYPE = 25,
    NTK_CLIPBOARD_ERROR_FILE_COPY_FAILED = 26,
    NTK_CLIPBOARD_ERROR_CANCELED = 27,
    NTK_CLIPBOARD_ERROR_TIMED_OUT = 28,
    NTK_CLIPBOARD_ERROR_DETECTION_FAILED = 29
};

typedef int32_t ntk_clipboard_scope_kind;
enum {
    NTK_CLIPBOARD_SCOPE_GENERAL = 0,
    NTK_CLIPBOARD_SCOPE_NAMED = 1,
    NTK_CLIPBOARD_SCOPE_UNIQUE = 2
};

typedef int32_t ntk_clipboard_content_kind;
enum {
    NTK_CLIPBOARD_CONTENT_PLAIN_TEXT = 0,
    NTK_CLIPBOARD_CONTENT_HTML = 1,
    NTK_CLIPBOARD_CONTENT_URL = 2,
    NTK_CLIPBOARD_CONTENT_IMAGE_FILE = 3,
    NTK_CLIPBOARD_CONTENT_IMAGE_DATA = 4,
    NTK_CLIPBOARD_CONTENT_COLOR = 5,
    NTK_CLIPBOARD_CONTENT_CUSTOM_DATA = 6,
    NTK_CLIPBOARD_CONTENT_MULTIPLE_TEXT = 7,
    NTK_CLIPBOARD_CONTENT_MULTI_REPRESENTATION = 8
};

/* What to load, and what a load returned. */
typedef int32_t ntk_clipboard_item_kind;
enum {
    NTK_CLIPBOARD_ITEM_TEXT = 0,
    NTK_CLIPBOARD_ITEM_URL = 1,
    NTK_CLIPBOARD_ITEM_IMAGE = 2,
    NTK_CLIPBOARD_ITEM_FILE = 3
};

typedef int32_t ntk_clipboard_change_event_kind;
enum {
    NTK_CLIPBOARD_CHANGE_CHANGED = 0,
    NTK_CLIPBOARD_CHANGE_REMOVED = 1
};

/* A set of detection patterns: an OR of these bits. */
typedef uint32_t ntk_clipboard_patterns;
enum {
    NTK_CLIPBOARD_PATTERN_PROBABLE_WEB_URL = 1u << 0,
    NTK_CLIPBOARD_PATTERN_PROBABLE_WEB_SEARCH = 1u << 1,
    NTK_CLIPBOARD_PATTERN_NUMBER = 1u << 2,
    NTK_CLIPBOARD_PATTERN_LINK = 1u << 3,
    NTK_CLIPBOARD_PATTERN_EMAIL_ADDRESS = 1u << 4,
    NTK_CLIPBOARD_PATTERN_PHONE_NUMBER = 1u << 5,
    NTK_CLIPBOARD_PATTERN_POSTAL_ADDRESS = 1u << 6,
    NTK_CLIPBOARD_PATTERN_CALENDAR_EVENT = 1u << 7,
    NTK_CLIPBOARD_PATTERN_FLIGHT_NUMBER = 1u << 8,
    NTK_CLIPBOARD_PATTERN_MONEY_AMOUNT = 1u << 9,
    NTK_CLIPBOARD_PATTERN_SHIPMENT_TRACKING_NUMBER = 1u << 10
};

/** What ntk_clipboard_read_async returned. */
typedef struct ntk_clipboard_read_result ntk_clipboard_read_result;
/** What ntk_clipboard_get_snapshot_async returned. */
typedef struct ntk_clipboard_snapshot ntk_clipboard_snapshot;
/** What ntk_clipboard_load_item_async returned. */
typedef struct ntk_clipboard_loaded_item ntk_clipboard_loaded_item;
/** A change of the observed pasteboard. */
typedef struct ntk_clipboard_change ntk_clipboard_change;
/** A change listener registration. */
typedef struct ntk_clipboard_listener ntk_clipboard_listener;

/* Every callback is called on the main thread. A handle or string it receives is owned by the
   receiver and is NULL unless error is NTK_CLIPBOARD_ERROR_NONE. */
typedef void (NTK_CALL *ntk_clipboard_done_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code);
typedef void (NTK_CALL *ntk_clipboard_read_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                               ntk_clipboard_read_result* result);
/** data is NULL when the pasteboard has no item of the type. */
typedef void (NTK_CALL *ntk_clipboard_data_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                               ntk_bytes* data);
typedef void (NTK_CALL *ntk_clipboard_snapshot_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                                   ntk_clipboard_snapshot* snapshot);
/** name is the pasteboard's name, to use in ntk_clipboard_scope. */
typedef void (NTK_CALL *ntk_clipboard_name_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                               ntk_string* name);
typedef void (NTK_CALL *ntk_clipboard_patterns_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                                   ntk_clipboard_patterns patterns);
/** values_json is a JSON object: detectedPatterns, probableWebURL, probableWebSearch, number, links,
    emailAddresses, phoneNumbers, postalAddresses, calendarEvents, flightNumbers, moneyAmounts,
    shipmentTrackingNumbers. */
typedef void (NTK_CALL *ntk_clipboard_values_fn)(void* user_data, ntk_clipboard_error error, uint32_t system_code,
                                                 ntk_string* values_json);
typedef void (NTK_CALL *ntk_clipboard_load_fn)(void* user_data, uint64_t request_id, ntk_clipboard_error error,
                                               uint32_t system_code, ntk_clipboard_loaded_item* item);
/** changed is nonzero when the pasteboard changed since the last check. */
typedef void (NTK_CALL *ntk_clipboard_foreground_change_fn)(void* user_data, ntk_clipboard_error error,
                                                            uint32_t system_code, int32_t changed);
/** The observed pasteboard changed. change is owned by the receiver. */
typedef void (NTK_CALL *ntk_clipboard_change_fn)(void* user_data, ntk_clipboard_change* change);

#pragma pack(push, 8)
/** NULL means the general pasteboard. */
typedef struct ntk_clipboard_scope {
    uint32_t                 struct_size;
    uint32_t                 reserved0;
    ntk_clipboard_scope_kind kind;
    uint32_t                 reserved1;
    const char*              name;           /**< Required for NAMED and UNIQUE. */
} ntk_clipboard_scope;

/** One type of a MULTI_REPRESENTATION content. Every element of an array has the same struct_size. */
typedef struct ntk_clipboard_representation {
    uint32_t       struct_size;
    uint32_t       reserved0;
    const char*    ut_type;
    const uint8_t* data;
    size_t         data_size;
} ntk_clipboard_representation;

/** kind decides which fields are read; the others are not. */
typedef struct ntk_clipboard_write_content {
    uint32_t                   struct_size;
    uint32_t                   reserved0;
    ntk_clipboard_content_kind kind;
    uint32_t                   reserved1;
    const char*                text;         /**< PLAIN_TEXT (required); the plain text of HTML (NULL is ""). */
    const char*                html;         /**< HTML. */
    const char*                url;          /**< URL. */
    const char*                path;         /**< IMAGE_FILE. */
    const uint8_t*             data;         /**< IMAGE_DATA, CUSTOM_DATA. */
    size_t                     data_size;
    const char*                ut_type;      /**< IMAGE_DATA, CUSTOM_DATA. */
    double                     red;          /**< COLOR: 0.0 to 1.0. */
    double                     green;
    double                     blue;
    double                     alpha;        /**< Required; 0.0 is transparent. */
    const char* const*         texts;        /**< MULTIPLE_TEXT. */
    size_t                     text_count;
    const ntk_clipboard_representation* representations;  /**< MULTI_REPRESENTATION. */
    size_t                     representation_count;
} ntk_clipboard_write_content;

/** NULL or zero-filled: local to this device, no expiration. */
typedef struct ntk_clipboard_copy_options {
    uint32_t struct_size;
    uint32_t reserved0;
    int32_t  not_local_only;                 /**< Nonzero: Handoff may carry it to other devices. */
    int32_t  has_expiration;                 /**< Nonzero: expiration_unix_seconds is read. */
    double   expiration_unix_seconds;
} ntk_clipboard_copy_options;
#pragma pack(pop)

/* The pasteboard is used on the main thread. scope may be NULL (general) unless noted. */
ntk_clipboard_error NTK_CALL ntk_clipboard_copy_async(const ntk_clipboard_write_content* content,
                                                      const ntk_clipboard_copy_options* options,
                                                      const ntk_clipboard_scope* scope,
                                                      ntk_clipboard_done_fn callback, void* user_data,
                                                      ntk_release_fn release);
ntk_clipboard_error NTK_CALL ntk_clipboard_append_async(const ntk_clipboard_write_content* content,
                                                        const ntk_clipboard_scope* scope,
                                                        ntk_clipboard_done_fn callback, void* user_data,
                                                        ntk_release_fn release);
/** When another app wrote the content, iOS asks the user to allow the paste and the main thread stops
    until the answer. A denied paste is not a failure: one item with no types and no text. To avoid the
    wait, use ntk_clipboard_load_item_async. */
ntk_clipboard_error NTK_CALL ntk_clipboard_read_async(const ntk_clipboard_scope* scope,
                                                      ntk_clipboard_read_fn callback, void* user_data,
                                                      ntk_release_fn release);
/** Stops the main thread like ntk_clipboard_read_async; a denied paste gives NULL. The bytes are returned
    as they are. */
ntk_clipboard_error NTK_CALL ntk_clipboard_read_data_async(const char* ut_type, const ntk_clipboard_scope* scope,
                                                           ntk_clipboard_data_fn callback, void* user_data,
                                                           ntk_release_fn release);
/** matching_types may be NULL when matching_type_count is 0: no matching is done. */
ntk_clipboard_error NTK_CALL ntk_clipboard_get_snapshot_async(const char* const* matching_types,
                                                              size_t matching_type_count,
                                                              const ntk_clipboard_scope* scope,
                                                              ntk_clipboard_snapshot_fn callback, void* user_data,
                                                              ntk_release_fn release);
ntk_clipboard_error NTK_CALL ntk_clipboard_clear_async(const ntk_clipboard_scope* scope,
                                                       ntk_clipboard_done_fn callback, void* user_data,
                                                       ntk_release_fn release);
/** kind is NAMED (name is required) or UNIQUE (name is not read); GENERAL is rejected. */
ntk_clipboard_error NTK_CALL ntk_clipboard_create_pasteboard_async(ntk_clipboard_scope_kind kind, const char* name,
                                                                   ntk_clipboard_name_fn callback, void* user_data,
                                                                   ntk_release_fn release);
/** scope must not be NULL. */
ntk_clipboard_error NTK_CALL ntk_clipboard_remove_pasteboard_async(const ntk_clipboard_scope* scope,
                                                                   ntk_clipboard_done_fn callback, void* user_data,
                                                                   ntk_release_fn release);
ntk_clipboard_error NTK_CALL ntk_clipboard_detect_patterns_async(ntk_clipboard_patterns patterns,
                                                                 const ntk_clipboard_scope* scope,
                                                                 ntk_clipboard_patterns_fn callback, void* user_data,
                                                                 ntk_release_fn release);
/** Reads the content, so iOS can ask the user to allow the paste; the main thread does not stop. The 5-second
    timeout includes the time to answer (NTK_CLIPBOARD_ERROR_TIMED_OUT); a denied paste gives
    NTK_CLIPBOARD_ERROR_DETECTION_FAILED. */
ntk_clipboard_error NTK_CALL ntk_clipboard_detect_values_async(ntk_clipboard_patterns patterns,
                                                               const ntk_clipboard_scope* scope,
                                                               ntk_clipboard_values_fn callback, void* user_data,
                                                               ntk_release_fn release);
/** Does not stop the main thread. The 15-second timeout includes the time the user takes to answer the
    paste prompt; a load not answered by then ends with NTK_CLIPBOARD_ERROR_TIMED_OUT, and a later answer
    delivers nothing. A denied paste gives NTK_CLIPBOARD_ERROR_LOAD_FAILED. ut_type is required for NTK_CLIPBOARD_ITEM_FILE and not read otherwise.
    out_request_id may be NULL; then the load cannot be cancelled. */
ntk_clipboard_error NTK_CALL ntk_clipboard_load_item_async(ntk_clipboard_item_kind kind, const char* ut_type,
                                                           const ntk_clipboard_scope* scope,
                                                           ntk_clipboard_load_fn callback, void* user_data,
                                                           ntk_release_fn release, uint64_t* out_request_id);
/** From any thread; does not wait. The load ends with NTK_CLIPBOARD_ERROR_CANCELED. A paste prompt can still
    appear when the load is starting at that moment. An unknown or finished request_id does nothing. */
ntk_clipboard_error NTK_CALL ntk_clipboard_cancel_load(uint64_t request_id);
/** Only one pasteboard is observed at a time; this replaces the previous one. When this fails with
    NTK_CLIPBOARD_ERROR_UNAVAILABLE, the previous one has already stopped. */
ntk_clipboard_error NTK_CALL ntk_clipboard_start_observing_async(const ntk_clipboard_scope* scope,
                                                                 ntk_clipboard_done_fn callback, void* user_data,
                                                                 ntk_release_fn release);
/** Stops observing for everyone in the process, also when another component started it. */
ntk_clipboard_error NTK_CALL ntk_clipboard_stop_observing_async(ntk_clipboard_done_fn callback, void* user_data,
                                                                ntk_release_fn release);
/** Compares the change count with the last check; it does not look at the foreground. The first check gives 0. */
ntk_clipboard_error NTK_CALL ntk_clipboard_check_foreground_change_async(const ntk_clipboard_scope* scope,
                                                                         ntk_clipboard_foreground_change_fn callback,
                                                                         void* user_data, ntk_release_fn release);

/** Registering does not start observing. */
ntk_clipboard_error NTK_CALL ntk_clipboard_add_change_listener(ntk_clipboard_change_fn callback, void* user_data,
                                                               ntk_release_fn release,
                                                               ntk_clipboard_listener** out_listener);
/** Removes the registration; observing goes on. The handle is invalid afterwards; user_data is not until
    release. Does nothing for NULL. */
void NTK_CALL ntk_clipboard_listener_remove(ntk_clipboard_listener* listener);

/* Readers: pointers stay valid until the handle is freed. out_size may be NULL. An index out of range
   gives NULL or 0. */
size_t      NTK_CALL ntk_clipboard_read_result_item_count(const ntk_clipboard_read_result* result);
size_t      NTK_CALL ntk_clipboard_read_result_item_type_count(const ntk_clipboard_read_result* result, size_t item);
const char* NTK_CALL ntk_clipboard_read_result_item_type_at(const ntk_clipboard_read_result* result, size_t item,
                                                            size_t index, size_t* out_size);
const char* NTK_CALL ntk_clipboard_read_result_item_text_at(const ntk_clipboard_read_result* result, size_t item,
                                                            size_t* out_size);
const char* NTK_CALL ntk_clipboard_read_result_item_url_at(const ntk_clipboard_read_result* result, size_t item,
                                                           size_t* out_size);
const char* NTK_CALL ntk_clipboard_read_result_item_image_type_at(const ntk_clipboard_read_result* result, size_t item,
                                                                  size_t* out_size);
void        NTK_CALL ntk_clipboard_read_result_free(ntk_clipboard_read_result* result);

int32_t     NTK_CALL ntk_clipboard_snapshot_has_strings(const ntk_clipboard_snapshot* snapshot);
int32_t     NTK_CALL ntk_clipboard_snapshot_has_urls(const ntk_clipboard_snapshot* snapshot);
int32_t     NTK_CALL ntk_clipboard_snapshot_has_images(const ntk_clipboard_snapshot* snapshot);
int32_t     NTK_CALL ntk_clipboard_snapshot_has_colors(const ntk_clipboard_snapshot* snapshot);
size_t      NTK_CALL ntk_clipboard_snapshot_number_of_items(const ntk_clipboard_snapshot* snapshot);
size_t      NTK_CALL ntk_clipboard_snapshot_type_count(const ntk_clipboard_snapshot* snapshot);
const char* NTK_CALL ntk_clipboard_snapshot_type_at(const ntk_clipboard_snapshot* snapshot, size_t index,
                                                    size_t* out_size);
size_t      NTK_CALL ntk_clipboard_snapshot_item_type_count(const ntk_clipboard_snapshot* snapshot, size_t item);
const char* NTK_CALL ntk_clipboard_snapshot_item_type_at(const ntk_clipboard_snapshot* snapshot, size_t item,
                                                         size_t index, size_t* out_size);
/** Nonzero when matching was requested; 0 means the two functions below have nothing. */
int32_t     NTK_CALL ntk_clipboard_snapshot_has_matching(const ntk_clipboard_snapshot* snapshot);
size_t      NTK_CALL ntk_clipboard_snapshot_matching_count(const ntk_clipboard_snapshot* snapshot);
/** The index of the matching item, or -1 when index is out of range or for NULL. */
int64_t     NTK_CALL ntk_clipboard_snapshot_matching_at(const ntk_clipboard_snapshot* snapshot, size_t index);
void        NTK_CALL ntk_clipboard_snapshot_free(ntk_clipboard_snapshot* snapshot);

/** -1 for NULL. */
ntk_clipboard_item_kind NTK_CALL ntk_clipboard_loaded_item_kind(const ntk_clipboard_loaded_item* item);
const char*    NTK_CALL ntk_clipboard_loaded_item_text(const ntk_clipboard_loaded_item* item, size_t* out_size);
const char*    NTK_CALL ntk_clipboard_loaded_item_url(const ntk_clipboard_loaded_item* item, size_t* out_size);
const uint8_t* NTK_CALL ntk_clipboard_loaded_item_image_data(const ntk_clipboard_loaded_item* item, size_t* out_size);
const char*    NTK_CALL ntk_clipboard_loaded_item_image_type(const ntk_clipboard_loaded_item* item, size_t* out_size);
/** You own the file and its parent directory: delete them when done. ntk_clipboard_loaded_item_free does not. */
const char*    NTK_CALL ntk_clipboard_loaded_item_file_path(const ntk_clipboard_loaded_item* item, size_t* out_size);
void           NTK_CALL ntk_clipboard_loaded_item_free(ntk_clipboard_loaded_item* item);

/** -1 for NULL. */
ntk_clipboard_change_event_kind NTK_CALL ntk_clipboard_change_kind(const ntk_clipboard_change* change);
/** -1 for NULL. */
ntk_clipboard_scope_kind        NTK_CALL ntk_clipboard_change_scope_kind(const ntk_clipboard_change* change);
const char* NTK_CALL ntk_clipboard_change_scope_name(const ntk_clipboard_change* change, size_t* out_size);
size_t      NTK_CALL ntk_clipboard_change_types_added_count(const ntk_clipboard_change* change);
const char* NTK_CALL ntk_clipboard_change_types_added_at(const ntk_clipboard_change* change, size_t index,
                                                         size_t* out_size);
size_t      NTK_CALL ntk_clipboard_change_types_removed_count(const ntk_clipboard_change* change);
const char* NTK_CALL ntk_clipboard_change_types_removed_at(const ntk_clipboard_change* change, size_t index,
                                                           size_t* out_size);
void        NTK_CALL ntk_clipboard_change_free(ntk_clipboard_change* change);

#ifdef __cplusplus
}
#endif
#endif
