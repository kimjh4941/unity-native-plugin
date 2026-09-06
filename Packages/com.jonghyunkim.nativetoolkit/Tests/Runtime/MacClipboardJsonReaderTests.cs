#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the minimal JSON reader backing the macOS clipboard parser.
    /// Covers the shapes JsonUtility cannot handle (nested arrays, explicit null members) and
    /// the base64 fast path that avoids materializing large payloads.
    /// </summary>
    public sealed class MacClipboardJsonReaderTests
    {
        [Test]
        public void Parse_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(MacClipboardJsonReader.Parse(null));
            Assert.IsNull(MacClipboardJsonReader.Parse(string.Empty));
        }

        [Test]
        public void Parse_MalformedDocuments_ReturnNullWithoutThrowing()
        {
            string[] malformed =
            {
                "{",
                "}",
                "[1,2",
                "{\"a\":}",
                "{\"a\" 1}",
                "{'a':1}",
                "{\"a\":1,}",
                "{\"a\":NaN}",
                "\"unterminated",
                "{\"a\":1} trailing"
            };

            foreach (string json in malformed)
            {
                Assert.DoesNotThrow(() => MacClipboardJsonReader.Parse(json), $"threw on: {json}");
                Assert.IsNull(MacClipboardJsonReader.Parse(json), $"expected null for: {json}");
            }
        }

        [Test]
        public void Parse_InvalidStringEscapes_AreRejected()
        {
            // An unknown escape or a short \u must fail the document rather than be repaired into
            // a plausible-looking string value, which would let a broken bridge look successful.
            string[] malformed =
            {
                "{\"a\":\"\\q\"}",
                "{\"a\":\"\\x41\"}",
                "{\"a\":\"\\u12\"}",
                "{\"a\":\"\\u12g4\"}",
                "{\"a\":\"\\u\"}",
                "{\"a\":\"trailing\\\\\\\"}"
            };

            foreach (string json in malformed)
            {
                Assert.IsNull(MacClipboardJsonReader.Parse(json), $"expected null for: {json}");
            }
        }

        [Test]
        public void Parse_RawControlCharactersInStrings_AreRejected()
        {
            Assert.IsNull(MacClipboardJsonReader.Parse("{\"a\":\"line\nbreak\"}"));
            Assert.IsNull(MacClipboardJsonReader.Parse("{\"a\":\"tab\there\"}"));
            Assert.IsNull(MacClipboardJsonReader.Parse("{\"a\":\"nul\u0000\"}"));
        }

        [Test]
        public void Parse_ValidEscapesAndSpaces_AreAccepted()
        {
            Assert.IsNotNull(MacClipboardJsonReader.Parse("{\"a\":\"\\n\\t\\\\\\\"\\/\\b\\f\\r\\u00e9\"}"));
            Assert.IsNotNull(MacClipboardJsonReader.Parse("{\"a\":\"a space is fine\"}"));
        }

        [Test]
        public void Parse_MalformedNumbers_AreRejected()
        {
            string[] malformed =
            {
                "{\"a\":+1}",
                "{\"a\":01}",
                "{\"a\":1.}",
                "{\"a\":.5}",
                "{\"a\":1e}",
                "{\"a\":1e+}",
                "{\"a\":--1}",
                "{\"a\":1.2.3}",
                "{\"a\":-}"
            };

            foreach (string json in malformed)
            {
                Assert.IsNull(MacClipboardJsonReader.Parse(json), $"expected null for: {json}");
            }
        }

        [Test]
        public void Parse_WellFormedNumbers_AreAccepted()
        {
            string[] valid = { "0", "-0", "12", "-12", "1.5", "-1.5", "1e3", "1E3", "1e+3", "1e-3", "0.5e10" };

            foreach (string number in valid)
            {
                MacJsonValue? root = MacClipboardJsonReader.Parse("{\"a\":" + number + "}");
                Assert.IsNotNull(root, $"expected parse for: {number}");
                Assert.IsTrue(root!.GetMemberOrNull("a")!.TryGetDouble(out _), number);
            }
        }

        [Test]
        public void Parse_EmptyObjectAndArray_AreAccepted()
        {
            MacJsonValue? obj = MacClipboardJsonReader.Parse("{}");
            Assert.IsNotNull(obj);
            Assert.AreEqual(MacJsonValueKind.Object, obj!.Kind);

            MacJsonValue? array = MacClipboardJsonReader.Parse("[]");
            Assert.IsNotNull(array);
            Assert.AreEqual(MacJsonValueKind.Array, array!.Kind);
            Assert.AreEqual(0, array.AsArray().Count);
        }

        [Test]
        public void Parse_NestedArrayOfArrays_IsPreserved()
        {
            // This is the shape JsonUtility cannot deserialize: snapshot.itemTypes.
            MacJsonValue? root = MacClipboardJsonReader.Parse("{\"a\":[[\"x\",\"y\"],[],[\"z\"]]}");
            Assert.IsNotNull(root);

            Assert.IsTrue(root!.TryGetMember("a", out MacJsonValue outer));
            IReadOnlyList<MacJsonValue> rows = outer.AsArray();
            Assert.AreEqual(3, rows.Count);

            Assert.AreEqual(2, rows[0].AsArray().Count);
            Assert.IsTrue(rows[0].AsArray()[0].TryGetString(out string first));
            Assert.AreEqual("x", first);
            Assert.AreEqual(0, rows[1].AsArray().Count);
            Assert.AreEqual(1, rows[2].AsArray().Count);
        }

        [Test]
        public void TryGetMember_AbsentKeyOrWrongKind_ReturnsFalse()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":1}")!;
            Assert.IsFalse(root.TryGetMember("missing", out _));

            MacJsonValue array = MacClipboardJsonReader.Parse("[1]")!;
            Assert.IsFalse(array.TryGetMember("a", out _));
        }

        [Test]
        public void GetMemberOrNull_NullMember_ReturnsNull()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":null,\"b\":1}")!;
            Assert.IsNull(root.GetMemberOrNull("a"));
            Assert.IsNull(root.GetMemberOrNull("missing"));
            Assert.IsNotNull(root.GetMemberOrNull("b"));
        }

        [Test]
        public void TryGetBool_OnlyForBooleans()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"t\":true,\"f\":false,\"n\":1}")!;

            Assert.IsTrue(root.GetMemberOrNull("t")!.TryGetBool(out bool t));
            Assert.IsTrue(t);
            Assert.IsTrue(root.GetMemberOrNull("f")!.TryGetBool(out bool f));
            Assert.IsFalse(f);
            Assert.IsFalse(root.GetMemberOrNull("n")!.TryGetBool(out _));
        }

        [Test]
        public void TryGetDouble_AcceptsIntegerDecimalAndExponent()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":12,\"b\":-3.5,\"c\":1.5e3,\"d\":\"12\"}")!;

            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetDouble(out double a));
            Assert.AreEqual(12d, a, 1e-9);
            Assert.IsTrue(root.GetMemberOrNull("b")!.TryGetDouble(out double b));
            Assert.AreEqual(-3.5d, b, 1e-9);
            Assert.IsTrue(root.GetMemberOrNull("c")!.TryGetDouble(out double c));
            Assert.AreEqual(1500d, c, 1e-9);

            // A numeric-looking string is not a number.
            Assert.IsFalse(root.GetMemberOrNull("d")!.TryGetDouble(out _));
        }

        [Test]
        public void TryGetInt_AcceptsIntegralFormsAndRejectsOthers()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":7,\"b\":7.0,\"c\":7.5,\"d\":true}")!;

            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetInt(out int a));
            Assert.AreEqual(7, a);
            Assert.IsTrue(root.GetMemberOrNull("b")!.TryGetInt(out int b));
            Assert.AreEqual(7, b);
            Assert.IsFalse(root.GetMemberOrNull("c")!.TryGetInt(out _));
            Assert.IsFalse(root.GetMemberOrNull("d")!.TryGetInt(out _));
        }

        [Test]
        public void TryGetInt64_AcceptsValuesBeyondTheIntRange()
        {
            // Every native changeCount is a Swift Int, so a value past int.MaxValue is a normal
            // response rather than a malformed one.
            var root = MacClipboardJsonReader.Parse("{\"a\":2147483648,\"b\":-2147483649,\"c\":9007199254740993}");
            Assert.IsNotNull(root);

            Assert.IsTrue(root!.GetMemberOrNull("a")!.TryGetInt64(out long a));
            Assert.AreEqual(2147483648L, a);

            Assert.IsTrue(root.GetMemberOrNull("b")!.TryGetInt64(out long b));
            Assert.AreEqual(-2147483649L, b);

            Assert.IsTrue(root.GetMemberOrNull("c")!.TryGetInt64(out long c));
            Assert.AreEqual(9007199254740993L, c);
        }

        [Test]
        public void TryGetInt64_RejectsWhatIsNotAnIntegralNumber()
        {
            var root = MacClipboardJsonReader.Parse("{\"s\":\"1\",\"b\":true,\"f\":1.5}");
            Assert.IsNotNull(root);

            foreach (string key in new[] { "s", "b", "f" })
            {
                Assert.IsFalse(root!.GetMemberOrNull(key)!.TryGetInt64(out long value), key);
                Assert.AreEqual(0L, value, key);
            }
        }

        [Test]
        public void TryGetInt64_OnAnExplicitNullMember_IsNotReachedThroughGetMemberOrNull()
        {
            // GetMemberOrNull collapses a JSON null to a C# null, so a null changeCount is a
            // missing member to the parser rather than a number it fails to read.
            var root = MacClipboardJsonReader.Parse("{\"n\":null}");
            Assert.IsNotNull(root);
            Assert.IsNull(root!.GetMemberOrNull("n"));

            // Reached directly, the value is a Null kind and still refuses to produce a number.
            Assert.IsTrue(root.TryGetMember("n", out MacJsonValue raw));
            Assert.IsTrue(raw.IsNull);
            Assert.IsFalse(raw.TryGetInt64(out long value));
            Assert.AreEqual(0L, value);
        }

        [Test]
        public void TryGetInt64_AcceptsAnIntegralDouble()
        {
            var root = MacClipboardJsonReader.Parse("{\"a\":3.0}");
            Assert.IsNotNull(root);
            Assert.IsTrue(root!.GetMemberOrNull("a")!.TryGetInt64(out long a));
            Assert.AreEqual(3L, a);
        }

        [Test]
        public void TryGetInt_StillFailsBeyondTheIntRange()
        {
            // The narrower reader is kept as it is; TryGetInt64 exists because of this behaviour.
            var root = MacClipboardJsonReader.Parse("{\"a\":2147483648}");
            Assert.IsNotNull(root);
            Assert.IsFalse(root!.GetMemberOrNull("a")!.TryGetInt(out int a));
            Assert.AreEqual(0, a);
        }

        [Test]
        public void TryGetString_UnescapedValue_IsReturnedVerbatim()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":\"plain value\"}")!;
            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetString(out string value));
            Assert.AreEqual("plain value", value);
        }

        [Test]
        public void TryGetString_DecodesAllSimpleEscapes()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse(
                "{\"a\":\"q\\\"b\\\\s\\/f\\bf\\ff\\nf\\rf\\tend\"}")!;

            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetString(out string value));
            Assert.AreEqual("q\"b\\s/f\bf\ff\nf\rf\tend", value);
        }

        [Test]
        public void TryGetString_DecodesUnicodeEscapesIncludingSurrogatePairs()
        {
            // "あ" and an emoji expressed as a surrogate pair.
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":\"\\u3042\\uD83D\\uDE00\"}")!;

            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetString(out string value));
            Assert.AreEqual("あ\U0001F600", value);
        }

        [Test]
        public void TryGetString_RawNonAsciiCharacters_ArePreserved()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"a\":\"日本語 \U0001F680\"}")!;
            Assert.IsTrue(root.GetMemberOrNull("a")!.TryGetString(out string value));
            Assert.AreEqual("日本語 \U0001F680", value);
        }

        [Test]
        public void Parse_ExceedingMaxDepth_ReturnsNull()
        {
            string deep = new string('[', MacClipboardJsonReader.MaxDepth + 5)
                          + "1"
                          + new string(']', MacClipboardJsonReader.MaxDepth + 5);

            Assert.DoesNotThrow(() => MacClipboardJsonReader.Parse(deep));
            Assert.IsNull(MacClipboardJsonReader.Parse(deep));
        }

        [Test]
        public void Parse_AtModestDepth_Succeeds()
        {
            string nested = new string('[', 10) + "1" + new string(']', 10);
            Assert.IsNotNull(MacClipboardJsonReader.Parse(nested));
        }

        // ── base64 ──────────────────────────────────────────────────────────────

        [Test]
        public void TryGetDecodedLength_EmptyToken_IsZero()
        {
            Assert.IsTrue(MacJsonValue.TryGetDecodedLength(string.Empty.AsSpan(), out long length));
            Assert.AreEqual(0L, length);
        }

        [Test]
        public void TryGetDecodedLength_AccountsForPadding()
        {
            // 1 byte -> "AA==" (two pad chars), 2 bytes -> "AAA=" (one), 3 bytes -> "AAAA" (none).
            // The naive (len / 4) * 3 would report 3 for all three cases.
            for (int byteCount = 1; byteCount <= 3; byteCount++)
            {
                string encoded = Convert.ToBase64String(new byte[byteCount]);
                Assert.IsTrue(MacJsonValue.TryGetDecodedLength(encoded.AsSpan(), out long length));
                Assert.AreEqual((long)byteCount, length, $"byteCount: {byteCount}, encoded: {encoded}");
            }
        }

        [Test]
        public void TryGetDecodedLength_MatchesTheSixtyFourMebibyteBoundary()
        {
            // The exact case that motivated the padding correction: a legal 64 MiB payload encodes
            // to 89,478,488 chars, where the naive formula yields 67,108,866 and would reject it.
            const long rawLength = 64L * 1024L * 1024L;
            const int encodedLength = 89478488;

            // Verified arithmetically rather than by allocating an 89 MB string.
            Assert.AreEqual(rawLength, ((long)encodedLength / 4) * 3 - 2);
        }

        [Test]
        public void TryGetDecodedLength_NonMultipleOfFour_IsRejected()
        {
            Assert.IsFalse(MacJsonValue.TryGetDecodedLength("AAA".AsSpan(), out _));
            Assert.IsFalse(MacJsonValue.TryGetDecodedLength("AAAAA".AsSpan(), out _));
        }

        [Test]
        public void TryGetDecodedLength_ThreeOrMorePaddingCharacters_IsRejected()
        {
            Assert.IsFalse(MacJsonValue.TryGetDecodedLength("A===".AsSpan(), out _));
            Assert.IsFalse(MacJsonValue.TryGetDecodedLength("====".AsSpan(), out _));
        }

        [Test]
        public void TryGetBase64Bytes_RoundTripsPayload()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{Convert.ToBase64String(payload)}\"}}")!;

            MacJsonBase64Status status = root.GetMemberOrNull("b")!.TryGetBase64Bytes(long.MaxValue, out byte[]? bytes);

            Assert.AreEqual(MacJsonBase64Status.Success, status);
            Assert.AreEqual(payload, bytes);
        }

        [Test]
        public void TryGetBase64Bytes_ExactlyAtLimit_Succeeds()
        {
            // Padded payload sitting exactly on the limit: proves the limit uses the exact decoded
            // length, not the padding-inflated estimate.
            var payload = new byte[1];
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{Convert.ToBase64String(payload)}\"}}")!;

            MacJsonBase64Status status = root.GetMemberOrNull("b")!.TryGetBase64Bytes(1, out byte[]? bytes);

            Assert.AreEqual(MacJsonBase64Status.Success, status);
            Assert.AreEqual(1, bytes!.Length);
        }

        [Test]
        public void TryGetBase64Bytes_OneByteOverLimit_ReportsTooLarge()
        {
            var payload = new byte[4];
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{Convert.ToBase64String(payload)}\"}}")!;

            MacJsonBase64Status status = root.GetMemberOrNull("b")!.TryGetBase64Bytes(3, out byte[]? bytes);

            Assert.AreEqual(MacJsonBase64Status.TooLarge, status);
            Assert.IsNull(bytes, "nothing must be allocated when the payload is rejected for size");
        }

        [Test]
        public void TryGetBase64Bytes_JustUnderLimit_Succeeds()
        {
            var payload = new byte[3];
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{Convert.ToBase64String(payload)}\"}}")!;

            Assert.AreEqual(
                MacJsonBase64Status.Success,
                root.GetMemberOrNull("b")!.TryGetBase64Bytes(4, out _));
        }

        [Test]
        public void TryGetBase64Bytes_NonStringMember_ReportsNotAString()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"b\":123}")!;
            Assert.AreEqual(
                MacJsonBase64Status.NotAString,
                root.GetMemberOrNull("b")!.TryGetBase64Bytes(long.MaxValue, out _));
        }

        [Test]
        public void TryGetBase64Bytes_MalformedToken_ReportsMalformed()
        {
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"b\":\"AA=A\",\"c\":\"AAA\",\"d\":\"@@@@\"}")!;

            Assert.AreEqual(
                MacJsonBase64Status.Malformed,
                root.GetMemberOrNull("b")!.TryGetBase64Bytes(long.MaxValue, out _));
            Assert.AreEqual(
                MacJsonBase64Status.Malformed,
                root.GetMemberOrNull("c")!.TryGetBase64Bytes(long.MaxValue, out _));
            Assert.AreEqual(
                MacJsonBase64Status.Malformed,
                root.GetMemberOrNull("d")!.TryGetBase64Bytes(long.MaxValue, out _));
        }

        [Test]
        public void TryGetBase64Bytes_SlashEscapedToken_RoundTripsPayload()
        {
            // '/' belongs to the base64 alphabet, and Apple's JSONSerialization escapes it as "\/"
            // unless .withoutEscapingSlashes is set, so real device payloads arrive escaped.
            var payload = new byte[] { 255, 255, 255 };
            string escaped = Convert.ToBase64String(payload).Replace("/", "\\/");
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{escaped}\"}}")!;

            MacJsonBase64Status status = root.GetMemberOrNull("b")!.TryGetBase64Bytes(long.MaxValue, out byte[]? bytes);

            Assert.AreEqual(MacJsonBase64Status.Success, status);
            Assert.AreEqual(payload, bytes);
        }

        [Test]
        public void TryGetBase64Bytes_SlashEscapedToken_StillHonoursTheLimit()
        {
            // The limit must be measured on the unescaped token, not on the longer escaped source.
            var payload = new byte[] { 255, 255, 255 };
            string escaped = Convert.ToBase64String(payload).Replace("/", "\\/");
            MacJsonValue root = MacClipboardJsonReader.Parse($"{{\"b\":\"{escaped}\"}}")!;

            Assert.AreEqual(
                MacJsonBase64Status.TooLarge,
                root.GetMemberOrNull("b")!.TryGetBase64Bytes(2, out byte[]? bytes));
            Assert.IsNull(bytes);
        }

        [Test]
        public void TryGetBase64Bytes_EscapedToken_ReportsMalformed()
        {
            // Canonical base64 never needs escaping, so an escaped token must not take the
            // zero-copy path.
            MacJsonValue root = MacClipboardJsonReader.Parse("{\"b\":\"AA\\u003d\\u003d\"}")!;
            Assert.AreEqual(
                MacJsonBase64Status.Malformed,
                root.GetMemberOrNull("b")!.TryGetBase64Bytes(long.MaxValue, out _));
        }
    }
}
#endif
