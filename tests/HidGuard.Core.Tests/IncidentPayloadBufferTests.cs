using Shouldly;
using Xunit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Incidents;

namespace HidGuard.Core.Tests;

public sealed class IncidentPayloadBufferTests
{
    private static readonly DeviceIdentifier DeviceA = new(@"\\?\HID#VID_046D&PID_C52B#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x046D, 0xC52B);
    private static readonly DeviceIdentifier DeviceB = new(@"\\?\HID#VID_16C0&PID_05DF#1111#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x16C0, 0x05DF);

    [Fact]
    public void DisabledByDefaultDropsKeystrokesAndReturnsNullSnapshot()
    {
        var buffer = new IncidentPayloadBuffer();
        buffer.IsEnabled.ShouldBeFalse();

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x41));
        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x42));

        var snapshot = buffer.TakeSnapshot(DeviceA);
        snapshot.ShouldBeNull();
    }

    [Fact]
    public void EnabledBuffersKeyDownEventsOnly()
    {
        var config = new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        };
        var buffer = new IncidentPayloadBuffer(config);
        buffer.IsEnabled.ShouldBeTrue();

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x41));
        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyUp, 0x41));
        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x42));

        var snapshot = buffer.TakeSnapshot(DeviceA);
        snapshot.ShouldNotBeNull();
        snapshot.Length.ShouldBe(2);
        snapshot[0].ShouldBe((byte)0x41);
        snapshot[1].ShouldBe((byte)0x42);

        buffer.TakeSnapshot(DeviceA).ShouldBeNull();
    }

    [Fact]
    public void RingBufferEvictsOldestKeystrokesWhenCapacityExceeded()
    {
        var config = new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 3
        };
        var buffer = new IncidentPayloadBuffer(config);

        for (var i = 1; i <= 5; i++)
        {
            buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, (uint)i));
        }

        var snapshot = buffer.TakeSnapshot(DeviceA);
        snapshot.ShouldNotBeNull();
        snapshot.Length.ShouldBe(3);
        snapshot[0].ShouldBe((byte)3);
        snapshot[1].ShouldBe((byte)4);
        snapshot[2].ShouldBe((byte)5);
    }

    [Fact]
    public void BuffersAreIsolatedPerDevice()
    {
        var config = new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        };
        var buffer = new IncidentPayloadBuffer(config);

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x10));
        buffer.Record(new KeystrokeEvent(DeviceB, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x20));

        var snapA = buffer.TakeSnapshot(DeviceA);
        var snapB = buffer.TakeSnapshot(DeviceB);

        snapA.ShouldNotBeNull();
        snapA.Length.ShouldBe(1);
        snapA[0].ShouldBe((byte)0x10);

        snapB.ShouldNotBeNull();
        snapB.Length.ShouldBe(1);
        snapB[0].ShouldBe((byte)0x20);
    }

    [Fact]
    public void RemoveDeviceCleansUpDeviceBuffer()
    {
        var config = new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        };
        var buffer = new IncidentPayloadBuffer(config);

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x10));
        buffer.RemoveDevice(DeviceA);

        buffer.TakeSnapshot(DeviceA).ShouldBeNull();
    }

    [Fact]
    public void ClearRemovesAllBuffers()
    {
        var config = new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        };
        var buffer = new IncidentPayloadBuffer(config);

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x10));
        buffer.Record(new KeystrokeEvent(DeviceB, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x20));
        buffer.Clear();

        buffer.TakeSnapshot(DeviceA).ShouldBeNull();
        buffer.TakeSnapshot(DeviceB).ShouldBeNull();
    }

    [Fact]
    public void ReconfiguringToDisabledClearsBuffers()
    {
        var buffer = new IncidentPayloadBuffer(new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        });

        buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x10));

        buffer.Configure(new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = false
        });

        buffer.IsEnabled.ShouldBeFalse();
        buffer.TakeSnapshot(DeviceA).ShouldBeNull();
    }

    [Fact]
    public void ReconfiguringToSmallerCapacityTrimsBuffers()
    {
        var buffer = new IncidentPayloadBuffer(new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 10
        });

        for (var i = 1; i <= 5; i++)
        {
            buffer.Record(new KeystrokeEvent(DeviceA, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, (uint)i));
        }

        buffer.Configure(new IncidentStoreConfiguration
        {
            EnableOptInPayloadCapture = true,
            CaptureWindowKeystrokes = 2
        });

        var snapshot = buffer.TakeSnapshot(DeviceA);
        snapshot.ShouldNotBeNull();
        snapshot.Length.ShouldBe(2);
        snapshot[0].ShouldBe((byte)4);
        snapshot[1].ShouldBe((byte)5);
    }

    [Fact]
    public void NullArgumentsShouldThrowArgumentNullException()
    {
        var buffer = new IncidentPayloadBuffer();

        Should.Throw<ArgumentNullException>(() => buffer.Configure(null!));
        Should.Throw<ArgumentNullException>(() => buffer.Record(null!));
        Should.Throw<ArgumentNullException>(() => buffer.TakeSnapshot(null!));
        Should.Throw<ArgumentNullException>(() => buffer.RemoveDevice(null!));
    }
}
