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

        content.ShouldContain("MyAppVersion \"1.0.0\"");
        content.ShouldContain("AppName={#MyAppName}");
        content.ShouldContain("DefaultDirName={autopf}\\{#MyAppName}");
        content.ShouldContain("OutputBaseFilename=HIDGuard-Setup-v{#MyAppVersion}");
        content.ShouldContain("ArchitecturesAllowed=x64");

        var lines = File.ReadAllLines(issPath);
        var doubleSlash = new string('/', 2);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith(';').ShouldBeFalse();
            trimmed.StartsWith(doubleSlash, StringComparison.Ordinal).ShouldBeFalse();
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
        content.ShouldContain("$hasAppBinaries");

        var lines = File.ReadAllLines(scriptPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith('#').ShouldBeFalse();
        }
    }

    [Fact]
    public void PackageScriptShouldExistAndContainPackagingSteps()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "package.ps1");

        File.Exists(scriptPath).ShouldBeTrue();

        var content = File.ReadAllText(scriptPath);
        content.ShouldContain("Compress-Archive");
        content.ShouldContain("SHA256SUMS.txt");
        content.ShouldContain("CHANGELOG.md");
        content.ShouldContain("HidGuard.Simulator.csproj");
        content.ShouldContain("HidGuard.App.csproj");

        var lines = File.ReadAllLines(scriptPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith('#').ShouldBeFalse();
        }
    }

    [Fact]
    public void ReleaseWorkflowShouldBeConfiguredCorrectly()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "release.yml");

        File.Exists(workflowPath).ShouldBeTrue();

        var content = File.ReadAllText(workflowPath);
        content.ShouldContain("name: Release");
        content.ShouldContain("tags:");
        content.ShouldContain("- 'v*'");
        content.ShouldContain("./scripts/package.ps1");
        content.ShouldContain("./scripts/build-installer.ps1");
        content.ShouldContain("softprops/action-gh-release");
        content.ShouldContain("SHA256SUMS.txt");

        var lines = File.ReadAllLines(workflowPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith('#').ShouldBeFalse();
        }
    }

    [Fact]
    public void CiWorkflowShouldVerifyAllArtifacts()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");

        File.Exists(workflowPath).ShouldBeTrue();

        var content = File.ReadAllText(workflowPath);
        content.ShouldContain("dotnet test");
        content.ShouldContain("--verify");
        content.ShouldContain("actions/upload-artifact");

        var lines = File.ReadAllLines(workflowPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            trimmed.StartsWith('#').ShouldBeFalse();
        }
    }

    [Fact]
    public void DirectoryBuildPropsShouldDeclareVersionOneZeroZero()
    {
        var root = FindRepositoryRoot();
        var propsPath = Path.Combine(root, "Directory.Build.props");

        File.Exists(propsPath).ShouldBeTrue();

        var content = File.ReadAllText(propsPath);
        content.ShouldContain("<Version>1.0.0</Version>");
        content.ShouldContain("<AssemblyVersion>1.0.0.0</AssemblyVersion>");
        content.ShouldContain("<FileVersion>1.0.0.0</FileVersion>");
        content.ShouldContain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
    }

    [Fact]
    public void ChangelogShouldDocumentVersionOneZeroZeroRelease()
    {
        var root = FindRepositoryRoot();
        var changelogPath = Path.Combine(root, "CHANGELOG.md");

        File.Exists(changelogPath).ShouldBeTrue();

        var content = File.ReadAllText(changelogPath);
        content.ShouldContain("## [1.0.0] - 2026-10-17");
        content.ShouldContain("HidGuard.Core");
        content.ShouldContain("HidGuard.Platform.Windows");
        content.ShouldContain("HidGuard.App");
        content.ShouldContain("HidGuard.Simulator");
        content.ShouldNotContain("<!--");
    }
}
