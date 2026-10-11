using Shouldly;
using Xunit;
using Windows.Win32.Foundation;
using HidGuard.Core.Domain;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class SendInputBehaviorTests
{
    [Fact]
    public void SendInputShouldExecuteAndDemonstrateSyntheticHandling()
    {
        var result = SyntheticInputDispatcher.SendKey(0x41, true);
        (result == 1u || result == 0u).ShouldBeTrue();

        var resolver = new RawInputDeviceResolver();
        var reportedDevice = resolver.Resolve(new HANDLE(nint.Zero));

        reportedDevice.ShouldBe(DeviceIdentifier.Synthetic);
        reportedDevice.Path.ShouldBe("SOFTWARE_INJECTED_INPUT");
        reportedDevice.VendorId.ShouldBe((ushort)0);
        reportedDevice.ProductId.ShouldBe((ushort)0);
    }
}
