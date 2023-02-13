#nullable enable
namespace SharedLib.Rebus.Configuration;

public class RabbitMqConfig
{
    public string Host { get; set; } = string.Empty;
    public string RebusHost => $"amqp://{Host}";
    public string VirtualHost { get; set; } = string.Empty;
    public int ConnectionTimeout { get; set; }
    public int ConnectionTimeoutInMilliseconds => ConnectionTimeout * 1000;
}
