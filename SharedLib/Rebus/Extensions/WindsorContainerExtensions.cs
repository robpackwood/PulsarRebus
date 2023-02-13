#nullable enable
using System;
using System.Reflection;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using RabbitMQ.Client;
using Rebus.Bus;
using Rebus.CastleWindsor;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Retry.Simple;
using SharedLib.Rebus.Configuration;

namespace SharedLib.Rebus.Extensions;

public static class WindsorContainerExtensions
{
    public static IWindsorContainer ConfigureRebus(
        this IWindsorContainer container, Config config, Assembly assembly,
        Action<RebusConfigurer>? additionalRebusConfigurer = null,
        bool registerTypedFactoryFacility = false)
    {
        if (!config.RebusConfig.IsSendOnlyEndpoint)
            container.AutoRegisterHandlersFromAssembly(assembly);

        var rebusConfigurer = GetRebusConfigurer(container, config);

        if (!config.RebusConfig.IsSendOnlyEndpoint)
            ConfigureRebusConsumer(rebusConfigurer, config);

        additionalRebusConfigurer?.Invoke(rebusConfigurer);
        container.Kernel.Resolver.AddSubResolver(new CollectionResolver(container.Kernel, true));

        if (registerTypedFactoryFacility)
            container.AddFacility<TypedFactoryFacility>();

        return container.Register(Component.For<Lazy<IBus>>().Instance(
                new Lazy<IBus>(() =>
                {
                    var bus = rebusConfigurer.Start();
                    bus.ConfigureMessageHandlerSubscribers(assembly);
                    return bus;
                })),
            Component.For<RebusConfig>().Instance(config.RebusConfig));
    }

    private static void ConfigureRebusConsumer(RebusConfigurer rebusConfigurer, Config config)
    {
        rebusConfigurer.Options(configurer =>
        {
            configurer.SetMaxParallelism(config.RebusConfig.MaxParallelism);

            if (config.RebusConfig.WorkerShutdownTimeoutInSeconds > 0)
                configurer.SetWorkerShutdownTimeout(
                    TimeSpan.FromSeconds(config.RebusConfig.WorkerShutdownTimeoutInSeconds));

            configurer.SimpleRetryStrategy(
                config.RebusConfig.ErrorQueueName, config.RebusConfig.ImmediateRetries + 1,
                config.RebusConfig.EnableSecondaryRetries);

            configurer.SetNumberOfWorkers(config.RebusConfig.WorkerCount);
        });

        rebusConfigurer.Timeouts(configurer => configurer.StoreInMemory());
        rebusConfigurer.Sagas(configurer => configurer.StoreInMemory());
    }

    private static RebusConfigurer GetRebusConfigurer(IWindsorContainer container, Config config)
    {
        var rebusConfigurer = Configure
            .With(new CastleWindsorContainerAdapter(container))
            .Logging(logger => logger.Serilog(Serilog.Log.Logger));

        ConfigureRabbitMqTransport(config, rebusConfigurer);
        return rebusConfigurer;
    }

    private static void ConfigureRabbitMqTransport(Config config, RebusConfigurer rebusConfigurer)
    {
        rebusConfigurer.Transport(configurer =>
        {
            var rabbitMqOptionsBuilder =
                config.RebusConfig.IsSendOnlyEndpoint
                    ? configurer.UseRabbitMqAsOneWayClient(config.RabbitMqConfig.RebusHost)
                    : configurer.UseRabbitMq(config.RabbitMqConfig.RebusHost, config.RebusConfig.EndpointName);

            if (config.RebusConfig.MaxPrefetchCount > 0)
                rabbitMqOptionsBuilder.Prefetch(config.RebusConfig.MaxPrefetchCount);

            rabbitMqOptionsBuilder
                .CustomizeConnectionFactory(customizer => ConfigureRabbitMqConnectionFactory(config, customizer))
                .SetPublisherConfirms(true);
        });
    }

    private static IConnectionFactory ConfigureRabbitMqConnectionFactory(Config config, IConnectionFactory customizer)
    {
        customizer.VirtualHost = config.RabbitMqConfig.VirtualHost;
        return customizer;
    }
}
