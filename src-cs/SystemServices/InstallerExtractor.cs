using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebAgentManagerApp
{
    public static class InstallerExtractor
    {
        public static List<WebAgentVersion> ScanAllVersions(string versionsDir, string exeDir, string toolsDir)
        {
            Dictionary<string, WebAgentVersion> foundMap = new Dictionary<string, WebAgentVersion>(StringComparer.OrdinalIgnoreCase);

            string[] versionRoots = new string[]
            {
                versionsDir,
                Path.Combine(exeDir, "versions"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
            };

            foreach (string vRoot in versionRoots)
            {
                if (Directory.Exists(vRoot))
                {
                    foreach (string dir in Directory.GetDirectories(vRoot, "web-agent*"))
                    {
                        string exe = Path.Combine(dir, "web-agent.exe");
                        if (File.Exists(exe))
                        {
                            BinaryPatcher.DisableAutoUpdate(exe);
                            var v = ParseVersionFromFolder(dir, exe);
                            if (!foundMap.ContainsKey(v.FolderName))
                            {
                                string targetDir = Path.Combine(versionsDir, v.FolderName);
                                if (!Directory.Exists(targetDir))
                                {
                                    try { CopyDirectory(dir, targetDir); } catch { }
                                }
                                string targetExe = Path.Combine(targetDir, "web-agent.exe");
                                if (File.Exists(targetExe))
                                {
                                    BinaryPatcher.DisableAutoUpdate(targetExe);
                                    v.ExecutablePath = targetExe;
                                }
                                foundMap[v.FolderName] = v;
                            }
                        }
                    }
                }
            }

            string[] downloadDirs = new string[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Descargas"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Descargas")
            };

            foreach (string d in downloadDirs)
            {
                if (Directory.Exists(d))
                {
                    foreach (string f in Directory.GetFiles(d, "web-agent*.exe"))
                    {
                        Match m = Regex.Match(Path.GetFileName(f), @"web-agent-([0-9.]+)-windows-(x64|x86)", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            string ver = m.Groups[1].Value;
                            string arch = m.Groups[2].Value.ToLower();
                            string folderName = "web-agent-" + ver + "-" + arch;
                            string targetExe = Path.Combine(versionsDir, folderName, "web-agent.exe");
                            if (!File.Exists(targetExe) || !foundMap.ContainsKey(folderName))
                            {
                                bool ok = ExtractInstaller(toolsDir, versionsDir, f, folderName);
                                if (ok)
                                {
                                    foundMap[folderName] = new WebAgentVersion
                                    {
                                        FolderName = folderName,
                                        Version = ver,
                                        Architecture = arch,
                                        ExecutablePath = targetExe,
                                        DisplayName = "WebAgent " + ver + " (" + arch + ")"
                                    };
                                }
                            }
                        }
                    }
                }
            }

            return foundMap.Values.OrderBy(v => v.FolderName).ToList();
        }

        public static WebAgentVersion ParseVersionFromFolder(string folderPath, string exePath)
        {
            string folderName = Path.GetFileName(folderPath);
            string ver = "Desconocida";
            string arch = folderName.Contains("x86") ? "x86" : "x64";

            string pkgJson = Path.Combine(folderPath, "package.json");
            if (File.Exists(pkgJson))
            {
                try
                {
                    string content = File.ReadAllText(pkgJson);
                    Match m = Regex.Match(content, @"""version""\s*:\s*""([^""]+)""");
                    if (m.Success) ver = m.Groups[1].Value;
                }
                catch { }
            }

            if (ver == "Desconocida")
            {
                Match m = Regex.Match(folderName, @"web-agent-([0-9.]+)");
                if (m.Success) ver = m.Groups[1].Value;
            }

            string cleanFolderName = folderName.Equals("web-agent", StringComparison.OrdinalIgnoreCase)
                ? "web-agent-" + ver + "-" + arch
                : folderName;

            return new WebAgentVersion
            {
                FolderName = cleanFolderName,
                Version = ver,
                Architecture = arch,
                ExecutablePath = exePath,
                DisplayName = "WebAgent " + ver + " (" + arch + ")"
            };
        }

        public static bool ExtractInstaller(string toolsDir, string versionsDir, string installerPath, string targetFolderName)
        {
            PathHelper.EnsureEmbeddedTools(toolsDir);
            string sevenZip = Path.Combine(toolsDir, "7z.exe");
            if (!File.Exists(sevenZip)) return false;

            string targetDir = Path.Combine(versionsDir, targetFolderName);
            string innerArchiveName = targetFolderName.Contains("x86") ? "app-32.7z" : "app-64.7z";
            string tempDir = Path.Combine(Path.GetTempPath(), "wa_ext_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(targetDir);

                var p1 = Process.Start(new ProcessStartInfo
                {
                    FileName = sevenZip,
                    Arguments = string.Format("e \"{0}\" \"$PLUGINSDIR\\{1}\" \"-o{2}\" -y", installerPath, innerArchiveName, tempDir),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                p1.WaitForExit();

                string innerPath = Path.Combine(tempDir, innerArchiveName);
                if (File.Exists(innerPath))
                {
                    var p2 = Process.Start(new ProcessStartInfo
                    {
                        FileName = sevenZip,
                        Arguments = string.Format("x \"{0}\" \"-o{1}\" -y", innerPath, targetDir),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                    p2.WaitForExit();

                    string hash = CalculateMD5(installerPath);
                    string iniContent = string.Format("[AGENT]\r\nCMDLINE={0}\r\nINSTDIR={1}\r\nINSTHASH={2}\r\n[UPDATE]\r\n", installerPath, targetDir, hash);
                    File.WriteAllText(Path.Combine(targetDir, "web-agent.ini"), iniContent);

                    string extractedExe = Path.Combine(targetDir, "web-agent.exe");
                    BinaryPatcher.DisableAutoUpdate(extractedExe);

                    return true;
                }
            }
            catch { }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
            return false;
        }

        public static string CalculateMD5(string filename)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        public static void CleanupRogueGlobalInstall(string versionsDir)
        {
            try
            {
                string globalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "web-agent");
                if (Directory.Exists(globalDir))
                {
                    string exe = Path.Combine(globalDir, "web-agent.exe");
                    if (File.Exists(exe))
                    {
                        var v = ParseVersionFromFolder(globalDir, exe);
                        string isolatedDir = Path.Combine(versionsDir, v.FolderName);
                        if (!Directory.Exists(isolatedDir))
                        {
                            try { CopyDirectory(globalDir, isolatedDir); } catch { }
                        }
                    }

                    foreach (var proc in Process.GetProcessesByName("web-agent"))
                    {
                        try
                        {
                            if (proc.MainModule.FileName.StartsWith(globalDir, StringComparison.OrdinalIgnoreCase))
                            {
                                proc.Kill();
                                proc.WaitForExit(1000);
                            }
                        }
                        catch { }
                    }

                    string uninst = Path.Combine(globalDir, "Uninstall web-agent.exe");
                    if (File.Exists(uninst))
                    {
                        try
                        {
                            var pu = Process.Start(new ProcessStartInfo
                            {
                                FileName = uninst,
                                Arguments = "/S",
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            });
                            pu.WaitForExit(3000);
                        }
                        catch { }
                    }

                    try { Directory.Delete(globalDir, true); } catch { }
                }
            }
            catch { }
        }
    }
}
