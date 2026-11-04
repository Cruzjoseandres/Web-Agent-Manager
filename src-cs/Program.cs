using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WebAgentManagerApp
{
    public class EnvironmentItem
    {
        public string Id { get; set; }
        public string Name { get; set; }            // ej: "Producción", "Desarrollo", "QA"
        public string ServerIp { get; set; }        // ej: "192.168.0.10:50200"
        public string AgentPort { get; set; }       // ej: "50241"
        public string VersionFolder { get; set; }   // ej: "web-agent-1.1.1-x64"
        public string Description { get; set; }
        public bool IsEnabled { get; set; }

        public EnvironmentItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Producción";
            AgentPort = "";
            IsEnabled = true;
        }
    }

    public class ClientItem
    {
        public string Id { get; set; }
        public string Name { get; set; }            // ej: "Maprial", "Totalpec", "Nola", "Lafage"
        public string Description { get; set; }
        public List<EnvironmentItem> Environments { get; set; }

        // Propiedades de retrocompatibilidad para migrar clientes viejos si existen
        public string ServerIp { get; set; }
        public string VersionFolder { get; set; }
        public string AgentPorts { get; set; }

        public ClientItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Environments = new List<EnvironmentItem>();
            AgentPorts = "";
        }
    }

    public static class UiPresentationHelper
    {
        public static string NormalizeBrowserUrl(string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(serverAddress)) return null;

            string candidate = serverAddress.Trim();
            bool hasHttpScheme = candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            bool hasHttpsScheme = candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (!hasHttpScheme && !hasHttpsScheme)
            {
                if (candidate.IndexOf("://", StringComparison.Ordinal) >= 0) return null;
                candidate = "http://" + candidate;
            }

            Uri uri;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri)) return null;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return null;
            if (string.IsNullOrWhiteSpace(uri.Host)) return null;

            return candidate;
        }

        public static List<EnvironmentItem> GetVisibleEnvironments(ClientItem client, string searchText)
        {
            var environments = (client != null && client.Environments != null)
                ? client.Environments
                : new List<EnvironmentItem>();
            string query = (searchText ?? "").Trim();

            if (query.Length == 0 || ContainsText(client != null ? client.Name : null, query))
            {
                return new List<EnvironmentItem>(environments);
            }

            return environments.Where(e => e != null &&
                (ContainsText(e.Name, query) || ContainsText(e.ServerIp, query))).ToList();
        }

        public static bool ClientMatchesSearch(ClientItem client, string searchText)
        {
            string query = (searchText ?? "").Trim();
            if (query.Length == 0) return true;
            if (client == null) return false;

            return ContainsText(client.Name, query) || GetVisibleEnvironments(client, query).Count > 0;
        }

        public static bool ShouldShowEnvironments(bool isCollapsed, string searchText)
        {
            return !isCollapsed || !string.IsNullOrWhiteSpace(searchText);
        }

        public class ServerAddressParts
        {
            public bool IsHttps { get; set; }
            public string Host { get; set; }
            public string WebPort { get; set; }

            public string BuildUrl()
            {
                string h = (Host ?? "").Trim();
                if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) h = h.Substring(7);
                else if (h.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) h = h.Substring(8);

                int slashIdx = h.IndexOf('/');
                if (slashIdx >= 0) h = h.Substring(0, slashIdx);

                string p = (WebPort ?? "").Trim();
                int colonIdx = h.LastIndexOf(':');
                if (colonIdx >= 0)
                {
                    if (string.IsNullOrWhiteSpace(p)) p = h.Substring(colonIdx + 1);
                    h = h.Substring(0, colonIdx);
                }

                if (string.IsNullOrWhiteSpace(h)) return "";

                string scheme = IsHttps ? "https://" : "http://";
                string portPart = !string.IsNullOrWhiteSpace(p) ? ":" + p : "";
                return scheme + h + portPart + "/webapp/";
            }
        }

        public static ServerAddressParts ParseServerAddress(string raw)
        {
            var parts = new ServerAddressParts { IsHttps = false, Host = "", WebPort = "" };
            if (string.IsNullOrWhiteSpace(raw)) return parts;

            string s = raw.Trim();
            if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                parts.IsHttps = true;
                s = s.Substring(8);
            }
            else if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                parts.IsHttps = false;
                s = s.Substring(7);
            }

            int slashIdx = s.IndexOf('/');
            if (slashIdx >= 0) s = s.Substring(0, slashIdx);

            int colonIdx = s.LastIndexOf(':');
            if (colonIdx >= 0)
            {
                parts.Host = s.Substring(0, colonIdx);
                parts.WebPort = s.Substring(colonIdx + 1);
            }
            else
            {
                parts.Host = s;
                parts.WebPort = "";
            }
            return parts;
        }

        public static string BuildServerAddress(bool isHttps, string host, string webPort)
        {
            return new ServerAddressParts { IsHttps = isHttps, Host = host, WebPort = webPort }.BuildUrl();
        }

        private static bool ContainsText(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public class WebAgentVersion
    {
        public string FolderName { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
        public string ExecutablePath { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

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

    public static class PortProcessHelper
    {
        public class PortOwnerInfo
        {
            public int Port;
            public int Pid;
            public bool HasEstablishedConnection;
            public string ExecutablePath;
        }

        public static List<PortOwnerInfo> GetWebAgentPorts()
        {
            var result = new List<PortOwnerInfo>();
            try
            {
                var webAgentProcs = new Dictionary<int, string>();
                foreach (var p in Process.GetProcessesByName("web-agent"))
                {
                    try { webAgentProcs[p.Id] = p.MainModule.FileName ?? ""; }
                    catch { webAgentProcs[p.Id] = ""; }
                }
                if (webAgentProcs.Count == 0) return result;

                var pNetstat = Process.Start(new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                string output = pNetstat.StandardOutput.ReadToEnd();
                pNetstat.WaitForExit(2000);

                var listenMap = new Dictionary<int, PortOwnerInfo>();
                var establishedPorts = new HashSet<int>();

                string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    Match m = Regex.Match(line, @"^\s*TCP\s+\S+:(\d+)\s+\S+:(\d+)\s+(\S+)\s+(\d+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        int localPort;
                        int remotePort;
                        int pid;
                        if (int.TryParse(m.Groups[1].Value, out localPort) &&
                            int.TryParse(m.Groups[2].Value, out remotePort) &&
                            int.TryParse(m.Groups[4].Value, out pid))
                        {
                            string state = m.Groups[3].Value.ToUpperInvariant();
                            if (state == "LISTENING" && webAgentProcs.ContainsKey(pid))
                            {
                                listenMap[localPort] = new PortOwnerInfo
                                {
                                    Port = localPort,
                                    Pid = pid,
                                    ExecutablePath = webAgentProcs[pid],
                                    HasEstablishedConnection = false
                                };
                            }
                            else if (state == "SYN_SENT" && webAgentProcs.ContainsKey(pid) && remotePort > 0)
                            {
                                if (!listenMap.ContainsKey(remotePort))
                                {
                                    listenMap[remotePort] = new PortOwnerInfo
                                    {
                                        Port = remotePort,
                                        Pid = pid,
                                        ExecutablePath = webAgentProcs[pid],
                                        HasEstablishedConnection = true
                                    };
                                }
                            }
                            else if (state == "ESTABLISHED")
                            {
                                establishedPorts.Add(localPort);
                            }
                        }
                    }
                }

                foreach (var kv in listenMap)
                {
                    if (establishedPorts.Contains(kv.Key))
                    {
                        kv.Value.HasEstablishedConnection = true;
                    }
                    result.Add(kv.Value);
                }
            }
            catch { }
            return result;
        }

        public static int KillUnconnectedWebAgents()
        {
            int killed = 0;
            try
            {
                var ports = GetWebAgentPorts();
                var connectedPids = new HashSet<int>(ports.Where(x => x.HasEstablishedConnection).Select(x => x.Pid));

                foreach (var p in Process.GetProcessesByName("web-agent"))
                {
                    try
                    {
                        if (!connectedPids.Contains(p.Id))
                        {
                            bool recentlyStarted = false;
                            try
                            {
                                if ((DateTime.Now - p.StartTime).TotalSeconds < 12) recentlyStarted = true;
                            }
                            catch { }

                            if (!recentlyStarted)
                            {
                                p.Kill();
                                p.WaitForExit(1500);
                                killed++;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return killed;
        }

        public static void KillProcessByIdSafe(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit(1500);
                System.Threading.Thread.Sleep(150);
            }
            catch { }
        }
    }

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

    public class MainWindow : Window
    {
        private string exeDir;
        private string localRoot;
        private string versionsDir;
        private string configFile;
        private string toolsDir;
        private string regKeyPath = @"Software\Classes\web-agent\shell\open\command";

        private List<ClientItem> clients = new List<ClientItem>();
        private List<WebAgentVersion> detectedVersions = new List<WebAgentVersion>();
        private List<PortProcessHelper.PortOwnerInfo> lastActivePorts = new List<PortProcessHelper.PortOwnerInfo>();
        private readonly HashSet<string> collapsedClientKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer statusTimer;

        private class EnvVisualBinding
        {
            public string ClientId;
            public string EnvId;
            public EnvironmentItem Env;
            public Border CardBorder;
            public Border StatusPill;
            public TextBlock StatusTxt;
            public Button ActionBtn;
            public bool LastRunning;
            public int EnvPort;
        }

        private readonly List<EnvVisualBinding> envBindings = new List<EnvVisualBinding>();
        private bool isUpdatingLiveStatus = false;

        // UI Controls
        private Border statusIconCircle;
        private TextBlock statusIconText;
        private TextBlock txtActiveBadge;
        private TextBlock txtActiveDetails;
        private StackPanel cardsPanel;
        private TextBlock txtToast;
        private TextBox txtSearch;
        private TextBlock txtSearchSummary;

        public MainWindow()
        {
            InitializePaths();
            InitializeWindowLayout();
            CleanupRogueGlobalInstall();
            ScanAllVersions();
            LoadClients();
            SyncRegistryWithActiveClients();
            RefreshUI();
            StartStatusTimer();
        }

        private void StartStatusTimer()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(1.0);
            statusTimer.Tick += async (s, e) =>
            {
                await UpdateAllLiveStatusesAsync();
            };
            statusTimer.Start();
        }

        private async System.Threading.Tasks.Task UpdateAllLiveStatusesAsync()
        {
            if (isUpdatingLiveStatus) return;
            isUpdatingLiveStatus = true;
            try
            {
                SyncRegistryWithActiveClients();
                var activePorts = await System.Threading.Tasks.Task.Run(() => PortProcessHelper.GetWebAgentPorts());
                lastActivePorts = activePorts ?? new List<PortProcessHelper.PortOwnerInfo>();
                UpdateStatusBannerWithPorts(activePorts);
                UpdateEnvironmentCardsWithPorts(activePorts);
            }
            catch { }
            finally
            {
                isUpdatingLiveStatus = false;
            }
        }

        private void CleanupRogueGlobalInstall()
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

        private void InitializePaths()
        {
            localRoot = PathHelper.GetDataRoot();
            exeDir = PathHelper.GetExeDir();

            versionsDir = PathHelper.GetVersionsDir();
            configFile = PathHelper.GetConfigFile();
            toolsDir = Path.Combine(localRoot, "tools");

            if (!Directory.Exists(versionsDir)) Directory.CreateDirectory(versionsDir);
            if (!Directory.Exists(Path.Combine(localRoot, "config"))) Directory.CreateDirectory(Path.Combine(localRoot, "config"));
            if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);

            string portableCfg = Path.Combine(exeDir, "config", "clientes.json");
            if (!File.Exists(configFile) && File.Exists(portableCfg) &&
                !string.Equals(portableCfg, configFile, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(portableCfg, configFile); } catch { }
            }

            PathHelper.EnsureEmbeddedTools(toolsDir);
            PathHelper.GetPermanentExePath();
        }

        private void InitializeWindowLayout()
        {
            Title = "TOTVS WebAgent Manager";
            Width = 920;
            Height = 720;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
            FontFamily = new FontFamily("Segoe UI");

            try
            {
                using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (iconStream != null)
                    {
                        Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    }
                }
            }
            catch { }

            Grid mainGrid = new Grid();
            mainGrid.Margin = new Thickness(20);
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Banner
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cards List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. ENCABEZADO
            Border headerBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            StackPanel headerStack = new StackPanel();
            TextBlock titleTxt = new TextBlock
            {
                Text = "TOTVS Protheus - Gestor de WebAgent",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529"))
            };
            TextBlock subTxt = new TextBlock
            {
                Text = "Administra tus clientes y sus ambientes (Producción, Desarrollo, QA), con detección en tiempo real de los procesos de Windows.",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                Margin = new Thickness(0, 3, 0, 0)
            };
            headerStack.Children.Add(titleTxt);
            headerStack.Children.Add(subTxt);
            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. BANNER DE ESTADO ACTUAL (TIEMPO REAL EN WINDOWS)
            Border statusBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            Grid statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            statusIconCircle = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD")),
                CornerRadius = new CornerRadius(20),
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 14, 0)
            };
            statusIconText = new TextBlock
            {
                Text = "OK",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusIconCircle.Child = statusIconText;
            Grid.SetColumn(statusIconCircle, 0);
            statusGrid.Children.Add(statusIconCircle);

            StackPanel statusInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            StackPanel line1 = new StackPanel { Orientation = Orientation.Horizontal };
            line1.Children.Add(new TextBlock { Text = "Agentes en Windows: ", FontSize = 13, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")) });
            txtActiveBadge = new TextBlock { Text = "Cargando...", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132")) };
            line1.Children.Add(txtActiveBadge);
            statusInfo.Children.Add(line1);

            txtActiveDetails = new TextBlock
            {
                Text = "Verificando procesos y puertos en Windows...",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            statusInfo.Children.Add(txtActiveDetails);
            Grid.SetColumn(statusInfo, 1);
            statusGrid.Children.Add(statusInfo);

            Button btnRefresh = CreateStyledButton("\u21BB Actualizar", "#6C757D", false);
            btnRefresh.Click += (s, e) => { ScanAllVersions(); RefreshUI(); ShowToast("Estado actualizado."); };
            Grid.SetColumn(btnRefresh, 2);
            statusGrid.Children.Add(btnRefresh);

            statusBorder.Child = statusGrid;
            Grid.SetRow(statusBorder, 1);
            mainGrid.Children.Add(statusBorder);

            // 3. BUSCADOR Y BARRA DE BOTONES (TOOLBAR)
            StackPanel toolbarSection = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 14)
            };

            Border searchBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEE2E6")),
                BorderThickness = new Thickness(1)
            };
            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock searchLabel = new TextBlock
            {
                Text = "Buscar:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343A40")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(searchLabel, 0);
            searchGrid.Children.Add(searchLabel);

            txtSearch = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(9, 6, 9, 6),
                MaxLength = 200,
                ToolTip = "Busca por nombre de cliente, ambiente o dirección IP/URL."
            };
            Grid.SetColumn(txtSearch, 1);
            searchGrid.Children.Add(txtSearch);

            Button btnClearSearch = CreateStyledButton("Limpiar", "#6C757D", false);
            btnClearSearch.Margin = new Thickness(8, 0, 0, 0);
            btnClearSearch.IsEnabled = false;
            btnClearSearch.Opacity = 0.55;
            btnClearSearch.Click += (s, e) =>
            {
                txtSearch.Clear();
                txtSearch.Focus();
            };
            Grid.SetColumn(btnClearSearch, 2);
            searchGrid.Children.Add(btnClearSearch);

            txtSearchSummary = new TextBlock
            {
                Text = "Todos los clientes",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(txtSearchSummary, 3);
            searchGrid.Children.Add(txtSearchSummary);

            txtSearch.TextChanged += (s, e) =>
            {
                bool hasSearch = !string.IsNullOrWhiteSpace(txtSearch.Text);
                btnClearSearch.IsEnabled = hasSearch;
                btnClearSearch.Opacity = hasSearch ? 1.0 : 0.55;
                RefreshUI(false);
            };

            searchBorder.Child = searchGrid;
            toolbarSection.Children.Add(searchBorder);

            WrapPanel toolbar = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            Button btnNewClient = CreateStyledButton("+ Nuevo Cliente", "#0D6EFD", true);
            btnNewClient.Padding = new Thickness(16, 9, 16, 9);
            btnNewClient.Click += (s, e) => OpenClientDialog(null);
            toolbar.Children.Add(btnNewClient);

            Button btnScan = CreateStyledButton("\U0001F50D Escanear Equipo", "#495057", false);
            btnScan.Margin = new Thickness(10, 0, 0, 0);
            btnScan.Click += (s, e) => {
                int found = ScanAllVersions();
                RefreshUI();
                MessageBox.Show("Se encontraron " + detectedVersions.Count + " versiones de WebAgent listas y protegidas.", "Escaneo Completo", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            toolbar.Children.Add(btnScan);

            Button btnImport = CreateStyledButton("\U0001F4E5 Importar Instalador (.exe)", "#198754", false);
            btnImport.Margin = new Thickness(10, 0, 0, 0);
            btnImport.Click += (s, e) => ImportInstallerManual();
            toolbar.Children.Add(btnImport);

            Button btnExport = CreateStyledButton("\U0001F4E4 Exportar Config (TI)", "#495057", false);
            btnExport.Margin = new Thickness(10, 0, 0, 0);
            btnExport.ToolTip = "Exporta todos los clientes y ambientes a un archivo JSON para compartir con el equipo.";
            btnExport.Click += (s, e) => ExportConfiguration();
            toolbar.Children.Add(btnExport);

            Button btnImportCfg = CreateStyledButton("\U0001F4C2 Importar Config", "#495057", false);
            btnImportCfg.Margin = new Thickness(10, 0, 0, 0);
            btnImportCfg.ToolTip = "Importa un archivo JSON de clientes y ambientes generado por TI o compañeros.";
            btnImportCfg.Click += (s, e) => ImportConfiguration();
            toolbar.Children.Add(btnImportCfg);

            Button btnKillIdle = CreateStyledButton("\U0001F9F9 Limpiar Puertos Libres", "#6C757D", false);
            btnKillIdle.Margin = new Thickness(10, 0, 0, 0);
            btnKillIdle.ToolTip = "Cierra procesos en espera sin desconectar los que tienen una pestaña activa.";
            btnKillIdle.Click += (s, e) => {
                int killed = PortProcessHelper.KillUnconnectedWebAgents();
                RefreshUI();
                ShowToast("Se limpiaron " + killed + " procesos inactivos.");
            };
            toolbar.Children.Add(btnKillIdle);

            toolbarSection.Children.Add(toolbar);
            Grid.SetRow(toolbarSection, 2);
            mainGrid.Children.Add(toolbarSection);

            // 4. LISTA DE TARJETAS DE CLIENTES Y AMBIENTES
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Margin = new Thickness(0, 0, 0, 14)
            };
            cardsPanel = new StackPanel { Orientation = Orientation.Vertical };
            scroll.Content = cardsPanel;
            Grid.SetRow(scroll, 3);
            mainGrid.Children.Add(scroll);

            // 5. BARRA INFERIOR (FOOTER)
            Border footerBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            Grid footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtToast = new TextBlock
            {
                Text = "Listo para operar.",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtToast, 0);
            footerGrid.Children.Add(txtToast);

            Button btnKillAll = CreateStyledButton("\u26A1 Detener Todos los Agentes", "#DC3545", false);
            btnKillAll.ToolTip = "Finaliza todos los procesos web-agent.exe que se estén ejecutando en Windows.";
            btnKillAll.Click += (s, e) => {
                var res = MessageBox.Show("¿Deseas cerrar todos los procesos de WebAgent que se estén ejecutando en Windows?", "Confirmar detención total", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    StopAllWebAgents();
                    RefreshUI();
                    ShowToast("Todos los agentes en Windows fueron detenidos.");
                }
            };
            Grid.SetColumn(btnKillAll, 1);
            footerGrid.Children.Add(btnKillAll);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 4);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private Button CreateStyledButton(string text, string hexColor, bool isPrimary)
        {
            Button btn = new Button
            {
                Content = text,
                Cursor = Cursors.Hand,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor))
            };

            if (isPrimary)
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.Background = Brushes.White;
                btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
            }

            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var elemFactory = new FrameworkElementFactory(typeof(Border));
            elemFactory.Name = "border";
            elemFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            elemFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            elemFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            elemFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
            elemFactory.AppendChild(contentFactory);

            template.VisualTree = elemFactory;

            var triggerOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            triggerOver.Setters.Add(new Setter { TargetName = "border", Property = Border.OpacityProperty, Value = 0.85 });
            template.Triggers.Add(triggerOver);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            btn.Style = style;

            return btn;
        }

        // ================= ESCANEO Y GESTIÓN DE VERSIONES =================
        public int ScanAllVersions()
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
                                bool ok = ExtractInstaller(f, folderName);
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

            detectedVersions = foundMap.Values.OrderBy(v => v.FolderName).ToList();
            return detectedVersions.Count;
        }

        private WebAgentVersion ParseVersionFromFolder(string folderPath, string exePath)
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

        private bool ExtractInstaller(string installerPath, string targetFolderName)
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

        private void ImportInstallerManual()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Seleccionar Instalador Oficial de WebAgent (.exe)";
            dlg.Filter = "Instaladores WebAgent (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                string folderName = "";
                Match m = Regex.Match(Path.GetFileName(dlg.FileName), @"web-agent-([0-9.]+)-windows-(x64|x86)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    folderName = "web-agent-" + m.Groups[1].Value + "-" + m.Groups[2].Value.ToLower();
                }
                else
                {
                    folderName = "web-agent-importado-" + DateTime.Now.ToString("yyyyMMdd");
                }

                bool ok = ExtractInstaller(dlg.FileName, folderName);
                if (ok)
                {
                    ScanAllVersions();
                    RefreshUI();
                    MessageBox.Show("La versión " + folderName + " fue importada exitosamente y ya está disponible en tu lista.", "Importación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No se pudo extraer el instalador seleccionado. Asegúrate de que sea un archivo de instalación oficial de WebAgent.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string CalculateMD5(string filename)
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

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        // ================= GESTIÓN DE CLIENTES Y AMBIENTES =================
        private void LoadClients()
        {
            if (File.Exists(configFile))
            {
                try
                {
                    string json = File.ReadAllText(configFile, Encoding.UTF8);
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    clients = js.Deserialize<List<ClientItem>>(json) ?? new List<ClientItem>();
                    clients = clients.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
                }
                catch { clients = new List<ClientItem>(); }
            }
            if (clients == null) clients = new List<ClientItem>();

            bool migrated = MigrateLegacyClients(clients);
            if (migrated || !File.Exists(configFile))
            {
                SaveClients();
            }
        }

        private bool MigrateLegacyClients(List<ClientItem> list)
        {
            if (list == null) return false;
            bool needsMigration = list.Any(c => c.Environments == null || c.Environments.Count == 0);
            if (!needsMigration) return false;

            var newClientsMap = new Dictionary<string, ClientItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var legacy in list)
            {
                if (legacy.Environments != null && legacy.Environments.Count > 0)
                {
                    if (!newClientsMap.ContainsKey(legacy.Name))
                        newClientsMap[legacy.Name] = legacy;
                    else
                        newClientsMap[legacy.Name].Environments.AddRange(legacy.Environments);
                    continue;
                }

                string rawName = (legacy.Name ?? "").Trim();
                string clientName = rawName;
                string envName = "Producción";

                if (rawName.IndexOf("Des", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rawName.IndexOf("Dev", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    envName = "Desarrollo";
                    clientName = Regex.Replace(rawName, @"\b(Des|Dev|Desarrollo)\b", "", RegexOptions.IgnoreCase).Trim(new char[] { ' ', '-', '_' });
                    if (string.IsNullOrWhiteSpace(clientName)) clientName = "Maprial";
                }
                else if (rawName.IndexOf("Prod", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    envName = "Producción";
                    clientName = Regex.Replace(rawName, @"\b(Prod|Produccion|Producción)\b", "", RegexOptions.IgnoreCase).Trim(new char[] { ' ', '-', '_' });
                    if (string.IsNullOrWhiteSpace(clientName)) clientName = rawName;
                }
                else if (rawName.IndexOf("Totalpec", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clientName = "Totalpec";
                    envName = "Producción";
                }

                if (!newClientsMap.ContainsKey(clientName))
                {
                    newClientsMap[clientName] = new ClientItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = clientName,
                        Description = legacy.Description,
                        Environments = new List<EnvironmentItem>()
                    };
                }

                var env = new EnvironmentItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = envName,
                    ServerIp = legacy.ServerIp,
                    AgentPort = legacy.AgentPorts,
                    VersionFolder = legacy.VersionFolder,
                    Description = legacy.Description,
                    IsEnabled = true
                };
                newClientsMap[clientName].Environments.Add(env);
            }

            list.Clear();
            list.AddRange(newClientsMap.Values);
            return true;
        }

        public void SaveClients()
        {
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var cleanList = clients.Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Environments = (c.Environments ?? new List<EnvironmentItem>()).Select(e => new
                    {
                        Id = e.Id,
                        Name = e.Name,
                        ServerIp = e.ServerIp,
                        AgentPort = e.AgentPort,
                        VersionFolder = e.VersionFolder,
                        Description = e.Description,
                        IsEnabled = e.IsEnabled
                    }).ToList()
                }).ToList();

                string json = js.Serialize(cleanList);
                File.WriteAllText(configFile, FormatJson(json), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar clientes: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            var sb = new StringBuilder();
            bool inQuote = false;
            int indent = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '\"')
                {
                    if (i > 0 && json[i - 1] == '\\') { }
                    else inQuote = !inQuote;
                    sb.Append(ch);
                }
                else if (inQuote)
                {
                    sb.Append(ch);
                }
                else if (ch == '{' || ch == '[')
                {
                    sb.Append(ch);
                    sb.Append("\r\n");
                    indent++;
                    sb.Append(new string(' ', indent * 4));
                }
                else if (ch == '}' || ch == ']')
                {
                    sb.Append("\r\n");
                    indent = Math.Max(0, indent - 1);
                    sb.Append(new string(' ', indent * 4));
                    sb.Append(ch);
                }
                else if (ch == ',')
                {
                    sb.Append(ch);
                    sb.Append("\r\n");
                    sb.Append(new string(' ', indent * 4));
                }
                else if (ch == ':')
                {
                    sb.Append(": ");
                }
                else if (!char.IsWhiteSpace(ch))
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private void ExportConfiguration()
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Title = "Exportar Configuración de Ambientes Protheus (TI)",
                    FileName = "ambientes_protheus_webagent.json",
                    Filter = "Archivo JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
                    DefaultExt = ".json"
                };
                if (sfd.ShowDialog() == true)
                {
                    // Exportar clientes y ambientes limpios de flags de sesión local
                    var exportList = new List<ClientItem>();
                    foreach (var c in clients)
                    {
                        var copyC = new ClientItem
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Description = c.Description,
                            Environments = new List<EnvironmentItem>()
                        };
                        if (c.Environments != null)
                        {
                            foreach (var e in c.Environments)
                            {
                                copyC.Environments.Add(new EnvironmentItem
                                {
                                    Id = e.Id,
                                    Name = e.Name,
                                    ServerIp = e.ServerIp,
                                    AgentPort = e.AgentPort,
                                    VersionFolder = e.VersionFolder,
                                    Description = e.Description,
                                    IsEnabled = true
                                });
                            }
                        }
                        exportList.Add(copyC);
                    }

                    JavaScriptSerializer js = new JavaScriptSerializer();
                    string json = js.Serialize(exportList);
                    File.WriteAllText(sfd.FileName, FormatJson(json), Encoding.UTF8);
                    ShowToast("Configuración exportada correctamente.");
                    MessageBox.Show("Configuración de ambientes exportada exitosamente a:\n" + sfd.FileName + "\n\nEste archivo contiene todos los clientes, ambientes, versiones y puertos.\nPuedes compartirlo con el equipo para que lo importen con 1 clic.", "Exportación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar configuración: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportConfiguration()
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Importar Configuración de Ambientes Protheus",
                    Filter = "Archivo JSON (*.json)|*.json|Todos los archivos (*.*)|*.*"
                };
                if (ofd.ShowDialog() == true)
                {
                    string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    List<ClientItem> imported = js.Deserialize<List<ClientItem>>(json);
                    if (imported == null || imported.Count == 0)
                    {
                        MessageBox.Show("El archivo seleccionado no contiene clientes válidos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MigrateLegacyClients(imported);

                    int addedClients = 0;
                    int addedEnvs = 0;
                    int updatedEnvs = 0;

                    foreach (var imp in imported)
                    {
                        if (string.IsNullOrWhiteSpace(imp.Name)) continue;
                        var existing = clients.FirstOrDefault(c => string.Equals(c.Name, imp.Name, StringComparison.OrdinalIgnoreCase));

                        if (existing == null)
                        {
                            clients.Add(imp);
                            addedClients++;
                            addedEnvs += (imp.Environments != null ? imp.Environments.Count : 0);
                        }
                        else
                        {
                            existing.Description = imp.Description;
                            if (imp.Environments != null)
                            {
                                if (existing.Environments == null) existing.Environments = new List<EnvironmentItem>();
                                foreach (var impEnv in imp.Environments)
                                {
                                    var exEnv = existing.Environments.FirstOrDefault(e => string.Equals(e.Name, impEnv.Name, StringComparison.OrdinalIgnoreCase));
                                    if (exEnv != null)
                                    {
                                        exEnv.ServerIp = impEnv.ServerIp;
                                        exEnv.AgentPort = impEnv.AgentPort;
                                        exEnv.VersionFolder = impEnv.VersionFolder;
                                        exEnv.Description = impEnv.Description;
                                        exEnv.IsEnabled = impEnv.IsEnabled;
                                        updatedEnvs++;
                                    }
                                    else
                                    {
                                        existing.Environments.Add(impEnv);
                                        addedEnvs++;
                                    }
                                }
                            }
                        }
                    }

                    SaveClients();
                    SyncRegistryWithActiveClients();
                    RefreshUI();
                    ShowToast("Configuración importada exitosamente.");
                    MessageBox.Show(string.Format("Configuración importada exitosamente:\n- {0} cliente(s) nuevo(s)\n- {1} ambiente(s) nuevo(s)\n- {2} ambiente(s) actualizados\n\nTodos los puertos y versiones ya quedaron listos.", addedClients, addedEnvs, updatedEnvs), "Importación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al importar configuración: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncRegistryWithActiveClients()
        {
            bool hasAnyEnabled = clients.Any(c => c.Environments != null && c.Environments.Any(e => e.IsEnabled));
            try
            {
                using (RegistryKey rootKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\web-agent"))
                {
                    if (rootKey != null)
                    {
                        rootKey.SetValue("", "URL:web-agent");
                        rootKey.SetValue("URL Protocol", "");
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(regKeyPath))
                {
                    if (!hasAnyEnabled)
                    {
                        key.SetValue("", "");
                    }
                    else
                    {
                        string permExe = PathHelper.GetPermanentExePath();
                        key.SetValue("", "\"" + permExe + "\" \"%1\"");
                    }
                }
            }
            catch { }
        }

        public async void StartEnvironment(EnvironmentItem env)
        {
            string targetFolder = env.VersionFolder;
            string targetExe = SmartRouter.ResolveVersionExecutable(targetFolder);
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                MessageBox.Show("No se encontró el ejecutable de la versión: " + targetFolder + ".\nUsa el botón 'Importar Instalador (.exe)' para agregarla.", "Versión no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BinaryPatcher.DisableAutoUpdate(targetExe);

            int port = 0;
            int.TryParse(env.AgentPort, out port);
            if (port <= 0)
            {
                string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                int.TryParse(pred, out port);
            }
            if (port <= 0) port = 21021;

            // 1. Limpiar cualquier WebAgent rogue (ej: el que inicia por defecto en el puerto 21021 o desde Programs\web-agent)
            var allRunningPorts = PortProcessHelper.GetWebAgentPorts();
            foreach (var rogue in allRunningPorts)
            {
                if (rogue.Port == 21021 || (!string.IsNullOrEmpty(rogue.ExecutablePath) && rogue.ExecutablePath.IndexOf("Programs\\web-agent\\", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    PortProcessHelper.KillProcessByIdSafe(rogue.Pid);
                }
            }

            // 2. Si ya hay un proceso en este puerto específico
            allRunningPorts = PortProcessHelper.GetWebAgentPorts();
            var existing = allRunningPorts.FirstOrDefault(p => p.Port == port);
            if (existing != null)
            {
                if (string.Equals(existing.ExecutablePath, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("El agente ya está en ejecución en el puerto " + port + ".");
                    await UpdateAllLiveStatusesAsync();
                    return;
                }
                PortProcessHelper.KillProcessByIdSafe(existing.Pid);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    Arguments = "web-agent:?port=" + port,
                    WorkingDirectory = Path.GetDirectoryName(targetExe),
                    UseShellExecute = true
                });
                ShowToast("Iniciando WebAgent en puerto " + port + "...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar WebAgent: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await System.Threading.Tasks.Task.Delay(500);
            await UpdateAllLiveStatusesAsync();
        }

        public async void StopEnvironment(EnvironmentItem env)
        {
            int port = 0;
            int.TryParse(env.AgentPort, out port);
            if (port <= 0)
            {
                string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                int.TryParse(pred, out port);
            }

            if (port > 0)
            {
                var ports = PortProcessHelper.GetWebAgentPorts();
                var existing = ports.FirstOrDefault(p => p.Port == port);
                if (existing != null)
                {
                    PortProcessHelper.KillProcessByIdSafe(existing.Pid);
                    ShowToast("WebAgent en puerto " + port + " detenido.");
                }
            }
            await System.Threading.Tasks.Task.Delay(300);
            await UpdateAllLiveStatusesAsync();
        }

        public async void StopAllWebAgents()
        {
            foreach (var proc in Process.GetProcessesByName("web-agent"))
            {
                try { proc.Kill(); proc.WaitForExit(1000); } catch { }
            }
            ShowToast("Todos los procesos de WebAgent han sido detenidos.");
            await System.Threading.Tasks.Task.Delay(300);
            await UpdateAllLiveStatusesAsync();
        }

        // ================= VISTA Y TARJETAS EN TIEMPO REAL =================
        private void UpdateStatusBannerWithPorts(List<PortProcessHelper.PortOwnerInfo> activePorts)
        {
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();
            var runningEnvsList = new List<string>();

            foreach (var po in activePorts)
            {
                bool matched = false;
                foreach (var c in clients)
                {
                    if (c.Environments == null) continue;
                    foreach (var e in c.Environments)
                    {
                        if (SmartRouter.EnvironmentMatchesPort(e, po.Port))
                        {
                            runningEnvsList.Add(string.Format("{0} > {1} (Port {2})", c.Name, e.Name, po.Port));
                            matched = true;
                            break;
                        }
                    }
                    if (matched) break;
                }
                if (!matched)
                {
                    if (po.Port == 21021)
                    {
                        runningEnvsList.Add("WebAgent Genérico TOTVS (Port 21021)");
                    }
                    else
                    {
                        runningEnvsList.Add("WebAgent activo (Port " + po.Port + ")");
                    }
                }
            }

            if (runningEnvsList.Count > 0)
            {
                bool hasRogue21021 = activePorts.Any(p => p.Port == 21021);
                statusIconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#FFF3CD" : "#D1E7DD"));
                statusIconText.Text = hasRogue21021 ? "!" : "OK";
                statusIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#664D03" : "#0F5132"));

                txtActiveBadge.Text = string.Join("  \u2022  ", runningEnvsList.ToArray());
                txtActiveBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#664D03" : "#0F5132"));

                if (hasRogue21021 && !activePorts.Any(p => p.Port != 21021))
                {
                    txtActiveDetails.Text = "Detectado instalador genérico en puerto 21021. Para entrar a Maprial BIB u otro ambiente, pulsa '\u25B6 Iniciar' en su tarjeta.";
                }
                else
                {
                    txtActiveDetails.Text = string.Format("Tienes {0} proceso(s) de WebAgent en ejecución real en este equipo.", runningEnvsList.Count);
                }
            }
            else
            {
                statusIconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                statusIconText.Text = "OFF";
                statusIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));

                txtActiveBadge.Text = "Ninguno (WebAgent en reposo / Detenido)";
                txtActiveBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                txtActiveDetails.Text = "Los agentes se iniciarán automáticamente al navegar en Protheus o puedes pulsar 'Iniciar' en cualquiera de tus ambientes.";
            }
        }

        private void UpdateEnvironmentCardsWithPorts(List<PortProcessHelper.PortOwnerInfo> activePorts)
        {
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();

            foreach (var b in envBindings)
            {
                int envPort = b.EnvPort;
                PortProcessHelper.PortOwnerInfo po = (envPort > 0) ? activePorts.FirstOrDefault(p => p.Port == envPort) : null;
                bool isRunning = (po != null);

                if (isRunning)
                {
                    b.CardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FFF4"));
                    b.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
                    b.CardBorder.BorderThickness = new Thickness(2);

                    b.StatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD"));
                    b.StatusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132"));
                    if (po != null && po.HasEstablishedConnection)
                    {
                        b.StatusTxt.Text = "\u25CF Conectado al Navegador (PID " + po.Pid + ")";
                    }
                    else
                    {
                        b.StatusTxt.Text = "\u25CF En ejecución (Port " + envPort + " \u2022 PID " + (po != null ? po.Pid.ToString() : "") + ")";
                    }

                    b.ActionBtn.Content = "\u25FC Detener";
                    b.ActionBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    b.ActionBtn.Foreground = Brushes.White;
                    b.ActionBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    b.ActionBtn.Tag = "STOP";
                    b.LastRunning = true;
                }
                else
                {
                    b.CardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
                    b.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                    b.CardBorder.BorderThickness = new Thickness(1);

                    b.StatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                    b.StatusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                    b.StatusTxt.Text = "\u25CB Detenido";

                    b.ActionBtn.Content = "\u25B6 Iniciar";
                    b.ActionBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
                    b.ActionBtn.Foreground = Brushes.White;
                    b.ActionBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
                    b.ActionBtn.Tag = "START";
                    b.LastRunning = false;
                }
            }
        }

        private void RefreshUI(bool refreshActivePorts = true)
        {
            List<PortProcessHelper.PortOwnerInfo> activePorts = refreshActivePorts
                ? PortProcessHelper.GetWebAgentPorts()
                : lastActivePorts;
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();
            lastActivePorts = activePorts;
            UpdateStatusBannerWithPorts(activePorts);
            cardsPanel.Children.Clear();
            envBindings.Clear();

            string searchText = txtSearch != null ? txtSearch.Text.Trim() : "";

            if (clients.Count == 0)
            {
                if (txtSearchSummary != null) txtSearchSummary.Text = "0 clientes";
                Border emptyState = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(30),
                    Margin = new Thickness(0, 10, 0, 0),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                    BorderThickness = new Thickness(1)
                };
                StackPanel sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = "No tienes ningún cliente registrado todavía.",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = "Haz clic en '+ Nuevo Cliente' para agregar tu primer cliente, o usa 'Importar Config' si TI te compartió un archivo.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyState.Child = sp;
                cardsPanel.Children.Add(emptyState);
                return;
            }

            int visibleClientCount = 0;
            int visibleEnvironmentCount = 0;

            foreach (var cli in clients)
            {
                if (string.IsNullOrWhiteSpace(cli.Name)) continue;
                if (!UiPresentationHelper.ClientMatchesSearch(cli, searchText)) continue;

                List<EnvironmentItem> visibleEnvironments = UiPresentationHelper.GetVisibleEnvironments(cli, searchText);
                visibleClientCount++;
                visibleEnvironmentCount += visibleEnvironments.Count;
                string clientKey = !string.IsNullOrWhiteSpace(cli.Id) ? cli.Id : "name:" + cli.Name;
                bool isCollapsed = collapsedClientKeys.Contains(clientKey);
                bool showEnvironments = UiPresentationHelper.ShouldShowEnvironments(isCollapsed, searchText);

                Border clientCard = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 16, 18, 16),
                    Margin = new Thickness(0, 0, 0, 14),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"))
                };

                StackPanel cardContent = new StackPanel();

                // Cabecera del Cliente
                Grid headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel titleStack = new StackPanel();
                TextBlock nameTxt = new TextBlock
                {
                    Text = "\U0001F3E2  " + cli.Name,
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529"))
                };
                titleStack.Children.Add(nameTxt);

                string descPart = !string.IsNullOrWhiteSpace(cli.Description) ? cli.Description + "  \u2022  " : "";
                int countEnv = cli.Environments != null ? cli.Environments.Count : 0;
                string environmentSummary = countEnv + " ambiente(s) registrado(s)";
                if (!string.IsNullOrWhiteSpace(searchText) && visibleEnvironments.Count != countEnv)
                {
                    environmentSummary = visibleEnvironments.Count + " coincidencia(s) de " + countEnv + " ambiente(s)";
                }
                TextBlock subTxt = new TextBlock
                {
                    Text = descPart + environmentSummary,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                titleStack.Children.Add(subTxt);
                Grid.SetColumn(titleStack, 0);
                headerGrid.Children.Add(titleStack);

                StackPanel clientActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                ClientItem curCli = cli;
                string currentClientKey = clientKey;
                Button btnCollapse = CreateStyledButton(
                    showEnvironments ? "\u25B2 Ocultar (" + visibleEnvironments.Count + ")" : "\u25BC Mostrar (" + countEnv + ")",
                    "#495057",
                    false);
                btnCollapse.Margin = new Thickness(0, 0, 8, 0);
                btnCollapse.ToolTip = string.IsNullOrWhiteSpace(searchText)
                    ? "Muestra u oculta los ambientes de este cliente."
                    : "Los resultados permanecen visibles mientras hay una búsqueda activa.";
                btnCollapse.IsEnabled = string.IsNullOrWhiteSpace(searchText);
                btnCollapse.Opacity = btnCollapse.IsEnabled ? 1.0 : 0.65;
                btnCollapse.Click += (s, e) =>
                {
                    if (collapsedClientKeys.Contains(currentClientKey))
                        collapsedClientKeys.Remove(currentClientKey);
                    else
                        collapsedClientKeys.Add(currentClientKey);
                    RefreshUI(false);
                };
                clientActions.Children.Add(btnCollapse);

                Button btnAddEnv = CreateStyledButton("+ Agregar Ambiente", "#0D6EFD", true);
                btnAddEnv.Margin = new Thickness(0, 0, 8, 0);
                btnAddEnv.Click += (s, e) => OpenEnvironmentDialog(curCli, null);
                clientActions.Children.Add(btnAddEnv);

                Button btnEditCli = CreateStyledButton("Editar", "#6C757D", false);
                btnEditCli.Margin = new Thickness(0, 0, 8, 0);
                btnEditCli.Click += (s, e) => OpenClientDialog(curCli);
                clientActions.Children.Add(btnEditCli);

                Button btnDelCli = CreateStyledButton("Eliminar", "#DC3545", false);
                btnDelCli.Click += (s, e) => {
                    var res = MessageBox.Show("¿Seguro que deseas eliminar el cliente '" + curCli.Name + "' y todos sus ambientes?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        if (curCli.Environments != null)
                        {
                            foreach (var env in curCli.Environments) StopEnvironment(env);
                        }
                        clients.Remove(curCli);
                        SaveClients();
                        SyncRegistryWithActiveClients();
                        RefreshUI();
                        ShowToast("Cliente eliminado.");
                    }
                };
                clientActions.Children.Add(btnDelCli);

                Grid.SetColumn(clientActions, 1);
                headerGrid.Children.Add(clientActions);

                cardContent.Children.Add(headerGrid);

                // Lista de Ambientes dentro del Cliente
                StackPanel envsPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };

                if (showEnvironments && visibleEnvironments.Count == 0)
                {
                    Border emptyEnv = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    emptyEnv.Child = new TextBlock
                    {
                        Text = "Este cliente aún no tiene ambientes configurados. Haz clic en '+ Agregar Ambiente' arriba para registrar Producción, Desarrollo, etc.",
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                        FontStyle = FontStyles.Italic
                    };
                    envsPanel.Children.Add(emptyEnv);
                }
                else if (showEnvironments)
                {
                    foreach (var env in visibleEnvironments)
                    {
                        int envPort = 0;
                        int.TryParse(env.AgentPort, out envPort);
                        if (envPort <= 0)
                        {
                            string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                            int.TryParse(pred, out envPort);
                        }

                        PortProcessHelper.PortOwnerInfo po = (envPort > 0) ? activePorts.FirstOrDefault(p => p.Port == envPort) : null;
                        bool isRunning = (po != null);

                        Border envCard = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#F0FFF4" : "#F8F9FA")),
                            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#86EFAC" : "#E9ECEF")),
                            BorderThickness = new Thickness(isRunning ? 2 : 1),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(14, 10, 14, 10),
                            Margin = new Thickness(0, 0, 0, 8)
                        };

                        Grid envGrid = new Grid();
                        envGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        envGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        StackPanel envInfo = new StackPanel();

                        // Línea 1: Nombre del ambiente + Badges
                        StackPanel r1 = new StackPanel { Orientation = Orientation.Horizontal };
                        TextBlock envTitle = new TextBlock
                        {
                            Text = "\U0001F4CC " + env.Name,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529")),
                            Margin = new Thickness(0, 0, 10, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        r1.Children.Add(envTitle);

                        Border verBadge = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        verBadge.Child = new TextBlock
                        {
                            Text = env.VersionFolder ?? "web-agent",
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold
                        };
                        r1.Children.Add(verBadge);

                        Border portBadge = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        portBadge.Child = new TextBlock
                        {
                            Text = "Puerto: " + (envPort > 0 ? envPort.ToString() : "(auto)"),
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold
                        };
                        r1.Children.Add(portBadge);

                        envInfo.Children.Add(r1);

                        // Línea 2: Servidor / IP y notas
                        if (!string.IsNullOrWhiteSpace(env.ServerIp) || !string.IsNullOrWhiteSpace(env.Description))
                        {
                            TextBlock r2 = new TextBlock
                            {
                                FontSize = 12,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                                Margin = new Thickness(0, 3, 0, 0)
                            };

                            if (!string.IsNullOrWhiteSpace(env.ServerIp))
                            {
                                r2.Inlines.Add(new Run("Servidor: "));
                                string browserUrl = UiPresentationHelper.NormalizeBrowserUrl(env.ServerIp);
                                if (!string.IsNullOrEmpty(browserUrl))
                                {
                                    string currentBrowserUrl = browserUrl;
                                    Hyperlink serverLink = new Hyperlink(new Run(env.ServerIp.Trim()))
                                    {
                                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                                        Cursor = Cursors.Hand,
                                        ToolTip = "Abrir en el navegador predeterminado"
                                    };
                                    serverLink.Click += (s, e) =>
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo
                                            {
                                                FileName = currentBrowserUrl,
                                                UseShellExecute = true
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("No se pudo abrir la dirección en el navegador:\n" + ex.Message, "No se pudo abrir el enlace", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        }
                                        e.Handled = true;
                                    };
                                    r2.Inlines.Add(serverLink);
                                }
                                else
                                {
                                    r2.Inlines.Add(new Run(env.ServerIp.Trim()));
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(env.Description))
                            {
                                string separator = !string.IsNullOrWhiteSpace(env.ServerIp) ? "  |  " : "";
                                r2.Inlines.Add(new Run(separator + env.Description));
                            }
                            envInfo.Children.Add(r2);
                        }

                        // Línea 3: Estado Real en Windows
                        StackPanel r3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
                        Border statusPill = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(8, 2, 8, 2)
                        };
                        TextBlock statusTxt = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold };

                        if (isRunning)
                        {
                            statusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD"));
                            statusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132"));
                            if (po != null && po.HasEstablishedConnection)
                            {
                                statusTxt.Text = "\u25CF Conectado al Navegador (PID " + po.Pid + ")";
                            }
                            else
                            {
                                statusTxt.Text = "\u25CF En ejecución (Port " + envPort + " \u2022 PID " + po.Pid + ")";
                            }
                        }
                        else
                        {
                            statusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                            statusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                            statusTxt.Text = "\u25CB Detenido";
                        }
                        statusPill.Child = statusTxt;
                        r3.Children.Add(statusPill);
                        envInfo.Children.Add(r3);

                        Grid.SetColumn(envInfo, 0);
                        envGrid.Children.Add(envInfo);

                        // Columna de Acciones del Ambiente
                        StackPanel envActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                        EnvironmentItem curEnv = env;
                        Button btnToggle = CreateStyledButton(isRunning ? "\u25FC Detener" : "\u25B6 Iniciar", isRunning ? "#DC3545" : "#198754", true);
                        btnToggle.Margin = new Thickness(0, 0, 6, 0);
                        btnToggle.Tag = isRunning ? "STOP" : "START";
                        btnToggle.Click += (s, e) =>
                        {
                            if ((string)btnToggle.Tag == "STOP")
                                StopEnvironment(curEnv);
                            else
                                StartEnvironment(curEnv);
                        };
                        envActions.Children.Add(btnToggle);

                        Button btnEditEnv = CreateStyledButton("Editar", "#6C757D", false);
                        btnEditEnv.Margin = new Thickness(0, 0, 6, 0);
                        btnEditEnv.Click += (s, e) => OpenEnvironmentDialog(curCli, curEnv);
                        envActions.Children.Add(btnEditEnv);

                        Button btnDelEnv = CreateStyledButton("Eliminar", "#DC3545", false);
                        btnDelEnv.Click += (s, e) => {
                            var res = MessageBox.Show("¿Seguro que deseas eliminar el ambiente '" + curEnv.Name + "'?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                StopEnvironment(curEnv);
                                curCli.Environments.Remove(curEnv);
                                SaveClients();
                                SyncRegistryWithActiveClients();
                                RefreshUI();
                                ShowToast("Ambiente eliminado.");
                            }
                        };
                        envActions.Children.Add(btnDelEnv);

                        Grid.SetColumn(envActions, 1);
                        envGrid.Children.Add(envActions);

                        envCard.Child = envGrid;
                        envsPanel.Children.Add(envCard);

                        // Registrar vinculación visual para actualización en vivo
                        envBindings.Add(new EnvVisualBinding
                        {
                            ClientId = cli.Id,
                            EnvId = env.Id,
                            Env = env,
                            CardBorder = envCard,
                            StatusPill = statusPill,
                            StatusTxt = statusTxt,
                            ActionBtn = btnToggle,
                            LastRunning = isRunning,
                            EnvPort = envPort
                        });
                    }
                }

                if (showEnvironments) cardContent.Children.Add(envsPanel);
                clientCard.Child = cardContent;
                cardsPanel.Children.Add(clientCard);
            }

            if (txtSearchSummary != null)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    txtSearchSummary.Text = visibleClientCount + " cliente(s)  •  " + visibleEnvironmentCount + " ambiente(s)";
                else
                    txtSearchSummary.Text = visibleClientCount + " cliente(s)  •  " + visibleEnvironmentCount + " coincidencia(s)";
            }

            if (visibleClientCount == 0)
            {
                Border noResults = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(30),
                    Margin = new Thickness(0, 10, 0, 0),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                    BorderThickness = new Thickness(1)
                };
                StackPanel noResultsContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                noResultsContent.Children.Add(new TextBlock
                {
                    Text = "No se encontraron clientes ni ambientes.",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                noResultsContent.Children.Add(new TextBlock
                {
                    Text = "Prueba con otro nombre de cliente, ambiente o dirección IP.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                noResults.Child = noResultsContent;
                cardsPanel.Children.Add(noResults);
            }
        }

        private void ShowToast(string message)
        {
            txtToast.Text = message;
            txtToast.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
        }

        // ================= DIALOGO: CREAR / EDITAR CLIENTE =================
        private void OpenClientDialog(ClientItem existingClient)
        {
            bool isNew = (existingClient == null);
            ClientItem item = isNew ? new ClientItem() : existingClient;

            Window dlg = new Window
            {
                Title = isNew ? "Crear Nuevo Cliente" : "Editar Cliente",
                Width = 460,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                FontFamily = new FontFamily("Segoe UI")
            };

            StackPanel form = new StackPanel { Margin = new Thickness(24) };

            form.Children.Add(new TextBlock { Text = isNew ? "Registrar Nuevo Cliente / Empresa" : "Actualizar Cliente", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });

            form.Children.Add(new TextBlock { Text = "Nombre del Cliente / Empresa (ej. Maprial, Totalpec):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtName = new TextBox { Text = item.Name ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(txtName);

            form.Children.Add(new TextBlock { Text = "Descripción / Notas generales (opcional):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtDesc = new TextBox { Text = item.Description ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 16) };
            form.Children.Add(txtDesc);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button btnCancel = CreateStyledButton("Cancelar", "#6C757D", false);
            btnCancel.Click += (s, e) => dlg.Close();
            btnRow.Children.Add(btnCancel);

            Button btnSave = CreateStyledButton(isNew ? "Crear Cliente" : "Guardar Cambios", "#0D6EFD", true);
            btnSave.Margin = new Thickness(10, 0, 0, 0);
            btnSave.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Debes ingresar un nombre para el cliente.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Name = txtName.Text.Trim();
                item.Description = txtDesc.Text.Trim();

                if (isNew) clients.Add(item);
                SaveClients();
                SyncRegistryWithActiveClients();
                RefreshUI();
                ShowToast(isNew ? "Cliente '" + item.Name + "' creado exitosamente." : "Cambios guardados en '" + item.Name + "'.");
                dlg.Close();
            };
            btnRow.Children.Add(btnSave);

            form.Children.Add(btnRow);
            dlg.Content = form;
            dlg.ShowDialog();
        }

        // ================= DIALOGO: CREAR / EDITAR AMBIENTE =================
        private void OpenEnvironmentDialog(ClientItem client, EnvironmentItem existingEnv)
        {
            bool isNew = (existingEnv == null);
            EnvironmentItem item = isNew ? new EnvironmentItem() : existingEnv;

            // Si es un ambiente nuevo y el cliente ya tiene otros ambientes, heredar IP, esquema y versión
            EnvironmentItem refEnv = null;
            if (isNew && client.Environments != null && client.Environments.Count > 0)
            {
                refEnv = client.Environments.LastOrDefault(e => !string.IsNullOrWhiteSpace(e.ServerIp)) ?? client.Environments[0];
            }

            UiPresentationHelper.ServerAddressParts initialParts;
            if (!isNew)
            {
                initialParts = UiPresentationHelper.ParseServerAddress(item.ServerIp);
            }
            else if (refEnv != null)
            {
                initialParts = UiPresentationHelper.ParseServerAddress(refEnv.ServerIp);
                initialParts.WebPort = ""; // usuario solo ingresa el nuevo puerto
            }
            else
            {
                initialParts = new UiPresentationHelper.ServerAddressParts { IsHttps = false, Host = "", WebPort = "" };
            }

            Window dlg = new Window
            {
                Title = isNew ? "Agregar Ambiente a " + client.Name : "Editar Ambiente de " + client.Name,
                Width = 510,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                FontFamily = new FontFamily("Segoe UI")
            };

            StackPanel form = new StackPanel { Margin = new Thickness(24) };

            form.Children.Add(new TextBlock
            {
                Text = isNew ? "Nuevo Ambiente para " + client.Name : "Editar Ambiente de " + client.Name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 14)
            });

            form.Children.Add(new TextBlock { Text = "Nombre del Ambiente (ej. Producción, Desarrollo, QA):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtName = new TextBox { Text = item.Name ?? "Producción", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(txtName);

            // Campos separados: IP/Host del Servidor y Puerto Web (Servidor)
            Grid srvGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Pixel) });
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });

            StackPanel hostCol = new StackPanel();
            hostCol.Children.Add(new TextBlock { Text = "IP / Host del Servidor:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtHost = new TextBox { Text = initialParts.Host ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, ToolTip = "Dirección IP o host del servidor Protheus (ej. 192.168.0.10 o erp.empresa.com)" };
            hostCol.Children.Add(txtHost);
            Grid.SetColumn(hostCol, 0);
            srvGrid.Children.Add(hostCol);

            StackPanel portCol = new StackPanel();
            portCol.Children.Add(new TextBlock { Text = "Puerto Web:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtWebPort = new TextBox { Text = initialParts.WebPort ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, ToolTip = "Puerto del servicio WebApp / Broker (ej. 50200, 50101, 30008)" };
            portCol.Children.Add(txtWebPort);
            Grid.SetColumn(portCol, 2);
            srvGrid.Children.Add(portCol);

            form.Children.Add(srvGrid);

            CheckBox chkHttps = new CheckBox
            {
                Content = "Usar conexión segura HTTPS (SSL)",
                IsChecked = initialParts.IsHttps,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343A40")),
                Margin = new Thickness(0, 0, 0, 6)
            };
            form.Children.Add(chkHttps);

            Border previewBox = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 10)
            };
            TextBlock lblUrlPreview = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            previewBox.Child = lblUrlPreview;
            form.Children.Add(previewBox);

            Action updatePreview = () => {
                string built = UiPresentationHelper.BuildServerAddress(chkHttps.IsChecked == true, txtHost.Text, txtWebPort.Text);
                lblUrlPreview.Text = "Enlace resultante: " + (string.IsNullOrEmpty(built) ? "(Ingresa IP y Puerto)" : built);
            };
            updatePreview();

            form.Children.Add(new TextBlock { Text = "Puerto(s) de WebAgent (se auto-calcula, o pon el del appserver.ini):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtPorts = new TextBox { Text = item.AgentPort ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
            form.Children.Add(txtPorts);

            TextBlock lblPortTip = new TextBlock
            {
                Text = "\u2139\uFE0F Protheus calcula el agente sumando 41 a los primeros 3 dígitos (ej: 50200 ➔ 50241, 50101 ➔ 50141). Si en appserver.ini definieron [WebAgent] Port=, puedes escribirlo manualmente aquí.",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(lblPortTip);

            bool autoCalculated = string.IsNullOrWhiteSpace(txtPorts.Text);
            txtWebPort.TextChanged += (s, e) => {
                updatePreview();
                if (autoCalculated || string.IsNullOrWhiteSpace(txtPorts.Text))
                {
                    string p = txtWebPort.Text.Trim();
                    string pred = !string.IsNullOrEmpty(p) ? SmartRouter.PredictWebAgentPort(":" + p) : "";
                    if (!string.IsNullOrEmpty(pred))
                    {
                        txtPorts.Text = pred;
                        autoCalculated = true;
                    }
                }
            };
            txtHost.TextChanged += (s, e) => updatePreview();
            chkHttps.Click += (s, e) => updatePreview();
            txtPorts.TextChanged += (s, e) => {
                string p = txtWebPort.Text.Trim();
                string pred = !string.IsNullOrEmpty(p) ? SmartRouter.PredictWebAgentPort(":" + p) : "";
                if (!string.IsNullOrEmpty(pred) && txtPorts.Text != pred)
                {
                    autoCalculated = false;
                }
            };

            form.Children.Add(new TextBlock { Text = "Versión de WebAgent requerida por este ambiente:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            ComboBox cboVersions = new ComboBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 10), IsEditable = true };

            var standardVersions = new string[] {
                "web-agent-1.1.1-x64",
                "web-agent-1.1.0-x64",
                "web-agent-1.0.25-x64",
                "web-agent-1.0.24-x64",
                "web-agent-1.0.23-x64",
                "web-agent-1.1.1-x86"
            };
            var versionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sv in standardVersions) versionSet.Add(sv);
            foreach (var v in detectedVersions) versionSet.Add(v.FolderName);
            if (!string.IsNullOrEmpty(item.VersionFolder)) versionSet.Add(item.VersionFolder);

            foreach (var verName in versionSet)
            {
                cboVersions.Items.Add(verName);
            }
            if (!string.IsNullOrEmpty(item.VersionFolder))
            {
                cboVersions.Text = item.VersionFolder;
            }
            else if (refEnv != null && !string.IsNullOrEmpty(refEnv.VersionFolder))
            {
                cboVersions.Text = refEnv.VersionFolder;
            }
            else if (cboVersions.Items.Count > 0)
            {
                cboVersions.SelectedIndex = 0;
            }
            form.Children.Add(cboVersions);

            form.Children.Add(new TextBlock { Text = "Descripción / Notas adicionales (opcional):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtDesc = new TextBox { Text = item.Description ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 16) };
            form.Children.Add(txtDesc);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button btnCancel = CreateStyledButton("Cancelar", "#6C757D", false);
            btnCancel.Click += (s, e) => dlg.Close();
            btnRow.Children.Add(btnCancel);

            Button btnSave = CreateStyledButton(isNew ? "Agregar Ambiente" : "Guardar Cambios", "#0D6EFD", true);
            btnSave.Margin = new Thickness(10, 0, 0, 0);
            btnSave.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Debes ingresar un nombre para el ambiente (ej. Producción o Desarrollo).", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string finalServerUrl = UiPresentationHelper.BuildServerAddress(
                    chkHttps.IsChecked == true,
                    txtHost.Text,
                    txtWebPort.Text
                );
                if (string.IsNullOrWhiteSpace(finalServerUrl))
                {
                    MessageBox.Show("Debes ingresar al menos la IP o Host del servidor y el Puerto Web.", "Campos requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string chosenVer = cboVersions.Text.Trim();
                if (string.IsNullOrWhiteSpace(chosenVer))
                {
                    MessageBox.Show("Debes seleccionar o escribir la versión de WebAgent requerida.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Name = txtName.Text.Trim();
                item.ServerIp = finalServerUrl;
                item.AgentPort = txtPorts.Text.Trim();
                item.VersionFolder = chosenVer;
                item.Description = txtDesc.Text.Trim();

                if (client.Environments == null) client.Environments = new List<EnvironmentItem>();
                if (isNew) client.Environments.Add(item);

                SaveClients();
                SyncRegistryWithActiveClients();
                RefreshUI();
                ShowToast(isNew ? "Ambiente '" + item.Name + "' agregado a " + client.Name + "." : "Cambios guardados en '" + item.Name + "'.");
                dlg.Close();
            };
            btnRow.Children.Add(btnSave);

            form.Children.Add(btnRow);
            dlg.Content = form;
            dlg.ShowDialog();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                        DateTime.Now.ToString("s") + " [AppDomain] " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown") + "\r\n");
                }
                catch { }
            };

            if (args != null && args.Length > 0 && args[0].StartsWith("web-agent:", StringComparison.OrdinalIgnoreCase))
            {
                SmartRouter.RouteWebAgentCall(args[0]);
                return;
            }

            try
            {
                Application app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                            DateTime.Now.ToString("s") + " [Dispatcher] " + e.Exception.ToString() + "\r\n");
                    }
                    catch { }
                    e.Handled = true;
                };
                app.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                        DateTime.Now.ToString("s") + " [MainCatch] " + ex.ToString() + "\r\n");
                }
                catch { }
                MessageBox.Show("Error al iniciar WebAgent Manager:\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
