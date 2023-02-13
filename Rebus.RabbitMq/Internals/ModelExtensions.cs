﻿using RabbitMQ.Client;

namespace Rebus.Internals;

internal static class ModelExtensions
{
    /// <summary>
    /// Disposes the specific 
    /// </summary>
    /// <param name="model"></param>
    internal static void SafeDrop(this IModel model)
    {
        if (model == null)
        {
            return;
        }
            
        try
        {
            model.Close();
            model.Dispose();
        }
        catch 
        {
            // it's so fucked up that these can throw exceptions
        }
    }
}