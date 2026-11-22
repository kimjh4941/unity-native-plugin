/**
 * @file Notification.h
 * @brief Local notifications: the delegate, the permission, showing and scheduling, categories and events.
 *
 * Rules for the whole C ABI (C ABI design part 1, chapter 1): every function may be called from any
 * thread, except ntk_notification_install (main thread only), and none waits for the main thread;
 * completions, events and accepted releases arrive on the main thread; strings are NUL-terminated
 * UTF-8; this header is C99, includes only <stddef.h> and <stdint.h> (through Common.h), and is ASCII only. The
 * declarations are those of Appendix A of the C ABI design part 2, which
 * scripts/check_c_abi_contract_ios.py compares with this file.
 */
#ifndef NATIVETOOLKITC_NOTIFICATION_H
#define NATIVETOOLKITC_NOTIFICATION_H

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
   - Every function except ntk_notification_install can be called from any thread, and none waits for the main thread. Completions,
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

/** The error of a notification function or callback: one of NTK_NOTIFICATION_ERROR_*. */
typedef int32_t ntk_notification_error;
/** The values of ntk_notification_error. */
enum {
    NTK_NOTIFICATION_ERROR_NONE = 0,
    NTK_NOTIFICATION_ERROR_INVALID_PARAMETER = 1,
    NTK_NOTIFICATION_ERROR_NOT_INITIALIZED = 2,
    NTK_NOTIFICATION_ERROR_NOT_SUPPORTED = 3,
    NTK_NOTIFICATION_ERROR_UNKNOWN = 4,
    NTK_NOTIFICATION_ERROR_OUT_OF_MEMORY = 5,
    NTK_NOTIFICATION_ERROR_NOT_FOREGROUND = 8,
    NTK_NOTIFICATION_ERROR_PERMISSION_DENIED = 10,
    NTK_NOTIFICATION_ERROR_SETTINGS_NOT_OPENED = 15,
    NTK_NOTIFICATION_ERROR_WRONG_THREAD = 16,
    NTK_NOTIFICATION_ERROR_ATTACHMENT_LOAD_FAILED = 17,
    NTK_NOTIFICATION_ERROR_ADD_REQUEST_FAILED = 18,
    NTK_NOTIFICATION_ERROR_SET_BADGE_COUNT_FAILED = 19,
    NTK_NOTIFICATION_ERROR_AUTHORIZATION_FAILED = 20
};

/** The sound of a notification (ntk_notification_content_set_sound): one of NTK_NOTIFICATION_SOUND_*. */
typedef int32_t ntk_notification_sound_kind;
/** The values of ntk_notification_sound_kind. */
enum {
    NTK_NOTIFICATION_SOUND_DEFAULT = 0,
    NTK_NOTIFICATION_SOUND_DEFAULT_CRITICAL = 1,
    NTK_NOTIFICATION_SOUND_NAMED = 2,
    NTK_NOTIFICATION_SOUND_NONE = 3
};

/** The interruption level of a notification (ntk_notification_content_set_interruption_level): one of
    NTK_NOTIFICATION_INTERRUPTION_*. */
typedef int32_t ntk_notification_interruption_level;
/** The values of ntk_notification_interruption_level. */
enum {
    NTK_NOTIFICATION_INTERRUPTION_NOT_SET = 0,
    NTK_NOTIFICATION_INTERRUPTION_PASSIVE = 1,
    NTK_NOTIFICATION_INTERRUPTION_ACTIVE = 2,
    NTK_NOTIFICATION_INTERRUPTION_TIME_SENSITIVE = 3,
    NTK_NOTIFICATION_INTERRUPTION_CRITICAL = 4
};

/** What a ntk_notification_trigger is: one of NTK_NOTIFICATION_TRIGGER_*. */
typedef int32_t ntk_notification_trigger_kind;
/** The values of ntk_notification_trigger_kind. */
enum {
    NTK_NOTIFICATION_TRIGGER_TIME_INTERVAL = 0,
    NTK_NOTIFICATION_TRIGGER_CALENDAR = 1,
    NTK_NOTIFICATION_TRIGGER_LOCATION = 2
};

