// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NuGet.Services;

namespace Ng
{
    public class Program
    {
        private static ILogger _logger;

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ng [package2catalog|feed2catalog|catalog2registration|catalog2lucene|catalog2dnx|frameworkcompatibility|copylucene|checklucene|clearlucene|db2lucene|lightning]");
        }

        public static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            // create an ILoggerFactory
            var arguments = CommandHelpers.GetArguments(args, 0);
            var elasticsearchEndpoint = CommandHelpers.GetElasticsearchEndpoint(arguments);
            var elasticsearchUsername = CommandHelpers.GetElasticsearchUsername(arguments);
            var elasticsearchPassword = CommandHelpers.GetElasticsearchPassword(arguments);
            var loggerFactory = Logging.CreateLoggerFactory(null, elasticsearchEndpoint, elasticsearchUsername, elasticsearchPassword);

            // create a logger that is scoped to this class (only)
            _logger = loggerFactory.CreateLogger<Program>();

            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return;
                }

                switch (args[0])
                {
                    case "package2catalog":
                        var packageToCatalog = new Feed2Catalog(loggerFactory);
                        packageToCatalog.Package(args, cancellationTokenSource.Token);
                        break;
                    case "feed2catalog":
                        var feedToCatalog = new Feed2Catalog(loggerFactory);
                        feedToCatalog.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2registration":
                        var catalog2Registration = new Catalog2Registration(loggerFactory);
                        catalog2Registration.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2lucene":
                        Catalog2Lucene.Run(args, cancellationTokenSource.Token);
                        break;
                    case "catalog2dnx":
                        var catalogToDnx = new Catalog2Dnx(loggerFactory);
                        catalogToDnx.Run(args, cancellationTokenSource.Token);
                        break;
                    case "copylucene":
                        CopyLucene.Run(args);
                        break;
                    case "checklucene":
                        CheckLucene.Run(args);
                        break;
                    case "clearlucene":
                        ResetLucene.Run(args);
                        break;
                    case "db2lucene":
                        Db2Lucene.Run(args, cancellationTokenSource.Token, loggerFactory);
                        break;
                    case "lightning":
                        Lightning.Run(args, cancellationTokenSource.Token);
                        break;
                    default:
                        PrintUsage();
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical("A critical exception occured in ng.exe!", e);
            }

            Trace.Close();
        }
    }
}
