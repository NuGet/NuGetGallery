// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.Jobs
{
    public abstract class NgJob
    {
        protected ILoggerFactory LoggerFactory;
        protected ILogger Logger;

        protected NgJob(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            // We want to make a logger using the subclass of this job.
            // GetType returns the subclass of this instance which we can then use to create a logger.
            Logger = LoggerFactory.CreateLogger(GetType());
        }

        public static string GetUsageBase()
        {
            return "Usage: ng [" + string.Join("|", NgJobFactory.JobMap.Keys) + "] "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]";
        }

        public virtual string GetUsage()
        {
            return GetUsageBase();
        }

        protected abstract void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken);

        protected abstract Task RunInternal(CancellationToken cancellationToken);
        
        public virtual async Task Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            Init(arguments, cancellationToken);
            await RunInternal(cancellationToken);
        }
    }
}
