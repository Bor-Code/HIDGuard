using System.Text;
using Shouldly;
using Xunit;
using HidGuard.Platform.Windows.Security;

namespace HidGuard.Platform.Windows.Tests;

public sealed class DpapiDataProtectorTests
{
    [Fact]
    public void ProtectAndUnprotectRoundtripShouldRestoreOriginalBytes()
    {
        var protector = new DpapiDataProtector();
        var plainBytes = Encoding.UTF8.GetBytes("HIDGuard DPAPI Sensitive Incident Payload Test");

        var cipherBytes = protector.Protect(plainBytes);

        cipherBytes.ShouldNotBeNull();
        cipherBytes.ShouldNotBe(plainBytes);

        var restoredBytes = protector.Unprotect(cipherBytes);

        restoredBytes.ShouldBe(plainBytes);
        Encoding.UTF8.GetString(restoredBytes).ShouldBe("HIDGuard DPAPI Sensitive Incident Payload Test");
    }

    [Fact]
    public void ProtectAndUnprotectWithEntropyShouldRestoreOriginalBytes()
    {
        var entropy = Encoding.UTF8.GetBytes("incident-entropy-salt");
        var protector = new DpapiDataProtector(optionalEntropy: entropy);
        var plainBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD };

        var cipherBytes = protector.Protect(plainBytes);
        cipherBytes.ShouldNotBeNull();

        var restoredBytes = protector.Unprotect(cipherBytes);
        restoredBytes.ShouldBe(plainBytes);
    }

    [Fact]
    public void ProtectShouldThrowArgumentNullExceptionWhenBufferIsNull()
    {
        var protector = new DpapiDataProtector();
        Should.Throw<ArgumentNullException>(() => protector.Protect(null!));
    }

    [Fact]
    public void UnprotectShouldThrowArgumentNullExceptionWhenBufferIsNull()
    {
        var protector = new DpapiDataProtector();
        Should.Throw<ArgumentNullException>(() => protector.Unprotect(null!));
    }
}
