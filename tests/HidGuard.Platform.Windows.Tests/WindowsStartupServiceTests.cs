using Shouldly;
using Xunit;
using HidGuard.Platform.Windows;

namespace HidGuard.Platform.Windows.Tests;

public sealed class WindowsStartupServiceTests
{
    private sealed class StubRegistryKeyAccessor : IRegistryKeyAccessor
    {
        public Dictionary<string, string> Storage { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool ShouldThrow { get; set; }

        public string? GetValue(string subKey, string valueName)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated registry read failure.");
            }

            var key = $"{subKey}\\{valueName}";
            return Storage.TryGetValue(key, out var val) ? val : null;
        }

        public void SetValue(string subKey, string valueName, string value)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated registry write failure.");
            }

            var key = $"{subKey}\\{valueName}";
            Storage[key] = value;
        }

        public void DeleteValue(string subKey, string valueName)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated registry delete failure.");
            }

            var key = $"{subKey}\\{valueName}";
            Storage.Remove(key);
        }
    }

    [Fact]
    public void IsStartWithWindowsEnabledShouldReturnFalseWhenValueDoesNotExist()
    {
        var accessor = new StubRegistryKeyAccessor();
        var service = new WindowsStartupService(accessor, @"C:\Program Files\HIDGuard\HidGuard.App.exe");

        service.IsStartWithWindowsEnabled().ShouldBeFalse();
    }

    [Fact]
    public void IsStartWithWindowsEnabledShouldReturnTrueWhenValueExists()
    {
        var accessor = new StubRegistryKeyAccessor();
        accessor.SetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "HIDGuard", "\"C:\\Program Files\\HIDGuard\\HidGuard.App.exe\" --minimized");
        var service = new WindowsStartupService(accessor, @"C:\Program Files\HIDGuard\HidGuard.App.exe");

        service.IsStartWithWindowsEnabled().ShouldBeTrue();
    }

    [Fact]
    public void SetStartWithWindowsTrueShouldWriteQuotedExecutablePathWithMinimizedFlag()
    {
        var accessor = new StubRegistryKeyAccessor();
        var service = new WindowsStartupService(accessor, @"C:\Program Files\HIDGuard\HidGuard.App.exe");

        var result = service.SetStartWithWindows(true);

        result.ShouldBeTrue();
        accessor.GetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "HIDGuard").ShouldBe("\"C:\\Program Files\\HIDGuard\\HidGuard.App.exe\" --minimized");
    }

    [Fact]
    public void SetStartWithWindowsFalseShouldDeleteValue()
    {
        var accessor = new StubRegistryKeyAccessor();
        accessor.SetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "HIDGuard", "existing");
        var service = new WindowsStartupService(accessor, @"C:\Program Files\HIDGuard\HidGuard.App.exe");

        var result = service.SetStartWithWindows(false);

        result.ShouldBeTrue();
        accessor.GetValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "HIDGuard").ShouldBeNull();
    }

    [Fact]
    public void SetStartWithWindowsShouldReturnFalseWhenRegistryThrows()
    {
        var accessor = new StubRegistryKeyAccessor { ShouldThrow = true };
        var service = new WindowsStartupService(accessor, @"C:\Program Files\HIDGuard\HidGuard.App.exe");

        var result = service.SetStartWithWindows(true);

        result.ShouldBeFalse();
    }
}
