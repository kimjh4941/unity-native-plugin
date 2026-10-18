#nullable enable

using NUnit.Framework;
using UnityEditor;
using JonghyunKim.NativeToolkit.Editor.Android;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidArchitectureCheck"/>: which target architectures stop an
    /// Android build before Gradle runs.
    /// </summary>
    public sealed class AndroidArchitectureCheckTests
    {
        [TestCase(AndroidArchitecture.ARM64)]
        [TestCase(AndroidArchitecture.X86_64)]
        [TestCase(AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64)]
        public void Check_SixtyFourBitOnly_Passes(AndroidArchitecture targets)
        {
            Assert.That(AndroidArchitectureCheck.Check(targets), Is.Null);
        }

        [TestCase(AndroidArchitecture.ARMv7)]
        [TestCase(AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64)]
        [TestCase(AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64)]
        public void Check_WithArmV7_FailsAndNamesIt(AndroidArchitecture targets)
        {
            string? error = AndroidArchitectureCheck.Check(targets);

            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("ARMv7"));
            Assert.That(error, Does.Contain("Target Architectures"));
        }

        [Test]
        public void Check_NoTarget_Fails()
        {
            Assert.That(AndroidArchitectureCheck.Check(AndroidArchitecture.None), Is.Not.Null);
        }
    }
}
