using System;
using System.IO;
using System.Diagnostics;

namespace WebAgentManagerApp
{
    public static class PathHelper
    {
        private static string cachedPermExe;

        public static string GetDataRoot()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WebAgentManager");
            if (!Directory.Exists(root))
            {
                try { Directory.CreateDirectory(root); } catch { }
            }
            return root;
        }

        public static string GetExeDir()
        {
            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        }

        public static string GetVersionsDir()
        {
            return Path.Combine(GetDataRoot(), "versions");
        }

        public static string GetConfigFile()
        {
            return Path.Combine(GetDataRoot(), "config", "clientes.json");
        }

        public static string GetPermanentExePath()
        {
            if (!string.IsNullOrEmpty(cachedPermExe)) return cachedPermExe;

            string permExe = Path.Combine(GetDataRoot(), "WebAgentManager.exe");
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(currentExe, permExe, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool needCopy = !File.Exists(permExe);
                    if (!needCopy)
                    {
                        FileInfo a = new FileInfo(currentExe);
                        FileInfo b = new FileInfo(permExe);
                        needCopy = a.Length != b.Length || a.LastWriteTimeUtc != b.LastWriteTimeUtc;
                    }
                    if (needCopy) File.Copy(currentExe, permExe, true);
                }
                catch { }
            }
            cachedPermExe = File.Exists(permExe) ? permExe : currentExe;
            return cachedPermExe;
        }

        public static string Resolve7zExe(string toolsDir)
        {
            string[] candidates = new string[]
            {
                Path.Combine(toolsDir, "7z.exe"),
                Path.Combine(GetExeDir(), "tools", "7z.exe"),
                Path.Combine(GetDataRoot(), "tools", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            };

            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }
            return null;
        }

        public static void EnsureEmbeddedTools(string toolsDir)
        {
            if (!Directory.Exists(toolsDir))
            {
                try { Directory.CreateDirectory(toolsDir); } catch { }
            }

            string targetExe = Path.Combine(toolsDir, "7z.exe");
            string targetDll = Path.Combine(toolsDir, "7z.dll");

            string localDir = Path.Combine(GetExeDir(), "tools");
            if (!File.Exists(targetExe))
            {
                string srcExe = Path.Combine(localDir, "7z.exe");
                if (File.Exists(srcExe))
                {
                    try { File.Copy(srcExe, targetExe, true); } catch { }
                }
            }

            if (!File.Exists(targetDll))
            {
                string srcDll = Path.Combine(localDir, "7z.dll");
                if (File.Exists(srcDll))
                {
                    try { File.Copy(srcDll, targetDll, true); } catch { }
                }
            }
        }
    }
}
