// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;

namespace NuGet.Jobs.Validation.Helper
{
    public class Job : JobBase
    {
        private ICommand _command;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var action = ParseEnum<Action>(JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.Action));

            var azureStorageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
            var containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.ContainerName);
            var cloudStorageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var galleryBaseAddress = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryBaseAddress);

            switch (action)
            {
                case Action.Rescan:
                    _command = new Rescan(
                        jobArgsDictionary,
                        LoggerFactory.CreateLogger<Rescan>(),
                        cloudStorageAccount,
                        containerName,
                        new NuGetV2Feed(new HttpClient(), LoggerFactory.CreateLogger<NuGetV2Feed>()),
                        new PackageValidationService(cloudStorageAccount, containerName, LoggerFactory),
                        galleryBaseAddress);
                    break;

                case Action.MarkClean:
                    _command = new MarkClean(
                        jobArgsDictionary,
                        LoggerFactory.CreateLogger<MarkClean>(),
                        cloudStorageAccount,
                        containerName,
                        new NuGetV2Feed(new HttpClient(), LoggerFactory.CreateLogger<NuGetV2Feed>()),
                        new PackageValidationAuditor(cloudStorageAccount, containerName, LoggerFactory),
                        galleryBaseAddress);
                    break;
            }
        }

        public async override Task Run()
        {
            using (Logger.BeginScope("Processing action {Action} scope id: {RunTraceId}", _command.Action, Guid.NewGuid()))
            {
                await _command.Run();
            }
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: {0} " +
                    $"-{JobArgumentNames.VaultName} <KeyVault name> " +
                    $"-{JobArgumentNames.ClientId} <KeyVault clientId> " +
                    $"-{JobArgumentNames.CertificateThumbprint} <KeyVault certificate thumbprint> " +
                    $"-{JobArgumentNames.DataStorageAccount} <Azure Blob Storage connection string> " +
                    $"-{JobArgumentNames.ContainerName} <validation job container name> " +
                    $"-{JobArgumentNames.GalleryBaseAddress} <gallery base address> " +
                    $"-{CommandLineArguments.Action} ({Action.Rescan.ToString()}|{Action.MarkClean.ToString()}) " +
                    $"[-{JobArgumentNames.StoreName} (My|Root|TrustedPeople|TrustedPublisher|AddressBook|AuthRoot|CertificateAuthority|Disallowed)] " +
                    $"[-{JobArgumentNames.StoreLocation} (LocalMachine|CurrentUser)] " +
                    $"[-{JobArgumentNames.ValidateCertificate} (true|false)] " +
                    $"[-{JobArgumentNames.InstrumentationKey} <AI instrumentation key>]",
                Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));

            Console.WriteLine();
            Console.WriteLine($"'-{CommandLineArguments.Action} {Action.Rescan.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{CommandLineArguments.PackageId} <package id>");
            Console.WriteLine($"\t-{CommandLineArguments.PackageVersion} <package version>");
            Console.WriteLine();
            Console.WriteLine($"'-{CommandLineArguments.Action} {Action.MarkClean.ToString()}' specific arguments: ");
            Console.WriteLine($"\t-{CommandLineArguments.PackageId} <package id>");
            Console.WriteLine($"\t-{CommandLineArguments.PackageVersion} <package version>");
            Console.WriteLine($"\t-{CommandLineArguments.ValidationId} <validation Id (GUID)>");
            Console.WriteLine($"\t-{CommandLineArguments.Alias} <alias>");
            Console.WriteLine($"\t-{CommandLineArguments.Comment} <comment>");
            Console.WriteLine();
            Console.WriteLine("Package Id and version are supposed to be urlencoded (https://www.bing.com/search?q=urlencode).");
            Console.WriteLine("You need it for package ids and versions containing non-latin unicode characters and versions containing '+'.");
        }

        private static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }
    }
}
