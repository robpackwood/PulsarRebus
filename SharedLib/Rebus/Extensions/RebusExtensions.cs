using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;
using SharedLib.Rebus.Configuration;

namespace SharedLib.Rebus.Extensions;

public static class RebusExtensions
{
    public static IBus ConfigureMessageHandlerSubscribers(this IBus bus, Assembly assembly)
    {
        var handlerType = typeof(IHandleMessages);

        var messageHandlerTypes = assembly.GetTypes().Where(
            type => handlerType.IsAssignableFrom(type) &&
                    type is { IsAbstract: false, IsInterface: false });

        var uniqueMessageHandlerTypes = new HashSet<Type>(
            from messageHandlerType in messageHandlerTypes
            from messageHandlerInterface in messageHandlerType.GetInterfaces()
            where messageHandlerInterface.IsGenericType
            select messageHandlerInterface.GetGenericArguments()[0]);

        var tasks = (from uniqueMessageHandlerType in uniqueMessageHandlerTypes
                     select bus.Subscribe(uniqueMessageHandlerType)).ToList();

        Task.WhenAll(tasks).GetAwaiter().GetResult();
        return bus;
    }

    public static string GetMessageOriginator(this IMessageContext messageContext)
    {
        return messageContext.Message?.Headers.TryGetValue(Headers.ReturnAddress, out var originator) ?? false
            ? originator
            : string.Empty;
    }

    public static bool IsLastDelayedRetry(this RebusConfig config, IMessageContext messageContext)
    {
        if (!config.EnableSecondaryRetries)
            return true;

        messageContext.Headers.TryGetValue(Headers.DeferCount, out var delayedRetries);
        int.TryParse(delayedRetries, out var retryCount);
        return config.SecondaryRetryCount == retryCount;
    }
}
