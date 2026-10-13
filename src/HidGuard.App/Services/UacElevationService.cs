using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace HidGuard.App.Services;

public sealed class UacElevationService : IUacElevationService
{
    public bool IsProcessElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool RestartElevated()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
