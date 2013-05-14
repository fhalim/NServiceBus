namespace EasyNetQ
{
    public class HostConfiguration : IHostConfiguration
    {
        public string Host { get; set; }
        public ushort Port { get; set; }
        public bool IsFailover { get; set; }
    }
}