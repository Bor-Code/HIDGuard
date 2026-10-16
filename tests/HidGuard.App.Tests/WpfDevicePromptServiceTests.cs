using Shouldly;
using Xunit;
using HidGuard.App.Services;
using HidGuard.Core.Domain;

namespace HidGuard.App.Tests;

public sealed class WpfDevicePromptServiceTests
{
    [Fact]
    public async Task PromptDeviceTrustAsyncWhenNoDispatcherShouldReturnNull()
    {
        var service = new WpfDevicePromptService();
        var dev = new DeviceIdentifier("HID\\VID_1234&PID_5678\\0", 0x1234, 0x5678);

        var result = await service.PromptDeviceTrustAsync(dev);

        result.ShouldBeNull();
    }
}
