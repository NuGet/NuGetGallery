// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery;

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
                    Logger.LogError(exception, "Task '{taskName}' failed: {Exception}", taskName, exception);
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
            containerBuilder.Register(context =>
                {
                    var configuration = context.Resolve<IOptionsSnapshot<MaintenanceEmailConfiguration>>().Value.ServiceBus;
                    if (configuration == null)
                    {
                        throw new InvalidOperationException("Email.ServiceBus configuration is required for staging expiration notifications.");
                    }

                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>("EmailTopic")
                .SingleInstance()
                .OnRelease(client => _ = client.CloseAsync());
            containerBuilder.RegisterType<EmailMessageEnqueuer>()
                .WithParameter((parameter, context) => parameter.ParameterType == typeof(ITopicClient),
                    (parameter, context) => context.ResolveKeyed<ITopicClient>("EmailTopic"))
                .As<IEmailMessageEnqueuer>();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<StagingBlobCleanupConfiguration>(configurationRoot.GetSection("StagingBlobCleanup"));
            services.Configure<StagingExpirationConfiguration>(configurationRoot.GetSection("StagingExpiration"));
            services.Configure<MaintenanceEmailConfiguration>(configurationRoot.GetSection("Email"));
            services.AddTransient<IMessageServiceConfiguration>(provider => provider.GetRequiredService<IOptionsSnapshot<MaintenanceEmailConfiguration>>().Value);
            services.AddTransient<IMessageService, AsynchronousEmailMessageService>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
        }

        internal MaintenanceEmailConfiguration GetEmailConfiguration()
        {
            return _serviceProvider.GetRequiredService<IOptionsSnapshot<MaintenanceEmailConfiguration>>().Value;
        }

        internal IMessageService GetMessageService()
        {
            return _serviceProvider.GetRequiredService<IMessageService>();
        }

        internal StagingBlobCleanupConfiguration GetStagingBlobCleanupConfiguration()
        {
            return _serviceProvider.GetRequiredService<IOptionsSnapshot<StagingBlobCleanupConfiguration>>().Value;
        }

        internal StagingExpirationConfiguration GetStagingExpirationConfiguration()
        {
            return _serviceProvider.GetRequiredService<IOptionsSnapshot<StagingExpirationConfiguration>>().Value;
        }

        internal BlobContainerClient CreateStagingBlobContainerClient(StagingBlobCleanupConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.StorageConnectionString))
            {
                throw new InvalidOperationException("Enabled staging blob cleanup requires a storage connection string.");
            }

            var storageMsi = _serviceProvider.GetRequiredService<IOptionsSnapshot<StorageMsiConfiguration>>().Value;
            return StorageAccountHelper.CreateBlobServiceClient(storageMsi, configuration.StorageConnectionString)
                .GetBlobContainerClient(CoreConstants.Folders.StagingFolderName);
        }
    }
}
