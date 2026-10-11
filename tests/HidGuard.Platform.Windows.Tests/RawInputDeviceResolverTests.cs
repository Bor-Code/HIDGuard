using Shouldly;
using Xunit;
using Windows.Win32.Foundation;
using HidGuard.Core.Domain;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class RawInputDeviceResolverTests
{
    [Fact]
    public void ResolveShouldReturnSyntheticDeviceForNullHandle()
    {
        var resolver = new RawInputDeviceResolver();
        var syntheticDevice = resolver.Resolve(new HANDLE(nint.Zero));

        syntheticDevice.ShouldBe(DeviceIdentifier.Synthetic);
        syntheticDevice.Path.ShouldBe("SOFTWARE_INJECTED_INPUT");
        syntheticDevice.VendorId.ShouldBe((ushort)0);
        syntheticDevice.ProductId.ShouldBe((ushort)0);
    }

    [Fact]
    public void EnumerateKeyboardsShouldNotThrowAndReturnCollection()
    {
        var resolver = new RawInputDeviceResolver();
        var keyboards = resolver.EnumerateKeyboards();

        keyboards.ShouldNotBeNull();
    }

    [Fact]
    public void InvalidateShouldNotThrowForNonExistentHandle()
    {
        var resolver = new RawInputDeviceResolver();
        Should.NotThrow(() => resolver.Invalidate(new HANDLE(new nint(12345))));
    }
}
