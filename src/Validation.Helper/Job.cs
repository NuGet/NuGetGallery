// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.Logging;

namespace NuGet.Jobs.Validation.Helper
{
    public class Job : JobBase
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<Job> _logger;
        private ICommand _command;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                if (!ApplicationInsights.Initialized)
                {
                    string instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                    if (!string.IsNullOrWhiteSpace(instrumentationKey))
                    {
                        ApplicationInsights.Initialize(instrumentationKey);
                    }
                }

                _loggerFactory = LoggingSetup.CreateLoggerFactory(LoggingSetup.CreateDefaultLoggerConfiguration(true));
                _logger = _loggerFactory.CreateLogger<Job>();

                // A hack to prevent Trace.<something> from printing to console. This code uses ILogger,
                // JobRunner unfortunately uses Trace and prints stuff that is not related to this tool. 
                // When (and if) JobRunner and all other jobs are updatedto use ILogger, this call should 
                // be removed.
                DisableTrace();

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
                        _loggerFactory.CreateLogger<Rescan>(),
                        cloudStorageAccount,
                        containerName,
                        new NuGetV2Feed(new HttpClient(), _loggerFactory.CreateLogger<NuGetV2Feed>()),
                        new PackageValidationService(cloudStorageAccount, containerName, _loggerFactory),
                        galleryBaseAddress);
                    break;

                case Action.MarkClean:
                    _command = new MarkClean(
                        jobArgsDictionary,
                        _loggerFactory.CreateLogger<MarkClean>(),
                        cloudStorageAccount,
                        containerName,
                        new NuGetV2Feed(new HttpClient(), _loggerFactory.CreateLogger<NuGetV2Feed>()),
                        new PackageValidationAuditor(cloudStorageAccount, containerName, _loggerFactory),
                        galleryBaseAddress);
                    break;
                }

                return true;
            }
            catch (Exception e)
            {
                if (_logger != null)
                {
                    _logger.LogError(TraceEvent.CommandLineProcessingFailed, e, "Exception occurred while processing command line arguments");
                }
                else
                {
                    Trace.TraceError("Exception occurred while processing command line arguments: {0}", e);
                }

                PrintUsage();
            }

            return false;
        }

        private static void DisableTrace()
        {
            int i = 0;
            while (i < Trace.Listeners.Count)
            {
                if (Trace.Listeners[i] is JobTraceListener)
                {
                    Trace.Listeners.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }
        }

        public async override Task<bool> Run()
        {
            using (_logger.BeginScope("Processing action {Action} scope id: {RunTraceId}", _command.Action, Guid.NewGuid()))
            {
                try
                {
                    return await _command.Run();
                }
                catch (Exception e)
                {
                    _logger.LogError(TraceEvent.HelperFailed, e, "Failed to run action");
                }
            }

            return false;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: {0} " +
                    $"-{JobArgumentNames.VaultName} <KeyVault name> " +
                    $"-{JobArgumentNames.ClientId} <KeyVault clientId> " +
                    $"-{JobArgumentNames.CertificateThumbprint} <KeyVault certificate thumbprint> " +
                    $"-{JobArgumentNames.LogsAzureStorageConnectionString} <azure logs blob storage connection string> " +
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
        }

        private static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value);
        }
    }
}
