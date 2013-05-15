namespace EasyNetQ
{
    using System;

    public interface IHostConfiguration
    {
        string Host { get; }
        ushort Port { get; }
        bool IsFailover { get; set; }
    }
}