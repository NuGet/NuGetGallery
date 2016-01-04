// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Indexing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    class Db2Lucene
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng db2lucene -connectionString <connectionString> -path <folder> [-verbose true|false]");
        }

        public static void Run(string[] args, CancellationToken cancellationToken)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null || arguments.Count == 0)
            {
                PrintUsage();
                return;
            }

            string connectionString = CommandHelpers.GetConnectionString(arguments);
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

            bool verbose = CommandHelpers.GetVerbose(arguments);

            TextWriter log = verbose ? Console.Out : new StringWriter();

            Sql2Lucene.Export(connectionString, path, log);
        }
    }
}
