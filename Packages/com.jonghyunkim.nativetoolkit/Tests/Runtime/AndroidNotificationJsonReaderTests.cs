#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using Reader = JonghyunKim.NativeToolkit.Runtime.Android.Notification.AndroidNotificationJsonReader;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidNotificationJsonReader"/>: the syntax it takes and refuses,
    /// string escapes, numbers, the nesting limit, a key written twice, and an absent key told apart
    /// from a null one (artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md, 7.1).
    /// </summary>
    public sealed class AndroidNotificationJsonReaderTests
    {
        [TestCase("{}")]
        [TestCase(" { \"a\" : [ 1 , true , null , \"x\" , { } ] } ")]
        [TestCase("[]")]
        [TestCase("\"text\"")]
        [TestCase("-0")]
        [TestCase("1.5e3")]
        public void Parse_TakesStrictJson(string json) => Assert.IsNotNull(Reader.Parse(json), json);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{\"id\":")]
        [TestCase("{a:1}")]
        [TestCase("{'a':1}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1,]")]
        [TestCase("{\"a\":1} x")]
        [TestCase("{\"a\"=1}")]
        [TestCase("{\"a\"=>1}")]
        [TestCase("{\"a\":1 /* c */}")]
        [TestCase("{\"a\":NaN}")]
        [TestCase("{\"a\":Infinity}")]
        [TestCase("{\"a\":01}")]
        [TestCase("{\"a\":1.}")]
        [TestCase("{\"a\":1e400}")]
        [TestCase("{\"a\":1e-400}")]
        [TestCase("{\"a\":\"\\x\"}")]
        [TestCase("{\"a\":\"\\u12\"}")]
        [TestCase("{\"a\":\"tab\there\"}")]
        [TestCase("{\"a\":\"open}")]
        [TestCase("tru")]
        public void Parse_RefusesWhatIsNotStrictJson(string? json) => Assert.IsNull(Reader.Parse(json), json ?? "null");

        [Test]
        public void Parse_UnescapesStrings()
        {
            AndroidJsonValue value = Reader.Parse("{\"s\":\"q\\\" b\\\\ s\\/ n\\n t\\t u\\u65e5\\ud83d\\ude00\"}")!;
            Assert.IsTrue(value.TryGetMember("s", out AndroidJsonValue s));
            Assert.IsTrue(s.TryGetString(out string text));
            Assert.AreEqual("q\" b\\ s/ n\n t\t u日😀", text);
        }

        [Test]
        public void Parse_ReadsNumbersAsDoubles()
        {
            AndroidJsonValue value = Reader.Parse("{\"a\":-12.75,\"b\":2147483648,\"c\":0e5}")!;
            Assert.AreEqual(-12.75, Number(value, "a"));
            Assert.AreEqual(2147483648d, Number(value, "b"));
            Assert.AreEqual(0d, Number(value, "c"));
        }

        [Test]
        public void Parse_NestsUpToTheLimit()
        {
            string Nested(int depth) => new string('[', depth) + new string(']', depth);
            Assert.IsNotNull(Reader.Parse(Nested(Reader.MaxContainers)), "at the limit");
            Assert.IsNull(Reader.Parse(Nested(Reader.MaxContainers + 1)), "past the limit");
        }

        [Test]
        public void Parse_KeyWrittenTwice_KeepsTheLaterValue()
        {
            AndroidJsonValue value = Reader.Parse("{\"a\":1,\"b\":2,\"a\":3}")!;
            Assert.AreEqual(3d, Number(value, "a"), "libcore's org.json keeps the later value too");
            CollectionAssert.AreEqual(new[] { "a", "b" }, value.Members.Select(m => m.Key), "the first occurrence keeps its place");
        }

        private static double? Number(AndroidJsonValue value, string key) =>
            value.TryGetMember(key, out AndroidJsonValue member) && member.TryGetNumber(out double number) ? number : null;

        [Test]
        public void TryGetMember_TellsAnAbsentKeyFromANullOne()
        {
            AndroidJsonValue value = Reader.Parse("{\"n\":null}")!;
            Assert.IsTrue(value.TryGetMember("n", out AndroidJsonValue n), "present");
            Assert.IsTrue(n.IsNull);
            Assert.IsFalse(value.TryGetMember("x", out _), "absent");
        }

        [Test]
        public void TypedReads_FailOnAnotherType()
        {
            AndroidJsonValue value = Reader.Parse("{\"s\":\"1\",\"n\":1,\"b\":true}")!;
            value.TryGetMember("s", out AndroidJsonValue s);
            value.TryGetMember("n", out AndroidJsonValue n);
            value.TryGetMember("b", out AndroidJsonValue b);
            Assert.IsFalse(s.TryGetNumber(out _), "a numeric string is not a number");
            Assert.IsFalse(n.TryGetString(out _), "a number is not a string");
            Assert.IsFalse(s.TryGetBool(out _), "a string is not a bool");
            Assert.IsTrue(b.TryGetBool(out bool flag) && flag);
            Assert.AreEqual(0, value.Elements.Count, "an object has no elements");
            Assert.AreEqual(0, s.Members.Count, "a string has no members");
        }
    }
}
#endif
