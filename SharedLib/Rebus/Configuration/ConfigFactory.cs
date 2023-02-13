#nullable enable
using Microsoft.Extensions.Configuration;

namespace SharedLib.Rebus.Configuration;

public static class ConfigFactory
{
    public static Config Create(IConfiguration configuration)
    {
        var rebusConfig = configuration.GetSection(nameof(RebusConfig))
            .Get<RebusConfig>() ?? new RebusConfig { IsSendOnlyEndpoint = true };

        var rabbitMqConfig = configuration.GetSection(nameof(RabbitMqConfig))
            .Get<RabbitMqConfig>() ?? new RabbitMqConfig();

        return new Config(rebusConfig, rabbitMqConfig);
    }
}
