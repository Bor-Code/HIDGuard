using Shouldly;
using Xunit;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsWorkstationLockerTests
{
    [Fact]
    public void LockWorkstationShouldExecuteWithoutUnhandledException()
    {
        var locker = new WindowsWorkstationLocker();

        var action = () => locker.LockWorkstation();

        action.ShouldNotThrow();
    }
}
