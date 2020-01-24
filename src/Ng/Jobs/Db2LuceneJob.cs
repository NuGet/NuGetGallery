// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Indexing;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;

namespace Ng.Jobs
{
    public class Db2LuceneJob : NgJob
    {
        private string _connectionString;
        private string _path;
        private string _source;
        private Uri _catalogIndexUrl;

        public Db2LuceneJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng db2lucene "
                   + $"-{Arguments.ConnectionString} <connectionString> "
                   + $"-{Arguments.Source} <catalogSource>"
                   + $"-{Arguments.Path} <folder> "
                   + $"[-{Arguments.Verbose} true|false]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            _source = arguments.GetOrThrow<string>(Arguments.Source);
            _path = arguments.GetOrThrow<string>(Arguments.Path);

            _catalogIndexUrl = new Uri(_source);
        }

        protected override Task RunInternalAsync(CancellationToken cancellationToken)
        {
            Sql2Lucene.Export(_connectionString, _catalogIndexUrl, _path, LoggerFactory);

            return Task.FromResult(false);
        }
    }
}
