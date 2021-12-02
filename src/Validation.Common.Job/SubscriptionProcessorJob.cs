// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.ServiceBus;

namespace NuGet.Jobs.Validation
{
    public abstract class SubscriptionProcessorJob<T> : ValidationJobBase
    {
        private const string SubscriptionProcessorConfigurationSectionName = "ServiceBus";

        /// <summary>
        /// The maximum amount of time that graceful shutdown can take before the job will
        /// forcefully end itself.
        /// </summary>
        private static readonly TimeSpan MaxShutdownTime = TimeSpan.FromMinutes(1);

        public override async Task Run()
        {
            var featureFlagRefresher = _serviceProvider.GetRequiredService<IFeatureFlagRefresher>();
            await featureFlagRefresher.StartIfConfiguredAsync();

            var processor = _serviceProvider.GetService<ISubscriptionProcessor<T>>();

            if (processor == null)
            {
                throw new Exception($"DI container was not set up to produce instances of ISubscriptionProcessor<{typeof(T).Name}>. " +
                    $"Call SubcriptionProcessorJob<T>.{nameof(ConfigureDefaultSubscriptionProcessor)}() or set it up your way.");
            }

            var configuration = _serviceProvider.GetService<IOptionsSnapshot<SubscriptionProcessorConfiguration>>();

            if (configuration == null || configuration.Value == null)
            {
                throw new Exception($"Failed to get the SubscriptionProcessorJob configuration. Call " +
                    $"SubcriptionProcessorJob<T>.{nameof(SetupDefaultSubscriptionProcessorConfiguration)}() or set it up your way.");
            }

            processor.Start(configuration.Value.MaxConcurrentCalls);

            // Wait a certain period of time, and then shutdown this process so that it is restarted.
            await Task.Delay(configuration.Value.ProcessDuration);

            if (!await processor.ShutdownAsync(MaxShutdownTime))
            {
                Logger.LogWarning(
                    "Failed to gracefully shutdown Service Bus subscription processor. {MessagesInProgress} messages left",
                    processor.NumberOfMessagesInProgress);
            }

            await featureFlagRefresher.StopAndWaitAsync();
        }

        protected override void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureDefaultJobServices(services, configurationRoot);
        }

        protected static void ConfigureDefaultSubscriptionProcessor(ContainerBuilder containerBuilder)
        {
            const string bindingKey = "SubscriptionProcessorJob_SubscriptionProcessorKey";

            containerBuilder
                .RegisterType<ScopedMessageHandler<T>>()
                .Keyed<IMessageHandler<T>>(bindingKey);

            containerBuilder
                .RegisterType<SubscriptionProcessor<T>>()
                .WithParameter(
                    (parameter, context) => parameter.ParameterType == typeof(IMessageHandler<T>),
                    (parameter, context) => context.ResolveKeyed(bindingKey, typeof(IMessageHandler<T>)))
                .As<ISubscriptionProcessor<T>>();
        }

        protected static void SetupDefaultSubscriptionProcessorConfiguration(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SubscriptionProcessorConfiguration>(configuration.GetSection(SubscriptionProcessorConfigurationSectionName));
        }
    }
}
