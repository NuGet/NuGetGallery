// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lucene.Net.Store.Azure;

namespace Ng.Jobs
{
    public class CopyLuceneJob : NgJob
    {
        private Lucene.Net.Store.Directory _srcDirectory;
        private Lucene.Net.Store.Directory _destDirectory;

        public CopyLuceneJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng copylucene "
                   + $"-{Arguments.SrcDirectoryType} file|azure "
                   + $"[-{Arguments.SrcPath} <file-path>]"
                   + "|"
                   + $"[-{Arguments.SrcStorageAccountName} <azure-acc> "
                   + $"-{Arguments.SrcStorageKeyValue} <azure-key> "
                   + $"-{Arguments.SrcStorageContainer} <azure-container>] "
                   + $"-{Arguments.DestDirectoryType} file|azure "
                   + $"[-{Arguments.DestPath} <file-path>]"
                   + "|"
                   + $"[-{Arguments.DestStorageAccountName} <azure-acc> "
                   + $"-{Arguments.DestStorageKeyValue} <azure-key> "
                   + $"-{Arguments.DestStorageContainer} <azure-container>] "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _srcDirectory = CommandHelpers.GetCopySrcLuceneDirectory(arguments);
            _destDirectory = CommandHelpers.GetCopyDestLuceneDirectory(arguments);
        }
        
        protected override Task RunInternal(CancellationToken cancellationToken)
        {
            Lucene.Net.Store.Directory.Copy(_srcDirectory, _destDirectory, true);

            if (_destDirectory is AzureDirectory)
            {
                // When the destination directory is an AzureDirectory,
                // create an empty write.lock to prevent writers from crashing.
                if (!_destDirectory.ListAll().Any(f =>
                    string.Equals(f, "write.lock", StringComparison.OrdinalIgnoreCase)))
                {
                    var writeLock = _destDirectory.CreateOutput("write.lock");
                    writeLock.Dispose();
                }
            }

            Logger.LogInformation("All Done");

            return Task.FromResult(false);
        }
    }
}
