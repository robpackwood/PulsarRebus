using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SharedLib.Hosting;

public static class HostConfigurationBuilder
{
    /// <summary>
    ///     Builds an IConfiguration instance based on the passed in json settings file
    /// </summary>
    /// <param name="jsonSettingsFile">Optional: the name of the json file containing settings</param>
    /// <returns>An IConfiguration instance for application configuration</returns>
    public static IConfiguration Build(string jsonSettingsFile = "appsettings.json")
    {
        string? basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(jsonSettingsFile, false, true)
            .AddEnvironmentVariables()
            .Build();
    }
}
