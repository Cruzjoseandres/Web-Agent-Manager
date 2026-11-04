using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WebAgentManagerApp
{
    public static class ConfigService
    {
        public static List<ClientItem> LoadClients(string configFile)
        {
            List<ClientItem> clients = null;
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
                SaveClients(configFile, clients);
            }
            return clients;
        }

        public static bool MigrateLegacyClients(List<ClientItem> list)
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

        public static void SaveClients(string configFile, List<ClientItem> clients)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            var cleanList = (clients ?? new List<ClientItem>()).Select(c => new
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

        public static void ExportConfiguration(List<ClientItem> clients, string targetPath)
        {
            var exportList = new List<ClientItem>();
            foreach (var c in clients ?? new List<ClientItem>())
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
            File.WriteAllText(targetPath, FormatJson(json), Encoding.UTF8);
        }

        public static void ImportConfiguration(string sourcePath, List<ClientItem> currentClients, out int addedClients, out int addedEnvs, out int updatedEnvs)
        {
            addedClients = 0;
            addedEnvs = 0;
            updatedEnvs = 0;

            string json = File.ReadAllText(sourcePath, Encoding.UTF8);
            JavaScriptSerializer js = new JavaScriptSerializer();
            List<ClientItem> imported = js.Deserialize<List<ClientItem>>(json);
            if (imported == null || imported.Count == 0)
            {
                throw new InvalidOperationException("El archivo seleccionado no contiene clientes válidos.");
            }

            MigrateLegacyClients(imported);

            foreach (var imp in imported)
            {
                if (string.IsNullOrWhiteSpace(imp.Name)) continue;
                var existing = currentClients.FirstOrDefault(c => string.Equals(c.Name, imp.Name, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    currentClients.Add(imp);
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
        }

        public static string FormatJson(string json)
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
    }
}