/** Which calendar fields of ntk_notification_trigger are read: an OR of these bits. */
enum {
    NTK_NOTIFICATION_CALENDAR_YEAR = 1u << 0,
    NTK_NOTIFICATION_CALENDAR_MONTH = 1u << 1,
    NTK_NOTIFICATION_CALENDAR_DAY = 1u << 2,
    NTK_NOTIFICATION_CALENDAR_HOUR = 1u << 3,
    NTK_NOTIFICATION_CALENDAR_MINUTE = 1u << 4,
    NTK_NOTIFICATION_CALENDAR_SECOND = 1u << 5
};

/** The bits of ntk_notification_action.options. */
enum {
    NTK_NOTIFICATION_ACTION_AUTHENTICATION_REQUIRED = 1u << 0,
    NTK_NOTIFICATION_ACTION_DESTRUCTIVE = 1u << 1,
    NTK_NOTIFICATION_ACTION_FOREGROUND = 1u << 2
};

/** The bits of ntk_notification_category.options. */
enum {
    NTK_NOTIFICATION_CATEGORY_CUSTOM_DISMISS_ACTION = 1u << 0,
    NTK_NOTIFICATION_CATEGORY_ALLOW_IN_CAR_PLAY = 1u << 1,
    NTK_NOTIFICATION_CATEGORY_HIDDEN_PREVIEWS_SHOW_TITLE = 1u << 2,
    NTK_NOTIFICATION_CATEGORY_ALLOW_ANNOUNCEMENT = 1u << 3  /**< Accepted, but has no effect: the system ignores it
                                                                since iOS 15, so the library does not pass it on. */
};

/** The authorization status (ntk_notification_get_authorization_status_async): one of NTK_NOTIFICATION_STATUS_*. */
typedef int32_t ntk_notification_authorization_status;
/** The values of ntk_notification_authorization_status. */
enum {
    NTK_NOTIFICATION_STATUS_NOT_DETERMINED = 0,
    NTK_NOTIFICATION_STATUS_DENIED = 1,
    NTK_NOTIFICATION_STATUS_AUTHORIZED = 2,
    NTK_NOTIFICATION_STATUS_PROVISIONAL = 3,
    NTK_NOTIFICATION_STATUS_EPHEMERAL = 4,
    NTK_NOTIFICATION_STATUS_UNKNOWN = 5
};

/** The options of ntk_notification_request_permission_async: an OR of these bits; the values are those of
    UNAuthorizationOptions. 0 means ALERT, SOUND and BADGE. */
enum {
    NTK_NOTIFICATION_AUTH_BADGE = 1u << 0,
    NTK_NOTIFICATION_AUTH_SOUND = 1u << 1,
    NTK_NOTIFICATION_AUTH_ALERT = 1u << 2,
    NTK_NOTIFICATION_AUTH_CAR_PLAY = 1u << 3,
    NTK_NOTIFICATION_AUTH_CRITICAL_ALERT = 1u << 4,
    NTK_NOTIFICATION_AUTH_PROVIDES_APP_SETTINGS = 1u << 5
};

/** What the user did with a notification (ntk_notification_interaction_kind): one of NTK_NOTIFICATION_EVENT_KIND_*. */
typedef int32_t ntk_notification_event_kind;
/** The values of ntk_notification_event_kind. */
enum {
    NTK_NOTIFICATION_EVENT_KIND_BODY_TAP = 0,
    NTK_NOTIFICATION_EVENT_KIND_ACTION = 1,
    NTK_NOTIFICATION_EVENT_KIND_DISMISS = 2,
    NTK_NOTIFICATION_EVENT_KIND_TEXT_INPUT = 3
};

