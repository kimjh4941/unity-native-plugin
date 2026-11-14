/**
 * @file Common.h
 * @brief What every part of the NativeToolkit C ABI shares: the version, the last system code
 *        (always 0 on iOS), and the three output handles. The declarations are the same as the
 *        Android and Windows Common.h (scripts/check_c_abi_contract_ios.py compares them).
 *
 * Rules for the whole C ABI (C ABI design part 1, chapter 1): every function may be called from any
 * thread, except ntk_notification_install (main thread only), and none waits for the main thread;
 * completions, events and accepted releases arrive on the main thread; strings are NUL-terminated
 * UTF-8; this header is C99, includes only <stddef.h> and <stdint.h>, and is ASCII only. The
 * declarations are those of Appendix A of the C ABI design part 2, which
 * scripts/check_c_abi_contract_ios.py compares with this file.
 */
#ifndef NATIVETOOLKITC_COMMON_H
#define NATIVETOOLKITC_COMMON_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_MSC_VER)
#define NTK_CALL __cdecl
#else
#define NTK_CALL
#endif

#define NTK_VERSION_MAJOR 2
#define NTK_VERSION_MINOR 0
#define NTK_VERSION_PATCH 0
/** (MAJOR << 16) | (MINOR << 8) | PATCH, written as a literal so that every binding generator can read it. */
#define NTK_VERSION 0x020000

/** Tells the caller that the library is done with a user_data pointer. Called exactly once. */
typedef void (NTK_CALL *ntk_release_fn)(void* user_data);

/** The version of the library, in the form of NTK_VERSION. */
uint32_t NTK_CALL ntk_version(void);

/** The raw value the OS reported for the last failure on this thread. Always 0 on iOS. */
uint32_t NTK_CALL ntk_last_system_code(void);

/** A string the library returned. */
typedef struct ntk_string ntk_string;
/** The text, NUL-terminated. Valid until the handle is freed. */
const char* NTK_CALL ntk_string_data(const ntk_string* s);
/** The length in bytes, without the terminator. */
size_t      NTK_CALL ntk_string_size(const ntk_string* s);
/** Releases the string. Does nothing for NULL. */
void        NTK_CALL ntk_string_free(ntk_string* s);

/** Bytes the library returned. */
typedef struct ntk_bytes ntk_bytes;
/** The bytes; never NULL for a valid handle, even when the size is 0. */
const uint8_t* NTK_CALL ntk_bytes_data(const ntk_bytes* b);
/** The number of bytes. */
size_t         NTK_CALL ntk_bytes_size(const ntk_bytes* b);
/** Releases the bytes. Does nothing for NULL. */
void           NTK_CALL ntk_bytes_free(ntk_bytes* b);

/** A list of strings the library returned. */
typedef struct ntk_string_list ntk_string_list;
/** The number of strings. */
size_t      NTK_CALL ntk_string_list_count(const ntk_string_list* list);
/** The string at index, or NULL when index is out of range. out_size may be NULL. */
const char* NTK_CALL ntk_string_list_at(const ntk_string_list* list, size_t index, size_t* out_size);
/** Releases the list. Does nothing for NULL. */
void        NTK_CALL ntk_string_list_free(ntk_string_list* list);

#ifdef __cplusplus
}
#endif
#endif
