namespace WebAgentManagerApp
{
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
}

