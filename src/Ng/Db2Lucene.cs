// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Indexing;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Ng
{
    internal class Db2Lucene
    {
        public static void Run(string[] args, CancellationToken cancellationToken, ILoggerFactory loggerFactory)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            var connectionString = CommandHelpers.GetConnectionString(arguments);
            if (connectionString == null)
            {
                PrintUsage();
                return;
            }

            string path = CommandHelpers.GetPath(arguments);
            if (path == null)
            {
                PrintUsage();
                return;
            }

            Sql2Lucene.Export(connectionString, path, loggerFactory);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ng db2lucene -connectionString <connectionString> -path <folder> [-verbose true|false]");
        }
    }
}
