using System;

namespace WebAgentManagerApp
{
    public class EnvironmentItem
    {
        public string Id { get; set; }
        public string Name { get; set; }            
        public string ServerIp { get; set; }        
        public string AgentPort { get; set; }       
        public string VersionFolder { get; set; }   
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
}
