using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WebAgentManagerApp
{
    public static class SmartRouter
    {
        public static bool EnvironmentMatchesPort(EnvironmentItem env, int port)
        {
            if (env == null || port <= 0) return false;
            string portStr = port.ToString();

            if (!string.IsNullOrWhiteSpace(env.AgentPort))
            {
                string[] parts = env.AgentPort.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string p in parts)
                {
                    if (string.Equals(p.Trim(), portStr, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            string srvPort = ExtractPort(env.ServerIp);
            if (!string.IsNullOrEmpty(srvPort) && srvPort.Length >= 3)
            {
                string standardAgentPort = srvPort.Substring(0, 3) + "41";
                if (string.Equals(standardAgentPort, portStr, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void RouteWebAgentCall(string uriArg)
        {
            bool acquired = false;
            System.Threading.Mutex routerMutex = null;
            try
            {
                routerMutex = new System.Threading.Mutex(false, @"Local\WebAgentManagerRouterMutex");
                try { acquired = routerMutex.WaitOne(3000); } catch (System.Threading.AbandonedMutexException) { acquired = true; }
                RouteCore(uriArg);
            }
            catch { }
            finally
            {
                if (acquired && routerMutex != null)
                {
                    try { routerMutex.ReleaseMutex(); } catch { }
                }
                if (routerMutex != null)
                {
                    try { routerMutex.Dispose(); } catch { }
                }
            }
        }

        private static void RouteCore(string uriArg)
        {
            try
            {
                int requestedPort = 0;
                Match mPort = Regex.Match(uriArg ?? "", @"port=(\d+)", RegexOptions.IgnoreCase);
                if (mPort.Success)
                {
                    int.TryParse(mPort.Groups[1].Value, out requestedPort);
                }

                if (requestedPort <= 0) return;

                string configFile = PathHelper.GetConfigFile();
                if (!File.Exists(configFile)) return;

                string json = File.ReadAllText(configFile);
                JavaScriptSerializer js = new JavaScriptSerializer();
                List<ClientItem> allClients = js.Deserialize<List<ClientItem>>(json) ?? new List<ClientItem>();

                // Asegurar formato con ambientes si el archivo es legacy
                foreach (var c in allClients)
                {
                    if (c.Environments == null) c.Environments = new List<EnvironmentItem>();
                    if (c.Environments.Count == 0 && (!string.IsNullOrEmpty(c.ServerIp) || !string.IsNullOrEmpty(c.AgentPorts)))
                    {
                        c.Environments.Add(new EnvironmentItem
                        {
                            Name = "Producción",
                            ServerIp = c.ServerIp,
                            AgentPort = c.AgentPorts,
                            VersionFolder = c.VersionFolder,
                            Description = c.Description,
                            IsEnabled = true
                        });
                    }
                }

                var portOwners = PortProcessHelper.GetWebAgentPorts();
                PortProcessHelper.PortOwnerInfo existingOnPort = portOwners.FirstOrDefault(p => p.Port == requestedPort);

                // 1. Buscar ambiente habilitado que coincida con este puerto
                EnvironmentItem selectedEnv = null;
                foreach (var cli in allClients)
                {
                    foreach (var env in cli.Environments)
                    {
                        if (env.IsEnabled && EnvironmentMatchesPort(env, requestedPort))
                        {
                            selectedEnv = env;
                            break;
                        }
                    }
                    if (selectedEnv != null) break;
                }

                // 2. Si no coincide con ninguno habilitado, verificar si coincide con uno deshabilitado -> bloquear
                if (selectedEnv == null)
                {
                    foreach (var cli in allClients)
                    {
                        foreach (var env in cli.Environments)
                        {
                            if (!env.IsEnabled && EnvironmentMatchesPort(env, requestedPort))
                            {
                                if (existingOnPort != null) PortProcessHelper.KillProcessByIdSafe(existingOnPort.Pid);
                                return;
                            }
                        }
                    }

                    // Si hay exactamente un ambiente sin puerto configurado, asignarlo dinámicamente
                    var pendingEnvs = allClients.SelectMany(c => c.Environments).Where(e => e.IsEnabled && string.IsNullOrWhiteSpace(e.AgentPort)).ToList();
                    if (pendingEnvs.Count == 1)
                    {
                        selectedEnv = pendingEnvs[0];
                        selectedEnv.AgentPort = requestedPort.ToString();
                        try { File.WriteAllText(configFile, js.Serialize(allClients)); } catch { }
                    }
                }

                if (selectedEnv == null) return;

                string targetExe = ResolveVersionExecutable(selectedEnv.VersionFolder);
                if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe)) return;

                BinaryPatcher.DisableAutoUpdate(targetExe);

                // Limpiar agentes genéricos en 21021 o desde Programs\web-agent que puedan interferir
                foreach (var p in portOwners)
                {
                    if (p.Port == 21021 || (!string.IsNullOrEmpty(p.ExecutablePath) && p.ExecutablePath.IndexOf("Programs\\web-agent\\", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        PortProcessHelper.KillProcessByIdSafe(p.Pid);
                    }
                }

                if (existingOnPort != null)
                {
                    if (string.Equals(existingOnPort.ExecutablePath, targetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        return; // Ya está corriendo la versión correcta
                    }
                    else
                    {
                        PortProcessHelper.KillProcessByIdSafe(existingOnPort.Pid);
                    }
                }

                string lockFile = Path.Combine(Path.GetTempPath(), "wam_port_" + requestedPort + ".lock");
                if (File.Exists(lockFile))
                {
                    try
                    {
                        string lockContent = File.ReadAllText(lockFile);
                        DateTime lastTime = File.GetLastWriteTimeUtc(lockFile);
                        if (string.Equals(lockContent.Trim(), targetExe, StringComparison.OrdinalIgnoreCase) &&
                            (DateTime.UtcNow - lastTime).TotalSeconds < 4.0 &&
                            Process.GetProcessesByName("web-agent").Length > 0)
                        {
                            return;
                        }
                    }
                    catch { }
                }
                try { File.WriteAllText(lockFile, targetExe); } catch { }

                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    Arguments = uriArg,
                    WorkingDirectory = Path.GetDirectoryName(targetExe),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static string ExtractPort(string serverIp)
        {
            if (string.IsNullOrWhiteSpace(serverIp)) return null;
            Match m = Regex.Match(serverIp, @":(\d{2,5})");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string PredictWebAgentPort(string serverIp)
        {
            string p = ExtractPort(serverIp);
            if (!string.IsNullOrEmpty(p) && p.Length >= 3)
            {
                return p.Substring(0, 3) + "41";
            }
            return "";
        }

        public static string ResolveVersionExecutable(string versionFolder)
        {
            if (string.IsNullOrWhiteSpace(versionFolder)) return null;

            string[] candidates = new string[]
            {
                Path.Combine(PathHelper.GetVersionsDir(), versionFolder, "web-agent.exe"),
                Path.Combine(PathHelper.GetExeDir(), "versions", versionFolder, "web-agent.exe")
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}

