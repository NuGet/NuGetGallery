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
        static void PrintUsage()
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

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            Lucene.Net.Store.Directory srcDirectory = CommandHelpers.GetCopySrcLuceneDirectory(arguments);
            if (srcDirectory == null)
            {
                Console.WriteLine("problem with src arguments");
                PrintUsage();
                return;
            }

            Lucene.Net.Store.Directory destDirectory = CommandHelpers.GetCopyDestLuceneDirectory(arguments);
            if (destDirectory == null)
            {
                Console.WriteLine("problem with dest arguments");
                PrintUsage();
                return;
            }

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
