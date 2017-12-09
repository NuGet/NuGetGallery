// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.PackageCertificates;
using NuGet.Services.Validation.PackageSigning;
using NuGet.Services.Validation.Vcs;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string VcsSectionName = "Vcs";
        private const string PackageSigningSectionName = "PackageSigning";
        private const string PackageCertificatesSectionName = "PackageCertificates";
        private const string RunnerConfigurationSectionName = "RunnerConfiguration";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string SmtpConfigurationSectionName = "Smtp";
        private const string EmailConfigurationSectionName = "Email";

        private const string VcsBindingKey = VcsSectionName;
        private const string PackageVerificationTopicClientBindingKey = "PackageVerificationTopicClient";
        private const string PackageSigningBindingKey = PackageSigningSectionName;
        private const string PackageCertificatesBindingKey = PackageCertificatesSectionName;
        private const string ValidationStorageBindingKey = "ValidationStorage";
        private const string OrchestratorBindingKey = "Orchestrator";

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private bool _validateOnly;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Indicates whether we had successful configuration validation or not
        /// </summary>
        public bool ConfigurationValidated { get; set; }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _validateOnly = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, ValidateArgument, defaultValue: false);
            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename, _validateOnly));
            ConfigurationValidated = false;
        }

        public override async Task Run()
        {
            var validator = GetRequiredService<ConfigurationValidator>();
            validator.Validate();
            ConfigurationValidated = true;

            if (_validateOnly)
            {
                Logger.LogInformation("Configuration validation successful");
                return;
            }

            var runner = GetRequiredService<OrchestrationRunner>();
            await runner.RunOrchestrationAsync();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            if (validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return uninjectedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();
            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<VcsConfiguration>(configurationRoot.GetSection(VcsSectionName));
            services.Configure<PackageSigningConfiguration>(configurationRoot.GetSection(PackageSigningSectionName));
            services.Configure<PackageCertificatesConfiguration>(configurationRoot.GetSection(PackageCertificatesSectionName));
            services.Configure<OrchestrationRunnerConfiguration>(configurationRoot.GetSection(RunnerConfigurationSectionName));
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<SmtpConfiguration>(configurationRoot.GetSection(SmtpConfigurationSectionName));
            services.Configure<EmailConfiguration>(configurationRoot.GetSection(EmailConfigurationSectionName));

            services.AddTransient<ConfigurationValidator>();
            services.AddTransient<OrchestrationRunner>();

            services.AddScoped<NuGetGallery.IEntitiesContext>(serviceProvider =>
                new NuGetGallery.EntitiesContext(
                    serviceProvider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value.ConnectionString,
                    readOnly: false));
            services.AddScoped(serviceProvider =>
                new ValidationEntitiesContext(
                    serviceProvider.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>().Value.ConnectionString));
            services.AddScoped<IValidationEntitiesContext>(serviceProvider =>
                serviceProvider.GetRequiredService<ValidationEntitiesContext>());
            services.AddScoped<IValidationStorageService, ValidationStorageService>();
            services.Add(ServiceDescriptor.Transient(typeof(NuGetGallery.IEntityRepository<>), typeof(NuGetGallery.EntityRepository<>)));
            services.AddTransient<NuGetGallery.ICorePackageService, NuGetGallery.CorePackageService>();
            services.AddTransient<ISubscriptionClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new SubscriptionClientWrapper(configuration.ConnectionString, configuration.TopicPath, configuration.SubscriptionName);
            });
            services.AddTransient<ITopicClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
            });
            services.AddTransient<IPackageValidationEnqueuer, PackageValidationEnqueuer>();
            services.AddTransient<IValidatorProvider, ValidatorProvider>();
            services.AddTransient<IValidationSetProvider, ValidationSetProvider>();
            services.AddTransient<IMessageHandler<PackageValidationMessageData>, ValidationMessageHandler>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<PackageValidationMessageData>, PackageValidationMessageDataSerializationAdapter>();
            services.AddTransient<IPackageCriteriaEvaluator, PackageCriteriaEvaluator>();
            services.AddTransient<VcsValidator>();
            services.AddTransient<IPackageSignatureVerificationEnqueuer, PackageSignatureVerificationEnqueuer>();
            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IValidatorStateService, ValidatorStateService>();
            services.AddTransient<PackageSigningValidator>();
            services.AddTransient<MailSenderConfiguration>(serviceProvider =>
            {
                var smtpConfigurationAccessor = serviceProvider.GetRequiredService<IOptionsSnapshot<SmtpConfiguration>>();
                var smtpConfiguration = smtpConfigurationAccessor.Value;
                return new MailSenderConfiguration
                {
                    DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                    Host = smtpConfiguration.SmtpHost,
                    Port = smtpConfiguration.SmtpPort,
                    EnableSsl = smtpConfiguration.EnableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpConfiguration.Username, smtpConfiguration.Password)
                };
            });
            services.AddTransient<IMailSender>(serviceProvider =>
            {
                var mailSenderConfiguration = serviceProvider.GetRequiredService<MailSenderConfiguration>();
                return string.IsNullOrWhiteSpace(mailSenderConfiguration.Host)
                    ? (IMailSender)new DiskMailSender()
                    : (IMailSender)new MailSender(mailSenderConfiguration);
            });
            services.AddTransient<ICoreMessageServiceConfiguration, CoreMessageServiceConfiguration>();
            services.AddTransient<ICoreMessageService, CoreMessageService>();
            services.AddTransient<IMessageService, MessageService>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            /// Initialize dependencies for the <see cref="VcsValidator"/>. There is some additional complexity here
            /// because the implementations require ambiguous types (such as a <see cref="string"/> and a
            /// <see cref="CloudStorageAccount"/> which there may be more than one configuration of).
            containerBuilder
                .Register(c =>
                {
                    var vcsConfiguration = c.Resolve<IOptionsSnapshot<VcsConfiguration>>();
                    var cloudStorageAccount = CloudStorageAccount.Parse(vcsConfiguration.Value.DataStorageAccount);
                    return cloudStorageAccount;
                })
                .Keyed<CloudStorageAccount>(VcsBindingKey);
            containerBuilder
                .Register(c =>
                {
                    var serviceBusConfiguration = c.Resolve<IOptionsSnapshot<ServiceBusConfiguration>>();
                    var topicClient = new TopicClientWrapper(serviceBusConfiguration.Value.ConnectionString, serviceBusConfiguration.Value.TopicPath);
                    return topicClient;
                })
                .Keyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey);

            containerBuilder
                .RegisterType<PackageValidationService>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationService>();

            containerBuilder
                .RegisterType<PackageValidationAuditor>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationAuditor>();

            containerBuilder
                .RegisterType<PackageSignatureVerificationEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IBrokeredMessageSerializer<SignatureValidationMessage>),
                    (pi, ctx) => ctx.Resolve<SignatureValidationMessageSerializer>()
                    ))
                .As<IPackageSignatureVerificationEnqueuer>();

            containerBuilder
                .Register(c => 
                {
                    var configurationAccessor = c.Resolve<IOptionsSnapshot<ValidationConfiguration>>();
                    return new NuGetGallery.CloudBlobClientWrapper(configurationAccessor.Value.ValidationStorageConnectionString, false);
                })
                .Keyed<NuGetGallery.ICloudBlobClient>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterKeyedTypeWithKeyedParameter<NuGetGallery.ICoreFileStorageService, NuGetGallery.CloudBlobCoreFileStorageService, NuGetGallery.ICloudBlobClient>(
                    typeKey: ValidationStorageBindingKey,
                    parameterKey: ValidationStorageBindingKey);

            containerBuilder
                .RegisterKeyedTypeWithKeyedParameter<NuGetGallery.ICorePackageFileService, NuGetGallery.CorePackageFileService, NuGetGallery.ICoreFileStorageService>(
                    typeKey: ValidationStorageBindingKey,
                    parameterKey: ValidationStorageBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    IValidationOutcomeProcessor,
                    ValidationOutcomeProcessor,
                    NuGetGallery.ICorePackageFileService>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    IValidationSetProcessor,
                    ValidationSetProcessor,
                    NuGetGallery.ICorePackageFileService>(ValidationStorageBindingKey);

            containerBuilder
                .RegisterType<ScopedMessageHandler<PackageValidationMessageData>>()
                .Keyed<IMessageHandler<PackageValidationMessageData>>(OrchestratorBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    ISubscriptionProcessor<PackageValidationMessageData>, 
                    SubscriptionProcessor<PackageValidationMessageData>, 
                    IMessageHandler<PackageValidationMessageData>>(
                        OrchestratorBindingKey);

            ConfigurePackageSigningValidator(containerBuilder);
            ConfigurePackageCertificatesValidator(containerBuilder);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private static void ConfigurePackageSigningValidator(ContainerBuilder builder)
        {
            // Configure the validator state service for the package certificates validator.
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(Type),
                    (pi, ctx) => typeof(PackageSigningValidator))
                .Keyed<IValidatorStateService>(PackageSigningBindingKey);

            // Configure the package signature verification enqueuer.
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<PackageSigningConfiguration>>().Value.ServiceBus;

                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(PackageSigningBindingKey);

            builder
                .RegisterType<PackageSignatureVerificationEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), PackageSigningBindingKey)
                .As<IPackageSignatureVerificationEnqueuer>();

            // Configure the package signing validator.
            builder
                .RegisterType<PackageSigningValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), PackageSigningBindingKey)
                .As<PackageSigningValidator>();
        }

        private static void ConfigurePackageCertificatesValidator(ContainerBuilder builder)
        {
            // Configure the validator state service for the package certificates validator.
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(Type),
                    (pi, ctx) => typeof(PackageCertificatesValidator))
                .Keyed<IValidatorStateService>(PackageCertificatesBindingKey);

            // Configure the certificate verification enqueuer.
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<PackageCertificatesConfiguration>>().Value.ServiceBus;

                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(PackageCertificatesBindingKey);

            builder
                .RegisterType<CertificateVerificationEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), PackageCertificatesBindingKey)
                .As<ICertificateVerificationEnqueuer>();

            // Configure the certificates validator.
            builder
                .RegisterType<PackageCertificatesValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), PackageCertificatesBindingKey)
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(TimeSpan?),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<PackageCertificatesConfiguration>>().Value.CertificateRevalidationThreshold)
                .As<PackageCertificatesValidator>();
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
