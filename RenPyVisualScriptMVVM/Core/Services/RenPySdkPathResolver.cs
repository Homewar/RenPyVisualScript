using System;
using System.IO;
using System.Runtime.InteropServices;

namespace RenPyVisualScriptMVVM.Core.Services
{
    internal static class RenPySdkPathResolver
    {
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("SDK path is required.", nameof(path));

            return Path.GetFullPath(path.Trim().Trim('"'));
        }

        public static string ResolvePythonExecutable(string sdkPath)
        {
            sdkPath = NormalizePath(sdkPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var winPython = Path.Combine(sdkPath, "lib", "py3-windows-x86_64", "python.exe");
                if (File.Exists(winPython))
                    return winPython;

                return "python";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var linuxX64 = Path.Combine(sdkPath, "lib", "py3-linux-x86_64", "python");
                if (File.Exists(linuxX64))
                    return linuxX64;

                var linuxArm = Path.Combine(sdkPath, "lib", "py3-linux-aarch64", "python");
                if (File.Exists(linuxArm))
                    return linuxArm;

                return "python3";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var macCandidates = new[]
                {
                    Path.Combine(sdkPath, "lib", "py3-mac-x86_64", "python"),
                    Path.Combine(sdkPath, "lib", "py3-mac-universal", "python"),
                    Path.Combine(sdkPath, "lib", "py3-mac-arm64", "python")
                };

                foreach (var candidate in macCandidates)
                {
                    if (File.Exists(candidate))
                        return candidate;
                }

                return "python3";
            }

            return "python3";
        }

        public static bool IsValidSdkPath(string? sdkPath)
        {
            if (string.IsNullOrWhiteSpace(sdkPath))
                return false;

            string normalized;
            try
            {
                normalized = NormalizePath(sdkPath);
            }
            catch
            {
                return false;
            }

            if (!Directory.Exists(normalized))
                return false;

            if (!Directory.Exists(Path.Combine(normalized, "renpy")) ||
                !Directory.Exists(Path.Combine(normalized, "launcher")))
                return false;

            return File.Exists(Path.Combine(normalized, "renpy.py")) ||
                   File.Exists(Path.Combine(normalized, "renpy.exe")) ||
                   File.Exists(Path.Combine(normalized, "renpy.bat")) ||
                   File.Exists(Path.Combine(normalized, "renpy.sh")) ||
                   Directory.Exists(Path.Combine(normalized, "renpy.app"));
        }
    }
}
