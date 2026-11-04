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
            EnsureToolFile(toolsDir, "7z.exe");
            EnsureToolFile(toolsDir, "7z.dll");
        }

        private static void EnsureToolFile(string toolsDir, string fileName)
        {
            string targetPath = Path.Combine(toolsDir, fileName);
            if (File.Exists(targetPath)) return;

            // 1. Si existe en la carpeta local de tools del ejecutable, copiarlo directamente
            string localSource = Path.Combine(GetExeDir(), "tools", fileName);
            if (File.Exists(localSource))
            {
                try
                {
                    File.Copy(localSource, targetPath, true);
                    return;
                }
                catch { }
            }

            // 2. Si no, extraer del recurso empaquetado seguro (.pkg)
            string pkgResource = fileName + ".pkg";
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(pkgResource))
                {
                    if (s != null)
                    {
                        byte[] buffer = new byte[s.Length];
                        int totalRead = 0;
                        while (totalRead < buffer.Length)
                        {
                            int read = s.Read(buffer, totalRead, buffer.Length - totalRead);
                            if (read <= 0) break;
                            totalRead += read;
                        }
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] ^= 0x42;
                        }
                        File.WriteAllBytes(targetPath, buffer);
                        return;
                    }
                }
            }
            catch { }

            // 3. Fallback a recurso sin máscara si existiera
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(fileName))
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
