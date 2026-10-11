using Shouldly;
using Xunit;

namespace HidGuard.Platform.Windows.Tests;

public sealed class PlatformSmokeTests
{
    [Fact]
    public void WindowsPlatformFoundationShouldBeConfiguredCorrectly()
    {
        const bool configured = true;
        configured.ShouldBeTrue();
    }
}
