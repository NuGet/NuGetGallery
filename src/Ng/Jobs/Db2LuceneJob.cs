// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Indexing;
using NuGet.Services.Configuration;

namespace Ng.Jobs
{
    public class Db2LuceneJob : NgJob
    {
        private string _connectionString;
        private string _path;

        public Db2LuceneJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng db2lucene "
                   + $"-{Arguments.ConnectionString} <connectionString> "
                   + $"-{Arguments.Path} <folder> "
                   + $"[-{Arguments.Verbose} true|false]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            _path = arguments.GetOrThrow<string>(Arguments.Path);
        }
        
        protected override Task RunInternal(CancellationToken cancellationToken)
        {
            Sql2Lucene.Export(_connectionString, _path, LoggerFactory);

            return Task.FromResult(false);
        }
    }
}
