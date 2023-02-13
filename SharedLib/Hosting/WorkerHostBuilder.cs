using System;
using System.Reflection;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Castle.Windsor.MsDependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedLib.Hosting;

public static class WorkerHostBuilder
{
    public static IHostBuilder Build<TWorkerService>(
        Assembly assembly, IConfiguration configuration,
        Action<IWindsorContainer, IServiceProviderFactory<IWindsorContainer>>? additionalContainerConfigurer = null)
        where TWorkerService : BackgroundService
    {
        var serviceProviderFactory = new WindsorServiceProviderFactory();

        var hostBuilder = Host.CreateDefaultBuilder()
            .UseServiceProviderFactory(serviceProviderFactory)
            .ConfigureAppConfiguration((_, builder) => builder.AddConfiguration(configuration))
            .ConfigureServices((_, services) => services.AddHostedService<TWorkerService>())
            .ConfigureContainer<WindsorContainer>((_, container) =>
            {
                container.Install(FromAssembly.InThisApplication(assembly));
                additionalContainerConfigurer?.Invoke(container, serviceProviderFactory);
            });

        return hostBuilder;
    }
}
