using Shouldly;
using Xunit;
using HidGuard.App.Services;

namespace HidGuard.App.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void FirstInstanceShouldAcquireMutexSuccessfully()
    {
        var uniqueName = "Local\\Test_Mutex_" + Guid.NewGuid().ToString("N");

        using var service = new SingleInstanceService(uniqueName);

        service.IsSingleInstance.ShouldBeTrue();
    }

    [Fact]
    public void SecondInstanceShouldDetectExistingInstance()
    {
        var uniqueName = "Local\\Test_Mutex_" + Guid.NewGuid().ToString("N");

        using var first = new SingleInstanceService(uniqueName);
        using var second = new SingleInstanceService(uniqueName);

        first.IsSingleInstance.ShouldBeTrue();
        second.IsSingleInstance.ShouldBeFalse();
    }
}
