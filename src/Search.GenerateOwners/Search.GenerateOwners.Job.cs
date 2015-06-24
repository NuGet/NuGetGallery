// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Search.GenerateOwners
{
    internal class Job : JobBase
    {
        SqlExportArguments _args;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            _args = new SqlExportArguments(jobArgsDictionary, "ng-search-data", "owners.json");
            return true;
        }

        public override Task<bool> Run()
        {
            string sql = JobHelper.LoadResource(Assembly.GetExecutingAssembly(), "Scripts.Owners.sql");
            return JobHelper.RunSqlExport(_args, sql, "UserName", "Id");
        }
    }
}
