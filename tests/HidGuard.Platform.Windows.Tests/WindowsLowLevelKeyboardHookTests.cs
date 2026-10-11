using Shouldly;
using Xunit;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsLowLevelKeyboardHookTests
{
    [Fact]
    public void SuppressionActiveFlagShouldReflectState()
    {
        using var hook = new WindowsLowLevelKeyboardHook();

        hook.IsSuppressionActive.ShouldBeFalse();

        hook.IsSuppressionActive = true;
        hook.IsSuppressionActive.ShouldBeTrue();

        hook.IsSuppressionActive = false;
        hook.IsSuppressionActive.ShouldBeFalse();
    }

    [Fact]
    public void SuppressTemporarilyShouldActivateSuppression()
    {
        using var hook = new WindowsLowLevelKeyboardHook();

        hook.SuppressTemporarily(TimeSpan.FromSeconds(5));

        hook.IsSuppressionActive.ShouldBeTrue();
    }

    [Fact]
    public void LifecycleStartAndStopShouldManageThreadGracefully()
    {
        using var hook = new WindowsLowLevelKeyboardHook();

        hook.StartInterception();
        hook.StopInterception();

        hook.StopInterception();
    }
}
