#nullable enable

#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The lines between the iOS player tests and the UI driver (artifact/topics/ios-c-abi-2, UI driver design
    /// v2, chapter 3). Android's driver had no test at this layer; the iOS one builds its lines in a class
    /// without Unity's API so that the Editor can check them.
    /// </summary>
    public sealed class IosUiDriverProtocolTests
    {
        private const string Run = "0123456789abcdef0123456789abcdef";

        [Test]
        public void Hello_And_Bye()
        {
            Assert.AreEqual($"HELLO {Run} com.example.app", IosUiDriverProtocol.Hello(Run, "com.example.app"));
            Assert.AreEqual($"BYE {Run}", IosUiDriverProtocol.Bye(Run));
            Assert.IsTrue(IosUiDriverProtocol.IsReady($"READY {Run}", Run));
            Assert.IsFalse(IosUiDriverProtocol.IsReady($"READY {Run} ", Run));
            Assert.IsFalse(IosUiDriverProtocol.IsReady("READY 0123456789abcdef0123456789abcdee", Run));
        }

        [TestCase("")]
        [TestCase("0123456789ABCDEF0123456789ABCDEF")]
        [TestCase("0123456789abcdef")]
        [TestCase("0123456789abcdef0123456789abcdef0")]
        [TestCase("0123456789abcdef0123456789abcdef\n")]
        public void Hello_RejectsABadRun(string run)
        {
            Assert.Throws<ArgumentException>(() => IosUiDriverProtocol.Hello(run, "com.example.app"));
            Assert.Throws<ArgumentException>(() => IosUiDriverProtocol.Bye(run));
        }

        [TestCase("")]
        [TestCase("com.example app")]
        [TestCase("com.example\napp")]
        public void Hello_RejectsABadBundleId(string bundleId)
        {
            Assert.Throws<ArgumentException>(() => IosUiDriverProtocol.Hello(Run, bundleId));
        }

        [Test]
        public void NewRun_IsAValidRun()
        {
            Assert.DoesNotThrow(() => IosUiDriverProtocol.Bye(IosUiDriverProtocol.NewRun()));
        }

        [Test]
        public void Request_JoinsTheCommandAndArguments()
        {
            string ok = IosUiDriverProtocol.Base64("OK");
            Assert.AreEqual("T0s=", ok);
            Assert.AreEqual("7 alert-tap T0s=", IosUiDriverProtocol.Request(7, "alert-tap", new[] { ok }));
            Assert.AreEqual("8 front", IosUiDriverProtocol.Request(8, "front", Array.Empty<string>()));
            Assert.AreEqual("9 alert-type text 1 YWJj", IosUiDriverProtocol.Request(9, "alert-type", new[] { "text", "1", IosUiDriverProtocol.Base64("abc") }));
            Assert.AreEqual("44GC", IosUiDriverProtocol.Base64("あ"), "UTF-8");
        }

        [TestCase(0, "front")]
        [TestCase(1, "Front")]
        [TestCase(1, "alert tap")]
        [TestCase(1, "-front")]
        [TestCase(1, "")]
        [TestCase(1, "front\n")]
        public void Request_RejectsABadNumberOrCommand(int number, string command)
        {
            Assert.That(() => IosUiDriverProtocol.Request(number, command, Array.Empty<string>()), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("")]
        [TestCase("OK Cancel")]
        [TestCase("a\nb")]
        [TestCase("了解")]
        [TestCase("T0s=\n")]
        public void Request_RejectsAnArgumentThatIsNotRawOrBase64(string arg)
        {
            Assert.Throws<ArgumentException>(() => IosUiDriverProtocol.Request(1, "alert-tap", new[] { arg }));
        }

        [Test]
        public void Request_IsLimitedTo4096Bytes()
        {
            string fits = new('a', IosUiDriverProtocol.MaxLineBytes - "1 alert-tap ".Length);
            Assert.DoesNotThrow(() => IosUiDriverProtocol.Request(1, "alert-tap", new[] { fits }));
            Assert.Throws<ArgumentException>(() => IosUiDriverProtocol.Request(1, "alert-tap", new[] { fits + "a" }));
        }

        [TestCase("3 ok", 3, "ok")]
        [TestCase("3 ok opened", 3, "ok opened")]
        [TestCase("12 fail not-found", 12, "fail not-found")]
        [TestCase("12 fail device-only-refused", 12, "fail device-only-refused")]
        public void TryParseAnswer_ReadsTheThreeForms(string line, int number, string answer)
        {
            Assert.IsTrue(IosUiDriverProtocol.TryParseAnswer(line, out int n, out string a));
            Assert.AreEqual(number, n);
            Assert.AreEqual(answer, a);
        }

        [TestCase("")]
        [TestCase("ok")]
        [TestCase("3")]
        [TestCase("3 ")]
        [TestCase("0 ok")]
        [TestCase("-3 ok")]
        [TestCase("+3 ok")]
        [TestCase("3 ok ")]
        [TestCase("3 okay")]
        [TestCase("3 fail")]
        [TestCase("3 fail ")]
        [TestCase("3 fail Not Found")]
        [TestCase("3 fail not_found")]
        [TestCase("3 error x")]
        [TestCase("3 fail timeout\n")]
        [TestCase("BYE ok")]
        public void TryParseAnswer_RejectsAnythingElse(string line)
        {
            Assert.IsFalse(IosUiDriverProtocol.TryParseAnswer(line, out int n, out string a));
            Assert.AreEqual(0, n);
            Assert.AreEqual("", a);
        }

        [Test]
        public void SuccessAndValue()
        {
            Assert.IsTrue(IosUiDriverProtocol.IsSuccess("ok"));
            Assert.IsTrue(IosUiDriverProtocol.IsSuccess("ok opened"));
            Assert.IsFalse(IosUiDriverProtocol.IsSuccess("okay"));
            Assert.IsFalse(IosUiDriverProtocol.IsSuccess("fail timeout"));
            Assert.AreEqual("opened", IosUiDriverProtocol.ValueOf("ok opened"));
            Assert.AreEqual("", IosUiDriverProtocol.ValueOf("ok"));
            Assert.AreEqual("", IosUiDriverProtocol.ValueOf("fail timeout"));
        }

        [Test]
        public void AnswerTimeout_FollowsTheCommand()
        {
            Assert.AreEqual(30f, IosUiDriverProtocol.AnswerTimeoutFor("alert-tap", new[] { "T0s=" }));
            Assert.AreEqual(120f, IosUiDriverProtocol.AnswerTimeoutFor("permission-answer", new[] { "allow" }));
            Assert.AreEqual(32.5f, IosUiDriverProtocol.AnswerTimeoutFor("alert-absent", new[] { "2500", "T0s=" }));
            Assert.AreEqual(125f, IosUiDriverProtocol.AnswerTimeoutFor("sheet-absent", new[] { "5000" }));
        }
    }
}
#endif