/** The content of a notification. Not thread safe: build it on one thread. */
typedef struct ntk_notification_content ntk_notification_content;
/** The user's response to a notification. Owned by the receiver. */
typedef struct ntk_notification_interaction ntk_notification_interaction;
/** The system asked the app to show its notification settings. Owned by the receiver. */
typedef struct ntk_notification_settings_request ntk_notification_settings_request;
/** Pending or delivered notifications. Owned by the receiver. */
typedef struct ntk_notification_list ntk_notification_list;
/** An event listener registration. */
typedef struct ntk_notification_listener ntk_notification_listener;

/** Completes an operation that has no value to return. */
typedef void (NTK_CALL *ntk_notification_done_fn)(void* user_data, ntk_notification_error error, uint32_t system_code);
/** Completes ntk_notification_request_permission_async and ntk_notification_has_permission_async.
    granted is 1 when permitted, 0 when not. ntk_notification_has_permission_async gives 0 for
    NTK_NOTIFICATION_STATUS_EPHEMERAL; ntk_notification_request_permission_async gives 1. */
typedef void (NTK_CALL *ntk_notification_permission_fn)(void* user_data, ntk_notification_error error,
                                                        uint32_t system_code, int32_t granted);
/** Completes ntk_notification_get_authorization_status_async with the status. */
typedef void (NTK_CALL *ntk_notification_status_fn)(void* user_data, ntk_notification_error error,
                                                    uint32_t system_code,
                                                    ntk_notification_authorization_status status);
/** Completes ntk_notification_get_scheduled_async and ntk_notification_get_delivered_async. list is NULL
    unless error is NTK_NOTIFICATION_ERROR_NONE. */
typedef void (NTK_CALL *ntk_notification_list_fn)(void* user_data, ntk_notification_error error,
                                                  uint32_t system_code, ntk_notification_list* list);
/** Completes ntk_notification_get_launch_response_async.
    response is NULL when there is none for now: until the app first becomes active it can appear later. Use
    the interaction listener when you can (ntk_notification_interaction_is_launch). */
typedef void (NTK_CALL *ntk_notification_launch_fn)(void* user_data, ntk_notification_error error,
                                                    uint32_t system_code, ntk_notification_interaction* response);
/** The user responded to a notification. event is owned by the receiver. */
typedef void (NTK_CALL *ntk_notification_interaction_fn)(void* user_data, ntk_notification_interaction* event);
/** The system asked the app to show its notification settings. request is owned by the receiver. */
typedef void (NTK_CALL *ntk_notification_settings_request_fn)(void* user_data,
                                                              ntk_notification_settings_request* request);

#pragma pack(push, 8)
/** kind decides which fields are read; the others are not. */
typedef struct ntk_notification_trigger {
    uint32_t                      struct_size;          /**< Set to sizeof(ntk_notification_trigger). */
    uint32_t                      reserved0;            /**< Reserved; must be 0. */
    ntk_notification_trigger_kind kind;                 /**< TIME_INTERVAL, CALENDAR or LOCATION. */
    int32_t                       repeats;              /**< TIME_INTERVAL, CALENDAR. */
    double                        interval_seconds;     /**< TIME_INTERVAL: above 0; 60 or more when repeating. */
    uint32_t                      calendar_fields;      /**< CALENDAR: NTK_NOTIFICATION_CALENDAR_*; not 0. */
    int32_t                       year;                 /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_YEAR. */
    int32_t                       month;                /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_MONTH. */
    int32_t                       day;                  /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_DAY. */
    int32_t                       hour;                 /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_HOUR. */
    int32_t                       minute;               /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_MINUTE. */
    int32_t                       second;               /**< CALENDAR, with NTK_NOTIFICATION_CALENDAR_SECOND. */
    uint32_t                      reserved1;            /**< Reserved; must be 0. */
    const char*                   location_identifier;  /**< LOCATION. */
    double                        latitude;             /**< LOCATION: -90 to 90. */
    double                        longitude;            /**< LOCATION: -180 to 180. */
    double                        radius;               /**< LOCATION: above 0. */
    int32_t                       not_notify_on_entry;  /**< Nonzero: do not notify when entering. */
    int32_t                       notify_on_exit;       /**< Nonzero: notify when leaving. Location triggers need the
                                                             location permission and its Info.plist text. */
} ntk_notification_trigger;

