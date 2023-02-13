using System;
using System.Threading;
using System.Threading.Tasks;
using Castle.Windsor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedLib.Hosting;

public static class WorkerHostRunner
{
    public static async Task Run(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            await host.RunAsync(cancellationToken);
        }
        finally
        {
            host.Services.GetService<WindsorContainer>()?.Dispose();
            (host.Services.GetService<IHostLifetime>() as IDisposable)?.Dispose();
        }
    }
}
