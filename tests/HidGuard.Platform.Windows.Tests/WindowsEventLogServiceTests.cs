using Shouldly;
using Xunit;
using HidGuard.Core.Audit;
using HidGuard.Platform.Windows.Logging;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsEventLogServiceTests
{
    [Fact]
    public void WriteSecurityEventWithEmptyMessageShouldReturnFalse()
    {
        var service = new WindowsEventLogService("HIDGuard_UnitTest");
        var result = service.WriteSecurityEvent(SecurityEventIds.AttackDetected, SecurityEventSeverity.Warning, "");

        result.ShouldBeFalse();
    }

    [Fact]
    public void WriteSecurityEventWithWhitespaceMessageShouldReturnFalse()
    {
        var service = new WindowsEventLogService("HIDGuard_UnitTest");
        var result = service.WriteSecurityEvent(SecurityEventIds.AttackDetected, SecurityEventSeverity.Warning, "   ");

        result.ShouldBeFalse();
    }

    [Fact]
    public void WriteSecurityEventShouldNotThrowUnhandledException()
    {
        var service = new WindowsEventLogService("HIDGuard_UnitTest_NonExistentSource_" + Guid.NewGuid().ToString("N"));

        Should.NotThrow(() =>
        {
            service.WriteSecurityEvent(
                SecurityEventIds.AttackDetected,
                SecurityEventSeverity.Warning,
                "Simulated attack detected for test validation.");
        });
    }

    [Fact]
    public void WriteSecurityEventWithDifferentSeveritiesShouldExecuteSafely()
    {
        var service = new WindowsEventLogService();

        Should.NotThrow(() =>
        {
            service.WriteSecurityEvent(SecurityEventIds.ServiceStarted, SecurityEventSeverity.Information, "Service started.");
            service.WriteSecurityEvent(SecurityEventIds.AttackDetected, SecurityEventSeverity.Warning, "Warning event.");
            service.WriteSecurityEvent(SecurityEventIds.DeviceDisabled, SecurityEventSeverity.Error, "Error event.");
        });
    }
}