/** An action of a ntk_notification_category. Every element of an array has the same struct_size. */
typedef struct ntk_notification_action {
    uint32_t    struct_size;                            /**< Set to sizeof(ntk_notification_action). */
    uint32_t    reserved0;                              /**< Reserved; must be 0. */
    const char* identifier;                             /**< Required; not empty. */
    const char* title;                                  /**< Required; may be empty. */
    const char* sf_symbol_name;                         /**< NULL: none. */
    uint32_t    options;                                /**< NTK_NOTIFICATION_ACTION_*. */
    uint32_t    reserved1;                              /**< Reserved; must be 0. */
} ntk_notification_action;

/** A text input action of a ntk_notification_category. Every element of an array has the same struct_size. */
typedef struct ntk_notification_text_input_action {
    uint32_t    struct_size;                            /**< Set to sizeof(ntk_notification_text_input_action). */
    uint32_t    reserved0;                              /**< Reserved; must be 0. */
    const char* identifier;                             /**< Required; not empty. */
    const char* title;                                  /**< Required; may be empty. */
    const char* button_title;                           /**< Required; may be empty. */
    const char* placeholder;                            /**< Required; may be empty. */
} ntk_notification_text_input_action;

/** A category of actions, for ntk_notification_register_category_async. */
typedef struct ntk_notification_category {
    uint32_t                                  struct_size;  /**< Set to sizeof(ntk_notification_category). */
    uint32_t                                  reserved0;    /**< Reserved; must be 0. */
    uint32_t                                  options;  /**< NTK_NOTIFICATION_CATEGORY_*. */
    uint32_t                                  reserved1;    /**< Reserved; must be 0. */
    const char*                               identifier;   /**< Required; not empty. */
    const ntk_notification_action*            actions;      /**< May be NULL when action_count is 0. */
    size_t                                    action_count; /**< The number of actions; may be 0. */
    const ntk_notification_text_input_action* text_input_actions;  /**< May be NULL when the count is 0. */
    size_t                                    text_input_action_count;  /**< May be 0. */
} ntk_notification_category;
#pragma pack(pop)

/** Installs the library's UNUserNotificationCenter delegate, keeping the current one as the previous
    delegate. Main thread only; synchronous; never waits. Call it before
    application(_:didFinishLaunchingWithOptions:) returns (or inside scene(_:willConnectTo:options:)).
    Off the main thread it returns NTK_NOTIFICATION_ERROR_WRONG_THREAD at once and does nothing. It may be called
    from SwiftUI's App.init. Calling it before UIApplicationMain is outside the contract. */
ntk_notification_error NTK_CALL ntk_notification_install(void);

/** @name Adding notifications
    Without permission (the status is not authorized, provisional or ephemeral) these three end with
    NTK_NOTIFICATION_ERROR_PERMISSION_DENIED and add nothing; a status that changes right after the check is not
    caught. */
/** @{ */
/** Adds a request for content, with the id of content as its identifier. trigger may be NULL: deliver now. */
ntk_notification_error NTK_CALL ntk_notification_show_async(const ntk_notification_content* content,
                                                            const ntk_notification_trigger* trigger,
                                                            ntk_notification_done_fn callback, void* user_data,
                                                            ntk_release_fn release);
/** Replaces the pending request of identifier, or adds it. identifier must not be empty. The id of content is
    not used. trigger may be NULL: deliver now. With NTK_NOTIFICATION_ERROR_ADD_REQUEST_FAILED the previous
    request is already gone. */
ntk_notification_error NTK_CALL ntk_notification_update_async(const char* identifier,
                                                              const ntk_notification_content* content,
                                                              const ntk_notification_trigger* trigger,
                                                              ntk_notification_done_fn callback, void* user_data,
                                                              ntk_release_fn release);
