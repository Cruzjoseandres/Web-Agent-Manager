using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAgentManagerApp
{
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
}

