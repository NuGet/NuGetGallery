// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Infrastructure
{
    public interface IDbExecutorFactory
    {
        IDbExecutor OpenConnection(string connectionString);
    }
}
