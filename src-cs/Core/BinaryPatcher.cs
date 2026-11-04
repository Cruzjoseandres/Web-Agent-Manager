using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebAgentManagerApp
{
    public static class BinaryPatcher
    {
        public static void DisableAutoUpdate(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;
            NormalizeIni(Path.GetDirectoryName(exePath));
            PatchExecutable(exePath);
        }

        public static void NormalizeIni(string versionDir)
        {
            try
            {
                string iniPath = Path.Combine(versionDir, "web-agent.ini");
                if (!File.Exists(iniPath)) return;

                string[] lines = File.ReadAllLines(iniPath);
                var result = new List<string>();
                bool changed = false;
                foreach (string l in lines)
                {
                    string t = l.TrimStart();
                    if (t.StartsWith("PORT=", StringComparison.OrdinalIgnoreCase))
                    {
                        changed = true;
                        continue;
                    }
                    if (t.StartsWith("CMDLINE=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(t, "CMDLINE=", StringComparison.OrdinalIgnoreCase)) changed = true;
                        result.Add("CMDLINE=");
                        continue;
                    }
                    if (t.StartsWith("INSTHASH=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = t.Substring(9).Trim().ToLowerInvariant();
                        string wanted = "INSTHASH=" + val;
                        if (!string.Equals(t, wanted, StringComparison.Ordinal)) changed = true;
                        result.Add(wanted);
                        continue;
                    }
                    if (t.StartsWith("INSTDIR=", StringComparison.OrdinalIgnoreCase))
                    {
                        string wanted = "INSTDIR=" + versionDir;
                        if (!string.Equals(t, wanted, StringComparison.OrdinalIgnoreCase)) changed = true;
                        result.Add(wanted);
                        continue;
                    }
                    result.Add(l);
                }
                if (changed) File.WriteAllLines(iniPath, result.ToArray(), Encoding.Default);
            }
            catch { }
        }

        public static void PatchExecutable(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return;
                byte[] bytes = File.ReadAllBytes(exePath);
                bool modified = false;

                // Patrón de decisión de auto-update en web-agent.exe (x64 y x86):
                // 85 C0 74 02 B0 01 84 C0 -> test eax, eax ; je +2 ; mov al, 1 ; test al, al
                // Al cambiar B0 01 por B0 00 (mov al, 0), WebAgent SIEMPRE le responde al navegador
                // que está en la versión más reciente ("none") y NUNCA descarga ni inicia el instalador setup.exe.
                byte[] pattern = new byte[] { 0x85, 0xC0, 0x74, 0x02, 0xB0, 0x01, 0x84, 0xC0 };
                for (int i = 0; i <= bytes.Length - pattern.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (bytes[i + j] != pattern[j]) { match = false; break; }
                    }
                    if (match)
                    {
                        bytes[i + 5] = 0x00; // B0 00 = mov al, 0
                        modified = true;
                        break;
                    }
                }

                if (modified)
                {
                    File.WriteAllBytes(exePath, bytes);
                }
            }
            catch { }
        }
    }
}
