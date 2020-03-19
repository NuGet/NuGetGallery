// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;

namespace Ng.Jobs
{
    public abstract class NgJob
    {
        protected ISecretInjector SecretInjector { get; private set; }

        protected readonly IDictionary<string, string> GlobalTelemetryDimensions;
        protected readonly ITelemetryClient TelemetryClient;
        protected readonly ITelemetryService TelemetryService;
        protected readonly ILoggerFactory LoggerFactory;
        protected readonly ILogger Logger;

        protected int MaxDegreeOfParallelism { get; set; }

        protected NgJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
        {
            LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            TelemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            GlobalTelemetryDimensions = telemetryGlobalDimensions ?? throw new ArgumentNullException(nameof(telemetryGlobalDimensions));
            TelemetryService = new TelemetryService(telemetryClient, telemetryGlobalDimensions);

            // We want to make a logger using the subclass of this job.
            // GetType returns the subclass of this instance which we can then use to create a logger.
            Logger = LoggerFactory.CreateLogger(GetType());

            // Enable greater HTTP parallelization.
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            MaxDegreeOfParallelism = ServicePointManager.DefaultConnectionLimit;
        }

        public static string GetUsageBase()
        {
            return "Usage: ng [" + string.Join("|", NgJobFactory.JobMap.Keys) + "] "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.UseManagedIdentity} true|false "
                   + $"-{Arguments.ClientId} <keyvault-client-id> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"[-{Arguments.ValidateCertificate} true|false]]";
        }

        public virtual string GetUsage()
        {
            return GetUsageBase();
        }

        public void SetSecretInjector(ISecretInjector secretInjector)
        {
            SecretInjector = secretInjector;
        }

        protected abstract void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken);

        protected abstract Task RunInternalAsync(CancellationToken cancellationToken);

        public virtual async Task RunAsync(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            Init(arguments, cancellationToken);
            await RunInternalAsync(cancellationToken);
        }
    }
}