using System;
using System.Collections.Generic;

namespace WebAgentManagerApp
{
    public class ClientItem
    {
        public string Id { get; set; }
        public string Name { get; set; }           
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
}
