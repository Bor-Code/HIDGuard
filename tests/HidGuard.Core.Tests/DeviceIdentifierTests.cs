using Shouldly;
using Xunit;
using HidGuard.Core.Domain;

namespace HidGuard.Core.Tests;

public sealed class DeviceIdentifierTests
{
    [Fact]
    public void ConstructorShouldInitializePropertiesCorrectly()
    {
        const string path = @"\\?\HID#VID_046D&PID_C52B#7&37d80ae&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}";
        const ushort vid = 0x046D;
        const ushort pid = 0xC52B;

        var identifier = new DeviceIdentifier(path, vid, pid);

        identifier.Path.ShouldBe(path);
        identifier.VendorId.ShouldBe(vid);
        identifier.ProductId.ShouldBe(pid);
        identifier.FormattedHardwareId.ShouldBe("VID_046D&PID_C52B");
        identifier.ToString().ShouldBe("VID_046D&PID_C52B");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorShouldThrowWhenPathIsNullOrWhiteSpace(string? invalidPath)
    {
        Should.Throw<ArgumentException>(() => new DeviceIdentifier(invalidPath!, 0x1234, 0x5678));
    }

    [Fact]
    public void TwoInstancesWithIdenticalPropertiesShouldBeEqual()
    {
        var id1 = new DeviceIdentifier("PATH_A", 0x1234, 0x5678);
        var id2 = new DeviceIdentifier("PATH_A", 0x1234, 0x5678);

        id1.ShouldBe(id2);
        (id1 == id2).ShouldBeTrue();
    }

    [Fact]
    public void TwoInstancesWithDifferentPropertiesShouldNotBeEqual()
    {
        var id1 = new DeviceIdentifier("PATH_A", 0x1234, 0x5678);
        var id2 = new DeviceIdentifier("PATH_B", 0x1234, 0x5678);

        id1.ShouldNotBe(id2);
        (id1 != id2).ShouldBeTrue();
    }

    [Theory]
    [InlineData(@"\\?\HID#VID_046D&PID_C52B#0000", (ushort)0x046D, (ushort)0xC52B)]
    [InlineData(@"HID\vid_1234&pid_abcd&00", (ushort)0x1234, (ushort)0xABCD)]
    [InlineData(@"USB#VID_10C4&PID_EA60#0001", (ushort)0x10C4, (ushort)0xEA60)]
    public void TryParseIdsShouldExtractVidAndPidSuccessfully(string path, ushort expectedVid, ushort expectedPid)
    {
        var success = DeviceIdentifier.TryParseIds(path, out var vid, out var pid);

        success.ShouldBeTrue();
        vid.ShouldBe(expectedVid);
        pid.ShouldBe(expectedPid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NO_VID_OR_PID_HERE")]
    [InlineData("VID_XYZ&PID_1234")]
    [InlineData("VID_12&PID_34")]
    public void TryParseIdsShouldReturnFalseForInvalidPaths(string? invalidPath)
    {
        var success = DeviceIdentifier.TryParseIds(invalidPath!, out var vid, out var pid);

        success.ShouldBeFalse();
        vid.ShouldBe((ushort)0);
        pid.ShouldBe((ushort)0);
    }
}
