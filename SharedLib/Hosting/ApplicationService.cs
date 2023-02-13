using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SharedLib.Hosting;

public class ApplicationService
{
    public static async Task Run(
        Assembly assembly, string applicationName,
        Func<IConfiguration, Assembly, string, IHostBuilder> hostBuilderFunc,
        CancellationToken cancellationToken, Action<IHostBuilder, IConfiguration>? configureHostBuilder = null,
        string jsonSettingsFile = "appSettings.json")
    {
        var configuration = HostConfigurationBuilder.Build(jsonSettingsFile);
        var hostBuilder = hostBuilderFunc(configuration, assembly, applicationName);
        configureHostBuilder?.Invoke(hostBuilder, configuration);
        var endpointHost = hostBuilder.Build();
        await WorkerHostRunner.Run(endpointHost, cancellationToken);
    }
}