/** Schedules content as the request of identifier. identifier must not be empty. The id of content is not used.
    trigger must not be NULL. */
ntk_notification_error NTK_CALL ntk_notification_schedule_async(const char* identifier,
                                                                const ntk_notification_content* content,
                                                                const ntk_notification_trigger* trigger,
                                                                ntk_notification_done_fn callback, void* user_data,
                                                                ntk_release_fn release);
/** @} */

/** @name Removing notifications
    The order against other calls is decided by these completions: add new requests after the callback. */
/** @{ */
/** Removes the delivered notification of identifier. "" removes nothing and completes with NONE. */
ntk_notification_error NTK_CALL ntk_notification_remove_async(const char* identifier,
                                                              ntk_notification_done_fn callback, void* user_data,
                                                              ntk_release_fn release);
/** Removes every delivered notification. Also removes what the host app delivered itself. */
ntk_notification_error NTK_CALL ntk_notification_remove_all_async(ntk_notification_done_fn callback, void* user_data,
                                                                  ntk_release_fn release);
/** Cancels the pending request of identifier. "" removes nothing and completes with NONE. */
ntk_notification_error NTK_CALL ntk_notification_cancel_scheduled_async(const char* identifier,
                                                                        ntk_notification_done_fn callback,
                                                                        void* user_data, ntk_release_fn release);
/** Cancels every pending request. Also cancels what the host app scheduled itself. */
ntk_notification_error NTK_CALL ntk_notification_cancel_all_scheduled_async(ntk_notification_done_fn callback,
                                                                            void* user_data, ntk_release_fn release);
/** @} */

/** Lists the pending requests. */
ntk_notification_error NTK_CALL ntk_notification_get_scheduled_async(ntk_notification_list_fn callback,
                                                                     void* user_data, ntk_release_fn release);
/** Lists the delivered notifications. */
ntk_notification_error NTK_CALL ntk_notification_get_delivered_async(ntk_notification_list_fn callback,
                                                                     void* user_data, ntk_release_fn release);
/** Asks for permission to notify.
    options: NTK_NOTIFICATION_AUTH_*; an unknown bit is rejected. Put everything in the first request: once the
    status is decided, the system is not asked again and the current status is reported. Needs the foreground
    only while the status is undetermined. */
ntk_notification_error NTK_CALL ntk_notification_request_permission_async(uint32_t options,
                                                                          ntk_notification_permission_fn callback,
                                                                          void* user_data, ntk_release_fn release);
/** Gets the authorization status. */
ntk_notification_error NTK_CALL ntk_notification_get_authorization_status_async(ntk_notification_status_fn callback,
                                                                                void* user_data,
                                                                                ntk_release_fn release);
/** Tells whether notifications are permitted (see ntk_notification_permission_fn). */
ntk_notification_error NTK_CALL ntk_notification_has_permission_async(ntk_notification_permission_fn callback,
                                                                      void* user_data, ntk_release_fn release);
/** Opens the notification settings of the app. Needs the foreground. */
ntk_notification_error NTK_CALL ntk_notification_open_settings_async(ntk_notification_done_fn callback,
                                                                     void* user_data, ntk_release_fn release);
/** Sets the badge count of the app. count must not be negative. */
ntk_notification_error NTK_CALL ntk_notification_set_badge_count_async(int32_t count,
                                                                       ntk_notification_done_fn callback,
                                                                       void* user_data, ntk_release_fn release);
/** Registers category. The callback comes after the write and one read-back; the read-back is not reported. */
ntk_notification_error NTK_CALL ntk_notification_register_category_async(const ntk_notification_category* category,
                                                                         ntk_notification_done_fn callback,
                                                                         void* user_data, ntk_release_fn release);
/** Removes the category of identifier. "" removes nothing and completes with NONE. */
ntk_notification_error NTK_CALL ntk_notification_remove_category_async(const char* identifier,
                                                                       ntk_notification_done_fn callback,
                                                                       void* user_data, ntk_release_fn release);

