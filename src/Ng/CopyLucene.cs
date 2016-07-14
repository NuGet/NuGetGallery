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
                + "-"  + CommandHelpers.SrcDirectoryType + " file|azure "
                + "[-" + CommandHelpers.SrcPath          + " <file-path>] "
                + "| "
                + "[-"    + CommandHelpers.SrcStorageAccountName + " <azure-acc> "
                    + "-" + CommandHelpers.SrcStorageKeyValue    + " <azure-key> "
                    + "-" + CommandHelpers.SrcStorageContainer   + " <azure-container>] "
                + "-"  + CommandHelpers.DestDirectoryType + " file|azure "
                + "[-" + CommandHelpers.DestPath          + " <file-path>] "
                + "| "
                + "[-"     + CommandHelpers.DestStorageAccountName + " <azure-acc> "
                    + "-"  + CommandHelpers.DestStorageKeyValue    + " <azure-key> "
                    + "-"  + CommandHelpers.DestStorageContainer   + " <azure-container>] "
                    + "[-"     + CommandHelpers.VaultName             + " <keyvault-name> "
                        + "-"  + CommandHelpers.ClientId              + " <keyvault-client-id> "
                        + "-"  + CommandHelpers.CertificateThumbprint + " <keyvault-certificate-thumbprint> "
                        + "[-" + CommandHelpers.ValidateCertificate   + " true|false]]");
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
