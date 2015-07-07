// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Search.GenerateCuratedFeed
{
    internal class Job : JobBase
    {
        SqlExportArguments _args;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            _args = new SqlExportArguments(jobArgsDictionary, "ng-search-data", "curatedfeeds.json");
            return true;
        }

        public override Task<bool> Run()
        {
            string sql = JobHelper.LoadResource(Assembly.GetExecutingAssembly(), "Scripts.CuratedFeed.sql");
            return JobHelper.RunSqlExport(_args, sql, "FeedName", "Id");
        }
    }
}

