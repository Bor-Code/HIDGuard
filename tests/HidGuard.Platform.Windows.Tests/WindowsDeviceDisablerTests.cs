using Shouldly;
using Xunit;
using HidGuard.Core.Domain;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsDeviceDisablerTests
{
    [Theory]
    [InlineData(@"\\?\HID#VID_16C0&PID_05DF&MI_00#7&308B9915&0&0000#{884B96C3-56EF-11D1-BC8C-00A0C91405DD}", @"HID\VID_16C0&PID_05DF&MI_00\7&308B9915&0&0000")]
    [InlineData(@"\\.\HID#VID_046D&PID_C52B#0000#{884B96C3-56EF-11D1-BC8C-00A0C91405DD}", @"HID\VID_046D&PID_C52B\0000")]
    [InlineData(@"HID#VID_1234&PID_5678#INST123#{884B96C3-56EF-11D1-BC8C-00A0C91405DD}", @"HID\VID_1234&PID_5678\INST123")]
    public void ExtractDeviceInstanceIdShouldFormatInstanceIdCorrectly(string path, string expected)
    {
        var result = WindowsDeviceDisabler.ExtractDeviceInstanceId(path);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidPathWithoutDelimiters")]
    [InlineData(@"HID#OnlyOnePart")]
    public void ExtractDeviceInstanceIdWithInvalidPathShouldReturnNull(string invalidPath)
    {
        var result = WindowsDeviceDisabler.ExtractDeviceInstanceId(invalidPath);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DisableDeviceAsyncWithSyntheticDeviceShouldReturnFalse()
    {
        var disabler = new WindowsDeviceDisabler();

        var result = await disabler.DisableDeviceAsync(DeviceIdentifier.Synthetic);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task EnableDeviceAsyncWithSyntheticDeviceShouldReturnFalse()
    {
        var disabler = new WindowsDeviceDisabler();

        var result = await disabler.EnableDeviceAsync(DeviceIdentifier.Synthetic);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DisableDeviceAsyncWithNullDeviceShouldThrow()
    {
        var disabler = new WindowsDeviceDisabler();

        await Should.ThrowAsync<ArgumentNullException>(() =>
            disabler.DisableDeviceAsync(null!));
    }
}