/** @name Events
    These three return NTK_NOTIFICATION_ERROR_NOT_INITIALIZED until the delegate has been installed. Up to
    32 events wait for the next listener when there is none. */
/** @{ */
/** Registers callback for the user's responses to notifications. out_listener receives the registration. */
ntk_notification_error NTK_CALL ntk_notification_add_interaction_listener(ntk_notification_interaction_fn callback,
                                                                          void* user_data, ntk_release_fn release,
                                                                          ntk_notification_listener** out_listener);
/** Registers callback for the system's requests to show the app's notification settings. out_listener receives
    the registration.
    Comes only for an app that asked for NTK_NOTIFICATION_AUTH_PROVIDES_APP_SETTINGS: in its first request here, or
    natively with requestAuthorization. */
ntk_notification_error NTK_CALL ntk_notification_add_settings_request_listener(
    ntk_notification_settings_request_fn callback, void* user_data, ntk_release_fn release,
    ntk_notification_listener** out_listener);
/** Gets the response that launched the app (see ntk_notification_launch_fn). */
ntk_notification_error NTK_CALL ntk_notification_get_launch_response_async(ntk_notification_launch_fn callback,
                                                                           void* user_data, ntk_release_fn release);
/** @} */
/** Removes the registration. The handle is invalid afterwards; user_data is not until release. Does nothing
    for NULL. */
void NTK_CALL ntk_notification_listener_remove(ntk_notification_listener* listener);

/** @name The content builder
    The builder. Setters copy what they receive. A NULL string restores the default. */
/** @{ */
/** Creates a builder with id and title; the other values start at their defaults. id must not be empty.
    out_content receives the builder. */
ntk_notification_error NTK_CALL ntk_notification_content_create(const char* id, const char* title,
                                                                ntk_notification_content** out_content);
/** Releases the builder. Does nothing for NULL. */
void NTK_CALL ntk_notification_content_free(ntk_notification_content* content);
/** Sets the subtitle. */
ntk_notification_error NTK_CALL ntk_notification_content_set_subtitle(ntk_notification_content* content,
                                                                       const char* subtitle);
/** Sets the body. */
ntk_notification_error NTK_CALL ntk_notification_content_set_body(ntk_notification_content* content, const char* body);
/** Sets the badge: 0 or more; -1 means not set. */
ntk_notification_error NTK_CALL ntk_notification_content_set_badge(ntk_notification_content* content, int32_t badge);
/** Sets the sound. name is required for NTK_NOTIFICATION_SOUND_NAMED and not read otherwise. */
ntk_notification_error NTK_CALL ntk_notification_content_set_sound(ntk_notification_content* content,
                                                                    ntk_notification_sound_kind kind,
                                                                    const char* name);
/** Sets the category identifier. */
ntk_notification_error NTK_CALL ntk_notification_content_set_category(ntk_notification_content* content,
                                                                       const char* identifier);
/** Sets the interruption level. */
ntk_notification_error NTK_CALL ntk_notification_content_set_interruption_level(
    ntk_notification_content* content, ntk_notification_interruption_level level);
/** Sets the thread identifier. */
ntk_notification_error NTK_CALL ntk_notification_content_set_thread(ntk_notification_content* content,
                                                                     const char* identifier);
/** Sets the target content identifier. */
ntk_notification_error NTK_CALL ntk_notification_content_set_target_content(ntk_notification_content* content,
                                                                             const char* identifier);
/** Sets the relevance score: 0.0 to 1.0. */
ntk_notification_error NTK_CALL ntk_notification_content_set_relevance_score(ntk_notification_content* content,
                                                                              double score);
/** Sets the filter criteria. */
ntk_notification_error NTK_CALL ntk_notification_content_set_filter_criteria(ntk_notification_content* content,
                                                                              const char* criteria);
/** Sets the user info: a JSON object without null and without the reserved top-level key
    "com.jonghyunkim.nativetoolkit". */
ntk_notification_error NTK_CALL ntk_notification_content_set_user_info_json(ntk_notification_content* content,
                                                                             const char* json);
