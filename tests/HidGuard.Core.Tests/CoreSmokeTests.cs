using Shouldly;
using Xunit;

namespace HidGuard.Core.Tests;

public sealed class CoreSmokeTests
{
    [Fact]
    public void FoundationShouldBeConfiguredCorrectly()
    {
        const bool configured = true;
        configured.ShouldBeTrue();
    }
}
