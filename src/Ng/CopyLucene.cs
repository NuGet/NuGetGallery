// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Store.Azure;

namespace Ng
{
    static class CopyLucene
    {
        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng copylucene "
                + $"-{Constants.SrcDirectoryType} file|azure "
                + $"[-{Constants.SrcPath} <file-path>]"
                + "|"
                + $"[-{Constants.SrcStorageAccountName} <azure-acc> "
                    + $"-{Constants.SrcStorageKeyValue} <azure-key> "
                    + $"-{Constants.SrcStorageContainer} <azure-container>] "
                + $"-{Constants.DestDirectoryType} file|azure "
                + $"[-{Constants.DestPath} <file-path>]"
                + "|"
                + $"[-{Constants.DestStorageAccountName} <azure-acc> "
                    + $"-{Constants.DestStorageKeyValue} <azure-key> "
                    + $"-{Constants.DestStorageContainer} <azure-container>] "
                    + $"[-{Constants.VaultName} <keyvault-name> "
                        + $"-{Constants.ClientId} <keyvault-client-id> "
                        + $"-{Constants.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                        + $"[-{Constants.ValidateCertificate} true|false]]");
        }

        public static void Run(IDictionary<string, string> arguments)
        {
            Lucene.Net.Store.Directory srcDirectory = CommandHelpers.GetCopySrcLuceneDirectory(arguments);
            Lucene.Net.Store.Directory destDirectory = CommandHelpers.GetCopyDestLuceneDirectory(arguments);

            Lucene.Net.Store.Directory.Copy(srcDirectory, destDirectory, true);

            if (destDirectory is AzureDirectory)
            {
                // When the destination directory is an AzureDirectory,
                // create an empty write.lock to prevent writers from crashing.
                if (!destDirectory.ListAll().Any(f =>
                    String.Equals(f, "write.lock", StringComparison.OrdinalIgnoreCase)))
                {
                    var writeLock = destDirectory.CreateOutput("write.lock");
                    writeLock.Dispose();
                }
            }

            Console.WriteLine("All Done");
        }
    }
}
