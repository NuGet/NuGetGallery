// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;

namespace Ng
{
    public class Program
    {
        private static ILogger _logger;

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng [package2catalog|feed2catalog|catalog2registration|catalog2lucene|catalog2dnx|frameworkcompatibility|copylucene|checklucene|clearlucene|db2lucene|lightning] "
                + $"[-{Constants.VaultName} <keyvault-name> "
                        + $"-{Constants.ClientId} <keyvault-client-id> "
                        + $"-{Constants.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                        + $"[-{Constants.ValidateCertificate} true|false]]");
        }

        public static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            Action printToolUsage = PrintUsage;

            try
            {
                // Get arguments
                var arguments = CommandHelpers.GetArguments(args, 0);

                // Configure ApplicationInsights
                ApplicationInsights.Initialize(CommandHelpers.GetApplicationInsightsInstrumentationKey(arguments));

                // Create an ILoggerFactory
                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: true);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);

                // Create a logger that is scoped to this class (only)
                _logger = loggerFactory.CreateLogger<Program>();

                var cancellationTokenSource = new CancellationTokenSource();
                if (args.Length == 0)
                {
                    throw new ArgumentException("Missing tool specification");
                }

                switch (args[0])
                {
                    case "package2catalog":
                        printToolUsage = Feed2Catalog.PackagePrintUsage;
                        var packageToCatalog = new Feed2Catalog(loggerFactory);
                        packageToCatalog.Package(arguments, cancellationTokenSource.Token);
                        break;
                    case "feed2catalog":
                        printToolUsage = Feed2Catalog.PrintUsage;
                        var feedToCatalog = new Feed2Catalog(loggerFactory);
                        feedToCatalog.Run(arguments, cancellationTokenSource.Token);
                        break;
                    case "catalog2registration":
                        printToolUsage = Catalog2Registration.PrintUsage;
                        var catalog2Registration = new Catalog2Registration(loggerFactory);
                        catalog2Registration.Run(arguments, cancellationTokenSource.Token);
                        break;
                    case "catalog2lucene":
                        printToolUsage = Catalog2Lucene.PrintUsage;
                        Catalog2Lucene.Run(arguments, cancellationTokenSource.Token);
                        break;
                    case "catalog2dnx":
                        printToolUsage = Catalog2Dnx.PrintUsage;
                        var catalogToDnx = new Catalog2Dnx(loggerFactory);
                        catalogToDnx.Run(arguments, cancellationTokenSource.Token);
                        break;
                    case "copylucene":
                        printToolUsage = CopyLucene.PrintUsage;
                        CopyLucene.Run(arguments);
                        break;
                    case "checklucene":
                        printToolUsage = CheckLucene.PrintUsage;
                        CheckLucene.Run(arguments);
                        break;
                    case "clearlucene":
                        printToolUsage = ResetLucene.PrintUsage;
                        ResetLucene.Run(arguments);
                        break;
                    case "db2lucene":
                        printToolUsage = Db2Lucene.PrintUsage;
                        Db2Lucene.Run(arguments, cancellationTokenSource.Token, loggerFactory);
                        break;
                    case "lightning":
                        printToolUsage = Lightning.PrintUsage;
                        Lightning.Run(arguments, cancellationTokenSource.Token);
                        break;
                    default:
                        printToolUsage();
                        break;
                }
            }
            catch (ArgumentException ae)
            {
                var message = "A required argument was not found or was malformed/invalid.";
                if (_logger != null)
                {
                    _logger.LogError(message, ae);
                }
                else
                {
                    Console.Error.WriteLine(message, ae);
                }

                printToolUsage();
            }
            catch (Exception e)
            {
                var message = "A critical exception occured in ng.exe!";
                if (_logger != null)
                {
                    _logger.LogCritical(message, e);
                }
                else
                {
                    Console.Error.WriteLine(message, e);
                }
            }

            Trace.Close();
        }
    }
}
