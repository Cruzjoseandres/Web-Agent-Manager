using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace WebAgentManagerApp
{
    public static class RegistryService
    {
        private const string RegKeyPath = @"Software\Classes\web-agent\shell\open\command";

        public static void SyncRegistryWithActiveClients(List<ClientItem> clients)
        {
            bool hasAnyEnabled = clients != null && clients.Any(c => c.Environments != null && c.Environments.Any(e => e.IsEnabled));
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
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                {
                    if (key != null)
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
            }
            catch { }
        }
    }
}
