using System.IO;
using Shouldly;
using Xunit;

namespace HidGuard.App.Tests;

public sealed class InstallerScriptTests
{
    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "HidGuard.sln")))
        {
            current = current.Parent;
        }

        if (current == null)
        {
            throw new InvalidOperationException("Could not locate repository root containing HidGuard.sln.");
        }

        return current.FullName;
    }

    [Fact]
    public void InnoSetupScriptShouldExistAndContainValidDirectives()
    {
        var root = FindRepositoryRoot();
        var issPath = Path.Combine(root, "installer", "HIDGuard.iss");

        File.Exists(issPath).ShouldBeTrue();

        var content = File.ReadAllText(issPath);
        content.ShouldContain("[Setup]");
        content.ShouldContain("[Files]");
        content.ShouldContain("[Icons]");
        content.ShouldContain("[Registry]");
        content.ShouldContain("[Run]");
        content.ShouldContain("[Tasks]");

        content.ShouldContain("AppName={#MyAppName}");
        content.ShouldContain("DefaultDirName={autopf}\\{#MyAppName}");
        content.ShouldContain("OutputBaseFilename=HIDGuard-Setup-v{#MyAppVersion}");

        var lines = File.ReadAllLines(issPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith(';').ShouldBeFalse();
            trimmed.StartsWith("//", StringComparison.Ordinal).ShouldBeFalse();
        }
    }

    [Fact]
    public void BuildInstallerScriptShouldExistAndReferenceInnoSetup()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "build-installer.ps1");

        File.Exists(scriptPath).ShouldBeTrue();

        var content = File.ReadAllText(scriptPath);
        content.ShouldContain("ISCC.exe");
        content.ShouldContain("HIDGuard.iss");
        content.ShouldContain("SHA256SUMS.txt");
    }
}
