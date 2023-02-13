namespace SharedLib.Rebus.Configuration;

public class Config
{
    public Config(RebusConfig rebusConfig, RabbitMqConfig rabbitMqConfig)
    {
        RebusConfig = rebusConfig;
        RabbitMqConfig = rabbitMqConfig;
    }

    public RebusConfig RebusConfig { get; }
    public RabbitMqConfig RabbitMqConfig { get; }
}
