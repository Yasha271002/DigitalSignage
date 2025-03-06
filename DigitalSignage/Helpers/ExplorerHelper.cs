using System.Diagnostics;
using System.IO;

namespace DigitalSignage.Helpers;

public static class ExplorerHelper
{
    public static void KillExplorer()
    {
        if (DebugHelper.IsRunningInDebugMode)
            return;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "taskkill",
            Arguments = "/F /IM explorer.exe",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        process?.WaitForExit();
    }

    public static void RunExplorer()
    {
        if (DebugHelper.IsRunningInDebugMode)
            return;

        if (Process.GetProcessesByName("explorer").Length == 0)
            Process.Start(Path.Combine(Environment.GetEnvironmentVariable("windir"), "explorer.exe"));
    }
}