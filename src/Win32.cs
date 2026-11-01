using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace MHDoctor
{
    internal static class Win32
    {
        // ---------------------------------------------------------------- versao real do SO
        [StructLayout(LayoutKind.Sequential)]
        private struct RTL_OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEX versionInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        private const int LOGPIXELSX = 88;

        /// <summary>DPI do sistema (96, 120, 144, 192...).</summary>
        public static int SystemDpi()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                int dpi = GetDeviceCaps(dc, LOGPIXELSX);
                ReleaseDC(IntPtr.Zero, dc);
                if (dpi >= 72 && dpi <= 480) return dpi;
            }
            catch { }
            return 96;
        }

        /// <summary>
        /// Environment.OSVersion mente em builds recentes sem manifesto.
        /// RtlGetVersion devolve o build real (ex.: 10.0.26200).
        /// </summary>
        public static Version RealOsVersion()
        {
            try
            {
                var info = new RTL_OSVERSIONINFOEX();
                info.dwOSVersionInfoSize = Marshal.SizeOf(typeof(RTL_OSVERSIONINFOEX));
                if (RtlGetVersion(ref info) == 0)
                    return new Version(info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber);
            }
            catch
            {
                // cai no fallback abaixo
            }

            return Environment.OSVersion.Version;
        }

        public static bool IsWindows11(Version v)
        {
            return v.Major >= 10 && v.Build >= 22000;
        }

        public static bool IsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Converte uma string do registro em caminho de executavel.</summary>
        public static string ExtractExePath(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            string s = Environment.ExpandEnvironmentVariables(value).Trim();

            if (s.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = s.IndexOf('"', 1);
                if (end > 1) return s.Substring(1, end - 1);
                return s.Trim('"');
            }

            int exe = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return s.Substring(0, exe + 4);

            return s;
        }

        public static string SafeFullPath(string path)
        {
            try { return System.IO.Path.GetFullPath(path); }
            catch { return path; }
        }
    }

    internal static class Util
    {
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
        }

        public static string Join(IEnumerable<string> items, string separator)
        {
            var sb = new StringBuilder();
            foreach (string item in items)
            {
                if (sb.Length > 0) sb.Append(separator);
                sb.Append(item);
            }
            return sb.ToString();
        }

        public static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string na = a.TrimEnd('\\', '/');
            string nb = b.TrimEnd('\\', '/');
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }
    }
}
