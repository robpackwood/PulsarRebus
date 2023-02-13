using System.Collections.Generic;

namespace SharedLib.Rebus.Configuration;

public class RebusConfig
{
    public string EndpointName { get; set; } = string.Empty;
    public bool IsSendOnlyEndpoint { get; set; }
    public int MaxParallelism { get; set; } = 5;
    public int MaxPrefetchCount { get; set; }
    public string ErrorQueueName { get; set; } = string.Empty;
    public int ImmediateRetries { get; set; } = 5;
    public bool EnableSecondaryRetries { get; set; }
    public int SecondaryRetryCount { get; set; }
    public int SecondaryRetryTimeDelayInSeconds { get; set; }
    public int WorkerCount { get; set; } = 1;
    public int WorkerShutdownTimeoutInSeconds { get; set; }

    public IEnumerable<string> GetValidationErrorMessages()
    {
        if (!IsSendOnlyEndpoint && string.IsNullOrWhiteSpace(EndpointName))
            yield return $"{nameof(EndpointName)} must be provided when " +
                         $"{nameof(IsSendOnlyEndpoint)} is false.";

        if (EnableSecondaryRetries)
        {
            if (SecondaryRetryCount <= 0)
                yield return $"{nameof(SecondaryRetryCount)} must be greater than 0 when " +
                             $"{nameof(EnableSecondaryRetries)} is true.";

            if (SecondaryRetryTimeDelayInSeconds <= 0)
                yield return $"{nameof(SecondaryRetryTimeDelayInSeconds)} must be greater than 0 when " +
                             $"{nameof(EnableSecondaryRetries)} is true.";
        }

        if (MaxParallelism < 1 || MaxParallelism > 100)
            yield return $"{nameof(MaxParallelism)} value must be between 1 and 100.";
        if (MaxPrefetchCount < 0)
            yield return $"{nameof(MaxPrefetchCount)} value must be greater than or equal to 0.";
        if (WorkerCount < 1 || WorkerCount > 100)
            yield return $"{nameof(WorkerCount)} value must be between 1 and 100.";
        if (!IsSendOnlyEndpoint && string.IsNullOrEmpty(ErrorQueueName))
            yield return $"{nameof(ErrorQueueName)} must be provided.";
        if (ImmediateRetries < 0)
            yield return $"{nameof(ImmediateRetries)} cannot be negative.";
    }
}
