// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Infrastructure
{
    public class SqlDbExecutorFactory : IDbExecutorFactory
    {
        public IDbExecutor OpenConnection(string connectionString)
        {
            return new SqlExecutor(new SqlConnection(connectionString));
        }
    }
}
