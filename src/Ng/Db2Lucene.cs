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
        public static void Run(IDictionary<string, string> arguments, CancellationToken cancellationToken, ILoggerFactory loggerFactory)
        {
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
            Console.WriteLine("Usage: ng db2lucene "
                + $"-{Constants.ConnectionString} <connectionString> "
                + $"-{Constants.Path} <folder> "
                + $"[-{Constants.Verbose} true|false]");
        }
    }
}
