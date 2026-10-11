using Shouldly;
using Xunit;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsRawInputListenerLifecycleTests
{
    [Fact]
    public void ListenerShouldTransitionStateCorrectlyDuringLifecycle()
    {
        using var listener = new WindowsRawInputListener();

        listener.IsRunning.ShouldBeFalse();

        listener.StartListening();
        listener.IsRunning.ShouldBeTrue();

        listener.StartListening();
        listener.IsRunning.ShouldBeTrue();

        listener.StopListening();
        listener.IsRunning.ShouldBeFalse();

        listener.StopListening();
        listener.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void GetConnectedKeyboardsShouldReturnNonNullCollection()
    {
        using var listener = new WindowsRawInputListener();
        var keyboards = listener.GetConnectedKeyboards();

        keyboards.ShouldNotBeNull();
    }

    [Fact]
    public void StartListeningShouldThrowWhenDisposed()
    {
        var listener = new WindowsRawInputListener();
        listener.Dispose();

        Should.Throw<ObjectDisposedException>(() => listener.StartListening());
    }
}
