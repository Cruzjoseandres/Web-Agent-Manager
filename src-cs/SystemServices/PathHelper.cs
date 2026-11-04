using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;

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

        public static void EnsureEmbeddedTools(string toolsDir)
        {
            if (!Directory.Exists(toolsDir))
            {
                try { Directory.CreateDirectory(toolsDir); } catch { }
            }
            ExtractResourceIfMissing("7z.exe", Path.Combine(toolsDir, "7z.exe"));
            ExtractResourceIfMissing("7z.dll", Path.Combine(toolsDir, "7z.dll"));
        }

        private static void ExtractResourceIfMissing(string resourceName, string targetPath)
        {
            if (File.Exists(targetPath)) return;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(resourceName))
                {
                    if (s != null)
                    {
                        using (FileStream fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            s.CopyTo(fs);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
