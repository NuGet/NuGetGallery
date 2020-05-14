// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Runs all <see cref="MaintenanceTask"/>s against the Gallery database.
    /// </summary>
    public class Job : JsonConfigurationJob
    {
        public override async Task Run()
        {
            var failedTasks = new List<string>();

            foreach (var task in GetMaintenanceTasks())
            {
                var taskName = task.GetType().Name;

                try
                {
                    Logger.LogInformation("Running task '{taskName}'...", taskName);

                    await task.RunAsync(this);

                    Logger.LogInformation("Finished task '{taskName}'.", taskName);
                }
                catch (Exception exception)
                {
                    Logger.LogError("Task '{taskName}' failed: {Exception}", taskName, exception);
                    failedTasks.Add(taskName);
                }
            }
            
            if (failedTasks.Any())
            {
                throw new Exception($"{failedTasks.Count()} tasks failed: {string.Join(", ", failedTasks)}");
            }
        }

        public IEnumerable<MaintenanceTask> GetMaintenanceTasks()
        {
            var taskBaseType = typeof(MaintenanceTask);

            return taskBaseType.Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && taskBaseType.IsAssignableFrom(type))
                .Select(type => 
                    (MaintenanceTask) type.GetConstructor(
                        new Type[] { typeof(ILogger<>).MakeGenericType(type) })
                            .Invoke(new[] { CreateTypedLogger(type) }));
        }


        /// <summary>
        /// This is necessary because <see cref="LoggerFactoryExtensions.CreateLogger(ILoggerFactory, Type)"/> does not create a typed logger. 
        /// </summary>
        public ILogger CreateTypedLogger(Type type)
        {
            var typedCreateLoggerMethod =
                typeof(LoggerFactoryExtensions)
                .GetMethods()
                .SingleOrDefault(m =>
                    m.Name == nameof(LoggerFactoryExtensions.CreateLogger) &&
                    m.IsGenericMethod);

            return typedCreateLoggerMethod
                .MakeGenericMethod(type)
                .Invoke(null, new object[] { LoggerFactory }) as ILogger;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
        }
    }
}