/** Adds an attachment.
    file_url is a file:// URL. The system moves the file when the notification is added, so a second use of the
    same builder fails with NTK_NOTIFICATION_ERROR_ATTACHMENT_LOAD_FAILED. */
ntk_notification_error NTK_CALL ntk_notification_content_add_attachment(ntk_notification_content* content,
                                                                         const char* identifier,
                                                                         const char* file_url);
/** @} */

/** @name Readers
    Readers: pointers stay valid until the handle is freed. out_size may be NULL. */
/** @{ */
/** What the user did. -1 for NULL. */
ntk_notification_event_kind NTK_CALL ntk_notification_interaction_kind(const ntk_notification_interaction* event);
/** The identifier of the notification. */
const char* NTK_CALL ntk_notification_interaction_notification_id(const ntk_notification_interaction* event,
                                                               size_t* out_size);
/** The action identifier of the response. */
const char* NTK_CALL ntk_notification_interaction_action_id(const ntk_notification_interaction* event, size_t* out_size);
/** "" when the notification has no category. */
const char* NTK_CALL ntk_notification_interaction_category_id(const ntk_notification_interaction* event,
                                                           size_t* out_size);
/** The reply of a TEXT_INPUT response, or NULL. */
const char* NTK_CALL ntk_notification_interaction_user_text(const ntk_notification_interaction* event, size_t* out_size);
/** A JSON object. Dates are ISO 8601 strings, data is Base64, values that are not finite are null. */
const char* NTK_CALL ntk_notification_interaction_user_info_json(const ntk_notification_interaction* event,
                                                              size_t* out_size);
/** Nonzero when this response launched the app. A background action followed at once by bringing the app forward
    can also count. */
int32_t     NTK_CALL ntk_notification_interaction_is_launch(const ntk_notification_interaction* event);
/** Releases the event. Does nothing for NULL. */
void        NTK_CALL ntk_notification_interaction_free(ntk_notification_interaction* event);

/** The identifier of the notification the request came from; NULL when the request came from the Settings app. */
const char* NTK_CALL ntk_notification_settings_request_notification_id(
    const ntk_notification_settings_request* request, size_t* out_size);
/** Releases the request. Does nothing for NULL. */
void        NTK_CALL ntk_notification_settings_request_free(ntk_notification_settings_request* request);

/** The number of notifications. */
size_t      NTK_CALL ntk_notification_list_count(const ntk_notification_list* list);
/** The identifier of the notification at index. */
const char* NTK_CALL ntk_notification_list_identifier_at(const ntk_notification_list* list, size_t index,
                                                         size_t* out_size);
/** May be empty. NULL for a NULL list, a handle of another type or an index out of range; then out_size is 0. */
const char* NTK_CALL ntk_notification_list_title_at(const ntk_notification_list* list, size_t index, size_t* out_size);
/** NULL when the notification has none, and for a NULL list, a handle of another type or an index out of range;
    then out_size is 0. */
const char* NTK_CALL ntk_notification_list_subtitle_at(const ntk_notification_list* list, size_t index,
                                                       size_t* out_size);
/** NULL when the notification has none, and for a NULL list, a handle of another type or an index out of range;
    then out_size is 0. */
const char* NTK_CALL ntk_notification_list_body_at(const ntk_notification_list* list, size_t index, size_t* out_size);
/** May be empty. NULL for a NULL list, a handle of another type or an index out of range; then out_size is 0. */
const char* NTK_CALL ntk_notification_list_category_at(const ntk_notification_list* list, size_t index,
                                                       size_t* out_size);
/** Unix milliseconds of a delivered notification; 0 for a scheduled one or an index out of range. */
int64_t     NTK_CALL ntk_notification_list_date_millis_at(const ntk_notification_list* list, size_t index);
/** Releases the list. Does nothing for NULL. */
void        NTK_CALL ntk_notification_list_free(ntk_notification_list* list);
/** @} */

#ifdef __cplusplus
}
#endif
#endif
