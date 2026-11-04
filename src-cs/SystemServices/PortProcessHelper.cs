using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebAgentManagerApp
{
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
}

