#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Editor.iOS;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="IosMinimumVersionCheck"/>: which minimum iOS versions stop an iOS
    /// build (3-S design v3, 1.5). Compared as versions, not as text ("9.0" is below "18.0").
    /// </summary>
    public sealed class IosMinimumVersionCheckTests
    {
        [TestCase("18.0")]
        [TestCase("18")]
        [TestCase("18.4")]
        [TestCase("26.0")]
        [TestCase(" 18.0 ")]
        public void Check_EighteenOrLater_Passes(string minimum)
        {
            Assert.That(IosMinimumVersionCheck.Check(minimum), Is.Null);
        }

        [TestCase("17.6.1")]
        [TestCase("17")]
        [TestCase("15.0")]
        [TestCase("9.0")]
        public void Check_BelowEighteen_FailsAndSaysWhatToChange(string minimum)
        {
            string? error = IosMinimumVersionCheck.Check(minimum);

            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("18.0"));
            Assert.That(error, Does.Contain("Target minimum iOS Version"));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("eighteen")]
        public void Check_Unreadable_Fails(string? minimum)
        {
            string? error = IosMinimumVersionCheck.Check(minimum);

            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("cannot be read"));
        }
    }
}
