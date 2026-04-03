using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PortWhisperer.Core.Services;

public static class ShellRunner
{
    public static string? Run(string command, int timeoutMs = 10000)
    {
        try
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);

            return proc.ExitCode == 0 || !string.IsNullOrWhiteSpace(output)
                ? output.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool KillProcess(int pid, string signal = "SIGTERM")
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill(signal == "SIGKILL");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
