// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.ServiceBus;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class Job : ValidationJobBase
    {
        private const string RevalidationConfigurationSectionName = "RevalidateJob";

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);
        }

        public override async Task Run()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var revalidator = scope.ServiceProvider.GetRequiredService<ICertificateRevalidator>();

                // Both of these methods only do a chunk of the possible promotion/revalidating work before
                // completing. This "Run" method may need to run several times to promote all signatures
                // and to revalidate all stale certificates.
                await revalidator.PromoteSignaturesAsync();
                await revalidator.RevalidateStaleCertificatesAsync();
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(RevalidationConfigurationSectionName));

            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value);

            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ICertificateRevalidator, CertificateRevalidator>();
            services.AddTransient<IValidateCertificateEnqueuer, ValidateCertificateEnqueuer>();
            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();

            services.AddTransient<ITopicClient>(provider =>
            {
                var config = provider.GetRequiredService<ServiceBusConfiguration>();

                return new TopicClientWrapper(config.ConnectionString, config.TopicPath);
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }
    }
}